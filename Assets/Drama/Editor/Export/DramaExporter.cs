using System.Collections.Generic;
using System.IO;
using System.Linq;
using Drama.Runtime;
using Unity.GraphToolkit.Editor;
using UnityEditor;
using UnityEngine;

namespace Drama.Editor.Export
{
    /// <summary>一次导出的结果。</summary>
    internal sealed class DramaExportResult
    {
        internal string GraphPath;
        internal string OutputPath;
        internal bool Success;
        internal int ActionCount;
        internal List<string> Errors = new List<string>();
        internal List<string> Warnings = new List<string>();

        internal string Summary =>
            Success ? $"{ActionCount} 条指令"
                    : (Errors.Count > 0 ? Errors[0] : "失败");
    }

    /// <summary>
    /// 把 DramaGraph（.agv）转成运行时 DramaScript（.asset）。
    ///
    /// 流程：
    ///   ① 找入口（StartDramaNode）
    ///   ② 沿【流程端口】线性遍历，Context 展开成多条指令
    ///   ③ 遍历结束后回填节点之间的 Next
    ///   ④ 写出 ScriptableObject
    /// </summary>
    internal static class DramaExporter
    {
        internal const string DefaultOutputFolder = "Assets/Drama/Export";

        // ------------------------------------------------------------ 查找图

        /// <summary>扫描工程里所有 .agv。</summary>
        internal static List<string> FindAllGraphPaths()
        {
            var ext = "." + DramaGraph.AssetExtension;
            return AssetDatabase.GetAllAssetPaths()
                .Where(p => p.StartsWith("Assets/") && p.EndsWith(ext, System.StringComparison.OrdinalIgnoreCase))
                .OrderBy(p => p)
                .ToList();
        }

        // ------------------------------------------------------------ 导出

        internal static DramaExportResult Export(string graphPath, string outputFolder, bool writeAsset = true)
        {
            var result = new DramaExportResult { GraphPath = graphPath };

            var graph = GraphDatabase.LoadGraph<DramaGraph>(graphPath);
            if (graph == null)
            {
                result.Errors.Add($"加载失败：{graphPath}");
                return result;
            }

            var ctx = new DramaExportContext();

            // ① 入口
            var entry = FindEntryNode(graph, ctx);
            if (entry == null)
            {
                result.Errors.AddRange(ctx.Errors);
                return result;
            }

            // ② 线性遍历
            var ordered = Walk(graph, entry, ctx);

            // ③ 回填节点之间的 Next
            for (int i = 0; i + 1 < ordered.Count; i++)
                ctx.Link(ordered[i], ordered[i + 1]);

            result.Errors.AddRange(ctx.Errors);
            result.Warnings.AddRange(ctx.Warnings);
            result.ActionCount = ctx.Actions.Count;

            if (ctx.HasError) return result;

            if (ctx.Actions.Count == 0)
            {
                result.Errors.Add("没有产出任何指令 —— 检查是否所有节点都实现了 IDramaExportNode");
                return result;
            }

            // ④ 写出
            var script = ScriptableObject.CreateInstance<DramaScript>();
            script.FormatVersion = DramaScript.CurrentFormatVersion;
            script.DramaId = ReadDramaId(entry, ctx);
            script.SourceGraph = graphPath;
            script.Actions = ctx.Actions.ToList();
            script.EntryIndex = ordered.Count > 0 && ctx.TryGetFirstAction(ordered[0], out var e) ? e : -1;

            if (!writeAsset)
            {
                result.Success = true;
                Object.DestroyImmediate(script);
                return result;
            }

            var name = Path.GetFileNameWithoutExtension(graphPath);
            var outPath = $"{outputFolder.TrimEnd('/')}/{name}.asset";
            EnsureFolder(outputFolder);

            var existing = AssetDatabase.LoadAssetAtPath<DramaScript>(outPath);
            if (existing != null)
            {
                // 覆盖已有资产而不是删了重建 —— 保住 GUID，别的场景/预制体的引用不会断
                EditorUtility.CopySerialized(script, existing);
                EditorUtility.SetDirty(existing);
                Object.DestroyImmediate(script);
            }
            else
            {
                AssetDatabase.CreateAsset(script, outPath);
            }

            AssetDatabase.SaveAssets();

            result.OutputPath = outPath;
            result.Success = true;
            return result;
        }

        // ------------------------------------------------------------ 遍历

        static INode FindEntryNode(DramaGraph graph, DramaExportContext ctx)
        {
            var starts = graph.GetNodes().OfType<StartDramaNode>().ToList();

            if (starts.Count == 0) { ctx.Error("找不到「进入」节点（StartDramaNode）"); return null; }
            if (starts.Count > 1) { ctx.Error($"存在 {starts.Count} 个「进入」节点，只能有一个"); return null; }

            return starts[0];
        }

        static long ReadDramaId(INode entry, DramaExportContext ctx)
        {
            var port = entry.GetInputPortByName(StartDramaNode.DramaID);
            return ctx.Eval<long>(port, -1);
        }

        /// <summary>
        /// 从入口沿流程端口走一遍，顺带调用每个节点的 Export。
        /// 返回按执行顺序排列的节点列表。
        /// </summary>
        static List<INode> Walk(DramaGraph graph, INode entry, DramaExportContext ctx)
        {
            var ordered = new List<INode>();
            var visited = new HashSet<Hash128>();
            var node = entry;

            while (node != null)
            {
                if (!visited.Add(node.ID))
                {
                    ctx.Error("流程出现环，遍历中止", node);
                    break;
                }

                // 入口节点本身不产出指令
                if (!(node is StartDramaNode))
                {
                    ctx.BeginNode(node);
                    ExportNode(node, ctx);

                    // 只有真的产出了指令才计入链条，否则 Link 会把链接错位
                    if (ctx.TryGetFirstAction(node, out _))
                        ordered.Add(node);
                }

                node = NextNode(node, ctx);
            }

            return ordered;
        }

        static void ExportNode(INode node, DramaExportContext ctx)
        {
            var exported = false;

            if (node is IDramaExportNode self)
            {
                self.Export(ctx);
                exported = true;
            }

            // 容器节点：把内部的 Block 依次展开
            if (node is ContextNode context)
            {
                foreach (var block in context.BlockNodes)
                {
                    if (block is IDramaExportNode blockExport)
                    {
                        blockExport.Export(ctx);
                        exported = true;
                    }
                    else
                    {
                        ctx.Warn($"块「{block.GetType().Name}」还没实现 IDramaExportNode，已跳过", node);
                    }
                }
            }

            if (!exported)
                ctx.Warn($"节点「{node.GetType().Name}」还没实现 IDramaExportNode，已跳过", node);
        }

        /// <summary>
        /// 找下一个节点。
        /// 流程端口按【类型是 Untyped】识别，不按名字 ——
        /// 因为各节点的流程端口命名并不统一（DramaProtName / Output / DramaProtName0…）。
        /// </summary>
        static INode NextNode(INode node, DramaExportContext ctx)
        {
            var flowOuts = node.GetOutputPorts()
                .Where(p => p.DataType == typeof(Untyped))
                .ToList();

            if (flowOuts.Count == 0) return null;

            var connected = flowOuts.Where(p => p.IsConnected).ToList();
            if (connected.Count == 0) return null;

            if (connected.Count > 1)
            {
                // 多出口（分支）暂不支持，先按第一条走并明确报出来，避免静默丢内容
                ctx.Error($"节点有 {connected.Count} 个已连接的流程出口（分支），导出器暂不支持分支", node);
                return null;
            }

            var buf = new List<IPort>();
            connected[0].GetConnectedPorts(buf);
            return buf.Count > 0 ? buf[0].GetNode() : null;
        }

        // ------------------------------------------------------------ 工具

        static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;

            var parts = folder.Split('/');
            var cur = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                var next = cur + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(cur, parts[i]);
                cur = next;
            }
        }
    }
}

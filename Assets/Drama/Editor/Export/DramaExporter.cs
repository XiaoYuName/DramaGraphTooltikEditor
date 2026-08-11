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
        internal int ParallelForkCount;
        internal int JoinCount;
        internal List<string> Errors = new List<string>();
        internal List<string> Warnings = new List<string>();
    }

    /// <summary>
    /// DramaGraph（.agv）→ 运行时 DramaScript（.asset）。
    ///
    /// <b>并行 / 串行语义</b>（整个导出器的核心）：
    ///   一个流程输出端口连 1 条线 → 串行，下一条要等本条完全执行完
    ///   一个流程输出端口连 N 条线 → 并行，N 条同时开始
    /// 落到数据上就是 <see cref="DramaAction.Next"/> 这个数组的长度。
    ///
    /// 多条支线汇到同一个节点时，该指令的 <see cref="DramaAction.InboundCount"/> &gt; 1，
    /// 运行时应当等所有入边到齐后才执行一次。
    /// </summary>
    internal static class DramaExporter
    {
        internal const string DefaultOutputFolder = "Assets/Drama/Export";

        // ------------------------------------------------------------ 查找图

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

            // ② 收集所有可达节点并逐个翻译
            var reachable = CollectReachable(entry, ctx);
            foreach (var node in reachable)
            {
                ctx.BeginNode(node);
                if (!DramaNodeExporters.TryExport(node, ctx))
                    ctx.Warn($"节点「{node.GetType().Name}」还没有导出映射，已跳过", node);
            }

            // ③ 连线：决定串行还是并行
            foreach (var node in reachable)
                LinkNode(node, ctx);

            ctx.ComputeInbound();

            result.Errors.AddRange(ctx.Errors);
            result.Warnings.AddRange(ctx.Warnings);
            result.ActionCount = ctx.Actions.Count;
            result.ParallelForkCount = ctx.Actions.Count(a => a.IsParallelFork);
            result.JoinCount = ctx.Actions.Count(a => a.IsJoin);

            if (ctx.HasError) return result;

            if (ctx.Actions.Count == 0)
            {
                result.Errors.Add("没有产出任何指令");
                return result;
            }

            // ④ 写出
            var entryIndices = ResolveTargets(entry, ctx, new HashSet<Hash128>());
            if (entryIndices.Count == 0)
            {
                result.Errors.Add("入口节点后面没有接任何能导出的节点");
                return result;
            }

            var script = ScriptableObject.CreateInstance<DramaScript>();
            script.FormatVersion = DramaScript.CurrentFormatVersion;
            script.DramaId = ctx.Eval<long>(entry.GetInputPortByName(StartDramaNode.DramaID), -1);
            script.SourceGraph = graphPath;
            script.Actions = ctx.Actions;
            script.EntryIndex = entryIndices[0];

            if (entryIndices.Count > 1)
                ctx.Warn("入口直接连了多条并行线；运行时会从第一条开始，其余靠 Next 分发");

            if (!writeAsset)
            {
                result.Success = true;
                Object.DestroyImmediate(script);
                return result;
            }

            EnsureFolder(outputFolder);
            var outPath = $"{outputFolder.TrimEnd('/')}/{Path.GetFileNameWithoutExtension(graphPath)}.asset";

            // 主资产的对象名必须和文件名一致，否则 Inspector 每次都弹
            // "The main object name '' should match the asset filename"。
            // CreateInstance 出来的名字是空串，而下面覆盖分支的 CopySerialized
            // 连 m_Name 一起拷 —— 所以哪怕手动 Fix 过一次，下次导出又被空名字盖回去。
            script.name = Path.GetFileNameWithoutExtension(outPath);

            var existing = AssetDatabase.LoadAssetAtPath<DramaScript>(outPath);
            if (existing != null)
            {
                // 覆盖而不是删了重建 —— 保住 GUID，别处对这个资产的引用不会断
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

        /// <summary>从入口出发，广度优先收集所有沿流程端口可达的节点（每个只收一次）。</summary>
        static List<INode> CollectReachable(INode entry, DramaExportContext ctx)
        {
            var ordered = new List<INode>();
            var seen = new HashSet<Hash128> { entry.ID };
            var queue = new Queue<INode>();

            ordered.Add(entry);
            queue.Enqueue(entry);

            while (queue.Count > 0)
            {
                var node = queue.Dequeue();
                foreach (var next in DownstreamNodes(node))
                {
                    if (!seen.Add(next.ID)) continue;   // 汇合点会被多条路径指到，只收一次
                    ordered.Add(next);
                    queue.Enqueue(next);
                }
            }

            return ordered;
        }

        /// <summary>
        /// 本节点所有流程出口连到的下游节点（按端口顺序、端口内连接顺序）。
        /// 流程端口按【类型是 Untyped】识别，不按名字 ——
        /// 各节点的流程端口命名并不统一（DramaProtName / Output / DramaProtName0…）。
        /// </summary>
        static IEnumerable<INode> DownstreamNodes(INode node)
        {
            foreach (var port in FlowOutputs(node))
            {
                if (!port.IsConnected) continue;
                var buf = new List<IPort>();
                port.GetConnectedPorts(buf);
                foreach (var p in buf)
                {
                    var n = p.GetNode();
                    if (n != null) yield return n;
                }
            }
        }

        static List<IPort> FlowOutputs(INode node) =>
            node.GetOutputPorts().Where(p => p.DataType == typeof(Untyped)).ToList();

        // ------------------------------------------------------------ 连线

        static void LinkNode(INode node, DramaExportContext ctx)
        {
            // 分支节点：每个出口端口对应一个选项，不是并行
            if (node is ChangeDramaNode choiceNode)
            {
                LinkChoice(choiceNode, ctx);
                return;
            }

            // 普通节点：把所有流程出口的所有连接汇总
            //   1 个目标 → 串行；多个目标 → 并行
            var targets = new List<int>();
            foreach (var next in DownstreamNodes(node))
                foreach (var idx in ResolveTargets(next, ctx, new HashSet<Hash128>()))
                    if (!targets.Contains(idx))
                        targets.Add(idx);

            ctx.SetNext(node, targets);
        }

        static void LinkChoice(ChangeDramaNode node, DramaExportContext ctx)
        {
            if (!ctx.TryGetFirstAction(node, out var idx)) return;
            if (!(ctx.Actions[idx] is ChoiceAction choice)) return;

            for (int i = 0; i < choice.Options.Length; i++)
            {
                var port = node.GetOutputPortByName(DramaNode.NodeProtName + i);
                if (port == null || !port.IsConnected)
                {
                    ctx.Warn($"分支 {i} 的出口没有接任何节点", node);
                    continue;
                }

                var buf = new List<IPort>();
                port.GetConnectedPorts(buf);
                if (buf.Count == 0) continue;

                var branch = ResolveTargets(buf[0].GetNode(), ctx, new HashSet<Hash128>());
                choice.Options[i].Next = branch.Count > 0 ? branch[0] : -1;
            }

            // 选项自己带跳转目标，ChoiceAction 本身不再有顺序后继
            ctx.SetNext(node, System.Array.Empty<int>());
        }

        /// <summary>
        /// 求一个节点对应的"入口指令下标"。
        /// 如果这个节点没产出指令（比如还没写映射），就顺着它的下游继续找，
        /// 这样链条不会被断掉。
        /// </summary>
        static List<int> ResolveTargets(INode node, DramaExportContext ctx, HashSet<Hash128> guard)
        {
            var result = new List<int>();
            if (node == null || !guard.Add(node.ID)) return result;

            if (ctx.TryGetFirstAction(node, out var first))
            {
                result.Add(first);
                return result;
            }

            foreach (var next in DownstreamNodes(node))
                foreach (var idx in ResolveTargets(next, ctx, guard))
                    if (!result.Contains(idx))
                        result.Add(idx);

            return result;
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

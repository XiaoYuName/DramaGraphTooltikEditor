using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEngine;

namespace Drama.Editor.Export
{
    /// <summary>
    /// 剧情导出器窗口。把工程里的 .agv 剧情图转成运行时用的 DramaScript 资产。
    /// 菜单：Tools / Drama / 剧情导出器
    /// </summary>
    internal class DramaExportWindow : OdinEditorWindow
    {
        [MenuItem("Tools/Drama/剧情导出器", false, 10)]
        static void Open()
        {
            var win = GetWindow<DramaExportWindow>();
            win.titleContent = new GUIContent("剧情导出器");
            win.minSize = new Vector2(720f, 420f);
            win.Refresh();
            win.Show();
        }

        // ==================================================== 输出设置

        [Title("输出", TitleAlignment = TitleAlignments.Left)]
        [FolderPath(RequireExistingPath = false)]
        [LabelText("输出目录")]
        [InfoBox("导出的 .asset 会放在这里。这些资产可以整个目录拷到别的 Unity 工程使用 —— " +
                 "前提是对方工程也有同一份 Drama.Runtime 程序集（建议做成 UPM 包共享，避免 GUID 不一致）。",
                 InfoMessageType.Info)]
        public string OutputFolder = DramaExporter.DefaultOutputFolder;

        [LabelText("导出后在 Project 里选中产物")]
        public bool PingAfterExport = true;

        // ==================================================== 剧本列表

        [Title("剧本")]
        [TableList(AlwaysExpanded = true, DrawScrollView = true, MinScrollViewHeight = 160, HideToolbar = true)]
        [LabelText("剧情图")]
        public List<GraphEntry> Graphs = new List<GraphEntry>();

        [ShowInInspector, ReadOnly, LabelText("已选 / 总数"), PropertyOrder(9)]
        string SelectionInfo => $"{Graphs.Count(g => g.Selected)} / {Graphs.Count}";

        // ==================================================== 操作按钮

        [PropertyOrder(10)]
        [ButtonGroup("ops")]
        [Button("刷新列表", ButtonSizes.Medium)]
        void RefreshButton() => Refresh();

        [PropertyOrder(10)]
        [ButtonGroup("ops")]
        [Button("全选", ButtonSizes.Medium)]
        void SelectAll() => Graphs.ForEach(g => g.Selected = true);

        [PropertyOrder(10)]
        [ButtonGroup("ops")]
        [Button("全不选", ButtonSizes.Medium)]
        void SelectNone() => Graphs.ForEach(g => g.Selected = false);

        [PropertyOrder(11)]
        [ButtonGroup("run")]
        [Button("校验选中（不写文件）", ButtonSizes.Large)]
        [EnableIf(nameof(HasSelection))]
        void ValidateSelected() => Run(writeAsset: false);

        [PropertyOrder(11)]
        [ButtonGroup("run")]
        [Button("导出选中", ButtonSizes.Large)]
        [GUIColor(0.5f, 0.9f, 0.6f)]
        [EnableIf(nameof(HasSelection))]
        void ExportSelected() => Run(writeAsset: true);

        bool HasSelection => Graphs.Any(g => g.Selected);

        // ==================================================== 日志

        [Title("结果")]
        [PropertyOrder(20)]
        [ShowInInspector, ReadOnly, HideLabel]
        [MultiLineProperty(12)]
        public string Log = "（还没有执行过导出）";

        // ==================================================== 实现

        internal void Refresh()
        {
            var paths = DramaExporter.FindAllGraphPaths();
            var old = Graphs.ToDictionary(g => g.Path, g => g);

            Graphs = paths.Select(p =>
            {
                if (old.TryGetValue(p, out var exist)) return exist;
                return new GraphEntry
                {
                    Selected = true,
                    Name = System.IO.Path.GetFileNameWithoutExtension(p),
                    Path = p,
                };
            }).ToList();

            Log = Graphs.Count == 0
                ? "工程里没有找到 .agv 剧情图。"
                : $"找到 {Graphs.Count} 个剧情图。";
        }

        void Run(bool writeAsset)
        {
            var targets = Graphs.Where(g => g.Selected).ToList();
            if (targets.Count == 0) return;

            var sb = new StringBuilder();
            sb.AppendLine(writeAsset ? "=== 导出 ===" : "=== 校验（不写文件）===");

            var ok = 0;
            var failed = 0;
            var lastAsset = string.Empty;

            try
            {
                if (writeAsset) AssetDatabase.StartAssetEditing();

                for (int i = 0; i < targets.Count; i++)
                {
                    var entry = targets[i];

                    EditorUtility.DisplayProgressBar(
                        writeAsset ? "导出剧情" : "校验剧情",
                        entry.Name,
                        (float)i / targets.Count);

                    DramaExportResult r;
                    try
                    {
                        r = DramaExporter.Export(entry.Path, OutputFolder, writeAsset);
                    }
                    catch (Exception e)
                    {
                        r = new DramaExportResult { GraphPath = entry.Path };
                        r.Errors.Add($"异常：{e.Message}");
                        Debug.LogException(e);
                    }

                    entry.Apply(r, writeAsset);

                    if (r.Success)
                    {
                        ok++;
                        if (!string.IsNullOrEmpty(r.OutputPath)) lastAsset = r.OutputPath;
                        sb.AppendLine($"  ✔ {entry.Name}   {r.ActionCount} 条指令" +
                                      (string.IsNullOrEmpty(r.OutputPath) ? "" : $"   → {r.OutputPath}"));
                    }
                    else
                    {
                        failed++;
                        sb.AppendLine($"  ✘ {entry.Name}");
                    }

                    foreach (var w in r.Warnings) sb.AppendLine($"       ⚠ {w}");
                    foreach (var e in r.Errors) sb.AppendLine($"       ✘ {e}");
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                if (writeAsset)
                {
                    AssetDatabase.StopAssetEditing();
                    AssetDatabase.SaveAssets();
                    AssetDatabase.Refresh();
                }
            }

            sb.AppendLine();
            sb.AppendLine($"完成：成功 {ok}，失败 {failed}");
            Log = sb.ToString();

            if (writeAsset && PingAfterExport && !string.IsNullOrEmpty(lastAsset))
            {
                var obj = AssetDatabase.LoadMainAssetAtPath(lastAsset);
                if (obj != null) EditorGUIUtility.PingObject(obj);
            }
        }

        // ==================================================== 表格行

        [Serializable]
        internal class GraphEntry
        {
            [TableColumnWidth(36, Resizable = false)]
            [LabelText("  ")]
            public bool Selected = true;

            [TableColumnWidth(170)]
            [ReadOnly, LabelText("剧本")]
            public string Name;

            [TableColumnWidth(100, Resizable = false)]
            [ReadOnly, LabelText("状态")]
            public string Status = "—";

            [ReadOnly, LabelText("说明")]
            public string Detail = string.Empty;

            [HideInTables]
            public string Path;

            [TableColumnWidth(60, Resizable = false)]
            [Button("定位")]
            void Locate()
            {
                var obj = AssetDatabase.LoadMainAssetAtPath(Path);
                if (obj != null) { Selection.activeObject = obj; EditorGUIUtility.PingObject(obj); }
            }

            internal void Apply(DramaExportResult r, bool wroteAsset)
            {
                if (!r.Success)
                {
                    Status = "失败";
                    Detail = r.Errors.Count > 0 ? r.Errors[0] : "未知错误";
                    return;
                }

                Status = r.Warnings.Count > 0 ? "有警告" : (wroteAsset ? "已导出" : "校验通过");
                Detail = $"{r.ActionCount} 条指令" +
                         (r.Warnings.Count > 0 ? $"，{r.Warnings.Count} 条警告" : "");
            }
        }
    }
}

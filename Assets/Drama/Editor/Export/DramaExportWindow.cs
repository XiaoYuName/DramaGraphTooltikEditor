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

        // ==================================================== 宿主工程

        [Title("同步到宿主工程", TitleAlignment = TitleAlignments.Left)]
        [ShowInInspector]
        [FolderPath(AbsolutePath = true, RequireExistingPath = false)]
        [LabelText("宿主工程目录")]
        [InfoBox("填了就在导出之后额外往这里拷一份，留空则不同步。\n" +
                 "填的是<b>目标文件夹</b>的绝对路径，要在宿主工程的 Assets 下面，" +
                 "例如 D:/YourGame/Assets/AddressableAssets/Remote/Configs/Drama。",
                 InfoMessageType.Info)]
        [PropertyTooltip("记在 EditorPrefs 里，换一次就一直用这个")]
        public string HostFolder
        {
            get => EditorPrefs.GetString(HostFolderPrefKey, string.Empty);
            set => EditorPrefs.SetString(HostFolderPrefKey, value ?? string.Empty);
        }

        /// <summary>按工程区分：同一台机器上可能开着好几个编辑器工程，各自的宿主不一样。</summary>
        static string HostFolderPrefKey =>
            "Drama.Export.HostFolder." + Application.dataPath.GetHashCode();

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

        [HideInInspector]
        public string Log = "（还没有执行过导出）";

        /// <summary>只显示带 ⚠ / ✘ 的行，剧本多的时候用来直接找问题。</summary>
        bool _onlyProblems;

        Vector2 _logScroll;

        // ---- 显示缓存 ----
        // 日志可以有几千行，CalcSize/CalcHeight 每帧算一遍会卡；
        // 只在文本、过滤开关或视图宽度变了的时候重算。
        static GUIStyle _logStyle;
        string _viewCacheKey;
        float _measuredForWidth;
        float _contentWidth;
        readonly List<string> _chunks = new List<string>();
        readonly List<float> _chunkHeights = new List<float>();

        /// <summary>
        /// 一个 IMGUI 文本控件的字符数上不去太多（文本网格顶点数有上限，超了整段直接不画），
        /// 所以按行切成若干块分别画。
        /// </summary>
        const int ChunkChars = 6000;

        [Title("结果")]
        [PropertyOrder(20)]
        [OnInspectorGUI]
        void DrawLog()
        {
            if (_logStyle == null)
                _logStyle = new GUIStyle(EditorStyles.label)
                {
                    wordWrap = false,       // 长路径宁可横向滚动，也别折行搅乱缩进
                    richText = false,
                    padding = new RectOffset(4, 4, 1, 1),
                };

            using (new EditorGUILayout.HorizontalScope())
            {
                var lines = string.IsNullOrEmpty(Log) ? 0 : Log.Split('\n').Length;
                GUILayout.Label($"{lines} 行", EditorStyles.miniLabel);
                GUILayout.FlexibleSpace();
                _onlyProblems = GUILayout.Toggle(_onlyProblems, "只看问题", EditorStyles.miniButton, GUILayout.Width(64f));
                if (GUILayout.Button("复制", EditorStyles.miniButton, GUILayout.Width(48f)))
                    EditorGUIUtility.systemCopyBuffer = Log;
                if (GUILayout.Button("清空", EditorStyles.miniButton, GUILayout.Width(48f)))
                    Log = string.Empty;
            }

            RebuildViewIfNeeded();

            var viewHeight = Mathf.Clamp(position.height * 0.45f, 160f, 1200f);
            var viewRect = GUILayoutUtility.GetRect(0f, viewHeight, GUILayout.ExpandWidth(true));
            GUI.Box(viewRect, GUIContent.none, EditorStyles.helpBox);

            var inner = new Rect(viewRect.x + 1f, viewRect.y + 1f, viewRect.width - 2f, viewRect.height - 2f);
            MeasureIfNeeded(inner.width - 16f);

            var total = 0f;
            for (int i = 0; i < _chunkHeights.Count; i++) total += _chunkHeights[i];

            _logScroll = GUI.BeginScrollView(inner, _logScroll,
                new Rect(0f, 0f, _contentWidth, total));

            var y = 0f;
            for (int i = 0; i < _chunks.Count; i++)
            {
                var h = _chunkHeights[i];
                // 视野外的块不画：几千行的日志滚起来才不掉帧
                if (y + h >= _logScroll.y && y <= _logScroll.y + inner.height)
                    EditorGUI.SelectableLabel(new Rect(0f, y, _contentWidth, h), _chunks[i], _logStyle);
                y += h;
            }

            GUI.EndScrollView();
        }

        void RebuildViewIfNeeded()
        {
            var key = (_onlyProblems ? "P" : "A") + (Log?.Length ?? 0) + "|" + (Log ?? string.Empty).GetHashCode();
            if (key == _viewCacheKey) return;
            _viewCacheKey = key;

            var text = _onlyProblems ? FilterProblems(Log) : (Log ?? string.Empty);

            _chunks.Clear();
            var sb = new StringBuilder();
            foreach (var line in text.Split('\n'))
            {
                if (sb.Length > 0 && sb.Length + line.Length > ChunkChars)
                {
                    _chunks.Add(sb.ToString().TrimEnd('\n'));
                    sb.Clear();
                }
                sb.Append(line).Append('\n');
            }
            if (sb.Length > 0) _chunks.Add(sb.ToString().TrimEnd('\n'));

            _measuredForWidth = -1f;   // 逼着重新量一遍
        }

        void MeasureIfNeeded(float viewWidth)
        {
            if (Mathf.Approximately(viewWidth, _measuredForWidth)) return;
            _measuredForWidth = viewWidth;

            _contentWidth = Mathf.Max(viewWidth, 64f);
            for (int i = 0; i < _chunks.Count; i++)
                _contentWidth = Mathf.Max(_contentWidth, _logStyle.CalcSize(new GUIContent(_chunks[i])).x + 8f);

            _chunkHeights.Clear();
            for (int i = 0; i < _chunks.Count; i++)
                _chunkHeights.Add(_logStyle.CalcHeight(new GUIContent(_chunks[i]), _contentWidth));
        }

        /// <summary>
        /// 挑出 ⚠ / ✘ 的行。缩进的问题行本身看不出是哪个剧本的，
        /// 所以顺带把它上面那行剧本名带出来。
        /// </summary>
        static string FilterProblems(string log)
        {
            if (string.IsNullOrEmpty(log)) return string.Empty;

            var sb = new StringBuilder();
            string pendingGraph = null;

            foreach (var raw in log.Split('\n'))
            {
                var line = raw.TrimEnd('\r');

                if (line.StartsWith("  ✔", StringComparison.Ordinal))
                {
                    pendingGraph = line;
                    continue;
                }
                if (line.StartsWith("  ✘", StringComparison.Ordinal))
                {
                    sb.AppendLine(line);
                    pendingGraph = null;
                    continue;
                }

                if (line.IndexOf('⚠') < 0 && line.IndexOf('✘') < 0) continue;

                if (pendingGraph != null)
                {
                    sb.AppendLine(pendingGraph);
                    pendingGraph = null;
                }
                sb.AppendLine(line);
            }

            return sb.Length == 0 ? "没有警告或错误。" : sb.ToString();
        }

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
                                      (r.ParallelForkCount > 0 ? $"，{r.ParallelForkCount} 处并行" : "") +
                                      (r.JoinCount > 0 ? $"，{r.JoinCount} 处汇合" : "") +
                                      (string.IsNullOrEmpty(r.OutputPath) ? "" : $"   → {r.OutputPath}"));

                        if (writeAsset && !string.IsNullOrEmpty(r.OutputPath) && !string.IsNullOrEmpty(HostFolder))
                            sb.AppendLine("       " + SyncToHost(r.OutputPath));
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
            _logScroll = Vector2.zero;

            if (writeAsset && PingAfterExport && !string.IsNullOrEmpty(lastAsset))
            {
                var obj = AssetDatabase.LoadMainAssetAtPath(lastAsset);
                if (obj != null) EditorGUIUtility.PingObject(obj);
            }
        }

        /// <summary>
        /// 把刚导出的产物再拷一份到宿主工程，返回一行给日志用的说明。
        ///
        /// <b>只能走文件拷贝</b>：宿主目录在本工程之外，AssetDatabase 够不着。
        ///
        /// <b>.meta 只在目标那边还没有的时候才拷。</b> GUID 一变，宿主的 Addressables
        /// 条目和所有引用这份资产的地方就全断了；目标已经有 .meta 就说明宿主认过这个资产，
        /// 保持它原来的 GUID 才是对的。第一次同步没有 .meta，这时候把本工程的带过去，
        /// 两边 GUID 一致（就是输出目录那条提示说的事）。
        /// </summary>
        string SyncToHost(string assetPath)
        {
            var folder = HostFolder;

            try
            {
                // 拷到自己工程里没有意义，还会多出一份重复资产
                var projectRoot = System.IO.Path.GetFullPath(
                    System.IO.Path.Combine(Application.dataPath, ".."));
                if (System.IO.Path.GetFullPath(folder).StartsWith(projectRoot, StringComparison.OrdinalIgnoreCase))
                    return "⚠ 宿主目录指向的是本工程，已跳过同步";

                System.IO.Directory.CreateDirectory(folder);

                var fileName = System.IO.Path.GetFileName(assetPath);
                var target = System.IO.Path.Combine(folder, fileName);

                System.IO.File.Copy(assetPath, target, overwrite: true);

                var meta = assetPath + ".meta";
                var targetMeta = target + ".meta";
                if (System.IO.File.Exists(meta) && !System.IO.File.Exists(targetMeta))
                    System.IO.File.Copy(meta, targetMeta);

                return $"↳ 已同步到宿主：{target}";
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                return $"⚠ 同步到宿主失败：{e.Message}";
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
                         (r.ParallelForkCount > 0 ? $"，并行 {r.ParallelForkCount}" : "") +
                         (r.JoinCount > 0 ? $"，汇合 {r.JoinCount}" : "") +
                         (r.Warnings.Count > 0 ? $"，警告 {r.Warnings.Count}" : "");
            }
        }
    }
}

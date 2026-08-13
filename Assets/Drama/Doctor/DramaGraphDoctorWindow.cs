using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEngine;

namespace Drama.Doctor
{
    /// <summary>
    /// 剧情图体检工具。诊断「图打开是空的 / 节点少了」这类问题。
    ///
    /// ★ 本工具刻意放在独立的 DramaDoctor 程序集里，<b>不引用 DramaEditor / Drama.Runtime</b>。
    ///   因为「图打开是空的」最常见的原因就是 DramaEditor 编译失败 ——
    ///   如果工具本身也在那个程序集里，出问题时它自己就没了，等于没有工具。
    ///
    /// 原理：.agv 里的节点是按 { class, ns, asm } <b>名字</b>反序列化的（和 [SerializeReference] 一样）。
    ///   工具直接把 .agv 当文本读，抽出所有类型引用，再逐个尝试在当前程序域里解析。
    ///   解析不到的就是打不开的原因 —— 不需要 GraphToolkit 参与，图坏成什么样都能读。
    /// </summary>
    internal class DramaGraphDoctorWindow : OdinEditorWindow
    {
        [MenuItem("Tools/Drama/剧情图体检", false, 30)]
        static void Open()
        {
            var win = GetWindow<DramaGraphDoctorWindow>();
            win.titleContent = new GUIContent("剧情图体检");
            win.minSize = new Vector2(760f, 520f);
            win.Scan();
            win.Show();
        }

        // ==================================================== 顶部警告

        [PropertyOrder(-10)]
        [InfoBox(
            "图打开是空的 / 节点少了的时候，【绝对不要保存那张图】。\n" +
            "一保存就会把解析失败的节点当成\"已删除\"写回文件，数据永久丢失，改好环境也救不回来。\n" +
            "正确做法：关掉图窗口 → 先修编译 → 再打开。",
            InfoMessageType.Error)]
        [ShowInInspector, ReadOnly, HideLabel, DisplayAsString]
        string Warning => "⚠ 打不开就别保存";

        // ==================================================== 环境

        [Title("当前机器的环境")]
        [ShowInInspector, ReadOnly, LabelText("DramaEditor 程序集")]
        [GUIColor(nameof(EditorAsmColor))]
        string EditorAsmState { get; set; } = "-";

        [ShowInInspector, ReadOnly, LabelText("Drama.Runtime 程序集")]
        [GUIColor(nameof(RuntimeAsmColor))]
        string RuntimeAsmState { get; set; } = "-";

        [ShowInInspector, ReadOnly, LabelText("Scripting Define")]
        string DefineState { get; set; } = "-";

        [ShowInInspector, ReadOnly, LabelText("关键依赖")]
        [ListDrawerSettings(IsReadOnly = true, ShowFoldout = false)]
        List<string> Dependencies { get; set; } = new List<string>();

        bool m_EditorAsmOk, m_RuntimeAsmOk;
        Color EditorAsmColor => m_EditorAsmOk ? Color.white : new Color(1f, 0.5f, 0.45f);
        Color RuntimeAsmColor => m_RuntimeAsmOk ? Color.white : new Color(1f, 0.5f, 0.45f);

        // ==================================================== 图列表

        [Title("剧情图")]
        [TableList(AlwaysExpanded = true, DrawScrollView = true, MinScrollViewHeight = 170, HideToolbar = true)]
        [LabelText("扫描结果")]
        public List<GraphReport> Reports = new List<GraphReport>();

        [PropertyOrder(10)]
        [ButtonGroup("ops")]
        [Button("重新扫描", ButtonSizes.Large)]
        void ScanButton() => Scan();

        [PropertyOrder(10)]
        [ButtonGroup("ops")]
        [Button("复制诊断报告", ButtonSizes.Large)]
        void CopyReport()
        {
            EditorGUIUtility.systemCopyBuffer = BuildTextReport();
            Debug.Log("[剧情图体检] 诊断报告已复制到剪贴板。");
        }

        // ==================================================== 图 ID 重复

        [Title("图 ID 重复检查")]
        [PropertyOrder(14)]
        [InfoBox(
            "每张 .agv 内部有一个 Graph ID（GraphModelImp 的 m_Guid / m_HashGuid），和文件名、" +
            "Unity 资产 GUID 都不是一回事。\n" +
            "【复制 .agv 文件】或【在 Project 里 Ctrl+D 复制】会把这个 ID 一起复制。" +
            "GraphToolkit 按它索引已打开的图，ID 撞了就只有一张能正常打开，其余打开是空的。\n" +
            "新建图请一律走 Assets → Create → Drama → 剧情编辑器，不要复制文件。",
            InfoMessageType.Warning)]
        [ShowInInspector, ReadOnly, LabelText("结论")]
        [GUIColor(nameof(DupColor))]
        string DuplicateState { get; set; } = "-";

        [PropertyOrder(15)]
        [ShowInInspector, ReadOnly, LabelText("重复分组")]
        [ListDrawerSettings(IsReadOnly = true, ShowFoldout = true)]
        List<string> DuplicateGroups { get; set; } = new List<string>();

        bool m_HasDuplicate;
        Color DupColor => m_HasDuplicate ? new Color(1f, 0.5f, 0.45f) : Color.white;

        [PropertyOrder(16)]
        [LabelText("我已经关掉所有剧情图窗口")]
        [Tooltip("图还开着的话，Unity 内存里是旧 ID，保存时会把文件覆盖回去，改了等于白改")]
        public bool ConfirmGraphWindowsClosed;

        [PropertyOrder(17)]
        [Button("重新生成重复图的 ID（每组保留第一个）", ButtonSizes.Large)]
        [EnableIf("@" + nameof(m_HasDuplicate) + " && " + nameof(ConfirmGraphWindowsClosed))]
        [GUIColor(0.95f, 0.75f, 0.4f)]
        void RegenerateDuplicateIds()
        {
            var groups = Reports.Where(r => !string.IsNullOrEmpty(r.GraphHash))
                                .GroupBy(r => r.GraphHash)
                                .Where(g => g.Count() > 1)
                                .ToList();
            if (groups.Count == 0) { Debug.Log("[剧情图体检] 没有重复的图 ID。"); return; }

            var targets = groups.SelectMany(g => g.Skip(1)).ToList();

            var ok = EditorUtility.DisplayDialog(
                "重新生成图 ID",
                $"将修改这 {targets.Count} 个文件的内部 Graph ID：\n\n" +
                string.Join("\n", targets.Select(t => "  " + t.Path)) +
                "\n\n每组保留第一个不动。改动只涉及 GraphModelImp 的 m_Guid / m_HashGuid，" +
                "节点和连线一个都不碰。\n\n" +
                "★ 建议先确认这些文件已提交进 git，万一有问题能回滚。\n\n继续吗？",
                "重新生成", "取消");
            if (!ok) return;

            var root = Path.GetDirectoryName(Application.dataPath)?.Replace('\\', '/') ?? "";
            var done = 0;
            var sb = new StringBuilder();

            foreach (var t in targets)
            {
                var full = Path.Combine(root, t.Path);
                if (!File.Exists(full)) { sb.AppendLine($"✘ 找不到 {t.Path}"); continue; }

                var text = File.ReadAllText(full);
                var m = s_GraphGuidBlock.Match(text);
                if (!m.Success) { sb.AppendLine($"✘ {t.Path} 里没找到 GraphModelImp 的 GUID 块"); continue; }

                var (v0, v1, hash) = NewGraphId();
                var replaced = s_GraphGuidBlock.Replace(text,
                    mm => mm.Groups[1].Value + v0 + mm.Groups[3].Value + v1 + mm.Groups[5].Value + hash,
                    1);

                File.WriteAllText(full, replaced);
                sb.AppendLine($"✔ {t.Path}\n      {t.GraphHash.Substring(0, 8)}… → {hash.Substring(0, 8)}…");
                done++;
            }

            AssetDatabase.Refresh();
            Detail = $"已重新生成 {done} 个图的 ID：\n\n{sb}\n重新扫描确认一下，然后逐张打开验证。";
            Scan();
        }

        // .agv 里 GraphModelImp 的 GUID 块。整份文件里只出现一次，定点替换是安全的。
        static readonly Regex s_GraphGuidBlock = new Regex(
            @"(class:\s*GraphModelImp[^\r\n]*\r?\n\s*data:\s*\r?\n\s*m_Guid:\s*\r?\n\s*m_Value0:\s*)(-?\d+)" +
            @"(\s*\r?\n\s*m_Value1:\s*)(-?\d+)" +
            @"(\s*\r?\n\s*m_HashGuid:\s*\r?\n\s*serializedVersion:\s*\d+\s*\r?\n\s*Hash:\s*)([0-9a-fA-F]+)",
            RegexOptions.Compiled);

        /// <summary>
        /// 造一个新的图 ID。
        /// Hash 字符串 = m_Value0 的小端十六进制 + m_Value1 的小端十六进制 —— 两者必须自洽，
        /// 只改一个会得到一张自相矛盾的图。
        /// </summary>
        static (long v0, long v1, string hash) NewGraphId()
        {
            var bytes = Guid.NewGuid().ToByteArray();
            var v0 = BitConverter.ToInt64(bytes, 0);
            var v1 = BitConverter.ToInt64(bytes, 8);
            return (v0, v1, LeHex(v0) + LeHex(v1));
        }

        static string LeHex(long v)
        {
            var b = BitConverter.GetBytes(v);      // 小端机器上就是 LSB 在前
            if (!BitConverter.IsLittleEndian) Array.Reverse(b);
            var sb = new StringBuilder(16);
            foreach (var x in b) sb.Append(x.ToString("x2"));
            return sb.ToString();
        }

        // ==================================================== 详情

        [Title("缺失的类型")]
        [PropertyOrder(20)]
        [ShowInInspector, ReadOnly, HideLabel]
        [MultiLineProperty(12)]
        public string Detail = "（点「重新扫描」开始）";

        // ==================================================== 扫描

        internal void Scan()
        {
            ScanEnvironment();

            var root = Path.GetDirectoryName(Application.dataPath)?.Replace('\\', '/') ?? "";
            var files = AssetDatabase.GetAllAssetPaths()
                .Where(p => p.StartsWith("Assets/") && p.EndsWith(".agv", StringComparison.OrdinalIgnoreCase))
                .OrderBy(p => p)
                .ToList();

            Reports = new List<GraphReport>();
            var sb = new StringBuilder();

            foreach (var rel in files)
            {
                var full = Path.Combine(root, rel);
                if (!File.Exists(full)) continue;

                var rep = Analyze(rel, File.ReadAllText(full));
                Reports.Add(rep);

                if (rep.MissingTypeCount > 0)
                {
                    sb.AppendLine($"【{rep.Name}】文件里有 {rep.NodeRefCount} 个节点引用，"
                                + $"其中 {rep.MissingTypeCount} 个类型解析不到：");
                    foreach (var kv in rep.MissingDetail)
                        sb.AppendLine($"    ✘ {kv}");
                    sb.AppendLine();
                }
            }

            // ---- 跨文件比对图 ID ----
            var dupGroups = Reports.Where(r => !string.IsNullOrEmpty(r.GraphHash))
                                   .GroupBy(r => r.GraphHash)
                                   .Where(g => g.Count() > 1)
                                   .ToList();

            m_HasDuplicate = dupGroups.Count > 0;
            DuplicateGroups = new List<string>();

            var dupPaths = new HashSet<string>();
            foreach (var g in dupGroups)
            {
                DuplicateGroups.Add($"{g.Key.Substring(0, 8)}…  ←  " + string.Join(" / ", g.Select(x => x.Name)));
                foreach (var r in g.Skip(1)) dupPaths.Add(r.Path);   // 每组第一个视为"原件"
            }

            DuplicateState = m_HasDuplicate
                ? $"✘ 有 {dupGroups.Count} 组图共用同一个 ID —— 每组只有一张能打开，其余打开是空的"
                : "✔ 每张图的 ID 都是唯一的";

            foreach (var r in Reports)
            {
                r.ShortId = string.IsNullOrEmpty(r.GraphHash) ? "-" : r.GraphHash.Substring(0, 8);
                r.Status = r.MissingTypeCount > 0 ? $"缺 {r.MissingTypeCount} 个节点"
                         : dupPaths.Contains(r.Path) ? "ID 重复"
                         : "正常";
            }

            if (m_HasDuplicate)
            {
                sb.AppendLine("【图 ID 重复】以下几组共用同一个 Graph ID：");
                foreach (var s in DuplicateGroups) sb.AppendLine("    " + s);
                sb.AppendLine();
                sb.AppendLine("成因：复制 .agv 文件（或在 Project 里 Ctrl+D）会把图内部的 ID 一起复制。");
                sb.AppendLine("后果：GraphToolkit 按这个 ID 索引已打开的图，撞了就只有一张能打开。");
                sb.AppendLine("修法：关掉所有剧情图窗口 → 勾上面的确认 → 点「重新生成重复图的 ID」。");
                sb.AppendLine("以后新建图走 Assets → Create → Drama → 剧情编辑器，别复制文件。");
                sb.AppendLine();
            }

            if (sb.Length == 0)
            {
                sb.AppendLine("所有图的节点类型都能正常解析，图 ID 也没有重复 —— 这台机器可以正常打开它们。");
                if (!m_EditorAsmOk || !m_RuntimeAsmOk)
                    sb.AppendLine("\n但上面的程序集状态是红的，先把编译修好再说。");
            }
            else
            {
                sb.AppendLine("---- 怎么修 ----");
                sb.AppendLine("解析不到 = 那个程序集没编译出来。按顺序查：");
                sb.AppendLine("  1. Console 里有没有红色编译错误（这是根因，其余都是症状）");
                sb.AppendLine("  2. Odin Inspector 装了吗（Assets/Plugins/Sirenix）");
                sb.AppendLine("  3. DOTween 装了吗（Assets/Plugins/Demigiant）");
                sb.AppendLine("  4. UniTask 装了吗（Package Manager 里找 com.cysharp.unitask）");
                sb.AppendLine("  5. Player Settings → Scripting Define Symbols 里有 UNITASK_DOTWEEN_SUPPORT 吗");
                sb.AppendLine("  6. git pull 拿到最新代码了吗（节点类是新加的话，老代码里没有）");
            }

            Detail = sb.ToString();
        }

        void ScanEnvironment()
        {
            m_EditorAsmOk = FindAssembly("DramaEditor") != null;
            m_RuntimeAsmOk = FindAssembly("Drama.Runtime") != null;

            EditorAsmState = m_EditorAsmOk
                ? "✔ 已加载（节点类型可用）"
                : "✘ 没有加载 —— 图里的自定义节点全都会解析失败";

            RuntimeAsmState = m_RuntimeAsmOk
                ? "✔ 已加载"
                : "✘ 没有加载 —— DramaEditor 依赖它，它挂了 DramaEditor 也编不出来";

            var defines = PlayerSettings.GetScriptingDefineSymbols(
                UnityEditor.Build.NamedBuildTarget.FromBuildTargetGroup(
                    BuildPipeline.GetBuildTargetGroup(EditorUserBuildSettings.activeBuildTarget)));
            var hasDotweenSupport = (defines ?? "").Contains("UNITASK_DOTWEEN_SUPPORT");
            DefineState = hasDotweenSupport
                ? "✔ 有 UNITASK_DOTWEEN_SUPPORT"
                : "✘ 缺 UNITASK_DOTWEEN_SUPPORT（Player Settings → Scripting Define Symbols 里加）";

            Dependencies = new List<string>
            {
                (FindAssembly("Sirenix.OdinInspector.Attributes") != null ? "✔" : "✘") + " Odin Inspector",
                (FindAssembly("DOTween") != null ? "✔" : "✘") + " DOTween",
                (FindAssembly("UniTask") != null ? "✔" : "✘") + " UniTask",
                (FindAssembly("UnityEditor.GraphToolkitModule") != null ? "✔" : "✘") + " GraphToolkit",
            };
        }

        static System.Reflection.Assembly FindAssembly(string name)
        {
            foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
                if (string.Equals(a.GetName().Name, name, StringComparison.OrdinalIgnoreCase))
                    return a;
            return null;
        }

        // .agv 里的形状： type: {class: TalkNode, ns: Drama.Editor, asm: DramaEditor}
        static readonly Regex s_TypeRef = new Regex(
            @"type:\s*\{class:\s*([^,}]*),\s*ns:\s*([^,}]*),\s*asm:\s*([^}]*)\}",
            RegexOptions.Compiled);

        static GraphReport Analyze(string relPath, string text)
        {
            var rep = new GraphReport
            {
                Name = Path.GetFileNameWithoutExtension(relPath),
                Path = relPath,
            };

            // 图自己的 ID。复制 .agv 会把它一起复制，两张图撞 ID 就只有一张能打开。
            var gm = s_GraphGuidBlock.Match(text);
            rep.GraphHash = gm.Success ? gm.Groups[6].Value.ToLowerInvariant() : "";

            var resolvable = new Dictionary<string, bool>();
            var perType = new Dictionary<string, int>();

            foreach (Match m in s_TypeRef.Matches(text))
            {
                var cls = m.Groups[1].Value.Trim();
                var ns = m.Groups[2].Value.Trim();
                var asm = m.Groups[3].Value.Trim();

                // 空条目（rid: -2 那种占位）跳过
                if (string.IsNullOrEmpty(cls) || string.IsNullOrEmpty(asm)) continue;

                var key = $"{(string.IsNullOrEmpty(ns) ? cls : ns + "." + cls)}, {asm}";
                perType.TryGetValue(key, out var c);
                perType[key] = c + 1;

                if (!resolvable.ContainsKey(key))
                    resolvable[key] = ResolveType(ns, cls, asm) != null;

                rep.NodeRefCount++;
            }

            foreach (var kv in perType)
            {
                if (resolvable[kv.Key]) continue;
                rep.MissingTypeCount += kv.Value;
                rep.MissingDetail.Add($"{kv.Key}   ×{kv.Value}");
            }

            // Status 由 Scan 统一定（图 ID 是否重复要跨文件比对，这里看不到别的文件）
            rep.Summary = $"引用 {rep.NodeRefCount} 个"
                        + (rep.MissingTypeCount > 0 ? $"，解析失败 {rep.MissingTypeCount} 个" : "，全部可解析");
            return rep;
        }

        static Type ResolveType(string ns, string cls, string asm)
        {
            var full = string.IsNullOrEmpty(ns) ? cls : ns + "." + cls;

            var t = Type.GetType($"{full}, {asm}");
            if (t != null) return t;

            var a = FindAssembly(asm);
            if (a != null) { t = a.GetType(full, false); if (t != null) return t; }

            // 程序集名变过的情况：全域按全名找一遍
            foreach (var any in AppDomain.CurrentDomain.GetAssemblies())
            {
                t = any.GetType(full, false);
                if (t != null) return t;
            }
            return null;
        }

        string BuildTextReport()
        {
            var sb = new StringBuilder();
            sb.AppendLine("==== 剧情图体检报告 ====");
            sb.AppendLine($"Unity        : {Application.unityVersion}");
            sb.AppendLine($"工程         : {Path.GetDirectoryName(Application.dataPath)}");
            sb.AppendLine($"DramaEditor  : {EditorAsmState}");
            sb.AppendLine($"Drama.Runtime: {RuntimeAsmState}");
            sb.AppendLine($"Define       : {DefineState}");
            foreach (var d in Dependencies) sb.AppendLine($"依赖         : {d}");
            sb.AppendLine();
            foreach (var r in Reports)
                sb.AppendLine($"{r.Name,-16} {r.Status,-18} {r.Summary}");
            sb.AppendLine();
            sb.AppendLine(Detail);
            return sb.ToString();
        }

        // ==================================================== 表格行

        [Serializable]
        public class GraphReport
        {
            [TableColumnWidth(120)]
            [ReadOnly, LabelText("图")]
            public string Name;

            [TableColumnWidth(130, Resizable = false)]
            [ReadOnly, LabelText("状态")]
            public string Status;

            [ReadOnly, LabelText("说明")]
            public string Summary;

            [TableColumnWidth(90, Resizable = false)]
            [ReadOnly, LabelText("图 ID")]
            public string ShortId;

            [HideInTables] public string Path;
            [HideInTables] public string GraphHash;
            [HideInTables] public int NodeRefCount;
            [HideInTables] public int MissingTypeCount;
            [HideInTables] public List<string> MissingDetail = new List<string>();

            [TableColumnWidth(120, Resizable = false)]
            [Button("尝试打开")]
            void TryOpen()
            {
                if (MissingTypeCount > 0)
                {
                    var ok = EditorUtility.DisplayDialog(
                        "这张图现在打不全",
                        $"「{Name}」有 {MissingTypeCount} 个节点的类型解析不到。\n\n" +
                        "现在打开会看到一张不完整（甚至空）的图。\n" +
                        "★ 这种状态下【千万不要保存】，一保存数据就永久没了。\n\n" +
                        "仍要打开吗？",
                        "只看不存", "取消");
                    if (!ok) return;
                }

                var obj = AssetDatabase.LoadMainAssetAtPath(Path);
                if (obj == null) { Debug.LogError($"[剧情图体检] 加载不了 {Path}"); return; }
                AssetDatabase.OpenAsset(obj);
            }
        }
    }
}

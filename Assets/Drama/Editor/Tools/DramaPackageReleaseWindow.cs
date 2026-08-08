using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Drama.Editor.Tools
{
    /// <summary>
    /// Drama Runtime 包的发布工具：改版本号 → 提交 → 打 tag → 推送。
    /// 菜单：Tools / Drama / 包发布
    ///
    /// 所有 git 操作都调用系统的 git 命令行，工作目录是工程根。
    /// 推送类操作需要先勾「我确认推送到远端」，避免误点。
    /// </summary>
    internal class DramaPackageReleaseWindow : OdinEditorWindow
    {
        [MenuItem("Tools/Drama/包发布", false, 20)]
        static void Open()
        {
            var win = GetWindow<DramaPackageReleaseWindow>();
            win.titleContent = new GUIContent("包发布");
            win.minSize = new Vector2(700f, 560f);
            win.RefreshStatus();
            win.Show();
        }

        // ==================================================== 配置

        [Title("包")]
        [LabelText("package.json 路径"), Tooltip("相对工程根目录")]
        [OnValueChanged(nameof(RefreshStatus))]
        public string PackageJsonPath = "Packages/com.lumino.drama.runtime/package.json";

        [LabelText("tag 前缀"), Tooltip("一个仓库里可能有多个包，加前缀区分。最终 tag = 前缀 + 版本号")]
        public string TagPrefix = "drama-runtime/v";

        // ==================================================== 状态（只读）

        [Title("当前状态")]
        [ShowInInspector, ReadOnly, LabelText("包名")]
        string PackageName { get; set; } = "-";

        [ShowInInspector, ReadOnly, LabelText("当前版本")]
        string CurrentVersion { get; set; } = "-";

        [ShowInInspector, ReadOnly, LabelText("分支")]
        string Branch { get; set; } = "-";

        [ShowInInspector, ReadOnly, LabelText("远端")]
        string Remote { get; set; } = "-";

        [ShowInInspector, ReadOnly, LabelText("最后一次提交")]
        string LastCommit { get; set; } = "-";

        [ShowInInspector, ReadOnly, LabelText("工作区")]
        [GUIColor(nameof(DirtyColor))]
        string DirtyState { get; set; } = "-";

        [ShowInInspector, ReadOnly, LabelText("已有 tag（本包）")]
        [ListDrawerSettings(IsReadOnly = true, ShowFoldout = true, ShowItemCount = true)]
        List<string> ExistingTags { get; set; } = new List<string>();

        Color DirtyColor => m_IsDirty ? new Color(1f, 0.85f, 0.4f) : Color.white;
        bool m_IsDirty;
        string m_ProjectRoot;

        [PropertyOrder(5)]
        [Button("刷新状态", ButtonSizes.Medium)]
        void RefreshButton() => RefreshStatus();

        // ==================================================== 发布参数

        [Title("发布")]
        [PropertyOrder(10)]
        [LabelText("新版本号"), Tooltip("留空表示不改版本，直接用当前版本打 tag")]
        public string NewVersion = "";

        [PropertyOrder(10)]
        [ButtonGroup("bump")]
        [Button("＋Patch", ButtonSizes.Medium)]
        void BumpPatch() => NewVersion = Bump(2);

        [PropertyOrder(10)]
        [ButtonGroup("bump")]
        [Button("＋Minor", ButtonSizes.Medium)]
        void BumpMinor() => NewVersion = Bump(1);

        [PropertyOrder(10)]
        [ButtonGroup("bump")]
        [Button("＋Major", ButtonSizes.Medium)]
        void BumpMajor() => NewVersion = Bump(0);

        [PropertyOrder(11)]
        [LabelText("提交说明")]
        public string CommitMessage = "chore(drama-runtime): release";

        [PropertyOrder(12)]
        [ShowInInspector, ReadOnly, LabelText("即将打的 tag")]
        string PlannedTag => TagPrefix + (string.IsNullOrWhiteSpace(NewVersion) ? CurrentVersion : NewVersion.Trim());

        [PropertyOrder(13)]
        [InfoBox("推送会改动共享远端仓库，且 tag 一旦推出去别人就可能拉到。确认无误再勾。",
                 InfoMessageType.Warning, VisibleIf = "@!" + nameof(ConfirmPush))]
        [LabelText("我确认推送到远端")]
        public bool ConfirmPush;

        // ==================================================== 操作按钮

        [PropertyOrder(20)]
        [ButtonGroup("steps")]
        [Button("① 写入版本号", ButtonSizes.Large)]
        [EnableIf("@!string.IsNullOrWhiteSpace(" + nameof(NewVersion) + ")")]
        void StepWriteVersion()
        {
            if (!WriteVersion(NewVersion.Trim())) return;
            AssetDatabase.Refresh();
            RefreshStatus();
        }

        [PropertyOrder(20)]
        [ButtonGroup("steps")]
        [Button("② 提交", ButtonSizes.Large)]
        void StepCommit()
        {
            var pkgDir = Path.GetDirectoryName(PackageJsonPath)?.Replace('\\', '/');
            if (string.IsNullOrEmpty(pkgDir)) { Log("包路径不对"); return; }

            Git($"add -- \"{pkgDir}\"");
            var msg = $"{CommitMessage} {PlannedTag}";
            var r = Git($"commit -m \"{msg}\"");
            if (r.code != 0 && r.all.Contains("nothing to commit"))
                Log("没有待提交的改动，跳过。");
            RefreshStatus();
        }

        [PropertyOrder(20)]
        [ButtonGroup("steps")]
        [Button("③ 打 tag", ButtonSizes.Large)]
        void StepTag()
        {
            var tag = PlannedTag;
            if (ExistingTags.Contains(tag))
            {
                Log($"tag「{tag}」已存在。要重发请先删除：git tag -d {tag}");
                return;
            }
            Git($"tag -a \"{tag}\" -m \"{tag}\"");
            RefreshStatus();
        }

        [PropertyOrder(21)]
        [ButtonGroup("push")]
        [Button("④ 推送提交", ButtonSizes.Large)]
        [EnableIf(nameof(ConfirmPush))]
        [GUIColor(0.95f, 0.75f, 0.4f)]
        void StepPushCommits()
        {
            Git($"push origin {Branch}");
            RefreshStatus();
        }

        [PropertyOrder(21)]
        [ButtonGroup("push")]
        [Button("⑤ 推送 tag", ButtonSizes.Large)]
        [EnableIf(nameof(ConfirmPush))]
        [GUIColor(0.95f, 0.75f, 0.4f)]
        void StepPushTag()
        {
            Git($"push origin \"{PlannedTag}\"");
            RefreshStatus();
        }

        [PropertyOrder(22)]
        [Button("一键发布（①→⑤ 全跑）", ButtonSizes.Gigantic)]
        [EnableIf(nameof(ConfirmPush))]
        [GUIColor(0.5f, 0.9f, 0.6f)]
        void ReleaseAll()
        {
            var tag = PlannedTag;
            var ok = EditorUtility.DisplayDialog(
                "确认发布",
                $"将执行：\n\n" +
                $"  1. 写入版本号 {(string.IsNullOrWhiteSpace(NewVersion) ? "(不改)" : NewVersion.Trim())}\n" +
                $"  2. git add + commit\n" +
                $"  3. git tag {tag}\n" +
                $"  4. git push origin {Branch}\n" +
                $"  5. git push origin {tag}\n\n" +
                $"远端：{Remote}\n\n推送后别人就能拉到这个版本，确认继续？",
                "发布", "取消");

            if (!ok) { Log("已取消。"); return; }

            m_Log.Clear();
            if (!string.IsNullOrWhiteSpace(NewVersion)) { StepWriteVersion(); }
            StepCommit();
            StepTag();
            StepPushCommits();
            StepPushTag();
            Log($"\n完成。目标工程 manifest 用：\n{ManifestLine}");
        }

        // ==================================================== 给别的工程用的依赖行

        [Title("给别人的导入地址")]
        [PropertyOrder(30)]
        [InfoBox("把下面任意一种发给使用方即可。方式 A 最省事：Unity 顶上 Package Manager → 左上角「+」→ " +
                 "Add package from git URL… → 粘贴 → Add。\n" +
                 "前提：对方机器装了 Git、能访问这个远端地址，并且工程里已有 Odin 和 DOTween。",
                 InfoMessageType.Info)]
        [LabelText("锁定到 tag")]
        [Tooltip("勾上 = 别人永远拉到这个版本（推荐）。取消 = 拉默认分支最新，队友之间可能版本不一致")]
        public bool PinToTag = true;

        /// <summary>仓库地址（补 .git 后缀）。</summary>
        string RepoUrl
        {
            get
            {
                var url = string.IsNullOrEmpty(Remote) || Remote == "-" ? "<还没有远端>" : Remote;
                if (!url.EndsWith(".git")) url += ".git";
                return url;
            }
        }

        /// <summary>包在仓库里的相对路径。</summary>
        string PackageDirInRepo => Path.GetDirectoryName(PackageJsonPath)?.Replace('\\', '/');

        // ---- A：Package Manager 直接粘的一行 URL ----

        [PropertyOrder(31)]
        [ShowInInspector, ReadOnly]
        [LabelText("A · Package Manager 用")]
        [MultiLineProperty(2)]
        string GitUrl =>
            $"{RepoUrl}?path={PackageDirInRepo}" + (PinToTag ? $"#{PlannedTag}" : "");

        [PropertyOrder(32)]
        [Button("复制 A（git URL）", ButtonSizes.Medium)]
        void CopyGitUrl() => CopyToClipboard(GitUrl, "git URL");

        // ---- B：manifest.json 里的一行 ----

        [PropertyOrder(33)]
        [ShowInInspector, ReadOnly]
        [LabelText("B · manifest 依赖行")]
        [MultiLineProperty(2)]
        string ManifestLine => $"\"{PackageName}\": \"{GitUrl}\"";

        [PropertyOrder(34)]
        [Button("复制 B（依赖行）", ButtonSizes.Medium)]
        void CopyManifestLine() => CopyToClipboard(ManifestLine, "manifest 依赖行");

        // ---- C：完整 manifest 片段 ----

        [PropertyOrder(35)]
        [ShowInInspector, ReadOnly]
        [LabelText("C · 完整片段")]
        [MultiLineProperty(6)]
        string ManifestSnippet =>
            "{\n" +
            "  \"dependencies\": {\n" +
            $"    {ManifestLine}\n" +
            "  }\n" +
            "}";

        [PropertyOrder(36)]
        [Button("复制 C（完整片段）", ButtonSizes.Medium)]
        void CopyManifestSnippet() => CopyToClipboard(ManifestSnippet, "manifest 片段");

        // ---- 便捷：把地址发给别人前先自查 ----

        [PropertyOrder(37)]
        [Button("自检：这个 tag 推上去了吗", ButtonSizes.Medium)]
        void CheckTagPushed()
        {
            if (!PinToTag) { Log("没有锁定 tag，别人会拉默认分支最新，无需自检。"); return; }

            var tag = PlannedTag;
            var r = Git($"ls-remote --tags origin \"refs/tags/{tag}\"");
            if (r.code != 0) { Log("查询失败，检查网络 / 远端权限。"); return; }

            Log(string.IsNullOrWhiteSpace(r.all)
                ? $"❌ 远端还没有「{tag}」。别人现在拉会失败 —— 先点 ⑤ 推送 tag。"
                : $"✅ 远端已有「{tag}」，可以把地址发给别人了。");
        }

        void CopyToClipboard(string text, string what)
        {
            EditorGUIUtility.systemCopyBuffer = text;
            Log($"已复制{what}到剪贴板：\n    {text}");
        }

        // ==================================================== 日志

        [Title("输出")]
        [PropertyOrder(40)]
        [ShowInInspector, ReadOnly, HideLabel]
        [MultiLineProperty(14)]
        string LogText => m_Log.ToString();

        readonly StringBuilder m_Log = new StringBuilder();

        [PropertyOrder(41)]
        [Button("清空输出", ButtonSizes.Small)]
        void ClearLog() => m_Log.Clear();

        // ==================================================== 实现

        internal void RefreshStatus()
        {
            m_ProjectRoot = Path.GetDirectoryName(Application.dataPath)?.Replace('\\', '/');

            // package.json
            var full = Path.Combine(m_ProjectRoot ?? "", PackageJsonPath);
            if (File.Exists(full))
            {
                var json = File.ReadAllText(full);
                PackageName = Match(json, "\"name\"\\s*:\\s*\"([^\"]+)\"", "-");
                CurrentVersion = Match(json, "\"version\"\\s*:\\s*\"([^\"]+)\"", "-");
            }
            else
            {
                PackageName = CurrentVersion = "(找不到 package.json)";
            }

            // git
            Branch = GitQuiet("rev-parse --abbrev-ref HEAD").Trim();
            Remote = GitQuiet("config --get remote.origin.url").Trim();
            LastCommit = GitQuiet("log -1 --oneline").Trim();

            var status = GitQuiet("status --porcelain").Trim();
            m_IsDirty = !string.IsNullOrEmpty(status);
            var lines = m_IsDirty ? status.Split('\n').Length : 0;
            DirtyState = m_IsDirty ? $"有 {lines} 处未提交改动" : "干净";

            ExistingTags = new List<string>();
            var tags = GitQuiet($"tag --list \"{TagPrefix}*\"").Trim();
            if (!string.IsNullOrEmpty(tags))
                foreach (var t in tags.Split('\n'))
                    if (!string.IsNullOrWhiteSpace(t)) ExistingTags.Add(t.Trim());
        }

        string Bump(int part)
        {
            var v = string.IsNullOrWhiteSpace(NewVersion) ? CurrentVersion : NewVersion;
            var m = Regex.Match(v ?? "", @"^(\d+)\.(\d+)\.(\d+)");
            if (!m.Success) { Log($"版本号「{v}」不是 x.y.z 格式，改不了"); return NewVersion; }

            var n = new[] { int.Parse(m.Groups[1].Value), int.Parse(m.Groups[2].Value), int.Parse(m.Groups[3].Value) };
            n[part]++;
            for (int i = part + 1; i < 3; i++) n[i] = 0;
            return $"{n[0]}.{n[1]}.{n[2]}";
        }

        bool WriteVersion(string version)
        {
            var full = Path.Combine(m_ProjectRoot ?? "", PackageJsonPath);
            if (!File.Exists(full)) { Log("找不到 package.json"); return false; }

            var json = File.ReadAllText(full);
            var replaced = Regex.Replace(json, "(\"version\"\\s*:\\s*\")([^\"]+)(\")", "${1}" + version + "${3}");
            if (replaced == json) { Log("package.json 里没找到 version 字段"); return false; }

            File.WriteAllText(full, replaced);
            Log($"版本号 → {version}");
            return true;
        }

        static string Match(string text, string pattern, string fallback)
        {
            var m = Regex.Match(text, pattern);
            return m.Success ? m.Groups[1].Value : fallback;
        }

        void Log(string s)
        {
            m_Log.AppendLine(s);
            Repaint();
        }

        // ------------------------------------------------------------ git

        (int code, string all) Git(string args)
        {
            Log($"$ git {args}");
            var r = RunGit(args);
            if (!string.IsNullOrWhiteSpace(r.all)) Log(Indent(r.all));
            if (r.code != 0) Log($"  ↑ 退出码 {r.code}");
            return (r.code, r.all);
        }

        string GitQuiet(string args)
        {
            var r = RunGit(args);
            return r.code == 0 ? r.stdout : string.Empty;
        }

        (int code, string stdout, string stderr, string all) RunGit(string args)
        {
            var root = m_ProjectRoot ?? Path.GetDirectoryName(Application.dataPath)?.Replace('\\', '/');

            try
            {
                var psi = new ProcessStartInfo("git", args)
                {
                    WorkingDirectory = root,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8,
                };

                using (var p = new Process { StartInfo = psi })
                {
                    var so = new StringBuilder();
                    var se = new StringBuilder();

                    p.OutputDataReceived += (_, e) => { if (e.Data != null) so.AppendLine(e.Data); };
                    p.ErrorDataReceived += (_, e) => { if (e.Data != null) se.AppendLine(e.Data); };

                    p.Start();
                    p.BeginOutputReadLine();
                    p.BeginErrorReadLine();

                    if (!p.WaitForExit(60000))
                    {
                        try { p.Kill(); } catch { }
                        return (-1, "", "超时（60s）", "git 执行超时");
                    }
                    p.WaitForExit();   // 等异步读取的回调收尾

                    var all = (so.ToString() + se.ToString()).TrimEnd();
                    return (p.ExitCode, so.ToString(), se.ToString(), all);
                }
            }
            catch (Exception e)
            {
                var msg = $"调不起 git：{e.Message}\n确认 git 已安装并在 PATH 里。";
                Debug.LogWarning(msg);
                return (-1, "", msg, msg);
            }
        }

        static string Indent(string s)
        {
            var sb = new StringBuilder();
            foreach (var line in s.Split('\n'))
                if (!string.IsNullOrWhiteSpace(line)) sb.AppendLine("    " + line.TrimEnd());
            return sb.ToString().TrimEnd();
        }
    }
}


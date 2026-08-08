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
        [Button("一键发布（①→⑥ 全跑）", ButtonSizes.Gigantic)]
        [EnableIf(nameof(ConfirmPush))]
        [GUIColor(0.5f, 0.9f, 0.6f)]
        void ReleaseAll()
        {
            var tag = PlannedTag;
            var npmStep = HasRegistry
                ? $"  6. npm publish --registry {RegistryUrl}\n"
                : "  6. （没填 registry 地址，跳过 npm publish —— 宿主工程的 Update 将无法发现新版本）\n";

            var ok = EditorUtility.DisplayDialog(
                "确认发布",
                $"将执行：\n\n" +
                $"  1. 写入版本号 {(string.IsNullOrWhiteSpace(NewVersion) ? "(不改)" : NewVersion.Trim())}\n" +
                $"  2. git add + commit\n" +
                $"  3. git tag {tag}\n" +
                $"  4. git push origin {Branch}\n" +
                $"  5. git push origin {tag}\n" +
                npmStep + "\n" +
                $"远端：{Remote}\n\n推送后别人就能拉到这个版本，确认继续？",
                "发布", "取消");

            if (!ok) { Log("已取消。"); return; }

            m_Log.Clear();
            if (!string.IsNullOrWhiteSpace(NewVersion)) { StepWriteVersion(); }
            StepCommit();
            StepTag();
            StepPushCommits();
            StepPushTag();

            if (HasRegistry)
            {
                var dir = PackageDirInRepo;
                if (!string.IsNullOrEmpty(dir)) Npm($"publish --registry {RegistryUrl}", dir);
                Log($"\n完成。宿主工程 manifest 用（Update 可用）：\n{RegistryManifestSnippet}");
            }
            else
            {
                Log("\n没填 registry 地址，只发了 git tag。");
                Log($"宿主工程 manifest 用（但 Update 发现不了新版本）：\n{ManifestLine}");
            }
        }

        // ==================================================== Registry 发布

        const string k_RegistryPrefKey = "Drama.Release.RegistryUrl";

        [Title("Registry 发布（让宿主工程的 Update 真的能用）")]
        [PropertyOrder(25)]
        [InfoBox("git 依赖的 Update 按钮只会按 URL 里写死的 tag 重新拉一次 —— tag 是固定的，" +
                 "所以永远「没有新版本」。UPM 对 git 没有版本发现能力，这不是配置问题。\n\n" +
                 "想让宿主工程点 Update 就能升级，包必须发到 scoped registry。",
                 InfoMessageType.Warning)]
        [ShowInInspector, LabelText("registry 地址")]
        [Tooltip("私有 npm registry，比如 http://192.168.10.226:4873。记在 EditorPrefs 里，换机器要重填")]
        public string RegistryUrl
        {
            get => EditorPrefs.GetString(k_RegistryPrefKey, "");
            set => EditorPrefs.SetString(k_RegistryPrefKey, value ?? "");
        }

        /// <summary>包名的前两段，作为 scopedRegistries 的 scope。com.lumino.drama.runtime → com.lumino</summary>
        string ScopePrefix
        {
            get
            {
                var parts = (PackageName ?? "").Split('.');
                return parts.Length >= 2 ? $"{parts[0]}.{parts[1]}" : PackageName;
            }
        }

        bool HasRegistry => !string.IsNullOrWhiteSpace(RegistryUrl);

        [PropertyOrder(25.5f)]
        [ShowInInspector, ReadOnly, LabelText("npm 位置")]
        [GUIColor(nameof(NpmColor))]
        string NpmPathDisplay =>
            ResolveNpmPath() ?? "找不到 —— 装 Node.js（nodejs.org 的 LTS），装完点右边「重新查找」";

        Color NpmColor => ResolveNpmPath() != null ? Color.white : new Color(1f, 0.85f, 0.4f);

        [PropertyOrder(25.5f)]
        [Button("重新查找 npm", ButtonSizes.Medium)]
        void RefreshNpmPath()
        {
            var p = ResolveNpmPath(forceRefresh: true);
            Log(p != null ? $"找到 npm：{p}" : "还是找不到 npm。确认 Node.js 装好了，路径里有 npm.cmd。");
        }

        [PropertyOrder(26)]
        [ButtonGroup("npm")]
        [Button("⑥ npm publish", ButtonSizes.Large)]
        [EnableIf(nameof(HasRegistry))]
        [GUIColor(0.95f, 0.75f, 0.4f)]
        void StepNpmPublish()
        {
            var dir = PackageDirInRepo;
            if (string.IsNullOrEmpty(dir)) { Log("包路径不对"); return; }

            var ok = EditorUtility.DisplayDialog(
                "确认发布到 registry",
                $"将执行：\n\n" +
                $"  npm publish --registry {RegistryUrl}\n" +
                $"  工作目录：{dir}\n\n" +
                $"包：{PackageName} {CurrentVersion}\n\n" +
                $"npm 不允许覆盖已发布的版本号，发之前确认版本已经 bump 过。\n" +
                $"发出去别人就能拉到，确认继续？",
                "发布", "取消");
            if (!ok) { Log("已取消。"); return; }

            Npm($"publish --registry {RegistryUrl}", dir);
        }

        [PropertyOrder(26)]
        [ButtonGroup("npm")]
        [Button("自检：registry 上有哪些版本", ButtonSizes.Large)]
        [EnableIf(nameof(HasRegistry))]
        void CheckRegistryVersions()
        {
            var who = NpmQuiet($"whoami --registry {RegistryUrl}");
            Log(who.code == 0 && !string.IsNullOrWhiteSpace(who.all)
                ? $"已登录：{who.all.Trim()}"
                : $"⚠ 未登录。先在命令行跑：npm login --registry {RegistryUrl}");

            var r = NpmQuiet($"view {PackageName} versions --registry {RegistryUrl}");
            if (r.code != 0)
            {
                Log(r.all.Contains("E404") || r.all.Contains("404")
                    ? $"registry 上还没有「{PackageName}」，这会是第一次发布。"
                    : $"查询失败：\n{Indent(r.all)}");
                return;
            }

            Log($"registry 上已有版本：\n{Indent(r.all)}");
            Log(r.all.Contains($"'{CurrentVersion}'") || r.all.Contains($"\"{CurrentVersion}\"")
                ? $"❌ {CurrentVersion} 已经发过了，npm 会拒绝覆盖 —— 先 bump 版本号再发。"
                : $"✅ {CurrentVersion} 还没发过，可以 publish。");
        }

        // ==================================================== 给别的工程用的依赖行

        [Title("给别人的导入地址")]
        [PropertyOrder(30)]
        [InfoBox("推荐方式 R（registry）—— 只有它能让宿主工程的 Update 按钮真正发现新版本。\n" +
                 "git 那两种（A / B）装完之后想升级只能手改 manifest 里的 tag。\n" +
                 "无论哪种，对方工程都必须已有 Odin、DOTween 和 UniTask。",
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

        // ---- R：registry 方式（唯一能让 Update 生效的）----

        [PropertyOrder(30.5f)]
        [ShowInInspector, ReadOnly]
        [LabelText("R · 宿主 manifest 片段")]
        [MultiLineProperty(14)]
        [EnableIf(nameof(HasRegistry))]
        string RegistryManifestSnippet =>
            !HasRegistry
                ? "（先在上面填 registry 地址）"
                : "{\n" +
                  "  \"scopedRegistries\": [\n" +
                  "    {\n" +
                  "      \"name\": \"Lumino\",\n" +
                  $"      \"url\": \"{RegistryUrl}\",\n" +
                  "      \"scopes\": [\n" +
                  $"        \"{ScopePrefix}\"\n" +
                  "      ]\n" +
                  "    }\n" +
                  "  ],\n" +
                  "  \"dependencies\": {\n" +
                  $"    \"{PackageName}\": \"{CurrentVersion}\"\n" +
                  "  }\n" +
                  "}";

        [PropertyOrder(30.6f)]
        [Button("复制 R（registry 片段）", ButtonSizes.Medium)]
        [EnableIf(nameof(HasRegistry))]
        void CopyRegistrySnippet() => CopyToClipboard(RegistryManifestSnippet, "registry manifest 片段");

        [PropertyOrder(30.7f)]
        [InfoBox("宿主工程改用 R 之后，记得把原来那行 git 依赖删掉 —— 同一个包名留两条会打架。\n" +
                 "改完 Package Manager 里的徽章会从 Git 变成 registry 名，Versions 页签列出全部已发版本，" +
                 "Update 才会真的发现新版本。", InfoMessageType.None)]
        [ShowInInspector, ReadOnly, HideLabel]
        string RegistryHint => "";

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
            var r = RunProcess("git", args);
            if (!string.IsNullOrWhiteSpace(r.all)) Log(Indent(r.all));
            if (r.code != 0) Log($"  ↑ 退出码 {r.code}");
            return (r.code, r.all);
        }

        string GitQuiet(string args)
        {
            var r = RunProcess("git", args);
            return r.code == 0 ? r.stdout : string.Empty;
        }

        // ------------------------------------------------------------ npm

        static string s_NpmPath;

        /// <summary>
        /// 找 npm 的绝对路径。
        ///
        /// 为什么不直接写 "npm.cmd" 让系统去 PATH 里找：
        ///   Unity 进程的 PATH 是启动那一刻继承的。刚装完 Node.js 不重启 Unity，
        ///   进程里根本看不到新的 PATH，只会报「系统找不到指定的文件」。
        ///
        /// 所以这里除了进程自己的 PATH，还去读 User / Machine 两级 ——
        /// 那两级是实时从注册表取的，装完 Node 立刻就能找到，不用重启 Unity。
        /// </summary>
        static string ResolveNpmPath(bool forceRefresh = false)
        {
            if (!forceRefresh && !string.IsNullOrEmpty(s_NpmPath) && File.Exists(s_NpmPath))
                return s_NpmPath;

            s_NpmPath = null;
            var isWin = Application.platform == RuntimePlatform.WindowsEditor;

            // Windows 上 npm 是个 .cmd，UseShellExecute=false 时必须给全名
            var exeNames = isWin ? new[] { "npm.cmd", "npm.exe" } : new[] { "npm" };

            var targets = isWin
                ? new[] { EnvironmentVariableTarget.Process, EnvironmentVariableTarget.User, EnvironmentVariableTarget.Machine }
                : new[] { EnvironmentVariableTarget.Process };

            foreach (var target in targets)
            {
                string path;
                try { path = Environment.GetEnvironmentVariable("PATH", target); }
                catch { continue; }
                if (string.IsNullOrEmpty(path)) continue;

                foreach (var dir in path.Split(Path.PathSeparator))
                {
                    if (string.IsNullOrWhiteSpace(dir)) continue;
                    foreach (var exe in exeNames)
                    {
                        try
                        {
                            var full = Path.Combine(dir.Trim(), exe);
                            if (File.Exists(full)) return s_NpmPath = full;
                        }
                        catch { /* PATH 里可能有非法路径，跳过 */ }
                    }
                }
            }

            // PATH 里没有就翻常见安装位置
            var candidates = isWin
                ? new[]
                {
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "nodejs", "npm.cmd"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "nodejs", "npm.cmd"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "npm", "npm.cmd"),
                }
                : new[] { "/usr/local/bin/npm", "/opt/homebrew/bin/npm", "/usr/bin/npm" };

            foreach (var c in candidates)
                if (File.Exists(c)) return s_NpmPath = c;

            return null;
        }

        (int code, string all) Npm(string args, string workingSubDir)
        {
            var npm = ResolveNpmPath();
            if (npm == null)
            {
                Log("找不到 npm。装 Node.js（nodejs.org 的 LTS）之后再试。\n" +
                    "  已经装过的话点一下上面的「重新查找 npm」—— 不用重启 Unity。");
                return (-1, "");
            }

            Log($"$ npm {args}");
            var r = RunProcess(npm, args, workingSubDir, Path.GetDirectoryName(npm));
            if (!string.IsNullOrWhiteSpace(r.all)) Log(Indent(r.all));
            if (r.code != 0) Log($"  ↑ 退出码 {r.code}");
            else Log("✅ 完成。宿主工程现在能在 Package Manager 里看到这个版本了。");
            return (r.code, r.all);
        }

        (int code, string all) NpmQuiet(string args)
        {
            var npm = ResolveNpmPath();
            if (npm == null) return (-1, "找不到 npm");

            var r = RunProcess(npm, args, null, Path.GetDirectoryName(npm));
            return (r.code, r.all);
        }

        // ------------------------------------------------------------ 进程

        /// <param name="workingSubDir">相对工程根的子目录，null = 工程根。</param>
        /// <param name="extraPathDir">额外塞进子进程 PATH 的目录（npm 要靠它找到旁边的 node.exe）。</param>
        (int code, string stdout, string stderr, string all) RunProcess(
            string exe, string args, string workingSubDir = null, string extraPathDir = null)
        {
            var root = m_ProjectRoot ?? Path.GetDirectoryName(Application.dataPath)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(workingSubDir))
                root = Path.Combine(root ?? "", workingSubDir).Replace('\\', '/');

            try
            {
                var psi = new ProcessStartInfo(exe, args)
                {
                    WorkingDirectory = root,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8,
                };

                // npm.cmd 要靠 PATH 找到旁边的 node.exe。Unity 进程的 PATH 可能是旧的，
                // 所以把 npm 所在目录显式塞到子进程 PATH 最前面。
                if (!string.IsNullOrEmpty(extraPathDir))
                {
                    var existing = psi.EnvironmentVariables.ContainsKey("PATH")
                        ? psi.EnvironmentVariables["PATH"] : "";
                    psi.EnvironmentVariables["PATH"] = extraPathDir + Path.PathSeparator + existing;
                }

                using (var p = new Process { StartInfo = psi })
                {
                    var so = new StringBuilder();
                    var se = new StringBuilder();

                    p.OutputDataReceived += (_, e) => { if (e.Data != null) so.AppendLine(e.Data); };
                    p.ErrorDataReceived += (_, e) => { if (e.Data != null) se.AppendLine(e.Data); };

                    p.Start();
                    p.BeginOutputReadLine();
                    p.BeginErrorReadLine();

                    if (!p.WaitForExit(120000))
                    {
                        try { p.Kill(); } catch { }
                        return (-1, "", "超时（120s）", $"{exe} 执行超时");
                    }
                    p.WaitForExit();   // 等异步读取的回调收尾

                    var all = (so.ToString() + se.ToString()).TrimEnd();
                    return (p.ExitCode, so.ToString(), se.ToString(), all);
                }
            }
            catch (Exception e)
            {
                var msg = $"调不起 {exe}：{e.Message}\n确认它已安装并在 PATH 里"
                          + (exe.StartsWith("npm") ? "（npm 随 Node.js 一起装）。" : "。");
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


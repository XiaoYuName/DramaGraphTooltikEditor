using System;
using System.Collections.Generic;
using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Drama.Runtime
{
    // ============================================================================
    //  运行时剧情数据模型
    //
    //  依赖：UnityEngine + Odin(Attributes) + DOTween。
    //  【不】依赖 UnityEditor、GraphToolkit，也不依赖本工程的任何编辑器类型。
    //  整个 Drama.Runtime 程序集是要拷到别的 Unity 工程里跑的，
    //  目标工程必须同样装有 Odin 和 DOTween。
    //
    //  ⚠️ 跨工程注意事项：
    //   1. [SerializeReference] 按 { 类名, 命名空间, 程序集名 } 反序列化。
    //      改类名 / 命名空间 / asmdef 名字 → 已导出资产里对应指令全部变 null。
    //   2. DramaScript 资产的 m_Script 是 .cs 文件的 GUID，两个工程必须一致。
    //      → 建议把本目录抽成 UPM 包共享。
    // ============================================================================

    #region 执行语义

    // ------------------------------------------------------------------------
    //  ★ 并行 / 串行语义（整套结构的核心）
    //
    //  编辑器里一个流程输出端口可以连多条线。约定：
    //
    //    连 1 条  → 【串行】本条完全执行完毕（含动画/等待）后，才执行下一条
    //    连 N 条  → 【并行】本条执行完后，N 条同时开始，互不等待
    //
    //  对应到数据结构就是 DramaAction.Next 数组的长度：
    //    0 → 结束 ｜ 1 → 串行 ｜ >1 → 并行
    //
    //  汇合：多条支线连到同一节点时该指令 InboundCount > 1，
    //  运行时应等所有入边到齐后只执行一次。
    //
    //  运行时执行器骨架：
    //
    //      async UniTask Run(int index) {
    //          var a = script.Actions[index];
    //          if (a.InboundCount > 1 && !CountdownArrived(a)) return;   // 汇合点等齐
    //          await Execute(a);                                         // 真正干活
    //          if (a.Next.Length == 1) await Run(a.Next[0]);             // 串行
    //          else if (a.Next.Length > 1)                               // 并行
    //              await UniTask.WhenAll(a.Next.Select(Run));
    //      }
    // ------------------------------------------------------------------------

    #endregion

    #region 基础类型

    /// <summary>多语言引用。指向本地化表里的一条。</summary>
    [Serializable]
    public struct LocalizedRef
    {
        [LabelText("多语言表")] public string Table;
        [LabelText("多语言键")] public string Key;

        public bool IsEmpty => string.IsNullOrEmpty(Table) && string.IsNullOrEmpty(Key);
        public override string ToString() => IsEmpty ? "(空)" : $"{Table}/{Key}";
    }

    /// <summary>说话人寻址方式。</summary>
    public enum ESpeakerKind
    {
        [LabelText("旁白")]     Aside = 0,
        [LabelText("主角")]     Hero = 1,
        [LabelText("自定义")]   Custom = 2,
        [LabelText("指定角色")] Actor = 3,
    }

    public enum EBalloonKind
    {
        [LabelText("无动效")] Normal = 0,
        [LabelText("抖动")]   Shake = 1,
        [LabelText("震撼")]   Shock = 2,
    }

    public enum EActorShowKind
    {
        [LabelText("直接显示")] Show = 0,
        [LabelText("直接隐藏")] Hide = 1,
        [LabelText("淡入")]     FadeIn = 2,
        [LabelText("淡出")]     FadeOut = 3,
    }

    public enum EActorShowDirection
    {
        [LabelText("左")] Left = 0,
        [LabelText("右")] Right = 1,
        [LabelText("中")] Center = 2,
    }

    /// <summary>
    /// 立绘用哪种资源。决定运行时实例化哪种立绘，以及去角色表的哪个字段取路径。
    ///
    /// <b>Spine 必须是 0</b>：这个字段是后加的，之前导出的资产里没有它，
    /// 反序列化后会落到默认值 0 —— 那时候只有 Spine 一种，落到 Spine 才是对的。
    /// </summary>
    public enum EActorAssetKind
    {
        [LabelText("骨骼")]  Spine = 0,
        [LabelText("图片")]  Texture = 1,
        [LabelText("Live2D")] Live2D = 2,
    }

    public enum EShakeAxis
    {
        [LabelText("位置 XY")] PositionXY = 0,
        [LabelText("位置 Z")]  PositionZ = 1,
        [LabelText("旋转")]    Rotation = 2,
    }

    public enum ETransitionPhase
    {
        [LabelText("淡入")]     In = 0,
        [LabelText("淡出")]     Out = 1,
        [LabelText("淡入淡出")] InOut = 2,
    }

    public enum EScreenTransitionKind
    {
        [LabelText("淡入淡出")] Fade = 0,
        [LabelText("竖条")]     VenetianBlind = 1,
        [LabelText("百叶窗")]   Comb = 2,
    }

    public enum EBgTransitionKind
    {
        [LabelText("无")]       None = 0,
        [LabelText("淡入淡出")] Fade = 1,
        [LabelText("竖条")]     VenetianBlind = 2,
        [LabelText("百叶窗")]   Comb = 3,
    }

    public enum ETalkFrame
    {
        [LabelText("普通")]  Normal = 0,
        [LabelText("CG 版")] HCG = 1,
    }

    #endregion

    #region 指令基类

    /// <summary>
    /// 一条剧情指令。用 <c>[SerializeReference]</c> 多态存进 <see cref="DramaScript.Actions"/>。
    /// </summary>
    [Serializable]
    public abstract class DramaAction
    {
        /// <summary>指令类型名，调试用。</summary>
        public abstract string Kind { get; }

        /// <summary>列表里显示的一行摘要。</summary>
        public virtual string Summary => Kind;

        // ---------------- 流程结构（导出器生成，不要手改） ----------------

        [FoldoutGroup("流程结构", Order = 100)]
        [LabelText("下标"), ReadOnly]
        public int Index = -1;

        [FoldoutGroup("流程结构")]
        [LabelText("后继"), ReadOnly]
        [Tooltip("长度 0 = 结束；1 = 串行；>1 = 并行同时启动")]
        public int[] Next = Array.Empty<int>();

        [FoldoutGroup("流程结构")]
        [LabelText("入边数"), ReadOnly]
        [Tooltip(">1 表示这是并行支线的汇合点，运行时要等所有入边到齐才执行一次")]
        public int InboundCount = 1;

        [FoldoutGroup("流程结构")]
        [ShowInInspector, LabelText("执行方式"), ReadOnly]
        public string FlowMode
        {
            get
            {
                var n = Next?.Length ?? 0;
                var head = n == 0 ? "结束" : (n == 1 ? "串行" : $"并行 ×{n}");
                return InboundCount > 1 ? $"{head}（汇合点，等 {InboundCount} 条入边）" : head;
            }
        }

        public bool IsParallelFork => Next != null && Next.Length > 1;
        public bool IsJoin => InboundCount > 1;

        /// <summary>
        /// 除 <see cref="Next"/> 之外的跳转目标（目前只有选项分支用）。
        ///
        /// 这里描述的是【图结构】而不是执行逻辑 —— 运行时要靠它算可达性和汇合点，
        /// 所以必须放在数据层，不能藏在 Handler 里。
        /// </summary>
        public virtual void CollectBranchTargets(List<int> into) { }
    }

    #endregion

    #region 对话

    /// <summary>说一句台词。等玩家点击或自动等待后推进。</summary>
    [Serializable]
    public sealed class TalkAction : DramaAction
    {
        public override string Kind => "台词";

        [LabelText("文本")] public LocalizedRef Text;
        [LabelText("语音")] public LocalizedRef Voice;

        [LabelText("说话人")] public ESpeakerKind Speaker;

        [LabelText("自定义名字"), ShowIf(nameof(Speaker), ESpeakerKind.Custom)]
        public LocalizedRef SpeakerName;

        [LabelText("角色ID"), ShowIf(nameof(Speaker), ESpeakerKind.Actor)]
        public int ActorId;

        [LabelText("对话框动效")] public EBalloonKind Balloon;
        [LabelText("自动等待(秒)"), Tooltip("0 = 等玩家点击")] public float AutoWaitSeconds;
        [LabelText("名字颜色")] public Color NameColor = Color.white;

        public override string Summary => $"台词 · {Speaker} · {Text}";
    }

    /// <summary>显示 / 隐藏对话框。</summary>
    [Serializable]
    public sealed class TalkShowAction : DramaAction
    {
        public override string Kind => "对话框显隐";
        [LabelText("显示")] public bool Show = true;
        public override string Summary => Show ? "对话框 · 显示" : "对话框 · 隐藏";
    }

    /// <summary>切换对话框皮肤。</summary>
    [Serializable]
    public sealed class SetTalkFrameAction : DramaAction
    {
        public override string Kind => "对话框样式";
        [LabelText("样式")] public ETalkFrame Frame;
        public override string Summary => $"对话框样式 · {Frame}";
    }

    #endregion

    #region 流程

    /// <summary>等待一段时间。</summary>
    [Serializable]
    public sealed class WaitAction : DramaAction
    {
        public override string Kind => "等待";
        [LabelText("时长(秒)")] public float Seconds;
        public override string Summary => $"等待 · {Seconds:0.###}s";
    }

    /// <summary>
    /// 停下来等玩家推进一步（点一下屏幕）。
    ///
    /// <b>给没有台词的场合用</b>——比如整屏 CG：画面已经摆好了，但不该自己往下走，
    /// 要等玩家看够了点一下。有台词的时候不需要它，台词本身就会等
    /// （见 <see cref="TalkAction"/>）。
    ///
    /// <b>"点一下"具体是什么输入，是宿主的事。</b> 本指令只表达"等玩家推进"，
    /// 走的和台词翻页同一个口子（<see cref="Services.IDialogueView.WaitForAdvanceAsync"/>）——
    /// 宿主以后想让空格 / Esc 也算数，在那个实现里多接几个键就行，这一层不用改。
    ///
    /// <b>没有"最多等几秒"这种字段</b>：要定时往下走就用「等待」指令
    /// （<see cref="WaitAction"/>），两条指令各管一件事。
    /// </summary>
    [Serializable]
    public sealed class WaitInputAction : DramaAction
    {
        public override string Kind => "等待点击";
        public override string Summary => "等待点击";
    }

    /// <summary>本剧本结束，跳到另一个剧本。</summary>
    [Serializable]
    public sealed class GotoDramaAction : DramaAction
    {
        public override string Kind => "跳转剧本";
        [LabelText("目标剧本ID"), Tooltip("<= 0 表示直接结束，不接后续")] public long DramaId = -1;
        public override string Summary => DramaId > 0 ? $"跳转剧本 · {DramaId}" : "结束";
    }

    /// <summary>玩家多选一。每个选项各自跳到一条支线。</summary>
    [Serializable]
    public sealed class ChoiceAction : DramaAction
    {
        public override string Kind => "选项分支";

        [Serializable]
        public struct Option
        {
            [LabelText("选项文字")] public LocalizedRef Text;
            [LabelText("跳转到"), ReadOnly, Tooltip("目标指令下标；-1 = 没接东西")] public int Next;
        }

        [LabelText("选项")]
        [ListDrawerSettings(ShowFoldout = true, IsReadOnly = true)]
        public Option[] Options = Array.Empty<Option>();

        public override string Summary => $"选项分支 ×{Options?.Length ?? 0}";

        /// <summary>选项的跳转目标不走 <see cref="DramaAction.Next"/>，得单独报给流程层。</summary>
        public override void CollectBranchTargets(List<int> into)
        {
            if (Options == null) return;
            foreach (var o in Options)
                if (o.Next >= 0) into.Add(o.Next);
        }
    }

    /// <summary>
    /// 设置立绘 Animator 的参数。Live2D 的表情 / 动作走 Unity 状态机，靠这组指令驱动。
    ///
    /// 拆成四条而不是一条带类型字段的：Unity 的 <c>SetBool</c> / <c>SetInteger</c> /
    /// <c>SetFloat</c> / <c>SetTrigger</c> 值类型各不相同，合成一条就得塞三个值字段
    /// 外加一个"看哪个"的枚举，读的人和写的人都要多绕一层。
    /// </summary>
    [Serializable]
    public sealed class ActorAnimBoolAction : DramaAction
    {
        public override string Kind => "立绘Animator Bool";
        [LabelText("角色ID")] public int ActorId = -1;
        [LabelText("参数名")] public string ParameterName;
        [LabelText("值")]     public bool Value;
        public override string Summary => $"Animator · {ParameterName} = {Value}";
    }

    /// <inheritdoc cref="ActorAnimBoolAction"/>
    [Serializable]
    public sealed class ActorAnimIntAction : DramaAction
    {
        public override string Kind => "立绘Animator Int";
        [LabelText("角色ID")] public int ActorId = -1;
        [LabelText("参数名")] public string ParameterName;
        [LabelText("值")]     public int Value;
        public override string Summary => $"Animator · {ParameterName} = {Value}";
    }

    /// <inheritdoc cref="ActorAnimBoolAction"/>
    [Serializable]
    public sealed class ActorAnimFloatAction : DramaAction
    {
        public override string Kind => "立绘Animator Float";
        [LabelText("角色ID")] public int ActorId = -1;
        [LabelText("参数名")] public string ParameterName;
        [LabelText("值")]     public float Value;
        public override string Summary => $"Animator · {ParameterName} = {Value}";
    }

    /// <inheritdoc cref="ActorAnimBoolAction"/>
    [Serializable]
    public sealed class ActorAnimTriggerAction : DramaAction
    {
        public override string Kind => "立绘Animator Trigger";
        [LabelText("角色ID")] public int ActorId = -1;
        [LabelText("参数名")] public string ParameterName;

        /// <summary>true = ResetTrigger，撤掉一个还没被状态机消费掉的触发。</summary>
        [LabelText("重置")] public bool Reset;

        public override string Summary =>
            Reset ? $"Animator · 重置 {ParameterName}" : $"Animator · 触发 {ParameterName}";
    }

    /// <summary>领取任务。</summary>
    [Serializable]
    public sealed class ReceiveTaskAction : DramaAction
    {
        public override string Kind => "领取任务";
        [LabelText("任务ID")] public long TaskId = -1;
        public override string Summary => $"领取任务 · {TaskId}";
    }

    /// <summary>
    /// 发一份奖励，并弹出"获得奖励"界面。
    ///
    /// 奖励内容由宿主的奖励表决定，这里只给 ID。发放和弹窗都归宿主
    /// （见 <see cref="Services.IDramaGameBridge.ShowRewardAsync"/>）。
    ///
    /// <b>手动模式下会停下来等玩家关掉弹窗</b>，自动 / 跳过模式下弹窗自己收掉、剧情继续。
    /// <b>读档的静默重放期间整条指令跳过</b> —— 奖励当年已经发过了，
    /// 再发一次玩家每读一次档就白拿一份。
    /// </summary>
    [Serializable]
    public sealed class ReceiveRewardAction : DramaAction
    {
        public override string Kind => "获取奖励";
        [LabelText("奖励表ID")] public long RewardId = -1;
        public override string Summary => $"获取奖励 · {RewardId}";
    }

    /// <summary>
    /// 切换<b>游戏内的真实场景</b>，不是剧情的背景图。
    ///
    /// 和 <c>ChangeBackgroundAction</c> 的区别：那个只是换剧情舞台后面那张图，
    /// 这个会真的去加载 / 卸载场景。放在"流程"而不是"场景 / 演出"里，
    /// 是因为它动的是宿主的世界状态，不是这一幕的表现。
    /// </summary>
    [Serializable]
    public sealed class ChangeGameSceneAction : DramaAction
    {
        public override string Kind => "游戏场景";

        /// <summary>大场景（地图）ID。小于等于 0 = 留在当前大场景里换小场景。</summary>
        [LabelText("大场景ID")] public long MapSceneId = -1;

        /// <summary>小场景ID。</summary>
        [LabelText("小场景ID")] public long MinSceneId = -1;

        public override string Summary => MapSceneId > 0
            ? $"游戏场景 · {MapSceneId} / {MinSceneId}"
            : $"游戏场景 · 换小场景 {MinSceneId}";
    }

    /// <summary>
    /// 游戏场景里那些"和剧情无关的东西"的显隐：场景 NPC、地图配置的场景默认UI。
    ///
    /// <b>不是一次性的，是个持续状态。</b> 剧情期间宿主默认把两者都收起来（要独占屏幕），
    /// 而 <see cref="ChangeGameSceneAction"/> 切场景会重新生成 NPC、重新开默认UI ——
    /// 所以这条指令改的是"意图"，宿主每次场景就绪时按它重新应用一遍。
    ///
    /// 正因为是持续的，<b>切场景节点上不需要再带一份显隐参数</b>：
    /// 想让新场景露出 NPC，在切场景之前摆一条本指令即可，切完自动生效、不会闪。
    /// </summary>
    [Serializable]
    public sealed class SceneVisibilityAction : DramaAction
    {
        public override string Kind => "场景显隐";

        [LabelText("显示场景NPC")] public bool ShowNpc = true;

        /// <summary>地图配置里那个「场景默认UI」。不是剧情自己的界面。</summary>
        [LabelText("显示场景默认UI")] public bool ShowSceneUI = true;

        public override string Summary =>
            $"场景显隐 · NPC{(ShowNpc ? "显示" : "隐藏")} · 默认UI{(ShowSceneUI ? "显示" : "隐藏")}";
    }

    /// <summary>
    /// 打开一个界面，<b>等玩家把它关掉再往下走</b>。
    ///
    /// 和 <see cref="EndUIDramaAction"/> 的区别是时机：那条是"剧情结束了顺便开个界面"、
    /// 开完剧情就没了；这条是剧情<b>中途</b>插一个界面，玩家关掉之后剧情接着演。
    ///
    /// 等待方式和 <see cref="ReceiveRewardAction"/> 一样：正常模式等玩家关，
    /// 自动 / 跳过模式下界面自己收掉、剧情不停（见
    /// <see cref="Services.IDramaGameBridge.ShowUIAsync"/>）。
    /// 读档的静默重放期间整条跳过 —— 重放不该往玩家脸上弹界面。
    /// </summary>
    [Serializable]
    public sealed class ShowUIAction : DramaAction
    {
        /// <summary>宿主 UI 系统里的界面ID（就是界面名，比如 "MainUI"）。</summary>
        [LabelText("界面ID")] public string UiPage;

        public override string Kind => "打开界面";

        public override string Summary =>
            string.IsNullOrEmpty(UiPage) ? "打开界面 · (没填)" : $"打开界面 · {UiPage}";
    }

    /// <summary>
    /// 剧情结束并打开一段引导。
    ///
    /// 和 <see cref="EndUIDramaAction"/> 是姊妹指令，区别只是打开的东西不一样
    /// （一个是界面名、一个是引导ID）。同样<b>没有后继</b>，执行完剧情就结束了；
    /// 也同样只是把 ID 报给宿主，真正开始引导的时机由宿主的收尾流程决定。
    /// </summary>
    [Serializable]
    public sealed class EndGuideDramaAction : DramaAction
    {
        public override string Kind => "引导结束";

        /// <summary>引导表的 ID。小于等于 0 = 只结束，不开引导。</summary>
        [LabelText("引导ID")] public long GuideId = -1;

        public override string Summary => GuideId > 0 ? $"结束 · 引导 {GuideId}" : "结束";
    }

    /// <summary>
    /// 剧情结束并打开一个界面。
    ///
    /// <b>这条指令自己不打开 UI</b>，只是把"结束之后开哪个"报上去 ——
    /// 剧情 UI 的收尾（关剧情面板、把进剧情前那批界面还回去）发生在整段播放<b>之后</b>，
    /// 在指令里当场打开的话会被紧接着的收尾盖掉或压到底层。
    ///
    /// 它没有输出流程端口，所以执行完 <c>Next</c> 为空、剧情到此为止 ——
    /// "结束"从来不是一条指令，而是"没有后继"这个状态。
    /// </summary>
    [Serializable]
    public sealed class EndUIDramaAction : DramaAction
    {
        public override string Kind => "UI结束";

        /// <summary>宿主 UI 系统里的界面名。空 = 只结束，不开界面。</summary>
        [LabelText("界面名")] public string UiPage;

        public override string Summary =>
            string.IsNullOrEmpty(UiPage) ? "结束" : $"结束 · 打开 {UiPage}";
    }

    #endregion

    #region 立绘

    /// <summary>立绘出现 / 消失。</summary>
    [Serializable]
    public sealed class ActorShowAction : DramaAction
    {
        public override string Kind => "立绘显隐";

        [LabelText("角色ID")]   public int ActorId;

        /// <summary>
        /// 用哪种立绘。图里是三个不同的节点（立绘骨骼 / 立绘图 / 立绘Live2D），
        /// 到了数据这一层收敛成同一条指令 + 一个类型字段 —— 三种立绘除了资源类型，
        /// 摆位和显隐参数完全一样，拆成三条指令会把 Handler 和舞台逻辑也复制三份。
        /// </summary>
        [LabelText("立绘类型")] public EActorAssetKind AssetKind;

        [LabelText("显示方式")] public EActorShowKind ShowKind;
        [LabelText("方向")]     public EActorShowDirection Direction;
        [LabelText("位置")]     public Vector2 Position;
        /// <summary>
        /// 缩放倍率，1 = 原始大小。
        ///
        /// ⚠️ 0.1.10 之前这个字段叫 <c>ScalePercent</c>、单位是百分比（100 = 原始大小），
        /// 和 <see cref="ActorScaleAction.Scale"/> 的倍率语义对不上，策划在两个节点里
        /// 填同一个数会得到差 100 倍的结果。改名是为了让旧资产读不到这个字段、
        /// 直接落到默认值 1（正常大小），而不是静默按 0.01 倍显示。
        /// <b>改完要全量重新导出。</b>
        /// </summary>
        [LabelText("缩放"), Tooltip("1 = 原始大小")] public Vector2 Scale = Vector2.one;
        [LabelText("时长(秒)")] public float DurationSeconds;
        [LabelText("缓动")]     public Ease Ease = Ease.Linear;
        // 0.2.1 起没有"等待动画结束"这个字段了：
        // 「不等动画」和「把下一条连成并行分支」是同一件事，图的拓扑（Next.Length > 1）
        // 已经能表达，再来个 bool 只会和拓扑打架。本指令一律等动画跑完。

        public override string Summary => $"立绘{ShowKind} · 角色{ActorId}";
    }

    /// <summary>立绘移动到指定位置。</summary>
    [Serializable]
    public sealed class ActorMoveAction : DramaAction
    {
        public override string Kind => "立绘位置";
        [LabelText("角色ID")]   public int ActorId;
        [LabelText("目标位置")] public Vector2 Position;
        [LabelText("时长(秒)")] public float DurationSeconds;
        [LabelText("缓动")]     public Ease Ease = Ease.Linear;
        public override string Summary => $"立绘位置 · 角色{ActorId} → {Position}";
    }

    /// <summary>立绘缩放。</summary>
    [Serializable]
    public sealed class ActorScaleAction : DramaAction
    {
        public override string Kind => "立绘缩放";
        [LabelText("角色ID")]   public int ActorId;
        /// <summary>倍率，1 = 原始大小。默认给 one 而不是 zero —— 漏填会把立绘缩成看不见。</summary>
        [LabelText("目标缩放")] public Vector3 Scale = Vector3.one;
        [LabelText("时长(秒)")] public float DurationSeconds;
        [LabelText("缓动")]     public Ease Ease = Ease.Linear;
        public override string Summary => $"立绘缩放 · 角色{ActorId} → {Scale}";
    }

    /// <summary>立绘旋转（欧拉角）。</summary>
    [Serializable]
    public sealed class ActorRotateAction : DramaAction
    {
        public override string Kind => "立绘旋转";
        [LabelText("角色ID")]   public int ActorId;
        [LabelText("目标角度")] public Vector3 Rotation;
        [LabelText("时长(秒)")] public float DurationSeconds;
        [LabelText("缓动")]     public Ease Ease = Ease.Linear;
        public override string Summary => $"立绘旋转 · 角色{ActorId} → {Rotation}";
    }

    /// <summary>相对当前位置做偏移小动作（可循环）。</summary>
    [Serializable]
    public sealed class ActorOffsetMoveAction : DramaAction
    {
        public override string Kind => "立绘小动作";
        [LabelText("角色ID")]   public int ActorId;
        [LabelText("偏移")]     public Vector3 Offset;
        [LabelText("时长(秒)")] public float DurationSeconds;
        [LabelText("缓动")]     public Ease Ease = Ease.Linear;
        [LabelText("次数")]     public int LoopCount = 1;
        [LabelText("循环方式")] public LoopType LoopType = LoopType.Restart;
        public override string Summary => $"小动作 · 角色{ActorId} · 偏移{Offset}";
    }

    /// <summary>抖动一段时间后停止。</summary>
    [Serializable]
    public sealed class ActorShakeAction : DramaAction
    {
        public override string Kind => "立绘抖动";
        [LabelText("角色ID")]   public int ActorId;
        [LabelText("振幅")]     public float Amplitude = 0.5f;
        [LabelText("轴")]       public EShakeAxis Axis;
        [LabelText("时长(秒)")] public float DurationSeconds = 0.3f;
        [LabelText("结束归位")] public bool RestoreOnEnd = true;
        public override string Summary => $"抖动 · 角色{ActorId} · {DurationSeconds:0.##}s";
    }

    /// <summary>按间隔持续震动。</summary>
    [Serializable]
    public sealed class ActorVibrateAction : DramaAction
    {
        public override string Kind => "立绘震动";
        [LabelText("角色ID")]   public int ActorId;
        [LabelText("振幅")]     public float Amplitude = 0.5f;
        [LabelText("轴")]       public EShakeAxis Axis;
        [LabelText("间隔(秒)")] public float IntervalSeconds = 0.3f;
        [LabelText("时长(秒)")] public float DurationSeconds = 0.3f;
        /// <summary>趋近目标点的速度，越大越"硬"。原实现 ≤0 兜底 5。</summary>
        [LabelText("平滑速度")] public float SmoothSpeed = 5f;
        [LabelText("结束归位")] public bool RestoreOnEnd = true;
        public override string Summary => $"震动 · 角色{ActorId} · {DurationSeconds:0.##}s";
    }

    /// <summary>换 Spine 皮肤。</summary>
    [Serializable]
    public sealed class ActorSetSkinAction : DramaAction
    {
        public override string Kind => "立绘皮肤";
        [LabelText("角色ID")] public int ActorId;
        [LabelText("皮肤名")] public string SkinName;
        public override string Summary => $"皮肤 · 角色{ActorId} → {SkinName}";
    }

    /// <summary>播 Spine 动画。</summary>
    [Serializable]
    public sealed class ActorPlayAnimationAction : DramaAction
    {
        public override string Kind => "立绘动画";
        [LabelText("角色ID")] public int ActorId;
        [LabelText("动画名")] public string AnimationName;
        [LabelText("轨道")]   public int TrackIndex = 1;
        [LabelText("循环")]   public bool Loop;
        [LabelText("倍速")]   public float TimeScale = 1f;

        public override string Summary =>
            string.IsNullOrEmpty(AnimationName)
                ? $"动画 · 角色{ActorId} · 轨道{TrackIndex} · (未指定动画名)"
                : $"动画 · 角色{ActorId} · {AnimationName} · 轨道{TrackIndex}";
    }

    /// <summary>
    /// 「非说话人压暗 / 微缩」这套效果的总开关。
    ///
    /// <b>不针对任何角色，是全局设置。</b> 本指令只负责开关，
    /// 真正的应用发生在每条 <see cref="TalkAction"/> 上 —— 舞台按当前说话人，
    /// 把说话人恢复原样、其他人压暗微缩（见 <see cref="Services.IActorStage.SetSpeaker"/>）。
    ///
    /// 所以剧本里一般在开头设一次就够，中途想临时关掉某种效果再设一次。
    /// </summary>
    [Serializable]
    public sealed class ActorHighlightAction : DramaAction
    {
        public override string Kind => "讲话人突出";

        [LabelText("压暗非说话人")] public bool Gray;

        /// <summary>压暗到多少亮度，1 = 不压暗。旧工程写死 0.8。</summary>
        [LabelText("压暗亮度"), Tooltip("1 = 原始亮度，越小越暗")]
        public float DimBrightness = 0.8f;

        [LabelText("微缩非说话人")] public bool Shrink;

        /// <summary>非说话人缩到多少倍，1 = 不缩。旧工程写死 0.95。</summary>
        [LabelText("微缩倍率"), Tooltip("1 = 原始大小")]
        public float ShrinkScale = 0.95f;

        public override string Summary =>
            $"讲话人突出 · 压暗{(Gray ? $"{DimBrightness:0.##}" : "关")} 微缩{(Shrink ? $"{ShrinkScale:0.##}" : "关")}";
    }

    #endregion

    #region CG

    // CG = 全屏的一张大图（本工程是全屏 Live2D）。它和立绘的关系不是"另一种立绘"：
    //   · 寻址不同 —— CG 有自己的配置表，一张双人 CG 属于"哪个角色"是答不上来的
    //   · 单槽位   —— 同时只有一张，所以这一族指令全都不需要 ID
    //   · 进入时自动把立绘整层藏掉，退出时恢复（照原工程的做法，漏配一条就穿帮）
    //
    // 参数结构刻意和立绘那套保持一致（位置/缩放/旋转/小动作/抖动/震动），
    // 图里也是复用同一批 Block —— 是容器决定语义，不是块。

    /// <summary>
    /// CG 出现。<b>时长为 0 就是瞬时显示</b>。
    ///
    /// 不像 <see cref="ActorShowAction"/> 那样带个"显示方式"枚举：
    /// 那边一条指令要表达显示/隐藏/淡入/淡出四种组合，这边显示和隐藏是两条指令，
    /// 剩下的"瞬时还是带动画"用时长就够了。
    /// </summary>
    [Serializable]
    public sealed class CGShowAction : DramaAction
    {
        public override string Kind => "CG出现";
        [LabelText("CG ID")]    public long CgId = -1;
        [LabelText("时长(秒)")] public float DurationSeconds;
        [LabelText("缓动")]     public Ease Ease = Ease.Linear;
        public override string Summary => $"CG出现 · {CgId}";
    }

    /// <summary>CG 关闭，同时把立绘层恢复回来。时长为 0 就是瞬时。</summary>
    [Serializable]
    public sealed class CGHideAction : DramaAction
    {
        public override string Kind => "CG关闭";
        [LabelText("时长(秒)")] public float DurationSeconds;
        [LabelText("缓动")]     public Ease Ease = Ease.Linear;
        public override string Summary => "CG关闭";
    }

    /// <summary>CG 位移。全屏 CG 默认满屏居中，这里给的是在此基础上的偏移。</summary>
    [Serializable]
    public sealed class CGMoveAction : DramaAction
    {
        public override string Kind => "CG位置";
        [LabelText("目标位置")] public Vector2 Position;
        [LabelText("时长(秒)")] public float DurationSeconds;
        [LabelText("缓动")]     public Ease Ease = Ease.Linear;
        public override string Summary => $"CG位置 · → {Position}";
    }

    /// <summary>CG 缩放。做"推近"这类镜头感用。</summary>
    [Serializable]
    public sealed class CGScaleAction : DramaAction
    {
        public override string Kind => "CG缩放";
        /// <summary>倍率，1 = 原始大小。默认给 one 而不是 zero —— 漏填会把 CG 缩成看不见。</summary>
        [LabelText("目标缩放")] public Vector3 Scale = Vector3.one;
        [LabelText("时长(秒)")] public float DurationSeconds;
        [LabelText("缓动")]     public Ease Ease = Ease.Linear;
        public override string Summary => $"CG缩放 · → {Scale}";
    }

    /// <summary>CG 旋转（欧拉角）。</summary>
    [Serializable]
    public sealed class CGRotateAction : DramaAction
    {
        public override string Kind => "CG旋转";
        [LabelText("目标角度")] public Vector3 Rotation;
        [LabelText("时长(秒)")] public float DurationSeconds;
        [LabelText("缓动")]     public Ease Ease = Ease.Linear;
        public override string Summary => $"CG旋转 · → {Rotation}";
    }

    /// <summary>
    /// CG 相对当前位置做偏移小动作（可循环）。
    /// 无限循环 + 往复就是缓慢平移的呼吸感镜头，语义见 <see cref="ActorOffsetMoveAction"/>。
    /// </summary>
    [Serializable]
    public sealed class CGOffsetMoveAction : DramaAction
    {
        public override string Kind => "CG小动作";
        [LabelText("偏移")]     public Vector3 Offset;
        [LabelText("时长(秒)")] public float DurationSeconds;
        [LabelText("缓动")]     public Ease Ease = Ease.Linear;
        [LabelText("次数")]     public int LoopCount = 1;
        [LabelText("循环方式")] public LoopType LoopType = LoopType.Restart;
        public override string Summary => $"CG小动作 · 偏移{Offset}";
    }

    /// <summary>CG 抖动（硬抖）。</summary>
    [Serializable]
    public sealed class CGShakeAction : DramaAction
    {
        public override string Kind => "CG抖动";
        [LabelText("振幅")]     public float Amplitude = 0.5f;
        [LabelText("轴")]       public EShakeAxis Axis;
        [LabelText("时长(秒)")] public float DurationSeconds = 0.3f;
        [LabelText("结束归位")] public bool RestoreOnEnd = true;
        public override string Summary => $"CG抖动 · {DurationSeconds:0.##}s";
    }

    /// <summary>CG 震动（柔震）。</summary>
    [Serializable]
    public sealed class CGVibrateAction : DramaAction
    {
        public override string Kind => "CG震动";
        [LabelText("振幅")]     public float Amplitude = 0.5f;
        [LabelText("轴")]       public EShakeAxis Axis;
        [LabelText("间隔(秒)")] public float IntervalSeconds = 0.3f;
        [LabelText("时长(秒)")] public float DurationSeconds = 0.3f;
        [LabelText("平滑速度")] public float SmoothSpeed = 5f;
        [LabelText("结束归位")] public bool RestoreOnEnd = true;
        public override string Summary => $"CG震动 · {DurationSeconds:0.##}s";
    }

    /// <summary>
    /// 设置 CG 模型 Animator 的参数。CG 是全屏 Live2D，表情 / 动作走 Unity 状态机。
    ///
    /// 和立绘那四条的唯一区别是<b>没有 ID</b>：CG 单槽位，加了也只能拿来校验、不能拿来寻址。
    /// 拆成四条而不是一条带类型字段的，理由同 <see cref="ActorAnimBoolAction"/>。
    /// </summary>
    [Serializable]
    public sealed class CGAnimBoolAction : DramaAction
    {
        public override string Kind => "CG Animator Bool";
        [LabelText("参数名")] public string ParameterName;
        [LabelText("值")]     public bool Value;
        public override string Summary => $"CG Animator · {ParameterName} = {Value}";
    }

    /// <inheritdoc cref="CGAnimBoolAction"/>
    [Serializable]
    public sealed class CGAnimIntAction : DramaAction
    {
        public override string Kind => "CG Animator Int";
        [LabelText("参数名")] public string ParameterName;
        [LabelText("值")]     public int Value;
        public override string Summary => $"CG Animator · {ParameterName} = {Value}";
    }

    /// <inheritdoc cref="CGAnimBoolAction"/>
    [Serializable]
    public sealed class CGAnimFloatAction : DramaAction
    {
        public override string Kind => "CG Animator Float";
        [LabelText("参数名")] public string ParameterName;
        [LabelText("值")]     public float Value;
        public override string Summary => $"CG Animator · {ParameterName} = {Value}";
    }

    /// <inheritdoc cref="CGAnimBoolAction"/>
    [Serializable]
    public sealed class CGAnimTriggerAction : DramaAction
    {
        public override string Kind => "CG Animator Trigger";
        [LabelText("参数名")] public string ParameterName;
        /// <summary>true = ResetTrigger，撤掉一个还没被状态机消费掉的触发。</summary>
        [LabelText("重置")]   public bool Reset;
        public override string Summary =>
            Reset ? $"CG Animator · 重置 {ParameterName}" : $"CG Animator · 触发 {ParameterName}";
    }

    #endregion

    #region 场景 / 演出

    /// <summary>全屏转场（淡入淡出 / 百叶窗 / 竖条）。</summary>
    [Serializable]
    public sealed class ScreenTransitionAction : DramaAction
    {
        public override string Kind => "全屏转场";

        [LabelText("转场样式")] public EScreenTransitionKind TransitionKind;
        [LabelText("阶段")]     public ETransitionPhase Phase;
        [LabelText("淡入(秒)")] public float InSeconds = 1f;
        [LabelText("淡出(秒)")] public float OutSeconds = 1f;
        [LabelText("颜色")]     public Color Color = Color.white;
        [LabelText("透明度")]   public float Alpha = 1f;
        [LabelText("缓动")]     public Ease Ease = Ease.Linear;

        public override string Summary => $"转场 · {TransitionKind} · {Phase}";
    }

    /// <summary>切换背景图。</summary>
    [Serializable]
    public sealed class ChangeBackgroundAction : DramaAction
    {
        public override string Kind => "切换背景";
        [LabelText("背景ID")]   public long BackgroundId = -1;
        [LabelText("转场")]     public EBgTransitionKind Transition;
        [LabelText("淡入(秒)")] public float InSeconds;
        [LabelText("淡出(秒)")] public float OutSeconds;
        public override string Summary => $"背景 → {BackgroundId}（{Transition}）";
    }

    // ---- 背景变换（推远 / 平移 / 转一下，做镜头感用）
    //
    // BackgroundId 目前只有一张背景层，实现方大可以忽略它 ——
    // 留着是因为数据一旦导出就不好改（[SerializeReference]），
    // 而接口改起来是免费的。跟 ActorId 一个路子。

    /// <summary>背景位移。</summary>
    [Serializable]
    public sealed class BackgroundMoveAction : DramaAction
    {
        public override string Kind => "背景位置";
        [LabelText("背景ID")]   public long BackgroundId = -1;
        [LabelText("目标位置")] public Vector2 Position;
        [LabelText("时长(秒)")] public float DurationSeconds;
        [LabelText("缓动")]     public Ease Ease = Ease.Linear;
        public override string Summary => $"背景位置 · {BackgroundId} → {Position}";
    }

    /// <summary>背景旋转。</summary>
    [Serializable]
    public sealed class BackgroundRotateAction : DramaAction
    {
        public override string Kind => "背景旋转";
        [LabelText("背景ID")]   public long BackgroundId = -1;
        [LabelText("目标角度")] public Vector3 Rotation;
        [LabelText("时长(秒)")] public float DurationSeconds;
        [LabelText("缓动")]     public Ease Ease = Ease.Linear;
        public override string Summary => $"背景旋转 · {BackgroundId} → {Rotation}";
    }

    /// <summary>背景缩放。</summary>
    [Serializable]
    public sealed class BackgroundScaleAction : DramaAction
    {
        public override string Kind => "背景缩放";
        [LabelText("背景ID")]   public long BackgroundId = -1;
        [LabelText("目标缩放")] public Vector3 Scale = Vector3.one;
        [LabelText("时长(秒)")] public float DurationSeconds;
        [LabelText("缓动")]     public Ease Ease = Ease.Linear;
        public override string Summary => $"背景缩放 · {BackgroundId} → {Scale}";
    }

    /// <summary>播放 BGM。</summary>
    [Serializable]
    public sealed class PlayMusicAction : DramaAction
    {
        public override string Kind => "播放音乐";
        [LabelText("音频ID")] public string MusicId;
        public override string Summary => $"音乐 · {MusicId}";
    }

    #endregion

    #region 剧本资产

    /// <summary>一个剧本。由编辑器工程的导出器生成，运行时只读。</summary>
    public class DramaScript : ScriptableObject
    {
        /// <summary>当前导出器写出的格式版本。运行时读到更高版本应当拒绝加载。</summary>
        public const int CurrentFormatVersion = 2;

        [Title("剧本")]
        [LabelText("剧情ID"), ReadOnly]
        public long DramaId;

        [LabelText("入口指令"), ReadOnly]
        public int EntryIndex = -1;

        [FoldoutGroup("来源信息")]
        [LabelText("格式版本"), ReadOnly]
        public int FormatVersion = CurrentFormatVersion;

        [FoldoutGroup("来源信息")]
        [LabelText("来源图"), ReadOnly]
        public string SourceGraph;

        [Title("指令表")]
        [LabelText("指令")]
        [ListDrawerSettings(ListElementLabelName = "Summary", IsReadOnly = true,
                            ShowFoldout = true, ShowIndexLabels = true)]
        [SerializeReference]
        public List<DramaAction> Actions = new List<DramaAction>();

        public int ActionCount => Actions?.Count ?? 0;

        public DramaAction GetAction(int index)
        {
            if (Actions == null || index < 0 || index >= Actions.Count) return null;
            return Actions[index];
        }

        /// <summary>
        /// 遍历所有可达指令（调试用，不代表真实执行时序）。
        /// 真实执行请按 <see cref="DramaAction.Next"/> 的并行/串行语义驱动。
        /// </summary>
        public IEnumerable<DramaAction> WalkAll()
        {
            if (EntryIndex < 0 || ActionCount == 0) yield break;

            var seen = new HashSet<int>();
            var stack = new Stack<int>();
            stack.Push(EntryIndex);

            while (stack.Count > 0)
            {
                var i = stack.Pop();
                if (i < 0 || i >= ActionCount) continue;
                if (!seen.Add(i)) continue;

                var a = Actions[i];
                if (a == null) continue;
                yield return a;

                if (a.Next == null) continue;
                for (int k = a.Next.Length - 1; k >= 0; k--)
                    stack.Push(a.Next[k]);
            }
        }
    }

    #endregion
}

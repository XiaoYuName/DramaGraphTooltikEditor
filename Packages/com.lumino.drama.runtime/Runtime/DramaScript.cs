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

    /// <summary>领取任务。</summary>
    [Serializable]
    public sealed class ReceiveTaskAction : DramaAction
    {
        public override string Kind => "领取任务";
        [LabelText("任务ID")] public long TaskId = -1;
        public override string Summary => $"领取任务 · {TaskId}";
    }

    #endregion

    #region 立绘

    /// <summary>立绘出现 / 消失。</summary>
    [Serializable]
    public sealed class ActorShowAction : DramaAction
    {
        public override string Kind => "立绘显隐";

        [LabelText("角色ID")]   public int ActorId;
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
        [LabelText("等待动画结束"), Tooltip("不勾则发起动画后立刻继续下一条")] public bool WaitForCompletion = true;

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

    /// <summary>非说话者置灰 / 微缩，突出当前说话人。</summary>
    [Serializable]
    public sealed class ActorHighlightAction : DramaAction
    {
        public override string Kind => "讲话人突出";
        [LabelText("角色ID")] public int ActorId;
        [LabelText("置灰")]   public bool Gray;
        [LabelText("微缩")]   public bool Shrink;
        public override string Summary => $"讲话人突出 · 角色{ActorId}";
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

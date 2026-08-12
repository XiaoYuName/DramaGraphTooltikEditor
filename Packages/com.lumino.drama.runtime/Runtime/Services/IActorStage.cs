using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;

namespace Drama.Runtime.Services
{
    /// <summary>
    /// 一个在台上的立绘。
    ///
    /// <b>刻意不认识 Spine。</b> 宿主工程可以拿 SkeletonGraphic 实现一个、
    /// SkeletonAnimation 实现一个，甚至普通 Sprite 实现一个，
    /// Handler 层完全不需要知道区别。
    /// </summary>
    public interface IActorView
    {
        int ActorId { get; }

        /// <summary>位移 / 缩放 / 旋转 / 抖动都是 DOTween 直接动它。</summary>
        Transform Root { get; }

        void SetAlpha(float alpha);

        /// <summary>
        /// 压暗到指定亮度，<b>1 = 原始亮度</b>。<see cref="ActorHighlightAction"/> 用。
        ///
        /// 收的是数值而不是 bool：具体压到多少由剧本配，"开关 + 数值"的判断在舞台那边做完了
        /// （见 <see cref="IActorStage.SetHighlightMode"/>），立绘只管照数值执行。
        /// 不要动 alpha —— 那是显隐动画的地盘。
        /// </summary>
        void SetDim(float brightness);

        /// <summary>缩到指定倍率，<b>1 = 原始大小</b>。<see cref="ActorHighlightAction"/> 用。</summary>
        void SetShrink(float scale);

        /// <summary>换皮肤。<see cref="ActorSetSkinAction"/> 用。</summary>
        void SetSkin(string skinName);

        /// <summary>
        /// 播动画。<paramref name="loop"/> 为 true 时应当立刻返回（不然永远等不到结束）。
        ///
        /// 三种立绘各自解释：Spine 是 AnimationState 的动画名，
        /// 图片立绘是序列帧的名字，Live2D 一般走 Animator 的状态名。
        /// 不支持的实现打一条警告后返回即可，别抛。
        /// </summary>
        UniTask PlayAnimationAsync(string animationName, int track, bool loop, float timeScale, CancellationToken ct);

        /// <summary>
        /// 设置 Animator 参数。Live2D 的表情 / 动作走 Unity 状态机，靠这组指令驱动。
        ///
        /// <b>没有 Animator 的实现（Spine / 静态图片）应当打一条警告后什么都不做。</b>
        /// 抛异常会把整段剧情打断，而策划在错误的立绘上挂了个 Animator 指令
        /// 是个配置错误，不该升级成播放事故。
        /// </summary>
        void SetAnimatorBool(string parameterName, bool value);

        /// <inheritdoc cref="SetAnimatorBool"/>
        void SetAnimatorInt(string parameterName, int value);

        /// <inheritdoc cref="SetAnimatorBool"/>
        void SetAnimatorFloat(string parameterName, float value);

        /// <summary>
        /// SetTrigger / ResetTrigger。<paramref name="reset"/> 为 true 时是撤销一个
        /// 还没被状态机消费掉的触发 —— 不撤的话它会一直挂着，等走到某个能用它的状态时突然触发。
        /// </summary>
        void SetAnimatorTrigger(string parameterName, bool reset);
    }

    /// <summary>
    /// 「非说话人压暗 / 微缩」的开关和强度。
    ///
    /// 做成结构体而不是四个参数：两个 bool 两个 float 摆一排，
    /// 调用方把两个 float 传反了编译器不会拦你。
    /// </summary>
    public readonly struct ActorHighlightSettings
    {
        /// <summary>要不要压暗非说话人。</summary>
        public readonly bool Dim;

        /// <summary>压暗到多少亮度，1 = 原始亮度。</summary>
        public readonly float DimBrightness;

        /// <summary>要不要微缩非说话人。</summary>
        public readonly bool Shrink;

        /// <summary>缩到多少倍，1 = 原始大小。</summary>
        public readonly float ShrinkScale;

        public ActorHighlightSettings(bool dim, float dimBrightness, bool shrink, float shrinkScale)
        {
            Dim = dim;
            DimBrightness = dimBrightness;
            Shrink = shrink;
            ShrinkScale = shrinkScale;
        }

        /// <summary>旧工程的默认手感：压暗关、微缩开 0.95。</summary>
        public static ActorHighlightSettings Default => new ActorHighlightSettings(false, 0.8f, true, 0.95f);
    }

    /// <summary>
    /// 立绘舞台。管 ActorId → IActorView 的实例、加载和释放，
    /// 以及所有"发起了但没等它结束"的动画。
    /// </summary>
    public interface IActorStage
    {
        /// <summary>
        /// 拿到（必要时加载并入场）指定角色的立绘。
        ///
        /// <paramref name="kind"/> 决定实例化哪种立绘、去角色表的哪个字段取路径。
        /// <b>同一个角色同时只应当有一种立绘在台上</b>：剧本先用骨骼出场、
        /// 后面又用图片出场同一个角色时，实现里应当把旧的换掉而不是叠两份，
        /// 否则 <see cref="Find"/> 拿到哪一个就成了随机的。
        /// </summary>
        UniTask<IActorView> AcquireAsync(int actorId, EActorAssetKind kind, CancellationToken ct);

        /// <summary>找已经在台上的立绘；不在台上返回 null。</summary>
        IActorView Find(int actorId);

        /// <summary>
        /// 把立绘放到指定方向的位置上（左 / 中 / 右）。
        ///
        /// <b>"挂在哪"是舞台的事，不是立绘自己的事</b>，所以这个方法在 IActorStage 上
        /// 而不是 IActorView 上 —— 三个方向具体对应什么坐标 / 挂在哪个锚点下，
        /// 只有摆布局的舞台知道。
        ///
        /// <see cref="ActorShowAction.Position"/> 是<b>在此基础上的偏移</b>：
        /// Handler 会先调本方法定方向，再写 Root.localPosition。
        /// 所以实现里改的应当是<b>父节点</b>（或锚点），别去写 localPosition，
        /// 不然紧接着就被 Position 覆盖掉了。
        /// </summary>
        void SetDirection(IActorView actor, EActorShowDirection direction);

        /// <summary>
        /// 「非说话人压暗 / 微缩」这套效果的开关 + 强度。<see cref="ActorHighlightAction"/> 用。
        ///
        /// 实现里要<b>立刻按当前说话人重刷一遍</b>，不能等下一句台词 ——
        /// 剧本中途关掉效果时，已经压暗的立绘得马上恢复，否则会一直暗着。
        /// </summary>
        void SetHighlightMode(ActorHighlightSettings settings);

        /// <summary>
        /// 告诉舞台"现在是谁在说话"，舞台据此把说话人恢复原样、其他人压暗 / 微缩。
        /// <paramref name="actorId"/> 小于等于 0 表示没有具体说话人（旁白 / 主角 / 自定义名），
        /// 这种情况下所有立绘都恢复原样。
        ///
        /// <b>由 <see cref="TalkAction"/> 的 Handler 每句调一次</b>，不是剧本里的显式指令 ——
        /// 策划只管在图里配"这句谁说的"，突出效果自动跟着走。
        /// 开关关掉时本方法应当什么都不做（或把所有人恢复原样）。
        /// </summary>
        void SetSpeaker(int actorId);

        /// <summary>
        /// 显隐。<paramref name="duration"/> 为 0 就是瞬间切换。
        ///
        /// 实现里产生的 Tween <b>必须登记到舞台自己名下</b>，这样
        /// <see cref="CompleteAllTweens"/> 才收得住。本方法自己是会被 await 的，
        /// 但同一个立绘身上还有别的"发起了不等它"的动画（<see cref="ActorOffsetMoveAction"/>
        /// 的 LoopCount 为负数时、循环的 Spine 动画），以及 Skip 时被中途取消的动画 ——
        /// 这些都要靠登记 + CompleteAllTweens 收口，否则会漏到下一段剧情里。
        /// </summary>
        UniTask SetVisibleAsync(IActorView actor, bool visible, float duration, Ease ease, CancellationToken ct);

        /// <summary>
        /// 把所有还在跑的立绘动画立刻推到终点。
        /// 剧本结束 / 跳转 / 切到 Skip 时调，防止游离动画漏到下一段剧情里。
        /// </summary>
        void CompleteAllTweens();

        /// <summary>清空舞台并释放立绘资源。</summary>
        void ReleaseAll();
    }
}

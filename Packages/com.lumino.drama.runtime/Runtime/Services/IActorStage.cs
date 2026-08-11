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

        /// <summary>非说话人置灰。<see cref="ActorHighlightAction"/> 用。</summary>
        void SetGray(bool gray);

        /// <summary>非说话人微缩。<see cref="ActorHighlightAction"/> 用。</summary>
        void SetShrink(bool shrink);

        /// <summary>换皮肤。<see cref="ActorSetSkinAction"/> 用。</summary>
        void SetSkin(string skinName);

        /// <summary>
        /// 播动画。<paramref name="loop"/> 为 true 时应当立刻返回（不然永远等不到结束）。
        /// </summary>
        UniTask PlayAnimationAsync(string animationName, int track, bool loop, float timeScale, CancellationToken ct);
    }

    /// <summary>
    /// 立绘舞台。管 ActorId → IActorView 的实例、加载和释放，
    /// 以及所有"发起了但没等它结束"的动画。
    /// </summary>
    public interface IActorStage
    {
        /// <summary>拿到（必要时加载并入场）指定角色的立绘。</summary>
        UniTask<IActorView> AcquireAsync(int actorId, CancellationToken ct);

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

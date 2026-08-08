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
        /// 显隐。<paramref name="duration"/> 为 0 就是瞬间切换。
        ///
        /// 实现里产生的 Tween <b>必须登记到舞台自己名下</b>，
        /// 这样 <see cref="CompleteAllTweens"/> 才收得住 —— 见
        /// <see cref="ActorShowAction.WaitForCompletion"/> 为 false 的情况。
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

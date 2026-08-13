using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;

namespace Drama.Runtime.Services
{
    /// <summary>
    /// CG 层：全屏的一张大图（本工程是全屏 Live2D）。
    ///
    /// <b>单槽位</b>——同时只有一张 CG 在台上，所以这一族方法都不带 ID，
    /// 变换 / 抖动 / Animator 指令说的都是"当前那张"。
    ///
    /// <b>刻意不认识 Live2D</b>，和 <see cref="IActorStage"/> 不认识 Spine 是同一个道理：
    /// 换个工程可以用一张静态大图、一段视频、一个 Spine 来实现，
    /// 结构都是"显示 → 变换 → 隐藏"，包里只规定结构。
    /// </summary>
    public interface IDramaCG
    {
        /// <summary>
        /// 当前这张 CG 的变换目标。位移 / 缩放 / 旋转 / 抖动 / 小动作都是 DOTween 直接动它。
        ///
        /// <b>没有 CG 在台上时返回 null</b>，Handler 会当这条指令不适用直接跳过 ——
        /// 剧本把变换指令写在 CG出现 之前是顺序错误，不该升级成播放事故。
        /// </summary>
        Transform Root { get; }

        /// <summary>
        /// 显示一张 CG。<paramref name="duration"/> 为 0 就是瞬时。
        ///
        /// <b>实现里要把立绘整层藏掉</b>（<see cref="HideAsync"/> 时恢复）——
        /// 这是 CG 的固有语义，不是靠剧本额外写一条隐藏指令来保证的。
        ///
        /// 换一张 CG 时直接再调一次即可，实现负责把上一张收掉。
        /// </summary>
        UniTask ShowAsync(long cgId, float duration, Ease ease, CancellationToken ct);

        /// <summary>关掉当前 CG 并恢复立绘层。没有 CG 在台上时什么都不做。</summary>
        UniTask HideAsync(float duration, Ease ease, CancellationToken ct);

        /// <summary>
        /// 设置 CG 模型 Animator 的参数。没有 Animator 的实现打一条警告后跳过，别抛 ——
        /// 语义和 <see cref="IActorView.SetAnimatorBool"/> 一致。
        /// </summary>
        void SetAnimatorBool(string parameterName, bool value);

        /// <inheritdoc cref="SetAnimatorBool"/>
        void SetAnimatorInt(string parameterName, int value);

        /// <inheritdoc cref="SetAnimatorBool"/>
        void SetAnimatorFloat(string parameterName, float value);

        /// <summary>SetTrigger / ResetTrigger，语义见 <see cref="IActorView.SetAnimatorTrigger"/>。</summary>
        void SetAnimatorTrigger(string parameterName, bool reset);

        /// <summary>
        /// 把还在跑的 CG 动画立刻推到终点。剧本结束 / 跳转 / 切到跳过时调。
        ///
        /// 无限循环的小动作（<see cref="CGOffsetMoveAction"/> 次数为负）是"发起了不等它"的，
        /// 不靠这里收口就会漏到下一段剧情里。
        /// </summary>
        void CompleteAllTweens();

        /// <summary>清空 CG 层并释放资源。剧本收尾时调。</summary>
        void Clear();
    }
}

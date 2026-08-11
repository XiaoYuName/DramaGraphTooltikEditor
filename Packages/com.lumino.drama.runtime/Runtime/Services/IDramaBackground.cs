using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Drama.Runtime.Services
{
    /// <summary>
    /// 背景层。管背景图的切换，以及背景自身的位移 / 旋转 / 缩放。
    ///
    /// <b>刻意不认识背景是怎么摆的。</b> 一张全屏 Image 是一种实现，
    /// 带视差的多层 SpriteRenderer 是另一种 —— 接口只出"换图"和"给我根节点"。
    ///
    /// <see cref="BackgroundId"/> 之所以到处都带着：目前项目只有一张背景层，
    /// 实现方完全可以忽略这个参数、永远返回同一个根节点。留着是因为
    /// <b>数据一旦导出就不好改</b>（<c>[SerializeReference]</c>），而接口改起来是免费的。
    /// 跟 <see cref="IActorStage.Find"/> 拿 actorId 是一个路子。
    /// </summary>
    public interface IDramaBackground
    {
        /// <summary>
        /// 换背景图。<paramref name="sprite"/> 由 Handler 通过
        /// <see cref="IDramaAssetProvider.LoadBackgroundAsync"/> 加载好后传进来，
        /// 实现方不需要自己碰资源系统。
        ///
        /// 转场为 <see cref="EBgTransitionKind.None"/> 时应当瞬切，两个时长都忽略。
        ///
        /// <b>实现要点：换图时要掐掉背景自己还在跑的变换动画</b>（位移 / 旋转 / 缩放），
        /// 否则"推镜到一半换了张图"会让新图接着上一张的运镜跑下去。
        /// 是<b>掐掉</b>（停在当前值）而不是推到终点，也<b>不要</b>顺手归零 transform ——
        /// 归零是 <see cref="ReleaseAll"/> 的事，剧本要新图从原点开始会自己接一条位置指令。
        /// </summary>
        UniTask ChangeAsync(long backgroundId, Sprite sprite, EBgTransitionKind kind,
                            float inSeconds, float outSeconds, CancellationToken ct);

        /// <summary>
        /// 背景的根节点，位移 / 旋转 / 缩放都是 DOTween 直接动它。
        /// 拿不到（背景还没建出来）返回 null，Handler 会跳过这条指令。
        /// </summary>
        Transform GetRoot(long backgroundId);

        /// <summary>把还在跑的背景动画立刻推到终点。剧本结束 / 跳转时调。</summary>
        void CompleteAllTweens();

        /// <summary>
        /// 清空背景并释放资源。
        ///
        /// <b>要把根节点的 position / rotation / scale 一起归零。</b>
        /// 这三个是跨指令持续的状态，不归零的话上一本剧本的运镜会带到下一本 ——
        /// 这个 bug 只在 Goto 连播时才现，单本测试测不出来。
        /// </summary>
        void ReleaseAll();
    }
}

using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;

namespace Drama.Runtime.Services
{
    /// <summary>
    /// 全屏转场遮罩。<see cref="ScreenTransitionAction"/> 用。
    ///
    /// <b>刻意不认识具体转场怎么画。</b> 纯色 Image 推 alpha 是一种实现，
    /// 百叶窗 Shader 是另一种，带遮罩贴图的又是一种——
    /// 但不管哪种，结构都是"盖上 → 揭开"这两下，所以接口只出这两个方法，
    /// <see cref="EScreenTransitionKind"/> 交给实现自己去分派。
    /// </summary>
    public interface IDramaScreen
    {
        /// <summary>
        /// 盖上遮罩（画面转黑 / 转白）。
        ///
        /// <b>跑完停在 <paramref name="alpha"/> 上，不要自动还原</b> ——
        /// 剧本经常是「盖上 → 换背景换立绘 → 揭开」，中间那几条指令就指望遮罩一直挡着。
        /// </summary>
        UniTask CoverAsync(EScreenTransitionKind kind, float seconds, Color color, float alpha, Ease ease, CancellationToken ct);

        /// <summary>揭开遮罩（画面恢复）。跑完遮罩应当完全透明且不吃点击。</summary>
        UniTask RevealAsync(EScreenTransitionKind kind, float seconds, Ease ease, CancellationToken ct);

        /// <summary>
        /// 立刻把遮罩清干净。剧本结束 / 跳转 / 被打断时调 ——
        /// 剧本可能正停在「盖着黑幕」的状态，不清就是一块黑屏卡在玩家脸上。
        /// </summary>
        void Clear();
    }
}

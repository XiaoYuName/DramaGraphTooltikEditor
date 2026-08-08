using System.Threading;
using Cysharp.Threading.Tasks;
using Drama.Runtime.Flow;

namespace Drama.Runtime.Handlers
{
    /// <summary>
    /// 全屏转场。
    ///
    /// <see cref="ETransitionPhase"/> 决定跑哪一半：
    ///   In    盖上遮罩，停在那儿不还原 —— 后面几条指令在黑幕下偷偷换立绘 / 背景
    ///   Out   揭开遮罩，画面恢复
    ///   InOut 盖上再揭开，中间不夹别的指令
    /// </summary>
    public sealed class ScreenTransitionActionHandler : DramaSimpleActionHandler<ScreenTransitionAction>
    {
        protected override async UniTask RunAsync(ScreenTransitionAction a, IDramaContext ctx, CancellationToken ct)
        {
            if (ctx.Screen == null) return;

            // Skip 时不放转场动画，但遮罩该盖上还得盖上 —— 所以是把时长压成 0，不是整条跳过
            var instant = ctx.Mode == EDramaPlaybackMode.Skip;
            var inSeconds = instant ? 0f : a.InSeconds;
            var outSeconds = instant ? 0f : a.OutSeconds;

            switch (a.Phase)
            {
                case ETransitionPhase.In:
                    await ctx.Screen.CoverAsync(a.TransitionKind, inSeconds, a.Color, a.Alpha, a.Ease, ct);
                    break;

                case ETransitionPhase.Out:
                    await ctx.Screen.RevealAsync(a.TransitionKind, outSeconds, a.Ease, ct);
                    break;

                case ETransitionPhase.InOut:
                    await ctx.Screen.CoverAsync(a.TransitionKind, inSeconds, a.Color, a.Alpha, a.Ease, ct);
                    await ctx.Screen.RevealAsync(a.TransitionKind, outSeconds, a.Ease, ct);
                    break;
            }
        }
    }
}

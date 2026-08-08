using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Drama.Runtime.Flow
{
    /// <summary>
    /// 所有"等一段时间"都走这里。
    ///
    /// 存在的意义：把"快进 / 跳过怎么影响时长"收在一处。
    /// 不然二十个 Handler 里会散落二十份 <c>if (ctx.Mode == Skip)</c>，
    /// 以后想调快进倍率得挨个改。
    /// </summary>
    public static class DramaWait
    {
        /// <summary>快进时的时长倍率。</summary>
        public static float FastForwardScale = 0.25f;

        /// <summary>按当前播放模式换算后的实际时长。做 Tween 时长时用这个。</summary>
        public static float Scale(float seconds, EDramaPlaybackMode mode)
        {
            switch (mode)
            {
                case EDramaPlaybackMode.Skip:        return 0f;
                case EDramaPlaybackMode.FastForward: return seconds * FastForwardScale;
                default:                             return seconds;
            }
        }

        public static float Scale(float seconds, IDramaContext ctx) => Scale(seconds, ctx.Mode);

        /// <summary>等一段时间。Skip 模式下立刻返回。</summary>
        public static UniTask Seconds(float seconds, IDramaContext ctx, CancellationToken ct)
        {
            var actual = Scale(seconds, ctx.Mode);
            if (actual <= 0f) return UniTask.CompletedTask;

            return UniTask.Delay(TimeSpan.FromSeconds(actual),
                                 DelayType.DeltaTime, PlayerLoopTiming.Update, ct);
        }
    }
}

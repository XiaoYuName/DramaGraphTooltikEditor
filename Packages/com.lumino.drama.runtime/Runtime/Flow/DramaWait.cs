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

        /// <summary>
        /// 等一段时间。Skip 模式下立刻返回。
        ///
        /// <b>逐帧按当前模式扣剩余时间，而不是进来时一次性算好总时长。</b>
        /// 后者的话，玩家在一条 3 秒的等待中途点跳过是没有反应的 ——
        /// <c>UniTask.Delay</c> 已经按老模式定好了闹钟，模式再变也叫不醒它，
        /// 表现出来就是"点了跳过还得干等这 3 秒走完"。
        /// </summary>
        public static async UniTask Seconds(float seconds, IDramaContext ctx, CancellationToken ct)
        {
            if (seconds <= 0f) return;

            var remaining = seconds;

            while (true)
            {
                // 跳过：不管还剩多少，立刻结束
                if (ctx.Mode == EDramaPlaybackMode.Skip) return;

                await UniTask.Yield(PlayerLoopTiming.Update, ct);

                // 快进是把时长压缩，等价于让时间流得更快
                var rate = ctx.Mode == EDramaPlaybackMode.FastForward && FastForwardScale > 0f
                    ? 1f / FastForwardScale
                    : 1f;

                remaining -= UnityEngine.Time.deltaTime * rate;
                if (remaining <= 0f) return;
            }
        }
    }
}

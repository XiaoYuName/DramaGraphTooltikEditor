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

        /// <summary>
        /// 这个模式下"一切从速"吗（等待归零、不等玩家输入）。
        ///
        /// 跳过和读档恢复在<b>时长</b>上完全一致，区别只在意图，
        /// 所以凡是判时长的地方都该问这一句，而不是各写各的 <c>== Skip</c> ——
        /// 漏一处的症状是"读档时卡在某条指令上等玩家点击"，而且很难联想到原因。
        /// </summary>
        public static bool IsInstant(EDramaPlaybackMode mode) =>
            mode == EDramaPlaybackMode.Skip || mode == EDramaPlaybackMode.Restoring;

        /// <summary>按当前播放模式换算后的实际时长。做 Tween 时长时用这个。</summary>
        public static float Scale(float seconds, EDramaPlaybackMode mode)
        {
            if (IsInstant(mode)) return 0f;
            return mode == EDramaPlaybackMode.FastForward ? seconds * FastForwardScale : seconds;
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
                // 跳过 / 读档恢复：不管还剩多少，立刻结束
                if (IsInstant(ctx.Mode)) return;

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

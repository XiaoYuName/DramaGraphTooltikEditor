namespace Drama.Runtime.Flow
{
    /// <summary>播放模式。Handler 里的等待时长要看它。</summary>
    public enum EDramaPlaybackMode
    {
        /// <summary>正常播放，台词等玩家点击。</summary>
        Normal = 0,

        /// <summary>自动播放，台词到点自动翻页。</summary>
        Auto = 1,

        /// <summary>快进，动画加速、等待缩短。</summary>
        FastForward = 2,

        /// <summary>跳过，所有等待归零，只保留状态变更。</summary>
        Skip = 3,
    }

    /// <summary>
    /// Handler 碰外部世界的唯一入口。
    ///
    /// Runtime 包只【定义】这个接口，宿主工程去【实现】——
    /// 台词框长什么样、立绘怎么加载，每个工程都不一样，包里不能知道。
    ///
    /// 现在只有流程层用得上的成员。等开始写具体 Handler 时，
    /// 表现层服务（IDialogueView / IActorStage / IAudioService / ...）
    /// 作为属性追加到这里即可 —— 对流程层是纯增量，不影响已写好的执行器。
    /// </summary>
    public interface IDramaContext
    {
        EDramaPlaybackMode Mode { get; }
    }
}

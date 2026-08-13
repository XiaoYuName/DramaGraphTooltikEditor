using Drama.Runtime.Services;

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

        /// <summary>
        /// 读档恢复中：从剧本开头静默重放到存档点，把舞台"堆"回当时的样子。
        ///
        /// 和 <see cref="Skip"/> 的区别是<b>意图</b>而不是时长 —— 两者等待都归零，
        /// 但跳过是玩家在看戏、恢复是玩家还没进场。以后加音效之类"演出性"指令时，
        /// 跳过要放、恢复不能放（读个档炸出二十声音效），靠这个枚举分得开。
        /// </summary>
        Restoring = 4,
    }

    /// <summary>
    /// Handler 碰外部世界的唯一入口。
    ///
    /// Runtime 包只【定义】这些服务接口，宿主工程去【实现】——
    /// 台词框长什么样、立绘用 SkeletonGraphic 还是 SkeletonAnimation、
    /// 资源怎么加载，每个工程都不一样，包里不能知道。
    /// </summary>
    public interface IDramaContext
    {
        /// <summary>
        /// 当前播放模式。<b>可写</b>：玩家中途点自动 / 跳过要立刻生效，
        /// 而 <see cref="DramaPlayer"/> 读档恢复期间也会临时把它切成
        /// <see cref="EDramaPlaybackMode.Restoring"/>、到达存档点再还原。
        /// </summary>
        EDramaPlaybackMode Mode { get; set; }

        /// <summary>
        /// 恢复存档时，按当年的顺序取回下一个选择。
        ///
        /// 静默重放会重新走到选项节点，但那时候不能弹面板问玩家 ——
        /// 得把当年选的那个原样喂回去，否则重放会走上另一条支线，
        /// 恢复出来的现场就不是存档时的现场了。
        ///
        /// 实现方在取走的同时要把它记进"本次已选路径"，
        /// 不然恢复完再存一次档，前面这些选择就丢了。
        /// </summary>
        /// <returns>没有记录可取时返回 false，照常问玩家。</returns>
        bool TryTakeRestoredChoice(out int optionIndex);

        /// <summary>玩家选完之后回报一次，供存档记录走过的支线。</summary>
        void ReportChoicePicked(int optionIndex);

        IDialogueView       Dialogue     { get; }
        IChoiceView         Choice       { get; }
        IActorStage         Actors       { get; }
        IDramaCG            CG           { get; }
        IDramaScreen        Screen       { get; }
        IDramaBackground    Background   { get; }
        IDramaLocalization  Localization { get; }
        IDramaAssetProvider Assets       { get; }
        IDramaAudio         Audio        { get; }
        IDramaGameBridge    Game         { get; }
    }
}

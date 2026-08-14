using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Drama.Runtime.Services
{
    /// <summary>
    /// 剧情要用的资源。
    ///
    /// <b>入参一律是剧情里的业务 ID，不是 Addressables 地址。</b>
    /// ID → 地址的映射是宿主工程配置表的事，本包不该知道，
    /// 宿主写个几十行的 adapter 接到自己封装的 Addressables 层即可。
    ///
    /// 注意<b>台词语音不在这里</b> —— 语音是多语言资源，走
    /// <see cref="IDramaLocalization.ResolveVoiceAsync"/>。
    ///
    /// 生命周期：Director 在每段剧本开始前预载、结束后 <see cref="ReleaseAll"/>。
    /// </summary>
    public interface IDramaAssetProvider
    {
        UniTask<Sprite> LoadBackgroundAsync(long backgroundId, CancellationToken ct);

        // 立绘不在这里：包无法规定"一个立绘资源"到底是什么 —— 可能是 Prefab，
        // 可能是 Spine 的 SkeletonDataAsset，也可能是一张贴图。
        // 而且包里没有任何东西需要它（Handler 只调 IActorStage.AcquireAsync），
        // 所以由宿主自己定加载方式，包只通过 DramaAssetKeys.ActorIds 告诉宿主"本段要用哪些角色"。

        // BGM 不在这里：MusicId 是宿主音频系统的配置表 ID，clip 由那套系统自己持有，
        // 剧情这边既不该加载也不该释放。见 IDramaAudio.PlayMusic。

        /// <summary>整段剧本结束时释放本段加载的全部资源。</summary>
        void ReleaseAll();
    }

    /// <summary>
    /// 多语言。文本和语音都走这里。
    ///
    /// 台词文本 → String Table；台词语音 → Asset Table。
    /// 两者在编辑器里都是 <see cref="LocalizedRef"/>（`LocalizationNode` / `LocalizationAudioNode` 产出的）。
    /// </summary>
    public interface IDramaLocalization
    {
        /// <summary>
        /// 查文本表。<b>要求是同步的</b> —— 每句台词都 await 一次表查询既难写又容易掉帧，
        /// 所以约定由 <see cref="PreloadStringTablesAsync"/> 提前把表拉进内存，这里只做纯查询。
        /// </summary>
        string Resolve(LocalizedRef reference);

        // 语音不在这里取：Handler 只把引用交给 IDramaAudio.PlayVoice，
        // "引用 → clip" 那一步是宿主音频层内部的事，包不需要经手。

        /// <summary>播剧本前预热本段用到的文本表。</summary>
        UniTask PreloadStringTablesAsync(IReadOnlyCollection<string> tables, CancellationToken ct);

        /// <summary>播剧本前预热本段用到的语音表。</summary>
        UniTask PreloadAssetTablesAsync(IReadOnlyCollection<string> tables, CancellationToken ct);
    }

    /// <summary>剧情要回调的业务逻辑（领任务、发奖励之类）。</summary>
    public interface IDramaGameBridge
    {
        UniTask ReceiveTaskAsync(long taskId, CancellationToken ct);

        /// <summary>
        /// 切换游戏内的真实场景。<see cref="ChangeGameSceneAction"/> 用。
        ///
        /// <b>要等场景真的切完再返回</b> —— 后面的指令（换背景、出立绘）都是演给新场景看的，
        /// 不等的话会在旧场景上演一半。宿主那边的转场往往是"发起了就不管"的，
        /// 实现里需要自己盯着它的完成标志。
        /// </summary>
        /// <param name="mapSceneId">大场景 ID，小于等于 0 表示留在当前大场景里只换小场景。</param>
        UniTask ChangeGameSceneAsync(long mapSceneId, long minSceneId, CancellationToken ct);

        /// <summary>
        /// 报上"剧情结束之后要打开哪个界面"。<see cref="EndUIDramaAction"/> 用。
        ///
        /// <b>实现里只应当记下来，不要当场打开。</b> 调用发生在剧情还没收尾的时候，
        /// 当场打开会被随后的"关剧情面板 + 还原进剧情前的界面"盖掉。
        /// 什么时候真正打开由宿主的收尾流程决定。
        /// </summary>
        void RequestOpenUIOnEnd(string uiPage);

        /// <summary>
        /// 报上"剧情结束之后要播哪段引导"。<see cref="EndGuideDramaAction"/> 用。
        ///
        /// 和 <see cref="RequestOpenUIOnEnd"/> 一样：<b>实现里只记下来，不要当场开</b>，
        /// 引导多半要指着某个界面上的按钮，而那些界面要等剧情收尾之后才还原回来。
        /// </summary>
        void RequestStartGuideOnEnd(long guideId);

        /// <summary>
        /// 设置游戏场景里那些"和剧情无关的东西"的显隐：场景 NPC、地图配置的场景默认UI。
        /// <see cref="SceneVisibilityAction"/> 用。
        ///
        /// <b>实现里要把它当成一个持续的"意图"存下来，而不是执行一次就完。</b>
        /// 剧情中途切场景会重新生成 NPC、重新开默认UI，只在收到指令那一刻做一次是拦不住的 ——
        /// 每次场景就绪时都要按最后一次的意图重新应用。
        /// </summary>
        void SetSceneVisibility(bool showNpc, bool showSceneUI);
    }

    /// <summary>
    /// 音频播放。
    ///
    /// <b>两个都收"标识"而不是 clip</b> —— 谁去把标识变成声音，是宿主的事：
    /// BGM 走宿主音频配置表的 ID，语音走多语言 Asset Table 的引用。
    /// 这样 Handler 层不用为了播一句语音先 await 一次资源加载，台词也就不会被挡住。
    /// </summary>
    public interface IDramaAudio
    {
        /// <summary>播 BGM。<paramref name="musicId"/> 是宿主音频配置表的 ID，原样透传。</summary>
        void PlayMusic(string musicId);

        /// <summary>
        /// 播台词语音。<paramref name="reference"/> 为空引用时什么都不做。
        ///
        /// <b>实现应当是"即发即忘"的</b>：内部异步取 clip 再播，不要让调用方等 ——
        /// 台词该立刻显示出来，语音晚几帧进来是可以接受的。
        /// 取不到就当没配语音，别抛。
        /// </summary>
        void PlayVoice(LocalizedRef reference);

        void StopVoice();
    }
}

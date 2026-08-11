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

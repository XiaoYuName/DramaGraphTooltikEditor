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
        /// <summary>立绘。返回的是可实例化的 Prefab（SkeletonGraphic 或 SkeletonAnimation 都行）。</summary>
        UniTask<GameObject> LoadActorAsync(int actorId, CancellationToken ct);

        UniTask<Sprite> LoadBackgroundAsync(long backgroundId, CancellationToken ct);

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

        /// <summary>
        /// 取台词语音。取不到就返回 null，Handler 会当没配语音处理。
        ///
        /// 保持异步是因为 Asset Table 天然是异步加载的，而且语音是一句一个、
        /// 全量预载不现实；真要消除等待就在 <see cref="PreloadAssetTablesAsync"/> 里预热。
        /// </summary>
        UniTask<AudioClip> ResolveVoiceAsync(LocalizedRef reference, CancellationToken ct);

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
    /// <b>BGM 传 ID，语音传 clip</b> —— 不是不统一，是两者来源本来就不同：
    /// BGM 走宿主自己的音频配置表（ID → clip 那一步归宿主，剧情不该知道），
    /// 台词语音是多语言资源，由 <see cref="IDramaLocalization.ResolveVoiceAsync"/>
    /// 从 Asset Table 里取出来，到这儿已经是 clip 了。
    /// </summary>
    public interface IDramaAudio
    {
        /// <summary>播 BGM。<paramref name="musicId"/> 是宿主音频配置表的 ID，原样透传。</summary>
        void PlayMusic(string musicId);

        void PlayVoice(AudioClip clip);
        void StopVoice();
    }
}

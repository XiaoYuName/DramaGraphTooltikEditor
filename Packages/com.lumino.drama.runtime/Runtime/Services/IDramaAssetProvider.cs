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

        UniTask<AudioClip> LoadMusicAsync(string musicId, CancellationToken ct);

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

    /// <summary>音频播放。</summary>
    public interface IDramaAudio
    {
        void PlayMusic(AudioClip clip);
        void PlayVoice(AudioClip clip);
        void StopVoice();
    }
}

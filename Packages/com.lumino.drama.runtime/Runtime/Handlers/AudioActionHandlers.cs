using System.Threading;
using Cysharp.Threading.Tasks;
using Drama.Runtime.Flow;

namespace Drama.Runtime.Handlers
{
    /// <summary>
    /// 播放 BGM。
    ///
    /// <b>这里不加载任何东西。</b> MusicId 是宿主音频配置表的 ID，
    /// clip 由那套系统自己持有（配置表一加载就在内存里），剧情只负责把 ID 报过去。
    ///
    /// Skip / 快进也照播 —— BGM 是状态而不是演出，跳过时更不能让它停在上一首。
    /// </summary>
    public sealed class PlayMusicActionHandler : DramaSimpleActionHandler<PlayMusicAction>
    {
        protected override UniTask RunAsync(PlayMusicAction a, IDramaContext ctx, CancellationToken ct)
        {
            if (!string.IsNullOrEmpty(a.MusicId))
                ctx.Audio?.PlayMusic(a.MusicId);

            return UniTask.CompletedTask;
        }
    }
}

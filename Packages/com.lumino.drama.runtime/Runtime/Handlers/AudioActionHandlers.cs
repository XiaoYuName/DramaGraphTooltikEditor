using System.Threading;
using Cysharp.Threading.Tasks;
using Drama.Runtime.Flow;
using UnityEngine;

namespace Drama.Runtime.Handlers
{
    /// <summary>
    /// 播放 BGM。
    ///
    /// 只是把 <see cref="Services.IDramaAssetProvider"/> 和 <see cref="Services.IDramaAudio"/>
    /// 串一下，没有任何工程特有的东西，所以放在包里。
    ///
    /// 正常情况下这里【不会】真的产生一次加载 —— MusicId 在
    /// <see cref="DramaAssetKeys.Collect"/> 里已经被收走、开播前预载过了，
    /// 这一下只是从 Provider 的缓存里取出来。
    /// </summary>
    public sealed class PlayMusicActionHandler : DramaSimpleActionHandler<PlayMusicAction>
    {
        protected override async UniTask RunAsync(PlayMusicAction a, IDramaContext ctx, CancellationToken ct)
        {
            if (string.IsNullOrEmpty(a.MusicId) || ctx.Assets == null || ctx.Audio == null)
                return;

            var clip = await ctx.Assets.LoadMusicAsync(a.MusicId, ct);
            if (clip == null)
            {
                Debug.LogWarning($"[Drama] BGM 资源缺失：{a.MusicId}");
                return;
            }

            // Skip / 快进也照播 —— BGM 是状态而不是演出，跳过时更不能让它停在上一首
            ctx.Audio.PlayMusic(clip);
        }
    }
}

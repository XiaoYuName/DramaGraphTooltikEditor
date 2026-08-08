using System.Threading;
using Cysharp.Threading.Tasks;
using Drama.Runtime.Flow;
using Drama.Runtime.Services;

namespace Drama.Runtime.Handlers
{
    /// <summary>
    /// 说一句台词，然后等玩家（或自动等待）推进。
    ///
    /// 这个 Handler 是整套东西里唯一会"停下来等人"的地方，
    /// 也是流程测试里 GateHandler 模拟的那个闸门的真身。
    /// </summary>
    public sealed class TalkActionHandler : DramaSimpleActionHandler<TalkAction>
    {
        /// <summary>主角名字怎么取。宿主在装配时塞进来（一般是玩家昵称）。</summary>
        public System.Func<string> HeroNameProvider;

        /// <summary>角色ID → 显示名。取不到就返回空。</summary>
        public System.Func<int, string> ActorNameProvider;

        protected override async UniTask RunAsync(TalkAction a, IDramaContext ctx, CancellationToken ct)
        {
            var line = new DialogueLine
            {
                Body        = ctx.Localization.Resolve(a.Text),
                SpeakerName = ResolveSpeakerName(a, ctx),
                NameColor   = a.NameColor,
                Balloon     = a.Balloon,
                Voice       = a.Voice.IsEmpty ? null : await ctx.Localization.ResolveVoiceAsync(a.Voice, ct),
            };

            if (line.Voice != null && ctx.Mode != EDramaPlaybackMode.Skip)
                ctx.Audio.PlayVoice(line.Voice);

            // 打字机跑完（或被玩家点断）
            await ctx.Dialogue.ShowLineAsync(line, ctx.Mode, ct);

            if (ctx.Mode == EDramaPlaybackMode.Skip)
            {
                ctx.Audio.StopVoice();
                return;
            }

            if (a.AutoWaitSeconds > 0f)
            {
                // 自动等待期间玩家点了也要能提前翻页 —— 谁先到听谁的
                await UniTask.WhenAny(
                    DramaWait.Seconds(a.AutoWaitSeconds, ctx, ct),
                    ctx.Dialogue.WaitForAdvanceAsync(ct));
            }
            else
            {
                await ctx.Dialogue.WaitForAdvanceAsync(ct);
            }
        }

        string ResolveSpeakerName(TalkAction a, IDramaContext ctx)
        {
            switch (a.Speaker)
            {
                case ESpeakerKind.Aside:  return string.Empty;
                case ESpeakerKind.Hero:   return HeroNameProvider?.Invoke() ?? string.Empty;
                case ESpeakerKind.Custom: return ctx.Localization.Resolve(a.SpeakerName);
                case ESpeakerKind.Actor:  return ActorNameProvider?.Invoke(a.ActorId) ?? string.Empty;
                default:                  return string.Empty;
            }
        }
    }

    /// <summary>对话框显隐。</summary>
    public sealed class TalkShowActionHandler : DramaSimpleActionHandler<TalkShowAction>
    {
        protected override UniTask RunAsync(TalkShowAction a, IDramaContext ctx, CancellationToken ct)
            => ctx.Dialogue.SetVisibleAsync(a.Show, ct);
    }

    /// <summary>对话框皮肤。</summary>
    public sealed class SetTalkFrameActionHandler : DramaSimpleActionHandler<SetTalkFrameAction>
    {
        protected override UniTask RunAsync(SetTalkFrameAction a, IDramaContext ctx, CancellationToken ct)
        {
            ctx.Dialogue.SetFrame(a.Frame);
            return UniTask.CompletedTask;
        }
    }
}

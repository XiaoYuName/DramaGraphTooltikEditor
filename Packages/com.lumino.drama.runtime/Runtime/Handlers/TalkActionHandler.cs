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
        protected override async UniTask RunAsync(TalkAction a, IDramaContext ctx, CancellationToken ct)
        {
            // 全部原样透传，一个都不解析 —— 原因见 DialogueLine 的注释。
            // 这里刻意没有任何 await：台词要立刻显示出来，不能被语音加载挡住。
            var line = new DialogueLine
            {
                TextRef        = a.Text,
                Speaker        = a.Speaker,
                ActorId        = a.ActorId,
                SpeakerNameRef = a.SpeakerName,
                NameColor      = a.NameColor,
                Balloon        = a.Balloon,
                VoiceRef       = a.Voice,
            };

            if (!a.Voice.IsEmpty && ctx.Mode != EDramaPlaybackMode.Skip)
                ctx.Audio.PlayVoice(a.Voice);

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

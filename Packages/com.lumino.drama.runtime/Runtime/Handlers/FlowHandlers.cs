using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Drama.Runtime.Flow;

namespace Drama.Runtime.Handlers
{
    /// <summary>等待。</summary>
    public sealed class WaitActionHandler : DramaSimpleActionHandler<WaitAction>
    {
        protected override UniTask RunAsync(WaitAction a, IDramaContext ctx, CancellationToken ct)
            => DramaWait.Seconds(a.Seconds, ctx, ct);
    }

    /// <summary>选项分支。玩家选完直接跳到那条支线。</summary>
    public sealed class ChoiceActionHandler : DramaActionHandler<ChoiceAction>
    {
        protected override async UniTask<DramaFlowResult> ExecuteAsync(
            ChoiceAction a, IDramaContext ctx, CancellationToken ct)
        {
            if (a.Options == null || a.Options.Length == 0)
                return DramaFlowResult.Continue;

            var labels = a.Options.Select(o => ctx.Localization.Resolve(o.Text)).ToArray();
            var picked = await ctx.Choice.PickAsync(labels, ct);

            if (picked < 0 || picked >= a.Options.Length)
                return DramaFlowResult.Stop;

            // Next 为 -1（选项没接东西）时 Jump 会自动退化成 Stop
            return DramaFlowResult.Jump(a.Options[picked].Next);
        }
    }

    /// <summary>
    /// 跳转剧本。
    ///
    /// 注意这里【不】递归去播下一个剧本 —— 那样连播 50 段就是 50 层嵌套的
    /// Player 和 50 份没释放的资源。只把意图报上去，由外层 Director 循环换本子。
    /// </summary>
    public sealed class GotoDramaActionHandler : DramaActionHandler<GotoDramaAction>
    {
        protected override UniTask<DramaFlowResult> ExecuteAsync(
            GotoDramaAction a, IDramaContext ctx, CancellationToken ct)
            => UniTask.FromResult(DramaFlowResult.Goto(a.DramaId));   // <= 0 时自动变 Stop
    }

    /// <summary>领取任务。</summary>
    public sealed class ReceiveTaskActionHandler : DramaSimpleActionHandler<ReceiveTaskAction>
    {
        protected override UniTask RunAsync(ReceiveTaskAction a, IDramaContext ctx, CancellationToken ct)
            => a.TaskId > 0 ? ctx.Game.ReceiveTaskAsync(a.TaskId, ct) : UniTask.CompletedTask;
    }
}

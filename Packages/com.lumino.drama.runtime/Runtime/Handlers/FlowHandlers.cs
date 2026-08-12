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

            int picked;

            // 读档恢复：把当年选的那个原样喂回去，不弹面板。
            // 取不到记录（剧本改过、存档是老版本）就退化成正常询问 ——
            // 恢复中途弹个选项面板很怪，但比走错支线、恢复出错误的现场强
            if (ctx.Mode == EDramaPlaybackMode.Restoring && ctx.TryTakeRestoredChoice(out var restored))
            {
                picked = restored;
            }
            else
            {
                // 原样把引用交下去，不在这儿查表 —— 面板要挂着等玩家，
                // 这期间切语言得跟着变，查好的字符串做不到（见 IChoiceView.PickAsync）
                var labels = a.Options.Select(o => o.Text).ToArray();
                picked = await ctx.Choice.PickAsync(labels, ct);

                // 记录放在校验之后：非法选择（面板还没实现、被取消）不该进存档路径，
                // 否则下次恢复会拿一个 -1 去喂，直接把剧情停在这儿
                if (picked >= 0 && picked < a.Options.Length)
                    ctx.ReportChoicePicked(picked);
            }

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

    /// <summary>
    /// 切换游戏内的真实场景。
    ///
    /// <b>Skip / 读档恢复也照切</b> —— 场景是状态不是演出，
    /// 跳过时更不能让剧情停在上一个场景里；恢复时也得把玩家放回当年那个场景。
    /// </summary>
    public sealed class ChangeGameSceneActionHandler : DramaSimpleActionHandler<ChangeGameSceneAction>
    {
        protected override UniTask RunAsync(ChangeGameSceneAction a, IDramaContext ctx, CancellationToken ct)
        {
            if (a.MapSceneId <= 0 && a.MinSceneId <= 0)
            {
                UnityEngine.Debug.LogWarning($"[Drama] #{a.Index} 游戏场景两个 ID 都没填，已跳过");
                return UniTask.CompletedTask;
            }

            return ctx.Game?.ChangeGameSceneAsync(a.MapSceneId, a.MinSceneId, ct) ?? UniTask.CompletedTask;
        }
    }

    /// <summary>
    /// 剧情结束并打开一个界面。
    ///
    /// 只是把界面名报给宿主，真正打开的时机由宿主的收尾流程决定 —— 原因见
    /// <see cref="Services.IDramaGameBridge.RequestOpenUIOnEnd"/>。
    /// 本指令没有后继，执行完剧情就结束了。
    /// </summary>
    public sealed class EndUIDramaActionHandler : DramaSimpleActionHandler<EndUIDramaAction>
    {
        protected override UniTask RunAsync(EndUIDramaAction a, IDramaContext ctx, CancellationToken ct)
        {
            if (!string.IsNullOrEmpty(a.UiPage))
                ctx.Game?.RequestOpenUIOnEnd(a.UiPage);

            return UniTask.CompletedTask;
        }
    }
}

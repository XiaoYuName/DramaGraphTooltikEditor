using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Drama.Runtime.Flow;
using UnityEngine;

namespace Drama.Runtime.Handlers
{
    /// <summary>等待。</summary>
    public sealed class WaitActionHandler : DramaSimpleActionHandler<WaitAction>
    {
        protected override UniTask RunAsync(WaitAction a, IDramaContext ctx, CancellationToken ct)
            => DramaWait.Seconds(a.Seconds, ctx, ct);
    }

    /// <summary>
    /// 等玩家点一下再往下走。
    ///
    /// 走的是台词翻页那同一个口子 —— 点击入口在宿主那边是一个盖满全屏的按钮，
    /// 和对话框显不显示无关，所以整屏 CG（对话框藏着）时照样点得动。
    ///
    /// <b>跳过 / 读档恢复不等人。</b> 恢复期间尤其要紧：这一条不成立的话，
    /// 静默重放会停在这儿等点击，读档就再也走不到存档点了 —— 和
    /// <see cref="TalkActionHandler"/> 里那条判断是同一个理由。
    /// </summary>
    public sealed class WaitInputActionHandler : DramaSimpleActionHandler<WaitInputAction>
    {
        protected override async UniTask RunAsync(WaitInputAction a, IDramaContext ctx, CancellationToken ct)
        {
            if (DramaWait.IsInstant(ctx.Mode))
            {
                return;
            }

            await ctx.Dialogue.WaitForAdvanceAsync(ct);
        }
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
        {
            // ★ 读档恢复是静默重放，任务当年已经领过了 —— 不拦的话玩家每读一次档就重领一次
            if (a.TaskId <= 0 || ctx.Mode == EDramaPlaybackMode.Restoring)
            {
                return UniTask.CompletedTask;
            }

            return ctx.Game.ReceiveTaskAsync(a.TaskId, ct);
        }
    }

    /// <summary>
    /// 发一份奖励并弹"获得奖励"界面。
    ///
    /// <b>等不等玩家由宿主按模式决定</b>（正常模式等玩家关弹窗，自动 / 跳过自己收掉），
    /// 和台词把 mode 交给 View 是同一个路子 —— 这一层不该知道弹窗长什么样。
    ///
    /// <b>读档恢复期间整条跳过</b>：静默重放会把整段剧情重走一遍，
    /// 奖励当年发过了，再发一次等于每读一次档白拿一份。
    /// 这正是 Restoring 和 Skip 要分成两个模式的用处 —— 跳过时玩家在看戏，奖励照发。
    /// </summary>
    public sealed class ReceiveRewardActionHandler : DramaSimpleActionHandler<ReceiveRewardAction>
    {
        protected override UniTask RunAsync(ReceiveRewardAction a, IDramaContext ctx, CancellationToken ct)
        {
            if (a.RewardId <= 0 || ctx.Mode == EDramaPlaybackMode.Restoring)
            {
                return UniTask.CompletedTask;
            }

            return ctx.Game?.ShowRewardAsync(a.RewardId, ctx.Mode, ct) ?? UniTask.CompletedTask;
        }
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
    /// 进小游戏，等玩完再往下走。
    ///
    /// 和 <see cref="ShowUIActionHandler"/> 同一个套路：等不等由宿主按模式决定，
    /// 读档的静默重放期间整条跳过 —— 重放不该把玩家丢进小游戏里。
    /// </summary>
    public sealed class PlayMinGameActionHandler : DramaSimpleActionHandler<PlayMinGameAction>
    {
        protected override UniTask RunAsync(PlayMinGameAction a, IDramaContext ctx, CancellationToken ct)
        {
            // 没填类型就当这条不存在；读档的静默重放也不玩 ——
            // 那些关卡玩家当年已经过了，重放时再把他丢进去等于让他重打一遍
            if (a.MinGameId < 0 || ctx.Mode == EDramaPlaybackMode.Restoring)
            {
                return UniTask.CompletedTask;
            }

            // 返回时一定是通关了：失败由小游戏自己弹失败界面让玩家重试，不回到剧情。
            // 所以这里没有"成败分支"要处理，也不用往选项路径里记东西
            return ctx.Game?.PlayMinGameAsync(a.MinGameId, ct) ?? UniTask.CompletedTask;
        }
    }

    /// <summary>
    /// 剧情中途开一个界面，等玩家关掉再往下走。
    ///
    /// 和 <see cref="ReceiveRewardActionHandler"/> 同一个套路：等不等玩家由宿主按模式决定，
    /// 读档的静默重放期间整条跳过（重放不该往玩家脸上弹界面）。
    /// </summary>
    public sealed class ShowUIActionHandler : DramaSimpleActionHandler<ShowUIAction>
    {
        protected override UniTask RunAsync(ShowUIAction a, IDramaContext ctx, CancellationToken ct)
        {
            if (string.IsNullOrEmpty(a.UiPage) || ctx.Mode == EDramaPlaybackMode.Restoring)
            {
                return UniTask.CompletedTask;
            }

            return ctx.Game?.ShowUIAsync(a.UiPage, ctx.Mode, ct) ?? UniTask.CompletedTask;
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

    /// <summary>
    /// 剧情结束并播一段引导。做法和 <see cref="EndUIDramaActionHandler"/> 完全一致 ——
    /// 只报 ID，什么时候真正开始由宿主的收尾流程决定。
    /// </summary>
    public sealed class EndGuideDramaActionHandler : DramaSimpleActionHandler<EndGuideDramaAction>
    {
        protected override UniTask RunAsync(EndGuideDramaAction a, IDramaContext ctx, CancellationToken ct)
        {
            if (a.GuideId > 0)
                ctx.Game?.RequestStartGuideOnEnd(a.GuideId);

            return UniTask.CompletedTask;
        }
    }

    /// <summary>
    /// 场景 NPC / 场景默认UI 的显隐。改的是一个持续意图，见
    /// <see cref="Services.IDramaGameBridge.SetSceneVisibility"/>。
    /// </summary>
    public sealed class SceneVisibilityActionHandler : DramaSimpleActionHandler<SceneVisibilityAction>
    {
        protected override UniTask RunAsync(SceneVisibilityAction a, IDramaContext ctx, CancellationToken ct)
        {
            ctx.Game?.SetSceneVisibility(a.ShowNpc, a.ShowSceneUI);
            return UniTask.CompletedTask;
        }
    }

    // ==================================================== 功能开放 / 临时显隐
    //
    // 六条都是"把参数原样递给宿主"的一行 Handler，没有等待、不影响流程走向。
    //
    // ⚠ 六条都<b>刻意不拦 `Restoring`</b>（读档的静默重放），和发奖励 / 领任务那种相反：
    //   解锁是幂等的集合写入，重放一遍结果一样；
    //   显隐意图<b>根本不在存档里</b>，正是靠重放才能恢复到退出时的样子。
    //   —— 这两条要是拦了，读档之后引导藏起来的按钮会全冒出来。

    /// <summary>
    /// 解锁系统功能。见 <see cref="Services.IDramaGameBridge.UnlockSystemFunction"/>。
    /// </summary>
    public sealed class UnlockSystemFunctionActionHandler : DramaSimpleActionHandler<UnlockSystemFunctionAction>
    {
        protected override UniTask RunAsync(UnlockSystemFunctionAction a, IDramaContext ctx, CancellationToken ct)
        {
            if (a.FunctionId < 0)
            {
                Debug.LogWarning($"[Drama] #{a.Index} 解锁系统功能没填功能，已跳过");
                return UniTask.CompletedTask;
            }

            ctx.Game?.UnlockSystemFunction(a.FunctionId);
            return UniTask.CompletedTask;
        }
    }

    /// <summary>
    /// 解锁角色功能。见 <see cref="Services.IDramaGameBridge.UnlockCharacterFunction"/>。
    /// </summary>
    public sealed class UnlockCharacterFunctionActionHandler : DramaSimpleActionHandler<UnlockCharacterFunctionAction>
    {
        protected override UniTask RunAsync(UnlockCharacterFunctionAction a, IDramaContext ctx, CancellationToken ct)
        {
            // 两个参数缺一不可：功能是挂在角色上的，"厨房"解锁的是"这个角色的厨房"
            if (a.CharacterId <= 0 || a.FunctionFlag <= 0)
            {
                Debug.LogWarning($"[Drama] #{a.Index} 解锁角色功能的角色ID / 功能没填全" +
                                 $"（角色{a.CharacterId}、功能{a.FunctionFlag}），已跳过");
                return UniTask.CompletedTask;
            }

            ctx.Game?.UnlockCharacterFunction(a.CharacterId, a.FunctionFlag);
            return UniTask.CompletedTask;
        }
    }

    /// <summary>
    /// 解锁地图入口。见 <see cref="Services.IDramaGameBridge.UnlockMap"/>。
    /// </summary>
    public sealed class UnlockMapActionHandler : DramaSimpleActionHandler<UnlockMapAction>
    {
        protected override UniTask RunAsync(UnlockMapAction a, IDramaContext ctx, CancellationToken ct)
        {
            // 小地图ID 允许是 -1（那是"大地图上那个入口本身"），大地图ID 不允许
            if (a.MapSceneId <= 0)
            {
                Debug.LogWarning($"[Drama] #{a.Index} 解锁地图没填大地图ID，已跳过");
                return UniTask.CompletedTask;
            }

            ctx.Game?.UnlockMap(a.MapSceneId, a.SubSceneId);
            return UniTask.CompletedTask;
        }
    }

    /// <summary>
    /// 系统功能按钮的临时显隐。见 <see cref="Services.IDramaGameBridge.SetSystemFunctionVisible"/>。
    /// </summary>
    public sealed class SystemFunctionVisibilityActionHandler : DramaSimpleActionHandler<SystemFunctionVisibilityAction>
    {
        protected override UniTask RunAsync(SystemFunctionVisibilityAction a, IDramaContext ctx, CancellationToken ct)
        {
            if (a.FunctionId < 0)
            {
                Debug.LogWarning($"[Drama] #{a.Index} 系统功能显隐没填功能，已跳过");
                return UniTask.CompletedTask;
            }

            ctx.Game?.SetSystemFunctionVisible(a.FunctionId, a.Visible);
            return UniTask.CompletedTask;
        }
    }

    /// <summary>
    /// 角色功能按钮的临时显隐。见 <see cref="Services.IDramaGameBridge.SetCharacterFunctionVisible"/>。
    /// </summary>
    public sealed class CharacterFunctionVisibilityActionHandler
        : DramaSimpleActionHandler<CharacterFunctionVisibilityAction>
    {
        protected override UniTask RunAsync(CharacterFunctionVisibilityAction a, IDramaContext ctx, CancellationToken ct)
        {
            if (a.CharacterId <= 0 || a.FunctionFlag <= 0)
            {
                Debug.LogWarning($"[Drama] #{a.Index} 角色功能显隐的角色ID / 功能没填全" +
                                 $"（角色{a.CharacterId}、功能{a.FunctionFlag}），已跳过");
                return UniTask.CompletedTask;
            }

            ctx.Game?.SetCharacterFunctionVisible(a.CharacterId, a.FunctionFlag, a.Visible);
            return UniTask.CompletedTask;
        }
    }

    /// <summary>
    /// 地图入口的临时显隐。见 <see cref="Services.IDramaGameBridge.SetMapVisible"/>。
    /// </summary>
    public sealed class MapVisibilityActionHandler : DramaSimpleActionHandler<MapVisibilityAction>
    {
        protected override UniTask RunAsync(MapVisibilityAction a, IDramaContext ctx, CancellationToken ct)
        {
            if (a.MapSceneId <= 0)
            {
                Debug.LogWarning($"[Drama] #{a.Index} 地图显隐没填大地图ID，已跳过");
                return UniTask.CompletedTask;
            }

            ctx.Game?.SetMapVisible(a.MapSceneId, a.SubSceneId, a.Visible);
            return UniTask.CompletedTask;
        }
    }
}

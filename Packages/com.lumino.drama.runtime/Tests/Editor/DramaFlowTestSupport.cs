using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Drama.Runtime;
using Drama.Runtime.Flow;
using NUnit.Framework;
using UnityEngine;

namespace Drama.Runtime.Tests
{
    // ============================================================================
    //  流程语义测试用的脚手架。
    //
    //  这些测试全部是 EditMode 的同步测试：Handler 要么立刻完成，要么卡在
    //  UniTaskCompletionSource 上等测试代码手动放行 —— 都不需要 PlayerLoop 转，
    //  所以不用 [UnityTest]，跑得也快、时序完全确定。
    // ============================================================================

    /// <summary>测试专用指令。只有一个名字，用来断言执行顺序。</summary>
    public sealed class MarkAction : DramaAction
    {
        public override string Kind => "标记";
        public string Name;
        public override string Summary => $"标记 · {Name}";
    }

    /// <summary>测试专用指令。执行时挂起，等测试代码 Open() 才放行。</summary>
    public sealed class GateAction : DramaAction
    {
        public override string Kind => "闸门";
        public string Name;
        public override string Summary => $"闸门 · {Name}";
    }

    /// <summary>测试专用指令。执行时直接返回指定的流程结果。</summary>
    public sealed class FlowAction : DramaAction
    {
        public override string Kind => "流程";
        public string Name;
        public DramaFlowResult Result;
        public override string Summary => $"流程 · {Name}";
    }

    // ------------------------------------------------------------ 记录器

    public sealed class ExecutionLog
    {
        readonly List<string> m_Entries = new List<string>();

        public IReadOnlyList<string> Entries => m_Entries;
        public int Count => m_Entries.Count;

        public void Add(string name) => m_Entries.Add(name);
        public int CountOf(string name) => m_Entries.FindAll(e => e == name).Count;
        public int IndexOf(string name) => m_Entries.IndexOf(name);
        public string Trace => string.Join(" → ", m_Entries);

        public void AssertOrder(params string[] expected) =>
            CollectionAssert.AreEqual(expected, m_Entries, $"实际执行顺序：{Trace}");
    }

    // ------------------------------------------------------------ 上下文与 Handler

    /// <summary>
    /// 流程测试用的上下文。服务默认全是 null —— 流程语义那批测试用的 Handler
    /// 压根不碰服务，真要用的测试自己往里塞 mock。
    /// </summary>
    public sealed class TestContext : IDramaContext
    {
        public EDramaPlaybackMode Mode { get; set; } = EDramaPlaybackMode.Normal;

        public Drama.Runtime.Services.IDialogueView       Dialogue     { get; set; }
        public Drama.Runtime.Services.IChoiceView         Choice       { get; set; }
        public Drama.Runtime.Services.IActorStage         Actors       { get; set; }
        public Drama.Runtime.Services.IDramaScreen        Screen       { get; set; }
        public Drama.Runtime.Services.IDramaBackground    Background   { get; set; }
        public Drama.Runtime.Services.IDramaLocalization  Localization { get; set; }
        public Drama.Runtime.Services.IDramaAssetProvider Assets       { get; set; }
        public Drama.Runtime.Services.IDramaAudio         Audio        { get; set; }
        public Drama.Runtime.Services.IDramaGameBridge    Game         { get; set; }

        /// <summary>读档恢复时按顺序喂回去的选择。测试可以直接往里塞。</summary>
        public readonly System.Collections.Generic.Queue<int> RestoredChoices =
            new System.Collections.Generic.Queue<int>();

        /// <summary>玩家（或恢复）实际选了哪些，按顺序。</summary>
        public readonly System.Collections.Generic.List<int> PickedChoices =
            new System.Collections.Generic.List<int>();

        public bool TryTakeRestoredChoice(out int optionIndex)
        {
            if (RestoredChoices.Count == 0)
            {
                optionIndex = -1;
                return false;
            }

            optionIndex = RestoredChoices.Dequeue();
            PickedChoices.Add(optionIndex);
            return true;
        }

        public void ReportChoicePicked(int optionIndex) => PickedChoices.Add(optionIndex);
    }

    public sealed class MarkHandler : DramaSimpleActionHandler<MarkAction>
    {
        readonly ExecutionLog m_Log;
        public MarkHandler(ExecutionLog log) => m_Log = log;

        protected override UniTask RunAsync(MarkAction action, IDramaContext ctx, CancellationToken ct)
        {
            m_Log.Add(action.Name);
            return UniTask.CompletedTask;
        }
    }

    public sealed class FlowHandler : DramaActionHandler<FlowAction>
    {
        readonly ExecutionLog m_Log;
        public FlowHandler(ExecutionLog log) => m_Log = log;

        protected override UniTask<DramaFlowResult> ExecuteAsync(FlowAction action, IDramaContext ctx, CancellationToken ct)
        {
            m_Log.Add(action.Name);
            return UniTask.FromResult(action.Result);
        }
    }

    /// <summary>闸门 Handler。执行到某个闸门时记录并挂起，测试代码调 <see cref="Open"/> 放行。</summary>
    public sealed class GateHandler : DramaSimpleActionHandler<GateAction>
    {
        readonly ExecutionLog m_Log;
        readonly Dictionary<string, UniTaskCompletionSource> m_Pending = new Dictionary<string, UniTaskCompletionSource>();

        public GateHandler(ExecutionLog log) => m_Log = log;

        public bool IsWaiting(string name) => m_Pending.ContainsKey(name);

        public void Open(string name)
        {
            Assert.IsTrue(m_Pending.TryGetValue(name, out var tcs), $"闸门「{name}」还没被执行到");
            m_Pending.Remove(name);
            tcs.TrySetResult();
        }

        protected override UniTask RunAsync(GateAction action, IDramaContext ctx, CancellationToken ct)
        {
            m_Log.Add(action.Name);
            var tcs = new UniTaskCompletionSource();
            m_Pending[action.Name] = tcs;
            return tcs.Task.AttachExternalCancellation(ct);
        }
    }

    // ------------------------------------------------------------ 建图

    /// <summary>
    /// 手搓 DramaScript。指令按 Add 顺序拿到下标，连线用下标。
    /// </summary>
    public sealed class ScriptBuilder
    {
        readonly DramaScript m_Script;

        public ScriptBuilder(long dramaId = 1)
        {
            m_Script = ScriptableObject.CreateInstance<DramaScript>();
            m_Script.DramaId = dramaId;
            m_Script.FormatVersion = DramaScript.CurrentFormatVersion;
            m_Script.EntryIndex = 0;
        }

        public int Mark(string name) => Add(new MarkAction { Name = name });
        public int Gate(string name) => Add(new GateAction { Name = name });
        public int Flow(string name, DramaFlowResult result) => Add(new FlowAction { Name = name, Result = result });

        public int Choice(string name, params int[] optionTargets)
        {
            var options = new ChoiceAction.Option[optionTargets.Length];
            for (int i = 0; i < optionTargets.Length; i++)
                options[i] = new ChoiceAction.Option { Next = optionTargets[i] };
            return Add(new ChoiceAction { Options = options });
        }

        int Add(DramaAction action)
        {
            action.Index = m_Script.Actions.Count;
            m_Script.Actions.Add(action);
            return action.Index;
        }

        /// <summary>连线。给 0 个目标 = 结束，1 个 = 串行，多个 = 并行。</summary>
        public ScriptBuilder Link(int from, params int[] targets)
        {
            m_Script.Actions[from].Next = targets ?? Array.Empty<int>();
            return this;
        }

        public ScriptBuilder Entry(int index)
        {
            m_Script.EntryIndex = index;
            return this;
        }

        /// <summary>按导出器的算法补上 InboundCount（只为让测试数据贴近真实资产）。</summary>
        public DramaScript Build()
        {
            foreach (var a in m_Script.Actions) a.InboundCount = 0;
            foreach (var a in m_Script.Actions)
            {
                if (a.Next == null) continue;
                foreach (var n in a.Next)
                    if (n >= 0 && n < m_Script.Actions.Count)
                        m_Script.Actions[n].InboundCount++;
            }
            foreach (var a in m_Script.Actions)
                if (a.InboundCount == 0) a.InboundCount = 1;

            return m_Script;
        }
    }

    // ------------------------------------------------------------ 跑测试的壳

    public sealed class TestHarness
    {
        public readonly ExecutionLog Log = new ExecutionLog();
        public readonly TestContext Context = new TestContext();
        public readonly GateHandler Gates;
        public readonly DramaPlayer Player;

        /// <summary>每条指令执行时的播放模式。验证读档恢复用：重放段应当是 Restoring，之后是原模式。</summary>
        public readonly System.Collections.Generic.List<(int Index, EDramaPlaybackMode Mode)> ModeTrace =
            new System.Collections.Generic.List<(int, EDramaPlaybackMode)>();

        public TestHarness()
        {
            Gates = new GateHandler(Log);
            var registry = new DramaHandlerRegistry()
                .Register(new MarkHandler(Log))
                .Register(new FlowHandler(Log))
                .Register(Gates);
            Player = new DramaPlayer(registry);

            // ActionExecuting 是在切模式【之后】触发的，所以这里读到的
            // 正是这条指令实际执行时的模式
            Player.ActionExecuting += a => ModeTrace.Add((a.Index, Context.Mode));
        }

        public EDramaPlaybackMode ModeAt(int actionIndex) =>
            ModeTrace.Find(t => t.Index == actionIndex).Mode;

        /// <summary>跑到底并要求同步跑完（图里没有闸门时用）。</summary>
        public DramaPlayResult PlayToEnd(DramaScript script, CancellationToken ct = default)
        {
            var awaiter = Player.PlayAsync(script, Context, ct).GetAwaiter();
            Assert.IsTrue(awaiter.IsCompleted, $"播放没有同步跑完，卡住了。已执行：{Log.Trace}");
            return awaiter.GetResult();
        }

        /// <summary>从头静默重放到 restoreUntilIndex 再正常播。</summary>
        public DramaPlayResult PlayRestoring(DramaScript script, int restoreUntilIndex,
                                             CancellationToken ct = default)
        {
            var awaiter = Player
                .PlayAsync(script, Context, ct, entryIndex: -1, restoreUntilIndex: restoreUntilIndex)
                .GetAwaiter();
            Assert.IsTrue(awaiter.IsCompleted, $"播放没有同步跑完，卡住了。已执行：{Log.Trace}");
            return awaiter.GetResult();
        }

        /// <summary>开始播放但不要求跑完，返回 awaiter 供后续断言。</summary>
        public UniTask<DramaPlayResult>.Awaiter Start(DramaScript script, CancellationToken ct = default) =>
            Player.PlayAsync(script, Context, ct).GetAwaiter();
    }
}

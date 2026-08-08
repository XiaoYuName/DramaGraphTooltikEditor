using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Drama.Runtime.Flow
{
    /// <summary>一次播放的结局。</summary>
    public readonly struct DramaPlayResult
    {
        public enum EKind
        {
            /// <summary>正常走完。</summary>
            Completed = 0,

            /// <summary>请求换剧本，见 <see cref="GotoDramaId"/>。</summary>
            Goto = 1,

            /// <summary>被取消（退出剧情 / 场景销毁）。</summary>
            Cancelled = 2,
        }

        public readonly EKind Kind;
        public readonly long GotoDramaId;

        DramaPlayResult(EKind kind, long gotoDramaId)
        {
            Kind = kind;
            GotoDramaId = gotoDramaId;
        }

        public static readonly DramaPlayResult Completed = default;
        public static readonly DramaPlayResult Cancelled = new DramaPlayResult(EKind.Cancelled, -1);
        public static DramaPlayResult Goto(long dramaId) => new DramaPlayResult(EKind.Goto, dramaId);

        public override string ToString() =>
            Kind == EKind.Goto ? $"Goto({GotoDramaId})" : Kind.ToString();
    }

    /// <summary>
    /// 剧本执行器。负责按 <see cref="DramaAction.Next"/> 的串行 / 并行语义驱动指令，
    /// 具体每条指令干什么全交给 <see cref="IDramaActionHandler"/>。
    ///
    /// <b>并行的做法是结构化 fork-join</b>：
    ///   遇到 <c>Next.Length &gt; 1</c>，从 <see cref="DramaScriptIndex"/> 取预先算好的汇合点，
    ///   各分支跑到汇合点就停，WhenAll 等齐后由本层继续执行汇合点。
    ///   不用运行期计数器，所以选项分支不会把汇合点饿死。详见 DramaScriptIndex 的注释。
    ///
    /// 一个 DramaPlayer 可以反复用来播不同剧本，但同一时刻只能有一次播放在跑
    /// （每次 PlayAsync 内部会新建一个 Runner，状态不共享）。
    /// </summary>
    public sealed class DramaPlayer
    {
        readonly DramaHandlerRegistry m_Handlers;

        /// <summary>
        /// 单次播放最多执行多少条指令。防止导出出错造成的死循环把编辑器整个卡死。
        /// 正常剧本远远够用；真有超长循环剧情再调大。
        /// </summary>
        public int MaxSteps = 1_000_000;

        /// <summary>每执行一条指令前触发。用来做已读记录、调试日志、剧情回放。</summary>
        public event Action<DramaAction> ActionExecuting;

        public DramaPlayer(DramaHandlerRegistry handlers)
        {
            m_Handlers = handlers ?? throw new ArgumentNullException(nameof(handlers));
        }

        /// <param name="entryIndex">从哪条指令开始。负数 = 用剧本自己的 <see cref="DramaScript.EntryIndex"/>（读档时可以传别的）。</param>
        public async UniTask<DramaPlayResult> PlayAsync(
            DramaScript script, IDramaContext ctx, CancellationToken ct, int entryIndex = -1)
        {
            if (script == null) throw new ArgumentNullException(nameof(script));
            if (ctx == null) throw new ArgumentNullException(nameof(ctx));

            if (script.FormatVersion > DramaScript.CurrentFormatVersion)
                throw new NotSupportedException(
                    $"剧本 {script.DramaId} 的格式版本 {script.FormatVersion} 高于运行时支持的 {DramaScript.CurrentFormatVersion}，请更新运行时包");

            var index = DramaScriptIndex.Get(script);
            var start = entryIndex >= 0 ? entryIndex : script.EntryIndex;
            if (!index.IsValidIndex(start)) return DramaPlayResult.Completed;

            var runner = new Runner(script, index, m_Handlers, ctx, this);

            try
            {
                await runner.RunAsync(start, null, ct);
            }
            catch (OperationCanceledException)
            {
                return DramaPlayResult.Cancelled;
            }

            if (ct.IsCancellationRequested) return DramaPlayResult.Cancelled;
            return runner.HasGoto ? DramaPlayResult.Goto(runner.GotoDramaId) : DramaPlayResult.Completed;
        }

        internal void RaiseActionExecuting(DramaAction action) => ActionExecuting?.Invoke(action);

        // ------------------------------------------------------------ 单次播放的状态

        /// <summary>
        /// 嵌套并行时的"停在哪"栈。
        ///
        /// 外层 fork 的分支里又有 fork 时，内层分支既要停在内层汇合点，
        /// 也要停在外层汇合点（内层如果压根没有汇合点，就只剩外层这个约束）。
        /// 所以停止条件是一个集合而不是单个值。嵌套很浅，链表足够。
        /// </summary>
        sealed class StopScope
        {
            public readonly int Index;
            public readonly StopScope Parent;

            public StopScope(int index, StopScope parent)
            {
                Index = index;
                Parent = parent;
            }

            public static bool Contains(StopScope scope, int index)
            {
                for (var s = scope; s != null; s = s.Parent)
                    if (s.Index == index) return true;
                return false;
            }
        }

        sealed class Runner
        {
            readonly DramaScript m_Script;
            readonly DramaScriptIndex m_Index;
            readonly DramaHandlerRegistry m_Handlers;
            readonly IDramaContext m_Ctx;
            readonly DramaPlayer m_Owner;

            int m_Steps;

            internal bool HasGoto;
            internal long GotoDramaId;

            internal Runner(DramaScript script, DramaScriptIndex index, DramaHandlerRegistry handlers,
                            IDramaContext ctx, DramaPlayer owner)
            {
                m_Script = script;
                m_Index = index;
                m_Handlers = handlers;
                m_Ctx = ctx;
                m_Owner = owner;
            }

            /// <summary>
            /// 从 index 开始顺着执行，直到走到 stop 里的某个下标 / 没有后继 / 被要求停下。
            /// </summary>
            internal async UniTask RunAsync(int index, StopScope stop, CancellationToken ct)
            {
                while (true)
                {
                    if (!m_Index.IsValidIndex(index)) return;
                    if (StopScope.Contains(stop, index)) return;
                    if (HasGoto) return;

                    ct.ThrowIfCancellationRequested();

                    if (++m_Steps > m_Owner.MaxSteps)
                        throw new InvalidOperationException(
                            $"剧本 {m_Script.DramaId} 执行超过 {m_Owner.MaxSteps} 条指令，判定为死循环（当前 #{index}）");

                    var action = m_Script.Actions[index];
                    if (action == null) return;

                    m_Owner.RaiseActionExecuting(action);

                    var result = await m_Handlers.Resolve(action).ExecuteAsync(action, m_Ctx, ct);

                    switch (result.Kind)
                    {
                        case DramaFlowResult.EKind.Stop:
                            return;

                        case DramaFlowResult.EKind.Goto:
                            HasGoto = true;
                            GotoDramaId = result.GotoDramaId;
                            return;

                        case DramaFlowResult.EKind.Jump:
                            index = result.JumpTarget;
                            continue;
                    }

                    var next = action.Next;
                    var branchCount = next?.Length ?? 0;

                    if (branchCount == 0) return;                    // 结束
                    if (branchCount == 1) { index = next[0]; continue; }   // 串行

                    // 并行：各分支跑到汇合点为止
                    var join = m_Index.JoinOf(index);
                    var innerStop = join >= 0 ? new StopScope(join, stop) : stop;

                    var branches = new UniTask[branchCount];
                    for (int i = 0; i < branchCount; i++)
                        branches[i] = RunAsync(next[i], innerStop, ct);   // 同步起跑，靠 WhenAll 汇合

                    await UniTask.WhenAll(branches);

                    if (join < 0) return;      // 各分支已经各自跑到底了
                    index = join;              // 汇合点由本层执行，正好保证只跑一次
                }
            }
        }
    }
}

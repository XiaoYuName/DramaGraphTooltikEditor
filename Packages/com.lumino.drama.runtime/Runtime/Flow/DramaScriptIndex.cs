using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Drama.Runtime.Flow
{
    /// <summary>
    /// 一个 <see cref="DramaScript"/> 的静态结构分析结果，播放前算一次、缓存住。
    ///
    /// <b>它存在的唯一理由：并行分支的汇合点。</b>
    ///
    /// 数据里的 <see cref="DramaAction.InboundCount"/> 是【静态入边数】，
    /// 用它做"等所有入边到齐"会死锁：
    ///   图里只要有 <see cref="ChoiceAction"/>，玩家就只会走一条分支，
    ///   未选分支永远不到达汇合点 → 那个汇合点永远等不齐 → 后面整段剧情卡死。
    ///   （另外计数器是共享状态，剧本重播 / 图里有回环时不 reset 就再也走不通。）
    ///
    /// 所以这里改用【结构化 fork-join】：
    ///   对每个 <c>Next.Length &gt; 1</c> 的 fork，预先算出它各条分支的汇合点，
    ///   运行时各分支跑到汇合点就停，<c>WhenAll</c> 等齐后由发起 fork 的那一层继续往下。
    ///   完全不需要运行期计数，也就没有死锁的余地。
    ///
    /// 汇合点 = 各分支可达集合的交集里"最早"的那个节点（即直接后必经点 / immediate post-dominator）。
    /// 交集为空说明这几条分支各跑各的、永不重逢，此时汇合点是 <see cref="NoJoin"/>。
    /// </summary>
    public sealed class DramaScriptIndex
    {
        /// <summary>没有汇合点：各分支各自跑到底。</summary>
        public const int NoJoin = -1;

        static readonly ConditionalWeakTable<DramaScript, DramaScriptIndex> s_Cache =
            new ConditionalWeakTable<DramaScript, DramaScriptIndex>();

        static readonly int[] s_Empty = Array.Empty<int>();

        readonly DramaScript m_Script;
        readonly int[][] m_Successors;   // 每条指令的全部结构后继（Next + 选项分支），已去重去越界
        readonly int[] m_Join;           // fork 指令的汇合点；非 fork 恒为 NoJoin
        readonly int[] m_Depth;          // 从入口 BFS 的最短跳数；-1 = 不可达
        readonly List<string> m_Diagnostics = new List<string>();

        // 可达集合的备忘录。剧本图规模很小（几十~几百条），直接存全集最省事
        readonly Dictionary<int, HashSet<int>> m_ReachCache = new Dictionary<int, HashSet<int>>();

        public DramaScript Script => m_Script;
        public int ActionCount => m_Successors.Length;

        /// <summary>结构上的可疑之处。导出器应该拦住大部分，这里兜底给个日志。</summary>
        public IReadOnlyList<string> Diagnostics => m_Diagnostics;

        // ------------------------------------------------------------ 构造

        /// <summary>取缓存的分析结果，没有就现算。</summary>
        public static DramaScriptIndex Get(DramaScript script)
        {
            if (script == null) throw new ArgumentNullException(nameof(script));
            return s_Cache.GetValue(script, Build);
        }

        /// <summary>不走缓存地重新分析（测试 / 编辑器里改完图重算用）。</summary>
        public static DramaScriptIndex Build(DramaScript script)
        {
            if (script == null) throw new ArgumentNullException(nameof(script));
            return new DramaScriptIndex(script);
        }

        DramaScriptIndex(DramaScript script)
        {
            m_Script = script;

            var count = script.ActionCount;
            m_Successors = new int[count][];
            m_Join = new int[count];
            m_Depth = new int[count];

            BuildSuccessors();
            BuildDepth();
            BuildJoins();
        }

        // ------------------------------------------------------------ 查询

        /// <summary>某条指令的全部结构后继（含选项分支目标）。只用于结构分析，不代表执行顺序。</summary>
        public IReadOnlyList<int> SuccessorsOf(int index) =>
            index >= 0 && index < m_Successors.Length ? m_Successors[index] : s_Empty;

        /// <summary>
        /// 并行 fork 的汇合点下标；不是 fork 或各分支永不重逢时返回 <see cref="NoJoin"/>。
        /// </summary>
        public int JoinOf(int forkIndex) =>
            forkIndex >= 0 && forkIndex < m_Join.Length ? m_Join[forkIndex] : NoJoin;

        /// <summary>从入口走到这条指令的最短跳数；-1 表示入口走不到（死代码）。</summary>
        public int DepthOf(int index) =>
            index >= 0 && index < m_Depth.Length ? m_Depth[index] : -1;

        public bool IsValidIndex(int index) => index >= 0 && index < m_Successors.Length;

        // ------------------------------------------------------------ 后继表

        void BuildSuccessors()
        {
            var buffer = new List<int>();

            for (int i = 0; i < m_Successors.Length; i++)
            {
                var action = m_Script.Actions[i];
                if (action == null)
                {
                    m_Diagnostics.Add($"#{i} 指令为 null（多半是 [SerializeReference] 找不到类型了，检查类名/命名空间/程序集名有没有改过）");
                    m_Successors[i] = s_Empty;
                    continue;
                }

                buffer.Clear();

                if (action.Next != null)
                {
                    foreach (var n in action.Next)
                    {
                        if (!IsValidIndex(n)) { m_Diagnostics.Add($"#{i} {action.Kind} 的后继 {n} 越界，已忽略"); continue; }
                        if (!buffer.Contains(n)) buffer.Add(n);
                    }
                }

                var branchStart = buffer.Count;
                action.CollectBranchTargets(buffer);
                for (int k = buffer.Count - 1; k >= branchStart; k--)
                {
                    var n = buffer[k];
                    if (!IsValidIndex(n) || buffer.IndexOf(n) < k) buffer.RemoveAt(k);
                }

                m_Successors[i] = buffer.Count == 0 ? s_Empty : buffer.ToArray();
            }
        }

        // ------------------------------------------------------------ 深度（BFS）

        void BuildDepth()
        {
            for (int i = 0; i < m_Depth.Length; i++) m_Depth[i] = -1;

            var entry = m_Script.EntryIndex;
            if (!IsValidIndex(entry)) return;

            var queue = new Queue<int>();
            m_Depth[entry] = 0;
            queue.Enqueue(entry);

            while (queue.Count > 0)
            {
                var i = queue.Dequeue();
                foreach (var n in m_Successors[i])
                {
                    if (m_Depth[n] >= 0) continue;
                    m_Depth[n] = m_Depth[i] + 1;
                    queue.Enqueue(n);
                }
            }
        }

        // ------------------------------------------------------------ 汇合点

        void BuildJoins()
        {
            for (int i = 0; i < m_Join.Length; i++)
            {
                m_Join[i] = NoJoin;

                var action = m_Script.Actions[i];
                if (action?.Next == null || action.Next.Length <= 1) continue;   // 不是并行 fork

                m_Join[i] = ComputeJoin(i, action);
            }
        }

        int ComputeJoin(int fork, DramaAction action)
        {
            // ① 各分支的可达集合求交
            HashSet<int> intersection = null;

            foreach (var branch in action.Next)
            {
                if (!IsValidIndex(branch)) continue;

                var reach = Reachable(branch);
                if (intersection == null) intersection = new HashSet<int>(reach);
                else intersection.IntersectWith(reach);

                if (intersection.Count == 0) break;
            }

            if (intersection == null || intersection.Count == 0)
            {
                // 分支永不重逢。合法（比如各自 Goto 别的剧本），只是提醒一下
                m_Diagnostics.Add($"#{fork} {action.Kind} 并行 ×{action.Next.Length}，各分支没有共同汇合点，会各自跑到底");
                return NoJoin;
            }

            // ② 交集里挑"最早"的那个：它能到达交集中其余所有点
            var best = NoJoin;
            var bestDepth = int.MaxValue;

            foreach (var candidate in intersection)
            {
                if (!Reachable(candidate).IsSupersetOf(intersection)) continue;
                var d = DepthRank(candidate);
                if (d < bestDepth) { bestDepth = d; best = candidate; }
            }

            if (best != NoJoin) return best;

            // ③ 兜底：图里有环之类的怪结构，交集里挑不出支配者，退化成取最浅的那个
            foreach (var candidate in intersection)
            {
                var d = DepthRank(candidate);
                if (d < bestDepth) { bestDepth = d; best = candidate; }
            }

            m_Diagnostics.Add($"#{fork} {action.Kind} 的分支交集里找不到唯一汇合点（图里可能有环），退化成取最浅的 #{best}");
            return best;
        }

        int DepthRank(int index)
        {
            var d = m_Depth[index];
            return d < 0 ? int.MaxValue - 1 : d;   // 不可达的排最后，但别撞上 int.MaxValue 哨兵
        }

        /// <summary>从 start 出发能走到的全部指令（含 start 自己）。带环也安全。</summary>
        HashSet<int> Reachable(int start)
        {
            if (m_ReachCache.TryGetValue(start, out var cached)) return cached;

            var set = new HashSet<int>();
            var stack = new Stack<int>();
            stack.Push(start);

            while (stack.Count > 0)
            {
                var i = stack.Pop();
                if (!set.Add(i)) continue;
                foreach (var n in m_Successors[i]) stack.Push(n);
            }

            m_ReachCache[start] = set;
            return set;
        }
    }
}

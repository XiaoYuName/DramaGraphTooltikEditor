using System.Collections.Generic;
using System.Linq;
using Drama.Runtime;
using Unity.GraphToolkit.Editor;
using UnityEngine;

namespace Drama.Editor.Export
{
    /// <summary>
    /// 导出过程中的收集器 + 求值器。
    ///
    /// 一个节点可能产出多条指令（比如对话节点里有 N 个台词块）。
    /// 同一节点内部 Emit 的指令会自动【串行】串好；
    /// 节点与节点之间的连接由 <see cref="DramaExporter"/> 在遍历结束后按
    /// 「一个出口连几条线」决定是串行还是并行。
    /// </summary>
    internal sealed class DramaExportContext
    {
        readonly List<DramaAction> m_Actions = new List<DramaAction>();

        // 每个节点产出的指令区间：节点ID → 第一条 / 最后一条的下标
        readonly Dictionary<Hash128, int> m_FirstOf = new Dictionary<Hash128, int>();
        readonly Dictionary<Hash128, int> m_LastOf = new Dictionary<Hash128, int>();

        readonly List<string> m_Errors = new List<string>();
        readonly List<string> m_Warnings = new List<string>();

        INode m_Current;

        internal List<DramaAction> Actions => m_Actions;
        internal IReadOnlyList<string> Errors => m_Errors;
        internal IReadOnlyList<string> Warnings => m_Warnings;
        internal bool HasError => m_Errors.Count > 0;

        // ------------------------------------------------------------ 节点区间

        /// <summary>切换当前正在导出的节点。之后 Emit 的指令都算在它名下。</summary>
        internal void BeginNode(INode node) => m_Current = node;

        internal bool TryGetFirstAction(INode node, out int index) =>
            m_FirstOf.TryGetValue(node.ID, out index);

        internal bool TryGetLastAction(INode node, out int index) =>
            m_LastOf.TryGetValue(node.ID, out index);

        internal bool ProducedActions(INode node) => m_FirstOf.ContainsKey(node.ID);

        /// <summary>写出一条指令。同一节点内连续 Emit 会自动串成串行链。</summary>
        internal int Emit(DramaAction action)
        {
            if (action == null) return -1;

            action.Index = m_Actions.Count;

            // 同一节点内部：上一条串行指向这一条
            if (m_Current != null && m_LastOf.TryGetValue(m_Current.ID, out var prev))
                m_Actions[prev].Next = new[] { action.Index };

            m_Actions.Add(action);

            if (m_Current != null)
            {
                if (!m_FirstOf.ContainsKey(m_Current.ID))
                    m_FirstOf[m_Current.ID] = action.Index;
                m_LastOf[m_Current.ID] = action.Index;
            }

            return action.Index;
        }

        /// <summary>
        /// 设置某节点最后一条指令的后继。
        /// targets 有 1 个 = 串行；多个 = 并行。
        /// </summary>
        internal void SetNext(INode from, IReadOnlyList<int> targets)
        {
            if (!m_LastOf.TryGetValue(from.ID, out var last)) return;
            m_Actions[last].Next = targets == null || targets.Count == 0
                ? System.Array.Empty<int>()
                : targets.ToArray();
        }

        /// <summary>统计每条指令的入边数量，用于标记汇合点。</summary>
        internal void ComputeInbound()
        {
            foreach (var a in m_Actions) a.InboundCount = 0;

            foreach (var a in m_Actions)
            {
                if (a.Next == null) continue;
                foreach (var n in a.Next)
                    if (n >= 0 && n < m_Actions.Count)
                        m_Actions[n].InboundCount++;
            }

            // 入口没有入边，规范化成 1，免得运行时把它当成"永远等不齐"
            foreach (var a in m_Actions)
                if (a.InboundCount == 0) a.InboundCount = 1;
        }

        // ------------------------------------------------------------ 求值

        /// <summary>
        /// 取端口的值。
        /// 未连线 → 端口内嵌编辑框里的值；已连线 → 去上游节点求值。
        /// </summary>
        internal T Eval<T>(IPort port, T fallback = default)
        {
            if (port == null) return fallback;

            if (!port.IsConnected)
                return port.TryGetValue<T>(out var inline) ? inline : fallback;

            var src = port.FirstConnectedPort;
            if (src == null) return fallback;

            switch (src.GetNode())
            {
                case IConstantNode c:
                    return c.TryGetValue<T>(out var cv) ? cv : fallback;

                case IVariableNode vn:
                    return vn.Variable != null && vn.Variable.TryGetDefaultValue<T>(out var vv) ? vv : fallback;

                default:
                    return TryEvalCustom(src, out T custom) ? custom : fallback;
            }
        }

        /// <summary>本工程自定义的"值提供者"节点。</summary>
        bool TryEvalCustom<T>(IPort srcPort, out T value)
        {
            value = default;
            var node = srcPort.GetNode();

            // 多语言节点：输出 DramaLocalizationProt
            if (node is LocalizationNode || node is LocalizationAudioNode)
            {
                if (typeof(T) != typeof(DramaLocalizationProt)) return false;

                var table = Eval<string>(node.GetInputPortByName("LocalizationTable"), string.Empty);
                var key = Eval<string>(node.GetInputPortByName("LocalizationKey"), string.Empty);

                object boxed = new DramaLocalizationProt { Table = table, Value = key };
                value = (T)boxed;
                return true;
            }

            return false;
        }

        /// <summary>把编辑器的多语言结构转成运行时结构。</summary>
        internal LocalizedRef EvalLocalized(IPort port)
        {
            var prot = Eval<DramaLocalizationProt>(port, null);
            if (prot == null) return default;
            return new LocalizedRef { Table = prot.Table, Key = prot.Value };
        }

        /// <summary>取节点选项的值。</summary>
        internal T Option<T>(INode node, string optionName, T fallback = default)
        {
            var opt = node?.GetNodeOptionByName(optionName);
            if (opt == null) return fallback;
            return opt.TryGetValue<T>(out var v) ? v : fallback;
        }

        /// <summary>取端口值的快捷方式（按端口名）。</summary>
        internal T Port<T>(INode node, string portName, T fallback = default) =>
            Eval(node?.GetInputPortByName(portName), fallback);

        // ------------------------------------------------------------ 诊断

        internal void Error(string message, INode node = null) => m_Errors.Add(Describe(message, node));
        internal void Warn(string message, INode node = null) => m_Warnings.Add(Describe(message, node));

        static string Describe(string message, INode node)
        {
            if (node == null) return message;
            var title = string.IsNullOrEmpty(node.Title) ? node.GetType().Name : node.Title;
            return $"[{title}] {message}";
        }
    }
}

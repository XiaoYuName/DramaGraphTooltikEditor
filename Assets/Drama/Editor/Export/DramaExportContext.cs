using System.Collections.Generic;
using Drama.Runtime;
using Unity.GraphToolkit.Editor;
using UnityEngine;

namespace Drama.Editor.Export
{
    /// <summary>
    /// 导出时传给每个节点的上下文。提供三件事：
    ///   · <see cref="Emit"/>  写出一条运行时指令
    ///   · <see cref="Eval{T}"/> 求端口的值（自动处理"连线了就去上游取"）
    ///   · <see cref="Error"/> / <see cref="Warn"/> 收集问题
    ///
    /// 节点之间的 Next 串联不用节点自己管 —— 一个节点内部 Emit 的多条指令会自动依次串好，
    /// 节点与节点之间由 <see cref="DramaExporter"/> 在遍历结束后统一回填。
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

        internal IReadOnlyList<DramaAction> Actions => m_Actions;
        internal IReadOnlyList<string> Errors => m_Errors;
        internal IReadOnlyList<string> Warnings => m_Warnings;
        internal bool HasError => m_Errors.Count > 0;

        // ------------------------------------------------------------ 节点区间

        internal void BeginNode(INode node) => m_Current = node;

        internal bool TryGetFirstAction(INode node, out int index) =>
            m_FirstOf.TryGetValue(node.ID, out index);

        internal bool TryGetLastAction(INode node, out int index) =>
            m_LastOf.TryGetValue(node.ID, out index);

        /// <summary>把一条指令写进结果。同一个节点内连续 Emit 会自动串成链。</summary>
        internal int Emit(DramaAction action)
        {
            if (action == null) return -1;

            action.Index = m_Actions.Count;

            // 同一节点内部：上一条指向这一条
            if (m_Current != null && m_LastOf.TryGetValue(m_Current.ID, out var prev))
                m_Actions[prev].Next = action.Index;

            m_Actions.Add(action);

            if (m_Current != null)
            {
                if (!m_FirstOf.ContainsKey(m_Current.ID))
                    m_FirstOf[m_Current.ID] = action.Index;
                m_LastOf[m_Current.ID] = action.Index;
            }

            return action.Index;
        }

        /// <summary>把 from 节点的最后一条指令接到 to 节点的第一条。</summary>
        internal void Link(INode from, INode to)
        {
            if (!m_LastOf.TryGetValue(from.ID, out var last)) return;
            if (!m_FirstOf.TryGetValue(to.ID, out var first)) return;
            m_Actions[last].Next = first;
        }

        // ------------------------------------------------------------ 求值

        /// <summary>
        /// 取端口的值。
        /// 端口【未连线】→ 取端口上内嵌编辑框里的值；
        /// 端口【已连线】→ 去上游节点求值（常量节点 / 变量节点 / 多语言节点）。
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
                    // 自定义的"值提供者"节点走这里
                    if (TryEvalCustom(src, out T custom)) return custom;
                    return fallback;
            }
        }

        /// <summary>
        /// 本工程自定义的值提供节点。
        /// 现在用显式分支处理；等提供者变多了可以抽成 IDramaValueProvider 接口，
        /// 让节点自己回答，这里就不用改了。
        /// </summary>
        bool TryEvalCustom<T>(IPort srcPort, out T value)
        {
            value = default;
            var node = srcPort.GetNode();

            // LocalizationNode / LocalizationAudioNode：输出多语言引用
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

        // ------------------------------------------------------------ 诊断

        internal void Error(string message, INode node = null) =>
            m_Errors.Add(Describe(message, node));

        internal void Warn(string message, INode node = null) =>
            m_Warnings.Add(Describe(message, node));

        static string Describe(string message, INode node)
        {
            if (node == null) return message;
            var title = string.IsNullOrEmpty(node.Title) ? node.GetType().Name : node.Title;
            return $"[{title}] {message}";
        }
    }
}

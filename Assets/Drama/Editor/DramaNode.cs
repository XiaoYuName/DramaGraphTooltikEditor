using System;
using Unity.GraphToolkit.Editor;
using UnityEngine;

namespace Drama.Editor
{
    /// <summary>
    /// 基础剧情流节点
    /// </summary>
    [Serializable]
    public abstract class DramaNode : Node
    {
        public const string NodeProtName = "DramaProtName";

        public const string EventIDName = "EventID";

        /// <summary>
        /// 剧情流节点共用的进/出端口定义。
        /// 抽成静态方法是因为 C# 单继承 —— DramaContextNode 必须继承 ContextNode，
        /// 没法同时继承 DramaNode，只能共用这段定义。
        /// </summary>
        internal static void DefineProtPorts(IPortDefinitionContext context)
        {
            context.AddInputPort(NodeProtName)
                .WithConnectorUI(PortConnectorUI.Arrowhead)
                .WithDisplayName("输入")
                .Build();

            context.AddOutputPort(NodeProtName)
                .WithConnectorUI(PortConnectorUI.Arrowhead)
                .WithDisplayName("输出")
                .Build();
        }

        /// <summary>剧情流节点共用的选项定义。</summary>
        internal static void DefineCommonOptions(IOptionDefinitionContext context)
        {
            context.AddOption<long>(EventIDName)
                .WithDefaultValue(-1)
                .WithTooltip("事件ID")
                .Build();
        }

        protected override void OnDefinePorts(IPortDefinitionContext context)
        {
            DefineProtPorts(context);
        }

        protected override void OnDefineOptions(IOptionDefinitionContext context)
        {
            base.OnDefineOptions(context);
            DefineCommonOptions(context);
        }


    }
}


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
        
        protected override void OnDefinePorts(IPortDefinitionContext context)
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

        protected override void OnDefineOptions(IOptionDefinitionContext context)
        {
            base.OnDefineOptions(context);
            context.AddOption<long>(EventIDName)
                .WithDefaultValue(-1)
                .WithTooltip("事件ID")
                .Build();
        }
        

    }
}


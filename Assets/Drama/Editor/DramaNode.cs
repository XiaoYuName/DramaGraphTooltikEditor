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
            context.AddInputPort<DramaProt>(NodeProtName)
                .WithConnectorUI(PortConnectorUI.Arrowhead)
                .WithDisplayName("Prot")
                .Build();
            
            context.AddOutputPort<DramaProt>(NodeProtName)
                .WithConnectorUI(PortConnectorUI.Arrowhead)
                .WithDisplayName("Prot")
                .Build();
        }

        protected override void OnDefineOptions(IOptionDefinitionContext context)
        {
            base.OnDefineOptions(context);
            context.AddOption<long>(EventIDName)
                .WithDefaultValue(-1)
                .WithTooltip("事件的唯一ID")
                .Build();
        }
        

    }
}


using Unity.GraphToolkit.Editor;
using UnityEngine;

namespace Drama.Editor
{
    public class StartDramaNode : Node
    {
        protected override void OnDefinePorts(IPortDefinitionContext context)
        {
            context.AddInputPort<long>("DramaID")
                .WithDefaultValue(-1)
                .WithConnectorUI(PortConnectorUI.Arrowhead)
                .WithDisplayName("剧情ID")
                .Build();
            context.AddOutputPort<DramaProt>(DramaNode.NodeProtName)
                .WithConnectorUI(PortConnectorUI.Arrowhead)
                .WithDisplayName("Prot")
                .Build();
        }

        protected override void OnDefineOptions(IOptionDefinitionContext context)
        {
            base.OnDefineOptions(context);
        }
    }
}


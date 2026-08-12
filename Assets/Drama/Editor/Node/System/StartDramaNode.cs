using Unity.GraphToolkit.Editor;
using UnityEngine;

namespace Drama.Editor
{
    [Node("终端","Assets/Drama/Assets/Start.png","进入")]
    [System.Serializable]
    public class StartDramaNode : Node
    {
        public const string DramaID = "DramaID";
        
        protected override void OnDefinePorts(IPortDefinitionContext context)
        {
            context.AddInputPort<long>(DramaID)
                .WithDefaultValue(-1)
                .WithConnectorUI(PortConnectorUI.Arrowhead)
                .WithDisplayName("剧情ID")
                .Build();

            context.AddOutputPort("Output");
        }

        protected override void OnDefineOptions(IOptionDefinitionContext context)
        {
            base.OnDefineOptions(context);
        }
    }
}


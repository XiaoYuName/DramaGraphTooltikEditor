using Unity.GraphToolkit.Editor;
using UnityEngine;

namespace Drama.Editor
{
    [System.Serializable]
    [Node("终端","Assets/Drama/Assets/Goto.png","跳转结束")]
    public class GotoDramaNode : Node
    {
        protected override void OnDefinePorts(IPortDefinitionContext context)
        {
            base.OnDefinePorts(context);
            context.AddInputPort(DramaNode.NodeProtName)
                .WithDisplayName("输入")
                .Build();
            
            context.AddInputPort<long>(StartDramaNode.DramaID)
                .WithDefaultValue(-1)
                .WithConnectorUI(PortConnectorUI.Arrowhead)
                .WithDisplayName("剧情ID")
                .Build();
        }
    }
}


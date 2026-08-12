using Unity.GraphToolkit.Editor;
using UnityEngine;

namespace Drama.Editor
{
    [System.Serializable]
    [Node("终端","Assets/Drama/Assets/End.png","结束")]
    public class EndDramaNode : Node
    {
    
        protected override void OnDefinePorts(IPortDefinitionContext context)
        {
            base.OnDefinePorts(context);
            context.AddInputPort(DramaNode.NodeProtName)
                .WithDisplayName("输入")
                .Build();
            
        }
    }
}


using Unity.GraphToolkit.Editor;
using UnityEngine;

namespace Drama.Editor
{
    [System.Serializable]
    [Node("命令/任务","Assets/Drama/Assets/Task.png","领取任务")]
    public class ReceiveTask : DramaNode
    {
        public const string TaskID = "ReceiveTask";
        
        protected override void OnDefinePorts(IPortDefinitionContext context)
        {
            base.OnDefinePorts(context);
            
            context.AddInputPort<long>(TaskID)
                .WithDefaultValue(-1)
                .WithDisplayName("任务ID")
                .WithCapacity(PortCapacity.Single)
                .Build();
        }
        
        
    }
}


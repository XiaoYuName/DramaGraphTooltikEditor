using Unity.GraphToolkit.Editor;
using UnityEngine;

namespace Drama.Editor
{
    [System.Serializable]
    [Node("命令/流程","Assets/Drama/Assets/Wait.png","等待")]
    public class WaitNode : DramaNode
    {
        public const string WaitNodeName = "Wait";

        protected override void OnDefinePorts(IPortDefinitionContext context)
        {
            base.OnDefinePorts(context);

            context.AddInputPort<float>(WaitNodeName)
                .WithDefaultValue(1)
                .WithDisplayName("等待")
                .Build();
        }
    }
}

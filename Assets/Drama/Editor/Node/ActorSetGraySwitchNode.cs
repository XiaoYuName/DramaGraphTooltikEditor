using Unity.GraphToolkit.Editor;
using UnityEngine;

namespace Drama.Editor
{
    [System.Serializable]
    [Node("命令/骨骼","Assets/Drama/Assets/Scale.png","讲话人缩放")]
    public class ActorSetGraySwitchNode : ActorDramaNode
    {
        public const string IsFade = "isFade";
        public const string IsGray = "isGray";
        
        protected override void OnDefinePorts(IPortDefinitionContext context)
        {
            base.OnDefinePorts(context);
            context.AddInputPort<bool>(IsFade)
                .WithDefaultValue(false)
                .WithDisplayName("置灰")
                .Build();
            context.AddInputPort<bool>(IsGray)
                .WithDefaultValue(true)
                .WithDisplayName("微缩")
                .Build();
        }
    }
}


using DG.Tweening;
using Unity.GraphToolkit.Editor;
using UnityEngine;

namespace Drama.Editor
{
    [System.Serializable]
    [Node("命令/变换","Assets/Drama/Assets/Rotation.png","旋转")]
    public class ActorRotationNode : ActorDramaNode
    {
        public const string ActorRotationName = "actorRotation";
        
        public const string Duration = "duration";
        
        public const string ease = "ease";
        
        protected override void OnDefinePorts(IPortDefinitionContext context)
        {
            base.OnDefinePorts(context);

            context.AddInputPort<Vector3>(ActorRotationName)
                .WithDefaultValue(Vector3.zero)
                .WithDisplayName("旋转")
                .Build();

            context.AddInputPort<float>(Duration)
                .WithDefaultValue(0)
                .WithDisplayName("时间")
                .Build();

            context.AddInputPort<Ease>(ease)
                .WithDefaultValue(Ease.Linear)
                .WithDisplayName("曲线")
                .Build();
        }
    }
}


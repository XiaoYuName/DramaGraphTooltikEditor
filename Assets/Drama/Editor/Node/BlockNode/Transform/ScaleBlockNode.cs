using DG.Tweening;
using Unity.GraphToolkit.Editor;
using UnityEngine;

namespace Drama.Editor
{
    [System.Serializable]
    [Node("变换","Assets/Drama/Assets/Scale.png","缩放")]
    [UseWithContext(typeof(ActorTransformNode),typeof(ScreenTransformNode))]
    public class ScaleBlockNode  : BlockNode
    {
        public const string ActorScaleName = "actorScale";
        
        public const string Duration = "duration";
        
        public const string ease = "ease";
    
        protected override void OnDefinePorts(IPortDefinitionContext context)
        {
            base.OnDefinePorts(context);

            context.AddInputPort<Vector3>(ActorScaleName)
                .WithDefaultValue(Vector3.zero)
                .WithDisplayName("缩放")
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

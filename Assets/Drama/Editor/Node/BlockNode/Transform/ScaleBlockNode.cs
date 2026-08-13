using DG.Tweening;
using Unity.GraphToolkit.Editor;
using UnityEngine;

namespace Drama.Editor
{
    [System.Serializable]
    [Node("变换","Assets/Drama/Assets/Scale.png","缩放")]
    [UseWithContext(typeof(ActorTransformNode),typeof(ScreenTransformNode),typeof(CGTransformNode))]
    public class ScaleBlockNode  : BlockNode
    {
        public const string ActorScaleName = "actorScale";
        
        public const string Duration = "duration";
        
        public const string ease = "ease";
    
        protected override void OnDefinePorts(IPortDefinitionContext context)
        {
            base.OnDefinePorts(context);

            context.AddInputPort<Vector3>(ActorScaleName)
                .WithDefaultValue(Vector3.one)
                .WithDisplayName("缩放")
                .WithTooltip("倍率，1 = 原始大小。默认 one 而不是 zero —— 漏填会缩成看不见")
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

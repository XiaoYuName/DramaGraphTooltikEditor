using DG.Tweening;
using Unity.GraphToolkit.Editor;
using UnityEngine;

namespace Drama.Editor
{
    [System.Serializable]
    [Node("变换","Assets/Drama/Assets/Position.png","位置")]
    // 同一个块在不同容器下产出不同的指令（立绘位移 / 背景位移 / CG 位移），
    // 是容器决定语义 —— 见各自导出器里的注释
    [UseWithContext(typeof(ActorTransformNode),typeof(ScreenTransformNode),typeof(CGTransformNode))]
    public class PositionBlockNode : BlockNode
    {
        public const string ActorPositionName = "actorPosition";
        
        public const string Duration = "duration";
        
        public const string ease = "ease";
        
        protected override void OnDefinePorts(IPortDefinitionContext context)
        {
            base.OnDefinePorts(context);

            context.AddInputPort<Vector2>(ActorPositionName)
                .WithDefaultValue(Vector2.zero)
                .WithDisplayName("位置")
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

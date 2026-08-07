using System;
using DG.Tweening;
using Unity.GraphToolkit.Editor;
using UnityEngine;

namespace Drama.Editor
{
    [Node("命令/变换","Assets/Drama/Assets/Position.png","位置")]
    [Serializable]
    public class ActorPositionNode : ActorDramaNode
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


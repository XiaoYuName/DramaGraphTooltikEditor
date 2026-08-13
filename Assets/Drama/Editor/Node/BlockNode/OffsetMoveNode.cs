using DG.Tweening;
using Unity.GraphToolkit.Editor;
using UnityEngine;

namespace Drama.Editor
{
    [System.Serializable]
    [Node("命令/变换","Assets/Drama/Assets/ActionMovement.png","小动作")]
    [UseWithContext(typeof(ActorOffsetMoveNode),typeof(CGOffsetMoveNode))]
    public class OffsetMoveNode : BlockNode
    {
        public const string Offset = "Offset";
        
        public const string Duration = "duration";
        
        public const string ease = "ease";
        
        public const string count = "count";
        
        public const string loopType = "loopType";
        
        protected override void OnDefinePorts(IPortDefinitionContext context)
        {
            base.OnDefinePorts(context);
            context.AddInputPort<Vector3>(Offset)
                .WithDefaultValue(Vector3.zero)
                .WithDisplayName("偏移")
                .Build();
            
            context.AddInputPort<float>(Duration)
                .WithDefaultValue(0)
                .WithDisplayName("时间")
                .Build();

            context.AddInputPort<Ease>(ease)
                .WithDefaultValue(Ease.Linear)
                .WithDisplayName("曲线")
                .Build();

            context.AddInputPort<int>(count)
                .WithDefaultValue(1)
                .WithDisplayName("次数")
                .Build();

            context.AddInputPort<LoopType>(loopType)
                .WithDefaultValue(LoopType.Restart)
                .WithDisplayName("循环")
                .Build();

        }


        /// <summary>
        ///        <para>
        /// Called when the node is created or when the graph is enabled.
        /// </para>
        ///      </summary>
        public override void OnEnable()
        {
            base.OnEnable();
            if (ColorUtility.TryParseHtmlString("#002FA759", out var color))
            {
                DefaultColor = color;
            }
        }
    }
}


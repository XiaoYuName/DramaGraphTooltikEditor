using Unity.GraphToolkit.Editor;

namespace Drama.Editor
{
    /// <summary>Animator.SetInteger。</summary>
    [System.Serializable]
    [Node("命令/立绘/Animator","Assets/Drama/Assets/Event.png","触发Int")]
    public class ActorAnimSetIntNode : ActorAnimParameterNode
    {
        public const string Value = "Value";

        protected override void OnDefinePorts(IPortDefinitionContext context)
        {
            base.OnDefinePorts(context);

            context.AddInputPort<int>(Value)
                .WithDefaultValue(0)
                .WithDisplayName("值")
                .Delayed()
                .Build();
        }
    }
}

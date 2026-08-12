using Unity.GraphToolkit.Editor;

namespace Drama.Editor
{
    /// <summary>Animator.SetFloat。</summary>
    [System.Serializable]
    [Node("命令/立绘/Animator","Assets/Drama/Assets/Event.png","触发Float")]
    public class ActorAnimSetFloatNode : ActorAnimParameterNode
    {
        public const string Value = "Value";

        protected override void OnDefinePorts(IPortDefinitionContext context)
        {
            base.OnDefinePorts(context);

            context.AddInputPort<float>(Value)
                .WithDefaultValue(0f)
                .WithDisplayName("值")
                .Delayed()
                .Build();
        }
    }
}

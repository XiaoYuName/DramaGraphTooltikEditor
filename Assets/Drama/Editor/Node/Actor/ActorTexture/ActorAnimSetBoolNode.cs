using Unity.GraphToolkit.Editor;

namespace Drama.Editor
{
    /// <summary>Animator.SetBool。</summary>
    [System.Serializable]
    [Node("命令/立绘/Animator","Assets/Drama/Assets/Event.png","触发Bool")]
    public class ActorAnimSetBoolNode : ActorAnimParameterNode
    {
        public const string Value = "Value";

        protected override void OnDefinePorts(IPortDefinitionContext context)
        {
            base.OnDefinePorts(context);

            context.AddInputPort<bool>(Value)
                .WithDefaultValue(true)
                .WithDisplayName("值")
                .Build();
        }
    }
}

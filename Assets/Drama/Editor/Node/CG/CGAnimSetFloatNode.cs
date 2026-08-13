using Unity.GraphToolkit.Editor;

namespace Drama.Editor
{
    /// <summary>CG 的 Animator.SetFloat。</summary>
    [System.Serializable]
    [Node("命令/CG/Animator","Assets/Drama/Assets/Event.png","CG触发Float")]
    public class CGAnimSetFloatNode : CGAnimParameterNode
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

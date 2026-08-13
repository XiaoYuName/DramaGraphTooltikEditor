using Unity.GraphToolkit.Editor;

namespace Drama.Editor
{
    /// <summary>CG 的 Animator.SetBool。</summary>
    [System.Serializable]
    [Node("命令/CG/Animator","Assets/Drama/Assets/Event.png","CG触发Bool")]
    public class CGAnimSetBoolNode : CGAnimParameterNode
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

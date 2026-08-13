using Unity.GraphToolkit.Editor;

namespace Drama.Editor
{
    /// <summary>CG 的 Animator.SetInteger。</summary>
    [System.Serializable]
    [Node("命令/CG/Animator","Assets/Drama/Assets/Event.png","CG触发Int")]
    public class CGAnimSetIntNode : CGAnimParameterNode
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

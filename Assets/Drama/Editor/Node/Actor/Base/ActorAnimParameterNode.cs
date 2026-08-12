using Unity.GraphToolkit.Editor;

namespace Drama.Editor
{
    /// <summary>
    /// 「设置 Animator 参数」这一族的公共部分：一个参数名。
    /// 值端口由各子类按自己的类型加。
    ///
    /// <b>为什么是四个节点而不是一个带"参数类型"下拉的节点</b>：
    /// Unity 的 Animator 参数就 Bool / Int / Float / Trigger 四种，各对应一个 SetXxx，
    /// 类型不能混。做成下拉的话值端口要跟着切类型，图里已经连好的线会被切断，
    /// 而且策划改错一次就得重连。四个节点各自类型固定，连线永远是对的。
    /// </summary>
    [System.Serializable]
    public abstract class ActorAnimParameterNode : ActorDramaNode
    {
        public const string ParameterName = "ParameterName";

        protected override void OnDefinePorts(IPortDefinitionContext context)
        {
            // 基类加的是流程进/出口 + 角色ID 的透传端口
            base.OnDefinePorts(context);

            context.AddInputPort<string>(ParameterName)
                .WithDefaultValue("")
                .WithDisplayName("参数名")
                .WithTooltip("Animator 里那个参数的名字，大小写要和 Animator 面板里的一致")
                .Build();
        }
    }
}

using Unity.GraphToolkit.Editor;

namespace Drama.Editor
{
    /// <summary>
    /// 「设置 CG 的 Animator 参数」这一族的公共部分：一个参数名。
    ///
    /// <b>和立绘那组的唯一区别是没有 ID</b>：CG 是单槽位，同时只有一张在台上，
    /// 加个 CG ID 也只能拿来校验、不能拿来寻址 —— 那属于"看着精确其实没用"的参数，
    /// 每多一个必填项就多一个填错的机会。
    ///
    /// 为什么是四个节点而不是一个带"参数类型"下拉的：见 <see cref="ActorAnimParameterNode"/>。
    /// </summary>
    [System.Serializable]
    public abstract class CGAnimParameterNode : DramaNode
    {
        public const string ParameterName = "ParameterName";

        protected override void OnDefinePorts(IPortDefinitionContext context)
        {
            base.OnDefinePorts(context);

            context.AddInputPort<string>(ParameterName)
                .WithDefaultValue("")
                .WithDisplayName("参数名")
                .WithTooltip("CG 模型 Animator 里那个参数的名字，大小写要和 Animator 面板里的一致")
                .Build();
        }
    }
}

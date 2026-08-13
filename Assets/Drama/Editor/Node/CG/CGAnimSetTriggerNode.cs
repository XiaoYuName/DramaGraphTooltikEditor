using Unity.GraphToolkit.Editor;

namespace Drama.Editor
{
    /// <summary>
    /// CG 的 Animator.SetTrigger / ResetTrigger。
    ///
    /// Trigger 没有值、只有"触发"这个动作，所以比另外三个少一个值端口，
    /// 多一个「方式」选项来区分设置和撤销（理由见 <see cref="EAnimTriggerMode"/>）。
    /// </summary>
    [System.Serializable]
    [Node("命令/CG/Animator","Assets/Drama/Assets/Event.png","CG触发Trigger")]
    public class CGAnimSetTriggerNode : CGAnimParameterNode
    {
        public const string k_Mode = "Mode";

        protected override void OnDefineOptions(IOptionDefinitionContext context)
        {
            base.OnDefineOptions(context);

            context.AddOption<EAnimTriggerMode>(k_Mode)
                .WithDisplayName("方式")
                .WithDefaultValue(EAnimTriggerMode.Set)
                .WithTooltip("触发 = SetTrigger；重置 = ResetTrigger，用来撤掉一个还没被状态机消费的触发")
                .Build();
        }
    }
}

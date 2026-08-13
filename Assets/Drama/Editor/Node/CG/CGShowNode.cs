using Unity.GraphToolkit.Editor;

namespace Drama.Editor
{
    /// <summary>
    /// CG 出现。
    ///
    /// <b>进 CG 会自动把立绘整层藏掉</b>（退出时恢复），不用在图里额外写一条隐藏指令 ——
    /// 这是照原工程的做法：漏配一条就穿帮，做成自动更稳。
    /// </summary>
    [System.Serializable]
    [Node("命令/CG", "Assets/Drama/Assets/ChangeBG.png", "CG出现")]
    public class CGShowNode : CGVisibilityNodeBase
    {
        internal const string k_CgId = "CgID";

        protected override void OnDefineOptions(IOptionDefinitionContext context)
        {
            base.OnDefineOptions(context);

            context.AddOption<long>(k_CgId)
                .WithDisplayName("CG ID")
                .WithDefaultValue(-1L)
                .WithTooltip("CG 配置表的 ID，运行时按它取模型预制体")
                .Delayed()
                .Build();
        }
    }
}

using Unity.GraphToolkit.Editor;
using UnityEngine;

namespace Drama.Editor
{
    /// <summary>
    /// 引导结束：剧情到此为止，并播一段引导。
    ///
    /// 和「UI结束」是姊妹节点，区别只是打开的东西不一样（那个填界面名，这个填引导ID）。
    /// 同样<b>没有输出端口</b> —— "结束"从来不是一条指令，而是"没有后继"这个状态。
    ///
    /// 引导真正开始的时机在剧情<b>收尾之后</b>（关剧情面板、还原进剧情前的界面都做完）：
    /// 引导多半要指着某个界面上的按钮，那些界面得先回来。
    /// </summary>
    [System.Serializable]
    [Node("终端", "Assets/Drama/Assets/End.png", "引导结束")]
    public class EndGuideDramaNode : Node
    {
        public const string GuideID = "GuideID";

        protected override void OnDefinePorts(IPortDefinitionContext context)
        {
            base.OnDefinePorts(context);

            context.AddInputPort(DramaNode.NodeProtName)
                .WithDisplayName("输入")
                .Build();

            context.AddInputPort<long>(GuideID)
                .WithDefaultValue(-1)
                .WithDisplayName("引导ID")
                .Build();
        }
    }
}

using Unity.GraphToolkit.Editor;
using UnityEngine;

namespace Drama.Editor
{
    /// <summary>
    /// 打开界面：剧情中途弹一个界面出来，<b>等玩家把它关掉再往下走</b>。
    ///
    /// 和「UI结束」的区别是时机：那个是"剧情结束了顺便开个界面"，开完剧情就没了；
    /// 这个是剧情演到一半插一个界面，玩家关掉之后剧情接着演。
    ///
    /// 界面ID 就是宿主 UI 系统里的界面名（比如 <c>InventoryUI</c>）。
    ///
    /// 自动 / 跳过模式下界面自己收掉，剧情不停；读档恢复时整条跳过。
    /// </summary>
    [System.Serializable]
    [Node("命令/系统", "Assets/Drama/Assets/Show.png", "打开界面")]
    public class ShowUINode : DramaNode
    {
        public const string UIPageID = "UIPageID";

        protected override void OnDefinePorts(IPortDefinitionContext context)
        {
            base.OnDefinePorts(context);

            context.AddInputPort<string>(UIPageID)
                .WithDefaultValue("")
                .WithDisplayName("界面ID")
                .Build();
        }
    }
}

using Unity.GraphToolkit.Editor;
using UnityEngine;

namespace Drama.Editor
{
    /// <summary>
    /// 系统功能显隐：把主界面那排按钮里的<b>某一个</b>临时藏起来 / 放回来。
    ///
    /// 「系统功能」填的是<b>宿主那套枚举的整数值</b>（本作的对应值见节点使用手册的对照表）——
    /// 剧情系统是跨工程复用的，编辑器不该认识某个游戏有哪些系统功能。
    ///
    /// <b>这是剧情期间的临时覆盖，不进存档。</b> 典型用法是引导：
    /// 把别的按钮先藏掉、只留要教玩家点的那一个，教完再放回来。
    ///
    /// 最终可见 = <b>已解锁 && 没被本节点藏起来</b>。所以：
    /// <list type="bullet">
    /// <item>「显示」<b>开不出</b>一个还没解锁的功能 —— 那要用「解锁」节点</item>
    /// <item>藏起来之后忘了放回来，玩家读一次档就恢复了（这份意图不在存档里）</item>
    /// </list>
    ///
    /// 读档的静默重放期间照常执行 —— 正是靠重放把"退出时藏着的东西"重新藏回去。
    /// </summary>
    [System.Serializable]
    [Node("命令/流程", "Assets/Drama/Assets/Show.png", "系统功能显隐")]
    public class SystemFunctionVisibilityNode : DramaNode
    {
        public const string SystemFunction = "systemFunction";
        public const string Visible        = "visible";

        protected override void OnDefinePorts(IPortDefinitionContext context)
        {
            base.OnDefinePorts(context);

            // 默认 -1 而不是 0：宿主枚举的 0 往往是个合法功能，别把它当成"没填"
            context.AddInputPort<int>(SystemFunction)
                .WithDefaultValue(-1)
                .WithDisplayName("系统功能(枚举值)")
                .Build();

            context.AddInputPort<bool>(Visible)
                .WithDefaultValue(true)
                .WithDisplayName("显示")
                .Build();
        }
    }
}

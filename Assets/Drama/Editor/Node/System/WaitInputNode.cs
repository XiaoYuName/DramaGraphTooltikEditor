using Unity.GraphToolkit.Editor;
using UnityEngine;

namespace Drama.Editor
{
    /// <summary>
    /// 等待点击：停在这儿，等玩家点一下屏幕再往下走。
    ///
    /// <b>给没有台词的场合用</b> —— 比如整屏 CG：画面摆好了但不该自己往下走，
    /// 要等玩家看够。有台词时不需要它，台词本身就会等玩家翻页。
    ///
    /// 和「等待」节点的区别：那个是<b>到点自动走</b>，这个是<b>等人</b>。
    /// 想要"最多等 N 秒"，把两个连成并行分支即可（图的拓扑已经能表达）。
    ///
    /// 跳过 / 读档恢复时不等人，直接往下走。
    /// </summary>
    [System.Serializable]
    [Node("命令/流程", "Assets/Drama/Assets/Wait.png", "等待点击")]
    public class WaitInputNode : DramaNode
    {
    }
}

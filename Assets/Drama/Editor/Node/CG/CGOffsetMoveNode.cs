using Unity.GraphToolkit.Editor;

namespace Drama.Editor
{
    /// <summary>
    /// CG 动作。装的是和立绘动作同一个「小动作」块（<see cref="OffsetMoveNode"/>）。
    ///
    /// 和「CG变换」的区别是<b>相对位移 + 可循环</b>：
    /// CG变换给的是绝对位置，做"推到某个构图"；这个给的是在当前位置上的偏移，
    /// 设成无限循环 + 往复就是缓慢平移的呼吸感镜头。
    ///
    /// <b>循环次数为负 = 无限循环</b>，那种情况下指令不等它结束（否则剧本永远卡在这条），
    /// 由舞台收尾时统一收口 —— 和立绘动作同一套语义。
    ///
    /// 不需要填 CG ID —— 同时只有一张 CG 在台上。
    /// </summary>
    [System.Serializable]
    [Node("命令/CG", "Assets/Drama/Assets/ActionMovement.png", "CG动作")]
    public class CGOffsetMoveNode : DramaContextNode
    {
    }
}

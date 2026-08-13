using Unity.GraphToolkit.Editor;

namespace Drama.Editor
{
    /// <summary>
    /// CG 关闭，同时把立绘层恢复回来。
    ///
    /// 不需要填 CG ID —— 同时只有一张 CG 在台上，关的就是它。
    /// </summary>
    [System.Serializable]
    [Node("命令/CG", "Assets/Drama/Assets/ChangeBG.png", "CG关闭")]
    public class CGHideNode : CGVisibilityNodeBase
    {
    }
}

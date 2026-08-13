using Unity.GraphToolkit.Editor;

namespace Drama.Editor
{
    /// <summary>
    /// CG 变换。做"推近脸部"这类镜头感用 —— 全屏 CG 默认满屏居中，这里配的是在此基础上的偏移和缩放。
    ///
    /// 装的是和立绘、背景<b>同一批</b>位置 / 缩放块（<see cref="PositionBlockNode"/> 等）。
    /// 这是"容器决定语义"那条原则的第三次应用：同一个位置块，
    /// 放立绘容器下产出立绘位移、放背景容器下产出背景位移、放这儿产出 CG 位移。
    ///
    /// 不需要填 CG ID —— 同时只有一张 CG 在台上。
    /// </summary>
    [System.Serializable]
    [Node("命令/CG", "Assets/Drama/Assets/Rect.png", "CG变换")]
    public class CGTransformNode : DramaContextNode
    {
    }
}

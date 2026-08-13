using Unity.GraphToolkit.Editor;

namespace Drama.Editor
{
    /// <summary>
    /// CG 抖动。装的是和立绘抖动<b>同一批</b>块（<see cref="ShakeNode"/> 硬抖 / <see cref="VibrateNode"/> 柔震）。
    ///
    /// <b>抖的是 CG 本身，不是相机</b> —— 这一点和原工程不同。
    /// 原工程 CG 有独立的 CG 相机，所以它抖相机；我们的 CG 和背景共用同一台相机，
    /// 抖相机会把背景一起带上。抖 CG 自己（世界空间的模型跟着 Canvas 里的替身走）
    /// 视觉结果一样，而且不会波及别的层。
    ///
    /// 不需要填 CG ID —— 同时只有一张 CG 在台上。
    /// </summary>
    [System.Serializable]
    [Node("命令/CG", "Assets/Drama/Assets/ShakeIcon.png", "CG抖动")]
    public class CGShakeNode : DramaContextNode
    {
    }
}

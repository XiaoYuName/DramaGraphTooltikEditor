using Unity.GraphToolkit.Editor;

namespace Drama.Editor
{
    /// <summary>
    /// 立绘显示 / 隐藏（Live2D）。
    /// 参数定义全在 <see cref="ActorShowNodeBase"/>，三种立绘只有资源类型不同。
    /// </summary>
    [System.Serializable]
    [Node("命令/立绘/Live2D","Assets/Drama/Assets/ActorTextureShow.png","立绘Live2D")]
    public class ActorLive2DShow : ActorShowNodeBase
    {
    }
}

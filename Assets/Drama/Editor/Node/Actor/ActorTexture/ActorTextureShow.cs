using Unity.GraphToolkit.Editor;

namespace Drama.Editor
{
    /// <summary>
    /// 立绘显示 / 隐藏（普通图片）。
    /// 参数定义全在 <see cref="ActorShowNodeBase"/>，三种立绘只有资源类型不同。
    /// </summary>
    [System.Serializable]
    [Node("命令/立绘/Texture","Assets/Drama/Assets/ActorTextureShow.png","立绘图")]
    public class ActorTextureShow : ActorShowNodeBase
    {
    }
}

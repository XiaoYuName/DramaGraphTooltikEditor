using System;
using Unity.GraphToolkit.Editor;

namespace Drama.Editor
{
    /// <summary>
    /// 立绘显示 / 隐藏（Spine 骨骼）。
    /// 参数定义全在 <see cref="ActorShowNodeBase"/>，三种立绘只有资源类型不同。
    /// </summary>
    [Node("命令/立绘/Spine", "Assets/Drama/Assets/Start.png", "立绘骨骼")]
    [Serializable]
    public class ActorShowNode : ActorShowNodeBase
    {
    }
}

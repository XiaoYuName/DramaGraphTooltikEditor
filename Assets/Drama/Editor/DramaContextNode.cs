using System;
using Unity.GraphToolkit.Editor;

namespace Drama.Editor
{
    /// <summary>
    /// 基础剧情流【容器】节点。
    ///
    /// 和 <see cref="DramaNode"/> 的区别只有一个：它继承 <see cref="ContextNode"/>，
    /// 所以内部可以纵向堆放 <see cref="BlockNode"/>（可拖动排序，Block 之间不需要连线）。
    ///
    /// 端口和选项的定义与 DramaNode 完全一致 —— 复用 DramaNode 里的静态方法，
    /// 不重复写。（C# 单继承，没法让一个类同时是 DramaNode 和 ContextNode。）
    ///
    /// 想让某个节点能装 Block，就让它继承这个类；不需要装 Block 的继续继承 DramaNode。
    /// </summary>
    [Serializable]
    public abstract class DramaContextNode : ContextNode
    {
        protected override void OnDefinePorts(IPortDefinitionContext context)
        {
            DramaNode.DefineProtPorts(context);
        }

        protected override void OnDefineOptions(IOptionDefinitionContext context)
        {
            base.OnDefineOptions(context);
            DramaNode.DefineCommonOptions(context);
        }
    }
}

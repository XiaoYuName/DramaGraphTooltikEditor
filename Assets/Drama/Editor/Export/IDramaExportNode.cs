namespace Drama.Editor.Export
{
    /// <summary>
    /// 能被导出成运行时指令的节点。
    ///
    /// 用接口而不是在 DramaNode 上加抽象方法，是因为要导出的东西横跨三种基类：
    /// <c>DramaNode</c>（普通指令）、<c>DramaContextNode</c>（容器）、
    /// <c>BlockNode</c>（容器里的块）—— 它们没有共同的自定义基类。
    ///
    /// <b>加新节点的流程</b>：节点类实现本接口 → 在 Export 里 <c>ctx.Emit(...)</c>。
    /// 遍历器会自动带上它，不需要改导出器里的任何 switch。
    /// 没实现本接口的节点在导出时会记一条警告，不会中断。
    /// </summary>
    internal interface IDramaExportNode
    {
        /// <summary>
        /// 把本节点翻译成一条或多条运行时指令。
        /// 同一次调用里 Emit 的多条会自动按顺序串起来。
        /// </summary>
        void Export(DramaExportContext ctx);
    }
}

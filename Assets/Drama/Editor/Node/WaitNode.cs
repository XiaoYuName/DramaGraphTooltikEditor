using Drama.Editor.Export;
using Drama.Runtime;
using Unity.GraphToolkit.Editor;
using UnityEngine;

namespace Drama.Editor
{
    [System.Serializable]
    [Node("命令/流程","Assets/Drama/Assets/Wait.png","等待")]
    public class WaitNode : DramaNode, IDramaExportNode
    {
        public const string WaitNodeName = "Wait";

        protected override void OnDefinePorts(IPortDefinitionContext context)
        {
            base.OnDefinePorts(context);

            context.AddInputPort<float>(WaitNodeName)
                .WithDefaultValue(1)
                .WithDisplayName("等待")
                .Build();
        }

        // ==================== 导出 ====================

        void IDramaExportNode.Export(DramaExportContext ctx)
        {
            var seconds = ctx.Eval<float>(GetInputPortByName(WaitNodeName), 0f);

            if (seconds <= 0f)
                ctx.Warn("等待时长为 0，这个节点没有实际效果", this);

            ctx.Emit(new WaitAction { Seconds = seconds });
        }
    }
}

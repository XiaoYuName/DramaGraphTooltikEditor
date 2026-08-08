using Drama.Editor.Export;
using Drama.Runtime;
using Unity.GraphToolkit.Editor;
using UnityEngine;

namespace Drama.Editor
{
    [Node("剧情/对话块/台词", "Assets/Drama/Assets/Talk.png", "台词")]
    [UseWithContext(typeof(TalkNode))]
    [System.Serializable]
    public class TalkTextBlock : BlockNode, IDramaExportNode
    {

        public const string portText = "Text";
        public const string portVoice = "Voice";

        protected override void OnDefinePorts(IPortDefinitionContext context)
        {
            // Block 也可以有端口 —— 这样你就能把 LocalizationNode 的输出直接连进 Block 里
            context.AddInputPort<DramaLocalizationProt>(portText)
                .WithDisplayName("文本")
                .Build();

            context.AddInputPort<DramaLocalizationProt>(portVoice)
                .WithDisplayName("语音")
                .Build();
        }

        public override void OnEnable()
        {
            base.OnEnable();
            DefaultColor = new Color(0.95f, 0.78f, 0.35f);
        }

        // ==================== 导出 ====================

        /// <summary>
        /// 一个台词块 = 一条 TalkAction。
        /// 台词/语音是本块自己的；说话人、动效、等待、名字颜色是整段共用的，
        /// 从所属的 <see cref="TalkNode"/> 上取。
        /// </summary>
        void IDramaExportNode.Export(DramaExportContext ctx)
        {
            var action = new TalkAction
            {
                Text  = ctx.EvalLocalized(GetInputPortByName(portText)),
                Voice = ctx.EvalLocalized(GetInputPortByName(portVoice)),
            };

            if (ContextNode is TalkNode talk)
                talk.ApplySharedTo(action, ctx);
            else
                ctx.Warn("台词块不在对话节点里，共用参数取不到", this);

            if (action.Text.IsEmpty)
                ctx.Warn($"第 {Index + 1} 句台词没有绑定文本", ContextNode);

            ctx.Emit(action);
        }
    }
}


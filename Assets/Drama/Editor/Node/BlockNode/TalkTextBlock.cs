using Unity.GraphToolkit.Editor;
using UnityEngine;

namespace Drama.Editor
{
    [Node("剧情/对话块/台词", "Assets/Drama/Assets/Talk.png", "台词")]
    [UseWithContext(typeof(TalkNode))]
    [System.Serializable]
    public class TalkTextBlock : BlockNode
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

    }
}


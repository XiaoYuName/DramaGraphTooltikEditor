using Unity.GraphToolkit.Editor;
using UnityEngine;

namespace Drama.Editor
{
    [System.Serializable]
    [Node("命令/对话","Assets/Drama/Assets/Skin.png","对话框")]
    public class SetTalkFrameNode : DramaNode
    {
        public const string TalkFarme = "TalkFarme";
        
        protected override void OnDefinePorts(IPortDefinitionContext context)
        {
            base.OnDefinePorts(context);
            

            context.AddInputPort<TalkFrame>(TalkFarme)
                .WithDisplayName("对话框")
                .WithDefaultValue(TalkFrame.Normal)
                .Build();
        }
    }
}


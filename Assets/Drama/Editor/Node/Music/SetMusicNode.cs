using Unity.GraphToolkit.Editor;
using UnityEngine;

namespace Drama.Editor
{
    [System.Serializable]
    [Node("命令/音乐","Assets/Drama/Assets/Music.png","播放音乐")]
    public class SetMusicNode : DramaNode
    {
        public const string MusicID = "musicID";
        
        protected override void OnDefinePorts(IPortDefinitionContext context)
        {
            context.AddInputPort(NodeProtName)
                .WithDisplayName("输入")
                .WithCapacity(capacity: PortCapacity.Single)
                .Build();
            
            context.AddInputPort<string>(MusicID)
                .WithDisplayName("音频ID")
                .WithCapacity(capacity: PortCapacity.Single)
                .Build();
        }
    }
}


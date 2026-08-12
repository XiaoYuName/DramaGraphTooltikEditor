using Unity.GraphToolkit.Editor;
using UnityEngine;

namespace Drama.Editor
{
    [System.Serializable]
    [Node("命令/对话","Assets/Drama/Assets/Show.png","显隐")]
    public class TalkShowNode : DramaNode
    {
        public const string ShowNodeName = "isShow";
        
        protected override void OnDefinePorts(IPortDefinitionContext context)
        {
            base.OnDefinePorts(context);

            context.AddInputPort<bool>(ShowNodeName)
                .WithDefaultValue(true)
                .WithDisplayName("显隐")
                .Build();

        }
    }
}


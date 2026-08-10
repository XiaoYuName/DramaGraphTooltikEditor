using Unity.GraphToolkit.Editor;
using UnityEngine;

namespace Drama.Editor
{
    [System.Serializable]
    [Node("命令/背景","Assets/Drama/Assets/Rect.png","背景变化")]
    public class ScreenTransformNode : DramaContextNode
    {
        public const string ScreenID = "screenID";
        
        protected override void OnDefinePorts(IPortDefinitionContext context)
        {
            base.OnDefinePorts(context);
            context.AddInputPort<long>(ScreenID)
                .WithDefaultValue(-1)
                .Delayed()
                .WithDisplayName("场景ID")
                .Build();
        }
    }
}

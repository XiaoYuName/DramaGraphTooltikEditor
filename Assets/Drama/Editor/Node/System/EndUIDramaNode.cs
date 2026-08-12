using Unity.GraphToolkit.Editor;
using UnityEngine;

namespace Drama.Editor
{
    [System.Serializable]
    [Node("终端","Assets/Drama/Assets/End.png","UI结束")]
    public class EndUIDramaNode : Node
    {
        public const string uiPageName = "uiPage";
        
        protected override void OnDefinePorts(IPortDefinitionContext context)
        {
            base.OnDefinePorts(context);
            context.AddInputPort(DramaNode.NodeProtName)
                .WithDisplayName("输入")
                .Build();

            context.AddInputPort<string>(uiPageName)
                .WithDefaultValue("")
                .WithDisplayName("UI")
                .Build();

        }
    
    }
}

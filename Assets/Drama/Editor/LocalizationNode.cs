using Unity.GraphToolkit.Editor;
using UnityEngine;

namespace Drama.Editor
{
    [System.Serializable]
    public class LocalizationNode : Node
    {
        protected override void OnDefinePorts(IPortDefinitionContext context)
        {
            base.OnDefinePorts(context);
            context.AddInputPort<string>("Table")
                .WithDisplayName("TableKey")
                .Build();
            
            context.AddInputPort<string>("Key")
                .WithDisplayName("Key")
                .Build();
            
            context.AddOutputPort<DramaLocalizationProt>("LocalizationProt")
                .WithDisplayName("LocalizationProt")
                .Build();
        }
    }
}


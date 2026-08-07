using Unity.GraphToolkit.Editor;
using UnityEngine;

namespace Drama.Editor
{
    [Node("命令/设置皮肤","Assets/Drama/Assets/Skin.png","设置皮肤")]
    public class ActorSetSkin : DramaNode
    {
        public const string skinName = "deftual";
        
        protected override void OnDefineOptions(IOptionDefinitionContext context)
        {
            base.OnDefineOptions(context);
            context.AddOption<string>(skinName)
                .WithDisplayName("皮肤ID")
                .WithTooltip("Spine的皮肤ID")
                .Build();
        }
    } 
}


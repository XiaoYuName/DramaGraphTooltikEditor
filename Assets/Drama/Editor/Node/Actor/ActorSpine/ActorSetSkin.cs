using Unity.GraphToolkit.Editor;
using UnityEngine;


namespace Drama.Editor
{
    [System.Serializable]
    [Node("命令/立绘/Spine","Assets/Drama/Assets/Skin.png","皮肤")]
    public class ActorSetSkin : ActorDramaNode
    {
        public string SkinName = "skinName";

        protected override void OnDefinePorts(IPortDefinitionContext context)
        {
            base.OnDefinePorts(context);
            context.AddInputPort<string>(SkinName).WithDefaultValue("default")
                .WithDisplayName("皮肤")
                .Build();
        }
    }
}


using Unity.GraphToolkit.Editor;
using UnityEngine;

namespace Drama.Editor
{
    [System.Serializable]
    [Node("命令/场景","Assets/Drama/Assets/ChangeBG.png","游戏场景")]
    public class ChangeGameScreenNode : DramaNode
    {
        public const string MapSceneID = "MapSceneID";
        public const string MinSceneID = "MinSceneID";
        
        protected override void OnDefinePorts(IPortDefinitionContext context)
        {
            base.OnDefinePorts(context);

            context.AddInputPort<long>(MapSceneID)
                .WithDefaultValue(-1)
                .WithDisplayName("大场景")
                .Build();

            context.AddInputPort<long>(MinSceneID)
                .WithDefaultValue(-1)
                .WithDisplayName("小场景")
                .Build();
        }

        protected override void OnDefineOptions(IOptionDefinitionContext context)
        {
            base.OnDefineOptions(context);
        }
    }
}

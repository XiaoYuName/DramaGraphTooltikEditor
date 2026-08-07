using Unity.GraphToolkit.Editor;
using UnityEngine;

namespace Drama.Editor
{
    [System.Serializable]
    [Node("命令/场景","Assets/Drama/Assets/Screen.png","场景")]
    public class ScreenEffNode : DramaNode
    {
        protected override void OnDefineOptions(IOptionDefinitionContext context)
        {
            base.OnDefineOptions(context);
            

        }
    }
}


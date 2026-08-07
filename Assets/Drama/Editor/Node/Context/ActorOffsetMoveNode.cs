using DG.Tweening;
using Drama.Editor;
using Unity.GraphToolkit.Editor;
using UnityEngine;

namespace Drama.Editor
{
    [System.Serializable]
    [Node("命令/变换","Assets/Drama/Assets/ActionMovement.png","角色动作")]
    public class ActorOffsetMoveNode : ActorContextNode
    {
        protected override void OnDefinePorts(IPortDefinitionContext context)
        {
            base.OnDefinePorts(context);
        }

        /// <summary>
        ///        <para>
        /// Called when the node is created or when the graph is enabled.
        /// </para>
        ///      </summary>
        public override void OnEnable()
        {
            base.OnEnable();
            if (ColorUtility.TryParseHtmlString("#002FA759", out var color))
            {
                DefaultColor = color;
            }
        }
    }
}


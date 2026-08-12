using UnityEngine;

namespace Drama.Editor
{
    [System.Serializable]
    public abstract class ActorDramaNode : DramaNode
    {
        public const string ActorIDName = "ActorID";

        protected override void OnDefinePorts(IPortDefinitionContext context)
        {
            base.OnDefinePorts(context);
            context.AddInputPort<long>(ActorIDName)
                .WithDefaultValue(-1)
                .WithDisplayName("角色ID")
                .Build();

            context.AddOutputPort<long>(ActorIDName)
                .WithDisplayName("角色ID")
                .Build();
        }
    }

}

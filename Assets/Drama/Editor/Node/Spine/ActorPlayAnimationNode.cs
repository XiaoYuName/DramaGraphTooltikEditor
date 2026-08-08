using Unity.GraphToolkit.Editor;
using UnityEngine;

namespace Drama.Editor
{
    [System.Serializable]
    [Node("命令/骨骼","Assets/Drama/Assets/PlayAnimation.png","播放动画")]
    public class ActorPlayAnimationNode : ActorDramaNode
    {
        public const string AnimationName = "AnimationName";
        public const string TrackIndex = "TrackIndex";
        public const string isLooping = "isLooping";
        public const string TimeScale  = "TimeScale";

        protected override void OnDefinePorts(IPortDefinitionContext context)
        {
            base.OnDefinePorts(context);

            context.AddInputPort<string>(AnimationName)
                .WithDefaultValue("")
                .WithDisplayName("动画名")
                .Build();

            context.AddInputPort<int>(TrackIndex)
                .WithDefaultValue(1)
                .WithDisplayName("轨道")
                .Build();

            context.AddInputPort<bool>(isLooping)
                .WithDefaultValue(false)
                .WithDisplayName("循环")
                .Build();

            context.AddInputPort<float>(TimeScale)
                .WithDefaultValue(1.0f)
                .WithDisplayName("倍速")
                .Build();
        }
    }
}


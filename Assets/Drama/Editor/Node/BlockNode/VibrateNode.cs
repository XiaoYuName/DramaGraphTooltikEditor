using Unity.GraphToolkit.Editor;
using UnityEngine;


namespace Drama.Editor
{
    [System.Serializable]
    [UseWithContext(typeof(ActorOffsetShakeNode))]
    [Node("命令/变换","Assets/Drama/Assets/VibrateIcon.png","震动")]
    public class VibrateNode : BlockNode
    {
        /// <summary>
        /// 振幅
        /// </summary>
        public const string Amplitude = "amplitude";
        
        /// <summary>
        /// 间隔
        /// </summary>
        public const string Interval = "interval";
        
        /// <summary>
        /// 时间
        /// </summary>
        public const string Duration = "duration";
        
        public const string SmoothSpeed = "smoothSpeed";
        
        public const string ShakeAxis = "shake_axis";
        
        public const string RestoreOnEnd  = "restore_on_end";
        
        protected override void OnDefinePorts(IPortDefinitionContext context)
        {
            base.OnDefinePorts(context);
            context.AddInputPort<float>(Amplitude)
                .WithDefaultValue(0.5f)
                .WithDisplayName("振幅")
                .WithTooltip("振幅。正交方向 ±2×，对角 ±1×。原实现 ≤0 兜底 0.5")
                .Build();

            context.AddOutputPort<float>(Amplitude)
                .WithDisplayName("振幅")
                .Build();

            context.AddInputPort<ShakeAxis>(ShakeAxis)
                .WithDefaultValue(Editor.ShakeAxis.PositionXY)
                .WithDisplayName("轴")
                .Build();

            context.AddOutputPort<ShakeAxis>(ShakeAxis)
                .WithDisplayName("轴")
                .Build();

            context.AddInputPort<float>(Interval)
                .WithDefaultValue(0.3f)
                .WithDisplayName("间隔(秒)")
                .Build();

            context.AddOutputPort<float>(Interval)
                .WithDisplayName("间隔")
                .Build();
            
            context.AddInputPort<float>(Duration)
                .WithDefaultValue(0.3f)
                .WithDisplayName("时间(秒)")
                .Build();

            context.AddOutputPort<float>(Duration)
                .WithDisplayName("时间")
                .Build();

            context.AddInputPort<float>(SmoothSpeed)
                .WithDefaultValue(5f)
                .WithDisplayName("平滑速度")
                .WithTooltip("趋近目标点的速度，越大越硬。原实现 ≤0 兜底 5")
                .Build();

            context.AddOutputPort<float>(SmoothSpeed)
                .WithDisplayName("平滑速度")
                .Build();

            context.AddInputPort<bool>(RestoreOnEnd)
                .WithDefaultValue(true)
                .WithDisplayName("归位")
                .Build();

            context.AddOutputPort(RestoreOnEnd)
                .WithDisplayName("归位")
                .Build();

        }
    }
}


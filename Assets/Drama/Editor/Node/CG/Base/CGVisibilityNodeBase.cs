using DG.Tweening;
using Unity.GraphToolkit.Editor;
using UnityEngine;

namespace Drama.Editor
{
    /// <summary>
    /// 「CG出现」/「CG关闭」的公共部分：显隐方式 + 缓动，带动画时多一个时长端口。
    ///
    /// 方向不用配 —— 是进还是出由节点类型决定，所以两个节点只有 <c>[Node]</c> 那一行
    /// 和「CG出现」多出来的 CG ID 不同。
    /// </summary>
    [System.Serializable]
    public abstract class CGVisibilityNodeBase : DramaNode
    {
        // ---- Option 名 ----
        internal const string k_ShowKind = "ShowKind";
        internal const string k_Ease = "Ease";

        // ---- Port 名 ----
        internal const string k_Duration = "DurationMs";

        protected override void OnDefinePorts(IPortDefinitionContext context)
        {
            // 基类的进/出流程端口
            base.OnDefinePorts(context);

            // 动态端口：瞬时显隐没有时长可言，和立绘出现同一套做法
            if (IsAnimated())
            {
                context.AddInputPort<float>(k_Duration)
                    .WithDisplayName("时长(ms)")
                    .WithDefaultValue(600f)
                    .WithTooltip("淡入 / 淡出的时长；瞬时方式不需要")
                    .Build();

                context.AddOutputPort<float>(k_Duration)
                    .WithDisplayName("时长(ms)")
                    .Build();
            }
        }

        protected override void OnDefineOptions(IOptionDefinitionContext context)
        {
            base.OnDefineOptions(context);

            context.AddOption<ECGShowKind>(k_ShowKind)
                .WithDisplayName("显隐方式")
                .WithDefaultValue(ECGShowKind.Fade)
                .WithTooltip("淡入淡出会多出「时长(ms)」端口")
                .Build();

            context.AddOption<Ease>(k_Ease)
                .WithDisplayName("过度")
                .WithDefaultValue(Ease.Linear)
                .Build();
        }

        internal ECGShowKind GetShowKind()
        {
            var opt = GetNodeOptionByName(k_ShowKind);
            if (opt == null)
                return ECGShowKind.Fade;   // 首次定义时选项可能还不存在

            opt.TryGetValue<ECGShowKind>(out var kind);
            return kind;
        }

        /// <summary>带动画的方式才需要时长参数。</summary>
        internal bool IsAnimated() => GetShowKind() == ECGShowKind.Fade;

        public override void OnEnable()
        {
            base.OnEnable();
            DefaultColor = new Color(0.85f, 0.65f, 0.9f);
        }
    }
}

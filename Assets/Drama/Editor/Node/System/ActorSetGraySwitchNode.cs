using Unity.GraphToolkit.Editor;
using UnityEngine;

namespace Drama.Editor
{
    /// <summary>
    /// 「非说话人压暗 / 微缩」这套效果的总开关。
    ///
    /// <b>是全局设置，不针对具体角色</b>，所以继承 DramaNode 而不是 ActorDramaNode（没有角色ID端口）。
    /// 真正的应用发生在每条台词上：舞台按当前说话人自动把说话人恢复原样、其他人压暗微缩。
    /// 剧本里一般开头设一次就够。
    ///
    /// 两个开关做成 Option（不是端口），因为它们要驱动动态端口 ——
    /// 勾上才冒出对应的强度端口，没开的效果不占地方。
    /// </summary>
    [System.Serializable]
    [Node("命令/系统","Assets/Drama/Assets/Scale.png","讲话人缩放")]
    public class ActorSetGraySwitchNode : DramaNode
    {
        // ---- Option 名 ----
        // 名字是历史遗留（isFade 其实是压暗、isGray 其实是微缩），没改是怕已存的图丢值
        public const string IsFade = "isFade";
        public const string IsGray = "isGray";

        // ---- Port 名（动态，跟着上面两个开关出现）----
        public const string DimBrightness = "dimBrightness";
        public const string ShrinkScale   = "shrinkScale";

        /// <summary>旧工程写死的手感值，这里当默认值用。</summary>
        private const float DefaultDimBrightness = 0.8f;
        private const float DefaultShrinkScale   = 0.95f;

        protected override void OnDefineOptions(IOptionDefinitionContext context)
        {
            base.OnDefineOptions(context);

            context.AddOption<bool>(IsFade)
                .WithDisplayName("压暗非说话人")
                .WithDefaultValue(false)
                .WithTooltip("勾上后会多出「压暗亮度」端口")
                .Build();

            context.AddOption<bool>(IsGray)
                .WithDisplayName("微缩非说话人")
                .WithDefaultValue(true)
                .WithTooltip("勾上后会多出「微缩倍率」端口")
                .Build();
        }

        protected override void OnDefinePorts(IPortDefinitionContext context)
        {
            base.OnDefinePorts(context);

            if (GetIsDim())
            {
                context.AddInputPort<float>(DimBrightness)
                    .WithDisplayName("压暗亮度")
                    .WithDefaultValue(DefaultDimBrightness)
                    .WithTooltip("1 = 原始亮度，越小越暗。旧工程写死 0.8")
                    .Build();

                context.AddOutputPort<float>(DimBrightness)
                    .WithDisplayName("压暗亮度")
                    .Build();
            }

            if (GetIsShrink())
            {
                context.AddInputPort<float>(ShrinkScale)
                    .WithDisplayName("微缩倍率")
                    .WithDefaultValue(DefaultShrinkScale)
                    .WithTooltip("1 = 原始大小。旧工程写死 0.95")
                    .Build();

                context.AddOutputPort<float>(ShrinkScale)
                    .WithDisplayName("微缩倍率")
                    .Build();
            }
        }

        // ---- 动态端口的条件判断 ----

        public bool GetIsDim() => GetBoolOption(IsFade, false);

        public bool GetIsShrink() => GetBoolOption(IsGray, true);

        private bool GetBoolOption(string optionName, bool fallback)
        {
            var opt = GetNodeOptionByName(optionName);
            if (opt == null)
                return fallback;   // 首次定义时选项可能还不存在

            opt.TryGetValue<bool>(out var value);
            return value;
        }
    }
}

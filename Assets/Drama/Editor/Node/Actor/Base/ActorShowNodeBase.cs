using DG.Tweening;
using Unity.GraphToolkit.Editor;
using UnityEngine;

namespace Drama.Editor
{
    /// <summary>
    /// 「立绘出现」这一族的公共定义：角色ID / 方向 / 显示方式 / 过度，
    /// 加上位置、缩放，以及带动画时才出现的时长端口。
    ///
    /// <b>三种立绘（Spine / Texture / Live2D）除了资源类型完全一样</b>：
    /// 填一个角色ID，剩下的摆位和显隐参数一模一样。所以只有 <c>[Node]</c> 那一行不同，
    /// 定义全在这儿。分成三个节点而不是加一个"类型"下拉，是因为类型决定了运行时
    /// 该实例化哪种立绘，做成下拉的话策划改错一次要到播放时才发现。
    ///
    /// ⚠️ 端口名和选项名是<b>存档格式的一部分</b>，改名会让已有图上的连线和填值丢失。
    /// </summary>
    [System.Serializable]
    public abstract class ActorShowNodeBase : DramaNode
    {
        // ---- Option 名 ----
        internal const string k_CharId   = "CharId";
        internal const string k_ShowKind = "ShowKind";
        internal const string k_ShowDirection = "ShowDirection";
        internal const string k_Ease = "Ease";

        // ---- Port 名 ----
        internal const string k_Pos      = "Pos";
        internal const string k_Scale    = "Scale";
        internal const string k_Duration = "DurationMs";

        protected override void OnDefinePorts(IPortDefinitionContext context)
        {
            // 基类的进/出流程端口
            base.OnDefinePorts(context);

            // 位置和缩放做成端口，方便多个节点共用同一套位姿（接同一个变量节点）
            context.AddInputPort<Vector2>(k_Pos)
                .WithDisplayName("位置")
                .WithDefaultValue(Vector2.zero)
                .WithTooltip("立绘位置")
                .Build();

            context.AddOutputPort<Vector2>(k_Pos)
                .WithDisplayName("位置")
                .Build();

            context.AddInputPort<Vector2>(k_Scale)
                .WithDisplayName("缩放")
                .WithDefaultValue(Vector2.one)
                .WithTooltip("倍率，1 = 原始大小。和「立绘缩放」节点同一个口径")
                .Build();

            context.AddOutputPort<Vector2>(k_Scale)
                .WithDisplayName("缩放")
                .Build();

            // ---- 动态端口：只有带动画的显示方式才需要时长 ----
            // Show / Hide 是瞬时的，对应原始数据里 argc=3 的短形式。
            if (IsAnimated())
            {
                context.AddInputPort<float>(k_Duration)
                    .WithDisplayName("时长(ms)")
                    .WithDefaultValue(600f)
                    .WithTooltip("动画时长；Show/Hide 这类瞬时方式不需要")
                    .Build();

                context.AddOutputPort<float>(k_Duration)
                    .WithDisplayName("时长(ms)")
                    .Build();
            }
        }

        protected override void OnDefineOptions(IOptionDefinitionContext context)
        {
            base.OnDefineOptions(context);

            context.AddOption<int>(k_CharId)
                .WithDisplayName("角色ID")
                .WithDefaultValue(1)
                .WithTooltip("角色表 id，直接导出，不做槽位换算")
                .Delayed()
                .Build();

            context.AddOption<EActorShowDirection>(k_ShowDirection)
                .WithDisplayName("方向")
                .WithDefaultValue(EActorShowDirection.Left)
                .WithTooltip("展示出现的方向")
                .Delayed()
                .Build();

            context.AddOption<EActorShowKind>(k_ShowKind)
                .WithDisplayName("显示方式")
                .WithDefaultValue(EActorShowKind.FadeIn)
                .WithTooltip("FadeIn / FadeOut 带动画，会多出「时长(ms)」端口")
                .Build();

            context.AddOption<Ease>(k_Ease)
                .WithDisplayName("过度")
                .WithDefaultValue(Ease.Linear)
                .Build();
        }

        // ---- 动态端口的条件判断 ----

        internal EActorShowKind GetShowKind()
        {
            var opt = GetNodeOptionByName(k_ShowKind);
            if (opt == null)
                return EActorShowKind.FadeIn;   // 首次定义时选项可能还不存在

            opt.TryGetValue<EActorShowKind>(out var kind);
            return kind;
        }

        /// <summary>是否为带动画的显示方式（需要时长参数）。</summary>
        internal bool IsAnimated()
        {
            var kind = GetShowKind();
            return kind == EActorShowKind.FadeIn || kind == EActorShowKind.FadeOut;
        }

        public override void OnEnable()
        {
            base.OnEnable();
            DefaultColor = new Color(0.55f, 0.85f, 0.55f);
        }
    }
}

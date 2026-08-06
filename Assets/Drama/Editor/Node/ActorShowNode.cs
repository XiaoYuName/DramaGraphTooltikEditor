using System;
using Unity.GraphToolkit.Editor;
using UnityEngine;

namespace Drama.Editor
{
    /// <summary>
    /// 立绘显示 / 隐藏。对应原系统 opcode 2 = ActorShow（实测用量 1032 次，第三高频）。
    ///
    /// 字段映射（参数编号沿用原系统，便于对照文档）：
    ///   p0 charId     = 角色ID，直接存直接导出
    ///   p1 showKind   = 显示方式
    ///   p2 x   p3 y   = 位置
    ///   p4 sx  p5 sy  = 缩放百分比（100 = 原始大小）
    ///   p6 reserved   = 语义未确认，原始数据里出现过 1 和 6
    ///   p7 durationMs = 动画时长
    ///   p8 wait       = 是否阻塞（等动画播完才推进）
    ///
    /// 注意：本工程【不采用】原系统的「立绘槽位 + 10」编址。
    /// 原作里 actorIdx = 10 + 剧本头 actors 列表下标，需要维护一张槽位表；
    /// 这里直接用角色ID，没有槽位这层间接，也不需要在导出时做换算。
    /// </summary>
    [Node("命令/立绘/显示隐藏", "Assets/Drama/Assets/Start.png", "立绘显示")]
    [Serializable]
    public class ActorShowNode : DramaNode
    {
        // ---- Option 名 ----
        internal const string k_CharId   = "CharId";
        internal const string k_ShowKind = "ShowKind";
        internal const string k_ShowDirection = "ShowDirection";
        internal const string k_Reserved = "Reserved";
        internal const string k_Wait     = "Wait";

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
                .WithDefaultValue(new Vector2(100, 100))
                .WithTooltip("大小比例")
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

            context.AddOption<bool>(k_Wait)
                .WithDisplayName("等待动画结束")
                .WithDefaultValue(true)
                .WithTooltip("勾选则阻塞剧情推进，等动画播完再继续（p8）")
                .Build();

            context.AddOption<int>(k_Reserved)
                .WithDisplayName("保留参数")
                .WithDefaultValue(1)
                .WithTooltip("原系统 p6，语义未确认（实测出现过 1 和 6）。不确定就留 1")
                .ShowInInspectorOnly()
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

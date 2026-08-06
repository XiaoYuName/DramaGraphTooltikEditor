using System;
using Unity.GraphToolkit.Editor;
using UnityEngine;

namespace Drama.Editor
{
    /// <summary>
    /// 对话节点（容器）。内部可以放 <see cref="TalkTextBlock"/> 等 Block。
    /// 继承 DramaContextNode（而不是 DramaNode）才能装 Block。
    /// </summary>
    [Node("命令/对话","Assets/Drama/Assets/Talk.png","对话")]
    [Serializable]
    public class TalkNode : DramaContextNode
    {
        // 选项名
        const string k_Speaker    = "Speaker";
        const string k_ActorSlot  = "ActorSlot";
        const string k_Ballon     = "Ballon";
        const string k_SoundID    = "SoundID";
        const string k_SpCharID   = "SpCharID";
        const string k_WaitMs     = "WaitMs";
        const string k_NameColor  = "NameColor";

        /// <summary>自定义说话人名的多语言端口。只在 说话人 == Unknown 时存在。</summary>
        internal const string k_SpeakerName = "SpeakerName";



        protected override void OnDefinePorts(IPortDefinitionContext context)
        {


            base.OnDefinePorts(context);
            context.AddInputPort<EBallonKind>(k_Ballon)
                .WithDisplayName("对话框动效")
                .WithDefaultValue(EBallonKind.Normal)
                .Build();

            context.AddOutputPort<EBallonKind>(k_Ballon)
                .WithDisplayName("对话框动效")
                .Build();


            context.AddInputPort<float>(k_WaitMs)
                .WithDisplayName("自动等待(ms)")
                .WithDefaultValue(0)
                .Build();

            context.AddOutputPort<float>(k_WaitMs)
                .WithDisplayName("自动等待(ms)")
                .Build();

            context.AddInputPort<Color>(k_NameColor)
                .WithDefaultValue(Color.white)
                .WithDisplayName("名字颜色")
                .Build();

            context.AddOutputPort<Color>(k_NameColor)
                .WithDisplayName("名字颜色")
                .Build();

            // 说话人是 Option 不是端口 —— 见 OnDefineOptions 的注释。

            // ---- 动态端口：说话人 == Unknown（自定义）时才有「说话人名」----
            if (IsCustomSpeaker())
            {
                context.AddInputPort<DramaLocalizationProt>(k_SpeakerName)
                    .WithDisplayName("说话人名")
                    .WithTooltip("自定义说话人显示的名字，走多语言")
                    .Build();

                context.AddOutputPort<DramaLocalizationProt>(k_SpeakerName)
                    .WithDisplayName("说话人名")
                    .Build();
            }

            // ---- 动态端口：说话人 == ActorSlot 时才有「立绘槽位」----
            if (IsActorSlotSpeaker())
            {
                context.AddInputPort<int>(k_ActorSlot)
                    .WithDisplayName("立绘槽位")
                    .WithDefaultValue(0)
                    .Build();

                context.AddOutputPort<int>(k_ActorSlot)
                    .WithDisplayName("立绘槽位")
                    .Build();
            }
        }

        /// <summary>
        /// 「说话人」必须定义成 Option 而不是端口，原因有两条（都是实测结论）：
        ///
        /// 1. 改【Option 值】会自动重跑 DefineNode，改【端口内嵌值】不会 ——
        ///    用端口的话，选了 Unknown 之后动态端口不会出现，除非手动调 DefineNode()。
        /// 2. IPort.TryGetValue 在端口【已连线】时返回 false（值来自上游），
        ///    所以一旦有人往「说话人」上接线，条件判断就永远失效。
        ///
        /// Option 没有这两个问题：值总是可读，改了就自动重建。
        /// </summary>
        protected override void OnDefineOptions(IOptionDefinitionContext context)
        {
            base.OnDefineOptions(context);

            context.AddOption<ETalkSpeaker>(k_Speaker)
                .WithDisplayName("说话人")
                .WithDefaultValue(ETalkSpeaker.Aside)
                .WithTooltip("Unknown → 多出「说话人名」端口；Actor Slot → 多出「立绘槽位」端口")
                .Build();
        }

        ETalkSpeaker GetSpeaker()
        {
            var opt = GetNodeOptionByName(k_Speaker);
            if (opt == null)
                return ETalkSpeaker.Aside;   // 首次定义时选项可能还不存在

            opt.TryGetValue<ETalkSpeaker>(out var speaker);
            return speaker;
        }

        /// <summary>说话人是否为「自定义」（Unknown）。</summary>
        internal bool IsCustomSpeaker() => GetSpeaker() == ETalkSpeaker.Unknown;

        /// <summary>说话人是否取自立绘槽位。</summary>
        internal bool IsActorSlotSpeaker() => GetSpeaker() == ETalkSpeaker.ActorSlot;


        /// <summary>
        ///        <para>
        /// Called when the node is created or when the graph is enabled.
        /// </para>
        ///      </summary>
        public override void OnEnable()
        {
            base.OnEnable();
            DefaultColor = new Color(0.95f, 0.78f, 0.35f);
        }
    }
}

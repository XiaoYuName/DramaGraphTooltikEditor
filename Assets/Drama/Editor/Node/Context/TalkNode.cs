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

            context.AddInputPort<ETalkSpeaker>(k_Speaker)
                .WithDefaultValue(ETalkSpeaker.Aside)
                .WithDisplayName("说话人")
                .Build();

            
            context.AddOutputPort<ETalkSpeaker>(k_Speaker)
                .WithDisplayName("说话人")
                .Build();
            
            
            context.AddInputPort<int>(k_ActorSlot)
                .WithDisplayName("立绘槽位")
                .WithDefaultValue(0)
                .Build();

            context.AddOutputPort<int>(k_ActorSlot)
                .WithDisplayName("立绘槽位")
                .Build();
            
        }


        protected override void OnDefineOptions(IOptionDefinitionContext context)
        {
            base.OnDefineOptions(context);
            context.AddOption<bool>("isSpeaker")
                .WithDisplayName("自定义说话人")
                .WithTooltip("勾选后将使用该说话人输出")
                .Build();
        }

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


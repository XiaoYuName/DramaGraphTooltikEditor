using System;
using UnityEngine;

namespace Drama.Editor
{
    /// <summary>
    /// 台词节点
    /// </summary>
    [Serializable]
    public class TalkNode : DramaNode
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
            context.AddInputPort<DramaLocalizationProt>("LocalizationProt")
                .WithDisplayName("多语言")
                .Build();
            
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

            context.AddInputPort<DramaLocalizationProt>(k_SoundID)
                .WithDisplayName("语音ID")
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
        
    }
}


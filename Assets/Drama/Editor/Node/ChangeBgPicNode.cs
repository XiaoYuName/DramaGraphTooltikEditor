using System;
using Unity.GraphToolkit.Editor;
using UnityEngine;

namespace Drama.Editor
{
    [System.Serializable]
    [Node("命令","Assets/Drama/Assets/ChangeBG.png","切换背景")]
    public class ChangeBgPicNode : DramaNode
    {
        public const  string BackgroundID = "backgroundID";
        public const string TransitionKind = "transitionKind";
        
        
        public const string InDuration  = "InDuration";  
        public const string OutDuration = "OutDuration";
               

        protected override void OnDefinePorts(IPortDefinitionContext context)
        {
            base.OnDefinePorts(context);

            context.AddInputPort<long>(BackgroundID)
                .WithDefaultValue(-1)
                .WithDisplayName("背景ID")
                .Build();
            
            if (GetTransitionKind() != Editor.TransitionKind.None)
            {
                context.AddInputPort<float>(InDuration)
                    .WithDefaultValue(1f)
                    .WithDisplayName("淡入")
                    .Build();
                context.AddInputPort<float>(OutDuration)
                    .WithDefaultValue(1f)
                    .WithDisplayName("淡出")
                    .Build();
            }
        }

        protected override void OnDefineOptions(IOptionDefinitionContext context)
        {
            base.OnDefineOptions(context);

            context.AddOption<TransitionKind>(TransitionKind)
                .WithDefaultValue(Editor.TransitionKind.None)
                .WithDisplayName("过度")
                .Build();
        }


        public TransitionKind GetTransitionKind()
        {
            var opt = GetNodeOptionByName(TransitionKind);
            if (opt == null)
                return Editor.TransitionKind.None;   // 首次定义时选项可能还不存在

            opt.TryGetValue<TransitionKind>(out var transitionKind);
            return transitionKind;
        }
    }
}


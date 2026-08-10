using DG.Tweening;
using Unity.GraphToolkit.Editor;
using UnityEngine;

namespace Drama.Editor
{
    [System.Serializable]
    [Node("命令/过度","Assets/Drama/Assets/Comb.png","百叶窗")]
    [UseWithContext(typeof(ScreenEffNode))]
    public class CombBlockNode : BlockNode
    {
        public const string InputKind = "input_kind";
        
        public const string InDuration = "in_duration";
        public const string OutDuration = "out_duration";
        public const string FadeColor = "fadeColor";
        public const string Alpha = "alpha";
        public const string Ease = "ease";

        protected override void OnDefinePorts(IPortDefinitionContext context)
        {
            base.OnDefinePorts(context);
            switch (GetInputKind())
            {
                case Editor.InputKind.In:
                    context.AddInputPort<float>(InDuration)
                        .WithDefaultValue(1f)
                        .WithDisplayName("淡入")
                        .Build();
                    break;
                case Editor.InputKind.Out:
                    context.AddInputPort<float>(OutDuration)
                        .WithDefaultValue(1f)
                        .WithDisplayName("淡出")
                        .Build();
                    break;
                case Editor.InputKind.InOut:
                    context.AddInputPort<float>(InDuration)
                        .WithDefaultValue(1f)
                        .WithDisplayName("淡入")
                        .Build();
                    
                    context.AddInputPort<float>(OutDuration)
                        .WithDefaultValue(1f)
                        .WithDisplayName("淡出")
                        .Build();
                    break;
            }

            context.AddInputPort<Color>(FadeColor)
                .WithDefaultValue(Color.white)
                .WithDisplayName("颜色")
                .Build();

            context.AddInputPort<float>(Alpha)
                .WithDefaultValue(1f)
                .WithDisplayName("透明度")
                .Build();

            context.AddInputPort<Ease>(Ease)
                .WithDefaultValue(DG.Tweening.Ease.Linear)
                .WithDisplayName("曲线")
                .Build();

        }

        protected override void OnDefineOptions(IOptionDefinitionContext context)
        {
            base.OnDefineOptions(context);
            context.AddOption<InputKind>(InputKind)
                .WithDefaultValue(Editor.InputKind.InOut)
                .WithDisplayName("转场")
                .Build();
        }

        protected InputKind GetInputKind()
        {
            var op = GetNodeOptionByName(InputKind);
            if (op == null)
            {
                return Editor.InputKind.InOut;
            }

            op.TryGetValue<InputKind>(out var kind);
            return kind;
        }
    
    }
}


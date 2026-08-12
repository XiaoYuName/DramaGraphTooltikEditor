using Unity.GraphToolkit.Editor;
using UnityEngine;

namespace Drama.Editor
{
    [System.Serializable]
    [Node("命令/流程","Assets/Drama/Assets/Change.png","分支")]
    public class ChangeDramaNode : DramaNode
    {
        public const string OptionNumber = "optionNumber";
        public const string OptionName = "optionName";

        protected override void OnDefinePorts(IPortDefinitionContext context)
        {
            //base.OnDefinePorts(context);
            context.AddInputPort(NodeProtName)
                .WithDisplayName("输入")
                .Build();
            
            for (int i = 0; i < GetOptionNumber(); i++)
            {
                context.AddInputPort<DramaLocalizationProt>(OptionName + i)
                    .WithDisplayName("分支" + i)
                    .Build();

                context.AddOutputPort(NodeProtName + i)
                    .WithDisplayName("输出" + i)
                    .Build();
            }

            
        }

        protected override void OnDefineOptions(IOptionDefinitionContext context)
        {
            base.OnDefineOptions(context);
            context.AddOption<int>(OptionNumber)
                .WithDefaultValue(2)
                .WithDisplayName("选项")
                .Build();
        }

        protected int GetOptionNumber()
        {
            var opt = GetNodeOptionByName(OptionNumber);
            if (opt == null)
                return 0;   // 首次定义时选项可能还不存在

            opt.TryGetValue<int>(out var number);
            return number;
        }
    }
}


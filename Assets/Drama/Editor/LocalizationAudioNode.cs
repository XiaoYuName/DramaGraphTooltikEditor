using Unity.GraphToolkit.Editor;

namespace Drama.Editor
{
    [Node("功能/多语言","Assets/Drama/Assets/Localization.png","语音")]
    [System.Serializable]
    public class LocalizationAudioNode : Node
    {
        public const string localizationTable = "LocalizationTable";
        public const string localizationKey = "LocalizationKey";
        public const string value = "value";
        public const string localizationTableName = "多语言表";
        public const string localizationKeyName = "多语言键";
        public const string localizationValueName = "语音";
        
        protected override void OnDefinePorts(IPortDefinitionContext context)
        {
            base.OnDefinePorts(context);
            context.AddInputPort<string>(localizationTable)
                .WithDisplayName(localizationTableName)
                .Build();
            
            context.AddInputPort<string>(localizationKey)
                .WithDisplayName(localizationKeyName)
                .Build();
            
            context.AddOutputPort<DramaLocalizationProt>(value)
                .WithDisplayName(localizationValueName)
                .Build();
        }
    
    }
}


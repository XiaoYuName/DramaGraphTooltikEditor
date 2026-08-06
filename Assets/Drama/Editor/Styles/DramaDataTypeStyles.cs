using Unity.GraphToolkit.Editor;
using UnityEditor;
using UnityEngine;

namespace Drama.Editor
{
    [DataTypeStyleMapper(typeof(DramaGraph))]
    public class DramaDataTypeStyles : DataTypeStyleMapper
    {
        public DramaDataTypeStyles()
        {
            Register(
                typeof(DramaLocalizationProt),
                AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Drama/Assets/Text.png"),
                new Color(1f, 0.78f, 0.31f));
        }
    }
}
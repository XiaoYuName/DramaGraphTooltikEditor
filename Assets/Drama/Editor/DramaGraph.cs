using System;
using Unity.GraphToolkit.Editor;
using UnityEditor;
using UnityEngine;

namespace Drama.Editor
{
    [Serializable]
    [Graph(AssetExtension)]
    internal class DramaGraph : Graph
    {
        internal const string graphName = "DramaGraph";
        
        internal const string AssetExtension = "agv";
        
        /// <summary>
        /// Creates a new Visual Novel Director graph asset file in the project window.
        /// </summary>
        /// <remarks>This is also where we add the shortcut to create a new graph from the editor Asset menu.</remarks>
        [MenuItem("Assets/Create/Drama/剧情编辑器")]
        static void CreateAssetFile()
        {
            GraphDatabase.PromptInProjectBrowserToCreateNewAsset<DramaGraph>(graphName);
        }
        
    }
    
    [Serializable]
    public class DramaProt
    {
        /// <summary>
        /// 剧情ID
        /// </summary>
        public long DramaId;
        /// <summary>
        /// 事件ID
        /// </summary>
        public long DramaEventID;
    }

    [System.Serializable]
    public class DramaLocalizationProt
    {
        public string Table;
        public string Value;
    }

    /// <summary>对话框动效。Talk 的 p1。</summary>
    public enum EBallonKind
    {
        /// <summary>
        /// 无动效
        /// </summary>
        Normal = 0,
        
        Shake = 1,
        
        Shock = 2,
    }


    /// <summary>Talk 的说话人寻址方式。运行时 actorIdx 的语义分段。</summary>
    public enum ETalkSpeaker
    {
        Aside = 0,      // 旁白，不显示名字条
        Hero = 1,       // 主角
        Unknown = 3,    // "???"
        ActorSlot = 10, // 立绘槽位：actorIdx = IDX_ACTOR_SHIFT + slot
    }
}


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
        Unknown = 2,    // 自定义
        ActorSlot = 3, // 立绘槽位：actorIdx
    }

    /// <summary>
    /// 立绘显示方式
    /// </summary>
    public enum EActorShowKind
    {
        /// <summary>瞬时显示，无动画。值 0 未验证。</summary>
        Show = 0,

        /// <summary>瞬时隐藏，无动画。值 1 未验证。</summary>
        Hide = 1,

        /// <summary>淡入。实测确认。</summary>
        FadeIn = 7,

        /// <summary>淡出。实测确认。</summary>
        FadeOut = 8,
    }

    /// <summary>
    /// 立绘显示方向
    /// </summary>
    public enum EActorShowDirection
    {
        Left = 0,
        Right = 1,
        Center = 2,
    }
}


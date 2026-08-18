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

    /// <summary>
    /// CG 的显隐方式。
    ///
    /// 只有"瞬时 / 带动画"两个值，方向由节点自己决定（CG出现就是进、CG关闭就是出）。
    /// 刻意不复用 <see cref="EActorShowKind"/>：那个有 Show/Hide/FadeIn/FadeOut 四个值，
    /// 挂在「CG出现」上就能选出"淡出"这种没有意义的组合。
    /// </summary>
    public enum ECGShowKind
    {
        /// <summary>瞬时，无动画。</summary>
        Instant = 0,

        /// <summary>淡入 / 淡出。</summary>
        Fade = 1,
    }

    /// <summary>
    /// Animator 的 Trigger 怎么用。
    ///
    /// 之所以要有「重置」：Trigger 被 SetTrigger 之后如果没有任何转换条件消费掉它，
    /// 会一直挂在那儿，等状态机走到某个能用它的状态时突然触发。
    /// 剧本里"这次不走那条分支了"就得显式 ResetTrigger 撤掉。
    /// </summary>
    public enum EAnimTriggerMode
    {
        /// <summary>SetTrigger</summary>
        Set = 0,

        /// <summary>ResetTrigger</summary>
        Reset = 1,
    }
    
    public enum ShakeAxis
    {
        PositionXY,   // Position   —— 原实现唯一被指令用到的
        PositionZ,    // PositionZ  —— 代码里有，指令没开放
        Rotation,     // Angles     —— 代码里有，指令没开放
    }

    /// <summary>
    /// 「解锁」节点解锁的是什么。选哪个决定节点上出现哪些参数。
    ///
    /// 这是编辑器自己的概念，不是宿主枚举的镜像 —— 具体解锁哪个功能一律填
    /// <b>宿主枚举的整数值</b>（int 端口），这套编辑器才能跨工程用。
    /// </summary>
    public enum UnlockTargetKind
    {
        /// <summary>系统功能：主界面那排按钮。</summary>
        SystemFunction = 0,

        /// <summary>角色功能：某个角色的功能面板按钮。</summary>
        CharacterFunction = 1,

        /// <summary>地图入口：大地图上的点，或点开之后的小场景。</summary>
        Map = 2,
    }

    public enum TransitionKind
    {
        None = 0,
        Fade = 1,
        VenetianBlind = 2,
        Comb = 3,
    }

    public enum InputKind
    {
        In = 1,
        Out = 2,
        InOut = 3,
    }

    public enum EDramaScreenEff
    {
        None = 0,
        Fade = 1,
        FadeIn = 2,
        FadeOut = 3,
    }

    
    public enum TalkFrame
    {
        Normal = 0,
        HCG = 1
    }
}


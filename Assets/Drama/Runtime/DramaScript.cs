using System;
using System.Collections.Generic;
using UnityEngine;

namespace Drama.Runtime
{
    // ============================================================================
    //  运行时剧情数据模型
    //
    //  这一层【完全独立】：不引用 UnityEditor、不引用 GraphToolkit、不引用本工程的
    //  任何编辑器类型。整个 Drama.Runtime 程序集是要拷到别的 Unity 工程里跑的。
    //
    //  ⚠️ 跨工程注意事项（改之前务必看）：
    //   1. [SerializeReference] 按 { 类名, 命名空间, 程序集名 } 三个字符串反序列化。
    //      改类名 / 改命名空间 / 改 asmdef 名字 → 已导出的资产里对应节点全部变 null。
    //   2. DramaScript 资产里的 m_Script 是【.cs 文件的 GUID】。两个工程里
    //      DramaScript.cs 的 GUID 必须一致，否则对方工程看到的是 Missing Script。
    //      → 最稳的做法是把本目录抽成 UPM 包，两个工程装同一个包。
    //      → 现在还没导出过真实数据，是抽包的最佳时机；等有数据了再抽会全部失效。
    // ============================================================================

    /// <summary>多语言引用。指向本地化表里的一条。</summary>
    [Serializable]
    public struct LocalizedRef
    {
        public string Table;
        public string Key;

        public bool IsEmpty => string.IsNullOrEmpty(Table) && string.IsNullOrEmpty(Key);

        public override string ToString() => IsEmpty ? "(空)" : $"{Table}/{Key}";
    }

    /// <summary>说话人寻址方式。</summary>
    public enum ESpeakerKind
    {
        /// <summary>旁白，不显示名字条。</summary>
        Aside = 0,
        /// <summary>主角。</summary>
        Hero = 1,
        /// <summary>自定义名字，读 <see cref="TalkAction.SpeakerName"/>。</summary>
        Custom = 2,
        /// <summary>指定角色，读 <see cref="TalkAction.ActorId"/>。</summary>
        Actor = 3,
    }

    /// <summary>对话框动效。</summary>
    public enum EBalloonKind
    {
        Normal = 0,
        Shake = 1,
        Shock = 2,
    }

    // ---------------------------------------------------------------- 指令基类

    /// <summary>
    /// 一条剧情指令。用 <c>[SerializeReference]</c> 多态存进 <see cref="DramaScript.Actions"/>。
    /// </summary>
    [Serializable]
    public abstract class DramaAction
    {
        /// <summary>自己在 <see cref="DramaScript.Actions"/> 里的下标。</summary>
        public int Index = -1;

        /// <summary>下一条的下标；-1 表示到此结束。</summary>
        public int Next = -1;

        /// <summary>指令类型名，仅用于 Inspector 显示和调试。</summary>
        public abstract string Kind { get; }

        /// <summary>一行摘要，Inspector / 导出报告里显示用。</summary>
        public virtual string Summary => Kind;
    }

    // ---------------------------------------------------------------- 具体指令

    /// <summary>说一句台词。</summary>
    [Serializable]
    public sealed class TalkAction : DramaAction
    {
        public override string Kind => "Talk";

        public LocalizedRef  Text;
        public LocalizedRef  Voice;

        public ESpeakerKind  Speaker;
        /// <summary>Speaker == Custom 时使用。</summary>
        public LocalizedRef  SpeakerName;
        /// <summary>Speaker == Actor 时使用。</summary>
        public int           ActorId;

        public EBalloonKind  Balloon;
        /// <summary>打完字后自动等待的秒数；0 = 等玩家点击。</summary>
        public float         AutoWaitSeconds;
        public Color         NameColor = Color.white;

        public override string Summary => $"Talk  {Speaker}  {Text}";
    }

    /// <summary>等待一段时间。</summary>
    [Serializable]
    public sealed class WaitAction : DramaAction
    {
        public override string Kind => "Wait";

        /// <summary>秒。</summary>
        public float Seconds;

        public override string Summary => $"Wait  {Seconds:0.###}s";
    }

    // ---------------------------------------------------------------- 剧本资产

    /// <summary>
    /// 一个剧本 = 一份线性指令表。由编辑器工程的导出器生成，运行时只读。
    /// </summary>
    public class DramaScript : ScriptableObject
    {
        /// <summary>当前导出器写出的格式版本。运行时读到更高的版本应当拒绝加载。</summary>
        public const int CurrentFormatVersion = 1;

        public int    FormatVersion = CurrentFormatVersion;
        public long   DramaId;

        /// <summary>来源 .agv 的工程路径，仅用于排查问题。</summary>
        public string SourceGraph;

        /// <summary>入口指令下标；-1 表示空剧本。</summary>
        public int    EntryIndex = -1;

        [SerializeReference]
        public List<DramaAction> Actions = new List<DramaAction>();

        public int ActionCount => Actions?.Count ?? 0;

        public DramaAction GetAction(int index)
        {
            if (Actions == null || index < 0 || index >= Actions.Count) return null;
            return Actions[index];
        }

        /// <summary>从入口开始顺着 Next 遍历。带环保护。</summary>
        public IEnumerable<DramaAction> Walk()
        {
            var guard = 0;
            var i = EntryIndex;
            while (i >= 0 && i < ActionCount)
            {
                var a = Actions[i];
                if (a == null) yield break;
                yield return a;
                if (++guard > ActionCount) yield break;   // 数据损坏时兜底
                i = a.Next;
            }
        }
    }
}

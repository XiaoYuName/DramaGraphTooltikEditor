using System;
using System.Collections.Generic;

namespace Drama.Runtime.Flow
{
    /// <summary>
    /// 一份要预载的立绘：角色 + 用哪种资源。
    ///
    /// 同一个角色可能在剧本里既用骨骼出场、又用图片出场（比如某一幕换成 CG 立绘），
    /// 所以去重要按"角色 + 类型"这一对，不能只按角色 ID。
    /// </summary>
    public readonly struct ActorAssetRef : IEquatable<ActorAssetRef>
    {
        public readonly int ActorId;
        public readonly EActorAssetKind Kind;

        public ActorAssetRef(int actorId, EActorAssetKind kind)
        {
            ActorId = actorId;
            Kind = kind;
        }

        public bool Equals(ActorAssetRef other) => ActorId == other.ActorId && Kind == other.Kind;
        public override bool Equals(object obj) => obj is ActorAssetRef other && Equals(other);
        public override int GetHashCode() => (ActorId * 397) ^ (int)Kind;
        public override string ToString() => $"{ActorId}/{Kind}";
    }

    /// <summary>
    /// 扫一遍剧本，把它要用到的资源 ID 全收出来。
    ///
    /// 用途是<b>播放前批量预载</b>：剧情播到一半再去现加载立绘/语音必然卡顿。
    /// 包里只负责"收集"（因为只有包认识各种 Action 的字段），
    /// "怎么加载"交给宿主的 <see cref="Services.IDramaAssetProvider"/>。
    /// </summary>
    public readonly struct DramaAssetKeys
    {
        // ---- 走 IDramaAssetProvider（宿主的 Addressables 封装）

        /// <summary>
        /// 剧本里提到的所有角色 ID。<b>预载立绘资源请用 <see cref="ActorAssets"/></b> ——
        /// 这里只有 ID 没有类型，宿主不知道该去角色表的哪个路径字段取。
        /// 留着它是给"按角色预热名字表"这类只需要 ID 的用途。
        /// </summary>
        public readonly IReadOnlyCollection<int> ActorIds;

        /// <summary>
        /// 要预载的立绘：角色 + 用哪种资源。
        ///
        /// 只从 <see cref="ActorShowAction"/> 收 —— 它是唯一声明了立绘类型的指令。
        /// 位移 / 缩放 / 播动画那些只有角色 ID，本来也是在出场之后才轮得到。
        /// </summary>
        public readonly IReadOnlyCollection<ActorAssetRef> ActorAssets;

        public readonly IReadOnlyCollection<long> BackgroundIds;

        // BGM 不在这里：MusicId 是宿主音频配置表的 ID，clip 跟着配置表一起在内存里，
        // 没有"要预载的资源"这回事。本结构体里的东西一律是【需要预载的】，别往里塞别的。

        // ---- 走 IDramaLocalization（Unity Localization）
        /// <summary>台词 / 说话人名 / 选项文字用到的 String Table 表名。</summary>
        public readonly IReadOnlyCollection<string> StringTables;

        /// <summary>台词语音逐条的引用。想精确预热到条目时用。</summary>
        public readonly IReadOnlyCollection<LocalizedRef> VoiceRefs;

        /// <summary>台词语音用到的 Asset Table 表名，交给 PreloadAssetTablesAsync。</summary>
        public readonly IReadOnlyCollection<string> VoiceTables;

        DramaAssetKeys(HashSet<int> actors, HashSet<ActorAssetRef> actorAssets, HashSet<long> backgrounds,
                       List<LocalizedRef> voices, HashSet<string> stringTables, HashSet<string> voiceTables)
        {
            ActorIds = actors;
            ActorAssets = actorAssets;
            BackgroundIds = backgrounds;
            VoiceRefs = voices;
            StringTables = stringTables;
            VoiceTables = voiceTables;
        }

        public static DramaAssetKeys Collect(DramaScript script)
        {
            var actors = new HashSet<int>();
            var actorAssets = new HashSet<ActorAssetRef>();
            var backgrounds = new HashSet<long>();
            var voices = new List<LocalizedRef>();
            var voiceSeen = new HashSet<string>();
            var tables = new HashSet<string>();
            var voiceTables = new HashSet<string>();

            if (script?.Actions == null)
                return new DramaAssetKeys(actors, actorAssets, backgrounds, voices, tables, voiceTables);

            // 这里刻意遍历整张表而不是 WalkAll：
            // 预载多load几个没走到的分支，远比播到一半发现没加载要好
            foreach (var action in script.Actions)
            {
                switch (action)
                {
                    case null:
                        continue;

                    case TalkAction talk:
                        AddTable(tables, talk.Text);
                        AddTable(tables, talk.SpeakerName);
                        if (!talk.Voice.IsEmpty && voiceSeen.Add(talk.Voice.ToString()))
                        {
                            voices.Add(talk.Voice);
                            AddTable(voiceTables, talk.Voice);
                        }
                        if (talk.Speaker == ESpeakerKind.Actor) actors.Add(talk.ActorId);
                        break;

                    case ChoiceAction choice:
                        if (choice.Options != null)
                            foreach (var o in choice.Options) AddTable(tables, o.Text);
                        break;

                    case ChangeBackgroundAction bg:
                        if (bg.BackgroundId > 0) backgrounds.Add(bg.BackgroundId);
                        break;

                    // PlayMusicAction 刻意不收：MusicId 不是要加载的资源，见结构体开头的说明

                    case ActorShowAction a:
                        actors.Add(a.ActorId);
                        if (a.ActorId > 0) actorAssets.Add(new ActorAssetRef(a.ActorId, a.AssetKind));
                        break;
                    case ActorMoveAction a:          actors.Add(a.ActorId); break;
                    case ActorScaleAction a:         actors.Add(a.ActorId); break;
                    case ActorRotateAction a:        actors.Add(a.ActorId); break;
                    case ActorOffsetMoveAction a:    actors.Add(a.ActorId); break;
                    case ActorShakeAction a:         actors.Add(a.ActorId); break;
                    case ActorVibrateAction a:       actors.Add(a.ActorId); break;
                    case ActorSetSkinAction a:       actors.Add(a.ActorId); break;
                    case ActorPlayAnimationAction a: actors.Add(a.ActorId); break;
                    case ActorAnimBoolAction a:      actors.Add(a.ActorId); break;
                    case ActorAnimIntAction a:       actors.Add(a.ActorId); break;
                    case ActorAnimFloatAction a:     actors.Add(a.ActorId); break;
                    case ActorAnimTriggerAction a:   actors.Add(a.ActorId); break;
                    // ActorHighlightAction 是全局开关，不针对角色，没有可收的 ID
                }
            }

            actors.Remove(-1);   // -1 是"没指定角色"的哨兵，别去加载它
            return new DramaAssetKeys(actors, actorAssets, backgrounds, voices, tables, voiceTables);
        }

        static void AddTable(HashSet<string> tables, LocalizedRef reference)
        {
            if (!string.IsNullOrEmpty(reference.Table)) tables.Add(reference.Table);
        }
    }
}

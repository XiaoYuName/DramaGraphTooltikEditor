using System.Collections.Generic;

namespace Drama.Runtime.Flow
{
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
        public readonly IReadOnlyCollection<int> ActorIds;
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

        DramaAssetKeys(HashSet<int> actors, HashSet<long> backgrounds,
                       List<LocalizedRef> voices, HashSet<string> stringTables, HashSet<string> voiceTables)
        {
            ActorIds = actors;
            BackgroundIds = backgrounds;
            VoiceRefs = voices;
            StringTables = stringTables;
            VoiceTables = voiceTables;
        }

        public static DramaAssetKeys Collect(DramaScript script)
        {
            var actors = new HashSet<int>();
            var backgrounds = new HashSet<long>();
            var voices = new List<LocalizedRef>();
            var voiceSeen = new HashSet<string>();
            var tables = new HashSet<string>();
            var voiceTables = new HashSet<string>();

            if (script?.Actions == null)
                return new DramaAssetKeys(actors, backgrounds, voices, tables, voiceTables);

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

                    case ActorShowAction a:          actors.Add(a.ActorId); break;
                    case ActorMoveAction a:          actors.Add(a.ActorId); break;
                    case ActorScaleAction a:         actors.Add(a.ActorId); break;
                    case ActorRotateAction a:        actors.Add(a.ActorId); break;
                    case ActorOffsetMoveAction a:    actors.Add(a.ActorId); break;
                    case ActorShakeAction a:         actors.Add(a.ActorId); break;
                    case ActorVibrateAction a:       actors.Add(a.ActorId); break;
                    case ActorSetSkinAction a:       actors.Add(a.ActorId); break;
                    case ActorPlayAnimationAction a: actors.Add(a.ActorId); break;
                    case ActorHighlightAction a:     actors.Add(a.ActorId); break;
                }
            }

            actors.Remove(-1);   // -1 是"没指定角色"的哨兵，别去加载它
            return new DramaAssetKeys(actors, backgrounds, voices, tables, voiceTables);
        }

        static void AddTable(HashSet<string> tables, LocalizedRef reference)
        {
            if (!string.IsNullOrEmpty(reference.Table)) tables.Add(reference.Table);
        }
    }
}

using System.Linq;
using Drama.Runtime.Flow;
using NUnit.Framework;
using UnityEngine;

namespace Drama.Runtime.Tests
{
    public sealed class DramaAssetKeysTests
    {
        static DramaScript Make(params DramaAction[] actions)
        {
            var s = ScriptableObject.CreateInstance<DramaScript>();
            s.EntryIndex = 0;
            s.Actions.AddRange(actions);
            return s;
        }

        [Test]
        public void 文本表和语音表分开收集()
        {
            var keys = DramaAssetKeys.Collect(Make(
                new TalkAction
                {
                    Text  = new LocalizedRef { Table = "Story", Key = "L1" },
                    Voice = new LocalizedRef { Table = "StoryVoice", Key = "L1" },
                },
                new TalkAction
                {
                    Text        = new LocalizedRef { Table = "Story", Key = "L2" },
                    Speaker     = ESpeakerKind.Custom,
                    SpeakerName = new LocalizedRef { Table = "Names", Key = "N1" },
                }));

            CollectionAssert.AreEquivalent(new[] { "Story", "Names" }, keys.StringTables);
            CollectionAssert.AreEquivalent(new[] { "StoryVoice" }, keys.VoiceTables);
            Assert.AreEqual(1, keys.VoiceRefs.Count);
            Assert.AreEqual("StoryVoice/L1", keys.VoiceRefs.First().ToString());
        }

        [Test]
        public void 重复的语音只收一次()
        {
            var voice = new LocalizedRef { Table = "V", Key = "same" };
            var keys = DramaAssetKeys.Collect(Make(
                new TalkAction { Voice = voice },
                new TalkAction { Voice = voice }));

            Assert.AreEqual(1, keys.VoiceRefs.Count);
        }

        [Test]
        public void 立绘和背景按类型分桶()
        {
            var keys = DramaAssetKeys.Collect(Make(
                new ActorShowAction { ActorId = 10000 },
                new ActorMoveAction { ActorId = 10000 },
                new ActorSetSkinAction { ActorId = 10001 },
                // ActorHighlightAction 是全局开关，没有 ActorId，不该被收进来
                new ActorHighlightAction { Gray = true },
                new ChangeBackgroundAction { BackgroundId = 500 },
                // BGM 不该被收进来 —— MusicId 是音频配置表 ID，不是要预载的资源
                new PlayMusicAction { MusicId = "bgm_01" }));

            CollectionAssert.AreEquivalent(new[] { 10000, 10001 }, keys.ActorIds);
            CollectionAssert.AreEquivalent(new long[] { 500 }, keys.BackgroundIds);
        }

        [Test]
        public void 哨兵值不会被当成资源去加载()
        {
            var keys = DramaAssetKeys.Collect(Make(
                new ActorShowAction { ActorId = -1 },
                new ChangeBackgroundAction { BackgroundId = -1 }));

            CollectionAssert.IsEmpty(keys.ActorIds);
            CollectionAssert.IsEmpty(keys.BackgroundIds);
        }

        [Test]
        public void 选项文字的表也要预载()
        {
            var keys = DramaAssetKeys.Collect(Make(new ChoiceAction
            {
                Options = new[]
                {
                    new ChoiceAction.Option { Text = new LocalizedRef { Table = "Choices", Key = "A" } },
                    new ChoiceAction.Option { Text = new LocalizedRef { Table = "Choices", Key = "B" } },
                },
            }));

            CollectionAssert.AreEquivalent(new[] { "Choices" }, keys.StringTables);
        }

        [Test]
        public void 收集覆盖整张表而不只是可达分支()
        {
            // 未选中的分支也要预载，不然玩家一选就卡
            var s = Make(
                new TalkAction { Text = new LocalizedRef { Table = "T1", Key = "K" } },
                new TalkAction { Text = new LocalizedRef { Table = "T2", Key = "K" } });
            s.Actions[0].Next = new int[0];   // 第二条从入口不可达

            var keys = DramaAssetKeys.Collect(s);

            CollectionAssert.AreEquivalent(new[] { "T1", "T2" }, keys.StringTables);
        }
    }
}

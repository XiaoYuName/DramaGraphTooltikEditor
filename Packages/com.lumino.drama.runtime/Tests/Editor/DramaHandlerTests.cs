using System.Threading;
using Cysharp.Threading.Tasks;
using Drama.Runtime.Flow;
using Drama.Runtime.Handlers;
using Drama.Runtime.Services;
using NUnit.Framework;
using UnityEngine;

namespace Drama.Runtime.Tests
{
    public sealed class DramaHandlerTests
    {
        MockServices m_S;

        [SetUp]
        public void SetUp() => m_S = new MockServices();

        [TearDown]
        public void TearDown() => m_S.Actors.DestroyCreatedObjects();

        /// <summary>跑一个 Handler，返回它的 awaiter（可能还没完成）。</summary>
        UniTask<DramaFlowResult>.Awaiter Run(IDramaActionHandler handler, DramaAction action,
                                             CancellationToken ct = default) =>
            handler.ExecuteAsync(action, m_S.Context, ct).GetAwaiter();

        void RunToEnd(IDramaActionHandler handler, DramaAction action)
        {
            var awaiter = Run(handler, action);
            Assert.IsTrue(awaiter.IsCompleted, "Handler 没有同步跑完");
            awaiter.GetResult();
        }

        // ============================================================ WaitAction

        [Test]
        public void 等待_正常模式下会真的挂起()
        {
            var awaiter = Run(new WaitActionHandler(), new WaitAction { Seconds = 3f });
            Assert.IsFalse(awaiter.IsCompleted);
        }

        [Test]
        public void 等待_Skip模式下立刻返回()
        {
            m_S.Mode = EDramaPlaybackMode.Skip;
            var awaiter = Run(new WaitActionHandler(), new WaitAction { Seconds = 3f });
            Assert.IsTrue(awaiter.IsCompleted);
        }

        [Test]
        public void 等待_时长为零时立刻返回()
        {
            var awaiter = Run(new WaitActionHandler(), new WaitAction { Seconds = 0f });
            Assert.IsTrue(awaiter.IsCompleted);
        }

        [Test]
        public void 快进按倍率缩短时长()
        {
            Assert.AreEqual(1f,    DramaWait.Scale(1f, EDramaPlaybackMode.Normal));
            Assert.AreEqual(0.25f, DramaWait.Scale(1f, EDramaPlaybackMode.FastForward), 1e-5f);
            Assert.AreEqual(0f,    DramaWait.Scale(1f, EDramaPlaybackMode.Skip));
        }

        // ============================================================ TalkAction

        TalkAction Line(string key = "K") => new TalkAction
        {
            Text = new LocalizedRef { Table = "T", Key = key },
            Speaker = ESpeakerKind.Aside,
        };

        [Test]
        public void 台词_显示后停下来等玩家点击()
        {
            var awaiter = Run(new TalkActionHandler(), Line());

            Assert.AreEqual(1, m_S.Dialogue.Shown.Count);
            Assert.AreEqual("T/K", m_S.Dialogue.Shown[0].Body);
            Assert.IsFalse(awaiter.IsCompleted, "应该卡在等玩家翻页上");

            m_S.Dialogue.AdvanceLatch.Open();
            Assert.IsTrue(awaiter.IsCompleted);
        }

        [Test]
        public void 台词_打字机没跑完时不会先去等翻页()
        {
            m_S.Dialogue.AutoFinishTypewriter = false;
            var awaiter = Run(new TalkActionHandler(), Line());

            Assert.IsTrue(m_S.Dialogue.TypewriterLatch.IsWaiting);
            Assert.IsFalse(m_S.Dialogue.AdvanceLatch.IsWaiting, "打字机还没完就不该开始等翻页");

            m_S.Dialogue.TypewriterLatch.Open();
            Assert.IsTrue(m_S.Dialogue.AdvanceLatch.IsWaiting);
            Assert.IsFalse(awaiter.IsCompleted);

            m_S.Dialogue.AdvanceLatch.Open();
            Assert.IsTrue(awaiter.IsCompleted);
        }

        [Test]
        public void 台词_Skip模式下不等任何东西()
        {
            m_S.Mode = EDramaPlaybackMode.Skip;
            RunToEnd(new TalkActionHandler(), Line());

            Assert.AreEqual(1, m_S.Dialogue.Shown.Count);
            Assert.IsFalse(m_S.Dialogue.AdvanceLatch.IsWaiting);
        }

        [Test]
        public void 台词_自动等待期间玩家点击可以提前翻页()
        {
            var a = Line();
            a.AutoWaitSeconds = 5f;

            var awaiter = Run(new TalkActionHandler(), a);
            Assert.IsFalse(awaiter.IsCompleted);

            m_S.Dialogue.AdvanceLatch.Open();     // 5 秒没到，但玩家点了
            Assert.IsTrue(awaiter.IsCompleted);
        }

        [Test]
        public void 台词_说话人名字按类型解析()
        {
            var handler = new TalkActionHandler
            {
                HeroNameProvider  = () => "主角名",
                ActorNameProvider = id => $"角色{id}",
            };

            string NameOf(TalkAction a)
            {
                m_S.Dialogue.Shown.Clear();
                var awaiter = Run(handler, a);
                m_S.Dialogue.AdvanceLatch.Open();
                awaiter.GetResult();
                return m_S.Dialogue.Shown[0].SpeakerName;
            }

            Assert.AreEqual(string.Empty, NameOf(new TalkAction { Speaker = ESpeakerKind.Aside }));
            Assert.AreEqual("主角名",      NameOf(new TalkAction { Speaker = ESpeakerKind.Hero }));
            Assert.AreEqual("角色7",       NameOf(new TalkAction { Speaker = ESpeakerKind.Actor, ActorId = 7 }));
            Assert.AreEqual("T/N",         NameOf(new TalkAction
            {
                Speaker = ESpeakerKind.Custom,
                SpeakerName = new LocalizedRef { Table = "T", Key = "N" },
            }));
        }

        [Test]
        public void 台词_没配语音时不去加载()
        {
            var awaiter = Run(new TalkActionHandler(), Line());
            m_S.Dialogue.AdvanceLatch.Open();
            awaiter.GetResult();

            CollectionAssert.IsEmpty(m_S.Localization.RequestedVoices);
            Assert.AreEqual(0, m_S.Audio.VoicePlayCount);
        }

        [Test]
        public void 台词_配了语音就走多语言资源表加载并播放()
        {
            m_S.Localization.VoiceToReturn = AudioClip.Create("v", 16, 1, 8000, false);

            var a = Line();
            a.Voice = new LocalizedRef { Table = "Voice", Key = "0001" };

            var awaiter = Run(new TalkActionHandler(), a);
            m_S.Dialogue.AdvanceLatch.Open();
            awaiter.GetResult();

            Assert.AreEqual(1, m_S.Localization.RequestedVoices.Count);
            Assert.AreEqual("Voice/0001", m_S.Localization.RequestedVoices[0].ToString());
            Assert.AreEqual(1, m_S.Audio.VoicePlayCount);
        }

        // ============================================================ ActorShowAction

        ActorShowAction Show(EActorShowKind kind, bool wait) => new ActorShowAction
        {
            ActorId = 100,
            ShowKind = kind,
            DurationSeconds = 1f,
            WaitForCompletion = wait,
            Position = new Vector2(3f, 4f),
            ScalePercent = new Vector2(50f, 50f),
        };

        [Test]
        public void 立绘_先摆好布局再放动画()
        {
            RunToEnd(new ActorShowActionHandler(), Show(EActorShowKind.Show, true));

            var actor = m_S.Actors.Get(100);
            Assert.AreEqual(new Vector3(3f, 4f, 0f), actor.Root.localPosition);
            Assert.AreEqual(new Vector3(0.5f, 0.5f, 1f), actor.Root.localScale);
        }

        [Test]
        public void 立绘_勾了等待动画就真的等()
        {
            m_S.Actors.AutoFinishVisibility = false;

            var awaiter = Run(new ActorShowActionHandler(), Show(EActorShowKind.FadeIn, wait: true));

            Assert.IsFalse(awaiter.IsCompleted);
            m_S.Actors.VisibilityLatch.Open();
            Assert.IsTrue(awaiter.IsCompleted);
        }

        [Test]
        public void 立绘_没勾等待动画就立刻继续()
        {
            m_S.Actors.AutoFinishVisibility = false;

            var awaiter = Run(new ActorShowActionHandler(), Show(EActorShowKind.FadeIn, wait: false));

            // 动画还挂着，但 Handler 已经返回了 —— 这条动画归 IActorStage 收着
            Assert.IsTrue(awaiter.IsCompleted);
            Assert.IsTrue(m_S.Actors.VisibilityLatch.IsWaiting);

            m_S.Actors.VisibilityLatch.Open();   // 收尾，别留悬空的 tcs
        }

        [Test]
        public void 立绘_非淡入淡出不吃时长()
        {
            RunToEnd(new ActorShowActionHandler(), Show(EActorShowKind.Show, true));
            Assert.AreEqual(0f, m_S.Actors.LastDuration);
            Assert.AreEqual(true, m_S.Actors.LastVisible);
        }

        [Test]
        public void 立绘_Skip模式下淡入变成瞬间()
        {
            m_S.Mode = EDramaPlaybackMode.Skip;
            RunToEnd(new ActorShowActionHandler(), Show(EActorShowKind.FadeIn, true));
            Assert.AreEqual(0f, m_S.Actors.LastDuration);
        }

        [Test]
        public void 立绘_快进模式下淡入按倍率缩短()
        {
            m_S.Mode = EDramaPlaybackMode.FastForward;
            RunToEnd(new ActorShowActionHandler(), Show(EActorShowKind.FadeIn, true));
            Assert.AreEqual(0.25f, m_S.Actors.LastDuration, 1e-5f);
        }

        // ============================================================ 其余 Handler

        [Test]
        public void 立绘动画_没配动画名时跳过()
        {
            m_S.Actors.AcquireAsync(100, default).GetAwaiter().GetResult();

            LogAssert_ExpectWarning();
            RunToEnd(new ActorPlayAnimationActionHandler(),
                     new ActorPlayAnimationAction { ActorId = 100, AnimationName = "" });

            CollectionAssert.IsEmpty(m_S.Actors.Get(100).PlayedAnimations);
        }

        [Test]
        public void 立绘动画_配了动画名就播()
        {
            m_S.Actors.AcquireAsync(100, default).GetAwaiter().GetResult();

            RunToEnd(new ActorPlayAnimationActionHandler(),
                     new ActorPlayAnimationAction { ActorId = 100, AnimationName = "idle", TrackIndex = 2 });

            CollectionAssert.AreEqual(new[] { "idle" }, m_S.Actors.Get(100).PlayedAnimations);
        }

        [Test]
        public void 讲话人突出_置灰和微缩都传下去()
        {
            m_S.Actors.AcquireAsync(100, default).GetAwaiter().GetResult();

            RunToEnd(new ActorHighlightActionHandler(),
                     new ActorHighlightAction { ActorId = 100, Gray = true, Shrink = false });

            Assert.IsTrue(m_S.Actors.Get(100).Gray);
            Assert.IsFalse(m_S.Actors.Get(100).Shrink);
        }

        [Test]
        public void 选项_把选中支线报给流程层()
        {
            m_S.Choice.PickIndex = 1;

            var action = new ChoiceAction
            {
                Options = new[]
                {
                    new ChoiceAction.Option { Text = new LocalizedRef { Table = "T", Key = "A" }, Next = 5 },
                    new ChoiceAction.Option { Text = new LocalizedRef { Table = "T", Key = "B" }, Next = 9 },
                },
            };

            var awaiter = Run(new ChoiceActionHandler(), action);
            var result = awaiter.GetResult();

            CollectionAssert.AreEqual(new[] { "T/A", "T/B" }, m_S.Choice.LastOptions);
            Assert.AreEqual(DramaFlowResult.EKind.Jump, result.Kind);
            Assert.AreEqual(9, result.JumpTarget);
        }

        [Test]
        public void 领取任务_转交给业务层()
        {
            RunToEnd(new ReceiveTaskActionHandler(), new ReceiveTaskAction { TaskId = 42 });
            CollectionAssert.AreEqual(new long[] { 42 }, m_S.Game.ReceivedTasks);
        }

        [Test]
        public void 领取任务_ID非法时不打扰业务层()
        {
            RunToEnd(new ReceiveTaskActionHandler(), new ReceiveTaskAction { TaskId = -1 });
            CollectionAssert.IsEmpty(m_S.Game.ReceivedTasks);
        }

        static void LogAssert_ExpectWarning() =>
            UnityEngine.TestTools.LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(".*"));
    }
}

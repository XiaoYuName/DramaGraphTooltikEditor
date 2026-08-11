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
            Assert.AreEqual("K", m_S.Dialogue.Shown[0].TextRef.Key);
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
        public void 台词_说话人寻址方式原样透传给View()
        {
            // 名字怎么取是 View 的事（玩家昵称没有 Table/Key，角色名要查宿主配置表），
            // Handler 只负责把寻址方式交出去，一个字符串都不解析
            var handler = new TalkActionHandler();

            DialogueLine LineOf(TalkAction a)
            {
                m_S.Dialogue.Shown.Clear();
                var awaiter = Run(handler, a);
                m_S.Dialogue.AdvanceLatch.Open();
                awaiter.GetResult();
                return m_S.Dialogue.Shown[0];
            }

            Assert.AreEqual(ESpeakerKind.Aside, LineOf(new TalkAction { Speaker = ESpeakerKind.Aside }).Speaker);
            Assert.AreEqual(ESpeakerKind.Hero, LineOf(new TalkAction { Speaker = ESpeakerKind.Hero }).Speaker);

            var actor = LineOf(new TalkAction { Speaker = ESpeakerKind.Actor, ActorId = 7 });
            Assert.AreEqual(ESpeakerKind.Actor, actor.Speaker);
            Assert.AreEqual(7, actor.ActorId);

            var custom = LineOf(new TalkAction
            {
                Speaker = ESpeakerKind.Custom,
                SpeakerName = new LocalizedRef { Table = "T", Key = "N" },
            });
            Assert.AreEqual("T", custom.SpeakerNameRef.Table);
            Assert.AreEqual("N", custom.SpeakerNameRef.Key);
        }

        [Test]
        public void 台词_正文交的是引用而不是解析结果()
        {
            var a = Line();
            a.Text = new LocalizedRef { Table = "Dialogue", Key = "L1" };

            var awaiter = Run(new TalkActionHandler(), a);
            m_S.Dialogue.AdvanceLatch.Open();
            awaiter.GetResult();

            Assert.AreEqual("Dialogue", m_S.Dialogue.Shown[0].TextRef.Table);
            Assert.AreEqual("L1", m_S.Dialogue.Shown[0].TextRef.Key);
        }

        [Test]
        public void 台词_没配语音时不去播()
        {
            var awaiter = Run(new TalkActionHandler(), Line());
            m_S.Dialogue.AdvanceLatch.Open();
            awaiter.GetResult();

            CollectionAssert.IsEmpty(m_S.Audio.PlayedVoices);
        }

        [Test]
        public void 台词_配了语音就把引用交给音频层()
        {
            var a = Line();
            a.Voice = new LocalizedRef { Table = "Voice", Key = "0001" };

            var awaiter = Run(new TalkActionHandler(), a);
            m_S.Dialogue.AdvanceLatch.Open();
            awaiter.GetResult();

            Assert.AreEqual(1, m_S.Audio.PlayedVoices.Count);
            Assert.AreEqual("Voice/0001", m_S.Audio.PlayedVoices[0].ToString());

            // 引用也要一并交给 View —— 切语言时它得靠这个重播
            Assert.AreEqual("Voice/0001", m_S.Dialogue.Shown[0].VoiceRef.ToString());
        }

        [Test]
        public void 台词_显示不会被语音加载挡住()
        {
            // 语音改成传引用之后 Handler 里不再 await 资源加载，
            // 所以 ShowLineAsync 必须在同一个同步段里就被调到
            var a = Line();
            a.Voice = new LocalizedRef { Table = "Voice", Key = "0001" };

            Run(new TalkActionHandler(), a);

            Assert.AreEqual(1, m_S.Dialogue.Shown.Count, "台词应当立刻显示，不等语音");
        }

        // ============================================================ ActorShowAction

        ActorShowAction Show(EActorShowKind kind) => new ActorShowAction
        {
            ActorId = 100,
            ShowKind = kind,
            DurationSeconds = 1f,
            Position = new Vector2(3f, 4f),
            Scale = new Vector2(0.5f, 0.5f),   // 倍率，不是百分比
        };

        [Test]
        public void 立绘_先摆好布局再放动画()
        {
            RunToEnd(new ActorShowActionHandler(), Show(EActorShowKind.Show));

            var actor = m_S.Actors.Get(100);
            Assert.AreEqual(new Vector3(3f, 4f, 0f), actor.Root.localPosition);
            Assert.AreEqual(new Vector3(0.5f, 0.5f, 1f), actor.Root.localScale);
        }

        [Test]
        public void 立绘_方向要交给舞台()
        {
            var a = Show(EActorShowKind.Show);
            a.Direction = EActorShowDirection.Right;

            RunToEnd(new ActorShowActionHandler(), a);

            // 方向不能只导出不用 —— 这条测试就是防止 SetDirection 哪天又被漏掉
            Assert.AreEqual(EActorShowDirection.Right, m_S.Actors.Directions[100]);

            // 而且 Position 要在方向之后写，不能被方向覆盖
            Assert.AreEqual(new Vector3(3f, 4f, 0f), m_S.Actors.Get(100).Root.localPosition);
        }

        [Test]
        public void 立绘_一律等动画跑完才继续()
        {
            // 没有"不等动画"这个开关了 —— 想并行请在图里连并行分支。
            // 这条测试锁住"总是等"，防止哪天又冒出个 fire-and-forget 分支
            m_S.Actors.AutoFinishVisibility = false;

            var awaiter = Run(new ActorShowActionHandler(), Show(EActorShowKind.FadeIn));

            Assert.IsFalse(awaiter.IsCompleted);
            m_S.Actors.VisibilityLatch.Open();
            Assert.IsTrue(awaiter.IsCompleted);
        }

        [Test]
        public void 立绘_非淡入淡出不吃时长()
        {
            RunToEnd(new ActorShowActionHandler(), Show(EActorShowKind.Show));
            Assert.AreEqual(0f, m_S.Actors.LastDuration);
            Assert.AreEqual(true, m_S.Actors.LastVisible);
        }

        [Test]
        public void 立绘_Skip模式下淡入变成瞬间()
        {
            m_S.Mode = EDramaPlaybackMode.Skip;
            RunToEnd(new ActorShowActionHandler(), Show(EActorShowKind.FadeIn));
            Assert.AreEqual(0f, m_S.Actors.LastDuration);
        }

        [Test]
        public void 立绘_快进模式下淡入按倍率缩短()
        {
            m_S.Mode = EDramaPlaybackMode.FastForward;
            RunToEnd(new ActorShowActionHandler(), Show(EActorShowKind.FadeIn));
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

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
        public void TearDown()
        {
            m_S.Actors.DestroyCreatedObjects();
            m_S.CG.DestroyCreatedObjects();
        }

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
        public void 获取奖励_手动模式停下来等玩家关弹窗()
        {
            m_S.Game.HoldReward = true;
            var awaiter = Run(new ReceiveRewardActionHandler(), new ReceiveRewardAction { RewardId = 7 });

            Assert.AreEqual(1, m_S.Game.ShownRewards.Count);
            Assert.AreEqual(7, m_S.Game.ShownRewards[0].RewardId);
            Assert.AreEqual(EDramaPlaybackMode.Normal, m_S.Game.ShownRewards[0].Mode);
            Assert.IsFalse(awaiter.IsCompleted, "应该卡在等玩家关弹窗上");

            m_S.Game.RewardLatch.Open();
            Assert.IsTrue(awaiter.IsCompleted);
        }

        [Test]
        public void 获取奖励_跳过模式照发但把模式一起交给宿主()
        {
            // 跳过时玩家在看戏，奖励该发；弹窗怎么自己收掉是宿主的事，所以模式要交过去
            m_S.Mode = EDramaPlaybackMode.Skip;
            RunToEnd(new ReceiveRewardActionHandler(), new ReceiveRewardAction { RewardId = 7 });

            Assert.AreEqual(1, m_S.Game.ShownRewards.Count);
            Assert.AreEqual(EDramaPlaybackMode.Skip, m_S.Game.ShownRewards[0].Mode);
        }

        [Test]
        public void 获取奖励_读档恢复期间一律不发()
        {
            // ★ 静默重放会把整段剧情重走一遍，不拦的话玩家每读一次档就白拿一份奖励
            m_S.Mode = EDramaPlaybackMode.Restoring;
            RunToEnd(new ReceiveRewardActionHandler(), new ReceiveRewardAction { RewardId = 7 });

            Assert.AreEqual(0, m_S.Game.ShownRewards.Count);
        }

        [Test]
        public void 小游戏_停下来等玩家玩完()
        {
            m_S.Game.HoldMinGame = true;
            var awaiter = Run(new PlayMinGameActionHandler(), new PlayMinGameAction { MinGameId = 16 });

            Assert.AreEqual(1, m_S.Game.PlayedMinGames.Count);
            Assert.AreEqual(16, m_S.Game.PlayedMinGames[0], "枚举的整数值要原样交给宿主");
            Assert.IsFalse(awaiter.IsCompleted, "应该卡在等玩法结束上（含玩家点掉成功界面）");

            m_S.Game.MinGameLatch.Open();
            Assert.IsTrue(awaiter.IsCompleted);
        }

        [Test]
        public void 小游戏_跳过模式也照玩()
        {
            // 小游戏是玩家要动手的关卡，不是能快进的演出
            m_S.Mode = EDramaPlaybackMode.Skip;
            RunToEnd(new PlayMinGameActionHandler(), new PlayMinGameAction { MinGameId = 16 });

            Assert.AreEqual(1, m_S.Game.PlayedMinGames.Count);
        }

        [Test]
        public void 小游戏_没填类型时什么都不做()
        {
            RunToEnd(new PlayMinGameActionHandler(), new PlayMinGameAction { MinGameId = -1 });

            Assert.AreEqual(0, m_S.Game.PlayedMinGames.Count);
        }

        [Test]
        public void 小游戏_读档恢复期间不玩()
        {
            // 那些关卡玩家当年已经过了，重放时再丢进去等于让他重打一遍
            m_S.Mode = EDramaPlaybackMode.Restoring;
            RunToEnd(new PlayMinGameActionHandler(), new PlayMinGameAction { MinGameId = 16 });

            Assert.AreEqual(0, m_S.Game.PlayedMinGames.Count);
        }

        [Test]
        public void 打开界面_手动模式停下来等玩家关掉()
        {
            m_S.Game.HoldUI = true;
            var awaiter = Run(new ShowUIActionHandler(), new ShowUIAction { UiPage = "InventoryUI" });

            Assert.AreEqual(1, m_S.Game.ShownUIs.Count);
            Assert.AreEqual("InventoryUI", m_S.Game.ShownUIs[0].UiPage);
            Assert.IsFalse(awaiter.IsCompleted, "应该卡在等玩家关界面上");

            m_S.Game.UILatch.Open();
            Assert.IsTrue(awaiter.IsCompleted);
        }

        [Test]
        public void 打开界面_没填界面ID时什么都不做()
        {
            RunToEnd(new ShowUIActionHandler(), new ShowUIAction { UiPage = "" });

            Assert.AreEqual(0, m_S.Game.ShownUIs.Count);
        }

        [Test]
        public void 打开界面_读档恢复期间不弹()
        {
            // 静默重放不该往玩家脸上弹界面
            m_S.Mode = EDramaPlaybackMode.Restoring;
            RunToEnd(new ShowUIActionHandler(), new ShowUIAction { UiPage = "InventoryUI" });

            Assert.AreEqual(0, m_S.Game.ShownUIs.Count);
        }

        [Test]
        public void 领取任务_读档恢复期间不重复领()
        {
            m_S.Mode = EDramaPlaybackMode.Restoring;
            RunToEnd(new ReceiveTaskActionHandler(), new ReceiveTaskAction { TaskId = 3 });

            Assert.AreEqual(0, m_S.Game.ReceivedTasks.Count);
        }

        [Test]
        public void 等待点击_停下来等玩家点一下()
        {
            // 没有台词的场合（整屏 CG）用它把节奏交回玩家手里。
            // 点击入口在宿主那边是盖满全屏的按钮，和对话框显不显示无关
            var awaiter = Run(new WaitInputActionHandler(), new WaitInputAction());

            Assert.IsTrue(m_S.Dialogue.AdvanceLatch.IsWaiting);
            Assert.IsFalse(awaiter.IsCompleted, "应该卡在等玩家点击上");

            m_S.Dialogue.AdvanceLatch.Open();
            Assert.IsTrue(awaiter.IsCompleted);
        }

        [Test]
        public void 等待点击_Skip模式下不等人()
        {
            m_S.Mode = EDramaPlaybackMode.Skip;
            RunToEnd(new WaitInputActionHandler(), new WaitInputAction());

            Assert.IsFalse(m_S.Dialogue.AdvanceLatch.IsWaiting);
        }

        [Test]
        public void 等待点击_读档恢复期间不等人()
        {
            // ★ 这条不成立的话，静默重放会停在这儿等点击，读档就再也走不到存档点了
            m_S.Mode = EDramaPlaybackMode.Restoring;
            RunToEnd(new WaitInputActionHandler(), new WaitInputAction());

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

        // ============================================================ ActorHighlightAction

        [Test]
        public void 讲话人突出_只是全局开关不碰具体立绘()
        {
            RunToEnd(new ActorHighlightActionHandler(),
                     new ActorHighlightAction
                     {
                         Gray = true, DimBrightness = 0.5f,
                         Shrink = false, ShrinkScale = 0.9f,
                     });

            Assert.IsTrue(m_S.Actors.Highlight.Dim);
            Assert.AreEqual(0.5f, m_S.Actors.Highlight.DimBrightness);
            Assert.IsFalse(m_S.Actors.Highlight.Shrink);
            Assert.AreEqual(0.9f, m_S.Actors.Highlight.ShrinkScale);

            // 这条指令不该去动任何一个立绘，也不该报说话人
            Assert.AreEqual(0, m_S.Actors.SetSpeakerCalls);
        }

        [Test]
        public void 讲话人突出_说话人由台词逐句报给舞台()
        {
            var a = Line();
            a.Speaker = ESpeakerKind.Actor;
            a.ActorId = 42;

            var awaiter = Run(new TalkActionHandler(), a);
            m_S.Dialogue.AdvanceLatch.Open();
            awaiter.GetResult();

            Assert.AreEqual(42, m_S.Actors.LastSpeaker);
        }

        [Test]
        public void 讲话人突出_旁白没有说话人()
        {
            // 旁白 / 主角 / 自定义名都没有立绘上的说话人，要报 -1 让所有立绘恢复原样
            var awaiter = Run(new TalkActionHandler(), Line());   // Line() 默认是 Aside
            m_S.Dialogue.AdvanceLatch.Open();
            awaiter.GetResult();

            Assert.AreEqual(-1, m_S.Actors.LastSpeaker);
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

        // ============================================================ 立绘类型

        [TestCase(EActorAssetKind.Spine)]
        [TestCase(EActorAssetKind.Texture)]
        [TestCase(EActorAssetKind.Live2D)]
        public void 立绘_出场时把资源类型交给舞台(EActorAssetKind kind)
        {
            // 图里是三个不同的节点，到这一层只剩一条指令 + 一个类型字段。
            // 舞台靠它决定实例化哪种立绘、去角色表的哪个字段取路径
            RunToEnd(new ActorShowActionHandler(),
                     new ActorShowAction { ActorId = 100, AssetKind = kind });

            Assert.AreEqual(kind, m_S.Actors.AcquiredKinds[100]);
        }

        [Test]
        public void 立绘_老资产没有类型字段时按骨骼处理()
        {
            // AssetKind 是后加的字段，之前导出的资产反序列化后落到默认值 0。
            // 那时候只有 Spine 一种，落到 Spine 才是对的
            RunToEnd(new ActorShowActionHandler(), new ActorShowAction { ActorId = 100 });

            Assert.AreEqual(EActorAssetKind.Spine, m_S.Actors.AcquiredKinds[100]);
        }

        // ============================================================ 立绘 Animator

        [Test]
        public void 立绘Animator_四种参数各自打到对应方法()
        {
            m_S.Actors.AcquireAsync(100, EActorAssetKind.Live2D, default).GetAwaiter().GetResult();

            RunToEnd(new ActorAnimBoolActionHandler(),
                     new ActorAnimBoolAction { ActorId = 100, ParameterName = "IsAngry", Value = true });
            RunToEnd(new ActorAnimIntActionHandler(),
                     new ActorAnimIntAction { ActorId = 100, ParameterName = "Face", Value = 3 });
            RunToEnd(new ActorAnimFloatActionHandler(),
                     new ActorAnimFloatAction { ActorId = 100, ParameterName = "Blend", Value = 0.5f });
            RunToEnd(new ActorAnimTriggerActionHandler(),
                     new ActorAnimTriggerAction { ActorId = 100, ParameterName = "Wave" });
            RunToEnd(new ActorAnimTriggerActionHandler(),
                     new ActorAnimTriggerAction { ActorId = 100, ParameterName = "Wave", Reset = true });

            CollectionAssert.AreEqual(
                new[]
                {
                    "bool:IsAngry=True",
                    "int:Face=3",
                    "float:Blend=0.5",
                    "trigger:Wave",
                    "trigger:Wave:reset",
                },
                m_S.Actors.Get(100).AnimatorCalls);
        }

        [Test]
        public void 立绘Animator_立绘不在台上时静默跳过()
        {
            // 没 AcquireAsync 过。剧本顺序错了是导出该拦的事，运行时不该炸
            Assert.DoesNotThrow(() =>
                RunToEnd(new ActorAnimTriggerActionHandler(),
                         new ActorAnimTriggerAction { ActorId = 999, ParameterName = "Wave" }));
        }

        // ============================================================ CG

        [Test]
        public void CG_出现把ID和时长交给CG层()
        {
            RunToEnd(new CGShowActionHandler(),
                     new CGShowAction { CgId = 500, DurationSeconds = 0.6f });

            CollectionAssert.AreEqual(new long[] { 500 }, m_S.CG.Shown);
            Assert.AreEqual(0.6f, m_S.CG.LastDuration, 1e-5f);
        }

        [Test]
        public void CG_没填ID时跳过()
        {
            LogAssert_ExpectWarning();
            RunToEnd(new CGShowActionHandler(), new CGShowAction { CgId = -1 });

            CollectionAssert.IsEmpty(m_S.CG.Shown);
        }

        [TestCase(EDramaPlaybackMode.Skip)]
        [TestCase(EDramaPlaybackMode.Restoring)]
        public void CG_跳过和恢复时显隐时长归零(EDramaPlaybackMode mode)
        {
            m_S.Mode = mode;

            // CG 该出现还是要出现（那是状态），只是不放动画
            RunToEnd(new CGShowActionHandler(),
                     new CGShowAction { CgId = 500, DurationSeconds = 2f });

            CollectionAssert.AreEqual(new long[] { 500 }, m_S.CG.Shown);
            Assert.AreEqual(0f, m_S.CG.LastDuration);
        }

        /// <summary>
        /// CG 没出现就写变换 / 抖动 / 小动作是剧本的顺序问题，
        /// 运行时静默跳过即可，不该让整段剧情停下来。
        /// </summary>
        [Test]
        public void CG_不在台上时变换类指令静默跳过()
        {
            Assert.IsNull(m_S.CG.Root, "前提：还没 Show 过");

            Assert.DoesNotThrow(() =>
            {
                RunToEnd(new CGMoveActionHandler(), new CGMoveAction { Position = Vector2.one });
                RunToEnd(new CGScaleActionHandler(), new CGScaleAction { Scale = Vector3.one });
                RunToEnd(new CGRotateActionHandler(), new CGRotateAction());
                RunToEnd(new CGOffsetMoveActionHandler(), new CGOffsetMoveAction());
                RunToEnd(new CGShakeActionHandler(), new CGShakeAction());
                RunToEnd(new CGVibrateActionHandler(), new CGVibrateAction());
            });
        }

        [Test]
        public void CG_变换作用在CG层的Root上()
        {
            RunToEnd(new CGShowActionHandler(), new CGShowAction { CgId = 500 });

            // 时长为 0 时是直接写值，不起 Tween
            RunToEnd(new CGMoveActionHandler(),
                     new CGMoveAction { Position = new Vector2(3f, 4f) });

            Assert.AreEqual(new Vector3(3f, 4f, 0f), m_S.CG.Root.localPosition);
        }

        [Test]
        public void CG_Animator四种参数各自打到对应方法()
        {
            RunToEnd(new CGAnimBoolActionHandler(),
                     new CGAnimBoolAction { ParameterName = "IsShy", Value = true });
            RunToEnd(new CGAnimIntActionHandler(),
                     new CGAnimIntAction { ParameterName = "Face", Value = 2 });
            RunToEnd(new CGAnimFloatActionHandler(),
                     new CGAnimFloatAction { ParameterName = "Blend", Value = 0.25f });
            RunToEnd(new CGAnimTriggerActionHandler(),
                     new CGAnimTriggerAction { ParameterName = "Blink" });
            RunToEnd(new CGAnimTriggerActionHandler(),
                     new CGAnimTriggerAction { ParameterName = "Blink", Reset = true });

            CollectionAssert.AreEqual(
                new[]
                {
                    "bool:IsShy=True",
                    "int:Face=2",
                    "float:Blend=0.25",
                    "trigger:Blink",
                    "trigger:Blink:reset",
                },
                m_S.CG.AnimatorCalls);
        }

        // ============================================================ 其余 Handler

        [Test]
        public void 立绘动画_没配动画名时跳过()
        {
            m_S.Actors.AcquireAsync(100, EActorAssetKind.Spine, default).GetAwaiter().GetResult();

            LogAssert_ExpectWarning();
            RunToEnd(new ActorPlayAnimationActionHandler(),
                     new ActorPlayAnimationAction { ActorId = 100, AnimationName = "" });

            CollectionAssert.IsEmpty(m_S.Actors.Get(100).PlayedAnimations);
        }

        [Test]
        public void 立绘动画_配了动画名就播()
        {
            m_S.Actors.AcquireAsync(100, EActorAssetKind.Spine, default).GetAwaiter().GetResult();

            RunToEnd(new ActorPlayAnimationActionHandler(),
                     new ActorPlayAnimationAction { ActorId = 100, AnimationName = "idle", TrackIndex = 2 });

            CollectionAssert.AreEqual(new[] { "idle" }, m_S.Actors.Get(100).PlayedAnimations);
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

            Assert.AreEqual(DramaFlowResult.EKind.Jump, result.Kind);
            Assert.AreEqual(9, result.JumpTarget);
        }

        [Test]
        public void 选项_选项文字原样透传给View而不是查好的字符串()
        {
            var action = new ChoiceAction
            {
                Options = new[]
                {
                    new ChoiceAction.Option { Text = new LocalizedRef { Table = "T", Key = "A" }, Next = 5 },
                    new ChoiceAction.Option { Text = new LocalizedRef { Table = "T", Key = "B" }, Next = 9 },
                },
            };

            Run(new ChoiceActionHandler(), action).GetResult();

            CollectionAssert.AreEqual(
                new[]
                {
                    new LocalizedRef { Table = "T", Key = "A" },
                    new LocalizedRef { Table = "T", Key = "B" },
                },
                m_S.Choice.LastOptions);

            // 面板要挂着等玩家，这期间切语言得跟着变 —— 在这一层查表就定死了
            CollectionAssert.IsEmpty(m_S.Localization.Resolved, "选项文字不该在 Handler 层查表");
        }

        /// <summary>
        /// 读档恢复是唯一不问玩家的路径：静默重放走到选项时，
        /// 要把<b>当年选的那个</b>原样喂回去，弹面板会把重放卡死在这儿。
        /// </summary>
        [Test]
        public void 选项_读档恢复时直接用存档里的选择不弹面板()
        {
            m_S.Mode = EDramaPlaybackMode.Restoring;
            m_S.Context.RestoredChoices.Enqueue(1);

            // 面板真被弹出来就永远等不到人，测试会卡在这儿而不是通过
            m_S.Choice.HoldUntilPicked = true;

            var action = new ChoiceAction
            {
                Options = new[]
                {
                    new ChoiceAction.Option { Text = new LocalizedRef { Table = "T", Key = "A" }, Next = 5 },
                    new ChoiceAction.Option { Text = new LocalizedRef { Table = "T", Key = "B" }, Next = 9 },
                },
            };

            var awaiter = Run(new ChoiceActionHandler(), action);

            Assert.IsTrue(awaiter.IsCompleted, "恢复时不该停下来等玩家");
            Assert.IsNull(m_S.Choice.LastOptions, "恢复时压根不该碰选项面板");
            Assert.AreEqual(9, awaiter.GetResult().JumpTarget, "要走存档里记的那条支线");

            // 取走的同时要记进本轮路径，否则恢复完再存一次档，前面的选择就丢了
            CollectionAssert.AreEqual(new[] { 1 }, m_S.Context.PickedChoices);
        }

        [Test]
        public void 选项_恢复记录用光时退化成正常询问()
        {
            m_S.Mode = EDramaPlaybackMode.Restoring;   // 记录是空的
            m_S.Choice.PickIndex = 0;

            var action = new ChoiceAction
            {
                Options = new[]
                {
                    new ChoiceAction.Option { Text = new LocalizedRef { Table = "T", Key = "A" }, Next = 5 },
                },
            };

            var result = Run(new ChoiceActionHandler(), action).GetResult();

            // 恢复中途弹个面板是怪，但比走错支线、恢复出错误的现场强
            Assert.IsNotNull(m_S.Choice.LastOptions, "取不到记录时该退化成问玩家");
            Assert.AreEqual(5, result.JumpTarget);
        }

        /// <summary>
        /// 自动 / 跳过都不能替玩家做选择 —— 选项是分歧点，替他选等于把剧情走向也定了。
        /// 这一条和绝大多数 AVG 一致：自动播放到选项就停下来等人。
        ///
        /// <b>快进也在内</b>：快进是"加速播放"，不是读档 —— 读档走的是
        /// <see cref="EDramaPlaybackMode.Restoring"/>，见上面那两条。
        /// </summary>
        [TestCase(EDramaPlaybackMode.Auto)]
        [TestCase(EDramaPlaybackMode.Skip)]
        [TestCase(EDramaPlaybackMode.FastForward)]
        public void 选项_自动和跳过模式下依然等玩家选(EDramaPlaybackMode mode)
        {
            m_S.Mode = mode;
            m_S.Choice.HoldUntilPicked = true;
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
            Assert.IsFalse(awaiter.IsCompleted, $"{mode} 模式下也必须停下来等玩家选");

            m_S.Choice.PickLatch.Open();

            Assert.IsTrue(awaiter.IsCompleted);
            Assert.AreEqual(9, awaiter.GetResult().JumpTarget, "选完要走玩家选的那条支线");
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

        // ============================================================ 游戏场景

        [Test]
        public void 游戏场景_转交给业务层()
        {
            RunToEnd(new ChangeGameSceneActionHandler(),
                     new ChangeGameSceneAction { MapSceneId = 10, MinSceneId = 20 });

            CollectionAssert.AreEqual(new[] { (10L, 20L) }, m_S.Game.SceneChanges);
        }

        [Test]
        public void 游戏场景_只填小场景也照切()
        {
            // 大场景留空 = 留在当前大场景里换小场景，是合法用法
            RunToEnd(new ChangeGameSceneActionHandler(),
                     new ChangeGameSceneAction { MapSceneId = -1, MinSceneId = 20 });

            CollectionAssert.AreEqual(new[] { (-1L, 20L) }, m_S.Game.SceneChanges);
        }

        [Test]
        public void 游戏场景_两个ID都没填时跳过()
        {
            LogAssert_ExpectWarning();
            RunToEnd(new ChangeGameSceneActionHandler(),
                     new ChangeGameSceneAction { MapSceneId = -1, MinSceneId = -1 });

            CollectionAssert.IsEmpty(m_S.Game.SceneChanges);
        }

        // ============================================================ UI结束

        [Test]
        public void UI结束_把界面名报给宿主而不是当场打开()
        {
            RunToEnd(new EndUIDramaActionHandler(), new EndUIDramaAction { UiPage = "MainUI" });
            CollectionAssert.AreEqual(new[] { "MainUI" }, m_S.Game.RequestedEndUIs);
        }

        [Test]
        public void UI结束_没填界面名时等同于普通结束()
        {
            RunToEnd(new EndUIDramaActionHandler(), new EndUIDramaAction { UiPage = "" });
            CollectionAssert.IsEmpty(m_S.Game.RequestedEndUIs);
        }

        // ============================================================ 功能开放 / 临时显隐

        [Test]
        public void 解锁系统功能_枚举整数值原样交给宿主()
        {
            RunToEnd(new UnlockSystemFunctionActionHandler(),
                     new UnlockSystemFunctionAction { FunctionId = 3 });

            CollectionAssert.AreEqual(new[] { 3 }, m_S.Game.UnlockedSystemFunctions);
        }

        [Test]
        public void 解锁系统功能_没填时什么都不做()
        {
            LogAssert_ExpectWarning();
            RunToEnd(new UnlockSystemFunctionActionHandler(),
                     new UnlockSystemFunctionAction { FunctionId = -1 });

            CollectionAssert.IsEmpty(m_S.Game.UnlockedSystemFunctions);
        }

        [Test]
        public void 解锁系统功能_读档恢复期间照常执行()
        {
            // 解锁是幂等的集合写入，重放一遍结果一样 —— 和发奖励那种"重放就白拿"的指令相反
            m_S.Mode = EDramaPlaybackMode.Restoring;
            RunToEnd(new UnlockSystemFunctionActionHandler(),
                     new UnlockSystemFunctionAction { FunctionId = 0 });

            CollectionAssert.AreEqual(new[] { 0 }, m_S.Game.UnlockedSystemFunctions,
                                      "0 是合法的功能值（本作是地图），不能被当成没填");
        }

        [Test]
        public void 解锁角色功能_角色和功能一起交出去()
        {
            RunToEnd(new UnlockCharacterFunctionActionHandler(),
                     new UnlockCharacterFunctionAction { CharacterId = 1001, FunctionFlag = 8 });

            CollectionAssert.AreEqual(new[] { (1001L, 8) }, m_S.Game.UnlockedCharacterFunctions);
        }

        [Test]
        public void 解锁角色功能_缺角色ID时什么都不做()
        {
            LogAssert_ExpectWarning();
            RunToEnd(new UnlockCharacterFunctionActionHandler(),
                     new UnlockCharacterFunctionAction { CharacterId = -1, FunctionFlag = 8 });

            CollectionAssert.IsEmpty(m_S.Game.UnlockedCharacterFunctions);
        }

        [Test]
        public void 解锁角色功能_缺功能时什么都不做()
        {
            // 宿主那个枚举是 [Flags]，0 是"无任何功能"，解锁它没有意义
            LogAssert_ExpectWarning();
            RunToEnd(new UnlockCharacterFunctionActionHandler(),
                     new UnlockCharacterFunctionAction { CharacterId = 1001, FunctionFlag = 0 });

            CollectionAssert.IsEmpty(m_S.Game.UnlockedCharacterFunctions);
        }

        [Test]
        public void 解锁地图_小地图填负一表示大地图入口本身()
        {
            RunToEnd(new UnlockMapActionHandler(),
                     new UnlockMapAction { MapSceneId = 10000, SubSceneId = -1 });

            CollectionAssert.AreEqual(new[] { (10000L, -1L) }, m_S.Game.UnlockedMaps,
                                      "-1 是合法值，不能被当成没填过滤掉");
        }

        [Test]
        public void 解锁地图_没填大地图ID时什么都不做()
        {
            LogAssert_ExpectWarning();
            RunToEnd(new UnlockMapActionHandler(),
                     new UnlockMapAction { MapSceneId = -1, SubSceneId = 20001 });

            CollectionAssert.IsEmpty(m_S.Game.UnlockedMaps);
        }

        [Test]
        public void 系统功能显隐_显示和隐藏都传得过去()
        {
            RunToEnd(new SystemFunctionVisibilityActionHandler(),
                     new SystemFunctionVisibilityAction { FunctionId = 1, Visible = false });
            RunToEnd(new SystemFunctionVisibilityActionHandler(),
                     new SystemFunctionVisibilityAction { FunctionId = 1, Visible = true });

            CollectionAssert.AreEqual(new[] { (1, false), (1, true) },
                                      m_S.Game.SystemFunctionVisibilities);
        }

        [Test]
        public void 系统功能显隐_读档恢复期间照常执行()
        {
            // ★ 显隐意图不进存档，正是靠静默重放恢复的 ——
            //   要是拦掉，读档之后引导藏起来的按钮会全冒出来
            m_S.Mode = EDramaPlaybackMode.Restoring;
            RunToEnd(new SystemFunctionVisibilityActionHandler(),
                     new SystemFunctionVisibilityAction { FunctionId = 2, Visible = false });

            CollectionAssert.AreEqual(new[] { (2, false) }, m_S.Game.SystemFunctionVisibilities);
        }

        [Test]
        public void 角色功能显隐_三个参数都传得过去()
        {
            RunToEnd(new CharacterFunctionVisibilityActionHandler(),
                     new CharacterFunctionVisibilityAction
                     {
                         CharacterId = 1001, FunctionFlag = 16, Visible = false,
                     });

            CollectionAssert.AreEqual(new[] { (1001L, 16, false) },
                                      m_S.Game.CharacterFunctionVisibilities);
        }

        [Test]
        public void 角色功能显隐_缺参数时什么都不做()
        {
            LogAssert_ExpectWarning();
            RunToEnd(new CharacterFunctionVisibilityActionHandler(),
                     new CharacterFunctionVisibilityAction { CharacterId = 0, FunctionFlag = 16 });

            CollectionAssert.IsEmpty(m_S.Game.CharacterFunctionVisibilities);
        }

        [Test]
        public void 地图显隐_读档恢复期间照常执行()
        {
            m_S.Mode = EDramaPlaybackMode.Restoring;
            RunToEnd(new MapVisibilityActionHandler(),
                     new MapVisibilityAction { MapSceneId = 10000, SubSceneId = 20001, Visible = false });

            CollectionAssert.AreEqual(new[] { (10000L, 20001L, false) }, m_S.Game.MapVisibilities);
        }

        [Test]
        public void 地图显隐_没填大地图ID时什么都不做()
        {
            LogAssert_ExpectWarning();
            RunToEnd(new MapVisibilityActionHandler(),
                     new MapVisibilityAction { MapSceneId = 0, SubSceneId = -1 });

            CollectionAssert.IsEmpty(m_S.Game.MapVisibilities);
        }

        static void LogAssert_ExpectWarning() =>
            UnityEngine.TestTools.LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(".*"));
    }
}

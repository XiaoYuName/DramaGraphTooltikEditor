using Drama.Runtime;
using Unity.GraphToolkit.Editor;
using UnityEngine;

namespace Drama.Editor.Export
{
    /// <summary>
    /// 编辑器节点 → 运行时指令的映射层。
    ///
    /// 所有转换集中在这一个文件，而不是散落到各个节点类里 ——
    /// 编辑器类型和运行时类型是两套独立的东西，把"翻译"放在中间层，
    /// 两边都能各自演进，改映射也只用看这一个文件。
    ///
    /// <b>加新节点</b>：在 <see cref="TryExport"/> 的 switch 里加一个 case 即可。
    /// 节点也可以自己实现 <see cref="IDramaExportNode"/> 接管导出，那样优先级更高。
    ///
    /// 单位约定：编辑器里存什么单位，这里就转成运行时的真实单位（毫秒 → 秒）。
    /// DOTween 的 Ease / LoopType 是两边共用的类型，直接原样传，不做映射。
    /// </summary>
    internal static class DramaNodeExporters
    {
        /// <summary>把一个节点翻译成 0..N 条运行时指令。返回是否处理了。</summary>
        internal static bool TryExport(INode node, DramaExportContext ctx)
        {
            // 节点自己实现了导出就交给它
            if (node is IDramaExportNode self) { self.Export(ctx); return true; }

            switch (node)
            {
                // ------------------------------------------------ 对话
                case TalkNode talk:            ExportTalk(talk, ctx); return true;
                case TalkShowNode n:           ExportTalkShow(n, ctx); return true;
                case SetTalkFrameNode n:       ExportTalkFrame(n, ctx); return true;

                // ------------------------------------------------ 流程
                case WaitNode n:               ExportWait(n, ctx); return true;
                case WaitInputNode n:          ctx.Emit(new WaitInputAction()); return true;
                case GotoDramaNode n:          ExportGoto(n, ctx); return true;
                case ReceiveTask n:            ExportReceiveTask(n, ctx); return true;
                case ReceiveRewardNode n:      ExportReceiveReward(n, ctx); return true;
                case ShowUINode n:             ExportShowUI(n, ctx); return true;
                case ChangeDramaNode n:        ExportChoice(n, ctx); return true;
                case ChangeGameScreenNode n:   ExportChangeGameScene(n, ctx); return true;
                case SceneVisibilityNode n:    ExportSceneVisibility(n, ctx); return true;
                case EndUIDramaNode n:         ExportEndUI(n, ctx); return true;
                case EndGuideDramaNode n:      ExportEndGuide(n, ctx); return true;

                // ------------------------------------------------ 立绘
                // 三种立绘在图里是三个节点，到数据层收敛成同一条指令 + 一个类型字段
                case ActorShowNode n:          ExportActorShow(n, ctx, EActorAssetKind.Spine); return true;
                case ActorTextureShow n:       ExportActorShow(n, ctx, EActorAssetKind.Texture); return true;
                case ActorLive2DShow n:        ExportActorShow(n, ctx, EActorAssetKind.Live2D); return true;
                case ActorPlayAnimNode n: ExportActorTextureAnim(n, ctx); return true;
                case ActorAnimSetBoolNode n:    ExportActorAnimBool(n, ctx); return true;
                case ActorAnimSetIntNode n:     ExportActorAnimInt(n, ctx); return true;
                case ActorAnimSetFloatNode n:   ExportActorAnimFloat(n, ctx); return true;
                case ActorAnimSetTriggerNode n: ExportActorAnimTrigger(n, ctx); return true;
                case ActorTransformNode n:     ExportActorTransform(n, ctx); return true;
                case ActorSetSkin n:           ExportActorSkin(n, ctx); return true;
                case ActorPlayAnimationNode n: ExportActorAnim(n, ctx); return true;
                case ActorSetGraySwitchNode n: ExportActorHighlight(n, ctx); return true;
                case ActorOffsetMoveNode n:    ExportActorOffsetMove(n, ctx); return true;
                case ActorOffsetShakeNode n:   ExportActorShake(n, ctx); return true;

                // ------------------------------------------------ CG
                case CGShowNode n:             ExportCGShow(n, ctx); return true;
                case CGHideNode n:             ExportCGHide(n, ctx); return true;
                case CGTransformNode n:        ExportCGTransform(n, ctx); return true;
                case CGShakeNode n:            ExportCGShake(n, ctx); return true;
                case CGOffsetMoveNode n:       ExportCGOffsetMove(n, ctx); return true;
                case CGAnimSetBoolNode n:      ExportCGAnimBool(n, ctx); return true;
                case CGAnimSetIntNode n:       ExportCGAnimInt(n, ctx); return true;
                case CGAnimSetFloatNode n:     ExportCGAnimFloat(n, ctx); return true;
                case CGAnimSetTriggerNode n:   ExportCGAnimTrigger(n, ctx); return true;

                // ------------------------------------------------ 场景 / 音频
                case ScreenEffNode n:          ExportScreenEff(n, ctx); return true;
                case ScreenTransformNode n:    ExportScreenTransform(n, ctx); return true;
                case ChangeBgPicNode n:        ExportChangeBg(n, ctx); return true;
                case SetMusicNode n:           ExportMusic(n, ctx); return true;

                // ------------------------------------------------ 不产出指令的
                case StartDramaNode _:         return true;   // 入口，只是个锚点
                case LocalizationNode _:       return true;   // 值提供者，被别人求值
                case LocalizationAudioNode _:  return true;

                // 结束不是一条指令，是"Next 为空"这个状态。
                // 本节点没有输出流程端口 → ResolveTargets 返回空 → 上游的 Next 里没有它
                // → Next.Length == 0 → 执行流到此为止。所以这里什么都不用产出。
                case EndDramaNode _:           return true;

                default:
                    return false;
            }
        }

        // ==========================================================  对话

        static void ExportTalk(TalkNode talk, DramaExportContext ctx)
        {
            if (talk.BlockCount == 0)
            {
                ctx.Warn("对话节点里没有台词块，不会产出台词", talk);
                return;
            }

            // 整段共用的参数，每条台词都用同一份
            var speaker = ctx.Option(talk, TalkNode.ExportSpeakerOption, ETalkSpeaker.Aside);
            var balloon = MapBalloon(ctx.Port(talk, TalkNode.ExportBallonPort, EBallonKind.Normal));
            // 编辑器里是毫秒，运行时统一用秒
            var autoWait = ctx.Port(talk, TalkNode.ExportWaitMsPort, 0f) / 1000f;
            var nameColor = ctx.Port(talk, TalkNode.ExportNameColorPort, Color.white);

            var speakerName = speaker == ETalkSpeaker.Unknown
                ? ctx.EvalLocalized(talk.GetInputPortByName(TalkNode.ExportSpeakerNamePort))
                : default;

            var actorId = speaker == ETalkSpeaker.ActorSlot
                ? ctx.Port(talk, TalkNode.ExportActorSlotPort, 0)
                : 0;

            foreach (var block in talk.BlockNodes)
            {
                if (!(block is TalkTextBlock line))
                {
                    ctx.Warn($"对话节点里有不认识的块「{block.GetType().Name}」，已跳过", talk);
                    continue;
                }

                var action = new TalkAction
                {
                    Text            = ctx.EvalLocalized(line.GetInputPortByName(TalkTextBlock.portText)),
                    Voice           = ctx.EvalLocalized(line.GetInputPortByName(TalkTextBlock.portVoice)),
                    Speaker         = MapSpeaker(speaker),
                    SpeakerName     = speakerName,
                    ActorId         = actorId,
                    Balloon         = balloon,
                    AutoWaitSeconds = autoWait,
                    NameColor       = nameColor,
                };

                if (action.Text.IsEmpty)
                    ctx.Warn($"第 {line.Index + 1} 句台词没绑定文本", talk);

                ctx.Emit(action);
            }
        }

        static void ExportTalkShow(TalkShowNode n, DramaExportContext ctx) =>
            ctx.Emit(new TalkShowAction { Show = ctx.Port(n, TalkShowNode.ShowNodeName, true) });

        static void ExportTalkFrame(SetTalkFrameNode n, DramaExportContext ctx) =>
            ctx.Emit(new SetTalkFrameAction
            {
                Frame = MapTalkFrame(ctx.Port(n, SetTalkFrameNode.TalkFarme, TalkFrame.Normal))
            });

        // ==========================================================  流程

        static void ExportWait(WaitNode n, DramaExportContext ctx)
        {
            var seconds = ctx.Port(n, WaitNode.WaitNodeName, 0f);
            if (seconds <= 0f) ctx.Warn("等待时长为 0，这个节点没有实际效果", n);
            ctx.Emit(new WaitAction { Seconds = seconds });
        }

        static void ExportGoto(GotoDramaNode n, DramaExportContext ctx) =>
            ctx.Emit(new GotoDramaAction { DramaId = ctx.Port(n, StartDramaNode.DramaID, -1L) });

        static void ExportReceiveTask(ReceiveTask n, DramaExportContext ctx)
        {
            var id = ctx.Port(n, ReceiveTask.TaskID, -1L);
            if (id <= 0) ctx.Warn("任务ID 没填", n);
            ctx.Emit(new ReceiveTaskAction { TaskId = id });
        }

        /// <summary>获取奖励。手动模式会等玩家关掉弹窗，自动 / 跳过不等。</summary>
        static void ExportReceiveReward(ReceiveRewardNode n, DramaExportContext ctx)
        {
            var id = ctx.Port(n, ReceiveRewardNode.RewardID, -1L);
            if (id <= 0) ctx.Warn("奖励表ID 没填，运行时会跳过这条", n);
            ctx.Emit(new ReceiveRewardAction { RewardId = id });
        }

        /// <summary>打开界面。手动模式会等玩家关掉它，自动 / 跳过不等。</summary>
        static void ExportShowUI(ShowUINode n, DramaExportContext ctx)
        {
            var uiPage = ctx.Port(n, ShowUINode.UIPageID, string.Empty);
            if (string.IsNullOrEmpty(uiPage)) ctx.Warn("界面ID 没填，运行时会跳过这条", n);
            ctx.Emit(new ShowUIAction { UiPage = uiPage });
        }

        /// <summary>
        /// 游戏场景。切的是宿主真实的游戏场景，不是剧情背景图（那是「切换背景」节点）。
        ///
        /// 大场景留空（-1）是合法的，表示"留在当前大场景里只换小场景"；
        /// 但两个都空就没有任何意义了，拦下来。
        /// </summary>
        static void ExportChangeGameScene(ChangeGameScreenNode n, DramaExportContext ctx)
        {
            var mapSceneId = ctx.Port(n, ChangeGameScreenNode.MapSceneID, -1L);
            var minSceneId = ctx.Port(n, ChangeGameScreenNode.MinSceneID, -1L);

            if (mapSceneId <= 0 && minSceneId <= 0)
                ctx.Warn("游戏场景节点两个 ID 都没填，运行时会跳过这条", n);

            ctx.Emit(new ChangeGameSceneAction
            {
                MapSceneId = mapSceneId,
                MinSceneId = minSceneId,
            });
        }

        /// <summary>
        /// UI结束。和「结束」节点一样是终端（没有输出流程口 → Next 为空 → 流程到此为止），
        /// 区别是它会额外报一个"结束后打开哪个界面"。
        /// </summary>
        static void ExportEndUI(EndUIDramaNode n, DramaExportContext ctx)
        {
            var uiPage = ctx.Port(n, EndUIDramaNode.uiPageName, string.Empty);

            if (string.IsNullOrEmpty(uiPage))
                ctx.Warn("UI结束节点没填界面名，等同于普通「结束」", n);

            ctx.Emit(new EndUIDramaAction { UiPage = uiPage });
        }

        /// <summary>引导结束。和「UI结束」同一个路子，只是打开的东西是引导。</summary>
        static void ExportEndGuide(EndGuideDramaNode n, DramaExportContext ctx)
        {
            var guideId = ctx.Port(n, EndGuideDramaNode.GuideID, -1L);

            if (guideId <= 0)
                ctx.Warn("引导结束节点没填引导ID，等同于普通「结束」", n);

            ctx.Emit(new EndGuideDramaAction { GuideId = guideId });
        }

        /// <summary>
        /// 场景显隐。改的是持续状态，切场景也保持 ——
        /// 所以想让新场景露出 NPC，本节点要摆在「游戏场景」节点<b>前面</b>。
        /// </summary>
        static void ExportSceneVisibility(SceneVisibilityNode n, DramaExportContext ctx)
        {
            ctx.Emit(new SceneVisibilityAction
            {
                ShowNpc = ctx.Port(n, SceneVisibilityNode.ShowNpc, true),
                ShowSceneUI = ctx.Port(n, SceneVisibilityNode.ShowSceneUI, true),
            });
        }

        /// <summary>
        /// 分支节点。这里只产出带选项文字的 ChoiceAction，
        /// 每个选项的跳转目标由 <see cref="DramaExporter"/> 在连线阶段回填。
        /// </summary>
        static void ExportChoice(ChangeDramaNode n, DramaExportContext ctx)
        {
            var count = ctx.Option(n, ChangeDramaNode.OptionNumber, 0);
            if (count <= 0) { ctx.Error("分支节点的选项数为 0", n); return; }

            var options = new ChoiceAction.Option[count];
            for (int i = 0; i < count; i++)
            {
                options[i] = new ChoiceAction.Option
                {
                    Text = ctx.EvalLocalized(n.GetInputPortByName(ChangeDramaNode.OptionName + i)),
                    Next = -1,   // 连线阶段回填
                };
                if (options[i].Text.IsEmpty)
                    ctx.Warn($"分支 {i} 没有选项文字", n);
            }

            ctx.Emit(new ChoiceAction { Options = options });
        }

        // ==========================================================  立绘

        static int ActorId(INode n, DramaExportContext ctx) =>
            CheckActorId((int)ctx.Port(n, ActorDramaNode.ActorIDName, -1L), n, ctx);

        static int ActorIdOfContext(INode n, DramaExportContext ctx) =>
            CheckActorId((int)ctx.Port(n, ActorContextNode.ActorIDName, -1L), n, ctx);

        /// <summary>
        /// 角色ID 没配 / 没连上的话在这里喊一声。
        ///
        /// 运行时对这种情况是<b>静默</b>的（<c>Find(-1)</c> 返回 null，指令直接跳过），
        /// 现象是"立绘一动不动"，从表现上根本看不出是数据的问题——
        /// 所以必须在导出这一步就拦下来。
        /// </summary>
        static int CheckActorId(int actorId, INode n, DramaExportContext ctx)
        {
            if (actorId <= 0)
                ctx.Warn($"角色ID 是 {actorId}，这条指令运行时会被跳过（端口没填值，或者没从上游立绘节点连过来）", n);

            return actorId;
        }

        /// <summary>
        /// 立绘出现。三种立绘（骨骼 / 图片 / Live2D）共用这一个方法 ——
        /// 它们的参数定义在编辑器侧就是同一个基类（<see cref="ActorShowNodeBase"/>），
        /// 只有资源类型不同，由调用方按节点类型传进来。
        /// </summary>
        static void ExportActorShow(ActorShowNodeBase n, DramaExportContext ctx, EActorAssetKind assetKind)
        {
            var kind = ctx.Option(n, ActorShowNodeBase.k_ShowKind, EActorShowKind.FadeIn);
            var animated = kind == EActorShowKind.FadeIn || kind == EActorShowKind.FadeOut;

            ctx.Emit(new ActorShowAction
            {
                ActorId           = ctx.Option(n, ActorShowNodeBase.k_CharId, 1),
                AssetKind         = assetKind,
                ShowKind          = MapShowKind(kind),
                Direction         = MapShowDirection(ctx.Option(n, ActorShowNodeBase.k_ShowDirection, EActorShowDirection.Left)),
                Position          = ctx.Port(n, ActorShowNodeBase.k_Pos, Vector2.zero),
                // 倍率，不是百分比 —— 和「立绘缩放」节点保持同一个口径
                Scale             = ctx.Port(n, ActorShowNodeBase.k_Scale, Vector2.one),
                // 时长端口只在带动画时存在；编辑器里是毫秒
                DurationSeconds   = animated ? ctx.Port(n, ActorShowNodeBase.k_Duration, 600f) / 1000f : 0f,
                Ease            = ctx.Option(n, ActorShowNodeBase.k_Ease, DG.Tweening.Ease.Linear),
                // 没有"等不等动画"这个开关：想让动画和后面并行，图里连成并行分支即可
            });
        }

        /// <summary>
        /// 序列动画 / Animator 动画。<b>复用 Spine 那条指令</b> ——
        /// <c>IActorView.PlayAnimationAsync</c> 本来就不认识 Spine，
        /// "动画名"具体是骨骼动画、序列帧还是 Animator 的状态名，由台上那个立绘自己解释。
        ///
        /// <b>刻意不给这个节点「循环」端口</b>，导出恒为 false：
        ///   ① <c>Animator.Play</c> 运行时没有 loop 参数，循环与否是 clip 的导入设置，
        ///      给策划一个运行时执行不了的开关，是在 UI 上撒谎；
        ///   ② Loop 在这套代码里兼着"要不要等它播完"的职责，而本工程的原则是
        ///      "一律等动画跑完，想并行就在图里连并行分支"（见 ActorShowActionHandler 的注释）。
        /// 所以循环与否由实现方运行时自己判断 —— Animator 读 <c>stateInfo.loop</c>，
        /// 循环就立刻返回，单次就等它播完。策划什么都不用填，也就不可能填错。
        ///
        /// Spine 那个节点上的「循环」端口保留：<c>SetAnimation(track, name, loop)</c>
        /// 的 loop 是真的运行时参数，能生效。
        /// </summary>
        static void ExportActorTextureAnim(ActorPlayAnimNode n, DramaExportContext ctx)
        {
            var name = ctx.Port(n, ActorPlayAnimNode.AnimationName, string.Empty);
            if (string.IsNullOrEmpty(name)) ctx.Warn("序列动画没填动画名", n);

            ctx.Emit(new ActorPlayAnimationAction
            {
                ActorId       = ActorId(n, ctx),
                AnimationName = name,
                TrackIndex    = ctx.Port(n, ActorPlayAnimNode.TrackIndex, 0),
                Loop          = false,
                TimeScale     = ctx.Port(n, ActorPlayAnimNode.TimeScale, 1f),
            });
        }

        // ---- Animator 参数（Live2D 的表情 / 动作靠它驱动）

        static string AnimParameterName(ActorAnimParameterNode n, DramaExportContext ctx)
        {
            var name = ctx.Port(n, ActorAnimParameterNode.ParameterName, string.Empty);
            if (string.IsNullOrEmpty(name)) ctx.Warn("Animator 参数名没填，运行时会被跳过", n);
            return name;
        }

        static void ExportActorAnimBool(ActorAnimSetBoolNode n, DramaExportContext ctx) =>
            ctx.Emit(new ActorAnimBoolAction
            {
                ActorId       = ActorId(n, ctx),
                ParameterName = AnimParameterName(n, ctx),
                Value         = ctx.Port(n, ActorAnimSetBoolNode.Value, true),
            });

        static void ExportActorAnimInt(ActorAnimSetIntNode n, DramaExportContext ctx) =>
            ctx.Emit(new ActorAnimIntAction
            {
                ActorId       = ActorId(n, ctx),
                ParameterName = AnimParameterName(n, ctx),
                Value         = ctx.Port(n, ActorAnimSetIntNode.Value, 0),
            });

        static void ExportActorAnimFloat(ActorAnimSetFloatNode n, DramaExportContext ctx) =>
            ctx.Emit(new ActorAnimFloatAction
            {
                ActorId       = ActorId(n, ctx),
                ParameterName = AnimParameterName(n, ctx),
                Value         = ctx.Port(n, ActorAnimSetFloatNode.Value, 0f),
            });

        static void ExportActorAnimTrigger(ActorAnimSetTriggerNode n, DramaExportContext ctx) =>
            ctx.Emit(new ActorAnimTriggerAction
            {
                ActorId       = ActorId(n, ctx),
                ParameterName = AnimParameterName(n, ctx),
                Reset         = ctx.Option(n, ActorAnimSetTriggerNode.k_Mode, EAnimTriggerMode.Set) == EAnimTriggerMode.Reset,
            });

        /// <summary>
        /// 立绘变换。位置 / 旋转 / 缩放三种块共用一个容器，一个块产出一条指令。
        ///
        /// <b>块 → 指令的映射写在各自容器的导出器里，不要抽成公用方法。</b>
        /// 同一个 <see cref="PositionBlockNode"/> 放在立绘容器下产出 <c>ActorMoveAction</c>，
        /// 放在场景容器下将来产出的是另一条指令 —— 是"容器"决定语义，不是"块"。
        /// 将来某种块只允许出现在其中一个容器里，也是靠这里的 default 分支挡住。
        /// </summary>
        static void ExportActorTransform(ActorTransformNode n, DramaExportContext ctx)
        {
            var actorId = ActorIdOfContext(n, ctx);
            if (n.BlockCount == 0) { ctx.Warn("立绘变换节点里没有变换块", n); return; }

            foreach (var block in n.BlockNodes)
            {
                switch (block)
                {
                    case PositionBlockNode p:
                        ctx.Emit(new ActorMoveAction
                        {
                            ActorId         = actorId,
                            Position        = ctx.Port(p, PositionBlockNode.ActorPositionName, Vector2.zero),
                            DurationSeconds = ctx.Port(p, PositionBlockNode.Duration, 0f),
                            Ease            = ctx.Port(p, PositionBlockNode.ease, DG.Tweening.Ease.Linear),
                        });
                        break;

                    case RotationBlockNode r:
                        ctx.Emit(new ActorRotateAction
                        {
                            ActorId         = actorId,
                            Rotation        = ctx.Port(r, RotationBlockNode.ActorRotationName, Vector3.zero),
                            DurationSeconds = ctx.Port(r, RotationBlockNode.Duration, 0f),
                            Ease            = ctx.Port(r, RotationBlockNode.ease, DG.Tweening.Ease.Linear),
                        });
                        break;

                    case ScaleBlockNode s:
                        ctx.Emit(new ActorScaleAction
                        {
                            ActorId         = actorId,
                            Scale           = ctx.Port(s, ScaleBlockNode.ActorScaleName, Vector3.one),
                            DurationSeconds = ctx.Port(s, ScaleBlockNode.Duration, 0f),
                            Ease            = ctx.Port(s, ScaleBlockNode.ease, DG.Tweening.Ease.Linear),
                        });
                        break;

                    default:
                        ctx.Warn($"立绘变换节点不支持「{block.GetType().Name}」块，已跳过", n);
                        break;
                }
            }
        }

        /// <summary>
        /// 背景变化（背景的位移 / 旋转 / 缩放，做镜头感用）。
        ///
        /// 块类型和 <see cref="ExportActorTransform"/> 是同一批，但产出的是背景那套指令 ——
        /// 再次说明"是容器决定语义，不是块"。
        ///
        /// 注意这里<b>不</b>负责换图，换图是「切换背景」节点（<see cref="ChangeBgPicNode"/>）的事。
        /// </summary>
        static void ExportScreenTransform(ScreenTransformNode n, DramaExportContext ctx)
        {
            var bgId = ctx.Port(n, ScreenTransformNode.ScreenID, -1L);
            if (bgId <= 0) ctx.Warn("背景变化节点没填场景ID", n);

            if (n.BlockCount == 0) { ctx.Warn("背景变化节点里没有变换块", n); return; }

            foreach (var block in n.BlockNodes)
            {
                switch (block)
                {
                    case PositionBlockNode p:
                        ctx.Emit(new BackgroundMoveAction
                        {
                            BackgroundId    = bgId,
                            Position        = ctx.Port(p, PositionBlockNode.ActorPositionName, Vector2.zero),
                            DurationSeconds = ctx.Port(p, PositionBlockNode.Duration, 0f),
                            Ease            = ctx.Port(p, PositionBlockNode.ease, DG.Tweening.Ease.Linear),
                        });
                        break;

                    case RotationBlockNode r:
                        ctx.Emit(new BackgroundRotateAction
                        {
                            BackgroundId    = bgId,
                            Rotation        = ctx.Port(r, RotationBlockNode.ActorRotationName, Vector3.zero),
                            DurationSeconds = ctx.Port(r, RotationBlockNode.Duration, 0f),
                            Ease            = ctx.Port(r, RotationBlockNode.ease, DG.Tweening.Ease.Linear),
                        });
                        break;

                    case ScaleBlockNode s:
                        ctx.Emit(new BackgroundScaleAction
                        {
                            BackgroundId    = bgId,
                            Scale           = ctx.Port(s, ScaleBlockNode.ActorScaleName, Vector3.one),
                            DurationSeconds = ctx.Port(s, ScaleBlockNode.Duration, 0f),
                            Ease            = ctx.Port(s, ScaleBlockNode.ease, DG.Tweening.Ease.Linear),
                        });
                        break;

                    default:
                        ctx.Warn($"背景变化节点不支持「{block.GetType().Name}」块，已跳过", n);
                        break;
                }
            }
        }

        static void ExportActorSkin(ActorSetSkin n, DramaExportContext ctx)
        {
            // 注意：SkinName 是实例字段，存的是【端口名】而不是皮肤名
            var skin = ctx.Port(n, n.SkinName, "default");
            if (string.IsNullOrEmpty(skin)) ctx.Warn("皮肤名为空", n);
            ctx.Emit(new ActorSetSkinAction { ActorId = ActorId(n, ctx), SkinName = skin });
        }

        static void ExportActorAnim(ActorPlayAnimationNode n, DramaExportContext ctx)
        {
            var animName = ctx.Port(n, ActorPlayAnimationNode.AnimationName, string.Empty);
            if (string.IsNullOrEmpty(animName))
                ctx.Warn("动画名为空，运行时这条指令会被跳过", n);

            ctx.Emit(new ActorPlayAnimationAction
            {
                ActorId       = ActorId(n, ctx),
                AnimationName = animName,
                TrackIndex    = ctx.Port(n, ActorPlayAnimationNode.TrackIndex, 1),
                Loop          = ctx.Port(n, ActorPlayAnimationNode.isLooping, false),
                TimeScale     = ctx.Port(n, ActorPlayAnimationNode.TimeScale, 1f),
            });
        }

        /// <summary>
        /// 讲话人突出的总开关，不针对角色，所以没有 ActorId。
        ///
        /// 两个开关是 Option，两个强度是<b>动态端口</b>（开关没勾时端口不存在），
        /// 所以强度得走 ctx.Port 的 fallback，取不到就用旧工程写死的那个值。
        /// </summary>
        static void ExportActorHighlight(ActorSetGraySwitchNode n, DramaExportContext ctx)
        {
            var dim = ctx.Option(n, ActorSetGraySwitchNode.IsFade, false);
            var shrink = ctx.Option(n, ActorSetGraySwitchNode.IsGray, true);

            ctx.Emit(new ActorHighlightAction
            {
                Gray           = dim,
                DimBrightness  = dim ? ctx.Port(n, ActorSetGraySwitchNode.DimBrightness, 0.8f) : 1f,
                Shrink         = shrink,
                ShrinkScale    = shrink ? ctx.Port(n, ActorSetGraySwitchNode.ShrinkScale, 0.95f) : 1f,
            });
        }

        static void ExportActorOffsetMove(ActorOffsetMoveNode n, DramaExportContext ctx)
        {
            var actorId = ActorIdOfContext(n, ctx);
            if (n.BlockCount == 0) { ctx.Warn("角色动作节点里没有小动作块", n); return; }

            foreach (var block in n.BlockNodes)
            {
                if (!(block is OffsetMoveNode m))
                {
                    ctx.Warn($"不认识的块「{block.GetType().Name}」，已跳过", n);
                    continue;
                }

                ctx.Emit(new ActorOffsetMoveAction
                {
                    ActorId         = actorId,
                    Offset          = ctx.Port(m, OffsetMoveNode.Offset, Vector3.zero),
                    DurationSeconds = ctx.Port(m, OffsetMoveNode.Duration, 0f),
                    Ease            = ctx.Port(m, OffsetMoveNode.ease, DG.Tweening.Ease.Linear),
                    LoopCount       = ctx.Port(m, OffsetMoveNode.count, 1),
                    LoopType        = ctx.Port(m, OffsetMoveNode.loopType, DG.Tweening.LoopType.Restart),
                });
            }
        }

        static void ExportActorShake(ActorOffsetShakeNode n, DramaExportContext ctx)
        {
            var actorId = ActorIdOfContext(n, ctx);
            if (n.BlockCount == 0) { ctx.Warn("角色抖动节点里没有块", n); return; }

            foreach (var block in n.BlockNodes)
            {
                switch (block)
                {
                    case ShakeNode s:
                        ctx.Emit(new ActorShakeAction
                        {
                            ActorId         = actorId,
                            Amplitude       = ctx.Port(s, ShakeNode.Amplitude, 0.5f),
                            Axis            = MapAxis(ctx.Port(s, ShakeNode.ShakeAxis, Drama.Editor.ShakeAxis.PositionXY)),
                            DurationSeconds = ctx.Port(s, ShakeNode.Duration, 0.3f),
                            RestoreOnEnd    = ctx.Port(s, ShakeNode.RestoreOnEnd, true),
                        });
                        break;

                    case VibrateNode v:
                        ctx.Emit(new ActorVibrateAction
                        {
                            ActorId         = actorId,
                            Amplitude       = ctx.Port(v, VibrateNode.Amplitude, 0.5f),
                            Axis            = MapAxis(ctx.Port(v, VibrateNode.ShakeAxis, Drama.Editor.ShakeAxis.PositionXY)),
                            IntervalSeconds = ctx.Port(v, VibrateNode.Interval, 0.3f),
                            SmoothSpeed     = ctx.Port(v, VibrateNode.SmoothSpeed, 5f),
                            DurationSeconds = ctx.Port(v, VibrateNode.Duration, 0.3f),
                            RestoreOnEnd    = ctx.Port(v, VibrateNode.RestoreOnEnd, true),
                        });
                        break;

                    default:
                        ctx.Warn($"不认识的块「{block.GetType().Name}」，已跳过", n);
                        break;
                }
            }
        }

        // ==========================================================  CG

        // CG 是单槽位，所以这一族全都不带 ID —— 变换 / 抖动 / Animator 说的都是"当前那张"。
        // 三个容器装的是和立绘、背景【同一批】块，又一次说明是容器决定语义、不是块。

        static void ExportCGShow(CGShowNode n, DramaExportContext ctx)
        {
            var cgId = ctx.Option(n, CGShowNode.k_CgId, -1L);
            if (cgId <= 0) ctx.Warn("CG出现没填 CG ID，运行时会跳过这条", n);

            ctx.Emit(new CGShowAction
            {
                CgId            = cgId,
                DurationSeconds = CGDurationSeconds(n, ctx),
                Ease            = ctx.Option(n, CGVisibilityNodeBase.k_Ease, DG.Tweening.Ease.Linear),
            });
        }

        static void ExportCGHide(CGHideNode n, DramaExportContext ctx) =>
            ctx.Emit(new CGHideAction
            {
                DurationSeconds = CGDurationSeconds(n, ctx),
                Ease            = ctx.Option(n, CGVisibilityNodeBase.k_Ease, DG.Tweening.Ease.Linear),
            });

        /// <summary>
        /// CG 显隐的时长。时长端口只在「淡入淡出」时才存在，瞬时方式导出成 0 ——
        /// 运行时就是靠"时长为 0"表达瞬时的，不需要额外的方式字段。编辑器里填的是毫秒。
        /// </summary>
        static float CGDurationSeconds(CGVisibilityNodeBase n, DramaExportContext ctx) =>
            n.IsAnimated() ? ctx.Port(n, CGVisibilityNodeBase.k_Duration, 600f) / 1000f : 0f;

        static void ExportCGTransform(CGTransformNode n, DramaExportContext ctx)
        {
            if (n.BlockCount == 0) { ctx.Warn("CG变换节点里没有变换块", n); return; }

            foreach (var block in n.BlockNodes)
            {
                switch (block)
                {
                    case PositionBlockNode p:
                        ctx.Emit(new CGMoveAction
                        {
                            Position        = ctx.Port(p, PositionBlockNode.ActorPositionName, Vector2.zero),
                            DurationSeconds = ctx.Port(p, PositionBlockNode.Duration, 0f),
                            Ease            = ctx.Port(p, PositionBlockNode.ease, DG.Tweening.Ease.Linear),
                        });
                        break;

                    case RotationBlockNode r:
                        ctx.Emit(new CGRotateAction
                        {
                            Rotation        = ctx.Port(r, RotationBlockNode.ActorRotationName, Vector3.zero),
                            DurationSeconds = ctx.Port(r, RotationBlockNode.Duration, 0f),
                            Ease            = ctx.Port(r, RotationBlockNode.ease, DG.Tweening.Ease.Linear),
                        });
                        break;

                    case ScaleBlockNode s:
                        ctx.Emit(new CGScaleAction
                        {
                            Scale           = ctx.Port(s, ScaleBlockNode.ActorScaleName, Vector3.one),
                            DurationSeconds = ctx.Port(s, ScaleBlockNode.Duration, 0f),
                            Ease            = ctx.Port(s, ScaleBlockNode.ease, DG.Tweening.Ease.Linear),
                        });
                        break;

                    default:
                        ctx.Warn($"CG变换节点不支持「{block.GetType().Name}」块，已跳过", n);
                        break;
                }
            }
        }

        static void ExportCGOffsetMove(CGOffsetMoveNode n, DramaExportContext ctx)
        {
            if (n.BlockCount == 0) { ctx.Warn("CG动作节点里没有小动作块", n); return; }

            foreach (var block in n.BlockNodes)
            {
                if (!(block is OffsetMoveNode m))
                {
                    ctx.Warn($"不认识的块「{block.GetType().Name}」，已跳过", n);
                    continue;
                }

                ctx.Emit(new CGOffsetMoveAction
                {
                    Offset          = ctx.Port(m, OffsetMoveNode.Offset, Vector3.zero),
                    DurationSeconds = ctx.Port(m, OffsetMoveNode.Duration, 0f),
                    Ease            = ctx.Port(m, OffsetMoveNode.ease, DG.Tweening.Ease.Linear),
                    LoopCount       = ctx.Port(m, OffsetMoveNode.count, 1),
                    LoopType        = ctx.Port(m, OffsetMoveNode.loopType, DG.Tweening.LoopType.Restart),
                });
            }
        }

        static void ExportCGShake(CGShakeNode n, DramaExportContext ctx)
        {
            if (n.BlockCount == 0) { ctx.Warn("CG抖动节点里没有块", n); return; }

            foreach (var block in n.BlockNodes)
            {
                switch (block)
                {
                    case ShakeNode s:
                        ctx.Emit(new CGShakeAction
                        {
                            Amplitude       = ctx.Port(s, ShakeNode.Amplitude, 0.5f),
                            Axis            = MapAxis(ctx.Port(s, ShakeNode.ShakeAxis, Drama.Editor.ShakeAxis.PositionXY)),
                            DurationSeconds = ctx.Port(s, ShakeNode.Duration, 0.3f),
                            RestoreOnEnd    = ctx.Port(s, ShakeNode.RestoreOnEnd, true),
                        });
                        break;

                    case VibrateNode v:
                        ctx.Emit(new CGVibrateAction
                        {
                            Amplitude       = ctx.Port(v, VibrateNode.Amplitude, 0.5f),
                            Axis            = MapAxis(ctx.Port(v, VibrateNode.ShakeAxis, Drama.Editor.ShakeAxis.PositionXY)),
                            IntervalSeconds = ctx.Port(v, VibrateNode.Interval, 0.3f),
                            SmoothSpeed     = ctx.Port(v, VibrateNode.SmoothSpeed, 5f),
                            DurationSeconds = ctx.Port(v, VibrateNode.Duration, 0.3f),
                            RestoreOnEnd    = ctx.Port(v, VibrateNode.RestoreOnEnd, true),
                        });
                        break;

                    default:
                        ctx.Warn($"不认识的块「{block.GetType().Name}」，已跳过", n);
                        break;
                }
            }
        }

        static string CGAnimParameterName(CGAnimParameterNode n, DramaExportContext ctx)
        {
            var name = ctx.Port(n, CGAnimParameterNode.ParameterName, string.Empty);
            if (string.IsNullOrEmpty(name)) ctx.Warn("CG Animator 参数名没填，运行时会被跳过", n);
            return name;
        }

        static void ExportCGAnimBool(CGAnimSetBoolNode n, DramaExportContext ctx) =>
            ctx.Emit(new CGAnimBoolAction
            {
                ParameterName = CGAnimParameterName(n, ctx),
                Value         = ctx.Port(n, CGAnimSetBoolNode.Value, true),
            });

        static void ExportCGAnimInt(CGAnimSetIntNode n, DramaExportContext ctx) =>
            ctx.Emit(new CGAnimIntAction
            {
                ParameterName = CGAnimParameterName(n, ctx),
                Value         = ctx.Port(n, CGAnimSetIntNode.Value, 0),
            });

        static void ExportCGAnimFloat(CGAnimSetFloatNode n, DramaExportContext ctx) =>
            ctx.Emit(new CGAnimFloatAction
            {
                ParameterName = CGAnimParameterName(n, ctx),
                Value         = ctx.Port(n, CGAnimSetFloatNode.Value, 0f),
            });

        static void ExportCGAnimTrigger(CGAnimSetTriggerNode n, DramaExportContext ctx) =>
            ctx.Emit(new CGAnimTriggerAction
            {
                ParameterName = CGAnimParameterName(n, ctx),
                Reset         = ctx.Option(n, CGAnimSetTriggerNode.k_Mode, EAnimTriggerMode.Set) == EAnimTriggerMode.Reset,
            });

        // ==========================================================  场景 / 音频

        static void ExportScreenEff(ScreenEffNode n, DramaExportContext ctx)
        {
            if (n.BlockCount == 0) { ctx.Warn("场景节点里没有转场块", n); return; }

            foreach (var block in n.BlockNodes)
            {
                EScreenTransitionKind kind;
                switch (block)
                {
                    case FadeBlockNode _:      kind = EScreenTransitionKind.Fade; break;
                    case CombBlockNode _:      kind = EScreenTransitionKind.Comb; break;
                    case VenetianBlindNode _:  kind = EScreenTransitionKind.VenetianBlind; break;
                    default:
                        ctx.Warn($"不认识的转场块「{block.GetType().Name}」，已跳过", n);
                        continue;
                }

                // 三种转场块的端口名完全一致，可以共用取值
                var phase = MapPhase(ctx.Option(block, "input_kind", InputKind.InOut));

                ctx.Emit(new ScreenTransitionAction
                {
                    TransitionKind = kind,
                    Phase          = phase,
                    InSeconds      = phase == ETransitionPhase.Out ? 0f : ctx.Port(block, "in_duration", 1f),
                    OutSeconds     = phase == ETransitionPhase.In ? 0f : ctx.Port(block, "out_duration", 1f),
                    Color          = ctx.Port(block, "fadeColor", Color.white),
                    Alpha          = ctx.Port(block, "alpha", 1f),
                    Ease            = ctx.Port(block, "ease", DG.Tweening.Ease.Linear),
                });
            }
        }

        static void ExportChangeBg(ChangeBgPicNode n, DramaExportContext ctx)
        {
            var kind = ctx.Option(n, ChangeBgPicNode.TransitionKind, Drama.Editor.TransitionKind.None);
            var hasTransition = kind != Drama.Editor.TransitionKind.None;

            ctx.Emit(new ChangeBackgroundAction
            {
                BackgroundId = ctx.Port(n, ChangeBgPicNode.BackgroundID, -1L),
                Transition   = MapBgTransition(kind),
                // 淡入淡出端口只在有转场时存在
                InSeconds    = hasTransition ? ctx.Port(n, ChangeBgPicNode.InDuration, 1f) : 0f,
                OutSeconds   = hasTransition ? ctx.Port(n, ChangeBgPicNode.OutDuration, 1f) : 0f,
            });
        }

        static void ExportMusic(SetMusicNode n, DramaExportContext ctx)
        {
            var id = ctx.Port(n, SetMusicNode.MusicID, string.Empty);
            if (string.IsNullOrEmpty(id)) ctx.Warn("音频ID 为空", n);
            ctx.Emit(new PlayMusicAction { MusicId = id });
        }

        // ==========================================================  枚举映射
        //
        //  一律显式 switch，不用 (RuntimeEnum)(int)editorEnum 强转 ——
        //  哪天某一边插了个枚举值，强转会静默错位，switch 至少改起来看得见。

        static ESpeakerKind MapSpeaker(ETalkSpeaker v)
        {
            switch (v)
            {
                case ETalkSpeaker.Aside:     return ESpeakerKind.Aside;
                case ETalkSpeaker.Hero:      return ESpeakerKind.Hero;
                case ETalkSpeaker.Unknown:   return ESpeakerKind.Custom;
                case ETalkSpeaker.ActorSlot: return ESpeakerKind.Actor;
                default:                     return ESpeakerKind.Aside;
            }
        }

        static EBalloonKind MapBalloon(EBallonKind v)
        {
            switch (v)
            {
                case EBallonKind.Shake: return EBalloonKind.Shake;
                case EBallonKind.Shock: return EBalloonKind.Shock;
                default:                return EBalloonKind.Normal;
            }
        }

        static Runtime.EActorShowKind MapShowKind(EActorShowKind v)
        {
            switch (v)
            {
                case EActorShowKind.Show:    return Runtime.EActorShowKind.Show;
                case EActorShowKind.Hide:    return Runtime.EActorShowKind.Hide;
                case EActorShowKind.FadeIn:  return Runtime.EActorShowKind.FadeIn;
                case EActorShowKind.FadeOut: return Runtime.EActorShowKind.FadeOut;
                default:                     return Runtime.EActorShowKind.Show;
            }
        }

        static Runtime.EActorShowDirection MapShowDirection(EActorShowDirection v)
        {
            switch (v)
            {
                case EActorShowDirection.Right:  return Runtime.EActorShowDirection.Right;
                case EActorShowDirection.Center: return Runtime.EActorShowDirection.Center;
                default:                         return Runtime.EActorShowDirection.Left;
            }
        }

        static EShakeAxis MapAxis(Drama.Editor.ShakeAxis v)
        {
            switch (v)
            {
                case Drama.Editor.ShakeAxis.PositionZ: return EShakeAxis.PositionZ;
                case Drama.Editor.ShakeAxis.Rotation:  return EShakeAxis.Rotation;
                default:                               return EShakeAxis.PositionXY;
            }
        }

        static ETransitionPhase MapPhase(InputKind v)
        {
            switch (v)
            {
                case InputKind.In:  return ETransitionPhase.In;
                case InputKind.Out: return ETransitionPhase.Out;
                default:            return ETransitionPhase.InOut;
            }
        }

        static EBgTransitionKind MapBgTransition(Drama.Editor.TransitionKind v)
        {
            switch (v)
            {
                case Drama.Editor.TransitionKind.Fade:          return EBgTransitionKind.Fade;
                case Drama.Editor.TransitionKind.VenetianBlind: return EBgTransitionKind.VenetianBlind;
                case Drama.Editor.TransitionKind.Comb:          return EBgTransitionKind.Comb;
                default:                                        return EBgTransitionKind.None;
            }
        }

        static ETalkFrame MapTalkFrame(TalkFrame v) =>
            v == TalkFrame.HCG ? ETalkFrame.HCG : ETalkFrame.Normal;

    }
}

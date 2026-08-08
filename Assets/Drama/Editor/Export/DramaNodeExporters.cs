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
                case GotoDramaNode n:          ExportGoto(n, ctx); return true;
                case ReceiveTask n:            ExportReceiveTask(n, ctx); return true;
                case ChangeDramaNode n:        ExportChoice(n, ctx); return true;

                // ------------------------------------------------ 立绘
                case ActorShowNode n:          ExportActorShow(n, ctx); return true;
                case ActorPositionNode n:      ExportActorMove(n, ctx); return true;
                case ActorScaleNode n:         ExportActorScale(n, ctx); return true;
                case ActorRotationNode n:      ExportActorRotate(n, ctx); return true;
                case ActorSetSkin n:           ExportActorSkin(n, ctx); return true;
                case ActorPlayAnimationNode n: ExportActorAnim(n, ctx); return true;
                case ActorSetGraySwitchNode n: ExportActorHighlight(n, ctx); return true;
                case ActorOffsetMoveNode n:    ExportActorOffsetMove(n, ctx); return true;
                case ActorOffsetShakeNode n:   ExportActorShake(n, ctx); return true;

                // ------------------------------------------------ 场景 / 音频
                case ScreenEffNode n:          ExportScreenEff(n, ctx); return true;
                case ChangeBgPicNode n:        ExportChangeBg(n, ctx); return true;
                case SetMusicNode n:           ExportMusic(n, ctx); return true;

                // ------------------------------------------------ 不产出指令的
                case StartDramaNode _:         return true;   // 入口，只是个锚点
                case LocalizationNode _:       return true;   // 值提供者，被别人求值
                case LocalizationAudioNode _:  return true;

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
            (int)ctx.Port(n, ActorDramaNode.ActorIDName, -1L);

        static int ActorIdOfContext(INode n, DramaExportContext ctx) =>
            (int)ctx.Port(n, ActorContextNode.ActorIDName, -1L);

        static void ExportActorShow(ActorShowNode n, DramaExportContext ctx)
        {
            var kind = ctx.Option(n, ActorShowNode.k_ShowKind, EActorShowKind.FadeIn);
            var animated = kind == EActorShowKind.FadeIn || kind == EActorShowKind.FadeOut;

            ctx.Emit(new ActorShowAction
            {
                ActorId           = ctx.Option(n, ActorShowNode.k_CharId, 1),
                ShowKind          = MapShowKind(kind),
                Direction         = MapShowDirection(ctx.Option(n, ActorShowNode.k_ShowDirection, EActorShowDirection.Left)),
                Position          = ctx.Port(n, ActorShowNode.k_Pos, Vector2.zero),
                ScalePercent      = ctx.Port(n, ActorShowNode.k_Scale, new Vector2(100f, 100f)),
                // 时长端口只在带动画时存在；编辑器里是毫秒
                DurationSeconds   = animated ? ctx.Port(n, ActorShowNode.k_Duration, 600f) / 1000f : 0f,
                Ease            = ctx.Option(n, ActorShowNode.k_Ease, DG.Tweening.Ease.Linear),
                WaitForCompletion = ctx.Option(n, ActorShowNode.k_Wait, true),
            });
        }

        static void ExportActorMove(ActorPositionNode n, DramaExportContext ctx) =>
            ctx.Emit(new ActorMoveAction
            {
                ActorId         = ActorId(n, ctx),
                Position        = ctx.Port(n, ActorPositionNode.ActorPositionName, Vector2.zero),
                DurationSeconds = ctx.Port(n, ActorPositionNode.Duration, 0f),
                Ease            = ctx.Port(n, ActorPositionNode.ease, DG.Tweening.Ease.Linear),
            });

        static void ExportActorScale(ActorScaleNode n, DramaExportContext ctx) =>
            ctx.Emit(new ActorScaleAction
            {
                ActorId         = ActorId(n, ctx),
                Scale           = ctx.Port(n, ActorScaleNode.ActorScaleName, Vector3.one),
                DurationSeconds = ctx.Port(n, ActorScaleNode.Duration, 0f),
                Ease            = ctx.Port(n, ActorScaleNode.ease, DG.Tweening.Ease.Linear),
            });

        static void ExportActorRotate(ActorRotationNode n, DramaExportContext ctx) =>
            ctx.Emit(new ActorRotateAction
            {
                ActorId         = ActorId(n, ctx),
                Rotation        = ctx.Port(n, ActorRotationNode.ActorRotationName, Vector3.zero),
                DurationSeconds = ctx.Port(n, ActorRotationNode.Duration, 0f),
                Ease            = ctx.Port(n, ActorRotationNode.ease, DG.Tweening.Ease.Linear),
            });

        static void ExportActorSkin(ActorSetSkin n, DramaExportContext ctx)
        {
            // 注意：SkinName 是实例字段，存的是【端口名】而不是皮肤名
            var skin = ctx.Port(n, n.SkinName, "default");
            if (string.IsNullOrEmpty(skin)) ctx.Warn("皮肤名为空", n);
            ctx.Emit(new ActorSetSkinAction { ActorId = ActorId(n, ctx), SkinName = skin });
        }

        static void ExportActorAnim(ActorPlayAnimationNode n, DramaExportContext ctx) =>
            ctx.Emit(new ActorPlayAnimationAction
            {
                ActorId    = ActorId(n, ctx),
                TrackIndex = ctx.Port(n, ActorPlayAnimationNode.TrackIndex, 1),
                Loop       = ctx.Port(n, ActorPlayAnimationNode.isLooping, false),
                TimeScale  = ctx.Port(n, ActorPlayAnimationNode.TimeScale, 1f),
            });

        static void ExportActorHighlight(ActorSetGraySwitchNode n, DramaExportContext ctx) =>
            ctx.Emit(new ActorHighlightAction
            {
                ActorId = ActorId(n, ctx),
                // 端口名和显示名对不上：IsFade 显示的是「置灰」，IsGray 显示的是「微缩」
                Gray   = ctx.Port(n, ActorSetGraySwitchNode.IsFade, false),
                Shrink = ctx.Port(n, ActorSetGraySwitchNode.IsGray, true),
            });

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

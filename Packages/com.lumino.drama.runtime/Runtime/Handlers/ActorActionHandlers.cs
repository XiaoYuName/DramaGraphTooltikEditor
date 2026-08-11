using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Drama.Runtime.Flow;
using UnityEngine;

namespace Drama.Runtime.Handlers
{
    /// <summary>
    /// 立绘出现 / 消失。
    ///
    /// <b>一律等动画跑完才返回。</b> 想让动画和后面的指令并行，在图里把它们连成
    /// 并行分支（<c>Next.Length &gt; 1</c>）—— 那是同一件事的正确表达方式，
    /// 不需要指令自己带一个"不等"的开关。
    /// </summary>
    public sealed class ActorShowActionHandler : DramaSimpleActionHandler<ActorShowAction>
    {
        protected override async UniTask RunAsync(ActorShowAction a, IDramaContext ctx, CancellationToken ct)
        {
            var actor = await ctx.Actors.AcquireAsync(a.ActorId, ct);
            if (actor == null) return;

            // 布局先摆好再放动画，不然淡入过程中会看到位置跳变。
            //
            // 顺序不能反：先定方向（换锚点 / 父节点），再写 Position。
            // Position 是在方向基础上的偏移，反过来就被方向覆盖掉了。
            ctx.Actors.SetDirection(actor, a.Direction);

            // Scale 是倍率不是百分比，和 ActorScaleAction 一个口径，别在这儿再乘 0.01
            actor.Root.localPosition = a.Position;
            actor.Root.localScale = new Vector3(a.Scale.x, a.Scale.y, 1f);

            var visible = a.ShowKind == EActorShowKind.Show || a.ShowKind == EActorShowKind.FadeIn;
            var animated = a.ShowKind == EActorShowKind.FadeIn || a.ShowKind == EActorShowKind.FadeOut;
            var duration = animated ? DramaWait.Scale(a.DurationSeconds, ctx.Mode) : 0f;

            await ctx.Actors.SetVisibleAsync(actor, visible, duration, a.Ease, ct);
        }
    }

    /// <summary>立绘移动。</summary>
    public sealed class ActorMoveActionHandler : DramaSimpleActionHandler<ActorMoveAction>
    {
        protected override UniTask RunAsync(ActorMoveAction a, IDramaContext ctx, CancellationToken ct)
        {
            var actor = ctx.Actors.Find(a.ActorId);
            if (actor == null) return UniTask.CompletedTask;

            var duration = DramaWait.Scale(a.DurationSeconds, ctx.Mode);
            if (duration <= 0f)
            {
                actor.Root.localPosition = a.Position;
                return UniTask.CompletedTask;
            }

            return actor.Root.DOLocalMove(a.Position, duration)
                        .SetEase(a.Ease)
                        .ToUniTask(cancellationToken: ct);
        }
    }

    /// <summary>立绘缩放。</summary>
    public sealed class ActorScaleActionHandler : DramaSimpleActionHandler<ActorScaleAction>
    {
        protected override UniTask RunAsync(ActorScaleAction a, IDramaContext ctx, CancellationToken ct)
        {
            var actor = ctx.Actors.Find(a.ActorId);
            if (actor == null) return UniTask.CompletedTask;

            var duration = DramaWait.Scale(a.DurationSeconds, ctx.Mode);
            if (duration <= 0f)
            {
                actor.Root.localScale = a.Scale;
                return UniTask.CompletedTask;
            }

            return actor.Root.DOScale(a.Scale, duration)
                        .SetEase(a.Ease)
                        .ToUniTask(cancellationToken: ct);
        }
    }

    /// <summary>立绘旋转。</summary>
    public sealed class ActorRotateActionHandler : DramaSimpleActionHandler<ActorRotateAction>
    {
        protected override UniTask RunAsync(ActorRotateAction a, IDramaContext ctx, CancellationToken ct)
        {
            var actor = ctx.Actors.Find(a.ActorId);
            if (actor == null) return UniTask.CompletedTask;

            var duration = DramaWait.Scale(a.DurationSeconds, ctx.Mode);
            if (duration <= 0f)
            {
                actor.Root.localEulerAngles = a.Rotation;
                return UniTask.CompletedTask;
            }

            return actor.Root.DOLocalRotate(a.Rotation, duration)
                        .SetEase(a.Ease)
                        .ToUniTask(cancellationToken: ct);
        }
    }

    /// <summary>换皮肤。</summary>
    public sealed class ActorSetSkinActionHandler : DramaSimpleActionHandler<ActorSetSkinAction>
    {
        protected override UniTask RunAsync(ActorSetSkinAction a, IDramaContext ctx, CancellationToken ct)
        {
            ctx.Actors.Find(a.ActorId)?.SetSkin(a.SkinName);
            return UniTask.CompletedTask;
        }
    }

    /// <summary>播 Spine 动画。</summary>
    public sealed class ActorPlayAnimationActionHandler : DramaSimpleActionHandler<ActorPlayAnimationAction>
    {
        protected override UniTask RunAsync(ActorPlayAnimationAction a, IDramaContext ctx, CancellationToken ct)
        {
            if (string.IsNullOrEmpty(a.AnimationName))
            {
                Debug.LogWarning($"[Drama] #{a.Index} 立绘动画没配动画名，已跳过");
                return UniTask.CompletedTask;
            }

            var actor = ctx.Actors.Find(a.ActorId);
            if (actor == null) return UniTask.CompletedTask;

            // 循环动画不能等它结束（永远等不到），实现方应当立刻返回
            return actor.PlayAnimationAsync(a.AnimationName, a.TrackIndex, a.Loop, a.TimeScale, ct);
        }
    }

    /// <summary>
    /// 立绘小动作：从当前位置相对偏移一段，可循环。
    ///
    /// <b><see cref="ActorOffsetMoveAction.LoopCount"/> 为负数 = 无限循环</b>，
    /// 这种情况下 Handler 立刻返回不等它（否则剧本会永远卡在这一条）。
    /// 这条游离的 Tween 挂在立绘的 Root 上，由 <see cref="Services.IActorStage.CompleteAllTweens"/> 收口。
    /// </summary>
    public sealed class ActorOffsetMoveActionHandler : DramaSimpleActionHandler<ActorOffsetMoveAction>
    {
        protected override UniTask RunAsync(ActorOffsetMoveAction a, IDramaContext ctx, CancellationToken ct)
        {
            var actor = ctx.Actors.Find(a.ActorId);
            if (actor == null) return UniTask.CompletedTask;

            var duration = DramaWait.Scale(a.DurationSeconds, ctx.Mode);
            if (duration <= 0f)
            {
                actor.Root.localPosition += a.Offset;
                return UniTask.CompletedTask;
            }

            var tween = actor.Root.DOLocalMove(a.Offset, duration)
                             .SetRelative(true)             // 相对当前位置
                             .SetEase(a.Ease)
                             .SetLoops(a.LoopCount, a.LoopType);

            if (a.LoopCount < 0)
            {
                tween.ToUniTask(cancellationToken: ct).SuppressCancellationThrow().Forget();
                return UniTask.CompletedTask;
            }

            return tween.ToUniTask(cancellationToken: ct);
        }
    }

    /// <summary>立绘抖动（硬抖）。手感见 <see cref="DramaShake"/>。</summary>
    public sealed class ActorShakeActionHandler : DramaSimpleActionHandler<ActorShakeAction>
    {
        protected override UniTask RunAsync(ActorShakeAction a, IDramaContext ctx, CancellationToken ct)
        {
            var actor = ctx.Actors.Find(a.ActorId);
            if (actor == null) return UniTask.CompletedTask;

            return DramaShake.HardAsync(actor.Root, a.Axis, a.Amplitude,
                                        DramaWait.Scale(a.DurationSeconds, ctx.Mode),
                                        a.RestoreOnEnd, ct);
        }
    }

    /// <summary>立绘震动（柔震）。手感见 <see cref="DramaShake"/>。</summary>
    public sealed class ActorVibrateActionHandler : DramaSimpleActionHandler<ActorVibrateAction>
    {
        protected override UniTask RunAsync(ActorVibrateAction a, IDramaContext ctx, CancellationToken ct)
        {
            var actor = ctx.Actors.Find(a.ActorId);
            if (actor == null) return UniTask.CompletedTask;

            // 间隔不跟着快进缩 —— 缩了就变成另一种频率的动作了，只缩总时长
            return DramaShake.SoftAsync(actor.Root, a.Axis, a.Amplitude, a.IntervalSeconds, a.SmoothSpeed,
                                        DramaWait.Scale(a.DurationSeconds, ctx.Mode),
                                        a.RestoreOnEnd, ct);
        }
    }

    /// <summary>
    /// 讲话人突出的<b>总开关</b>。
    ///
    /// 这条指令不碰任何具体立绘 —— 它只是开关，真正的应用在每条台词上
    /// （<see cref="TalkActionHandler"/> 会调 <see cref="Services.IActorStage.SetSpeaker"/>）。
    /// </summary>
    public sealed class ActorHighlightActionHandler : DramaSimpleActionHandler<ActorHighlightAction>
    {
        protected override UniTask RunAsync(ActorHighlightAction a, IDramaContext ctx, CancellationToken ct)
        {
            ctx.Actors?.SetHighlightMode(
                new Services.ActorHighlightSettings(a.Gray, a.DimBrightness, a.Shrink, a.ShrinkScale));

            return UniTask.CompletedTask;
        }
    }
}

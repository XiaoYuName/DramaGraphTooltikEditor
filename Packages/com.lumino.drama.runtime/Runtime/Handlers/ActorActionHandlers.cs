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
    /// <b>唯一需要留神的是 <see cref="ActorShowAction.WaitForCompletion"/> == false：</b>
    /// Handler 立刻返回了，但动画还在跑。这条动画归 IActorStage 收着，
    /// 剧本结束 / 跳转时由 Director 调 CompleteAllTweens() 收口，
    /// 否则会漏到下一段剧情里去。
    /// </summary>
    public sealed class ActorShowActionHandler : DramaSimpleActionHandler<ActorShowAction>
    {
        protected override async UniTask RunAsync(ActorShowAction a, IDramaContext ctx, CancellationToken ct)
        {
            var actor = await ctx.Actors.AcquireAsync(a.ActorId, ct);
            if (actor == null) return;

            // 布局先摆好再放动画，不然淡入过程中会看到位置跳变
            actor.Root.localPosition = a.Position;
            actor.Root.localScale = new Vector3(a.ScalePercent.x * 0.01f, a.ScalePercent.y * 0.01f, 1f);

            var visible = a.ShowKind == EActorShowKind.Show || a.ShowKind == EActorShowKind.FadeIn;
            var animated = a.ShowKind == EActorShowKind.FadeIn || a.ShowKind == EActorShowKind.FadeOut;
            var duration = animated ? DramaWait.Scale(a.DurationSeconds, ctx.Mode) : 0f;

            var anim = ctx.Actors.SetVisibleAsync(actor, visible, duration, a.Ease, ct);

            if (a.WaitForCompletion) await anim;
            else anim.SuppressCancellationThrow().Forget();
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

    /// <summary>讲话人突出：把非说话人置灰 / 微缩。</summary>
    public sealed class ActorHighlightActionHandler : DramaSimpleActionHandler<ActorHighlightAction>
    {
        protected override UniTask RunAsync(ActorHighlightAction a, IDramaContext ctx, CancellationToken ct)
        {
            var actor = ctx.Actors.Find(a.ActorId);
            if (actor == null) return UniTask.CompletedTask;

            actor.SetGray(a.Gray);
            actor.SetShrink(a.Shrink);
            return UniTask.CompletedTask;
        }
    }
}

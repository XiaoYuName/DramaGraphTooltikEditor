using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Drama.Runtime.Flow;
using UnityEngine;

namespace Drama.Runtime.Handlers
{
    /// <summary>
    /// CG 层的指令。
    ///
    /// <b>结构和立绘那套刻意一致</b>（位置 / 缩放 / 旋转 / 小动作 / 抖动 / 震动），
    /// 区别只有两点：没有 ID（CG 是单槽位），以及目标从 <c>actor.Root</c> 换成 <c>ctx.CG.Root</c>。
    ///
    /// <b>没有 CG 在台上时一律静默跳过</b>：把变换指令写在「CG出现」之前是剧本的顺序问题，
    /// 不该让整段剧情停下来。
    /// </summary>
    public sealed class CGShowActionHandler : DramaSimpleActionHandler<CGShowAction>
    {
        protected override UniTask RunAsync(CGShowAction a, IDramaContext ctx, CancellationToken ct)
        {
            if (a.CgId <= 0)
            {
                Debug.LogWarning($"[Drama] #{a.Index} CG出现没填 CG ID，已跳过");
                return UniTask.CompletedTask;
            }

            return ctx.CG?.ShowAsync(a.CgId, DramaWait.Scale(a.DurationSeconds, ctx.Mode), a.Ease, ct)
                   ?? UniTask.CompletedTask;
        }
    }

    /// <summary>CG 关闭。</summary>
    public sealed class CGHideActionHandler : DramaSimpleActionHandler<CGHideAction>
    {
        protected override UniTask RunAsync(CGHideAction a, IDramaContext ctx, CancellationToken ct)
            => ctx.CG?.HideAsync(DramaWait.Scale(a.DurationSeconds, ctx.Mode), a.Ease, ct)
               ?? UniTask.CompletedTask;
    }

    /// <summary>CG 位移。</summary>
    public sealed class CGMoveActionHandler : DramaSimpleActionHandler<CGMoveAction>
    {
        protected override UniTask RunAsync(CGMoveAction a, IDramaContext ctx, CancellationToken ct)
        {
            Transform root = ctx.CG?.Root;
            if (root == null) return UniTask.CompletedTask;

            var duration = DramaWait.Scale(a.DurationSeconds, ctx.Mode);
            if (duration <= 0f)
            {
                root.localPosition = a.Position;
                return UniTask.CompletedTask;
            }

            return root.DOLocalMove(a.Position, duration).SetEase(a.Ease).ToUniTask(cancellationToken: ct);
        }
    }

    /// <summary>CG 缩放。</summary>
    public sealed class CGScaleActionHandler : DramaSimpleActionHandler<CGScaleAction>
    {
        protected override UniTask RunAsync(CGScaleAction a, IDramaContext ctx, CancellationToken ct)
        {
            Transform root = ctx.CG?.Root;
            if (root == null) return UniTask.CompletedTask;

            var duration = DramaWait.Scale(a.DurationSeconds, ctx.Mode);
            if (duration <= 0f)
            {
                root.localScale = a.Scale;
                return UniTask.CompletedTask;
            }

            return root.DOScale(a.Scale, duration).SetEase(a.Ease).ToUniTask(cancellationToken: ct);
        }
    }

    /// <summary>CG 旋转。</summary>
    public sealed class CGRotateActionHandler : DramaSimpleActionHandler<CGRotateAction>
    {
        protected override UniTask RunAsync(CGRotateAction a, IDramaContext ctx, CancellationToken ct)
        {
            Transform root = ctx.CG?.Root;
            if (root == null) return UniTask.CompletedTask;

            var duration = DramaWait.Scale(a.DurationSeconds, ctx.Mode);
            if (duration <= 0f)
            {
                root.localEulerAngles = a.Rotation;
                return UniTask.CompletedTask;
            }

            return root.DOLocalRotate(a.Rotation, duration).SetEase(a.Ease).ToUniTask(cancellationToken: ct);
        }
    }

    /// <summary>
    /// CG 小动作：从当前位置相对偏移一段，可循环。
    ///
    /// <b>次数为负 = 无限循环</b>，那种情况下立刻返回不等它（否则剧本会永远卡在这一条）。
    /// 这条游离的 Tween 由 <see cref="Services.IDramaCG.CompleteAllTweens"/> 收口。
    /// </summary>
    public sealed class CGOffsetMoveActionHandler : DramaSimpleActionHandler<CGOffsetMoveAction>
    {
        protected override UniTask RunAsync(CGOffsetMoveAction a, IDramaContext ctx, CancellationToken ct)
        {
            Transform root = ctx.CG?.Root;
            if (root == null) return UniTask.CompletedTask;

            var duration = DramaWait.Scale(a.DurationSeconds, ctx.Mode);
            if (duration <= 0f)
            {
                root.localPosition += a.Offset;
                return UniTask.CompletedTask;
            }

            var tween = root.DOLocalMove(a.Offset, duration)
                            .SetRelative(true)
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

    /// <summary>CG 抖动（硬抖）。手感见 <see cref="DramaShake"/>。</summary>
    public sealed class CGShakeActionHandler : DramaSimpleActionHandler<CGShakeAction>
    {
        protected override UniTask RunAsync(CGShakeAction a, IDramaContext ctx, CancellationToken ct)
        {
            Transform root = ctx.CG?.Root;
            if (root == null) return UniTask.CompletedTask;

            return DramaShake.HardAsync(root, a.Axis, a.Amplitude,
                                        DramaWait.Scale(a.DurationSeconds, ctx.Mode),
                                        a.RestoreOnEnd, ct);
        }
    }

    /// <summary>CG 震动（柔震）。</summary>
    public sealed class CGVibrateActionHandler : DramaSimpleActionHandler<CGVibrateAction>
    {
        protected override UniTask RunAsync(CGVibrateAction a, IDramaContext ctx, CancellationToken ct)
        {
            Transform root = ctx.CG?.Root;
            if (root == null) return UniTask.CompletedTask;

            // 间隔不跟着快进缩 —— 缩了就变成另一种频率的动作了，只缩总时长
            return DramaShake.SoftAsync(root, a.Axis, a.Amplitude, a.IntervalSeconds, a.SmoothSpeed,
                                        DramaWait.Scale(a.DurationSeconds, ctx.Mode),
                                        a.RestoreOnEnd, ct);
        }
    }

    // ---- Animator

    /// <summary>CG 的 Animator 参数（Bool / Int / Float / Trigger 四条）。</summary>
    public sealed class CGAnimBoolActionHandler : DramaSimpleActionHandler<CGAnimBoolAction>
    {
        protected override UniTask RunAsync(CGAnimBoolAction a, IDramaContext ctx, CancellationToken ct)
        {
            ctx.CG?.SetAnimatorBool(a.ParameterName, a.Value);
            return UniTask.CompletedTask;
        }
    }

    /// <inheritdoc cref="CGAnimBoolActionHandler"/>
    public sealed class CGAnimIntActionHandler : DramaSimpleActionHandler<CGAnimIntAction>
    {
        protected override UniTask RunAsync(CGAnimIntAction a, IDramaContext ctx, CancellationToken ct)
        {
            ctx.CG?.SetAnimatorInt(a.ParameterName, a.Value);
            return UniTask.CompletedTask;
        }
    }

    /// <inheritdoc cref="CGAnimBoolActionHandler"/>
    public sealed class CGAnimFloatActionHandler : DramaSimpleActionHandler<CGAnimFloatAction>
    {
        protected override UniTask RunAsync(CGAnimFloatAction a, IDramaContext ctx, CancellationToken ct)
        {
            ctx.CG?.SetAnimatorFloat(a.ParameterName, a.Value);
            return UniTask.CompletedTask;
        }
    }

    /// <inheritdoc cref="CGAnimBoolActionHandler"/>
    public sealed class CGAnimTriggerActionHandler : DramaSimpleActionHandler<CGAnimTriggerAction>
    {
        protected override UniTask RunAsync(CGAnimTriggerAction a, IDramaContext ctx, CancellationToken ct)
        {
            ctx.CG?.SetAnimatorTrigger(a.ParameterName, a.Reset);
            return UniTask.CompletedTask;
        }
    }
}

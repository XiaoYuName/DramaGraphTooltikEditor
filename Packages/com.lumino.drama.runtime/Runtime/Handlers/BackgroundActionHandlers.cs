using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Drama.Runtime.Flow;
using UnityEngine;

namespace Drama.Runtime.Handlers
{
    /// <summary>
    /// 切换背景图。
    ///
    /// 资源在这里加载而不是让 <see cref="Services.IDramaBackground"/> 自己去拿 ——
    /// 背景层只该管"怎么显示"，"从哪儿加载"是 <see cref="Services.IDramaAssetProvider"/> 的事。
    /// 正常情况下这一下不会真的产生加载，开播前已经预载过了。
    /// </summary>
    public sealed class ChangeBackgroundActionHandler : DramaSimpleActionHandler<ChangeBackgroundAction>
    {
        protected override async UniTask RunAsync(ChangeBackgroundAction a, IDramaContext ctx, CancellationToken ct)
        {
            if (ctx.Background == null || a.BackgroundId <= 0) return;

            var sprite = ctx.Assets == null ? null : await ctx.Assets.LoadBackgroundAsync(a.BackgroundId, ct);
            if (sprite == null)
            {
                Debug.LogWarning($"[Drama] 背景资源缺失：{a.BackgroundId}");
                return;
            }

            await ctx.Background.ChangeAsync(
                a.BackgroundId, sprite, a.Transition,
                DramaWait.Scale(a.InSeconds, ctx.Mode),
                DramaWait.Scale(a.OutSeconds, ctx.Mode),
                ct);
        }
    }

    /// <summary>背景位移。</summary>
    public sealed class BackgroundMoveActionHandler : DramaSimpleActionHandler<BackgroundMoveAction>
    {
        protected override UniTask RunAsync(BackgroundMoveAction a, IDramaContext ctx, CancellationToken ct)
        {
            var root = ctx.Background?.GetRoot(a.BackgroundId);
            if (root == null) return UniTask.CompletedTask;

            var duration = DramaWait.Scale(a.DurationSeconds, ctx.Mode);
            if (duration <= 0f)
            {
                root.localPosition = a.Position;
                return UniTask.CompletedTask;
            }

            return root.DOLocalMove(a.Position, duration)
                       .SetEase(a.Ease)
                       .ToUniTask(cancellationToken: ct);
        }
    }

    /// <summary>背景旋转。</summary>
    public sealed class BackgroundRotateActionHandler : DramaSimpleActionHandler<BackgroundRotateAction>
    {
        protected override UniTask RunAsync(BackgroundRotateAction a, IDramaContext ctx, CancellationToken ct)
        {
            var root = ctx.Background?.GetRoot(a.BackgroundId);
            if (root == null) return UniTask.CompletedTask;

            var duration = DramaWait.Scale(a.DurationSeconds, ctx.Mode);
            if (duration <= 0f)
            {
                root.localEulerAngles = a.Rotation;
                return UniTask.CompletedTask;
            }

            return root.DOLocalRotate(a.Rotation, duration)
                       .SetEase(a.Ease)
                       .ToUniTask(cancellationToken: ct);
        }
    }

    /// <summary>背景缩放。</summary>
    public sealed class BackgroundScaleActionHandler : DramaSimpleActionHandler<BackgroundScaleAction>
    {
        protected override UniTask RunAsync(BackgroundScaleAction a, IDramaContext ctx, CancellationToken ct)
        {
            var root = ctx.Background?.GetRoot(a.BackgroundId);
            if (root == null) return UniTask.CompletedTask;

            var duration = DramaWait.Scale(a.DurationSeconds, ctx.Mode);
            if (duration <= 0f)
            {
                root.localScale = a.Scale;
                return UniTask.CompletedTask;
            }

            return root.DOScale(a.Scale, duration)
                       .SetEase(a.Ease)
                       .ToUniTask(cancellationToken: ct);
        }
    }
}

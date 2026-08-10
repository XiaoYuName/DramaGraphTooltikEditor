using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Drama.Runtime.Handlers
{
    /// <summary>
    /// 抖动 / 震动的运动实现。
    ///
    /// <b>不是 DOTween 的 DOShake。</b> 手感对齐旧工程（<c>ShakeEff</c>），那套是逐帧改
    /// localPosition，跟 DOTween 的柏林噪声抖动完全不是一个味道，所以这里自己写循环：
    ///
    /// <list type="bullet">
    /// <item><b>Shake（硬抖）</b>：每帧瞬间跳到 8 个方位之一，不插值。颗粒感强，像受击。</item>
    /// <item><b>Vibrate（柔震）</b>：每隔一段时间换个随机目标点，逐帧 Lerp 趋近。柔和飘动，像发抖。</item>
    /// </list>
    ///
    /// 两者共用同一套 8 方位：<b>上下左右 ±2×振幅，四对角 ±1×振幅</b>，
    /// 并且保证不会连续两次挑到同一个方位（不然看着像卡住）。
    /// </summary>
    public static class DramaShake
    {
        /// <summary>柔震结束时平滑归位的时长。</summary>
        public const float RestoreSeconds = 0.1f;

        // 8 方位偏移，单位是"几倍振幅"。索引顺序照搬旧工程，别随手重排 —— 手感就在这张表里。
        static readonly Vector2[] k_Dirs =
        {
            new Vector2( 1f,  1f),   // 0 右上
            new Vector2(-2f,  0f),   // 1 左
            new Vector2( 2f,  0f),   // 2 右
            new Vector2( 1f, -1f),   // 3 右下
            new Vector2( 0f,  2f),   // 4 上
            new Vector2( 0f, -2f),   // 5 下
            new Vector2(-1f,  1f),   // 6 左上
            new Vector2(-1f, -1f),   // 7 左下
        };

        // Z 轴（前后推拉）用的是另一套权重，8 档从 -2 到 +2，不是简单的正负对称
        static readonly float[] k_DirsZ = { 1.33f, -2f, 2f, 1f, 1.66f, -1.66f, -1f, -1.33f };

        /// <summary>
        /// 硬抖。每帧瞬间跳到一个随机方位，跑完 <paramref name="duration"/> 秒后瞬间复位。
        /// </summary>
        public static async UniTask HardAsync(Transform root, EShakeAxis axis, float amplitude,
                                              float duration, bool restoreOnEnd, CancellationToken ct)
        {
            if (root == null) return;

            // 兜底值照抄旧工程，策划漏填时手感不会突变
            if (duration <= 0f) duration = 0.2f;
            if (amplitude <= 0f) amplitude = 0.5f;

            var angles = axis == EShakeAxis.Rotation;
            var origin = angles ? root.localEulerAngles : root.localPosition;
            var last = -1;
            var elapsed = 0f;

            try
            {
                while (elapsed < duration)
                {
                    Apply(root, angles, origin + Offset(axis, NextIndex(ref last), amplitude));

                    await UniTask.Yield(PlayerLoopTiming.Update, ct);
                    elapsed += Time.deltaTime;
                }
            }
            finally
            {
                // 被打断也要归位，不然立绘会永远停在偏移过的位置上
                if (restoreOnEnd && root != null) Apply(root, angles, origin);
            }
        }

        /// <summary>
        /// 柔震。每隔 <paramref name="interval"/> 秒换一个随机目标点，逐帧
        /// <c>Lerp(当前, 目标, dt × speed)</c> 趋近；跑完平滑归位。
        ///
        /// <b>和旧工程的一个有意差别</b>：旧的是"再调一次同一条指令就关掉"的开关语义，
        /// 在节点图里没法看出谁开谁关，漏放一个就永久震动。这里改成时长语义。
        /// </summary>
        public static async UniTask SoftAsync(Transform root, EShakeAxis axis, float amplitude, float interval,
                                              float speed, float duration, bool restoreOnEnd, CancellationToken ct)
        {
            if (root == null) return;

            if (interval <= 0f) interval = 0.2f;
            if (amplitude <= 0f) amplitude = 0.2f;
            if (speed <= 0f) speed = 5f;
            if (duration <= 0f)
            {
                // 旧工程这里是无限震动、等另一条指令来关；改成时长语义后没填时长就只震一个间隔
                Debug.LogWarning("[Drama] 震动没填时长，只震一个间隔");
                duration = interval;
            }

            var angles = axis == EShakeAxis.Rotation;
            var origin = angles ? root.localEulerAngles : root.localPosition;
            var last = -1;
            var elapsed = 0f;
            var sinceRetarget = float.MaxValue;   // 让第一帧就先挑一个目标
            var target = origin;

            try
            {
                while (elapsed < duration)
                {
                    if (sinceRetarget >= interval)
                    {
                        sinceRetarget = 0f;
                        target = origin + Offset(axis, NextIndex(ref last), amplitude);
                    }

                    var t = Time.deltaTime * speed;
                    if (angles)
                        root.localRotation = Quaternion.Slerp(root.localRotation, Quaternion.Euler(target), t);
                    else
                        root.localPosition = Vector3.Lerp(root.localPosition, target, t);

                    await UniTask.Yield(PlayerLoopTiming.Update, ct);
                    elapsed += Time.deltaTime;
                    sinceRetarget += Time.deltaTime;
                }

                // 正常跑完：平滑滑回原位，直接瞬移会看出一下"啪"的跳变
                if (restoreOnEnd) await RestoreAsync(root, angles, origin, ct);
            }
            catch (OperationCanceledException)
            {
                // 被打断就来不及平滑了，直接归位
                if (restoreOnEnd && root != null) Apply(root, angles, origin);
                throw;
            }
        }

        static UniTask RestoreAsync(Transform root, bool angles, Vector3 origin, CancellationToken ct)
        {
            if (root == null) return UniTask.CompletedTask;

            return angles
                ? root.DOLocalRotate(origin, RestoreSeconds).SetEase(Ease.Linear).ToUniTask(cancellationToken: ct)
                : root.DOLocalMove(origin, RestoreSeconds).SetEase(Ease.Linear).ToUniTask(cancellationToken: ct);
        }

        static void Apply(Transform root, bool angles, Vector3 value)
        {
            if (root == null) return;
            if (angles) root.localEulerAngles = value;
            else root.localPosition = value;
        }

        /// <summary>挑下一个方位，避开上一次挑中的那个。</summary>
        static int NextIndex(ref int last)
        {
            var n = Random.Range(0, k_Dirs.Length);
            if (n == last)
            {
                n++;
                if (n >= k_Dirs.Length) n = 0;
            }

            last = n;
            return n;
        }

        static Vector3 Offset(EShakeAxis axis, int index, float amplitude)
        {
            switch (axis)
            {
                case EShakeAxis.PositionZ:
                    return new Vector3(0f, 0f, k_DirsZ[index] * amplitude);

                // 旋转抖的是欧拉角的 X / Y，跟位置用同一张表
                default:
                    var d = k_Dirs[index];
                    return new Vector3(d.x * amplitude, d.y * amplitude, 0f);
            }
        }
    }
}

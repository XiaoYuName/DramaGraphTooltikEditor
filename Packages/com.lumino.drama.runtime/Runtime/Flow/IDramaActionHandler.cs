using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Drama.Runtime.Flow
{
    /// <summary>
    /// 一种指令的执行逻辑。
    ///
    /// 为什么不把 Execute 直接写进 <see cref="DramaAction"/>：
    ///   Action 是跟着 UPM 包跑到别的工程里的纯数据，而"台词怎么显示""立绘怎么淡入"
    ///   每个工程都不一样。而且 [SerializeReference] 的类一旦挂上表现层依赖，
    ///   改个程序集名整包已导出资产就全变 null。
    /// </summary>
    public interface IDramaActionHandler
    {
        Type ActionType { get; }

        UniTask<DramaFlowResult> ExecuteAsync(DramaAction action, IDramaContext ctx, CancellationToken ct);
    }

    /// <summary>强类型 Handler 基类，省掉每次手写强转。</summary>
    public abstract class DramaActionHandler<T> : IDramaActionHandler where T : DramaAction
    {
        public Type ActionType => typeof(T);

        UniTask<DramaFlowResult> IDramaActionHandler.ExecuteAsync(DramaAction action, IDramaContext ctx, CancellationToken ct) =>
            ExecuteAsync((T)action, ctx, ct);

        protected abstract UniTask<DramaFlowResult> ExecuteAsync(T action, IDramaContext ctx, CancellationToken ct);
    }

    /// <summary>
    /// 不改变流程走向的 Handler 基类（绝大多数指令属于这类）。
    /// 干完活自动 <see cref="DramaFlowResult.Continue"/>。
    /// </summary>
    public abstract class DramaSimpleActionHandler<T> : DramaActionHandler<T> where T : DramaAction
    {
        protected sealed override async UniTask<DramaFlowResult> ExecuteAsync(T action, IDramaContext ctx, CancellationToken ct)
        {
            await RunAsync(action, ctx, ct);
            return DramaFlowResult.Continue;
        }

        protected abstract UniTask RunAsync(T action, IDramaContext ctx, CancellationToken ct);
    }

    /// <summary>
    /// 指令类型 → Handler。
    ///
    /// 手动 <see cref="Register"/>，不要用反射扫程序集 —— IL2CPP 上代码裁剪会把
    /// 没被静态引用到的 Handler 裁掉，真机上才炸。
    /// </summary>
    public sealed class DramaHandlerRegistry
    {
        readonly Dictionary<Type, IDramaActionHandler> m_Map = new Dictionary<Type, IDramaActionHandler>();

        public int Count => m_Map.Count;

        public DramaHandlerRegistry Register(IDramaActionHandler handler)
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            m_Map[handler.ActionType] = handler;
            return this;
        }

        public bool TryGet(Type actionType, out IDramaActionHandler handler) =>
            m_Map.TryGetValue(actionType, out handler);

        public IDramaActionHandler Resolve(DramaAction action)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            if (m_Map.TryGetValue(action.GetType(), out var handler)) return handler;

            throw new DramaMissingHandlerException(action);
        }

        /// <summary>
        /// 检查一个剧本用到的指令类型是否都注册了。
        /// 建议在播放前调一次，把"播到第 37 条才炸"提前成"根本不开始播"。
        /// </summary>
        public List<Type> FindMissing(DramaScript script)
        {
            var missing = new List<Type>();
            if (script?.Actions == null) return missing;

            foreach (var action in script.Actions)
            {
                if (action == null) continue;
                var t = action.GetType();
                if (!m_Map.ContainsKey(t) && !missing.Contains(t)) missing.Add(t);
            }

            return missing;
        }
    }

    public sealed class DramaMissingHandlerException : Exception
    {
        public readonly Type ActionType;

        public DramaMissingHandlerException(DramaAction action)
            : base($"指令「{action.Kind}」(#{action.Index} {action.GetType().Name}) 没有注册 Handler")
        {
            ActionType = action.GetType();
        }
    }
}

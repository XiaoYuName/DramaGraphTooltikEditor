namespace Drama.Runtime.Flow
{
    /// <summary>
    /// Handler 执行完之后，告诉执行器接下来怎么走。
    ///
    /// 有了它，<see cref="DramaPlayer"/> 就不需要认识任何具体指令类型：
    ///   选项分支 → Handler 拿到玩家的选择，返回 <see cref="Jump"/>
    ///   跳转剧本 → Handler 返回 <see cref="Goto"/>，执行器层层退出，交给外层换剧本
    /// </summary>
    public readonly struct DramaFlowResult
    {
        public enum EKind
        {
            /// <summary>按 <see cref="DramaAction.Next"/> 正常往下走。</summary>
            Continue = 0,

            /// <summary>无视 Next，跳到指定下标。</summary>
            Jump = 1,

            /// <summary>本条执行流到此为止。</summary>
            Stop = 2,

            /// <summary>整个剧本结束，请求换到另一个剧本。</summary>
            Goto = 3,
        }

        public readonly EKind Kind;
        public readonly int JumpTarget;
        public readonly long GotoDramaId;

        DramaFlowResult(EKind kind, int jumpTarget, long gotoDramaId)
        {
            Kind = kind;
            JumpTarget = jumpTarget;
            GotoDramaId = gotoDramaId;
        }

        /// <summary>绝大多数 Handler 用这个。</summary>
        public static readonly DramaFlowResult Continue = default;

        public static readonly DramaFlowResult Stop = new DramaFlowResult(EKind.Stop, -1, -1);

        /// <summary>跳到指定指令。target &lt; 0 等价于 <see cref="Stop"/>。</summary>
        public static DramaFlowResult Jump(int target) =>
            target < 0 ? Stop : new DramaFlowResult(EKind.Jump, target, -1);

        /// <summary>换剧本。dramaId &lt;= 0 表示直接结束整段剧情。</summary>
        public static DramaFlowResult Goto(long dramaId) =>
            dramaId <= 0 ? Stop : new DramaFlowResult(EKind.Goto, -1, dramaId);
    }
}

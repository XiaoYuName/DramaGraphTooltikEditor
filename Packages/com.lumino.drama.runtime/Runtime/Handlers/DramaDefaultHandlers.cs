using Drama.Runtime.Flow;

namespace Drama.Runtime.Handlers
{
    /// <summary>
    /// 一次把包里自带的 Handler 全注册上。
    ///
    /// 全部手动 Register，不用反射扫程序集 —— IL2CPP 上代码裁剪会把没被
    /// 静态引用到的类型裁掉，那样只有真机上才炸。
    /// </summary>
    public static class DramaDefaultHandlers
    {
        public static DramaHandlerRegistry CreateDefault()
            => RegisterAll(new DramaHandlerRegistry());

        public static DramaHandlerRegistry RegisterAll(DramaHandlerRegistry registry)
        {
            // ---- 流程
            registry.Register(new WaitActionHandler());
            registry.Register(new ChoiceActionHandler());
            registry.Register(new GotoDramaActionHandler());
            registry.Register(new ReceiveTaskActionHandler());

            // ---- 对话
            registry.Register(new TalkActionHandler());
            registry.Register(new TalkShowActionHandler());
            registry.Register(new SetTalkFrameActionHandler());

            // ---- 演出
            registry.Register(new ScreenTransitionActionHandler());

            // ---- 立绘
            registry.Register(new ActorShowActionHandler());
            registry.Register(new ActorMoveActionHandler());
            registry.Register(new ActorScaleActionHandler());
            registry.Register(new ActorRotateActionHandler());
            registry.Register(new ActorSetSkinActionHandler());
            registry.Register(new ActorPlayAnimationActionHandler());
            registry.Register(new ActorHighlightActionHandler());

            // ---- 还没实现的（宿主自己补，或者等后续版本）：
            //   ActorOffsetMoveAction / ActorShakeAction / ActorVibrateAction
            //   ChangeBackgroundAction / PlayMusicAction
            // 漏了也不会播到一半才炸 —— DramaHandlerRegistry.FindMissing 会在播放前报出来

            return registry;
        }
    }
}

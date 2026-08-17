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
            registry.Register(new WaitInputActionHandler());
            registry.Register(new ChoiceActionHandler());
            registry.Register(new GotoDramaActionHandler());
            registry.Register(new ReceiveTaskActionHandler());
            registry.Register(new ReceiveRewardActionHandler());
            registry.Register(new ShowUIActionHandler());
            registry.Register(new ChangeGameSceneActionHandler());
            registry.Register(new SceneVisibilityActionHandler());
            registry.Register(new EndUIDramaActionHandler());
            registry.Register(new EndGuideDramaActionHandler());

            // ---- 对话
            registry.Register(new TalkActionHandler());
            registry.Register(new TalkShowActionHandler());
            registry.Register(new SetTalkFrameActionHandler());

            // ---- 演出
            registry.Register(new ScreenTransitionActionHandler());
            registry.Register(new PlayMusicActionHandler());

            // ---- 背景
            registry.Register(new ChangeBackgroundActionHandler());
            registry.Register(new BackgroundMoveActionHandler());
            registry.Register(new BackgroundRotateActionHandler());
            registry.Register(new BackgroundScaleActionHandler());

            // ---- 立绘
            registry.Register(new ActorShowActionHandler());
            registry.Register(new ActorMoveActionHandler());
            registry.Register(new ActorScaleActionHandler());
            registry.Register(new ActorRotateActionHandler());
            registry.Register(new ActorSetSkinActionHandler());
            registry.Register(new ActorPlayAnimationActionHandler());
            registry.Register(new ActorHighlightActionHandler());
            registry.Register(new ActorOffsetMoveActionHandler());
            registry.Register(new ActorShakeActionHandler());
            registry.Register(new ActorVibrateActionHandler());
            registry.Register(new ActorAnimBoolActionHandler());
            registry.Register(new ActorAnimIntActionHandler());
            registry.Register(new ActorAnimFloatActionHandler());
            registry.Register(new ActorAnimTriggerActionHandler());

            // ---- CG
            registry.Register(new CGShowActionHandler());
            registry.Register(new CGHideActionHandler());
            registry.Register(new CGMoveActionHandler());
            registry.Register(new CGScaleActionHandler());
            registry.Register(new CGRotateActionHandler());
            registry.Register(new CGOffsetMoveActionHandler());
            registry.Register(new CGShakeActionHandler());
            registry.Register(new CGVibrateActionHandler());
            registry.Register(new CGAnimBoolActionHandler());
            registry.Register(new CGAnimIntActionHandler());
            registry.Register(new CGAnimFloatActionHandler());
            registry.Register(new CGAnimTriggerActionHandler());

            // 至此所有 XxxAction 都有 Handler 了。加新指令时记得回来登记一条，
            // 漏了也不会播到一半才炸 —— DramaHandlerRegistry.FindMissing 会在播放前报出来。
            // 漏了也不会播到一半才炸 —— DramaHandlerRegistry.FindMissing 会在播放前报出来

            return registry;
        }
    }
}

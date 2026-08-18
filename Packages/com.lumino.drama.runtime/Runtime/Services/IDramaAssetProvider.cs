using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Drama.Runtime.Services
{
    /// <summary>
    /// 剧情要用的资源。
    ///
    /// <b>入参一律是剧情里的业务 ID，不是 Addressables 地址。</b>
    /// ID → 地址的映射是宿主工程配置表的事，本包不该知道，
    /// 宿主写个几十行的 adapter 接到自己封装的 Addressables 层即可。
    ///
    /// 注意<b>台词语音不在这里</b> —— 语音是多语言资源，走
    /// <see cref="IDramaLocalization.ResolveVoiceAsync"/>。
    ///
    /// 生命周期：Director 在每段剧本开始前预载、结束后 <see cref="ReleaseAll"/>。
    /// </summary>
    public interface IDramaAssetProvider
    {
        UniTask<Sprite> LoadBackgroundAsync(long backgroundId, CancellationToken ct);

        // 立绘不在这里：包无法规定"一个立绘资源"到底是什么 —— 可能是 Prefab，
        // 可能是 Spine 的 SkeletonDataAsset，也可能是一张贴图。
        // 而且包里没有任何东西需要它（Handler 只调 IActorStage.AcquireAsync），
        // 所以由宿主自己定加载方式，包只通过 DramaAssetKeys.ActorIds 告诉宿主"本段要用哪些角色"。

        // BGM 不在这里：MusicId 是宿主音频系统的配置表 ID，clip 由那套系统自己持有，
        // 剧情这边既不该加载也不该释放。见 IDramaAudio.PlayMusic。

        /// <summary>整段剧本结束时释放本段加载的全部资源。</summary>
        void ReleaseAll();
    }

    /// <summary>
    /// 多语言。文本和语音都走这里。
    ///
    /// 台词文本 → String Table；台词语音 → Asset Table。
    /// 两者在编辑器里都是 <see cref="LocalizedRef"/>（`LocalizationNode` / `LocalizationAudioNode` 产出的）。
    /// </summary>
    public interface IDramaLocalization
    {
        /// <summary>
        /// 查文本表。<b>要求是同步的</b> —— 每句台词都 await 一次表查询既难写又容易掉帧，
        /// 所以约定由 <see cref="PreloadStringTablesAsync"/> 提前把表拉进内存，这里只做纯查询。
        /// </summary>
        string Resolve(LocalizedRef reference);

        // 语音不在这里取：Handler 只把引用交给 IDramaAudio.PlayVoice，
        // "引用 → clip" 那一步是宿主音频层内部的事，包不需要经手。

        /// <summary>播剧本前预热本段用到的文本表。</summary>
        UniTask PreloadStringTablesAsync(IReadOnlyCollection<string> tables, CancellationToken ct);

        /// <summary>播剧本前预热本段用到的语音表。</summary>
        UniTask PreloadAssetTablesAsync(IReadOnlyCollection<string> tables, CancellationToken ct);
    }

    /// <summary>剧情要回调的业务逻辑（领任务、发奖励之类）。</summary>
    public interface IDramaGameBridge
    {
        UniTask ReceiveTaskAsync(long taskId, CancellationToken ct);

        /// <summary>
        /// 发一份奖励并弹出"获得奖励"界面，<b>等这一下交互结束再返回</b>。
        /// <see cref="ReceiveRewardAction"/> 用。
        ///
        /// <b>怎么等由实现方按 <paramref name="mode"/> 决定</b>（和
        /// <see cref="IDialogueView.ShowLineAsync"/> 收 mode 是同一个路子）：
        ///   正常模式 —— 挂在那儿等玩家自己关掉弹窗；
        ///   自动 / 跳过 —— 亮一下就自己收掉，别拦着剧情。
        ///
        /// 调用方保证<b>不会</b>在读档的静默重放期间调它（那时候奖励早发过了），
        /// 所以实现里可以放心发放，不用自己去防重复。
        /// </summary>
        /// <param name="rewardId">宿主奖励表的 ID。</param>
        UniTask ShowRewardAsync(long rewardId, Flow.EDramaPlaybackMode mode, CancellationToken ct);

        /// <summary>
        /// 打开一个界面并<b>等它被关掉</b>再返回。<see cref="ShowUIAction"/> 用。
        ///
        /// 等待方式和 <see cref="ShowRewardAsync"/> 完全一致：正常模式等玩家自己关，
        /// 自动 / 跳过模式下自己收掉、别拦着剧情。
        ///
        /// 和 <see cref="RequestOpenUIOnEnd"/> 不是一回事：那个是"剧情结束之后再开"、
        /// 只记不开；这个是剧情<b>中途</b>开出来并等在这儿。
        /// </summary>
        /// <param name="uiPage">宿主 UI 系统里的界面ID（界面名）。</param>
        UniTask ShowUIAsync(string uiPage, Flow.EDramaPlaybackMode mode, CancellationToken ct);

        /// <summary>
        /// 进一个小游戏玩法，<b>等它玩完并回报成败</b>。<see cref="PlayMinGameAction"/> 用。
        ///
        /// <paramref name="minGameId"/> 是<b>宿主自己那套小游戏枚举的整数值</b> ——
        /// 包不认识宿主有哪些小游戏，实现里转回自己的枚举即可。
        ///
        /// <b>失败不回来</b>：玩砸了由小游戏自己弹失败界面让玩家重试，一直重试到过关为止。
        /// 所以本方法返回时一定是"通关了"，剧情这边没有失败分支要处理。
        ///
        /// <b>要等玩家点掉成功界面再返回</b>，不是玩法判定通过就返回 ——
        /// 中间那个成功界面是给玩家看的，剧情不能抢在它前面继续演。
        ///
        /// <b>没有 mode 参数</b>：跳过 / 自动模式下也照玩，小游戏是玩家要动手的关卡，
        /// 不是能快进的演出（和选项面板一个道理）。
        /// </summary>
        UniTask PlayMinGameAsync(int minGameId, CancellationToken ct);

        /// <summary>
        /// 切换游戏内的真实场景。<see cref="ChangeGameSceneAction"/> 用。
        ///
        /// <b>要等场景真的切完再返回</b> —— 后面的指令（换背景、出立绘）都是演给新场景看的，
        /// 不等的话会在旧场景上演一半。宿主那边的转场往往是"发起了就不管"的，
        /// 实现里需要自己盯着它的完成标志。
        /// </summary>
        /// <param name="mapSceneId">大场景 ID，小于等于 0 表示留在当前大场景里只换小场景。</param>
        UniTask ChangeGameSceneAsync(long mapSceneId, long minSceneId, CancellationToken ct);

        /// <summary>
        /// 报上"剧情结束之后要打开哪个界面"。<see cref="EndUIDramaAction"/> 用。
        ///
        /// <b>实现里只应当记下来，不要当场打开。</b> 调用发生在剧情还没收尾的时候，
        /// 当场打开会被随后的"关剧情面板 + 还原进剧情前的界面"盖掉。
        /// 什么时候真正打开由宿主的收尾流程决定。
        /// </summary>
        void RequestOpenUIOnEnd(string uiPage);

        /// <summary>
        /// 报上"剧情结束之后要播哪段引导"。<see cref="EndGuideDramaAction"/> 用。
        ///
        /// 和 <see cref="RequestOpenUIOnEnd"/> 一样：<b>实现里只记下来，不要当场开</b>，
        /// 引导多半要指着某个界面上的按钮，而那些界面要等剧情收尾之后才还原回来。
        /// </summary>
        void RequestStartGuideOnEnd(long guideId);

        /// <summary>
        /// 设置游戏场景里那些"和剧情无关的东西"的显隐：场景 NPC、地图配置的场景默认UI。
        /// <see cref="SceneVisibilityAction"/> 用。
        ///
        /// <b>实现里要把它当成一个持续的"意图"存下来，而不是执行一次就完。</b>
        /// 剧情中途切场景会重新生成 NPC、重新开默认UI，只在收到指令那一刻做一次是拦不住的 ——
        /// 每次场景就绪时都要按最后一次的意图重新应用。
        /// </summary>
        void SetSceneVisibility(bool showNpc, bool showSceneUI);

        // ==================================================== 功能开放 / 临时显隐
        //
        // 本作的"解锁"就是那个 UI 按钮显不显示（没有"灰着但看得见"这一档），
        // 所以这六个口子最终都落在 SetActive 上。两组的区别很重要：
        //
        //   Unlock*            永久进度，<b>要写进存档</b>。默认全都没解锁，靠剧本一条条开。
        //                      必须幂等 —— 读档静默重放会把整段剧情重走一遍。
        //   Set*Visible        剧情期间的临时覆盖，<b>不要写进存档</b>。给引导用。
        //
        // 实现里最终可见性建议算成：<b>已解锁 && 没被藏</b>。
        // 也就是说 Set*Visible(true) 只是"撤掉隐藏"，不该把一个没解锁的功能变出来。
        //
        // 三个域的 ID 形状不一样，所以是六个方法而不是一个带枚举的大方法 ——
        // 那样每个调用点都得传三个用不上的参数。

        /// <summary>
        /// 解锁一个系统功能（主界面那排按钮）。<see cref="UnlockSystemFunctionAction"/> 用。
        ///
        /// <paramref name="systemFunctionValue"/> 是<b>宿主自己那套系统功能枚举的整数值</b>，
        /// 实现里转回自己的枚举即可 —— 和 <see cref="PlayMinGameAsync"/> 收 int 是一个道理。
        /// </summary>
        void UnlockSystemFunction(int systemFunctionValue);

        /// <summary>
        /// 解锁某个角色身上的一个功能。<see cref="UnlockCharacterFunctionAction"/> 用。
        ///
        /// <paramref name="functionValue"/> 是宿主角色功能枚举的整数值，<b>一次只开一个</b>
        /// （本作那个枚举是 <c>[Flags]</c>，所以传进来的是单个位）。
        /// </summary>
        void UnlockCharacterFunction(long characterId, int functionValue);

        /// <summary>
        /// 解锁一个地图入口。<see cref="UnlockMapAction"/> 用。
        /// </summary>
        /// <param name="subSceneId">小地图（小场景）ID。<b>-1 = 大地图上那个入口本身</b>。</param>
        void UnlockMap(long mapSceneId, long subSceneId);

        /// <summary>
        /// 系统功能按钮的临时显隐。<see cref="SystemFunctionVisibilityAction"/> 用。
        /// <b>不要进存档</b>，理由见本区顶部。
        /// </summary>
        void SetSystemFunctionVisible(int systemFunctionValue, bool visible);

        /// <summary>
        /// 角色功能按钮的临时显隐。<see cref="CharacterFunctionVisibilityAction"/> 用。
        /// <b>不要进存档</b>，理由见本区顶部。
        /// </summary>
        void SetCharacterFunctionVisible(long characterId, int functionValue, bool visible);

        /// <summary>
        /// 地图入口的临时显隐。<see cref="MapVisibilityAction"/> 用。
        /// <b>不要进存档</b>，理由见本区顶部。
        /// </summary>
        /// <param name="subSceneId">小地图（小场景）ID。<b>-1 = 大地图上那个入口本身</b>。</param>
        void SetMapVisible(long mapSceneId, long subSceneId, bool visible);
    }

    /// <summary>
    /// 音频播放。
    ///
    /// <b>两个都收"标识"而不是 clip</b> —— 谁去把标识变成声音，是宿主的事：
    /// BGM 走宿主音频配置表的 ID，语音走多语言 Asset Table 的引用。
    /// 这样 Handler 层不用为了播一句语音先 await 一次资源加载，台词也就不会被挡住。
    /// </summary>
    public interface IDramaAudio
    {
        /// <summary>播 BGM。<paramref name="musicId"/> 是宿主音频配置表的 ID，原样透传。</summary>
        void PlayMusic(string musicId);

        /// <summary>
        /// 播台词语音。<paramref name="reference"/> 为空引用时什么都不做。
        ///
        /// <b>实现应当是"即发即忘"的</b>：内部异步取 clip 再播，不要让调用方等 ——
        /// 台词该立刻显示出来，语音晚几帧进来是可以接受的。
        /// 取不到就当没配语音，别抛。
        /// </summary>
        void PlayVoice(LocalizedRef reference);

        void StopVoice();
    }
}

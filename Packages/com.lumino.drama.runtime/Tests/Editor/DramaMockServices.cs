using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Drama.Runtime.Flow;
using Drama.Runtime.Services;
using UnityEngine;

namespace Drama.Runtime.Tests
{
    // ============================================================================
    //  Handler 测试用的假服务。
    //
    //  一律用 UniTaskCompletionSource 做"可控的挂起"，不用 UniTask.Delay ——
    //  EditMode 下 PlayerLoop 不转，Delay 永远醒不过来。
    // ============================================================================

    /// <summary>一个能被测试代码手动放行的等待点。</summary>
    public sealed class Latch
    {
        UniTaskCompletionSource m_Tcs;

        public bool IsWaiting => m_Tcs != null;
        public int OpenedCount { get; private set; }

        public UniTask WaitAsync(CancellationToken ct)
        {
            m_Tcs = new UniTaskCompletionSource();
            return m_Tcs.Task.AttachExternalCancellation(ct);
        }

        public void Open()
        {
            var tcs = m_Tcs;
            m_Tcs = null;
            OpenedCount++;
            tcs?.TrySetResult();
        }
    }

    public sealed class MockDialogueView : IDialogueView
    {
        public readonly List<DialogueLine> Shown = new List<DialogueLine>();
        public readonly Latch TypewriterLatch = new Latch();
        public readonly Latch AdvanceLatch = new Latch();

        public EDramaPlaybackMode LastMode;
        public bool AutoFinishTypewriter = true;   // 默认打字机瞬间跑完，专测它时再关掉
        public bool? LastVisible;
        public ETalkFrame LastFrame;

        public UniTask ShowLineAsync(DialogueLine line, EDramaPlaybackMode mode, CancellationToken ct)
        {
            Shown.Add(line);
            LastMode = mode;
            return AutoFinishTypewriter ? UniTask.CompletedTask : TypewriterLatch.WaitAsync(ct);
        }

        public UniTask WaitForAdvanceAsync(CancellationToken ct) => AdvanceLatch.WaitAsync(ct);

        public UniTask SetVisibleAsync(bool visible, CancellationToken ct)
        {
            LastVisible = visible;
            return UniTask.CompletedTask;
        }

        public void SetFrame(ETalkFrame frame) => LastFrame = frame;
    }

    public sealed class MockChoiceView : IChoiceView
    {
        public string[] LastOptions;
        public int PickIndex;

        public UniTask<int> PickAsync(string[] options, CancellationToken ct)
        {
            LastOptions = options;
            return UniTask.FromResult(PickIndex);
        }
    }

    public sealed class MockActorView : IActorView
    {
        public MockActorView(int actorId)
        {
            ActorId = actorId;
            Root = new GameObject($"MockActor_{actorId}").transform;
        }

        public int ActorId { get; }
        public Transform Root { get; }

        public float Alpha = 1f;
        /// <summary>当前亮度 / 缩放倍率，1 = 原样。</summary>
        public float Brightness = 1f;
        public float ShrinkScale = 1f;
        public string Skin;
        public readonly List<string> PlayedAnimations = new List<string>();
        public readonly Latch AnimationLatch = new Latch();
        public bool AutoFinishAnimation = true;

        public void SetAlpha(float alpha) => Alpha = alpha;
        public void SetDim(float brightness) => Brightness = brightness;
        public void SetShrink(float scale) => ShrinkScale = scale;
        public void SetSkin(string skinName) => Skin = skinName;

        public UniTask PlayAnimationAsync(string animationName, int track, bool loop, float timeScale, CancellationToken ct)
        {
            PlayedAnimations.Add(animationName);
            return AutoFinishAnimation || loop ? UniTask.CompletedTask : AnimationLatch.WaitAsync(ct);
        }
    }

    public sealed class MockActorStage : IActorStage
    {
        readonly Dictionary<int, MockActorView> m_Actors = new Dictionary<int, MockActorView>();

        /// <summary>EditMode 测试会把 MockActorView 的 GameObject 建到当前打开的场景里，测完要收拾干净。</summary>
        public void DestroyCreatedObjects()
        {
            foreach (var a in m_Actors.Values)
                if (a.Root != null) Object.DestroyImmediate(a.Root.gameObject);
            m_Actors.Clear();
        }

        public readonly Latch VisibilityLatch = new Latch();
        public bool AutoFinishVisibility = true;
        public int CompleteAllCalls { get; private set; }
        public int ReleaseAllCalls { get; private set; }
        public bool? LastVisible;
        public float LastDuration;

        public MockActorView Get(int actorId) => m_Actors.TryGetValue(actorId, out var a) ? a : null;

        public UniTask<IActorView> AcquireAsync(int actorId, CancellationToken ct)
        {
            if (!m_Actors.TryGetValue(actorId, out var actor))
                m_Actors[actorId] = actor = new MockActorView(actorId);
            return UniTask.FromResult<IActorView>(actor);
        }

        public IActorView Find(int actorId) => Get(actorId);

        /// <summary>ActorId → 最后一次设过的方向。用来断言 Handler 有没有把方向传下来。</summary>
        public readonly Dictionary<int, EActorShowDirection> Directions = new Dictionary<int, EActorShowDirection>();

        public void SetDirection(IActorView actor, EActorShowDirection direction)
        {
            if (actor != null) Directions[actor.ActorId] = direction;
        }

        public ActorHighlightSettings Highlight { get; private set; } = ActorHighlightSettings.Default;

        /// <summary>最后一次报上来的说话人。-1 表示没有具体说话人。</summary>
        public int LastSpeaker { get; private set; } = -1;

        public int SetSpeakerCalls { get; private set; }

        public void SetHighlightMode(ActorHighlightSettings settings) => Highlight = settings;

        public void SetSpeaker(int actorId)
        {
            LastSpeaker = actorId;
            SetSpeakerCalls++;
        }

        public UniTask SetVisibleAsync(IActorView actor, bool visible, float duration, Ease ease, CancellationToken ct)
        {
            LastVisible = visible;
            LastDuration = duration;
            if (AutoFinishVisibility || duration <= 0f)
            {
                actor.SetAlpha(visible ? 1f : 0f);
                return UniTask.CompletedTask;
            }
            return VisibilityLatch.WaitAsync(ct);
        }

        public void CompleteAllTweens() => CompleteAllCalls++;
        public void ReleaseAll() => ReleaseAllCalls++;
    }

    /// <summary>查表就是把 Table/Key 拼起来，方便断言。</summary>
    public sealed class MockLocalization : IDramaLocalization
    {
        public readonly Dictionary<string, string> Overrides = new Dictionary<string, string>();
        public readonly List<string> PreloadedStringTables = new List<string>();
        public readonly List<string> PreloadedAssetTables = new List<string>();

        public string Resolve(LocalizedRef reference)
        {
            if (reference.IsEmpty) return string.Empty;
            var key = reference.ToString();
            return Overrides.TryGetValue(key, out var v) ? v : key;
        }

        public UniTask PreloadStringTablesAsync(IReadOnlyCollection<string> tables, CancellationToken ct)
        {
            PreloadedStringTables.AddRange(tables);
            return UniTask.CompletedTask;
        }

        public UniTask PreloadAssetTablesAsync(IReadOnlyCollection<string> tables, CancellationToken ct)
        {
            PreloadedAssetTables.AddRange(tables);
            return UniTask.CompletedTask;
        }
    }

    public sealed class MockAssetProvider : IDramaAssetProvider
    {
        public int ReleaseAllCalls { get; private set; }

        public UniTask<Sprite> LoadBackgroundAsync(long backgroundId, CancellationToken ct) =>
            UniTask.FromResult<Sprite>(null);

        public void ReleaseAll() => ReleaseAllCalls++;
    }

    public sealed class MockAudio : IDramaAudio
    {
        public int VoiceStopCount { get; private set; }

        /// <summary>最后一次播的 BGM 配置表 ID。</summary>
        public string LastMusicId;

        /// <summary>依次记下播过的语音引用。</summary>
        public readonly List<LocalizedRef> PlayedVoices = new List<LocalizedRef>();

        public void PlayMusic(string musicId) => LastMusicId = musicId;
        public void PlayVoice(LocalizedRef reference) => PlayedVoices.Add(reference);
        public void StopVoice() => VoiceStopCount++;
    }

    public sealed class MockScreen : IDramaScreen
    {
        /// <summary>依次记下每一步转场，形如 "Cover(Fade,1)" / "Reveal(Fade,0.5)"。</summary>
        public readonly List<string> Steps = new List<string>();

        public int ClearCalls { get; private set; }

        /// <summary>遮罩当前是不是盖着的。用来断言「Cover 之后不许自动还原」。</summary>
        public bool Covered { get; private set; }

        public UniTask CoverAsync(EScreenTransitionKind kind, float seconds, Color color, float alpha, Ease ease, CancellationToken ct)
        {
            Steps.Add($"Cover({kind},{seconds})");
            Covered = true;
            return UniTask.CompletedTask;
        }

        public UniTask RevealAsync(EScreenTransitionKind kind, float seconds, Ease ease, CancellationToken ct)
        {
            Steps.Add($"Reveal({kind},{seconds})");
            Covered = false;
            return UniTask.CompletedTask;
        }

        public void Clear()
        {
            ClearCalls++;
            Covered = false;
        }
    }

    public sealed class MockBackground : IDramaBackground
    {
        /// <summary>只有一张背景层，所以根节点也只有一个 —— 跟实际实现一致。</summary>
        public readonly Transform Root = new GameObject("MockBackground").transform;

        public long LastChangedTo = -1;
        public int ChangeCalls { get; private set; }
        public int CompleteCalls { get; private set; }
        public int ReleaseAllCalls { get; private set; }

        /// <summary>置 true 模拟"背景还没建出来"，用来测 Handler 会不会安全跳过。</summary>
        public bool RootMissing;

        public UniTask ChangeAsync(long backgroundId, Sprite sprite, EBgTransitionKind kind,
                                   float inSeconds, float outSeconds, CancellationToken ct)
        {
            ChangeCalls++;
            LastChangedTo = backgroundId;
            return UniTask.CompletedTask;
        }

        public Transform GetRoot(long backgroundId) => RootMissing ? null : Root;

        public void CompleteAllTweens() => CompleteCalls++;

        public void ReleaseAll() => ReleaseAllCalls++;
    }

    public sealed class MockGameBridge : IDramaGameBridge
    {
        public readonly List<long> ReceivedTasks = new List<long>();

        public UniTask ReceiveTaskAsync(long taskId, CancellationToken ct)
        {
            ReceivedTasks.Add(taskId);
            return UniTask.CompletedTask;
        }
    }

    /// <summary>把上面这堆一次性装配好。</summary>
    public sealed class MockServices
    {
        public readonly MockDialogueView Dialogue = new MockDialogueView();
        public readonly MockChoiceView Choice = new MockChoiceView();
        public readonly MockActorStage Actors = new MockActorStage();
        public readonly MockScreen Screen = new MockScreen();
        public readonly MockBackground Background = new MockBackground();
        public readonly MockLocalization Localization = new MockLocalization();
        public readonly MockAssetProvider Assets = new MockAssetProvider();
        public readonly MockAudio Audio = new MockAudio();
        public readonly MockGameBridge Game = new MockGameBridge();

        public readonly TestContext Context = new TestContext();

        public MockServices()
        {
            Context.Dialogue = Dialogue;
            Context.Choice = Choice;
            Context.Actors = Actors;
            Context.Screen = Screen;
            Context.Background = Background;
            Context.Localization = Localization;
            Context.Assets = Assets;
            Context.Audio = Audio;
            Context.Game = Game;
        }

        public EDramaPlaybackMode Mode
        {
            get => Context.Mode;
            set => Context.Mode = value;
        }
    }
}

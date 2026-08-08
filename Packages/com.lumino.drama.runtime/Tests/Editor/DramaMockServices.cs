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
        public bool Gray;
        public bool Shrink;
        public string Skin;
        public readonly List<string> PlayedAnimations = new List<string>();
        public readonly Latch AnimationLatch = new Latch();
        public bool AutoFinishAnimation = true;

        public void SetAlpha(float alpha) => Alpha = alpha;
        public void SetGray(bool gray) => Gray = gray;
        public void SetShrink(bool shrink) => Shrink = shrink;
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
        public readonly List<LocalizedRef> RequestedVoices = new List<LocalizedRef>();

        public AudioClip VoiceToReturn;

        public string Resolve(LocalizedRef reference)
        {
            if (reference.IsEmpty) return string.Empty;
            var key = reference.ToString();
            return Overrides.TryGetValue(key, out var v) ? v : key;
        }

        public UniTask<AudioClip> ResolveVoiceAsync(LocalizedRef reference, CancellationToken ct)
        {
            RequestedVoices.Add(reference);
            return UniTask.FromResult(VoiceToReturn);
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

        public UniTask<GameObject> LoadActorAsync(int actorId, CancellationToken ct) =>
            UniTask.FromResult<GameObject>(null);

        public UniTask<Sprite> LoadBackgroundAsync(long backgroundId, CancellationToken ct) =>
            UniTask.FromResult<Sprite>(null);

        public UniTask<AudioClip> LoadMusicAsync(string musicId, CancellationToken ct) =>
            UniTask.FromResult<AudioClip>(null);

        public void ReleaseAll() => ReleaseAllCalls++;
    }

    public sealed class MockAudio : IDramaAudio
    {
        public int VoicePlayCount { get; private set; }
        public int VoiceStopCount { get; private set; }
        public AudioClip LastMusic;

        public void PlayMusic(AudioClip clip) => LastMusic = clip;
        public void PlayVoice(AudioClip clip) => VoicePlayCount++;
        public void StopVoice() => VoiceStopCount++;
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

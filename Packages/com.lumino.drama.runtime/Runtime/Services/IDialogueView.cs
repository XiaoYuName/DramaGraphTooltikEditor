using System.Threading;
using Cysharp.Threading.Tasks;
using Drama.Runtime.Flow;
using UnityEngine;

namespace Drama.Runtime.Services
{
    /// <summary>一句已经解好多语言的台词。View 只管显示，不用再去查表。</summary>
    public struct DialogueLine
    {
        public string Body;
        public string SpeakerName;
        public Color NameColor;
        public EBalloonKind Balloon;

        /// <summary>本句语音，可能为 null（没配语音）。</summary>
        public AudioClip Voice;
    }

    /// <summary>对话框。</summary>
    public interface IDialogueView
    {
        /// <summary>
        /// 显示一句台词并放完打字机效果。
        ///
        /// 实现要点：打字机进行中玩家点一下应当立刻全文显示，
        /// 此时本方法返回，然后才轮到 <see cref="WaitForAdvanceAsync"/> 等真正的翻页。
        /// <paramref name="mode"/> 是 Skip / FastForward 时不要放动画。
        /// </summary>
        UniTask ShowLineAsync(DialogueLine line, EDramaPlaybackMode mode, CancellationToken ct);

        /// <summary>等玩家点击翻页。实现就是一个 UniTaskCompletionSource，点击回调里 TrySetResult。</summary>
        UniTask WaitForAdvanceAsync(CancellationToken ct);

        /// <summary>对话框整体显隐。<see cref="TalkShowAction"/> 用。</summary>
        UniTask SetVisibleAsync(bool visible, CancellationToken ct);

        /// <summary>切换对话框皮肤。<see cref="SetTalkFrameAction"/> 用。</summary>
        void SetFrame(ETalkFrame frame);
    }

    /// <summary>选项面板。</summary>
    public interface IChoiceView
    {
        /// <summary>弹出选项并等玩家选，返回选中的下标。</summary>
        UniTask<int> PickAsync(string[] options, CancellationToken ct);
    }
}

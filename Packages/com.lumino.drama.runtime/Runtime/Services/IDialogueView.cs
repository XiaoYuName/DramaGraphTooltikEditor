using System.Threading;
using Cysharp.Threading.Tasks;
using Drama.Runtime.Flow;
using UnityEngine;

namespace Drama.Runtime.Services
{
    /// <summary>
    /// 一句台词。<b>全是引用，没有解析好的字符串</b> —— 刻意的。
    ///
    /// 解析成 string 交给 View 的话，玩家在这句台词显示期间切语言就没人管了。
    /// 交引用，View 才能挂 Unity Localization 的绑定组件（把 <c>OnUpdateString</c>
    /// 接到打字机而不是 <c>text.text</c>），切语言自动重跑。
    ///
    /// <b>说话人名字连引用都不统一。</b> 四个来源：旁白空、主角是玩家昵称、
    /// 自定义是多语言引用、指定角色要查角色表 —— 其中<b>玩家昵称根本没有 Table/Key</b>。
    /// 而且"角色ID → 名字"是宿主配置表的事，包不该知道。
    /// 所以只交出寻址方式，View 按 <see cref="Speaker"/> 分支自己取。
    /// </summary>
    public struct DialogueLine
    {
        /// <summary>正文的多语言引用。</summary>
        public LocalizedRef TextRef;

        /// <summary>说话人寻址方式。View 按它决定名字从哪来。</summary>
        public ESpeakerKind Speaker;

        /// <summary><see cref="Speaker"/> 为 Actor 时有效，宿主自己拿它查角色表。</summary>
        public int ActorId;

        /// <summary><see cref="Speaker"/> 为 Custom 时有效，剧本里直接写的说话人名。</summary>
        public LocalizedRef SpeakerNameRef;

        public Color NameColor;
        public EBalloonKind Balloon;

        /// <summary>
        /// 本句语音的多语言引用，<c>IsEmpty</c> 表示没配语音。
        ///
        /// 同样不预解析：一是切语言时 View 要能重播，二是<b>预解析会让每句台词
        /// 都等一次 Asset Table 加载</b>——语音是一句一个、没法全量预热的。
        /// 播放走 <see cref="IDramaAudio.PlayVoice"/>，那边同样收引用。
        /// </summary>
        public LocalizedRef VoiceRef;
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
        /// <summary>
        /// 弹出选项并等玩家选，返回选中的下标。
        ///
        /// <b>收的是多语言引用而不是查好的字符串</b>，和台词（<see cref="DialogueLine.TextRef"/>）
        /// 一个口径：选项面板会一直挂在屏幕上等玩家，这期间玩家完全可能去设置里切语言。
        /// 交字符串的话那一刻就定死了，切完语言选项还是旧语种。
        /// 实现方应当把引用交给宿主的多语言组件（本工程是 <c>LocalizeStringEvent</c>），
        /// 由它自己响应语言变更。
        /// </summary>
        UniTask<int> PickAsync(LocalizedRef[] options, CancellationToken ct);
    }
}

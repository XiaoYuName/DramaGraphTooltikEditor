using Unity.GraphToolkit.Editor;
using UnityEngine;

namespace Drama.Editor
{
    /// <summary>
    /// 小游戏：让玩家去玩一段小游戏玩法，<b>过关了剧情才往下走</b>。
    ///
    /// 填的是<b>宿主小游戏枚举的整数值</b>（本作是服装小游戏 <c>ClothingMinGameType</c>，
    /// 比如熨斗=2、拼图=4、滴胶=8、宝石切割=16 …）。
    /// 这里刻意收 int 而不是某个具体枚举 —— 剧情系统是跨工程复用的包，
    /// 不该认识某个游戏有哪些小游戏。
    ///
    /// <b>没有"失败"出口</b>：玩砸了由小游戏自己弹失败界面让玩家重试，玩不过去就一直重试，
    /// 回到剧情时一定是过关的。所以这里只有一条路：玩完了，继续。
    ///
    /// <b>"玩完了"是玩家点掉成功界面之后</b>，不是玩法判定通过的那一刻 ——
    /// 中间那个成功界面要留给玩家看完。
    ///
    /// 跳过模式也照玩（关卡不是能快进的演出）；读档恢复时不玩，直接跳过这条。
    /// </summary>
    [System.Serializable]
    [Node("命令/流程", "Assets/Drama/Assets/Event.png", "小游戏")]
    public class PlayMinGameNode : DramaNode
    {
        public const string MinGameID = "MinGameID";

        protected override void OnDefinePorts(IPortDefinitionContext context)
        {
            base.OnDefinePorts(context);

            context.AddInputPort<int>(MinGameID)
                .WithDefaultValue(-1)
                .WithDisplayName("小游戏类型")
                .Build();
        }
    }
}

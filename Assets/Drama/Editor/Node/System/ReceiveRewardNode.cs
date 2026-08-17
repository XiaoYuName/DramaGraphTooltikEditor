using Unity.GraphToolkit.Editor;
using UnityEngine;

namespace Drama.Editor
{
    /// <summary>
    /// 获取奖励：按奖励表 ID 发一份奖励，并弹出「获得奖励」界面。
    ///
    /// <b>手动模式下剧情会停在这儿，等玩家把弹窗关掉再往下走</b>；
    /// 自动 / 跳过模式下弹窗自己收掉，剧情不停。
    ///
    /// <b>读档恢复时整条跳过</b> —— 奖励当年已经发过了，重放时再发一次，
    /// 玩家每读一次档就白拿一份。
    /// </summary>
    [System.Serializable]
    [Node("命令/任务", "Assets/Drama/Assets/Task.png", "获取奖励")]
    public class ReceiveRewardNode : DramaNode
    {
        public const string RewardID = "RewardID";

        protected override void OnDefinePorts(IPortDefinitionContext context)
        {
            base.OnDefinePorts(context);

            context.AddInputPort<long>(RewardID)
                .WithDefaultValue(-1)
                .WithDisplayName("奖励表ID")
                .Build();
        }
    }
}

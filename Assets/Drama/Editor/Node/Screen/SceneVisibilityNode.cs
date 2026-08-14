using Unity.GraphToolkit.Editor;
using UnityEngine;

namespace Drama.Editor
{
    /// <summary>
    /// 场景显隐：管游戏场景里那些"和剧情无关的东西"——场景 NPC、地图配置的场景默认UI。
    ///
    /// <b>进剧情时宿主默认把两者都收起来</b>（剧情要独占屏幕），所以想藏什么都不用摆这个节点；
    /// 只有想在剧情中途<b>露出来</b>的时候才需要它。
    ///
    /// <b>它改的是一个持续状态，切场景也保持。</b> 所以要让新场景露出 NPC，
    /// 把这个节点摆在「游戏场景」节点<b>前面</b>就行 —— 场景一建好就按新状态生成，不会闪。
    /// 反过来摆在后面的话，NPC 会先冒出来一帧再被收掉。
    /// </summary>
    [System.Serializable]
    [Node("命令/场景", "Assets/Drama/Assets/ChangeBG.png", "场景显隐")]
    public class SceneVisibilityNode : DramaNode
    {
        public const string ShowNpc = "ShowNpc";
        public const string ShowSceneUI = "ShowSceneUI";

        protected override void OnDefinePorts(IPortDefinitionContext context)
        {
            base.OnDefinePorts(context);

            context.AddInputPort<bool>(ShowNpc)
                .WithDefaultValue(true)
                .WithDisplayName("显示场景NPC")
                .Build();

            context.AddInputPort<bool>(ShowSceneUI)
                .WithDefaultValue(true)
                .WithDisplayName("显示场景默认UI")
                .Build();
        }
    }
}

using Unity.GraphToolkit.Editor;
using UnityEngine;

namespace Drama.Editor
{
    /// <summary>
    /// 地图显隐：把一个地图入口临时藏起来 / 放回来。
    ///
    /// 「小地图ID」填 <b>-1</b> 表示大地图上那个点本身，填具体小场景ID 就只管那一个入口 ——
    /// 和「解锁」节点的地图参数一个口径。
    ///
    /// <b>临时覆盖，不进存档</b>：最终可见 = 已解锁 && 没被藏，「显示」开不出没解锁的地图。
    /// 另外地图还有配置表里那套解锁条件（要多少钱 / 什么道具 / 星期几），
    /// 那是进不进得去的问题，和看不看得见是两层。
    /// </summary>
    [System.Serializable]
    [Node("命令/流程", "Assets/Drama/Assets/Show.png", "地图显隐")]
    public class MapVisibilityNode : DramaNode
    {
        public const string MapSceneID = "mapSceneID";
        public const string SubSceneID = "subSceneID";
        public const string Visible    = "visible";

        protected override void OnDefinePorts(IPortDefinitionContext context)
        {
            base.OnDefinePorts(context);

            context.AddInputPort<long>(MapSceneID)
                .WithDefaultValue(-1)
                .WithDisplayName("大地图ID")
                .Build();

            context.AddInputPort<long>(SubSceneID)
                .WithDefaultValue(-1)
                .WithDisplayName("小地图ID(-1=入口)")
                .Build();

            context.AddInputPort<bool>(Visible)
                .WithDefaultValue(true)
                .WithDisplayName("显示")
                .Build();
        }
    }
}

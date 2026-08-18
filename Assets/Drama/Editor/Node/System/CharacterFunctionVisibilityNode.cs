using Unity.GraphToolkit.Editor;
using UnityEngine;

namespace Drama.Editor
{
    /// <summary>
    /// 人物功能显隐：把某个角色功能面板里的<b>某一个</b>按钮临时藏起来 / 放回来。
    ///
    /// 两个参数缺一不可 —— 功能是<b>挂在角色上</b>的，"厨房"藏的是"这个角色的厨房"。
    /// 「角色功能」填的是<b>宿主那套枚举的整数值</b>（本作那个枚举是 <c>[Flags]</c>，
    /// 值是 1/2/4/8…，对照表见节点使用手册）。
    ///
    /// <b>临时覆盖，不进存档</b>，语义和「系统功能显隐」完全一致：
    /// 最终可见 = 已解锁 && 没被藏；「显示」开不出没解锁的功能。
    /// 另外角色功能还多一层过滤 —— 角色配置表里没配这个功能的，本节点也变不出来。
    /// </summary>
    [System.Serializable]
    [Node("命令/流程", "Assets/Drama/Assets/Show.png", "人物功能显隐")]
    public class CharacterFunctionVisibilityNode : DramaNode
    {
        public const string CharacterID       = "characterID";
        public const string CharacterFunction = "characterFunction";
        public const string Visible           = "visible";

        protected override void OnDefinePorts(IPortDefinitionContext context)
        {
            base.OnDefinePorts(context);

            context.AddInputPort<long>(CharacterID)
                .WithDefaultValue(-1)
                .WithDisplayName("角色ID")
                .Build();

            // 0 = 宿主枚举里的"无任何功能"，也就是没填
            context.AddInputPort<int>(CharacterFunction)
                .WithDefaultValue(0)
                .WithDisplayName("角色功能(枚举值)")
                .Build();

            context.AddInputPort<bool>(Visible)
                .WithDefaultValue(true)
                .WithDisplayName("显示")
                .Build();
        }
    }
}

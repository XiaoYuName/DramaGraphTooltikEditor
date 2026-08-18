using Unity.GraphToolkit.Editor;
using UnityEngine;

namespace Drama.Editor
{
    /// <summary>
    /// 解锁：把一个功能 / 地图入口<b>永久开放</b>给玩家。
    ///
    /// 本作的"解锁"就是<b>那个 UI 按钮显不显示</b>，没有"灰着但看得见"这一档。
    /// <b>默认什么都没解锁</b>（新档主界面那排按钮、大地图上的点、角色功能都是空的），
    /// 全靠剧本一条条开。<b>解锁跟着存档走</b>，读档之后还在。
    ///
    /// 「解锁类型」选什么，节点上就出现对应的参数：
    /// <list type="bullet">
    /// <item>系统功能 —— 一个「系统功能」值</item>
    /// <item>角色功能 —— 「角色ID」+「角色功能」两个参数（功能是挂在角色上的）</item>
    /// <item>地图 —— 「大地图ID」+「小地图ID」；小地图ID 填 <b>-1</b> 就是开大地图上那个点本身</item>
    /// </list>
    ///
    /// <b>功能值填的是宿主那套枚举的整数值</b>，不是这边的下拉框 ——
    /// 剧情系统是跨工程复用的，编辑器不该认识某个游戏有哪些系统功能 / 角色功能，
    /// 由宿主自己转回它的枚举（和「小游戏」节点收 int 是同一个道理）。
    /// 本作的对应值见《剧情系统-节点使用手册》里的对照表。
    ///
    /// 和三个「显隐」节点的区别：那三个是<b>剧情期间的临时藏 / 露</b>（不进存档，给引导用），
    /// 这个是永久进度。想让一个功能露出来，得先解锁 —— 显隐节点开不出没解锁的东西。
    ///
    /// 重复解锁没有副作用，读档静默重放期间也照常执行（幂等）。
    /// </summary>
    [System.Serializable]
    [Node("命令/流程", "Assets/Drama/Assets/Event.png", "解锁")]
    public class UnlockNode : DramaNode
    {
        public const string TargetKind        = "targetKind";
        public const string SystemFunction    = "systemFunction";
        public const string CharacterFunction = "characterFunction";
        public const string CharacterID       = "characterID";
        public const string MapSceneID        = "mapSceneID";
        public const string SubSceneID        = "subSceneID";

        protected override void OnDefinePorts(IPortDefinitionContext context)
        {
            base.OnDefinePorts(context);

            // 端口按解锁类型出现 —— 和「切换背景」节点按转场类型决定要不要淡入淡出端口一个路子
            switch (GetTargetKind())
            {
                case UnlockTargetKind.SystemFunction:
                    // 默认 -1 而不是 0：宿主枚举的 0 往往是个合法功能（本作是"地图"），
                    // 用 0 当"没填"会把它误当成填了
                    context.AddInputPort<int>(SystemFunction)
                        .WithDefaultValue(-1)
                        .WithDisplayName("系统功能(枚举值)")
                        .Build();
                    break;

                case UnlockTargetKind.CharacterFunction:
                    context.AddInputPort<long>(CharacterID)
                        .WithDefaultValue(-1)
                        .WithDisplayName("角色ID")
                        .Build();
                    // 本作那套角色功能枚举是 [Flags]，值是 1/2/4/8…；0 = 无功能 = 没填
                    context.AddInputPort<int>(CharacterFunction)
                        .WithDefaultValue(0)
                        .WithDisplayName("角色功能(枚举值)")
                        .Build();
                    break;

                case UnlockTargetKind.Map:
                    context.AddInputPort<long>(MapSceneID)
                        .WithDefaultValue(-1)
                        .WithDisplayName("大地图ID")
                        .Build();
                    context.AddInputPort<long>(SubSceneID)
                        .WithDefaultValue(-1)
                        .WithDisplayName("小地图ID(-1=入口)")
                        .Build();
                    break;
            }
        }

        protected override void OnDefineOptions(IOptionDefinitionContext context)
        {
            base.OnDefineOptions(context);

            context.AddOption<UnlockTargetKind>(TargetKind)
                .WithDefaultValue(UnlockTargetKind.SystemFunction)
                .WithDisplayName("解锁类型")
                .Build();
        }

        public UnlockTargetKind GetTargetKind()
        {
            var opt = GetNodeOptionByName(TargetKind);
            if (opt == null)
                return UnlockTargetKind.SystemFunction;   // 首次定义时选项还不存在

            opt.TryGetValue<UnlockTargetKind>(out var kind);
            return kind;
        }
    }
}

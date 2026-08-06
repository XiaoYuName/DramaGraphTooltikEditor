# GraphToolkit 常用 API 教程（中文）

> 配套文档：[`GraphToolkit.md`](GraphToolkit.md) 是**全量 API 速查表**（按类型罗列）。
> 本文是**任务导向教程**：按"我想做什么"组织，每条都给可直接抄的完整代码。
>
> 命名空间：`using Unity.GraphToolkit.Editor;`
> 适用：Unity 6000.4+（本工程 6000.7.0a3）

---

## 目录

| # | 章节 | 一句话 |
|---|---|---|
| 0 | [最小可运行示例](#0-最小可运行示例) | 30 行跑通一个图 |
| 1 | [定义图 Graph](#1-定义图-graph) | `[Graph]` + 创建菜单 |
| 2 | [定义节点 Node](#2-定义节点-node) | `[Node]` + 两个 OnDefine |
| 3 | [端口 Port 全解](#3-端口-port-全解) | ★ 最常用，builder 全部方法 |
| 4 | [节点选项 Option](#4-节点选项-option) | 不占端口的配置项 |
| 5 | [读值与遍历图](#5-读值与遍历图) | ★ 烘焙/求值的标准套路 |
| 6 | [容器节点 Context / Block](#6-容器节点-context--block) | 一段对话装多句台词 |
| 7 | [变量与子图](#7-变量与子图) | Blackboard、剧情段复用 |
| 8 | [校验与报错](#8-校验与报错) | 节点上打红叉 |
| 9 | [右键菜单与工具栏](#9-右键菜单与工具栏) | 编辑器扩展 |
| 10 | [外观定制](#10-外观定制) | 颜色、图标、USS |
| 11 | [代码建图](#11-代码建图) | 脚本自动生成图 |
| 12 | [运行时烘焙](#12-运行时烘焙-scriptedimporter) | ★ 架构关键 |
| 13 | [运行时调试可视化](#13-运行时调试可视化) | 节点跑马灯、进度条 |
| 14 | [速查：我想…→用…](#14-速查我想用) | 索引表 |

---

## 0. 最小可运行示例

放到 `Assets/Xxx/Editor/` 下（或有 Editor asmdef 的程序集），保存后就能用。

```csharp
using Unity.GraphToolkit.Editor;
using UnityEditor;
using UnityEngine;

namespace Demo.Editor
{
    // ① 定义图类型：扩展名 "demograph" 必须全项目唯一
    [Graph("demograph")]
    internal class DemoGraph : Graph
    {
        [MenuItem("Assets/Create/Demo/示例图")]
        static void Create() =>
            GraphDatabase.PromptInProjectBrowserToCreateNewAsset<DemoGraph>("新建示例图");
    }

    // ② 定义一个节点
    [Node("示例/加法", null, "加法")]     // 节点库路径 / 图标 / 显示标题
    [UseWithGraph(typeof(DemoGraph))]     // 只在 DemoGraph 里出现
    [System.Serializable]
    internal class AddNode : Node
    {
        protected override void OnDefinePorts(IPortDefinitionContext ctx)
        {
            ctx.AddInputPort<float>("a").WithDisplayName("加数 A").WithDefaultValue(0f).Build();
            ctx.AddInputPort<float>("b").WithDisplayName("加数 B").WithDefaultValue(0f).Build();
            ctx.AddOutputPort<float>("result").WithDisplayName("结果").Build();
        }
    }
}
```

在 Project 窗口右键 → Create → Demo → 示例图，双击打开，空格/右键在画布加节点。

---

## 1. 定义图 Graph

### 1.1 `[Graph]` 特性

```csharp
[Graph(extension: "agv", options: GraphOptions.SupportsSubgraphs)]
internal class DramaGraph : Graph { }
```

| 参数 | 说明 |
|---|---|
| `extension` | 资产文件扩展名，**不带点**，**全项目唯一**（Unity 靠它选 importer） |
| `options` | 可省略，默认 `GraphOptions.Default` |

`GraphOptions`（可按位或）：

| 值 | 作用 | 什么时候用 |
|---|---|---|
| `Default` / `None` | 默认 | 一般情况 |
| `SupportsSubgraphs` | 允许子图 | 剧情段要复用时 |
| `DisableAutoInclusionOfNodesFromGraphAssembly` | 关闭"同程序集 Node 自动进节点库" | 一个程序集里有多种图时 |

> **默认行为很重要**：和 Graph **同一程序集**里的所有 `Node` 子类，会自动出现在这个图的节点库中，**不需要加 `[UseWithGraph]`**。
> 本工程目前没有 asmdef，编辑器代码全在 `Assembly-CSharp-Editor`，所以是"全自动收录"。将来做第二种图时会互相污染，届时要么拆 asmdef，要么开这个 flag + 逐个标 `[UseWithGraph]`。

### 1.2 创建资产的菜单

```csharp
[MenuItem("Assets/Create/Drama/剧情编辑器")]
static void CreateAssetFile() =>
    GraphDatabase.PromptInProjectBrowserToCreateNewAsset<DramaGraph>("DramaGraph");
```
`PromptInProjectBrowserToCreateNewAsset` 会在 Project 窗口新建文件并**自动进入重命名状态**，体验最好。

### 1.3 生命周期钩子

```csharp
internal class DramaGraph : Graph
{
    public override void OnEnable()  { /* 图被创建 或 被加载进编辑器 */ }
    public override void OnDisable() { /* 图被卸载 / 关闭 */ }

    // 图发生任何变更后调用 —— 做全图校验就在这里
    public override void OnGraphChanged(GraphLogger logger) { }

    // 拦截连线：返回 false 则这条线连不上
    public override bool IsConnectionAllowed(IPort output, IPort input) => true;

    // 定义"引用本图的子图节点"上有哪些选项
    protected override void OnDefineSubgraphNodeOptions(Node.IOptionDefinitionContext ctx) { }
}
```

### 1.4 限制 Blackboard / 常量节点里能选哪些类型

> ⚠️ **本地 6000.7.0a3 还没有这组 API**（反射验证过），[6000.7 在线文档](https://docs.unity3d.com/6000.7/Documentation/ScriptReference/Unity.GraphToolkit.Editor.Graph.html)已经有了 —— 升级 Editor 后可用。先记着，别现在写。

默认情况下，Blackboard 新建变量、节点库新建常量时列出的类型，是**从图内所有节点的端口定义自动推导**出来的。剧情图里通常会混进一堆你不想让策划选的类型（比如 `Untyped`、内部用的 `DramaProt`）。这两个方法就是用来收窄的：

```csharp
internal class DramaGraph : Graph
{
    // Blackboard 变量创建菜单
    protected override IEnumerable<Type> BuildAvailableVariableTypes(
        IReadOnlyCollection<Type> baseSupportedTypes)
    {
        foreach (var t in baseSupportedTypes)
            if (t != typeof(Untyped) && t != typeof(DramaProt))   // 剔除内部类型
                yield return t;

        yield return typeof(Vector3);                              // 追加
    }

    // 节点库里的常量节点菜单
    protected override IEnumerable<Type> BuildAvailableConstantTypes(
        IReadOnlyCollection<Type> baseSupportedTypes)
    {
        // 剧情图只让建这四种常量
        yield return typeof(long);
        yield return typeof(string);
        yield return typeof(float);
        yield return typeof(Color);
    }
}
```

配套只读属性：`graph.SupportedTypes`、`graph.AvailableVariableTypes`、`graph.AvailableConstantTypes`。

| 坑 | 说明 |
|---|---|
| **别在这两个方法里读 `SupportedTypes`** | 循环依赖，抛 `InvalidOperationException`。用参数 `baseSupportedTypes` |
| 返回 `null` | 表示一个类型都不提供 |
| **只管 UI** | `graph.CreateVariable` / `CreateConstantNode` 用代码建任意类型都不受限 |

### 1.5 `GraphDatabase` 静态方法

| 方法 | 用途 |
|---|---|
| `GraphDatabase.CreateGraph<T>(assetPath)` | 代码创建图资产，返回实例 |
| `GraphDatabase.LoadGraph<T>(assetPath)` | 加载图（编辑器场景用） |
| `GraphDatabase.LoadGraphForImporter<T>(assetPath)` | **从磁盘读一份全新实例，专供 ScriptedImporter** |
| `GraphDatabase.SaveGraph(graph)` | 有改动才写盘 |
| `GraphDatabase.GetGraphAssetPath(graph)` | 取资产路径 |
| `GraphDatabase.GetGraphAssetGUID(graph)` | 取资产 GUID |
| `GraphDatabase.PromptInProjectBrowserToCreateNewAsset<T>(name)` | 交互式新建 |

> `LoadGraph` 和 `LoadGraphForImporter` 的区别：后者不走编辑器缓存，保证拿到磁盘上的最新内容，**导入器里必须用它**，否则会拿到脏数据。

---

## 2. 定义节点 Node

### 2.1 `[Node]` 特性的 4 个参数

```csharp
[Node("剧情/台词", "Assets/Drama/Icons/talk.png", "台词", "Assets/Drama/UI/Talk.uss")]
```

| 位置 | 参数 | 说明 | 省略后 |
|---|---|---|---|
| 1 | `categoryPath` | 节点库里的分类路径，`/` 分层 | 必填 |
| 2 | `iconPath` | 图标文件路径 | 无图标，传 `null` |
| 3 | `title` | 节点库条目名 + 实例化后的节点标题 | 用类名 |
| 4 | `stylesheet` | `.uss` 样式表路径 | 默认样式 |

四个重载：`(cat)`、`(cat, icon)`、`(cat, icon, title)`、`(cat, icon, title, uss)` —— 想只填标题就得把 icon 传 `null`。

### 2.2 节点骨架

```csharp
[Node("剧情/台词", null, "台词")]
[UseWithGraph(typeof(DramaGraph))]   // 可选：限定只在某些图里可用
[System.Serializable]                 // ★ 别忘了，节点要被 Unity 序列化
public class TalkNode : DramaNode
{
    // 端口名/选项名建议用 const，避免字符串散落
    const string k_Text  = "Text";
    const string k_Speed = "Speed";

    protected override void OnDefinePorts(IPortDefinitionContext ctx)
    {
        base.OnDefinePorts(ctx);      // 继承基类端口时记得调
        ctx.AddInputPort<string>(k_Text).WithDisplayName("台词").AsTextArea(2, 6).Build();
    }

    protected override void OnDefineOptions(IOptionDefinitionContext ctx)
    {
        base.OnDefineOptions(ctx);
        ctx.AddOption<float>(k_Speed).WithDisplayName("打字速度").WithDefaultValue(30f).Build();
    }

    public override void OnEnable()  { }   // 节点被创建 / 图被启用
    public override void OnDisable() { }   // 节点被删除 / 图被禁用
}
```

### 2.3 节点的运行期属性（可随时读写）

```csharp
node.Title    = "台词 #3";       // 头部主标题
node.Subtitle = "第一章";         // 头部副标题
node.Tooltip  = "悬停提示";
node.DefaultColor = new Color(0.2f, 0.6f, 1f);   // 顶部高亮条颜色
node.FillAmount = 60f;           // 进度条，0~100（不是 0~1）
node.Position = new Vector2(100, 200);

Hash128 id   = node.ID;          // 全局唯一 ID
Graph   g    = node.Graph;
bool connected = node.IsConnected;

node.DefineNode();               // 强制重建端口/选项（动态端口时手动调）
node.RemoveFromGraph();
```

> 已知 bug（官方承认）：**只改 `Subtitle` 不会触发重绘**，端口/选项配置变化才会。

### 2.4 `[UseWithGraph]`

```csharp
[UseWithGraph(typeof(DramaGraph), typeof(QuestGraph))]   // 支持多个
```
不加的话，靠"同程序集自动收录"。一个程序集只有一种图时可以不加。

---

## 3. 端口 Port 全解

★ 这是日常写得最多的部分。

### 3.1 四种创建方式

```csharp
protected override void OnDefinePorts(IPortDefinitionContext ctx)
{
    ctx.AddInputPort<float>("a")     // 泛型：最常用
    ctx.AddInputPort("a")            // 弱类型：需再 .WithDataType<T>() 或 .WithDataType(type)
    ctx.AddOutputPort<float>("r")
    ctx.AddOutputPort("r")
}
```

弱类型版本用于**类型在运行期才确定**的场景：

```csharp
Type t = GetTypeFromSomewhere();
ctx.AddInputPort("dyn").WithDataType(t).WithDisplayName("动态端口").Build();
```

### 3.2 Builder 方法总表

**输入/输出都有**（来自 `IPortBuilder<T>`）：

| 方法 | 说明 | 示例 |
|---|---|---|
| `.WithDisplayName(string)` | UI 上显示的标签（`portName` 只是内部标识） | `.WithDisplayName("台词")` |
| `.WithTooltip(string)` | 悬停提示 | `.WithTooltip("留空则用默认")` |
| `.WithConnectorUI(PortConnectorUI)` | 连接点形状：`Circle`(默认) / `Arrowhead` | `.WithConnectorUI(PortConnectorUI.Arrowhead)` |
| `.WithCapacity(PortCapacity)` | 可接连线数：`None` / `Single` / `Multi` | `.WithCapacity(PortCapacity.Multi)` |
| `.AsVertical()` | 竖向端口（连线从上往下走） | 做执行流很合适 |
| `.Build()` | **★ 必须调用，忘了端口不出现** | |

**仅输入端口额外有**（来自 `IInputBasePortBuilder<T>`）：

| 方法 | 说明 |
|---|---|
| `.WithDefaultValue(T)` | 未连线时端口内嵌编辑框的默认值 |
| `.Delayed()` | 等同 `[Delayed]`，回车/失焦才提交 |
| `.AsTextArea(minLines = 3, maxLines = 3)` | 等同 `[TextArea]`，多行文本框 |

> 输出端口**没有** `WithDefaultValue` —— 输出值由你的求值逻辑决定，不由 UI 填。

### 3.3 端口命名规则

- `portName` 在**同一节点、同一方向**内必须唯一。
- 输入和输出**可以重名**（两套命名空间），所以 `DramaNode` 里输入输出都叫 `"DramaProtName"` 是合法的。
- `GetInputPortByName("x")` / `GetOutputPortByName("x")` 按 `portName` 查，不是 `DisplayName`。

### 3.4 支持的端口类型

| 类别 | 例子 | 备注 |
|---|---|---|
| 基础类型 | `int` `long` `float` `bool` `string` | |
| Unity 类型 | `Vector2/3/4` `Color` `Quaternion` `AnimationCurve` | |
| 枚举 | `EBallonKind` | 渲染成下拉框；带 `[Flags]` 渲染成 MaskField 多选 |
| 自定义类 | `DramaProt` | **必须 `[Serializable]`** |
| UnityEngine.Object | `Sprite` `AudioClip` `GameObject` | 对象选择框 |
| **集合** | `List<int>` `string[]` `Vector2[]` | Inspector 可动态增删、逐项编辑 |
| **Untyped** | `Untyped` | **不携带数据的纯流程端口** |

```csharp
// 集合端口
ctx.AddInputPort<List<string>>("choices").WithDisplayName("选项列表").Build();
ctx.AddOutputPort<DramaProt[]>("branches").Build();

// 纯流程端口（做执行流用这个，而不是拿数据端口凑合）
ctx.AddInputPort<Untyped>("in").WithConnectorUI(PortConnectorUI.Arrowhead)
                               .WithCapacity(PortCapacity.Multi).AsVertical().Build();
ctx.AddOutputPort<Untyped>("out").WithConnectorUI(PortConnectorUI.Arrowhead)
                                 .WithCapacity(PortCapacity.Single).AsVertical().Build();
```

> ⚠️ Untyped 端口的 `DataType` 是 `typeof(Untyped)`，**不是 `null`**（6.6 起的破坏性变更）。遍历图判类型时注意。

### 3.5 端口容量的典型搭配

| 场景 | 输入 | 输出 |
|---|---|---|
| 执行流（一进多出汇聚） | `Multi` | `Single` |
| 数据流 | `Single`（一个输入只能有一个值源） | `Multi`（一个值可分发给多处） |
| 纯展示 | `None` | `None` |

### 3.6 完整示例：一个分支节点

```csharp
[Node("剧情/分支", null, "分支选择")]
[System.Serializable]
public class BranchNode : Node
{
    protected override void OnDefinePorts(IPortDefinitionContext ctx)
    {
        ctx.AddInputPort<Untyped>("in")
           .WithDisplayName("流入")
           .WithConnectorUI(PortConnectorUI.Arrowhead)
           .WithCapacity(PortCapacity.Multi)
           .Build();

        ctx.AddInputPort<string>("question")
           .WithDisplayName("问题")
           .AsTextArea(2, 4)
           .Build();

        ctx.AddInputPort<List<string>>("options")
           .WithDisplayName("选项")
           .Build();

        ctx.AddOutputPort<Untyped>("outA")
           .WithDisplayName("选项 A")
           .WithConnectorUI(PortConnectorUI.Arrowhead)
           .WithCapacity(PortCapacity.Single)
           .Build();

        ctx.AddOutputPort<Untyped>("outB")
           .WithDisplayName("选项 B")
           .WithConnectorUI(PortConnectorUI.Arrowhead)
           .WithCapacity(PortCapacity.Single)
           .Build();
    }
}
```

---

## 4. 节点选项 Option

**Option 不是端口** —— 它不能连线，只是节点头部（或 Inspector）里的一个字段。适合放"配置"而非"数据流"。

```csharp
protected override void OnDefineOptions(IOptionDefinitionContext ctx)
{
    ctx.AddOption<long>("EventID")
       .WithDisplayName("事件ID")
       .WithTooltip("事件的唯一ID，-1 表示不触发")
       .WithDefaultValue(-1L)
       .Delayed()               // 回车才提交，避免边打字边刷新
       .Build();

    ctx.AddOption<bool>("isSpeaker")
       .WithDisplayName("自定义说话人")
       .Build();

    ctx.AddOption<string>("note")
       .WithDisplayName("备注")
       .AsTextArea(3, 8)
       .ShowInInspectorOnly()   // 只在 Inspector 显示，不占节点头部空间
       .Build();

    // 弱类型版本
    ctx.AddOption("dyn", typeof(int)).WithDefaultValue(0).Build();
}
```

| 方法 | 说明 |
|---|---|
| `.WithDisplayName(string)` | 显示名 |
| `.WithTooltip(string)` | 提示 |
| `.WithDefaultValue(T)` | 默认值（必须 Unity 可序列化） |
| `.Delayed()` | 延迟提交 |
| `.AsTextArea(min, max)` | 多行文本 |
| `.ShowInInspectorOnly()` | **只在 Inspector 显示** |
| `.Build()` | ★ 必须 |

### Port 还是 Option？怎么选

| 判断 | 选 |
|---|---|
| 这个值需要从别的节点连过来吗？ | 需要 → **Port** |
| 这个值永远是手填的常量配置吗？ | 是 → **Option** |
| 想节省节点宽度、藏进 Inspector？ | **Option + ShowInInspectorOnly()** |

> 你现在 `TalkNode` 里的 `对话框动效` / `自动等待` / `名字颜色` / `说话人` / `立绘槽位` 都做成了 Port（还各配了一个同名 Output）。如果这些值实际上从不从别处连线，改成 Option 会让节点清爽很多。

---

## 5. 读值与遍历图

★ 烘焙、校验、求值都靠这一节。

### 5.1 取端口的值

```csharp
IPort p = node.GetInputPortByName("Text");

if (p.TryGetValue<string>(out var text))
{
    // 成功：端口【未连线】，text 是用户在端口内嵌框里填的值
}
else
{
    // 失败：端口【已连线】，值来自上游节点 —— 需要递归求值
}
```

> **这是最容易踩的坑**：`TryGetValue` 在端口**已连线**时返回 `false`，这是设计如此，不是 bug。

### 5.2 取选项的值

```csharp
node.GetNodeOptionByName("EventID").TryGetValue<long>(out var eventId);   // 总是可读
```

### 5.3 顺着连线走

```csharp
IPort input = node.GetInputPortByName("Text");

// 单个上游
if (input.IsConnected)
{
    IPort upstreamOut = input.FirstConnectedPort;     // 上游的输出端口
    INode upstreamNode = upstreamOut.GetNode();       // ← INodeExtensions 扩展方法
}

// 多个下游
var list = new List<IPort>();
node.GetOutputPortByName("out").GetConnectedPorts(list);
foreach (var downstreamIn in list)
{
    INode next = downstreamIn.GetNode();
}
```

### 5.4 标准求值套路（递归）

```csharp
static T Evaluate<T>(IPort inputPort, T fallback = default)
{
    if (!inputPort.IsConnected)
        return inputPort.TryGetValue<T>(out var v) ? v : fallback;

    var srcPort = inputPort.FirstConnectedPort;
    var srcNode = srcPort.GetNode();

    switch (srcNode)
    {
        case IConstantNode c:                        // 常量节点
            return c.TryGetValue<T>(out var cv) ? cv : fallback;

        case IVariableNode vn:                       // 变量节点
            return vn.Variable.TryGetDefaultValue<T>(out var vv) ? vv : fallback;

        case LocalizationNode loc:                   // 你自己的节点：自己算
            return (T)(object)BuildLocalization(loc);

        default:
            return fallback;
    }
}
```

### 5.5 遍历全图

```csharp
foreach (INode n in graph.GetNodes())
{
    switch (n)
    {
        case StartDramaNode start: /* ... */ break;
        case TalkNode talk:        /* ... */ break;
    }
}

// 也可以按下标
for (int i = 0; i < graph.NodeCount; i++) { var n = graph.GetNode(i); }
```

### 5.6 沿执行流走一遍（对话系统最常用）

```csharp
static INode FindStart(Graph g) =>
    g.GetNodes().FirstOrDefault(n => n is StartDramaNode);

static INode Next(INode cur, string outPortName = "out")
{
    var op = cur.GetOutputPortByName(outPortName);
    if (op == null || !op.IsConnected) return null;

    var buf = new List<IPort>();
    op.GetConnectedPorts(buf);
    return buf.Count > 0 ? buf[0].GetNode() : null;
}

// 用法
var visited = new HashSet<Hash128>();
for (var n = FindStart(graph); n != null; n = Next(n))
{
    if (!visited.Add(n.ID)) break;   // ★ 防成环死循环
    Bake(n);
}
```

---

## 6. 容器节点 Context / Block

`ContextNode` = 一个能装 `BlockNode` 的容器，Block 在容器里**纵向排列、可拖动排序**。
对话系统里非常好用：**一段对话 = 一个 Context，里面每句台词 = 一个 Block**，比一长串独立节点连线清爽得多。

```csharp
[Node("剧情/对话段", null, "对话段")]
[System.Serializable]
public class DialogueContext : ContextNode
{
    protected override void OnDefinePorts(IPortDefinitionContext ctx)
    {
        ctx.AddInputPort<Untyped>("in").WithConnectorUI(PortConnectorUI.Arrowhead).Build();
        ctx.AddOutputPort<Untyped>("out").WithConnectorUI(PortConnectorUI.Arrowhead).Build();
    }
}

[Node("剧情/台词块", null, "台词")]
[UseWithContext(typeof(DialogueContext))]   // 限定只能放进哪些 Context
[System.Serializable]
public class LineBlock : BlockNode
{
    protected override void OnDefineOptions(IOptionDefinitionContext ctx)
    {
        ctx.AddOption<string>("text").WithDisplayName("台词").AsTextArea(2, 5).Build();
    }
}
```

### Context 的操作 API

```csharp
ctx.CreateBlockNode<LineBlock>(index: -1);      // -1 = 追加到末尾
ctx.CreateBlockNode(typeof(LineBlock), 0);      // 插到最前
ctx.AddBlockNode(existingBlock);
ctx.InsertBlockNode(2, existingBlock);
ctx.RemoveBlockNode(block);
ctx.ClearBlockNodes();

BlockNode b = ctx.GetBlock(0);
int  n      = ctx.BlockCount;
foreach (BlockNode blk in ctx.BlockNodes) { }
```

### Block 侧

```csharp
ContextNode parent = block.ContextNode;
int order          = block.Index;     // 在容器里的顺序 —— 烘焙时直接按它排序
```

---

## 7. 变量与子图

### 7.1 变量（Blackboard）

```csharp
// 创建
IVariable v = graph.CreateVariable<long>("DramaID", -1L, VariableKind.Local);
IVariable v2 = graph.CreateVariable("Color", typeof(Color), Color.white, VariableKind.Local);

// 读写
v.Name = "剧情ID";
v.DataType = typeof(int);
v.VariableKind = VariableKind.Input;
v.TryGetDefaultValue<long>(out var dv);
v.TrySetDefaultValue(100L);

// 查询引用
var nodes = new List<IVariableNode>();
v.GetNodes(nodes);
int count = v.NodeCount;
bool used = v.IsConnected;

// 删除
v.RemoveFromGraph(forceRemove: true);            // true = 连引用它的节点一起删
graph.RemoveVariable(v, forceRemove: false);     // false = 有引用就删不掉，返回 false

// 遍历（★ 按 Blackboard 显示顺序）
foreach (var x in graph.GetVariables(SortMethod.Display)) { }
foreach (var x in graph.GetVariables(SortMethod.Creation)) { }

// 拖到画布上变成节点
IVariableNode vn = graph.AddVariableNode(v, new Vector2(100, 100), VariableNodeMode.Get);
```

`VariableKind`：

| 值 | 含义 |
|---|---|
| `Local` | 仅本图内使用 |
| `Input` | 作为**子图的入参** —— 会变成 SubgraphNode 上的输入端口 |
| `Output` | 作为**子图的出参** |

`VariableNodeMode`：`Get`（默认，只读）/ `Set`（多一个输入端口用来写值）。

### 7.2 子图

```csharp
// 主图开启支持
[Graph("agv", GraphOptions.SupportsSubgraphs)]
internal class DramaGraph : Graph { }

// 子图类型
[Graph("agvsub")]
[Subgraph(typeof(DramaGraph))]        // 声明它是 DramaGraph 的子图
internal class DramaSubGraph : Graph { }
```

```csharp
// 引用一个已存在的图资产
ISubgraphNode sn = graph.AddSubgraphNode(subgraphAsset, new Vector2(200, 0));

// 创建"本地子图"（数据内嵌在主图资产里，不单独出文件）
ISubgraphNode local = graph.CreateLocalSubgraphNode<DramaSubGraph>("战斗前对话", new Vector2(0, 0));

// 反查
Graph inner = sn.GetSubgraph();
```

子图里 `VariableKind.Input` / `Output` 的变量会**自动变成 SubgraphNode 上的端口**，这就是子图的传参机制。

---

## 8. 校验与报错

```csharp
internal class DramaGraph : Graph
{
    public override void OnGraphChanged(GraphLogger logger)
    {
        int startCount = 0;

        foreach (var n in GetNodes())
        {
            if (n is StartDramaNode) startCount++;

            if (n is TalkNode talk)
            {
                var loc = talk.GetInputPortByName("LocalizationProt");
                if (loc is { IsConnected: false })
                    logger.LogError("台词节点未绑定多语言", talk);

                talk.GetNodeOptionByName("EventID").TryGetValue<long>(out var id);
                if (id < 0)
                    logger.LogWarning("EventID 未设置", talk);
            }
        }

        if (startCount == 0) logger.LogError("缺少 StartDramaNode", this);
        if (startCount > 1)  logger.LogError($"存在 {startCount} 个起点节点", this);
    }
}
```

| 方法 | 效果 |
|---|---|
| `logger.Log(msg, context)` | 蓝色信息标记 |
| `logger.LogWarning(msg, context)` | 黄色警告 |
| `logger.LogError(msg, context)` | 红色错误 |

`context` 传节点实例 → 标记直接画在那个节点上；传 `this`(图) → 图级别错误。

### 带"一键修复"按钮的报错

```csharp
logger.LogError(
    "EventID 未设置",
    talk,
    new GraphLogAction("自动分配一个 ID", obj =>
    {
        if (obj is TalkNode t)
            t.GetNodeOptionByName("EventID").TrySetValue(GenerateNewId());
    }));
```

### 拦截非法连线

```csharp
public override bool IsConnectionAllowed(IPort output, IPort input)
{
    // 禁止把台词节点直接连回起点
    if (output.GetNode() is TalkNode && input.GetNode() is StartDramaNode)
        return false;

    return base.IsConnectionAllowed(output, input);
}
```

---

## 9. 右键菜单与工具栏

### 9.1 画布右键菜单

```csharp
[GraphMenu(typeof(DramaGraph))]
static void BuildCanvasMenu(GraphMenuContext c)
{
    // 点在节点上时才出现
    if (c.ClickedObject is INode node)
    {
        c.AppendAction("剧情/复制事件ID", () => EditorGUIUtility.systemCopyBuffer = node.ID.ToString());
    }

    c.AppendSeparator("");                       // 空串 = 根菜单分隔线
    c.AppendAction("剧情/校验全图", () => Validate(c.Graph));

    // 带状态回调：控制灰显/勾选
    c.AppendAction("剧情/导出",
        act => Export(c.Graph),
        act => c.Graph.NodeCount > 0
                 ? DropdownMenuAction.Status.Normal
                 : DropdownMenuAction.Status.Disabled,
        userData: null);
}
```

`GraphMenuContext` 成员：

| 成员 | 说明 |
|---|---|
| `Graph` | 被右键的图 |
| `ClickedObject` | 光标下的元素，点空白处是 `null`。用 `is INode` / `is IPort` 判断 |
| `MousePosition` | 右键时的世界坐标（新建节点时定位用） |
| `AppendAction(name, action)` | 加一项，`/` 分层做子菜单 |
| `AppendSeparator(subMenuPath)` | 加分隔线 |

### 9.2 Blackboard 右键菜单

```csharp
[BlackboardMenu(typeof(DramaGraph))]
static void BuildBlackboardMenu(GraphMenuContext c)
{
    c.AppendAction("批量新建剧情变量", () => { });
}
```

### 9.3 工具栏按钮

```csharp
[GraphToolbarElement("drama-export", typeof(DramaGraph), 100)]   // id / 图类型 / 排序(小的在前)
static VisualElement CreateExportButton()
{
    var btn = new Button(() => Debug.Log("导出")) { text = "导出剧情" };
    return btn;
}
```
返回任意 `VisualElement` 都行，可以是下拉菜单、开关、文本框。

---

## 10. 外观定制

### 10.1 节点颜色

```csharp
public override void OnEnable()
{
    DefaultColor = new Color(0.9f, 0.5f, 0.2f);   // 节点顶部高亮条
}
```

### 10.2 自定义数据类型的端口配色/图标

```csharp
[DataTypeStyleMapper(typeof(DramaGraph))]
public class DramaStyles : DataTypeStyleMapper
{
    public DramaStyles()
    {
        var icon = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Drama/Icons/prot.png");
        Register(typeof(DramaProt),             icon, new Color(0.3f, 0.8f, 1f));
        Register(typeof(DramaLocalizationProt), null, new Color(1f, 0.8f, 0.3f));
    }
}
```
> 目前**不支持**基类/泛型匹配（要一个个精确注册），Untyped 端口的样式也改不了 —— 官方在路线图上。

### 10.3 USS 样式表

```csharp
[Node("剧情/台词", null, "台词", "Assets/Drama/UI/TalkNode.uss")]
```

---

## 11. 代码建图

适合"已有数据 → 自动生成一张图"的场景（比如从旧的 Excel 剧情表批量迁移）。

```csharp
[MenuItem("Tools/Drama/从数据生成剧情图")]
static void GenerateGraph()
{
    var graph = GraphDatabase.CreateGraph<DramaGraph>("Assets/Drama/Assets/Generated.agv");

    graph.UndoBeginRecordGraph("生成剧情图");      // ★ 成对使用
    try
    {
        // 变量
        var dramaId = graph.CreateVariable<long>("DramaID", 1001L, VariableKind.Local);
        graph.AddVariableNode(dramaId, new Vector2(-200, 0));

        // 节点
        var start = new StartDramaNode { Position = new Vector2(0, 0) };
        graph.AddNode(start);

        Node prev = start;
        for (int i = 0; i < 5; i++)
        {
            var talk = new TalkNode { Position = new Vector2(300 * (i + 1), 0), Title = $"台词 {i}" };
            graph.AddNode(talk);

            graph.Connect(
                prev.GetOutputPortByName(DramaNode.NodeProtName),
                talk.GetInputPortByName(DramaNode.NodeProtName));

            prev = talk;
        }

        // 常量节点
        graph.CreateConstantNode<float>(new Vector2(0, 200), 1.5f);
    }
    finally
    {
        graph.UndoEndRecordGraph();               // ★ 提交 Undo + 刷新视图
    }

    GraphDatabase.SaveGraph(graph);
}
```

| API | 说明 |
|---|---|
| `graph.AddNode(node)` | 加节点（先 `new` 出来，设好 `Position`） |
| `graph.RemoveNode(node)` | 删节点 |
| `graph.Connect(outPort, inPort)` | 连线，已存在返回 `false` |
| `graph.Disconnect(outPort, inPort)` | 断线 |
| `graph.GetWire(outPort, inPort)` | 取连线对象，未连接返回 `null` |
| `graph.CreateConstantNode<T>(pos, value)` | 常量节点 |
| `graph.UndoBeginRecordGraph(name)` / `UndoEndRecordGraph()` | **脚本改图必须包起来**，否则不进 Undo 栈、视图不刷新 |

---

## 12. 运行时烘焙（ScriptedImporter）

★ **这是整个架构的关键。** GraphToolkit 是 Editor-only，打包时会被剥离，官方明确表示短期内不做运行时支持。唯一正解是导入时把图**烘焙**成你自己的运行时数据。

### 12.1 运行时数据模型（放 `Assets/Drama/Runtime/`，不引用任何 Editor 代码）

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Drama.Runtime
{
    [Serializable]
    public abstract class DramaRuntimeNode
    {
        public long EventID;
        public int  NextIndex = -1;      // 用下标而不是引用，序列化最省事
    }

    [Serializable]
    public class TalkRuntimeNode : DramaRuntimeNode
    {
        public string Table;
        public string Key;
        public int    Speaker;
        public int    ActorSlot;
        public Color  NameColor;
        public float  WaitMs;
    }

    public class DramaRuntimeGraph : ScriptableObject
    {
        public long DramaId;
        public int  StartIndex = -1;

        [SerializeReference]                       // ★ 多态列表必须用它
        public List<DramaRuntimeNode> Nodes = new();
    }
}
```

### 12.2 导入器（放 `Assets/Drama/Editor/`）

```csharp
using Drama.Runtime;
using Unity.GraphToolkit.Editor;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace Drama.Editor
{
    [ScriptedImporter(version: 1, ext: DramaGraph.AssetExtension)]
    internal class DramaGraphImporter : ScriptedImporter
    {
        public override void OnImportAsset(AssetImportContext ctx)
        {
            // ★ 必须用 LoadGraphForImporter，不能用 LoadGraph
            var graph = GraphDatabase.LoadGraphForImporter<DramaGraph>(ctx.assetPath);
            if (graph == null) return;

            var runtime = ScriptableObject.CreateInstance<DramaRuntimeGraph>();
            runtime.name = System.IO.Path.GetFileNameWithoutExtension(ctx.assetPath);

            Bake(graph, runtime, ctx);

            ctx.AddObjectToAsset("RuntimeGraph", runtime);
            ctx.SetMainObject(runtime);
        }

        static void Bake(DramaGraph graph, DramaRuntimeGraph rt, AssetImportContext ctx)
        {
            // 第一遍：建索引
            var indexOf = new System.Collections.Generic.Dictionary<Hash128, int>();
            foreach (var n in graph.GetNodes())
            {
                if (n is TalkNode talk)
                {
                    indexOf[talk.ID] = rt.Nodes.Count;
                    rt.Nodes.Add(BakeTalk(talk));
                }
                else if (n is StartDramaNode start)
                {
                    // 起点不进列表，只记它指向谁
                }
            }

            // 第二遍：连 NextIndex
            // ...（顺着 out 端口找下游，查 indexOf）

            if (rt.StartIndex < 0)
                ctx.LogImportError("剧情图缺少起点节点");
        }

        static TalkRuntimeNode BakeTalk(TalkNode talk)
        {
            var node = new TalkRuntimeNode();

            talk.GetNodeOptionByName(DramaNode.EventIDName).TryGetValue<long>(out var eid);
            node.EventID = eid;

            // 未连线的端口直接取内嵌值
            talk.GetInputPortByName("ActorSlot")?.TryGetValue<int>(out node.ActorSlot);
            talk.GetInputPortByName("WaitMs")?.TryGetValue<float>(out node.WaitMs);
            talk.GetInputPortByName("NameColor")?.TryGetValue<Color>(out node.NameColor);

            // 已连线的端口：顺着线去上游节点取
            var locPort = talk.GetInputPortByName("LocalizationProt");
            if (locPort is { IsConnected: true } &&
                locPort.FirstConnectedPort.GetNode() is LocalizationNode loc)
            {
                loc.GetInputPortByName("Table").TryGetValue<string>(out node.Table);
                loc.GetInputPortByName("Key").TryGetValue<string>(out node.Key);
            }

            return node;
        }
    }
}
```

### 12.3 关键点

| 点 | 说明 |
|---|---|
| `LoadGraphForImporter` | **不是** `LoadGraph`。前者绕过编辑器缓存，保证读到磁盘最新内容 |
| `[SerializeReference]` | 运行时节点列表要多态，必须用它，不能用 `[SerializeField]` |
| `ctx.AddObjectToAsset` + `SetMainObject` | 让 `.agv` 在 Project 里直接显示为运行时资产，可以拖到 Inspector 引用 |
| `version` | 烘焙逻辑改了就 +1，触发全量重导入 |
| `ctx.LogImportError/Warning` | 导入期报错会显示在 Console 并标红资产 |
| **不存作者态数据** | 节点坐标、颜色、标题这些别烘进去，运行时用不上 |

> 已核实：GraphToolkit 模块**自己没有注册任何 ScriptedImporter**（DLL 里搜不到 Importer 类型），本工程 `DramaGraph.agv.meta` 目前是 `DefaultImporter`。
> 也就是说 `.agv` 现在处于"无人认领"状态 —— 你加上 `[ScriptedImporter(1, "agv")]` 后会干净接管，不会和内置逻辑冲突。

### 12.4 官方参考实现

```bash
# Package Manager → Add package by name
com.unity.graphtoolkit-samples
```
`0.6.6-exp.1`，最低 6000.6.0b1。里面的 **Visual Novel Director** 就是同一套架构的完整实现：
`ScriptedImporter` → `VisualNovelRuntimeGraph : ScriptableObject` → `VisualNovelDirector : MonoBehaviour` → 每类节点一个 `Executor`。

---

## 13. 运行时调试可视化

在 Play Mode 里，把执行状态实时画回编辑器的图上（当前节点高亮、连线跑马灯、端口显示当前值）。

```csharp
using Unity.GraphToolkit.Editor;
using Unity.GraphToolkit.Editor.GraphVisualization;

// 创建（子图场景要传【根图】的 ID）
var ctx = Registry.CreateVisualizationContext(graph.ID);

// 或者复用已有的
var existing = Registry.GetActiveContext(graph.ID);

// —— 节点 ——
var n = ctx.GetNodeReference(nodeId);
n.FillAmount = 60f;                    // ★ 0~100 百分比
ctx.Motion.Play(n, animationSpeed: 1f);   // 高亮流动动画
ctx.Motion.Pause(n);
ctx.Motion.Stop(n);
n.ClearCustomization();

// —— 连线 ——
var w = ctx.GetWireReference(outPortId, inPortId);
w.IsDashed      = true;
w.Opacity       = 0.4f;
w.WidthOverride = 3f;
ctx.Motion.Play(w, 2f);
w.ClearCustomization();

// —— 端口预览 ——
var p = ctx.GetPortReference(portId);
p.SetPreview("当前值: 42");
p.TryGetPreview(out var s);
p.ClearPreview();

// —— 全局开关 ——
ctx.NodeCustomizationEnabled = true;
ctx.WireCustomizationEnabled = true;
ctx.PortPreviewEnabled       = true;
ctx.IsGraphLoaded;                     // 图当前是否打开着
ctx.IsValid;                           // Dispose 后变 false

ctx.ClearAllVisualization();
ctx.Dispose();                         // 实现了 IDisposable，可以 using
```

### 官方承认的坑

- **暗色主题 + 没设 `DefaultColor` 的节点**，Motion 动画几乎看不见 → 给节点设个颜色
- 进 Play Mode 后**第一个执行的节点** `FillAmount` 可能不刷新 → 改用 `Motion.Play`
- `SetPreview` 只支持 string，每次转字符串都产生 GC → **按变化推送，别逐帧刷**

---

## 14. 速查：我想…→用…

| 我想… | 用 |
|---|---|
| 新建一种图 | `[Graph("ext")] class X : Graph` |
| 加"Create 资产"菜单 | `GraphDatabase.PromptInProjectBrowserToCreateNewAsset<X>(name)` |
| 节点分类/改标题/换图标 | `[Node(categoryPath, iconPath, title, uss)]` |
| 限定节点只在某图出现 | `[UseWithGraph(typeof(X))]` |
| 加一个可连线的输入 | `ctx.AddInputPort<T>("name")...Build()` |
| 加一个不可连线的配置项 | `ctx.AddOption<T>("name")...Build()` |
| 端口显示中文名 | `.WithDisplayName("中文")` |
| 端口允许接多根线 | `.WithCapacity(PortCapacity.Multi)` |
| 做纯执行流的箭头端口 | `AddInputPort<Untyped>(...).WithConnectorUI(PortConnectorUI.Arrowhead)` |
| 多行文本输入 | `.AsTextArea(2, 6)` |
| 列表/数组端口 | `AddInputPort<List<T>>` / `AddInputPort<T[]>` |
| 选项藏进 Inspector | `.ShowInInspectorOnly()` |
| 读端口值 | `port.TryGetValue<T>(out v)`（**仅未连线时成功**） |
| 读选项值 | `node.GetNodeOptionByName(n).TryGetValue<T>(out v)` |
| 找上游节点 | `port.FirstConnectedPort.GetNode()` |
| 找所有下游 | `port.GetConnectedPorts(list)` |
| 遍历全图节点 | `graph.GetNodes()` |
| 在节点上打红叉 | `OnGraphChanged(logger)` + `logger.LogError(msg, node)` |
| 报错带"一键修复" | `new GraphLogAction("修复", obj => {...})` |
| 禁止某些连线 | 重写 `Graph.IsConnectionAllowed` |
| 一段对话装多句台词 | `ContextNode` + `BlockNode` + `[UseWithContext]` |
| Blackboard 变量 | `graph.CreateVariable<T>(name, def, VariableKind.Local)` |
| 剧情段复用 | `GraphOptions.SupportsSubgraphs` + `[Subgraph(typeof(主图))]` |
| 画布右键菜单 | `[GraphMenu(typeof(X))] static void M(GraphMenuContext c)` |
| 工具栏按钮 | `[GraphToolbarElement(id, typeof(X), order)]` |
| 自定义类型端口配色 | `DataTypeStyleMapper` + `Register(type, icon, color)` |
| 限制 Blackboard 能建哪些类型的变量 | 重写 `BuildAvailableVariableTypes`（⚠️ a3 暂无，需升级） |
| 限制节点库能建哪些类型的常量 | 重写 `BuildAvailableConstantTypes`（⚠️ a3 暂无，需升级） |
| 脚本批量建图 | `GraphDatabase.CreateGraph<X>` + `AddNode` + `Connect`，包在 Undo 里 |
| **让图能在运行时用** | `ScriptedImporter` + `LoadGraphForImporter<X>` 烘焙成 ScriptableObject |
| Play Mode 里高亮当前节点 | `GraphVisualization.Registry.CreateVisualizationContext(graph.ID)` |

---

## 附：五个最容易踩的坑

1. **忘了 `.Build()`** → 端口/选项根本不出现，也不报错。
2. **`TryGetValue` 在端口已连线时返回 `false`** —— 这是设计如此。要判 `IsConnected` 再决定走内嵌值还是递归上游。
3. **`FillAmount` 是 0~100**，不是 0~1。
4. **Untyped 端口的 `DataType` 是 `typeof(Untyped)` 不是 `null`**（6.6 破坏性变更）。
5. **脚本改图没包 `UndoBeginRecordGraph` / `UndoEndRecordGraph`** → 不进 Undo 栈，视图也不刷新，看起来像"改了没生效"。

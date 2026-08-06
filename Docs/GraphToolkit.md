# Unity GraphToolkit (UnityEditor.GraphToolkitModule) 速查手册

来源：`D:\UnityEditor\6000.7.0a3\Editor\Data\Managed\UnityEngine\UnityEditor.GraphToolkitModule.{dll,xml}`
命名空间：`Unity.GraphToolkit.Editor`（**纯 Editor 程序集，运行时不可用**）

---

## 1. 全景：只有 4 类东西

| 概念 | 基类 / 接口 | 说明 |
|---|---|---|
| 图 | `Graph` | 一个资产文件 = 一个 Graph 实例 |
| 节点 | `Node` / `ContextNode` / `BlockNode` | 用户自定义，重写 `OnDefinePorts` / `OnDefineOptions` |
| 端口 | `IPort`（由 builder 创建） | 输入/输出，带数据类型 |
| 变量 | `IVariable` + `IVariableNode` | Blackboard 里的变量，可拖到画布成节点 |
| 连线 | `Wire` | 只读视图对象，`Graph.Connect/Disconnect` 操作 |

内建但不需要你继承的节点：`IConstantNode`（常量）、`IVariableNode`（变量引用）、`ISubgraphNode`（子图）。

---

## 2. Graph

```csharp
[Graph(AssetExtension, GraphOptions.SupportsSubgraphs)]   // 扩展名必须全局唯一
internal class DramaGraph : Graph
{
    const string AssetExtension = "agv";

    [MenuItem("Assets/Create/Drama/剧情编辑器")]
    static void CreateAssetFile()
        => GraphDatabase.PromptInProjectBrowserToCreateNewAsset<DramaGraph>("DramaGraph");
}
```

### GraphOptions（Flags）
| 值 | 含义 |
|---|---|
| `None` / `Default` = 0 | 默认 |
| `SupportsSubgraphs` = 1 | 允许子图（配合 `[Subgraph(typeof(MainGraph))]`） |
| `DisableAutoInclusionOfNodesFromGraphAssembly` = 2 | **关闭"同程序集的 Node 自动进节点库"** |

> 默认行为：与 Graph 处于**同一程序集**的所有 `Node` 子类，会自动出现在该图的节点库里。
> 本工程目前 `Assets/Drama/Editor` 没有 asmdef，全部落在 `Assembly-CSharp-Editor`，所以节点是"自动被收进来"的。多图并存时会互相污染，建议加 asmdef 或改用 `[UseWithGraph]`。

### 可重写的钩子
```csharp
public virtual void OnEnable();                                  // 图被创建/加载
public virtual void OnDisable();                                 // 图被卸载
public virtual void OnGraphChanged(GraphLogger logger);          // 图变更后 → 做校验、报错
public virtual bool IsConnectionAllowed(IPort output, IPort input); // 连线合法性拦截
protected virtual void OnDefineSubgraphNodeOptions(Node.IOptionDefinitionContext ctx);
```

### 图操作 API（脚本化构图）
```csharp
void        AddNode(Node node);
void        RemoveNode(INode node);
INode       GetNode(int index);
IEnumerable<INode> GetNodes();
int         NodeCount { get; }

bool        Connect(IPort output, IPort input);      // 已存在返回 false
bool        Disconnect(IPort output, IPort input);
Wire        GetWire(IPort output, IPort input);      // 无连接返回 null

IVariable   CreateVariable<T>(string name, T defaultValue, VariableKind kind);
IVariable   CreateVariable(string name, Type valueType, object defaultValue, VariableKind kind);
IVariable   GetVariable(int index);
IEnumerable<IVariable> GetVariables(SortMethod sort);   // Creation / Display
bool        RemoveVariable(IVariable v, bool forceRemove);
IVariableNode AddVariableNode(IVariable v, Vector2 pos, VariableNodeMode mode); // Get / Set

IConstantNode CreateConstantNode<T>(Vector2 pos, T defaultValue);
ISubgraphNode AddSubgraphNode(Graph subgraph, Vector2 pos);
ISubgraphNode CreateLocalSubgraphNode<TSub>(string name, Vector2 pos);

void UndoBeginRecordGraph(string actionName);  // 成对使用，脚本改图必须包起来
void UndoEndRecordGraph();

Hash128 ID { get; }        // 图的全局唯一 ID（GraphVisualization 用它）
GUID AssetGuid { get; }
string Name { get; }
```

### ⚠️ 类型过滤 API —— 在线文档已有，本地 6000.7.0a3 尚未包含

[6000.7 在线 ScriptReference](https://docs.unity3d.com/6000.7/Documentation/ScriptReference/Unity.GraphToolkit.Editor.Graph.html) 上 `Graph` 有 8 个属性、3 个 protected 方法，比本地 DLL 多出下面这组。**本地反射逐一验证过：`Graph` 里名字含 Supported/Available/Build 的成员一个都没有，public 属性只有 `Name` / `VariableCount` / `NodeCount` / `ID` / `AssetGuid` 5 个。** 说明在线文档跑在比 a3 更新的 alpha 上，升级 Editor 后才能用。

```csharp
// 属性
public IReadOnlyCollection<Type> SupportedTypes;            // 本图支持的全部类型（端口+变量+常量）
public IReadOnlyCollection<Type> AvailableVariableTypes;    // Blackboard 里可创建的变量类型
public IReadOnlyCollection<Type> AvailableConstantTypes;    // 节点库里可创建的常量类型

// 可重写：控制 Blackboard 变量创建菜单里出现哪些类型
protected virtual IEnumerable<Type> BuildAvailableVariableTypes(
    IReadOnlyCollection<Type> baseSupportedTypes);

// 可重写：控制节点库常量节点菜单里出现哪些类型
protected virtual IEnumerable<Type> BuildAvailableConstantTypes(
    IReadOnlyCollection<Type> baseSupportedTypes);
```

`baseSupportedTypes` = 从**图内所有节点的端口定义里自动发现**的类型集合。默认实现原样返回。

```csharp
protected override IEnumerable<Type> BuildAvailableVariableTypes(
    IReadOnlyCollection<Type> baseSupportedTypes)
{
    foreach (var t in baseSupportedTypes)
        if (t != typeof(Untyped))       // 剔除 Untyped
            yield return t;

    yield return typeof(Vector3);       // 追加
}
```

要点：
- 返回 `null` = 一个都不提供。
- **绝对不要在这两个方法里访问 `SupportedTypes`** —— 循环依赖，抛 `InvalidOperationException`。要用参数 `baseSupportedTypes`。
- 只影响 **UI 菜单**。`graph.CreateVariable` / `graph.CreateConstantNode` 用代码创建任意类型**不受限制**。

### GraphDatabase（静态）
```csharp
static T      CreateGraph<T>(string assetPath);
static T      LoadGraph<T>(string assetPath);
static T      LoadGraphForImporter<T>(string assetPath);   // ★ ScriptedImporter 里用这个
static void   SaveGraph(Graph graph);
static void   PromptInProjectBrowserToCreateNewAsset<T>(string defaultName);
static string GetGraphAssetPath(Graph graph);
static GUID   GetGraphAssetGUID(Graph graph);
```

---

## 3. Node

```csharp
[Node("剧情/台词", iconPath: null, title: "台词", stylesheet: "Assets/.../Talk.uss")]
[UseWithGraph(typeof(DramaGraph))]
[Serializable]
public class TalkNode : DramaNode
{
    protected override void OnDefinePorts(IPortDefinitionContext ctx) { ... }
    protected override void OnDefineOptions(IOptionDefinitionContext ctx) { ... }
    public override void OnEnable() { }
    public override void OnDisable() { }
}
```

`[Node]` 的 4 个重载参数依次：`categoryPath`（节点库分类路径，用 `/` 分层）、`iconPath`、`title`、`stylesheet(.uss)`。

### 运行期属性（`INode` / `Node` 共有）
```
Hash128 ID; Graph Graph; Vector2 Position;
string Title / Subtitle / Tooltip;   // 可读写，OnDefine 之外也能改
Color DefaultColor;                  // 节点顶部高亮条颜色
float FillAmount;                    // 进度条（0..100），调试可视化很好用
int InputPortCount / OutputPortCount / NodeOptionCount;
bool IsConnected;
IPort GetInputPort(int) / GetInputPortByName(string) / GetInputPorts();
IPort GetOutputPort(int) / GetOutputPortByName(string) / GetOutputPorts();
INodeOption GetNodeOption(int) / GetNodeOptionByName(string); IEnumerable<INodeOption> NodeOptions;
void DefineNode();                   // 强制重建端口/选项（动态端口时手动调用）
void RemoveFromGraph();
```

扩展方法：`INodeExtensions.GetNode(this IPort port)` → 取端口所属节点。

### 端口定义（`IPortDefinitionContext`）
```csharp
ctx.AddInputPort<T>("name")     // → IInputPortBuilder<T>
ctx.AddInputPort("name")        // → IInputPortBuilder（弱类型，需 .WithDataType<T>() / .WithDataType(type)）
ctx.AddOutputPort<T>("name")
ctx.AddOutputPort("name")
```

Builder 链式方法（`IPortBuilder<T>` 公共部分）：
```
.WithDisplayName(string)         // UI 标签
.WithTooltip(string)
.WithConnectorUI(PortConnectorUI.Circle | Arrowhead)
.WithCapacity(PortCapacity.None | Single | Multi)
.AsVertical()                    // 竖向端口（上下连线，适合"流程"）
.Build()                         // ★ 必须调用
```
仅输入端口额外有（`IInputBasePortBuilder<T>`）：
```
.WithDefaultValue(T)             // 未连线时端口上的内嵌编辑框默认值
.Delayed()                       // 等同 [Delayed]
.AsTextArea(minLines: 3, maxLines: 3)   // 等同 [TextArea]
```

**端口名（`portName`）在同一节点同一方向内必须唯一**，输入和输出可以重名。`DisplayName` 只影响显示。

`Untyped` 类型：`AddInputPort<Untyped>("in")` 表示**不携带数据的纯流程端口**，做执行流（exec flow）就用它。

### 节点选项（`IOptionDefinitionContext`）——显示在节点头部/Inspector，不是端口
```csharp
ctx.AddOption<long>("EventID")
   .WithDefaultValue(-1L)
   .WithDisplayName("事件ID")
   .WithTooltip("事件的唯一ID")
   .Delayed()
   .ShowInInspectorOnly()      // 只在 Inspector 显示，不占节点头部
   .AsTextArea(3, 6)
   .Build();
```

### 取值 / 存值
```csharp
port.TryGetValue<T>(out var v);    // ★ 仅当端口"未连线"时返回内嵌字段值
port.TrySetValue<T>(v);
port.GetConnectedPorts(List<IPort> outList);
port.FirstConnectedPort;
option.TryGetValue<T>(out var v);  // 选项值总是可读
```
> 遍历图求值时的标准套路：`if (port.IsConnected) 递归上游节点; else port.TryGetValue(out v);`

---

## 4. ContextNode / BlockNode（对话系统强相关）

`ContextNode` 是一个**容器节点**，内部纵向排列若干 `BlockNode`。非常适合"一段对话 = 一个 Context，里面每句台词 = 一个 Block"。

```csharp
[Node("剧情/对话段")]
public class DialogueContext : ContextNode { }

[UseWithContext(typeof(DialogueContext))]     // 限定这个 Block 只能放进哪些 Context
public class LineBlock : BlockNode { }
```

```csharp
// ContextNode
void AddBlockNode(BlockNode b);
void CreateBlockNode<TBlock>(int index = -1);   // -1 = 追加到底部
void CreateBlockNode(Type blockType, int index = -1);
void InsertBlockNode(int index, BlockNode b);
void RemoveBlockNode(BlockNode b);
void ClearBlockNodes();
BlockNode GetBlock(int index);
int BlockCount { get; }
IEnumerable<BlockNode> BlockNodes { get; }

// BlockNode
ContextNode ContextNode { get; }
int Index { get; }
```

---

## 5. 变量 / 子图

```csharp
enum VariableKind { Local, Input, Output }      // Input/Output 即子图的入参/出参
enum VariableNodeMode { Get, Set }
```

```csharp
IVariable v = graph.CreateVariable<long>("DramaID", -1, VariableKind.Local);
v.Name; v.DataType; v.VariableKind;             // 均可写
v.TryGetDefaultValue<T>(out var d); v.TrySetDefaultValue<T>(d);
v.GetNodes(List<IVariableNode> outNodes); v.NodeCount; v.IsConnected;
v.RemoveFromGraph(forceRemove: true);
```

子图：主图加 `GraphOptions.SupportsSubgraphs`，子图类型加 `[Subgraph(typeof(DramaGraph))]`。
子图的 `VariableKind.Input/Output` 变量会自动变成 SubgraphNode 上的端口。
`ISubgraphNode.GetSubgraph()` 拿到被引用的 Graph。

---

## 6. 校验与报错

```csharp
public override void OnGraphChanged(GraphLogger logger)
{
    foreach (var n in GetNodes())
        if (n is TalkNode t && !t.GetInputPortByName("LocalizationProt").IsConnected)
            logger.LogError("台词未绑定多语言", t);
}
```
`GraphLogger.Log / LogWarning / LogError(message, context)`，`context` 传节点 → 节点上直接出错误标记。
带修复动作的重载：`LogError(msg, ctx, new GraphLogAction("自动修复", obj => { ... }))`。

---

## 7. 编辑器扩展点（全是特性 + 静态方法）

```csharp
[GraphMenu(typeof(DramaGraph))]                 // 画布右键菜单
static void OnCanvasMenu(GraphMenuContext c)
{
    if (c.ClickedObject is INode node) c.AppendAction("剧情/复制事件ID", () => { });
    c.AppendSeparator("");
    c.AppendAction("剧情/校验全图", () => { });
}

[BlackboardMenu(typeof(DramaGraph))]            // Blackboard 右键菜单
static void OnBlackboardMenu(GraphMenuContext c) { }
```
`GraphMenuContext`：`Graph`、`ClickedObject`（空白处为 null）、`MousePosition`、`AppendAction(...)`、`AppendSeparator(subMenuPath)`。

```csharp
[GraphToolbarElement("drama-export", typeof(DramaGraph), order: 100)]  // 工具栏按钮
```

```csharp
[DataTypeStyleMapper(typeof(DramaGraph))]       // 自定义数据类型的端口配色/图标
class DramaStyles : DataTypeStyleMapper
{
    public DramaStyles() { Register(typeof(DramaProt), icon, Color.cyan); }
}
```

---

## 8. GraphVisualization（运行时把执行状态画到图上 ★ 调试/预览神器）

```csharp
using var ctx = GraphVisualization.Registry.CreateVisualizationContext(graph.ID);
var n = ctx.GetNodeReference(nodeId);
n.FillAmount = 0.5f;                       // 节点进度条
ctx.Motion.Play(n, animationSpeed: 1f);    // 节点高亮流动动画
var w = ctx.GetWireReference(outPortId, inPortId);
w.IsDashed = true; w.Opacity = 0.4f; w.WidthOverride = 3f;
ctx.GetPortReference(portId).SetPreview("Hello");   // 端口旁显示当前值
ctx.ClearAllVisualization();
```
另有 `Registry.GetActiveContext(graphID)`、`contextRegistered` / `contextWillUnregister` 事件。
子图场景下 `graphID` 要传**根图**的 ID。

---

## 9. 关键约束 / 易踩坑

1. **`Unity.GraphToolkit.Editor` 是 Editor-only**，且 Unity 官方明确表示**短期内不会支持运行时**（见 §11）。`.agv` 只是编辑期数据，运行时读不到。
   → 对话系统必须做一层"烘焙"：`ScriptedImporter` 里 `GraphDatabase.LoadGraphForImporter<DramaGraph>(path)` 遍历节点，产出一个纯运行时 `ScriptableObject`（或 JSON/二进制）。
2. 节点数据靠 Unity 序列化：自定义节点字段要 `[SerializeField]`，自定义数据类要 `[Serializable]`，`WithDefaultValue` 的值必须能被 Unity 序列化。
3. `Build()` 忘了调 → 端口不会出现。
4. 同方向端口名重复 → 后者覆盖 / 行为异常。
5. 端口 `TryGetValue` 在**已连线**时返回 false，这是设计如此（值来自上游）。
6. **★ 用代码改图（包括改选项/端口的值）必须包在 `UndoBeginRecordGraph` / `UndoEndRecordGraph` 之间，否则会被静默还原。**
   实测（2026-08-06，6000.7.0a3）：不包 Undo 时 `INodeOption.TrySetValue` 返回 true、立刻 `TryGetValue` 也读得到新值，
   但下一次 `DefineNode` 重建选项时会**还原成 `WithDefaultValue` 的默认值**，界面上永远看不到变化。
   `UndoEndRecordGraph` 才会把改动提交进图模型并刷新视图。包了 Undo 的写入能活过域重载（即真正序列化了）。
7. `[Graph]` 的扩展名必须全项目唯一（Unity 靠它选 importer）。
8. **★ `OnGraphChanged` 在图加载期间就会被调用，那时 `DefineNode` 还没建出节点选项** ——
   此时 `GetNodeOptionByName(...)` 返回 `null`，在 `OnGraphChanged` 里同步读写选项会把所有节点都跳过。
   正确做法是**延到下一个编辑器 tick**：

   ```csharp
   static bool s_Running, s_Scheduled;

   internal static void Schedule(Graph graph)
   {
       if (s_Running || s_Scheduled) return;
       s_Scheduled = true;
       EditorApplication.delayCall += () => { s_Scheduled = false; DoWork(graph); };
   }
   ```

   另外 `UndoEndRecordGraph` 会**再触发一次 `OnGraphChanged`**，所以在 `OnGraphChanged` 里写图必须加防重入标志，
   否则无限递归。再配合 `Graph.OnEnable` 也排一次，可以兜住"图打开时数据就是脏的、之后没人改动"的情况。
9. **`graph.GetNodes()` 拿不到 `ContextNode` 内部的 `BlockNode`** —— 它只返回画布上的顶层节点。
   要全量遍历得自己下潜一层（`node is ContextNode ctx` → `ctx.BlockNodes`）。烘焙和校验都会踩到。

---

## 10. 完整枚举速查

```
GraphOptions      : None=0, Default=0, SupportsSubgraphs=1, DisableAutoInclusionOfNodesFromGraphAssembly=2
PortCapacity      : None=0, Single=1, Multi=2
PortConnectorUI   : Circle=0, Arrowhead=1
PortDirection     : None=0, Input=1, Output=2
VariableKind      : Local=0, Input=1, Output=2
VariableNodeMode  : Get=0, Set=1
SortMethod        : Creation=0, Display=1
```

---

## 11. 官方更新记录与路线图

来源（Unity 官方论坛，2026-08-06 抓取）：
- [Graph Toolkit Update in Unity 6.5 alpha](https://discussions.unity.com/t/graph-toolkit-update-in-unity-6-5-alpha/1712169)
- [Graph Toolkit Update in Unity 6.6 alpha](https://discussions.unity.com/t/graph-toolkit-update-in-unity-6-6-alpha/1721970)
- [Visual Novel Director 示例文档](https://docs.unity3d.com/Packages/com.unity.graphtoolkit@0.4/manual/visual-novel-director-explore-the-code.html)
- [ScriptReference](https://docs.unity3d.com/6000.5/Documentation/ScriptReference/Unity.GraphToolkit.Editor.Graph.html)

> **本工程用的是 6000.7.0a3，比 6.6 更新 —— 下面 6.5 / 6.6 的全部特性都已经在本地 DLL 里，可直接用。**（已用反射逐条核对过。）
> GraphToolkit 从 **Unity 6.4 起变成内置模块**（不再是 Package）。

### 6.5 alpha 新增

| 特性 | API |
|---|---|
| 代码建图 | `GraphDatabase.CreateGraph<T>` / `graph.CreateVariable<T>` / `AddVariableNode` / `AddNode` / `Connect` + Undo 批处理 |
| 节点自定义 | `[Node(category, iconPath, title)]`、`Subtitle`、`DefaultColor`、`AsVertical()`、`.AsTextArea()` |
| 数据类型样式 | `DataTypeStyleMapper` + `[DataTypeStyleMapper(typeof(TGraph))]` + `Register(type, icon, color)` |
| **集合端口** | 直接用泛型：`AddInputPort<List<int>>("A")`、`AddInputPort<string[]>("B")`、`AddOutputPort<Vector2[]>("C")`。Inspector 里可动态增删元素、逐项编辑；Blackboard 变量有 Mode 下拉可转成集合 |
| 变量显示顺序 | `Graph.GetVariables(SortMethod.Display)` |
| 子图节点选项 | `Graph.OnDefineSubgraphNodeOptions(IOptionDefinitionContext)` |
| Untyped 端口 | 不指定类型的端口，有专属图标/配色（暂不可通过 DataTypeStyleMapper 改） |

### 6.6 alpha 新增（主题：**调试与可视化反馈**）

| 特性 | API |
|---|---|
| 连线动画 | `ctx.Motion.Play/Stop/Pause(wire, speed)`；`wire.WidthOverride / Opacity / IsDashed / ClearCustomization()`；`ctx.GetWireReference(outPortId, inPortId)`（需 6000.6.0a8+） |
| 节点进度/动画 | `node.FillAmount`（**0–100 百分比**，不是 0–1）；`ctx.Motion.Play/Stop/Pause(node, speed)`；`ctx.GetNodeReference(nodeId)`（需 6000.6.0a7+） |
| 端口预览 | `ctx.GetPortReference(portId).SetPreview("text")` |
| 错误/警告标记 | `GraphLogger` + `GraphLogAction`（可挂"一键修复"回调） |
| ID/GUID 公开 | graph / node / port / 子图资产的 `ID`、`AssetGuid` |
| USS 换肤 | `[Node(..., stylesheet: "Assets/Stylesheets/X.uss")]` |
| 多连接容量 | `IPortBuilder.WithCapacity(PortCapacity.Single/Multi/None)` |
| 连线合法性 | 重写 `Graph.IsConnectionAllowed(IPort output, IPort input)` |
| 枚举 Flags | 带 `[Flags]` 的枚举自动渲染成 MaskField（多选） |
| 自定义工具栏 | `[GraphToolbarElement(id, graphType, order)]`，可放任意 VisualElement（含下拉菜单） |
| 补齐 API | `INodeOption.TrySetValue` |

> ⚠️ **6.6 破坏性变更**：Untyped 端口的 `DataType` 从 `null` 改为 `typeof(Untyped)`。以前对 `port.DataType` 做 null 判断的代码要改成判 `typeof(Untyped)`。

### 6.6 已知问题（官方承认）

- 暗色主题下、未设置颜色的节点，Motion 动画几乎看不见
- 进入 Play Mode 后**第一个执行的节点** `FillAmount` 可能不刷新（绕法：改用 Motion 动画）
- 端口预览只支持 string，每次转字符串都会产生 GC；官方建议**按变化推送**而不是逐帧刷新
- 只改 `Subtitle` 属性（不改端口/选项配置）时节点不重绘
- 端口连上线后，节点的折叠箭头会失效

### 官方样例包（强烈建议装）

```
Package Manager → Add package by name → com.unity.graphtoolkit-samples
版本 0.6.6-exp.1，最低 Unity 6000.6.0b1（本工程 6000.7.0a3 满足）
```
里面的 **Visual Novel Director** 示例就是"对话/剧情图"，且 0.6.6-exp.1 版已改写成演示新调试功能。**这是本工程最直接的参考实现。**

其架构（和 §9.1 的结论一致）：

| 层 | 类型 | 职责 |
|---|---|---|
| 编辑期 | GraphToolkit `Graph` / `Node` | 作者态，`.agv` 资产 |
| 导入 | `ScriptedImporter` | `LoadGraphForImporter<T>` 遍历图 → 烘焙 |
| 运行时数据 | `VisualNovelRuntimeGraph : ScriptableObject` | 存 `List<VisualNovelRuntimeGraphNode>`，节点用 `[SerializeReference]` 多态 |
| 运行时执行 | `VisualNovelDirector : MonoBehaviour` | 顺序驱动 |
| 节点执行器 | `VisualNovelRuntimeGraphNodeExecutor` 子类（如 `SetBackgroundExecutor`） | 一种节点一个执行器 |

官方原话（UnityAlexZ）：GraphToolkit 只提供图的**前端数据模型**，生成的图资产应当转换成你自己的、可在运行时执行的数据模型。自定义模型的好处是"只存运行时必需的最小数据，不带节点坐标之类的作者态信息"。

### 运行时支持现状（官方明确回复）

> "We have not started any work on supporting GTK at runtime. It is, however, still something in our backlog."

原因有三：很多图工具本来就不需要运行时执行；数据模型里塞了大量 UI 相关内容，运行时低效；需要运行时图的工具通常都自己实现数据模型。
→ **结论：ScriptedImporter 烘焙是官方推荐且唯一可行的路线，不要等运行时支持。**

### 路线图（官方"在雷达上"，无排期）

- **State Machine 图框架**（状态机）—— 正在开发中，计划公开发布
- 自定义节点支持 Unity 内置 Attribute（`[Range]` 等）
- 节点/图的预览贴图（类似 Shader Graph）
- 非编译期的节点注册方式（替代 `[Node]` 特性）
- `DataTypeStyleMapper` 支持基类与泛型
- 运行时图调试（live-linking）
- 更深度的 UI 定制（USS + 直接访问 VisualElement）—— 团队"正在调研"

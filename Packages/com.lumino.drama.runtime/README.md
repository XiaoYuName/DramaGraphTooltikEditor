# Drama Runtime

剧情系统的**运行时数据模型**。编辑器工程（DarmaViewEditor）把 `.agv` 剧情图导出成
`DramaScript` 资产，游戏工程装上本包就能读取并执行。

---

## 装到别的工程

`Packages/manifest.json` 里加一条（三选一）：

```jsonc
// ① 本地路径（同机开发最方便）
"com.lumino.drama.runtime": "file:../../DarmaViewEditor/Packages/com.lumino.drama.runtime"

// ② Git（推荐，团队协作用）
"com.lumino.drama.runtime": "https://你的仓库地址.git?path=Packages/com.lumino.drama.runtime"

// ③ 直接把整个文件夹拷进目标工程的 Packages/ 目录（内嵌包）
```

**必须用包的形式共享，不要复制 .cs 文件。** 原因见下。

---

## ⚠️ 三条不能违反的约定

导出的 `DramaScript` 资产靠这三样东西才能在别的工程里正确反序列化：

### 1. `.cs` 的 GUID 必须两边一致

资产里 `m_Script: {guid: 621039800177f9c4f926a12b0c863344}` 指向 `DramaScript.cs`。
两个工程里这个 GUID 不同 → 资产变成 **Missing Script** 空壳。

用包共享就天然一致（`.meta` 跟着包走）；手工复制 `.cs` 会生成新 GUID，必坏。

### 2. 程序集名 / 命名空间 / 类名不能改

`[SerializeReference]` 存的是 `{ 类名, 命名空间, 程序集名 }` 三个**字符串**，不是 GUID：

```
Drama.Runtime::Drama.Runtime.TalkAction
```

改 asmdef 名字、改 `namespace Drama.Runtime`、改任何 `XxxAction` 类名 →
已导出资产里对应指令**全部变 null**，而且不报错，只是数据没了。

要重命名的话，先改编辑器工程 → 全量重新导出 → 再更新目标工程的包。

### 3. 外部依赖

| 依赖 | 用途 | 备注 |
|---|---|---|
| **Odin Inspector** | `Sirenix.OdinInspector.Attributes` 的标注 | 非 UPM 包，目标工程自行导入 |
| **DOTween** | `Ease` / `LoopType` 是数据字段的类型 | 非 UPM 包，目标工程自行导入 |
| **UniTask** | 整个执行层的异步 | UPM 包 `com.cysharp.unitask` |

**另外必须定义 `UNITASK_DOTWEEN_SUPPORT` 宏。** 立绘动画的 Handler 用
`tween.ToUniTask(ct)` 把 DOTween 桥到 UniTask，这个扩展在 `UniTask.DOTween`
程序集里，而它的内容整体包在 `#if UNITASK_DOTWEEN_SUPPORT` 下。

这个宏本来由 UniTask 的 versionDefine 在检测到 UPM 包 `com.demigiant.dotween`
时自动定义 —— 但如果 DOTween 是以 DLL 形式放在 `Assets/Plugins/` 下（常见情况），
版本检测不会触发，得手动加到 Player Settings → Scripting Define Symbols。

---

## 数据结构

一个 `DramaScript` = 一张线性指令表 `List<DramaAction>`（`[SerializeReference]` 多态）。

### 并行 / 串行语义

每条指令的 `Next` 是**数组**，长度决定执行方式：

| `Next.Length` | 含义 |
|---|---|
| `0` | 结束 |
| `1` | **串行** —— 本条完全执行完（含动画/等待）后才执行下一条 |
| `> 1` | **并行** —— 本条执行完后，这几条同时开始，互不等待 |

选项分支是另一套：`ChoiceAction` 自己的 `Next` 为空，跳转目标挂在 `Options[i].Next` 上。

### ⚠️ 汇合点不要用 `InboundCount` 去等

`InboundCount > 1` 表示这条指令静态上有多条入边。**但拿它做"等所有入边到齐"会死锁**：
图里只要有 `ChoiceAction`，玩家就只走一条分支，未选分支永远不到达汇合点。
（另外计数器是共享状态，剧本重播 / 图里有回环时不 reset 就再也走不通。）

本包用的是**结构化 fork-join**：`DramaScriptIndex` 播放前算出每个 fork 的汇合点
（各分支可达集合的交集里最早的那个），各分支跑到汇合点就停，`WhenAll` 等齐后
由发起 fork 的那一层继续执行汇合点。零运行期计数，没有死锁的余地。

`InboundCount` 现在只用于导出期校验，不参与运行时决策。

---

## 运行时分层

```
Data/       DramaScript + 各 XxxAction        纯数据，不含任何执行逻辑
Flow/       DramaScriptIndex / DramaPlayer     流程语义，不认识任何具体指令类型
Handlers/   各 XxxActionHandler                指令逻辑，只调 Services 里的接口
Services/   IDialogueView / IActorStage / ...  ★ 接口在包里，实现在宿主工程
```

宿主工程要做的只有两件事：实现 `Services/` 下那几个接口，然后装配：

```csharp
var registry = DramaDefaultHandlers.CreateDefault();
var player   = new DramaPlayer(registry);

// 播放是个 goto 循环，不是递归 —— 连播 N 段剧情栈也是平的
while (dramaId > 0)
{
    var script = await LoadScriptAsync(dramaId, ct);

    var missing = registry.FindMissing(script);        // 播之前就暴露缺失的 Handler
    if (missing.Count > 0) { Debug.LogError(...); break; }

    var keys = DramaAssetKeys.Collect(script);         // 批量预载，别播到一半现加载
    await PreloadAsync(keys, ct);

    var result = await player.PlayAsync(script, ctx, ct);

    stage.CompleteAllTweens();                         // 收掉游离动画
    stage.ReleaseAll();
    assets.ReleaseAll();

    if (result.Kind != DramaPlayResult.EKind.Goto) break;
    dramaId = result.GotoDramaId;
}
```

### 版本

`DramaScript.FormatVersion` 与 `DramaScript.CurrentFormatVersion` 比对。
`DramaPlayer.PlayAsync` 读到更高版本会直接抛 `NotSupportedException`。

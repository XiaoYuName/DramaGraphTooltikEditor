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

本包依赖 **Odin Inspector**（`Sirenix.OdinInspector.Attributes`，运行时 DLL）
和 **DOTween**（`Ease` / `LoopType` 直接用的它的类型）。
这两个不是 UPM 包，目标工程要自行导入，否则本包编译不过。

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

`InboundCount > 1` 表示这条指令是并行支线的**汇合点**，运行时要等所有入边到齐后只执行一次。

选项分支是另一套：`ChoiceAction` 自己的 `Next` 为空，跳转目标挂在 `Options[i].Next` 上。

### 执行器骨架

```csharp
async UniTask Run(int index)
{
    var a = script.Actions[index];

    // 汇合点：等所有入边都到了才继续
    if (a.InboundCount > 1 && !CountdownArrived(a)) return;

    await Execute(a);                                  // 真正干活

    if (a.Next.Length == 1)                            // 串行
        await Run(a.Next[0]);
    else if (a.Next.Length > 1)                        // 并行
        await UniTask.WhenAll(a.Next.Select(Run));
}
```

### 版本

`DramaScript.FormatVersion` 与 `DramaScript.CurrentFormatVersion` 比对。
读到更高版本应当拒绝加载并提示升级本包。

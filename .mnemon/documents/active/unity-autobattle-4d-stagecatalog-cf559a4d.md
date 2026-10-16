---
id: "cf559a4d-9cb4-48a2-9691-1776e16deaf8"
title: "Unity AutoBattle 章节进度总览与首通奖励汇总（4d）：奖励公式收敛到 StageCatalog、已领金币按当前规则重算"
description: "HelloWorldProject 关卡制 4d 步：首通金币公式从 GameController 抽到 StageCatalog.FirstClearGold 单一来源、GrantFirstClearReward 签名由 (chapter, bool) 改为 (chapter, stage)、PlayerData.TotalFirstClearGoldClaimed/ClearedCountInChapter 采用「按当前配置重算而非记流水」、选关面板标题行改全局总览 + 左栏各章首通进度、Play 模式数值核对（26/110 关 · 4/11 章 · 6080/63800），以及面板开着时浏览章不重置的已知现象与两条遗留项。"
status: "active"
created_at: "2026-10-15T05:13:00.577Z"
updated_at: "2026-10-15T05:13:00.577Z"
content_hash: "996963e67a397ee5527adcea4f346c9cb7947045c175edfbc06374f2de6479e0"
source_paths:
  - "HelloWorldProject/Assets/Scripts/Stages.cs"
  - "HelloWorldProject/Assets/Scripts/Models.cs"
  - "HelloWorldProject/Assets/Scripts/GameController.cs"
  - "HelloWorldProject/Assets/Scripts/GameUI.cs"
  - "HelloWorldProject/Assets/Resources/game_config.json"
  - "HelloWorldProject/Assets/Screenshots/stage-summary.png"
  - "HelloWorldProject/Assets/Screenshots/stage-fresh.png"
session_ids:
  - "5d510e12-f89d-45c6-816a-5ffef5f5a40d"
memory_body_ids:
  []
---

# Unity AutoBattle 章节进度总览与首通奖励汇总（4d）

工程 `HelloWorldProject/`（Universal 2D，Editor 6000.6.5f1），全程序化搭建的挂机自动战斗。本步在 4c 的首通奖励之上加「全局进度总览」，并顺手把奖励公式收敛到唯一来源。规则见 4a 文档 `418948ff-876f-4f76-90af-50e55a5c8727`，选关面板见 4b 文档 `86f3fe99-d412-41f3-a55b-12811d5c3288`，首通奖励/存档 v6/`StageClearDelay` 见 4c 文档 `763c8adf-09a9-48af-9ac6-607a7011a53c`；本文只记 4d 的新增结论，不重复其规则。

## 设计决策：奖励公式收敛到唯一来源

4c 把首通金额算在 `GameController.GrantFirstClearReward` 内部；4d 需要在界面**预览**同一个数字。不去抄一份公式，而是抽到 `HelloWorldProject/Assets/Scripts/Stages.cs`：

```csharp
public static int FirstClearGold(int chapter, int stage)
{
    int gold = GameConfig.FirstClearGoldBase
        + GameConfig.FirstClearGoldPerChapter * Mathf.Max(0, chapter - 1);
    if (stage >= StagesPerChapter) gold += GameConfig.ChapterClearGoldBonus;
    return gold;
}
public static int FirstClearGoldOfKey(int stageKey) => FirstClearGold(stageKey / 100, stageKey % 100);
public static int TotalFirstClearGold(int chapters)
```

- `GameController.GrantFirstClearReward` 签名由 `(int chapter, bool chapterFinal)` 改为 `(int chapter, int stage)`，章末判断改为函数内部自算——调用方不再有机会传错「是不是章末」。
- 参数拆开（`chapter` + `stage` 两个 int）而不是传 `stageKey`，是为了让界面和存档两条路径都直接可调；只有已有 key 的地方才用 `FirstClearGoldOfKey`。
- 配置仍在 `HelloWorldProject/Assets/Resources/game_config.json:94-97`：`StageClearDelay=3`、`FirstClearGoldBase=120`、`FirstClearGoldPerChapter=80`、`ChapterClearGoldBonus=600`。

## 决策：已领金币按当前规则重算，不记流水

`HelloWorldProject/Assets/Scripts/Models.cs` 加两个方法：

```csharp
public int TotalFirstClearGoldClaimed();      // 遍历 clearedStages，用当前公式重算
public int ClearedCountInChapter(int chapter);
```

- `TotalFirstClearGoldClaimed()` 读的是 `clearedStages` 集合，金额**按当前配置重算**，因此改了首通配置，界面上的「已领」会立刻跟着变。这是有意的取舍：多存一份流水能保住历史数字，但会和配置对不上，而在「对不上配置」和「随手改配置就重算」之间，后者更不容易骗人。
- 用已通关集合推导，因此天然幂等，不需要额外的累计字段，也不用动存档版本（仍是 v6）。

## 界面（`HelloWorldProject/Assets/Scripts/GameUI.cs`）

- 选关面板标题下原来那行静态操作提示（「点左侧章节切换，点右侧关卡直接开打…」）换成全局总览——详情区本来已有一份操作提示，那行是重复的：

  > 总进度：首通 **26**/110 关 · 解锁 **4**/11 章 · 首通金币 **6,080** / 63,800

- 左栏每个章节按钮追加该章首通进度（`第 1 章 10/10`、`第 3 章 6/10`）。之所以逐章都标：关卡能回头重刷，只看「已过」分不清哪章还没打满。
- 面板状态字段仍是 `_stageViewChapter` / `_stagePanelWasOpen`（见 4b 文档），4d 未改其语义。

## 实测（Play 模式，`eval` 注入 PlayerData）

构造 1 章 10/10、2 章 10/10、3 章 6/10、4 章 0/10，`maxChapterUnlocked=4`：

| 项 | 结果 |
|---|---|
| 界面总览行 | `26/110 关 · 4/11 章 · 6,080 / 63,800` |
| `eval` 直查 | `cleared=26 gold=6080 all=63800` |
| 手算核对 | 第 1 章 1800 + 第 2 章 2600 + 第 3 章前 6 关 1680 = 6080；110 关全首通 = 63800 |
| 新档态 | `0/110 关 · 1/11 章 · 0 / 63,800` |
| 编译 | `compilationFailed: false`、`consoleErrors: 0` |

截图：`HelloWorldProject/Assets/Screenshots/stage-summary.png`（有进度）、`HelloWorldProject/Assets/Screenshots/stage-fresh.png`（新档）；4b 的 `stage-panel.png` 已被 `stage-summary.png` 取代删除。

已确认不是 bug 的现象：面板保持打开时连续改数据，浏览章节不会重置——重置只在「刚打开」那一帧触发（4b 特意加的 `_stagePanelWasOpen`）。正常玩法遇不到，面板开着时游戏整局冻结。要拿干净的新档截图须先关面板再打开。

## 工具注意

Editor 运行期间外部覆写 `HelloWorldProject/Assets/Scripts/*.cs` 会间歇失败：`Error: ReplaceFileW EIO (Win32 1175): <路径>`（本步在 `Stages.cs`、`GameUI.cs` 上各撞到一次以上）。这是 Unity 正持有同名文件，等 4–6 秒重试即成功，不需要改文件策略。

## 遗留项

1. `EliteUnlockLevel=11` / `BossUnlockLevel=21` 仍是死配置：关卡制已接管精英/Boss 的出现规则，这两个字段连 json 都还在（4a 就提出，4d 未处理）。
2. 关卡怪级固定、无软上限；`StageClearDelay=3` 也拦不住「5 秒连打 5 关」（4c 的新数据点，4d 未处理）。

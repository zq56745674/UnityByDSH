---
id: "763c8adf-09a9-48af-9ac6-607a7011a53c"
title: "Unity AutoBattle 首通奖励与通关横幅（4c）：clearedStages 存档 v6、StageClearDelay 接线"
description: "HelloWorldProject 关卡制（4a/4b）之上的 4c：首通奖励（只给金币的设计理由、120+80×(章-1)、章末+600）、PlayerData.clearedStages(List&lt;int&gt;, key=chapter*100+stage)与存档 v6、MarkStageFirstCleared 幂等、死配置 StageClearDelay 的发现与接线（_justClearedStage，默认 2→3 秒）、通关横幅合成单条与 CanvasGroup 覆盖浮层、选关面板首通汇总、Play 模式逐项实测，以及两个死配置/关卡怪级软上限遗留项。"
status: "active"
created_at: "2026-10-15T05:02:43.267Z"
updated_at: "2026-10-15T05:02:43.267Z"
content_hash: "51e08a05542027d99c975687699574c1e38692683baf269f456fa93c766e3a2c"
source_paths:
  - "HelloWorldProject/Assets/Scripts/GameController.cs"
  - "HelloWorldProject/Assets/Scripts/Models.cs"
  - "HelloWorldProject/Assets/Scripts/SaveSystem.cs"
  - "HelloWorldProject/Assets/Scripts/GameConfig.cs"
  - "HelloWorldProject/Assets/Scripts/GameConfigData.cs"
  - "HelloWorldProject/Assets/Scripts/GameUI.cs"
  - "HelloWorldProject/Assets/Resources/game_config.json"
  - "HelloWorldProject/Assets/Screenshots/chapter-clear.png"
  - "HelloWorldProject/Assets/Screenshots/stage-panel.png"
session_ids:
  - "3b308f95-93bd-4533-987b-89f4862585ff"
memory_body_ids:
  []
---

# Unity AutoBattle 首通奖励与通关横幅（4c）

工程 `HelloWorldProject/`（Universal 2D，Editor 6000.6.5f1），全程序化搭建的挂机自动战斗。本步在关卡/章节制之上叠加「首通奖励」，规则与推进逻辑见 4a 文档 `418948ff-876f-4f76-90af-50e55a5c8727`，选关面板见 4b 文档 `86f3fe99-d412-41f3-a55b-12811d5c3288`；本文不重复其规则。编译 `compilationFailed: false`、`consoleErrors: 0`，Play 模式逐项实测。

## 设计决策：首通只给金币，不给装备

这是刻意的取舍。掉落系统本来就在发装备，首通若再塞一件，就得回答「自动装备还是卖掉」，而这两件事都绑在掉落流程里（评分比较），硬塞会绕开那套逻辑。金币是干净的、无歧义的。

```
120 + 80 × (章节号 - 1)      每关首通
+ 600                        章末额外（ChapterClearGoldBonus）
```

第 2 章普通关 +200、章末 +800；第 3 章普通关 +280。

## 存档 v6 与首通记录

- `HelloWorldProject/Assets/Scripts/Models.cs`———`PlayerData.clearedStages`：`List<int>`，关卡编码为 `chapter * 100 + stage`（`PlayerData.StageKey(chapter, stage)`）。
- **必须用 `List` 而不是 `HashSet`**：`JsonUtility` 不序列化 `HashSet`。这个坑 4a 已经踩过一次，此处沿用 List。
- `HelloWorldProject/Assets/Scripts/SaveSystem.cs`：`CurrentSaveVersion` 升到 **6**；同一处补了漏掉的 `using System.Collections.Generic;`（v6 引入 `List<int>` 时遗漏，编译报 `SaveSystem.cs(55,16): error CS0246: The type or namespace name 'List<>' could not be found`）。
- 幂等发奖在 `PlayerData.MarkStageFirstCleared(chapter, stage)`——先查 `Contains` 再 `Add`，返回 true 才发奖励：

```csharp
int key = StageKey(chapter, stage);
if (clearedStages.Contains(key)) return false;
clearedStages.Add(key);
return true;
```

## 中途发现的真 bug：`GameConfig.StageClearDelay` 是死配置

4a 加了这个配置项、也写进了 `Assets/Resources/game_config.json`，但**没有任何代码读它**，只有两处声明、无第三处引用：

```
HelloWorldProject/Assets/Scripts/GameConfigData.cs:107:  public float StageClearDelay = 2f;
HelloWorldProject/Assets/Scripts/GameConfig.cs:294:      public static float StageClearDelay = 2f;
```

4c 正好需要它：通关后若 0.5 秒就进下一关，横幅还没看清怪已经在打了。接线方式是加 `_justClearedStage` 标志，重生延迟二选一：

```csharp
float respawnDelay = _justClearedStage
    ? GameConfig.StageClearDelay
    : GameConfig.MonsterRespawnDelay;
_justClearedStage = false;
```

默认值同时从 2 秒提到 **3** 秒（2 秒实战仍读不完横幅），`GameConfig.cs`、`GameConfigData.cs`、`game_config.json` 三处同步。

## 通关横幅

- 第一版拆成两条（`★ 首通 2-10` 与 `✦ 第 3 章 已解锁`），实测后者把前者顶掉，玩家看不到拿了多少金币。改为在通关分支末尾**合成一条**；`GrantFirstClearReward` 不再自己弹横幅、只返回金币数，由调用方拼接。
- 终稿文案：标题 `★ 第 2 章 通关！`，副标题 `+800 金币 · 第 3 章已解锁（怪物 Lv.30）`。
- 实现要点：横幅挂在 Canvas 的**最后**并带 `CanvasGroup`，因此能盖在选关/技能面板之上；最后 0.6 秒淡出；加**金边**（半透明底色压不住下面的日志文字，第一版能透读出后面的字）。公开只读状态：`BannerTitle` / `BannerSubtitle` / `BannerTimeLeft`，空串表示不显示。
- 章节解锁分支顺带 `SaveSystem.Save(Player)`。

## 记账顺序

`HelloWorldProject/Assets/Scripts/GameController.cs` 通关分支（约 778-808 行）里，**先记账再推进**：`AdvanceStage()` 会改 `chapterIndex`，之后再认不出刚打完哪一关。

```csharp
int clearedChapter = Player.chapterIndex;
int clearedStage = Player.stageIndex;
bool chapterFinal = clearedStage >= StageCatalog.StagesPerChapter;
bool firstClear = Player.MarkStageFirstCleared(clearedChapter, clearedStage);   // 先记账
int prevMaxChapter = Player.maxChapterUnlocked;
bool advanced = Player.AdvanceStage();                                          // 再推进
if (firstClear) GrantFirstClearReward(clearedChapter, chapterFinal);
```

## UI：选关面板首通汇总

`HelloWorldProject/Assets/Scripts/GameUI.cs` 的本章规则文本（约 765 行）新增「已首通：10/10」「首通奖励：280 金币（关末再 +600）」，与既有的怪物等级、每关击杀、关卡数、解锁提示并列。

## 实测（Play 模式）

| 项 | 结果 |
|---|---|
| 普通关首通 | `★ 首通奖励：+280 金币`（第 3 章：120+80×2） |
| 章末首通 | `★ 首通奖励：+800 金币（含章节通关奖励 +600）` |
| 金币增量 | 从 0 起通关 2-10 后 `gold=982` = 800 首通 + 182 战斗掉落 |
| 重刷已通关 | `gold` 5000 → 5182，**只涨战斗掉落的 182**，无首通行、无横幅 |
| 章节解锁 | `maxChapterUnlocked` 2 → 3，第 4 章起仍是 `×` 灰字 |
| 通关停顿 | `state=WaitingRespawn` 停在 3-1 不自动推进（证明 `StageClearDelay` 真接线了） |
| 横幅 | `banner=[★ 第 2 章 通关！] [+800 金币 · 第 3 章已解锁（怪物 Lv.30）] left=0.2` |
| 编译 | `compilationFailed: false`、`consoleErrors: 0` |

验证手法沿用 4a/4b：`eval` 在 Play 模式注入 `PlayerData`（等级 60、传奇装备、`maxChapterUnlocked`、`SelectStage`、把 `stageKillCount` 置为 `killTarget - 1`）后读回 `Player.gold` / `CurrentStage.Code` / `CurrentState` / `BannerTitle` / `BannerSubtitle` 与 `Log` 内容，并临时把 `StageClearDelay` 拉到 10 秒观察停顿。

截图：`HelloWorldProject/Assets/Screenshots/chapter-clear.png`（通关横幅）、`HelloWorldProject/Assets/Screenshots/stage-panel.png`（选关面板首通汇总）。

## 遗留（用户尚未决策）

1. **`EliteUnlockLevel` / `BossUnlockLevel` 现在彻底是死配置**——关卡制接管了精英/Boss 的出现规则，这两个字段连 json 都还在。`StageClearDelay` 刚证明了死配置会骗人，建议删除，但尚未执行。
2. **关卡怪级固定、没有软上限**（4a 遗留，4c 测出新数据点）。玩家等级远高于关卡怪级时几乎秒杀，`StageClearDelay=3` 也救不了「5 秒连打 5 关」。
3. 下一步候选：4d（章节进度总览 + 首通奖励汇总），或先清理上述两个死配置。

## 环境坑（复用）

写 `GameController.cs` / `game_config.json` 时多次出现 `Error: ReplaceFileW EIO (Win32 1175)`，同一次编辑重试即成功——Unity 侧持有文件句柄导致的瞬时写失败，不是内容问题。

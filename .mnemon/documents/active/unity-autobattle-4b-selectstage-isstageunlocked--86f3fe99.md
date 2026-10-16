---
id: "86f3fe99-d412-41f3-a55b-12811d5c3288"
title: "Unity AutoBattle 选关面板（4b）：章节级解锁、SelectStage/IsStageUnlocked API 与面板冻结战斗"
description: "HelloWorldProject 关卡制（4a）之上的选关面板实现：GameController 新增 StagePanelOpen/MaxChapterUnlocked/IsStageUnlocked/SelectStage/ToggleStagePanel，章节级解锁口径，与技能面板互斥、面板打开时 Update 整局冻结，四个实现坑（StageCatalog.Get 越界钳位、U+1F512 无字形、已过标记按进度线、浏览章节被每帧重置），以及 Play 模式逐项实测结果与遗留项。"
status: "active"
created_at: "2026-10-15T04:45:27.012Z"
updated_at: "2026-10-15T04:45:27.012Z"
content_hash: "8f803fcd2150a44243792c019849cc209575366f8c8cd147217c606fcc210f47"
source_paths:
  - "HelloWorldProject/Assets/Scripts/GameController.cs"
  - "HelloWorldProject/Assets/Scripts/GameUI.cs"
  - "HelloWorldProject/Assets/Screenshots/stage-panel.png"
  - "HelloWorldProject/Assets/Screenshots/stage-switch.png"
session_ids:
  - "76a59de8-851c-460f-b560-b57b6cf4ee7e"
memory_body_ids:
  []
---

# Unity AutoBattle 选关面板（4b）

工程 `HelloWorldProject/`（Universal 2D，Editor 6000.6.5f1）。用户指令：「4b 做完了」对应的实现步骤为 4b（选关界面）。编译通过并在 Play 模式逐项实测。

本步构建在关卡/章节制改版之上，规则、`Stages.cs`、存档 v5 与推进逻辑见文档 `418948ff-876f-4f76-90af-50e55a5c8727`（4a）；该文档明确「4a 没做选关界面，只保证玩家知道自己在哪一关」，本文补上这块，不重复其规则。选关面板与技能三选一面板同属 `GameController` 状态机之上的浮层，技能侧结论见 `bd713554-0919-4cdf-8426-557fc4ba4e4a`。

## 交互设计

- 左栏 11 个章节按钮，右栏是该浏览章的 10 个关卡，下方为本章规则（每关击杀数、关底档次、关卡数、解锁提示）。
- **章节级解锁**：解锁某章即该章 10 关全可打，没有「章内再逐关解锁」的二次门槛。
- 入口是左面板等级行右侧的按钮，按钮本身即当前关速览（如 `关卡 3-5 · 怪 Lv.30`）。做 4a 时关卡信息挤在等级行内，`关卡 11-10 · 怪 Lv.110` 几乎撑爆该行，所以挪到独立按钮上，既当入口又当状态显示。
- 选关面板与技能面板互斥，两个浮层不会叠加。

## 新增 API（`HelloWorldProject/Assets/Scripts/GameController.cs`）

```csharp
public bool StagePanelOpen { get; set; }
public int MaxChapterUnlocked => ...;
public bool IsStageUnlocked(int chapter, int stage);
public void SelectStage(int chapter, int stage);
public void ToggleStagePanel();
```

- `Update()` 开头加 `if (StagePanelOpen) return;`——面板一开整局冻结（含商店倒计时）。否则玩家挑关时怪照常输出，可能死在菜单里。
- `GameUI.cs` 侧相关成员：`_selectedSkillSlot`（技能槽，与关卡逻辑无关但同在选中态管理）、`RefreshStagePanel()`、`Refresh()` 中的 `RefreshShop(p)` / `RefreshSkillBar(p)` 每帧刷新链。

## 四个踩到的坑（复用价值最高）

1. **`StageCatalog.Get` 从不返回 null**——它把越界章节号 `Mathf.Clamp` 回合法范围。因此 `IsStageUnlocked` 不能拿 `Get(...) == null` 当边界检查，必须自己校验 `chapter` / `stage` 范围，否则 `SelectStage(1, 999)` 会被静默当成 `1-10`（等于点开最后一关）。
2. **锁形 emoji 没有字形**——第一版章节锁定态写 `🔒 第 5 章`，渲染为空白。Unity 默认字体带 ⚔ ☠ 🛒 等，但没有 U+1F512。改用 `×` 前缀 + 灰字。
3. **「已过」标记的划线口径**——第一版按 `maxChapterUnlocked` 划线，结果 3-6~3-10（当前章尚未打到的关）全被标成 `✔ 已过`。正确划线是**当前进度线**：当前章之前的章算已过，当前章内按关卡号比较；回头重刷低章时高章不标「已过」，否则玩家正站在低章却看到高章标记，只会看花。
4. **面板浏览章节被每帧弹回**——`RefreshStagePanel()` 每帧执行，初版无条件把 `_stageViewChapter` 设为当前章，导致点别的章节立刻被拉回。加 `_stagePanelWasOpen`，只在刚打开那一帧重置浏览章。

## 实测（Play 模式）

| 项 | 结果 |
|---|---|
| 解锁态 | 第 1-4 章可选（第 3 章高亮）、第 5-11 章灰字 `×` |
| 关卡态 | 3-1~3-4 `✔ 已过`、3-5 `▶ 当前`、3-6~3-10 无标记、3-10 标「章末」 |
| 切关 | `SelectStage(2,3)` → 状态回到 `WaitingRespawn`、怪 Lv.20、击杀数归零、掉开当前怪 |
| 锁定章拒绝 | `SelectStage(9,1)` → 关卡不变，日志 `🔒 第 9 章尚未解锁（当前最高 第 4 章）` |
| 越界拒绝 | `SelectStage(1,999)` → 关卡不变 |
| 同关点击 | 只重置本关进度，不换关、不动战斗状态 |
| 通关解锁 | 4-10 通关 → 5-1、`maxChapterUnlocked` 变 5、第 5 章立即可选 |
| 终局 | 11-10 再推进 `AdvanceStage()` 返回 `false`，进度不动 |
| 编译 | `compilationFailed: false`、`consoleErrors: 0` |

截图：`HelloWorldProject/Assets/Screenshots/stage-panel.png`（面板全貌）、`HelloWorldProject/Assets/Screenshots/stage-switch.png`（切关后战斗状态）。

验证手法：用 `eval` 在 Play 模式直接注入 `PlayerData`（等级 60、`maxChapterUnlocked=4`、`chapterIndex=3/stageIndex=5`、传奇装备、金币 2 万）后调 `SelectStage` / `AdvanceStage` 并读回 `Player.Log` 尾部，再用 `capture_game_view` 截图确认渲染；每次改脚本后 `editor_stop` → `AssetDatabase.Refresh(ForceUpdate)` → `editor_play` 重进。收尾已 `editor_stop`、`SaveSystem.Clear()` 清档。

## 遗留

1. **没有章节目录总览**（如「11 章 × 10 关、已通 34 关」的进度条）——当前是纯菜单，看不出整体进度。
2. **4a 的两个待定未动**：`EliteUnlockLevel=11` / `BossUnlockLevel=21` 已被关卡制绕过；关卡怪级完全固定，是否加软上限仍未决（同在文档 `418948ff-876f-4f76-90af-50e55a5c8727` 中记录，尚无用户决定）。
3. 下一步候选：**4c 首通奖励 + 章节解锁提示**（当前通关只有一行日志，没有奖励反馈）。

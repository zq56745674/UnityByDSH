---
id: "418948ff-876f-4f76-90af-50e55a5c8727"
title: "Unity AutoBattle 关卡/章节制改版（4a）：StageCatalog 规则、进度存档 v5 与阵亡重打"
description: "HelloWorldProject 由「按击杀计数刷精英/Boss」改为「章节关卡推进」：新增 Stages.cs（StageCatalog 程序化生成 11 章 × 10 关、关底精英/Boss）、PlayerData 关卡进度字段、存档升 v5 迁移、GameController 推进与阵亡重置逻辑、GameUI 关卡显示，含 Play 模式实测证据与两个待用户决策的开放问题（EliteUnlockLevel/BossUnlockLevel 被绕过、关卡怪级是否加软上限）。"
status: "active"
created_at: "2026-10-14T17:40:32.201Z"
updated_at: "2026-10-14T17:40:32.201Z"
content_hash: "c44c7d2c3bcbb712151c8c70ab635afaa5ef9263443efc3a2389b23e1a92ee5c"
source_paths:
  - "HelloWorldProject/Assets/Scripts/Stages.cs"
  - "HelloWorldProject/Assets/Scripts/GameController.cs"
  - "HelloWorldProject/Assets/Scripts/Models.cs"
  - "HelloWorldProject/Assets/Scripts/SaveSystem.cs"
  - "HelloWorldProject/Assets/Scripts/GameUI.cs"
  - "HelloWorldProject/Assets/Scripts/GameConfig.cs"
  - "HelloWorldProject/Assets/Resources/game_config.json"
  - "HelloWorldProject/Assets/Screenshots/stage.png"
  - "HelloWorldProject/Assets/Screenshots/stage-ui.png"
session_ids:
  - "6b729242-d49d-427a-85f5-31b2ef6cfa08"
memory_body_ids:
  []
---

# Unity AutoBattle 关卡/章节制改版（4a）

工程 `HelloWorldProject/`（Universal 2D，Editor 6000.6.5f1）。用户指令：「开始 4a」。编译通过并在 Play 模式逐项实测；收尾已 `editor_stop`、`runInBackground=false`、存档已清空（`SaveSystem.Clear()` → `hasSave=False`）。

本改版把战斗节奏从「无限刷怪 + 按普通怪击杀计数刷精英/Boss」换成「章节关卡推进」，与既有技能系统文档 `bd713554-0919-4cdf-8426-557fc4ba4e4a`（技能三选一）、`a68e8f3d-4799-4bff-8ae2-942a12bae043`（技能获取与 100 级后胜率）中关于 `GameController` 状态机与 `State` 枚举的描述互补：本次新增/改动的状态推进（`WaitingRespawn` → `Fighting` / `Shopping` / `Reviving` / `SkillChoice`）不改变那两份文档的技能发放结论。

## 新增 `HelloWorldProject/Assets/Scripts/Stages.cs`

```csharp
public class StageDef {
    public int chapter, stage;        // 1 起
    public int monsterLevel;          // 关卡固定怪级
    public int killTarget;            // 通关所需击杀数
    public MonsterTier clearTier;     // 关底档次
    public string Title => $"第 {chapter} 章 {Code}";
}
public static class StageCatalog { Get / KillTargetOf / ClearTierOf / Advance }
```

骨架与规则全部程序化生成，参数进了 `GameConfig`（可热改，已同步到 `Assets/Resources/game_config.json`）：`ChapterCount=11`、`StagesPerChapter=10`、`ChapterMonsterLevelStep=10`、`StageKillTargetBase=3`、`FirstBossChapter=2`。

- 11 章 × 10 关；第 N 章怪 **Lv.10N**（1-1 = Lv.10，11-10 = Lv.110）。
- 击杀目标按章递增：1~3 章 3 只、4~6 章 4 只、7~9 章 5 只、10 章起 6 只。
- **普通关关底是精英，章末关（x-10）是 Boss**；第 1 章作为新手章章末也只给精英（`FirstBossChapter=2`）。

## 推进逻辑取代旧的「刷怪计数」

`HelloWorldProject/Assets/Scripts/GameController.cs` 中原先「每 10 只普通怪刷精英、每 50 只刷 Boss」的整块逻辑删掉（`_eliteKillCount` / `_bossKillCount` 字段保留但不再用于推进）。现在：

- `SpawnMonsterNow()` 的怪级来源从 `Player.level` / `GameConfig.TierMonsterLevel(Player.level)` 改为 **`Player.CurrentStage.monsterLevel`**——怪级不再跟主角等级走。
- 击杀后 `stageKillCount++`；**本关剩 1 只**时把 `_pendingTier = clearTier`，复用既有商店流程（`OpenShop` → 关底），关底前回满血。
- 打满 `killTarget` → `AdvanceStage()` 进下一关；章末关之后进下一章 1 关；终局 11-10 再推进返回 `False`（不动）。
- **阵亡 = 本关重来**：`HandlePlayerDied()` 里若当前怪是非普通档（精英/Boss）则重置挑战计数，并新增日志 `✖ 本关进度作废（已击杀 N/M），第 X 章 X-Y 重新开始`，`stageKillCount` 归零、关底档次撤回、`CurrentState = State.Reviving` → 复活后 `SpawnMonster()`。这就是关卡制下的关卡墙：打不过就卡在这一关反复重试。

## 存档升到 v5

`HelloWorldProject/Assets/Scripts/SaveSystem.cs`：`CurrentSaveVersion` 4→5，新增 `chapterIndex` / `stageIndex` / `stageKillCount` / `maxChapterUnlocked`。`HelloWorldProject/Assets/Scripts/Models.cs` 增加 `PlayerData` 关卡进度方法（`CurrentStage` / `StageClearTier` / `EnterStage(chapter, stage)` / `AdvanceStage()`）。

**迁移按版本号判断而不是按字段值**——`JsonUtility` 对缺失字段给 0，只看值分不清「旧档」与「真的在第 1 关」。这是本工程存档迁移的既定做法，后续再加字段沿用。

## 最小 UI

`HelloWorldProject/Assets/Scripts/GameUI.cs` 等级行改为 `等级 42    巅峰 3    关卡 1-1 · 怪 Lv.10`（`_levelText`，约 910-912 行）。4a 没做选关界面，只保证玩家知道自己在哪一关、这关怪多少级。

## 实测（Editor eval + Play 模式）

| 项 | 结果 |
|---|---|
| 关卡数据 | 章 1 Lv.10/3 只/精英 · 章 6 Lv.60/4 只 · 章 9 Lv.90/5 只 · 章 11 Lv.110/6 只/章末 Boss |
| 推进 | `3-10 → AdvanceStage() True → 4-1`（已击杀归 0）；`11-10 再推进 False` 仍在 11-10 |
| 实战日志 | `▼ 本关还剩 1 只（关底 精英怪）` → `🛒 即将迎战精英怪，商店开放 3 秒` → `★ 通关 3-4！进入 第 3 章 3-5（怪物 Lv.30）` |
| 怪级固定 | 主角 Lv.60 打第 3 章，怪全是 Lv.30 |
| 阵亡 | `✖ 本关进度作废（已击杀 2/3），第 3 章 3-5 重新开始`，`stageKillCount` 回到 0，状态 `Reviving` |
| v4 旧档 | 读档落回 1-1 / 击杀 0 / 解锁章 1（等级等其它字段正常保留）；再存一次变 v5 且含 `chapterIndex` / `maxChapterUnlocked`；v5 往返正常 |
| 编译 | `compilationFailed: false`、`consoleErrors: 0` |

截图：`HelloWorldProject/Assets/Screenshots/stage.png`、`stage2.png`、`stage-ui.png`。

## 过程中的工具坑（本工作区复现）

- 用 `eval` 调 `GameController` 的 private 方法（如 `SpawnMonsterNow`）**编译不过**，报错却是 `Unreachable code detected（第 11 行，第 12 列）` 之类的位置误导信息。可靠做法是用反射调非 public 方法（`typeof(AutoBattle.GameController).GetMethod("HandlePlayerDied", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(gc, null)`），或用 Play 模式走自然流程。
- 造 v4 旧档时不要依赖磁盘上的 `autobattle_save.json`（本工程存档实际在 `PlayerPrefs`，路径 `GameConfig.SaveKey`，磁盘文件不存在）；用 `PlayerPrefs.SetString(SaveKey, legacyJson)` 直接注入旧 JSON 再验证迁移。

## 开放问题（等用户决策，4a 未处理）

1. **`GameConfig.EliteUnlockLevel=11` / `BossUnlockLevel=21` 现在被绕过**——关底档次只看关卡，不再看主角等级。是删掉这两个常量，还是留作将来的「等级门槛」？
2. **关卡怪级完全固定**，所以第 1 章对 Lv.60 的号是白给、第 8 章对 Lv.40 的号是必死。属关卡制本意（靠选关/回头刷），但是否要给软上限（如关卡怪级不超过主角等级 +N）以防卡死？
3. 用户被问到是否接着做 **4b（选关界面：章节目录 + 锁定态 + 可重复刷已通关卡）**——`maxChapterUnlocked` 字段已为它预留。

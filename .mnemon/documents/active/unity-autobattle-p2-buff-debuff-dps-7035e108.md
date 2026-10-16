---
id: "7035e108-9fb2-4a33-8f12-8282b1db79d0"
title: "Unity AutoBattle 战斗可读性层（P2）：Buff/Debuff 栏、伤害飘字、实时 DPS 的实现与两处坑"
description: "HelloWorldProject 的 GameUI 战斗可读性改造执行记录：Buff/Debuff 状态栏的数据来源与行定义、伤害飘字池（颜色/字号/日志解析游标/日志截断处理）、日志行右侧实时 DPS 与期望 DPS、CreateRect 的「y 向下为正」坐标系坑、Play 模式下 AssetDatabase.Refresh 不一定重编译的验证纪律，以及收尾清理项。"
status: "active"
created_at: "2026-10-14T17:13:51.632Z"
updated_at: "2026-10-14T17:13:51.632Z"
content_hash: "9a989927a40f429d7e25d5fbc5983de0b9f040e2bc50a4eca3ddd0b29fb1f02f"
source_paths:
  - "HelloWorldProject/Assets/Scripts/GameUI.cs"
  - "HelloWorldProject/Assets/Scripts/BattleSystem.cs"
  - "HelloWorldProject/Assets/Scripts/GameController.cs"
  - "HelloWorldProject/Assets/Scripts/GameConfig.cs"
  - "HelloWorldProject/Assets/Scripts/Models.cs"
  - "HelloWorldProject/Assets/Screenshots/p2.png"
session_ids:
  - "1f4c5132-05e9-4ec9-9036-adc88d5cc4c7"
memory_body_ids:
  []
---

# Unity AutoBattle 战斗可读性层（P2）

工程：`HelloWorldProject/`（Universal 2D，Editor 6000.6.5f1）。三项 UI 可读性改造已实现并在 Play 模式逐项实测通过。
相关既有文档（勿重复其内容）：`7c3b8ebf-3e86-4b45-8f70-ad53ec9188c5`（巅峰/技能系统与 UI 接入点）、`6231bd5c-ca9e-454e-8f30-c67546e352d7`（技能数值平衡与护盾实现坑）、`4f6621d0-f8c4-4dab-81fb-719d78ddf259`（装备评分口径）。

## 源文件与新增接口

- `HelloWorldProject/Assets/Scripts/GameUI.cs`（885 行）——三项功能全部在此：右面板状态栏、飘字层、DPS 行。界面全部由代码搭建（`BuildCanvas` / `BuildLeftPanel` / `BuildSkillBar` / `BuildRightPanel` / `BuildShopPanel` / `BuildSkillChoicePanel` / `BuildSkillPanel`），无 Prefab。
- `HelloWorldProject/Assets/Scripts/BattleSystem.cs`——新暴露 Buff 剩余时间：`BerserkTimeLeft`、`NearDeathTimeLeft`、`ShieldReadyIn`。
- `HelloWorldProject/Assets/Scripts/GameController.cs`——新增 `public BattleSystem Battle => _battle;`，把战斗系统实例交给 UI 读取，避免 UI 再去找 BattleSystem。
- `Models.cs:298`（`PlayerData`）与 `Models.cs:634`：`public int shield;`——护盾值的唯一来源。
- 截图：`HelloWorldProject/Assets/Screenshots/p2.png`（唯一保留的成果截图）。

## 1. Buff/Debuff 状态栏

右面板新增一条状态行，按状态存在与否显示/隐藏：

| 显示 | 触发条件 | 颜色 |
|---|---|---|
| ◆ 狂暴 剩余秒 | 狂暴 Buff 生效中 | 橙 |
| ◆ 免死 剩余秒 | 濒死爆发的免死窗口 | 粉 |
| ◆ 护盾 N | `player.shield > 0` | 青 |
| 技能名 冷却/就绪 | 两个主动技能槽 | 灰（冷却中）/ 绿（就绪） |

设计要点：剩余秒与就绪状态**只在 UI 侧做格式化**，数值全部来自 `BattleSystem` 新暴露的三个只读属性 + `player.shield`，不在 UI 里复算 CD，避免与战斗逻辑出现两份真值。

## 2. 伤害飘字

- 池大小 **14** 个对象，按来源分色分号：主角普攻 白 19 号；暴击 金 26 号带感叹号；技能 青 24 号；被击 红 18 号（被击暴击 深红 24 号）带负号。
- 动效：**1.15 秒**向上飘并淡出。
- 数据源不是新加事件，而是**解析日志行**：`主角 → 怪物` / `怪物 → 主角` / `✦ 技能！`（`BattleSystem.cs` 中对应的 `log.Add(new LogEntry(...))`，约 233、240、306、333、373、416、445、451 行）。
- **日志解析游标**：用 `_game.LogVersion`（`GameController.cs:89` 的属性；写入处 833/835 与截断处 840-844 各自 `LogVersion++`）+ 条数差作为游标。关键约束：`Log` 超过 `GameConfig.MaxLogEntries` 时会**截断头部**，所以只能处理「末尾新增的那几条」，不能按绝对下标回放，否则日志满后下标会整体前移。
- `LogEntry` 字段只有 `message`(string) 与 `color`(Color)；构建文本时用 `ColorUtility.ToHtmlStringRGB(e.color)`（`RefreshLog`，`GameUI.cs:734-755`），且只在 `LogVersion` 变化时重建，避免每帧拼大字符串。

## 3. 实时 DPS

放在日志标题行的右侧：**0.4 秒窗口平滑**的实时 DPS，旁边并列当前这只怪的**期望 DPS** 做对照。对照值就是「理论值 vs 实测值」，用来一眼看出输出是否被 buff/技能抬升。实测截图数值示例：`实时 DPS 2847    期望 7692`（刚开战正在爬升）。期望 DPS 的计算沿用 `HelloWorldProject/Assets/Scripts/EquipmentScore.cs:76` 的 `public static float Dps(PlayerData p, MonsterData m)`。

## 4. 两处坑（复用价值高）

### 坑 1：`GameUI.CreateRect` 的坐标系是「y 向下为正」

```csharp
rt.anchorMin = new Vector2(0f, 1f);
rt.anchorMax = new Vector2(0f, 1f);
rt.pivot     = new Vector2(0f, 1f);
rt.anchoredPosition = new Vector2(x, -y); // y 向下为正，写起来更直观
rt.sizeDelta = new Vector2(w, h);
```

飘字第一版直接写 `anchoredPosition = _floatPos`，**整层飘到了面板外面**。正确做法：内部统一存「向下偏移量」，只有赋值给 `anchoredPosition` 时才取负。新增任何按坐标定位的 UI 元素都要遵守这个约定。

### 坑 2：Play 模式下 `AssetDatabase.Refresh` 不一定重编译脚本

`editor_play` 连着两次返回 **"Already in play mode"**，说明新代码压根没进 Play 会话——白截了好几轮图才发现飘字没生效（当次 Buff 栏和期望 DPS 都正常，因为那部分是更早的改动）。**要验证新改动必须先 `editor_stop` 再 `AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate)`，然后重新 `editor_play`。** 这与 AGENTS.md 里 `recompile` 报 `up_to_date` 不等于已编译是同一条纪律的两个表现。

## 5. 验证流程与收尾

- 验证用 Editor `eval` 直接构造场景：`FindAnyObjectByType<AutoBattle.GameController>()` → 设 `Player.level=100 / paragonLevel=200`、`LootSystem.CreateEquipment(...)` 造 Lv.110 传说武器与衣服、`gold=50000`、`potionCount=5`、`FillHp()`，再 `ToggleRunning()`；截图走 `capture_game_view`。
- 收尾必做：`Application.runInBackground = false`（验证期需临时设 true，`editor_play` 本身没有 `run_in_background` 参数）、`editor_stop`、`console_status` 确认 0 错误。
- 清理：临时补丁脚本（`patch_ui.ps1`、`patch_p2.ps1`、`patch_float.ps1` 等）与全部中间截图（`p2-float*.png`、`p2-a/b.png`、`g1..g6.png`、`h1..h6.png`、`p2-1..8.png`）已删除，只保留 `HelloWorldProject/Assets/Screenshots/p2.png`。
- `E:\pytest\unitydsh1` 不是 git 仓库（`fatal: not a git repository`），所以没有 diff 可回滚，清理只能靠显式删文件。

---
id: "7c3b8ebf-3e86-4b45-8f70-ad53ec9188c5"
title: "Unity AutoBattle 巅峰等级与技能系统：设计、公式与接入点"
description: "HelloWorldProject 中巅峰等级（Paragon）与 15 个技能（主动/被动/触发）的完整设计：数值表、经验与技能等级公式、战斗循环接入点、6 槽装备限制与 UI 布局，含实测验证结论。"
status: "active"
created_at: "2026-10-12T16:41:30.197Z"
updated_at: "2026-10-12T16:41:30.197Z"
content_hash: "343ff924fb05029fd4feb87c1955df4f18ab81cbb84bef1fd5debe0ed73a765f"
source_paths:
  - "HelloWorldProject/Assets/Scripts/Skills.cs"
  - "HelloWorldProject/Assets/Scripts/GameUI.cs"
  - "HelloWorldProject/Assets/Scripts/GameController.cs"
  - "HelloWorldProject/Assets/Scripts/Models.cs"
session_ids:
  - "21ad90d2-8d0c-4643-b5a4-2ca70a6a2ca1"
memory_body_ids:
  []
---

# Unity AutoBattle 巅峰等级与技能系统

工程：`HelloWorldProject/`（Universal 2D，Editor 6000.6.5f1）。实现日期 2026-10-12，已完成并逐项在 Play 模式实测。

## 源文件

- `HelloWorldProject/Assets/Scripts/Skills.cs`（新增）——技能池定义，15 个技能的数值集中于此，便于平衡调整。
- `HelloWorldProject/Assets/Scripts/GameUI.cs`——技能栏与技能面板、面板布局常量。
- `HelloWorldProject/Assets/Scripts/GameController.cs`——技能装备/解锁查询、默认装配补齐、开关面板。
- `HelloWorldProject/Assets/Scripts/Models.cs`——`PlayerData` 的巅峰字段、`ExpProgress`、`SkillLevel`、`GetSkillSlot`。
- 截图：`HelloWorldProject/Assets/Screenshots/skill-bar.png`、`skill-panel.png`。

## 技能池（15 个）

| 类别 | 技能 | 数值 |
|---|---|---|
| 主动 | 重击 | 250% 攻击伤害，CD 8s |
| 主动 | 旋风斩 | 3 次 × 80%，CD 12s |
| 主动 | 狂暴 | 10s 内攻击 +50%、暴击率 +20%，CD 30s |
| 主动 | 斩杀 | 对生命 < 30% 的怪造成 600%，CD 20s |
| 被动 | 钢铁之躯 | 防御 +15% |
| 被动 | 狂暴之心 | 暴击率 +8% |
| 被动 | 致命打击 | 暴击伤害 +30% |
| 被动 | 吸血鬼 | 吸血 +5% |
| 被动 | 迅捷 | 攻击速度 +12% |
| 被动 | 巨人杀手 | 对精英/Boss 伤害 +20% |
| 被动 | 不死之身 | 生命 < 20% 时减伤 +40% |
| 触发 | 反击 | 受击 50% 概率反弹 70% 攻击伤害 |
| 触发 | 暴击连锁 | 暴击时 30% 概率追加 50% |
| 触发 | 濒死爆发 | 生命 < 15% 时攻击 +100%，持续 5s，CD 30s |
| 触发 | 护盾生成 | 每 15s 获得 15% 最大生命护盾 |

## 巅峰等级公式

- 满级（Lv.100）后经验转为巅峰经验；每级 **+1 攻击 / +1 防御**。
- 每 10 个巅峰等级**解锁一个被动技能**，顺序固定为池内顺序的前 N 个（非自选）。
- 升一级所需经验 = `2000 + 200 × 当前巅峰等级`。
- 技能等级 = `1 + 巅峰等级 / 10`，技能效果按 `×(1 + (等级−1) × 0.1)` 放大。
- 巅峰上限由 `MaxParagonLevel` 控制（当前 200）。
- 同源设计：「每 10 级获得被动」与「技能等级 +1」共用同一个 10 级间隔。

## 战斗接入

- **主动**：各自独立 CD + **1 秒公共冷却**，冷却结束自动释放；斩杀会等怪物残血才触发。
- **被动**：只在属性计算里叠加（`Defense`/`CritChance`/`CritDamage`/`Lifesteal`/`AttackSpeedBonus` 等），不进战斗循环。
- **触发**：挂在事件上——受击（反击）、暴击（暴击连锁）、低血（濒死爆发）、周期（护盾生成）。
- 装备限制：主动 / 被动 / 触发**各最多 2 个**，共 6 槽；`GetSkillSlot(i)` 取槽位技能 id（-1 为空）。
- 技能 CD **跨战斗保留**，避免每只怪开局放大招。

## 已定的设计权衡

1. **技能伤害不吃暴击**（只吃攻击力、减伤、巨人杀手），保证倍率可预期。
2. 被动解锁顺序固定（前 N 个），不可自选。
3. **新档默认装配**：重击 + 旋风斩 + 反击 + 暴击连锁；被动留空待解锁。**老存档读出来若六槽全空会自动补这套默认装配**（读档路径曾漏掉补默认，导致六槽全为 -1，已修复）。
4. **换技能会重置全部技能 CD**，避免换装刷冷却。
5. 未解锁的被动无法装备（按 `unlockedPassives` 拦截）。

## UI（GameUI.cs）

- 技能栏 6 槽，每槽显示「类别标签 + 名称 + 冷却秒数 + 底部进度条」。
- 点任一槽弹出技能面板：15 个技能按钮，`▶` 标记当前槽位、`●` 标记已装备、灰字标注「巅峰解锁」。
- 布局常量：`LeftPanelW = 420f`、`RightPanelX = 444f`、`RightPanelW = 824f`、`PanelH = 696f`。
- 已修的坑：技能栏曾横向超出面板宽度（槽位排到 484 > 420）且纵向被面板高度裁切；排布需满足槽位总宽 ≤ `LeftPanelW` 且顶部偏移 + 高度 ≤ `PanelH`。

## 实测结论（Play 模式）

| 验证项 | 结果 |
|---|---|
| 技能池 | 15 个（主动 4 / 被动 7 / 触发 4） |
| 巅峰升级 | 加 30000 经验 → 升 10 级 → 巅峰 10、解锁被动 1、技能等级 Lv.2、防御 +10 |
| 解锁顺序 | 仅「钢铁之躯」`解锁=True`，其余 False |
| 钢铁之躯 | 防御 445 → 517（Lv.2 即 16.5%） |
| 迅捷 | 攻速 17.8% → 31.0%（+13.2%），间隔 0.263s → 0.237s |
| 狂暴之心 | 暴击 14.9% → 23.7%（+8.8%） |
| 致命打击 | 暴伤 309% → 342%（+33%） |
| 护盾生成 | 日志 `✦ 护盾生成 +843`（= 5110 × 15% × 1.1） |
| 主动释放 | `✦ 重击！676 伤害`、`✦ 旋风斩！247 伤害` |
| 反击 | `✦ 反击！204/216/190 伤害` |

收尾状态：存档清空、已退出 Play、`runInBackground=false`、`consoleErrors: 0`。

## 复用注意

- 新增/调整技能只改 `Skills.cs` 的数值，战斗与 UI 都从技能池派生。
- 改技能栏布局时同步校验上述两个总量约束，改完必须进 Play 截图确认（`capture_game_view`），仅靠编译通过不足以发现裁切。

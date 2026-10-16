---
id: "8bb7e4c1-5ba6-4738-8ab6-4384c4e62219"
title: "Unity AutoBattle 胜率表改为「模拟玩家三选一」：LateGameProbe 评分口径、新基线表与巅峰 100 回退诊断"
description: "HelloWorldProject 的 LateGameProbe 从「随机送技能」改为模拟真人三选一（PickAndEquipBest + 三类型 Boss 击杀耗时评分），产出新胜率基线表、与旧口径逐档对比、巅峰 100 反常回退（82.5% vs 95.7%）的成因诊断，以及被实测否决的「满槽替换」方案与评分函数三轮迭代理由。"
status: "active"
created_at: "2026-10-14T16:57:04.101Z"
updated_at: "2026-10-14T16:57:04.101Z"
content_hash: "e3804c1fea978121b5a5cc2eba906290de79bc1ea219148ffc233781c9fafa3d"
source_paths:
  - "HelloWorldProject/Assets/Editor/LateGameProbe.cs"
  - "HelloWorldProject/Assets/Scripts/Models.cs"
  - "HelloWorldProject/Assets/Scripts/GameConfig.cs"
session_ids:
  - "7bedef49-f869-40e6-b9c4-59cc8ce78aef"
memory_body_ids:
  []
---

# Unity AutoBattle 胜率表改为「模拟玩家三选一」

用户指令（本次唯一目标）：「重跑一遍更贴真实的胜率」。这是文档 `bd713554-0919-4cdf-8426-557fc4ba4e4a`「遗留与注意事项」里那条待办的落地：该文档记录 `LateGameProbe.Build()` 没跟着三选一改造、仍是随机送技能，所以旧胜率表只是「技能乱给」的下界。执行日期 2026-10-14，工程 `HelloWorldProject`（Unity 6000.6.5f1）。

相关但不同主题：三选一 UI/存档机制 `bd713554-0919-4cdf-8426-557fc4ba4e4a`；技能数值平衡与旧胜率曲线 `6231bd5c-ca9e-454e-8f30-c67546e352d7`；技能边际强度审计 `2ab3d6dc-ceb7-4405-b908-9cd6b792905d`。

## 1. 探针改造（全部落在 `HelloWorldProject/Assets/Editor/LateGameProbe.cs`）

- 入口签名：`public static string RunWinrateCell(int paragon, int perType, bool resetHp, bool detail, int trials, bool smartPick = true)`（约 `:78`）；批量入口 `RunWinrate(int perType, bool resetHp, bool detail, int trials)` 内部对 `{0,10,20,30,40,50,70,100}` 逐档调用。
- `Build(int paragon, bool smartPick = true)`（`:120`）：`smartPick=true` 时按 `want = Mathf.Min(paragon / GameConfig.ParagonPassiveEvery, SkillCatalog.All.Length)` 循环抽候选并挑选——`p.RollSkillCandidates(GameConfig.SkillChoiceCount)` → `PickAndEquipBest(p, candidates)`；`smartPick=false` 走旧口径 `p.GrantRandomSkill()` 后手工塞空槽（纯对照组）。
- `PickAndEquipBest(PlayerData p, List<SkillId> candidates)`（`:162`）：对每个候选实际打一场，取分最高者；**只在对应 kind 的空槽里挑，不做满槽替换**。末尾 `p.GrantSkill(best)` 是必需的掩码落点，否则 `Build` 的 `while (p.OwnedSkillCount < want)` 会死循环（代码里已注明）。
- `TrialScore(PlayerData p, BattleSystem battle)`（`:200`）：`for (int t = 0; t < 3; t++)` 用 `MonsterData.CreateForLevel(110, (MonsterType)t, MonsterTier.Boss, MonsterAffix.None)` 打战士/弓箭手/盗贼三种 Boss，每场先 `battle.ResetSkillState()` + `p.FillHp()`，三种取平均。胜 → `1f + 100f / (1f + seconds)`（越快越高，恒 > 1）；败 → `(1f - (float)m.currentHp / m.maxHp) * 0.5f`（按把怪打到多少血给 0~0.5）。
- `Fight(...)`（约 `:565`）：0.05s 步进、上限 150000 步（=7500 秒），自动喝药（`PotionAutoUseHpPercent` / `PotionHealPercent` / `PotionCooldown` / `PotionMaxCount`），`MonsterKilled` → true、`PlayerDied` → false，并输出击杀耗时 `seconds`。每场新建 `BattleSystem`，主动技能 CD 从 0 起算，口径一致。

## 2. 评分函数的三轮迭代与理由（复用时最该看的部分）

1. **v1「胜 = 1，败 = 1 − 怪物剩余血量比」**：满装时场场都赢，所有候选都得 1.0，**选择退化成「永远取候选里的第一个」**。这是必须避开的评分退化。
2. **v2「胜按击杀耗时分档」→ `1 + 100/(1+秒数)`，败给 0~0.5**：让胜利之间也有区分度。
3. **v3「从只打盗贼 Boss 一场改为三类型各打一场取平均」**：只打盗贼一场时纯输出型技能被高估，而盗贼 Boss 恰恰是胜率短板；必须让生存型技能在评分里拿得到分。
4. **被否决的做法：槽满后允许替换现有技能。** 实测**反而拉低胜率**——巅峰 80：允许替换时 smartPick 51.8%，同档「随机送」对照组 61.8%。原因是「3 场战斗」的评分噪声远大于技能之间的真实差距。因此最终只保留「有空槽时挑最优」。

顺带记录一处探针签名/编译问题：`GameUI.cs(418,70): error CS1503: Argument 1: cannot convert from 'AutoBattle.SkillKind' to 'int'`（`KindShort` 收 int，需 `(int)def.kind`）——该错与探针改造同期出现，注意每次改 `SkillKind` 调用点时复查。

## 3. 新口径基线表（`RunWinrate(30, true, true, 20)`：每格 30 场/类型 × 20 次独立随机技能组合取平均，每场重置生命，类型加权 战45/弓30/盗25）

| 巅峰 | 技能数 | Lv.100 精英 | Lv.100 Boss | Lv.110 精英 | Lv.110 Boss |
|---|---|---|---|---|---|
| 0 | 0 | 100% | 100% | 87.7% | **0.1%** |
| 10 | 1 | 100% | 100% | 95.7% | **4.2%** |
| 20 | 2 | 100% | 100% | 98.8% | **11.2%** |
| 30 | 3 | 100% | 100% | 99.9% | **31.4%** |
| 40 | 4 | 100% | 100% | 99.9% | **42.8%** |
| 50 | 5 | 100% | 100% | 100% | **60.8%** |
| 70 | 7 | 100% | 100% | 100% | **79.1%** |
| 100 | 10 | 100% | 100% | 100% | **82.5%** |

Lv.110 Boss 逐类型（巅峰 0 → 100）：战士 0.0% → 89.3%，弓箭手 0.3% → 90.0%，盗贼 0.0% → 61.0%。

## 4. 与旧口径（随机送技能）对比 —— Lv.110 Boss

| 巅峰 | 旧（随机送） | 新（三选一挑） | 差 |
|---|---|---|---|
| 0 | 0.0% | 0.1% | — |
| 10 | 1.3% | 4.2% | +2.9 |
| 20 | 3.2% | 11.2% | +8.0 |
| 30 | 12.6% | 31.4% | +18.8 |
| 40 | 22.5% | 42.8% | +20.3 |
| 50 | 41.8% | 60.8% | +19.0 |
| 70 | 50.5% | 79.1% | +28.6 |
| 100 | 95.7% | **82.5%** | **−13.2** |

口径提醒：新表固定为「每格 30 场/类型 × 20 次抽样」；旧列数字取自此前抽样次数不同的运行（同一档单点曾测到 94.2%、87.8% 等），所以差异应按方向而非精确点位解读。

## 5. 两条结论

1. **「会挑技能」的收益是真实的**：巅峰 10~70 每档比「技能乱给」高 3~29 个百分点，说明三选一确实有策略价值。
2. **巅峰 100 反常回退（82.5% vs 95.7%）**：技能槽只有 6 个（主动/被动/触发各 2），满巅峰 10 个技能，而探针规则是「有空槽才挑」，所以它实际只决定了前 6 个；这 6 个的评分口径（三类型 Boss 击杀耗时）**系统性偏向输出型技能**，打盗贼 Boss 时生存不够——盗贼 61.0% 明显低于战士/弓箭手的约 90%。

## 6. 未决事项（检查点截止时用户尚未答复）

当时向用户提了两个修巅峰 100 反常的可选方向：①让评分对盗贼 Boss 加权（直接补最短板）；②槽满后允许替换，但每个候选多打几场降噪（注意第 2.4 条已实测替换在当前噪声下是负收益，采用前必须先降噪）。此外技能槽上限（6）与满巅峰技能数（10）的错配本身也是待决策的设计问题。

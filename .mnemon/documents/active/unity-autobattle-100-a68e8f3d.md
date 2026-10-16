---
id: "a68e8f3d-4799-4bff-8ae2-942a12bae043"
title: "Unity AutoBattle 技能获取机制改版（巅峰随机获得）与 100 级后成功率实测"
description: "HelloWorldProject 技能改为「100 级前无技能、满级后每 10 级巅峰随机抽一个」的实现要点（ownedSkillMask 掩码、GrantRandomSkill、装备校验）、Play 模式验证结果，以及 Lv.100/Lv.110 精英与 Boss 的分档胜率统计与统计口径教训。取代既有文档 7c3b8ebf 中的解锁顺序与默认装配结论。"
status: "active"
created_at: "2026-10-12T16:47:52.589Z"
updated_at: "2026-10-12T16:47:52.589Z"
content_hash: "e04299ee0d3303d207d1bbf6c6f0ec5eaceae6ba7195273c0353b7d2f653a6ec"
source_paths:
  - "HelloWorldProject/Assets/Scripts/Skills.cs"
  - "HelloWorldProject/Assets/Scripts/Models.cs"
  - "HelloWorldProject/Assets/Scripts/GameController.cs"
  - "HelloWorldProject/Assets/Scripts/SaveSystem.cs"
  - "HelloWorldProject/Assets/Scripts/GameUI.cs"
session_ids:
  - "c25f3e74-02c4-44f3-b86f-704b4f0a5ba1"
memory_body_ids:
  []
---

# Unity AutoBattle 技能获取机制改版与 100 级后成功率

工程：`HelloWorldProject/`（Universal 2D，Editor 6000.6.5f1）。改版日期 2026-10-12，已编译通过并逐项实测。

**本文取代既有文档 `7c3b8ebf-3e86-4b45-8f70-ad53ec9188c5`（Unity AutoBattle 巅峰等级与技能系统）中的以下过时结论**：被动按池内顺序解锁、`unlockedPassives` 计数建模、新档/老档默认装配重击+旋风斩+反击+暴击连锁、以及仅按解锁状态拦截被动装备。技能池的 15 个技能数值、巅峰等级与技能等级公式、战斗循环接入点、UI 布局常量在该文档中仍然有效，此处不重复。

## 需求变更（用户原话）

> 100级前无技能，100级后每升10级巅峰等级随机获得一个技能（主动、被动或触发）

## 源文件

- `HelloWorldProject/Assets/Scripts/Skills.cs`——技能池定义（15 个，数值不变）。
- `HelloWorldProject/Assets/Scripts/Models.cs`——`PlayerData.ownedSkillMask` 掩码、`OwnedSkillCount`、`GrantRandomSkill()`。
- `HelloWorldProject/Assets/Scripts/GameController.cs`——技能装备/获得校验、自动入槽与日志。
- `HelloWorldProject/Assets/Scripts/SaveSystem.cs`——掩码的存档读写（改版时曾写错字段名并修正）。
- `HelloWorldProject/Assets/Scripts/GameUI.cs`——技能面板按钮启用状态改为按「是否已获得」判定。

## 变更对照

| 项 | 改版前 | 改版后 |
|---|---|---|
| 技能来源 | 主动/触发默认可用，被动按顺序解锁 | **100 级前一个都没有**；满级后每 10 个巅峰等级从 15 个池中**随机抽一个**（类别不限） |
| 数据表示 | `unlockedPassives`（数量） | `ownedSkillMask`（15 位掩码，全获得 = 32767） |
| 装备校验 | 被动需已解锁 | **任何技能都必须已获得**，否则面板按钮不可用 |
| 新档默认装配 | 重击/旋风斩/反击/暴击连锁 | **无**，六个槽全空 |

## 实现要点

- `GrantRandomSkill()` 从未获得的技能中随机抽取；全部获得后返回 `-1`（UI 显示「已全部获得」），不会重复。
- 抽中的技能自动放入同类空槽并写战斗日志；该类槽位已满则只加入「已获得」集合，由玩家在面板中自行替换。
- 发放节奏复用 `GameConfig.ParagonPassiveEvery`（= 10 级一个），与技能等级 `1 + 巅峰/10` 共用间隔。
- 每类槽位上限 2 个（共 6 槽）的限制保持不变；技能 CD 跨战斗保留不变。

## 实测验证（Editor eval + Play 模式）

| 验证项 | 结果 |
|---|---|
| 100 级前 | `ownedSkillMask = 0`、已获得 0、被动加成 0、攻击 10 / 防御 5 |
| 连续随机抽取 18 次 | 得到 **15 个互不重复**的技能，掩码 32767（= 2¹⁵−1）；第 16 次起返回「已全部获得」 |
| 巅峰 10/20/30/40/50/60 | 依次获得 1/2/3/4/5/6 个，类别随机组合（实测出现过「0 主动」的组合），攻击/防御随巅峰线性增长 |
| 编译 | `consoleErrors: 0`；收尾后存档已清空、未进 Play、`runInBackground=false` |

## 100 级后挑战精英/Boss 成功率

条件：主角 Lv.100 + 传说武器/护甲全套 + 5 瓶药；技能按上表规则随机生成并自动装备；每格 400 场；按固定怪物类型分别统计后加权（战士 45% / 弓箭手 30% / 盗贼 25%）；技能冷却跨战斗延续，与实战一致。

| 巅峰 | 技能数 | Lv.100 精英 | Lv.100 Boss | Lv.110 精英 | Lv.110 Boss |
|---|---|---|---|---|---|
| 0 | 0 | 100.0% | 100.0% | 78.4% | **9.1%** |
| 10 | 1 | 100.0% | 100.0% | 88.8% | 18.5% |
| 20 | 2 | 100.0% | 100.0% | 93.0% | 24.9% |
| 30 | 3 | 100.0% | 100.0% | 96.7% | 34.4% |
| 40 | 4 | 100.0% | 100.0% | 97.5% | 41.3% |
| 50 | 5 | 100.0% | 100.0% | 98.2% | 49.3% |
| 70 | 7 | 100.0% | 100.0% | 99.3% | 61.3% |
| 100 | 10 | 100.0% | 100.0% | 99.5% | **73.3%** |

## 结论与设计含义

- **巅峰技能只对 Lv.110 有意义**：Lv.100 的精英与 Boss 在巅峰 0 就已 100% 胜率，满级传说套打同等级梯度怪本身即碾压，技能没有提升空间。
- **Lv.110 精英**：78.4% → 99.5% 平滑爬升，技能起到「补齐短板」作用。
- **Lv.110 Boss**：9.1% → 73.3%，是技能系统真正的作用区；每 10 级巅峰约 +8~12 个百分点，接近线性成长。
- 也就是说 **Lv.110 Boss 成为「巅峰等级检定」**：无技能近乎打不过，巅峰 100（10 个技能）才有约七成把握，符合设计目标。

## 统计口径教训（重要）

第一版成功率脚本把「怪物类型随机抽取」与「类型权重」乘了两次，等价于 `Σw² = 0.355`，把实际 100% 的 Lv.100 两列显示成约 34%。修正方式是**按固定类型分别统计胜率、最后再加权**。凡是「先随机抽类型、再按类型加权」的统计都必须避免这种重复加权。改版前的 34% 数据作废，以上表为准。

另注：`eval` 传入的 C# 脚本若在第 22 行这类位置出现 `Unreachable code detected`，是脚本自身写法问题（示例中为诊断用脚本提前 return），不是工程编译错误——工程侧 `compilationFailed` 与 `consoleErrors` 均为干净状态。

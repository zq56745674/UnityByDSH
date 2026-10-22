---
id: "83ec5a02-9b24-4ebe-87f8-036d825ec99f"
title: "Unity AutoBattle 转生加速落地与 50 种子复跑（2026-10-22，Exp 0.60 / Legacy 3）"
description: "转生加速参数落地（RebirthExpPerRank 0.60、RebirthLegacyLevelPerRank 3）的改点行号、50 种子复跑分布（第2世 82.6%、满轮回 47.1%）、验证命令与文档同步、遗留项。"
status: "active"
created_at: "2026-10-22T14:53:47.072Z"
updated_at: "2026-10-22T14:53:47.072Z"
content_hash: "c9b41002fff9059d6d6d5903df5117bb4d61c1d8dc9ed3588a1046fb87d1233e"
source_paths:
  - "docs/rebirth.md"
  - "README.md"
  - "HelloWorldProject/Assets/Scripts/GameConfig.cs"
  - "HelloWorldProject/Assets/Scripts/GameConfigData.cs"
  - "HelloWorldProject/Assets/Scripts/Models.PlayerData.cs"
  - "HelloWorldProject/Assets/Editor/AchievementRegression.cs"
session_ids:
  - "345c5236-d098-4030-b45f-6b2869c4c7ee"
memory_body_ids:
  []
---

# Unity AutoBattle 转生加速落地与复跑（2026-10-22）

本记录是 `47c2321e-0137-4b31-8551-d7e4fb9c60d4`（2026-10-21 五槽期平衡调查）中"转生加速 92.4% / 63.5%、建议动 `RebirthExpPerRank` 与 `RebirthLegacyLevelPerRank`、参数均未写入产品配置"这一开放项的**收尾**：两个参数已按候选落地，并用同一批 50 种子复跑验证。前置分析与机制拆解见该文档 §2，本文只记录落地状态与实测。

## 1. 决策与依据

- 采用前置调查给出的候选：`RebirthExpPerRank` **0.20 → 0.60**、`RebirthLegacyLevelPerRank` **2 → 3**。
- 为什么是这两个：单加经验只能压"第 2 世"（Legacy rank0 时起始等级仍是 1，传承不参与）；满轮回后半程瓶颈是章节推进/起始等级，必须经验与起始等级同加。单独 Exp 到 0.70 仍卡在 0.503，单独 Legacy rank4 只有 0.558，叠加才到 0.471。
- 为什么不动战斗/金币/掉落：战斗 +5% 封顶会直接推高无尽层数（无尽首死中位已 315，目标 ≤305），金币在五槽满级后本就过剩，掉落只影响装备成型速度、不改天花板。

## 2. 落地位置（数值三处同步 + 文案/断言）

| 项 | 位置 |
|---|---|
| `RebirthExpPerRank = 0.60f` | `HelloWorldProject/Assets/Scripts/GameConfig.cs:687`；`HelloWorldProject/Assets/Scripts/GameConfigData.cs:158`；`HelloWorldProject/Assets/Resources/game_config.json:139` |
| `RebirthLegacyLevelPerRank = 3` | `GameConfig.cs:696`；`GameConfigData.cs:161`；`game_config.json:142` |
| 注释"历练 +60%/级" | `HelloWorldProject/Assets/Scripts/Models.PlayerData.cs:343` |
| 断言：rank5 经验 ×4（+300%）、起始 Lv.16 | `HelloWorldProject/Assets/Editor/AchievementRegression.cs:750-756` |

计算入口（不改）：`Models.PlayerData.cs:344` `RebirthExpMultiplier => 1f + GameConfig.RebirthExpPerRank * rebirthGrowthRank`；`:353` `RebirthStartLevel => 1 + RebirthLegacyLevelPerRank * rebirthLegacyRank`；面板文案 `RebirthCatalog.cs:57,61`。加载器钳位：`GameConfig.Loader.cs:252` float[0,1]、`:255` int[1,10]，新值都在范围内。

## 3. 复跑实测（落地后，同 50 种子）

种子 20261019..20261068，`maxAttemptsPerStage=30`，`reforgeQuality=0.80`（沿用洗练 0.9 口径，第 1 世基线因此比旧 0.6 口径慢约 62s）。

| 局 | 通关 | 中位耗时 | P10 | P90 | 中位阵亡 | 结束巅峰 | 起始等级 |
|---|---|---|---|---|---|---|---|
| 第 1 世（全 0） | 38/50 | 805.75s | 710.5 | 1019.7 | 10 | P44 | Lv1 |
| 第 2 世（G1/F1/A1，Legacy rank0） | 47/50 | 665.40s | 565.1 | 930.8 | 0 | P56 | Lv1 |
| 满轮回（全 5） | 50/50 | 379.72s | 325.4 | 450.3 | 0 | P85 | Lv16 |

- 第 2 世 / 第 1 世 = **82.6%**，落在目标 75~85%。
- 满轮回 / 第 1 世 = **47.1%**，落在目标 35~50%。
- 与落地前的预跑（379.75s）一致（0.03s 为四舍五入），说明参数确实生效、探针口径没漂。

## 4. 验证

- `recompile` → `completed`；`GameConfig.Reload()` 读回 `exp=0.6 legacy=3 gold=0.2`，无缺键告警（`GameConfig.Loader.cs:63` `ReportMissingKeys`）。
- `BalanceRegression.RunStaticOnly()` → `PASS — 静态哨兵与基线一致`。
- `AchievementRegression.RunCore()` → `成就 §11 核心 PASS | 通过 644 / 失败 0`。

## 5. 文档同步

- `docs/rebirth.md`：§4（成本/效果表）、§8、§9.1、§9.2、§11.3、§13.2、附录 A、§14 全部改为 0.60 与 3；§14.4 重写为"复测、落地与复跑"，附落地行号与复跑表；抬头与 §11 的"候选未落地"注记同步改成已落地。
- `README.md:254` 已知问题条目由"加速候选未落地"改为"转生加速已落地"，补 82.6% / 47.1%；战斗 +5% 偏强的建议保留（本轮未动战斗数值，无尽首死 315 不变）。

## 6. 遗留与注意

- **无尽首死中位仍 315**（目标 ≤305）：与转生参数无关，需另行动战斗加成上限或无尽曲线。
- 满轮回开局 Lv.16 会跳过前几级；经验满级 +300% 意味着后期轮回升级极快、前几章进一步缩短——若后续再加经验，优先复测这一段的时长分布。
- 探针口径：`EndlessPacing.BuildFullPlayer` 与 `LateGameProbe.Build` 必须在技能三选一之后套 rank；"rank 在技能前"跑出的满轮回无尽中位 125 层是伪影，不要引用。
- 工作区共 13 个文件改动（含上一轮未提交内容），未 commit / push。

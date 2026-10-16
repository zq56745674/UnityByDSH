---
id: "4f6621d0-f8c4-4dab-81fb-719d78ddf259"
title: "Unity AutoBattle 装备评分改版：线性权重 → 增量期望 DPS（含暴击率/暴伤失衡发现）"
description: "HelloWorldProject 装备评分口径从线性加权求和改为「增量期望 DPS + 有效生命/8」，含必须用增量基准与角色当前属性算的原因、木桩相关性实测（ρ 0.090→0.914）、LegacyScore 改名与审脚本迁移，以及由此暴露的暴击率词条边际价值为暴伤 7.5 倍、传说衣被稀有衣顶掉的失衡与三个候选调整方向。"
status: "active"
created_at: "2026-10-12T17:45:44.584Z"
updated_at: "2026-10-12T17:45:44.584Z"
content_hash: "e457b7853b4552d9eddb58be0691ed2d008761091a05971de11047db2c91e666"
source_paths:
  - "HelloWorldProject/Assets/Scripts/EquipmentScore.cs"
  - "HelloWorldProject/Assets/Scripts/Models.cs"
  - "HelloWorldProject/Assets/Scripts/GameConfig.cs"
  - "HelloWorldProject/Assets/Scripts/GameController.cs"
  - "HelloWorldProject/Assets/Scripts/GameUI.cs"
  - "HelloWorldProject/Assets/Editor/LateGameProbe.cs"
  - "HelloWorldProject/Assets/Screenshots/score-new.png"
session_ids:
  - "19866104-a1e1-4b47-bd96-d11f97e7aaa1"
memory_body_ids:
  []
---

# Unity AutoBattle 装备评分改版：增量期望 DPS

工程：`HelloWorldProject/`（Universal 2D，Editor 6000.6.5f1）。改动日期 2026-10-12，编译无错误，Editor eval + Play 模式实测通过。

**相关既有文档**
- `e0af2b00-883b-47fe-ad14-f22f8f78f565`（攻速上限 / 护甲穿透 / 怪物速度表）：其中「评分权重 `ScoreWeightArmorPen = 800`」等**线性评分权重**的用法已被本文的期望 DPS 口径取代（权重常量仅保留给审计脚本）。
- `a68e8f3d-4799-4bff-8ae2-942a12bae043`（技能获取改版与 100 级后胜率）、`7c3b8ebf-3e86-4b45-8f70-ad53ec9188c5`（巅峰与技能系统）：技能数值与巅峰公式仍有效，本文改动了评分与自动换装判据。
- 注意 `a68e8f3d` 的 Lv.110 Boss 胜率结论是**旧的线性评分 + 旧攻速上限**下测的，评分换口径后自动换装结果会变，需重测。

## 1. 新评分口径

新增 `HelloWorldProject/Assets/Scripts/EquipmentScore.cs`：

```
评分 = Δ(对标准怪 Lv.100 普通近战的期望 DPS) + Δ(有效生命) / 8
```

三个设计点（都是实现时踩出来的，改口径时不要丢）：

1. **必须是增量口径**：基准是「同槽位空着」，即这件装备比空槽多带来多少战力。第一版算「穿上后的角色总战力」，武器和衣服都显示 **8789**——因为那算的是「这个角色的战力」，两件同时在身上数字自然相同。改增量后武器 8646 / 衣服 4331（另一组随机器：武器 6459 / 衣服 2565 / 另一把武器 8846）。
2. **必须带角色当前属性计算**：这正是「更合理」的核心——角色暴伤 697% 时 +8% 暴击率极值钱，暴伤掉回 200% 时就廉价。评分因此不是装备的固有属性，而是「对当前角色」的边际价值。
3. **`/8` 是生存折算系数**，按当前数值平衡定的标，让两个槽位同量级（实测 Lv.20~100 武器/衣服比值 1.51 / 1.72 / 1.88 / 1.89 / 2.17）。它不影响同槽位比较：武器不改有效生命、衣服不改 DPS。

## 2. 为什么换口径（动机与证据）

`DPS ∝ 攻击 × 攻速 × (1 + 暴击率 × (暴击倍率 − 1))` 是**连乘**关系，线性权重求和表达不了。

审计脚本 `HelloWorldProject/Assets/Editor/LateGameProbe.cs`（`ScoreAudit:142`、`ScoreRankAudit:199`、`ScoreBattleAudit:263`、`ScoreDummyAudit:333`、`ScoreWeightSearch:413`）的实测：40 把 Lv.100 传说武器、同一件衣服、同一套技能、木桩 Lv.110 Boss 60 秒 ×2 次取平均。

| 评分口径 | Spearman ρ（评分 vs 木桩总伤害） |
|---|---|
| 旧线性权重（原状） | 0.090 |
| 旧权重穷搜最优（暴击率 700 / 暴伤 120） | 0.398 |
| **新期望 DPS** | **0.914** |

## 3. 迁移与落点

- `HelloWorldProject/Assets/Scripts/Models.cs:158-166`：`Equipment.Score`（线性加权）**改名 `LegacyScore`**，只保留给 `LateGameProbe.cs` 的权重审计用；主属性、主属性暴击与全部副词条（攻击/防御/生命/暴击率/暴伤/攻速/全属性/穿甲）原来都计入 `ScoreWeight*`。
- `HelloWorldProject/Assets/Scripts/GameController.cs`：自动换装比较改用新评分；两条日志（自动穿戴 / 卖出）随之更新；统计计数 `TotalEquipped` / `TotalSold`（同文件 54-65 行区）用于核对换装频率。
- `HelloWorldProject/Assets/Scripts/GameUI.cs`：装备评分显示与比较文案，实测出现过「(1) 显示和日志都通了（`↓ 弱于当前衣服（816 < 2565）`）」。
- `HelloWorldProject/Assets/Scripts/GameConfig.cs`：`ScoreWeight*` 常量保留，另有生存折算相关常量随 `/8` 标定调整。
- 截图：`HelloWorldProject/Assets/Screenshots/score-new.png`（新评分显示）。

## 4. 由新评分暴露的失衡：暴击率词条碾压暴伤词条

现象：自动换装把**传说**衣服换成**稀有**衣服（92 次击杀、34 件掉落只换装 1 次，最终衣服为 Rare 防御 +255 / 生命 +1703）。

排查结论：**不是评分 bug，是数值真的歪**。两件属性几乎相同的稀有衣服评分差 3 倍：

| 衣服 | 副词条 | 评分 |
|---|---|---|
| A（稀有，当前） | 暴击率 +10.1% / 全属性 +8.9% | **3132**（另一测为 3129） |
| B（稀有，对照） | 暴击伤害 +48.5% / 全属性 +8.8% | 925（另一测为 1086） |

角色实测面板：暴击率 **15.1%**、暴伤 **643.8%**（收尾分析中报 697%）、攻速 7.9%、全属性 19.1%、穿甲 8.4%。

- +10.1% 暴击率 → `1 + 0.101 × 5.438` ≈ **+54.9% DPS**
- +48.5% 暴伤 → `1 + 0.151 × 0.485` ≈ **+7.3% DPS**

**暴击率词条的边际价值是暴伤词条的 7.5 倍**，而词条池里两者等概率出现（词条池 5 种，见 `e0af2b00`）。这就是传说被稀有顶掉的机制。

### 未决：三个候选调整方向（用户尚未选择）

① 压低暴伤来源（武器主属性暴伤现在 ~450%，是膨胀主因）；② 提高暴击率来源；③ 给暴伤做边际递减。当时的收尾状态是等待用户选定方向，**不要自行改数值**。

## 5. 验证流程备注

Play 模式验证用 `LateGameProbe` + Editor eval 组合；验证前后需自行设置/恢复 `runInBackground`（本工程 `editor_play` 没有 `run_in_background` 参数），收尾为 `editor_stop` + `console_status` 确认 0 错误 + 恢复 `runInBackground=false`。

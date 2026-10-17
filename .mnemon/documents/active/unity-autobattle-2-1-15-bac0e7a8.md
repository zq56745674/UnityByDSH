---
id: "bac0e7a8-2b43-4d5e-a78f-41ce570585c4"
title: "Unity AutoBattle 装备强化（步骤 2）：只加主属性的 +1~+15、品质费用系数、评分缓存键修复"
description: "HelloWorldProject 步骤 2 装备强化的完整落地记录：六处配置项与公式（EnhanceBonusPerLevel 0.03 / EnhanceMaxLevel 15 / 品质费用系数表）、Equipment 的强化学派生口径、面板与入口按钮位置、Play 实测数值（+15 倍率 1.45×、暴击暴伤不动、金币不足拦截）、以及「评分缓存按引用比较必须把 enhanceLevel 纳入键」这个必踩的坑与平衡回归 PASS 结论。"
status: "active"
created_at: "2026-10-16T05:44:44.411Z"
updated_at: "2026-10-16T07:37:59.895Z"
content_hash: "9db8f5b3ad0f573df6ecb9f8076cbf162adb29c961c6a720482e2aa6f7cd9bb0"
source_paths:
  - "HelloWorldProject/Assets/Scripts/Models.cs"
  - "HelloWorldProject/Assets/Scripts/GameController.cs"
  - "HelloWorldProject/Assets/Scripts/GameUI.cs"
  - "HelloWorldProject/Assets/Scripts/GameConfig.cs"
  - "HelloWorldProject/Assets/Scripts/GameConfig.Tables.cs"
  - "HelloWorldProject/Assets/Editor/BalanceRegression.cs"
  - "HelloWorldProject/Assets/Screenshots/enhance-panel.png"
  - "HelloWorldProject/Assets/Screenshots/enhance-entry.png"
session_ids:
  - "session-820fee2d-3ea4-4964-9f54-6440131b1026"
memory_body_ids:
  []
---

# Unity AutoBattle 装备强化（步骤 2）

工程 `HelloWorldProject/`（Universal 2D，Editor 6000.6.5f1），workspace 根 `E:\pytest\unitydsh1`。本文记录 12 步路线图 **步骤 2：装备强化（+1~+15）**（P1 核心循环闭环第二步）。相关文档：项目总览与接手指南 `42b8694e-f097-43db-9f58-9d606e02cfd1`；多存档槽与平衡回归 `cc30b461-fc35-4515-a393-f03aacb0d948`；评分口径 `8bb7e4c1-5ba6-4738-8ab6-4384c4e62219`。

## 用户给的三条硬约束（m05714 原话）

> 执行2 增加装备强化 只提升主属性（不包含暴击、暴伤），每次提升不要太大

1. **只提升主属性**：武器 = 攻击；衣服 = 防御 + 生命。
2. **不包含暴击、暴伤**：武器的 `critChance` / `critDamage` 主词条**完全不受强化影响**。
3. **每次提升不要太大**：每级 +3% 基础主属性，满级 +15 → 合计 1.45×。

## 设计决策与理由

**为什么是每级 +3%、上限 +15。** 满级总倍率 1.45×，与「换一件同品质装备」的提升量级相当——强化能顶半件装备，但不会让装备品质失去意义。若取 +5%/级则满级 1.75×，会让低品质高强化压过传说，破坏掉落追求。

**为什么费用随品质递增。** 费用 = `(EnhanceBaseCost + EnhanceCostPerLevel × 目标等级) × 品质系数`。品质系数表 `EnhanceQualityFactors = { 0.6f, 0.8f, 1.0f, 1.3f, 1.6f }`（普通→传说）：好装备强化更贵，避免「传说装备被廉价刷满」。

> 参数名注意：`GameConfig.EnhanceCost(int targetLevel, EquipmentQuality q)` 的第一个参数实际接收的是**当前**强化等级（目标等级 = 当前 + 1），与 `Equipment.EnhanceCost`（读 `enhanceLevel`）一致。实测 `enhanceLevel=5`、传说 → `(150 + 300×5) × 1.6 = 2640`。

**为什么强化值走派生属性而不是改原始字段。** `Equipment` 的原始 `attack` / `defense` / `hp` 保持不变，强化只通过 `EnhancedAttack` / `EnhancedDefense` / `EnhancedHp`（= 原始值 × `EnhanceMultiplier`）暴露。理由：原始值是**掉落掷点的产物**，是可存档、可复现的事实；强化等级是另一维状态。混在一起会让「洗练重掷副词条」（步骤 3）和评分口径都失去干净基线。

**为什么评分缓存必须把 `enhanceLevel` 纳入键。** 这是本次唯一踩到的真 bug，详见下文「坑」。

## 实现位置（6 个文件）

| 文件 | 新增内容 |
|---|---|
| `HelloWorldProject/Assets/Scripts/GameConfig.cs` | 标量：`EnhanceBonusPerLevel = 0.03f`、`EnhanceMaxLevel = 15`、`EnhanceBaseCost = 150`、`EnhanceCostPerLevel = 300`；公式 `EnhanceCost(int targetLevel, EquipmentQuality q)` |
| `HelloWorldProject/Assets/Scripts/GameConfig.Tables.cs` | `EnhanceQualityFactors = { 0.6f, 0.8f, 1.0f, 1.3f, 1.6f }`（普通/优秀/稀有/史诗/传说） |
| `HelloWorldProject/Assets/Scripts/GameConfigData.cs` | 4 个镜像字段（json 热改入口） |
| `HelloWorldProject/Assets/Scripts/Models.cs` | `Equipment.enhanceLevel`；`EnhancedAttack/Defense/Hp`；`EnhanceMultiplier`；`CanEnhance`；`EnhanceCost`；`EnhanceText`；`EnhanceGainText`；`Title` 追加 `+N` 后缀；`EnhanceResult` 枚举（`Ok` / `MaxLevel` / `NotEnoughGold`）；`PlayerData.MaxHp/Attack/Defense` 改用 `Enhanced*`；`Equipment.StatLine` 也改用强化值 |
| `HelloWorldProject/Assets/Scripts/GameController.cs` | `EnhancePanelOpen`；`ToggleEnhancePanel()`；`CloseEnhancePanel()`；`EnhanceEquipment(EquipmentSlot)` 返回 `EnhanceResult`；`CloseAllPanels()` 纳入第四个面板；`Update()` 的冻结判据加 `EnhancePanelOpen` |
| `HelloWorldProject/Assets/Scripts/GameUI.cs` | 字段区；`BuildEnhancePanel` / `RefreshEnhancePanel` / `DescribeEnhanceCard`；`OnEnhanceEntryClicked` / `OnEnhanceSlotClicked`；「强化」入口按钮 `(300, 303, 92, 26)`（装备标题行右侧）；评分缓存键加入 `enhanceLevel`；`_atkText` / `_defText` 改用 `Enhanced*` |

## Play 模式实测数值

```
MAXED after 12 steps -> lv15 extraAttempt=MaxLevel      ← 15-3 已强化 3 级，12 步到顶
wpn atk 523 -> 758  (x1.449)                            ← 满级倍率正好 1.45×
char atk now 1112  cc 0.179434                          ← 暴击率不变（约束 2 成立）
poorGold(10) attempt=NotEnoughGold costWouldBe=1680
panelOpen=True
```

面板文案实测：「攻击 523 → 758 / 已强化到上限 +15 / 已满级」；金币不足时按钮仍可点但返回 `NotEnoughGold`。费用序列实测：+1 240 金币、+2 720、+3 1200、+4 1680、+5 2160、+6 2640……（240 步进 = 300 × 稀有系数 0.8）。

## 坑：评分缓存按引用比较

`GameUI` 里装备评分有缓存，**按装备对象引用比较**。强化是在**同一个对象上改字段**（`enhanceLevel++`），引用不变 → 缓存判定「没换装备」→ 评分显示不刷新。修法：把 `enhanceLevel` 一并纳入缓存键。这类 bug 在「就地修改」型状态变更里必踩，写缓存键时要问「哪些字段变化会影响这个缓存值」。

另：`Equipment.StatLine` 原先直接读原始 `attack`/`defense`/`hp`，导致左侧装备行显示未强化值、而角色属性区显示强化值，**同一界面上两处口径矛盾**。已统一改为读 `Enhanced*`。

## 回归：静态哨兵与胜率表

`HelloWorldProject/Assets/Editor/BalanceRegression.cs` 的静态哨兵与完整胜率表**双双 PASS**（`seed=20261016, trials=12, perType=30`）——强化是纯新增系统，未触碰既有数值。

**静态哨兵已覆盖强化参数**（后续补入；`Enhance` 相关采样 57 条，哨兵条目总数 52 → 109）。新增覆盖：

- 标量与表：`EnhanceBonusPerLevel` / `EnhanceMaxLevel` / `EnhanceBaseCost` / `EnhanceCostPerLevel` / `EnhanceQualityFactors[0..4]`
- 费用公式二维采样：4 种品质 × 4 个等级档（0/1/7/14），共 16 条 `EnhanceCost(lv,q)`
- 派生属性手算（固定基础值 `attack=1000, defense=800, hp=5000`，**不消耗随机数**，故为精确比对）：`EnhanceMultiplier` / `EnhancedAttack` / `EnhancedDefense` / `EnhancedHp` / `CanEnhance` / `EquipEnhanceCost` 各 5 个等级档（0/1/7/14/15）
- 满级边界：`CostAtMaxLevel = 0`、`CanEnhanceAtMaxLevel = 0`

**为什么采样派生属性便足以守住「不碰暴击暴伤」这条红线。** `Enhanced*` 是强化作用于战斗数值的唯一出口；谁若在 `EnhanceMultiplier` 的路径上掺进暴击/暴伤，这些采样立刻变红。

**反向验证（证明哨兵不是摆设）**：把基线里 `EnhanceBonusPerLevel=0.03` 篡改成 `0.05` 后跑 `RunStaticOnly()` → `FAIL — 1 处偏离基线`，且精确指向该条；还原后立即 PASS。

**基线已重写**：`E:\pytest\unitydsh1\_balance\baseline.json` 现为 9089 字节（原约 7KB），胜率表同批重采样。探针构造的装备 `enhanceLevel` 恒为 0，故胜率数值未变。

## 遗留

无。金币出口已由此步打开（此前唯一出口是 320/瓶的生命药水），强化成为大额金币消耗点。

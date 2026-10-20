---
id: "bc1ba543-3f2f-449c-8d11-7e4cb238d13a"
title: "Unity AutoBattle 商店 2.0 与经济系统设计（v1 提案，未实现）"
description: "HelloWorldProject 挂机游戏商店系统重设计：商店不在决策链上的诊断、掉落量与金币成本估算、补给/工坊/行商三 tab 与装备精华货币、换装传承结算、掉落调整方向、存档字段、验收指标与 MVP 顺序。设计稿，未写代码。"
status: "active"
created_at: "2026-10-19T18:00:30.218Z"
updated_at: "2026-10-19T18:00:30.218Z"
content_hash: "78b9308166e8cac36655dfc4a46434b3e1e7dd0151be3f1bbdd35a2510aa7118"
source_paths:
  - "HelloWorldProject/Assets/Scripts/GameController.cs"
  - "HelloWorldProject/Assets/Scripts/GameConfig.cs"
  - "HelloWorldProject/Assets/Scripts/GameConfig.Tables.cs"
  - "HelloWorldProject/Assets/Scripts/LootSystem.cs"
  - "HelloWorldProject/Assets/Scripts/Models.cs"
  - "HelloWorldProject/Assets/Scripts/SaveSystem.cs"
  - "docs/rebirth.md"
session_ids:
  - "36e819b9-a606-42cb-81e0-d964f6d72305"
memory_body_ids:
  []
---

# Unity AutoBattle 商店 2.0 与经济系统设计（v1 设计提案，未实现）

工程：`HelloWorldProject/`（Universal 2D，Editor 6000.6.5f1）。状态：**仅设计，未动代码，未经用户确认**。用户原话："现在的商店太简陋了，设计一下商店系统，感觉装备很容易出，卖装备也没什么用，还有什么好的设计"。

相关文档：项目全局地图见 `42b8694e-f097-43db-9f58-9d606e02cfd1`（现状总览与接手指南）；装备/技能数值见 `7c3b8ebf-3e86-4b45-8f70-ad53ec9188c5`（巅峰与技能系统）。另有同风格的"仅设计未开发"稿 `docs/rebirth.md`（转生）。

## 诊断（结论先行）

- 商店的问题不是"货太少"，而是**它不在决策链上**：目前只有药剂、精英/Boss 前强制 3 秒弹窗、自动购买补满，没有任何选择。
- 装备问题不是单纯"掉率太高"，而是四件事叠加：**每关关底保底掉落 + 只有 5 个槽 + 换装净损失 + 重复装备无用途**。只加商品解决不了。
- 因此优先级最高的不是加商店商品，而是**换装结算**（有强化不自动替换、旧件回收/传承、不得凭空消失），它同时让玩家敢强化、给金币稳定出口。

## 现状证据（代码）

- `HelloWorldProject/Assets/Scripts/GameConfig.cs`：`DropChance = 0.30f`、`DropChancePerExtraSlot = 0.05f`、`SmartDropChance = 0.30f`、`EndlessDropLevelCap = 150`。
- `HelloWorldProject/Assets/Scripts/GameConfig.Tables.cs`：`QualityWeights = { 0.625f, 0.25f, 0.09f, 0.03f, 0.005f }`（`QualityNames` = 普通/优秀/稀有/史诗/传说，索引 202 行）；`SellQualityMultipliers = { 1f, 2f, 4f, 8f, 16f }`；`MonsterTierDropQualityWeights`（148 行，精英/Boss 保底品质权重，普通怪为 null 仍走 QualityWeights）。
- `HelloWorldProject/Assets/Scripts/GameController.cs` `ResolveDrops()`（约 1522-1582 行）：普通怪按 `RollDrop(已解锁槽数)` 判定，精英必掉 1 件、Boss 必掉 2 件（`MonsterTierGuaranteedDrops`）；精英权重 稀有 70/史诗 25/传说 5，Boss 权重 史诗 80/传说 20；精英/Boss 掉落等级取怪物等级（梯度更高），无尽模式 `dropLevel` 被 `EndlessDropLevelCap` 截断。
- 更弱掉落走卖店（`GameConfig.SellPrice(level, quality)`），更强者自动穿戴；`TryAutoEquip()`（约 1608 行）→ `WearEquipment()`（约 1652 行）时**旧装备直接消失**，既不卖店也不返还强化/洗练。
- `HelloWorldProject/Assets/Scripts/LootSystem.cs`：品质权重抽样、`CreateEquipmentAtLevel`。`Models.cs`：`QualityName` 走 `QualityNames` 索引。`SaveSystem.cs` 477-478 行已有品质越界防御。
- 只解锁小槽（头盔/鞋/饰品）时会补发一件当前等级稀有 +0 起手装（`EnsureEquipmentSlotStarters`，约 1363 行，用 `slotStarterMask` 记账防重复）。

### 掉落量级估算（11 章 × 10 关 = 110 关）

100 个精英关底 × 1 件（稀有 70/史诗 25/传说 5）+ 10 个 Boss × 2 件（史诗 80/传说 20）+ 普通怪约 370 只按约 45% 折算约 166 件 ⇒ 合计约 286 件，其中约 10 件传说、46 件史诗。**身上只有 5 个槽**，所以"装备很容易出"是结构性结果，不是错觉。

### 金币/成本量级

Lv100 传说卖价 4,832；传说 +0→+15 累计约 54,000；Lv110 传说洗练 31 次约 86 万。卖单件的钱只是强化/洗练的零头——这正是玩家理性策略"不强化、等毕业装"的根因，也是 README 中 P200 结余约 560 万、P1000 约 8.59 亿的来源。

## 商店 2.0（定位：装备服务店，不卖随机装备）

面板 680×480，三个 tab：

1. **补给**：生命药剂 20 + 5×L（限 5，沿用）；狂暴/铁壁药剂 1,500 + 500×c（每章 1，下一场 Boss +25% 攻/防）；复活护符 8,000 + 3,000×c（每局 1，50% 血复活，第 6 章解锁）。
2. **工坊**：强化传承（旧件累计强化费 30% + 1,000，不限次，把旧件 +等级转给新件、旧件消耗）；词条锁定券（6,000 + 2,000×c，每章 1，下次洗练锁 1 条且不乘 ×3）；品质保底（30,000 + 10,000×c + 40 精华，每章 1，下个 Boss 指定槽位必掉史诗+）。
3. **行商**：每章刷 3 件**可见**装备（不是盲盒），至少 1 条指定词条，品质/评分/价格全可见，金币 + 精华计价，每件限购 1；每章 1 次免费刷新，之后 5,000×c；库存与已购按章节存盘，重载不刷。稀有 4,000 + 2,000×c + 20 精华；史诗 15,000 + 6,000×c + 60 精华；**传说不上架**，只靠掉落/保底，以保住掉落价值。

### 装备精华（解决"卖装备没用"）

新增轻量货币：卖出→金币；分解→精华（普通 1 / 优秀 2 / 稀有 4 / 史诗 8 / 传说 20，再 ×(1 + L/100)）。精华用于行商装备、品质保底、锁定券。不需要背包：弱掉落按设置自动"卖出/分解"，不打断自动战斗。每件掉落因此有两种用途，玩家决策从"捡不捡"变成"卖钱还是拆材料"。

## 换装结算（关键，建议最先做）

1. 当前装备有强化/洗练时**默认不自动替换**，进待选托盘；
2. 对比面板加"传承"按钮：花金币把旧件 +等级转到新件，旧件消耗；
3. 不传承则旧件**自动卖出或分解**，不能凭空消失；
4. 设置加"自动传承（金币足够时）"与"弱掉落自动分解"。

## 配套掉落调整（可后置，方向已定）

先上商店观察，不同时砍掉落；仍嫌泛滥再按序调：Boss 从 2 件必掉改为 1 件必掉 + 1 次普通判定；精英传说 5%→2%、史诗 25%→18%；智能掉落只补空槽、满槽后随机重复件进分解。不建议一次全砍。

## 存档与验收

- 新增字段：`essence`、`shopChapter`、`shopPurchasedMask`、`shopRefreshCount`（版本号沿 SaveSystem 现有链递增）；`autoSalvage`、`autoTransfer` 放 PlayerPrefs。
- 商店在 3-10 后解锁（与头盔同步），前期不堆信息；左栏金币旁显示精华；对比面板加"分解"与"传承"。
- 验收：`NewGameProgression` 11 个种子中 11-10 通关数不低于当前 7/11；章节胜率变化 ±3% 内；P200 金币余额降 30~50% 且 P1000 不更差；行商每轮目购买 2~4 件、传承使用率 >30%；无"买入→分解/卖出"套利。

## 其他候选设计（按优先级）

1. **Boss 三选一**：每章末掉 3 件可见装备选 1 件，其余分解——只弹 10 次，把掉落变成选择而不打断节奏。
2. **商店专属唯一装备/套装**（5 件，带改变 build 的特效而非纯数值），作为商店身份与精华长线出口。
3. Boss 悬赏/契约（花金币指定槽位或提难度换保底）。
4. 装备图鉴/收藏（重复装备→精华→图鉴）。
5. 自动处理过滤（按品质自动卖/分解/锁定，纯 QoL）。
6. 更彻底方案：强化等级绑定**槽位**而非装备本身（换装永不掉强化），改动大，二期评估。

不建议：商店主卖随机装备（等于花金币买大概率不如身上的）、永久属性药水、无限金币换装备。

## MVP 顺序

1. 商店面板 + 取消精英/Boss 前强制 3 秒弹窗 + 补给/工坊两 tab；
2. 弱掉落"卖出/分解" + 精华 + 行商 3 件装备；
3. 换装结算：有强化不自动换 + 对比面板传承；
4. 跑探针，再决定掉落削弱、锁定券/保底、唯一装备。

后续可选动作：落成 `docs/shop.md`，或按 MVP 拆成开发任务。

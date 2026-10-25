# 转生设计

状态：2026-10-18 设计稿，**2026-10-20 v1 已实现**（存档实际 v9；成就顺延 v10），**2026-10-25 v2 已实现**（保留最低分一件 + 本档最快轮回 Top-5，存档 v12）。实现记录见文末 §14，v2 见 §14.6；B4c（P100 门槛）与 D3（财运掉落）复评见 §14.7 / §14.8。当前存档 **v13**（B2 成就点兑换，不改转生字段）。
涉及存档：原计划 **v10**（当时 v8；成就设计稿计划 v9）。实际先落转生 v9、成就 v10、无尽榜 v11、v2 纪录 v12、成就点兑换 v13；迁移按版本分支，不绑定绝对数字。
相关文件（已落地）：`Models.PlayerData.cs` / `RebirthCatalog.cs` / `GameController.Rebirth.cs` / `GameUI.Rebirth.cs` / `GameUI.Panels.cs` / `SaveSystem.cs` / `GameConfig.cs` / `GameConfigData.cs` / `Assets/Resources/game_config.json`
参考：`docs/achievements.md`（成就与累计统计的保留约定）、`docs/endless-mode.md`（最高层与检查点口径）、`_balance/balance-analysis.md`（终局金币与节奏）

## 0. 一句话

转生是通关 11-10 后的可选重置：清空本世的等级、关卡、装备与技能，按本世巅峰、无尽最高层和金币折算「轮回印记」，用来购买五条永久升级，让下一世更快回到终局。

## 1. 为什么做

### 1.1 路线图第 10 步，终局缺少「重来一次」的理由

README 的 12 步路线图已完成 9 步，剩下成就、转生、表现层。11-10 通关后，玩家的循环只剩：刷装备 → 卖店 → 洗练 → 再刷；装备评分与词条会逐渐收敛，金币继续变多，但没有任何机制要求他重新经过前 10 章。

转生提供的是**主动重置 + 永久加速**：玩家自愿放弃本世进度，换下一世更快的升级、金币、掉落与少量战斗加成。它不增加新的关卡内容，却让已有的 11 章 × 10 关重新有玩的价值。

### 1.2 给终局金币一个「一次性大额出口」

README 已知问题第 4 条：P200 满级兜里约 560 万、P1000 约 8.593 亿金币（2026-10-22 五槽实测 859,284,167，洗练 0.9 口径；0.6 口径为 8.598 亿），锁 3 条追 80% 词条的中位花费只有约 7 万。金币没有足够大的确定性用途。

转生把**转生瞬间的全部金币**按对数折算成轮回印记，金币越多印记越多，但边际递减。它不是「金币商店」，而是一次性把本世剩余金币换成下一世的起点。注意：它只能消耗一次；买满五条升级后，金币仍需要别的出口，本设计不承诺独自解决通胀。

### 1.3 与成就互补：一个记录，一个加速

成就记录「做过什么」，转生改变「下一世多快」。成就设计稿已经约定：转生保留成就与累计统计，只重置等级/关卡/装备等局内进度（`docs/achievements.md` §4.1）。本设计沿用这条约定，不把成就做成转生货币，也不让转生清空历史。

### 1.4 它不解决什么

- **不是新关卡**。转生后仍打同样的 11 章；变化来自等级、金币、掉落与少量战斗加成。
- **不是无限成长**。五条升级各 5 级，战斗加成封顶 +5%，避免把 P1000 无尽 290 层的基线继续推高。
- **不是完整的金币出口**。满级后金币仍会堆积；消耗型商店、提高强化上限等是另一件事。
- **不是账号级转生**。每个存档槽独立；全局无尽纪录仍跨槽保留。
- **不做剧情、新角色、新技能、音效或立绘**。

## 2. 目标与非目标

### 2.1 目标

1. 通关 11-10 后，在存档面板出现清晰的「轮回」入口；未解锁时也能打开只读说明。
2. 转生条件明确：本世 11-10 已首通、Lv.100、巅峰 100；每次转生都要重新打到 11-10 与 P100。
3. 转生瞬间按公式结算「轮回印记」，并把本世金币全部消耗；面板展示每一档的来源，玩家能自己验算。
4. 五条永久升级，各 5 级，合计 110 印记；效果可预期、可热改、有上限。
5. 重置范围只覆盖「本世进度」，保留成就/累计统计、设置、引导状态、无尽最高层与本档轮回记录。
6. 转生后无尽解锁状态保留，但新一世未满级前不能开无尽；未重通 11-10 前只能从第 1 层开始。
7. 存档从 v8 升到 v9（成就顺延 v10），旧档可读；坏档字段有夹取，切档/删档/重开不串数据。
8. 可在 Play 模式完整验收，并给 `NewGameProgression` / `EndlessPacing` / `BalanceRegression` 提供可重复的基线。

### 2.2 非目标（v1 不做）

- 账号级 / 跨存档转生、云同步、排行榜、赛季。
- 转生专属角色、专属技能、专属装备槽、专属关卡。
- 装备继承 / 传家宝 / 保留一件装备。v1 全部清空；如果实测反馈过重，v2 再讨论「保留一件评分最低槽位」或「按卖价折印记」。
- 印记退款 / 洗点 / 重置升级。买错就买错，费用低，避免再加一套确认流程。
- 印记换金币、换装备、换掉落保底。印记只买五条固定升级。
- 每日/每周转生奖励、首通额外印记、转生排行榜。
- 多语言；v1 只做中文。
- 新手引导新增第 7 步。转生是终局功能，入口放存档面板即可，不拉长首次引导。

## 3. 玩家看到什么

### 3.1 入口

存档面板底部左侧新增一个按钮与一行状态（存档面板 300,196,680×304；三张卡到 y=230，关闭按钮在 586,250）：

| 控件 | 位置 | 尺寸 | 文案 |
|---|---|---|---|
| 轮回按钮 `_rebirthButton` | 20, 250 | 120×34 | `♾ 轮回` |
| 轮回状态 `_rebirthStatus` | 148, 250 | 300×34 | 见下 |

状态文案：

- 未解锁：`通关 11-10 解锁`
- 已解锁、条件未满足：`第 N 世 · 巅峰 {P}/100`
- 已解锁、可转生：`第 N 世 · 预计 {M} 印记`
- 已解锁、有未花印记：`第 N 世 · 印记 {M}`

按钮始终可点：未解锁/未满足时打开只读面板，说明差什么，不把入口藏起来。点开后关闭存档面板，再开轮回面板，避免两层浮层叠加。

### 3.2 轮回面板

面板 680×680、居中 300,20（v1 为 680×540 / 300,90；v2 为底部最快轮回榜抬高 140px）；整局冻结，和选关/技能/存档/强化/洗练/无尽/设置/对比/引导互斥。

```
┌──────────────────────────────────────────────────────────────┐
│ ♾ 轮回                                            第 2 世 [关闭] │
│ 转生消耗本世全部金币，按巅峰、无尽层数与金币折算轮回印记。       │
│ ┌──────────────────────────────┐ ┌──────────────────────────┐ │
│ │ 印记  8                       │ │ 历史最高巅峰 200          │ │
│ │ 本世 11-10 ✓                  │ │ 无尽最高 45 层            │ │
│ │ 金币折算 +5                   │ │ 转生后从 Lv.1 开始        │ │
│ └──────────────────────────────┘ └──────────────────────────┘ │
│ 战斗本能  攻击/防御/最大生命 +1%/级      Lv 0/5   3 印记 [升级] │
│ 历练      经验 +60%/级                   Lv 1/5   3 印记 [升级] │
│ 财运      金币 +20%/级，掉落 +2%/级      Lv 0/5   2 印记 [升级] │
│ 传承      下次转生起始等级 +3/级         Lv 0/5   4 印记 [升级] │
│ 行囊      下次转生起始药剂 +1、金币 +500/级 Lv 0/5 1 印记 [升级] │
│ 本次可获得 8 印记（巅峰 2 + 无尽 0 + 金币 5 + 基础 1）            │
│ 保留：印记与升级、成就与累计统计、设置、无尽最高纪录、最快轮回榜  │
│ 重置：等级/巅峰/金币/药剂/关卡/首通/其余 4 件装备/技能/待选       │
│ [转生 · 获得 8 印记]   [未满足：本世尚未通关 11-10]               │
└──────────────────────────────────────────────────────────────┘
```

要点：

- 顶部标题右侧显示「第 N 世」；关闭按钮 566,12,90×30。
- 摘要左卡：可用印记、本世 11-10 状态、金币折算；右卡：历史最高巅峰、无尽最高层、转生后起始等级。
- 五行升级每行 640×48，y=160+i×54；名称/效果左侧，等级中间，费用按钮右侧。等级用 `Lv r/5`，不用星星，避免和技能品质混淆。
- 底部三行：本次可得明细、保留/重置、主按钮 + 不可用原因。主按钮 190×34，失败原因 440×34，红色/灰色。
- 未满足条件时按钮置灰，但面板仍可浏览与购买已有印记的升级（如果之前有剩余印记）。
- 购买升级立即扣印记并写盘；不弹二级确认。按钮文案 `升级 3` 同时显示费用；印记不够显示 `需要 3`，已满级显示 `已满`。
- 转生按钮是**两段确认**：第一次点击变成 `确认转生（3）`，3 秒内再点才执行；3 秒后或关闭面板自动还原。用 `Time.unscaledTime`，因为面板打开时游戏冻结。
- 成功后关闭面板，横幅显示 `✦ 第 2 世 · 轮回 +8 印记`，副标题 `Lv.1 开始 · 印记余额 8 · 无尽纪录会保留`；日志写一条 `♾ 转生完成：第 2 世，获得 8 印记（巅峰 2 / 无尽 0 / 金币 5 / 基础 1）`，并追加本世用时/名次与保留装备两行（v2）。
- 转生后回到 `WaitingRespawn`，`IsRunning=false`，不自动开打；玩家点「开始」。

### 3.3 第一次转生的体验

1. 首通 11-10，存档面板出现 `♾ 轮回`，状态 `通关 11-10 解锁` 变为 `第 1 世 · 巅峰 0/100`。
2. 玩家继续打无尽/终局，到 P100；状态变成 `第 1 世 · 预计 4 印记`。
3. 打开面板，看到 5 条升级和 4 印记；推荐先买「历练 1」（+20% 经验，2 印记）或「财运 1」（+20% 金币/+2% 掉落，2 印记），剩余留作下一世。
4. 点转生，3 秒确认；等级回到 1（或传承后的起始等级），金币清空，技能/关卡清空，装备只保留评分最低的 1 件（v2）；印记、升级与最快轮回榜保留。
5. 下一世用更快的经验/金币重打，约 75~85% 的时间回到 11-10；再转生，印记更多，逐轮加速。

## 4. 规则

### 4.1 解锁与可转生条件

| 条件 | 说明 |
|---|---|
| 本档曾首通 11-10 | `rebirthCount > 0` 或当前 `IsStageFirstCleared(11,10)`；只认首通记录 |
| 当前 Lv.100 | `level >= GameConfig.MaxLevel` |
| 当前巅峰 100 | `paragonLevel >= GameConfig.RebirthUnlockParagon`（默认 100） |
| 本世 11-10 已首通 | 转生会清空 `clearedStages`，所以每次都要重新打回 11-10 |
| 不在无尽进行中 | `!EndlessActive`，且无尽结果面板已关闭 |
| 没有待选技能 | `!ChoicePending`（技能三选一 / 待选次数 > 0） |
| 没有其他阻塞流程 | 引导、强化、洗练、对比等面板不阻塞入口；打开轮回面板时先关闭它们 |

`CanRebirth` 要返回**具体原因**，面板直接显示第一条未满足项，例如：

- `未满足：首次通关 11-10 后解锁`
- `未满足：等级 87/100`
- `未满足：巅峰 64/100`
- `未满足：本世尚未通关 11-10`
- `未满足：请先结束无尽模式`
- `未满足：请先选完待选技能`

### 4.2 轮回印记

印记是转生时一次性获得的货币，按本世三项表现折算：

```
P = clamp(paragonLevel, 0, 1000)
F = clamp(endlessBestFloor, 0, 9999)      // 本档历史最高，不清零
G = max(0, gold)                          // 转生瞬间、自动卖待选之后的金币

pMarks = min(10, floor(P / 100))
fMarks = min(5,  floor(F / 50))
gMarks = min(14, floor(log2(max(1, G) / 100000)))

M = 1 + pMarks + fMarks + gMarks
```

- 基础 1 点，保证第一次转生不是 0。
- 巅峰每 100 级 1 点，最多 10 点（P1000）。
- 无尽每 50 层 1 点，最多 5 点（250 层以上封顶）。
- 金币每翻一倍 1 点：10 万以下 0，20 万 1，40 万 2，80 万 3……8.19 亿 13，16.38 亿 14；当前 `int` 金币上限下最多 14 点。
- 印记上限 30（1+10+5+14），不会溢出；`rebirthMarks` 再夹到 0..999999。
- 金币在结算后被**全部消耗**；行囊的起始金币是转生后新发的，不参与本次折算。
- 待选装备在结算前按 `GameConfig.SellPrice` 自动卖出，直接按卖价加到 `gold`，不走 `AddGold` 的财运倍率（否则折算会被放大），所得计入 G，并写日志；已穿在身上的五件装备不折价、不返还。
- 印记在转生成功后加入 `rebirthMarks`；玩家可以在面板里随时购买升级，购买立即写盘。

### 4.3 五条永久升级

| 轨道 | 每级效果 | 1 级 | 2 级 | 3 级 | 4 级 | 5 级 | 总费用 |
|---|---|---|---|---|---|---|---|
| 战斗本能 `Battle` | 攻击/防御/最大生命 +1% | 3 | 4 | 5 | 6 | 7 | 25 |
| 历练 `Growth` | 经验 +60% | 2 | 3 | 4 | 5 | 6 | 20 |
| 财运 `Fortune` | 金币 +20%，掉落 +2% | 2 | 3 | 4 | 5 | 6 | 20 |
| 传承 `Legacy` | 下次转生起始等级 +3 | 4 | 5 | 6 | 7 | 8 | 30 |
| 行囊 `Alchemy` | 下次转生起始药剂 +1、金币 +500 | 1 | 2 | 3 | 4 | 5 | 15 |

- 费用公式：`cost = 基础费用 + 当前等级`，所以 0→1 是表里的第一档，4→5 是最后一档。
- 总费用 110 印记；单次转生最多 30，满级需要多轮。
- 战斗/历练/财运**立即生效**（影响当前这一世）；传承/行囊只影响**下一次转生**的起始状态，面板必须标注「下一世生效」。
- 战斗加成是最终乘区，加在巅峰之后；不要乘进装备主属性，否则五槽与强化会放大。
- 升级不可退款、不可洗点；v1 不做重置按钮。

### 4.4 重置与保留

| 数据 | 转生后 |
|---|---|
| 等级 / 经验 | `level = 1 + 2×LegacyRank`，`exp = 0` |
| 巅峰 / 巅峰经验 | `paragonLevel = 0`，`paragonExp = 0` |
| 金币 | `gold = 0`，随后 `gold += 500×AlchemyRank` |
| 药剂 | `potionCount = AlchemyRank`；`autoBuyPotion` 保留 |
| 当前关卡 | `chapterIndex = 1`、`stageIndex = 1`、`stageKillCount = 0` |
| 章节解锁 | `maxChapterUnlocked = 1`、`clearedStages.Clear()` |
| 五件装备 | 全部 `null`；强化等级、洗练词条、锁定状态一起消失 |
| 待选掉落 | 全部按卖价自动卖出，计入本次金币与印记；随后清空 |
| 技能 | `ownedSkillMask = 0`；六个槽 = -1；`pendingSkillPicks = 0` |
| 生命 / 护盾 | `FillHp()` 满血；`shield = 0` |
| 战斗状态 | `Mode = Campaign`；结束无尽本局；`CurrentState = WaitingRespawn`；`IsRunning = false` |
| 本局统计 | `TotalKills/TotalDrops/TotalEquipped/TotalSold/TotalGoldEarned/TotalDeaths/EliteKills/BossKills` 清零 |
| 横幅 / 日志 | 清空，写一条转生完成日志 |
| 轮回计数 | `rebirthCount += 1`；`rebirthBestParagon = max(旧值, 本次 P)` |
| 印记 / 升级 | 保留；本次印记加入 `rebirthMarks` |
| 无尽最高层 | **保留** `endlessBestFloor`；全局纪录 `EndlessRecord` 不动 |
| 成就 / 累计统计 | **保留**（成就 v10 的 lifetime 字段不参与重置） |
| 设置 / 引导 | **保留**；`GameSettings` 在 PlayerPrefs，不随存档 |
| 存档槽 | 只影响当前槽；其他槽不变 |

### 4.5 边界情况

| 情况 | 行为 |
|---|---|
| 未解锁 / 条件不足 | 面板只读，主按钮置灰并显示原因；不弹确认 |
| 打开面板时正在战斗 | 打开即冻结；关闭后继续。转生成功后不自动继续 |
| 无尽进行中 | 禁止转生；必须先撤退或死亡结束本局 |
| 无尽结果面板开着 | 禁止转生；先关闭结果面板 |
| 技能三选一 / 待选次数 > 0 | 禁止转生；先选完。避免重置后 `ChoicePending` 残留 |
| 有未领取的横幅 | 允许；转生时清空并显示新横幅 |
| 引导未完成 | 允许；保留 GuideDone 状态，不重新弹 6 步引导 |
| 自动购买药剂开着 | 保留设置；起始药剂按行囊，商店开放后自动买 |
| 关闭面板 / 倒计时结束 | 取消确认，按钮还原；不执行转生 |
| 连续点击转生 | 第二次点击后设置 `_rebirthBusy`，防重入；成功后立即清空 |
| 存档写入失败 | 现有 `SaveSystem.Save` 无返回值；v1 记录错误日志，内存状态仍生效；下次切档/写盘再试。后续可加 `TrySave` |
| 坏档 | `FixupRebirth` 夹取计数、印记、等级；非法值按 0；不因坏档阻止进入游戏 |

### 4.6 与无尽模式的关系

转生会清空 `clearedStages`，而当前 `EndlessUnlocked` 只看「本档是否首通 11-10」。如果不改，转生后无尽入口会消失，记录的 290 层也成了死数据。规则改为：

```
EndlessUnlocked = rebirthCount > 0 || 当前档 IsStageFirstCleared(11,10)
CanEnterEndless = EndlessUnlocked && level >= 100 && !ChoicePending
```

- 转生后仍能打开无尽面板看纪录，但 Lv.1 不能开打；到 Lv.100 才能开始。
- 新一世**未重通 11-10** 前，`StartEndlessRun` 只能从第 1 层开始，忽略 `endlessBestFloor - 4` 的检查点；重通 11-10 后恢复检查点。
- `endlessBestFloor` 保留，所以重通后可以从接近历史最高的层数继续；这也让「先重打战役、再冲纪录」有意义。
- 全局纪录 `EndlessRecord` 不随转生/删档变化，只用于展示。

## 5. 数据与公式

### 5.1 印记示例

| 巅峰 P | 无尽 F | 金币 G | 巅峰点 | 无尽点 | 金币点 | 合计 M |
|---:|---:|---:|---:|---:|---:|---:|
| 100 | 0 | 100,000 | 1 | 0 | 0 | 2 |
| 100 | 0 | 500,000 | 1 | 0 | 2 | 4 |
| 200 | 45 | 5,600,000 | 2 | 0 | 5 | 8 |
| 500 | 100 | 20,000,000 | 5 | 2 | 7 | 15 |
| 1000 | 290 | 859,000,000 | 10 | 5 | 13 | 29 |
| 1000 | 500 | 2,100,000,000 | 10 | 5 | 14 | 30 |

复现口径：

- `floor(log2(G/100000))` 用整数循环实现，避免浮点边界：从 100,000 开始，能翻倍就加一点，最多 14 点。
- 金币为 0 或负数时按 0 点；`gold` 是 int，折算用 long 中间值，避免 `gold * 2` 溢出。
- 印记只与**转生瞬间**有关，之后金币花掉/赚到不影响已得印记。

### 5.2 永久加成的代码口径

`PlayerData` 增加派生属性（全部用 `Mathf.Clamp` 防御坏档）：

```
int RebirthBattleRank  => Mathf.Clamp(rebirthBattleRank, 0, 5);
int RebirthGrowthRank  => Mathf.Clamp(rebirthGrowthRank, 0, 5);
int RebirthFortuneRank => Mathf.Clamp(rebirthFortuneRank, 0, 5);
int RebirthLegacyRank  => Mathf.Clamp(rebirthLegacyRank, 0, 5);
int RebirthAlchemyRank => Mathf.Clamp(rebirthAlchemyRank, 0, 5);

float RebirthAttackMultiplier  => 1f + GameConfig.RebirthBattlePerRank * RebirthBattleRank;
float RebirthDefenseMultiplier => 1f + GameConfig.RebirthBattlePerRank * RebirthBattleRank;
float RebirthHpMultiplier      => 1f + GameConfig.RebirthBattlePerRank * RebirthBattleRank;
float RebirthExpMultiplier     => 1f + GameConfig.RebirthExpPerRank * RebirthGrowthRank;
float RebirthGoldMultiplier    => 1f + GameConfig.RebirthGoldPerRank * RebirthFortuneRank;
float RebirthDropBonus         => GameConfig.RebirthDropPerRank * RebirthFortuneRank;
int   RebirthStartLevel        => 1 + GameConfig.RebirthLegacyLevelPerRank * RebirthLegacyRank;
int   RebirthStartPotions      => GameConfig.RebirthAlchemyPotionPerRank * RebirthAlchemyRank;
int   RebirthStartGold         => GameConfig.RebirthAlchemyGoldPerRank * RebirthAlchemyRank;
```

战斗乘区放在**最终值**，不要乘进装备/巅峰的中间项：

```
MaxHp = Round(((BaseMaxHp + armor.HP + helmet.HP) * (1 + AllStats) + ParagonHp) * RebirthHpMultiplier);
Attack = Round(((BaseAttack + weapon.Attack) * (1 + AllStats) + ParagonAttack) * RebirthAttackMultiplier);
Defense = Round((((BaseDefense + armor.Def + helmet.Def) * (1 + AllStats)) * (1 + IronBody) + ParagonDef)
                * RebirthDefenseMultiplier);
```

这样 +5% 是「最终战力 +5%」，不会因为五槽/强化/词条再放大。装备评分用同一套 `PlayerData` 属性，因此评分数字会整体略升；同槽位排序不变，不需要改评分公式。

### 5.3 经验、金币、掉落

- **经验**：在 `PlayerData.AddExp(int amount)` 内统一乘 `RebirthExpMultiplier`，再走现有升级/巅峰循环。日志要显示乘后的实际经验，不要显示基础值。探针调用 `AddExp` 也会吃到加成，正好用来模拟转生档。
- **金币**：新增 `PlayerData.AddGold(int baseAmount)`，内部用 long 乘 `RebirthGoldMultiplier`，夹到 int，返回实际到账值。所有正向金币来源必须改走它：

  | 文件 | 位置 | 来源 |
  |---|---|---|
  | `GameController.cs` | 约 608 | 卖待选 |
  | `GameController.cs` | 约 1215 | 击杀/首通奖励 |
  | `GameController.cs` | 约 1569 | 掉落弱于当前，卖店 |
  | `GameController.cs` | 约 1633 | 其他卖店分支 |
  | `GameController.Endless.cs` | 约 185 | 无尽层通过金币 |

  扣金币（强化 481、洗练 657、买药 1011、自动买药 1029）**不乘**金币倍率，仍然是原价。`TotalGoldEarned` 要加实际到账值，成就的累计金币也记实际值。
- **掉落**：`LootSystem.RollDrop(int unlockedSlots, float bonusChance = 0f)`；`chance = Clamp01(GameConfig.DropChance + DropChancePerExtraSlot × extra + bonusChance)`。`GameController` 调 `RollDrop(unlockedSlots, Player.RebirthDropBonus)`；探针不传时默认 0，保持旧基线。五槽满掉落 45% + 10% = 55%，不额外封顶（仍在 0..1 内）。

### 5.4 起始状态

```
level = RebirthStartLevel;          // 1..11
exp = 0;
paragonLevel = 0; paragonExp = 0;
potionCount = RebirthStartPotions;  // 0..5
gold = RebirthStartGold;            // 0..2500
currentHp = MaxHp;
```

- 起始等级只跳过 1~10 级，不送技能、不送装备；装备槽的解锁仍按章节重新走。
- 起始金币很少（最多 2500），主要让玩家能买第一瓶药/第一次强化，不构成经济优势。
- 起始药剂与起始金币在**转生后**发放，不参与本次印记折算。

## 6. 存档 v9（原计划 v10）

> 本节只覆盖 v1 转生字段（v9）。v2 新增的 `lifeSeconds` / `rebirthTop` 在存档 **v12**，见 §14.6.4。

### 6.1 新增字段

`SaveData` 与 `PlayerData` 各加 8 个字段：

| 字段 | 类型 | 范围 | 说明 |
|---|---|---|---|
| `rebirthCount` | int | 0..99 | 第几世；0 表示未转生 |
| `rebirthMarks` | int | 0..999999 | 当前可用印记 |
| `rebirthBestParagon` | int | 0..1000 | 历史最高巅峰，用于面板展示 |
| `rebirthBattleRank` | int | 0..5 | 战斗本能 |
| `rebirthGrowthRank` | int | 0..5 | 历练 |
| `rebirthFortuneRank` | int | 0..5 | 财运 |
| `rebirthLegacyRank` | int | 0..5 | 传承 |
| `rebirthAlchemyRank` | int | 0..5 | 行囊 |

不新增 `rebirthTotalMarks`；成就需要「累计印记」时可用 `rebirthCount` 与购买记录推导，或后续再加。

### 6.2 迁移与修复

- `CurrentSaveVersion` 从 8 升到 10（若先做转生则为 9；见文首）。
- `Save()` 逐字段复制 8 个新字段；`endlessBestFloor` 仍夹 0..9999。
- `Load()`：

```
player.rebirthCount      = data.version >= 10 ? Clamp(data.rebirthCount, 0, 99) : 0;
player.rebirthMarks      = data.version >= 10 ? Clamp(data.rebirthMarks, 0, 999999) : 0;
player.rebirthBestParagon= data.version >= 10 ? Clamp(data.rebirthBestParagon, 0, 1000) : 0;
player.rebirthBattleRank = data.version >= 10 ? Clamp(data.rebirthBattleRank, 0, 5) : 0;
... 其余四条同理
```

- `FixupRebirth(player)` 在读档末尾、`EndlessRecord.SyncGlobal` 之前调用：
  - 所有 rank 夹到 0..5；
  - `rebirthCount` 夹 0..99，`rebirthMarks` 夹 0..999999；
  - `rebirthBestParagon = max(rebirthBestParagon, paragonLevel)`，再夹 0..1000；
  - 如果 `rebirthCount == 0` 但任一 rank > 0（手工改档/旧实验档），直接清零 rank 与 marks，避免「第 0 世却有点数」。
- `Describe()` 的 `SlotInfo` 增加 `rebirthCount`，仅当 `version >= 10` 读取；不迁移、不写盘。存档卡可显示「第 N 世」，不改布局。
- `ClearSlot` / `DeleteSlot` / `ResetGame` 仍然 `new PlayerData()`，自然回到 0 世；不需要额外迁移。
- `SwitchSlot` 先 `new PlayerData()` 再 `Load`，新字段随档切换，不会串。

### 6.3 写盘时机

- 转生成功：重置后立即 `SaveSystem.Save(Player)`，再显示横幅；顺序不能反，否则崩溃会留下「已重置但未写盘」。
- 购买升级：扣印记/加 rank 后立即 `SaveSystem.Save(Player)`；金额小但不可回滚，必须落盘。
- 自动卖待选：在内存里卖，随后随转生一起写盘；不需要单独写。
- `SaveSystem.Save` 目前无返回值；如果磁盘失败，日志会报错，但 UI 仍显示成功。v1 接受这个限制，后续可加 `TrySave`。

## 7. 技术设计

### 7.1 文件与职责

| 文件 | 职责 |
|---|---|
| `RebirthCatalog.cs`（新） | 五条轨道的名称/描述/费用/效果文本；印记公式；`MarksFor` / `ParagonMarks` / `FloorMarks` / `GoldMarks` |
| `Models.PlayerData.cs` | 8 个字段；5 个 rank 派生属性；战斗/经验/金币/掉落/起始状态派生；`AddGold`；`AddExp` 乘经验 |
| `GameController.Rebirth.cs`（新） | `RebirthPanelOpen`、`CanRebirth(out reason)`、`PendingRebirthMarks`、`ToggleRebirthPanel`、`BuyRebirthRank`、`PerformRebirth`、重置与日志 |
| `GameUI.Rebirth.cs`（新） | 存档面板入口按钮/状态、轮回面板构建/刷新、倒计时确认、购买回调 |
| `GameUI.Panels.cs` | `BuildSavePanel` 加入口；`RefreshSavePanel` 更新状态 |
| `GameUI.Core.cs` | `RefreshPanels` 调 `RefreshRebirthPanel`；`RefreshHud` 的 modal 判断加入轮回面板；`CloseAllPanels` 关闭轮回 |
| `GameController.cs` | `CloseAllPanels` / `ChoicePending` / `Update` 的冻结逻辑接入轮回面板；金币来源改 `AddGold` |
| `GameController.Endless.cs` / `Endless.cs` | 解锁/等级/检查点规则 |
| `LootSystem.cs` | `RollDrop` 增加 `bonusChance` 可选参数 |
| `SaveSystem.cs` | v9 字段、迁移与坏档夹取、`Describe` |
| `GameConfig.cs` / `GameConfigData.cs` / `game_config.json` | 新配置键与夹取 |
| `Assets/Editor/NewGameProgression.cs` | 转生档模拟参数与轨迹 |
| `Assets/Editor/EndlessPacing.cs` | 满轮回模板，验证无尽天花板 |
| `Assets/Editor/BalanceRegression.cs` | 静态公式/费用/印记表哨兵 |

### 7.2 建议签名

```
public enum RebirthTrack { Battle = 0, Growth = 1, Fortune = 2, Legacy = 3, Alchemy = 4 }

public static class RebirthCatalog
{
    public const int MaxRank = 5;
    public static string NameOf(RebirthTrack track);
    public static string EffectText(RebirthTrack track, int rank);
    public static string NextEffectText(RebirthTrack track, int currentRank);
    public static int CostOf(RebirthTrack track, int currentRank);
    public static int TotalCost(RebirthTrack track);
    public static int MarksFor(int paragon, int bestFloor, int gold);
    public static int ParagonMarks(int paragon);
    public static int FloorMarks(int bestFloor);
    public static int GoldMarks(int gold);
}

public partial class GameController
{
    public bool RebirthPanelOpen { get; private set; }
    public bool RebirthUnlocked { get; }
    public bool CanRebirth(out string reason);
    public int PendingRebirthMarks { get; }
    public void ToggleRebirthPanel();
    public void CloseRebirthPanel();
    public bool BuyRebirthRank(RebirthTrack track);
    public bool PerformRebirth();   // 内部再校验一次，UI 的两段确认不是唯一防线
}

public partial class PlayerData
{
    public int AddGold(int baseAmount); // 返回实际到账
    // AddExp 内部乘 RebirthExpMultiplier
}
```

### 7.3 转生执行顺序

```
1. CanRebirth 校验（含无尽/待选/等级/巅峰/首通）
2. 自动卖出所有待选，金币 += SellPrice；记日志
3. int marks = RebirthCatalog.MarksFor(paragon, endlessBestFloor, gold)
4. rebirthBestParagon = max(rebirthBestParagon, paragon)
5. rebirthCount += 1
6. rebirthMarks += marks
7. 重置本世字段（§4.4）
8. level = RebirthStartLevel；gold = RebirthStartGold；potionCount = RebirthStartPotions
9. FillHp()；shield = 0
10. ResetSessionState()（清怪、浮层、待选、计时器、统计、日志）
11. Mode = Campaign；CurrentState = WaitingRespawn；IsRunning = false
12. SaveSystem.Save(Player)
13. 显示横幅/日志
```

注意第 10 步：`ResetSessionState()` 会清日志与统计，所以第 13 步的日志必须放在它之后；`Player` 对象本身不被替换，成就/累计字段不会被清。

### 7.4 与现有面板的接线

- `GameController.CloseAllPanels()` 增加 `RebirthPanelOpen = false`。
- `GameController.ChoicePending` 不把轮回面板算进去（它不是「选择」），但 `GameUI.RefreshHud` 的 `modalOpen` 要加 `IsOpen(_rebirthPanel)`，避免打开时还能点主界面。
- `GameController.Update` 现有的「任一浮层打开就冻结」逻辑要包含轮回面板；如果它用 `AnyPanelOpen` 汇总，记得加新面板。
- `GameUI.BuildCanvas` 在 `BuildComparePanel` / `BuildSettingsPanel` / `BuildGuide` 之后、`BuildBanner` 之前调用 `BuildRebirthPanel(root)`，保证横幅在最上层。
- `GameUI.RefreshPanels` 在 `RefreshGuide` 之后、`RefreshBanner` 之前调 `RefreshRebirthPanel()`。
- 打开轮回面板时先 `CloseSavePanel()` / `CloseAllPanels()`，再 `RebirthPanelOpen = true`，避免存档面板与轮回面板同时存在。

### 7.5 无尽规则改动

```
// GameController.cs
public bool EndlessUnlocked =>
    Player != null && (Player.rebirthCount > 0 ||
        Player.IsStageFirstCleared(GameConfig.ChapterCount, GameConfig.StagesPerChapter));

// GameController.Endless.cs
public bool CanEnterEndless =>
    EndlessUnlocked && Player != null && Player.level >= GameConfig.MaxLevel && !ChoicePending;

public int EndlessSuggestedStartFloor =>
    Player != null && Player.IsStageFirstCleared(GameConfig.ChapterCount, GameConfig.StagesPerChapter)
        ? EndlessCatalog.StartFloorFor(Player.endlessBestFloor)
        : 1;

// StartEndlessRun：未重通 11-10 时，最大起步层强制为 1
int maxStart = Player.IsStageFirstCleared(11,10) ? Mathf.Max(1, Player.endlessBestFloor) : 1;
int floor = Mathf.Clamp(startFloor, 1, maxStart);
```

`GameUI.Endless.cs` 的「开始挑战 / 再次挑战」统一用 `EndlessSuggestedStartFloor`，不要直接 `StartFloorFor(best)`，否则新一世会从 286 层开始。

## 8. 配置

`Assets/Resources/game_config.json` 新增标量（`GameConfigData` / `GameConfig` 同步；`ReportMissingKeys` 会检查 97+ 键）：

| 键 | 默认 | 夹取 | 说明 |
|---|---:|---|---|
| `RebirthUnlockParagon` | 100 | 0..1000 | 转生最低巅峰 |
| `RebirthBattlePerRank` | 0.01 | 0..0.05 | 每级攻/防/血 |
| `RebirthExpPerRank` | 0.60 | 0..1 | 每级经验 |
| `RebirthGoldPerRank` | 0.20 | 0..1 | 每级金币 |
| `RebirthDropPerRank` | 0.02 | 0..0.2 | 每级掉落 |
| `RebirthLegacyLevelPerRank` | 3 | 1..10 | 每级起始等级 |
| `RebirthAlchemyPotionPerRank` | 1 | 0..5 | 每级起始药剂 |
| `RebirthAlchemyGoldPerRank` | 500 | 0..5000 | 每级起始金币 |
| `RebirthBattleCostBase` | 3 | 1..10 | 战斗费用基数 |
| `RebirthGrowthCostBase` | 2 | 1..10 | 历练费用基数 |
| `RebirthFortuneCostBase` | 2 | 1..10 | 财运费用基数 |
| `RebirthLegacyCostBase` | 4 | 1..10 | 传承费用基数 |
| `RebirthAlchemyCostBase` | 1 | 1..10 | 行囊费用基数 |
| `RebirthMarkParagonStep` | 100 | 10..1000 | 每多少巅峰 1 点 |
| `RebirthMarkFloorStep` | 50 | 10..500 | 每多少层 1 点 |
| `RebirthMarkGoldBase` | 100000 | 10000..1000000 | 金币翻倍基数 |

- 费用表用「基数 + 当前等级」公式，不需要 25 个键；上限 `MaxRank=5` 可以写死在 `RebirthCatalog`，因为它是结构而不是平衡旋钮。
- 印记公式的三项上限（10/5/14）也写死在 `RebirthCatalog`；如果要热改，再加键，避免配置膨胀。
- 所有配置在 `GameConfig.Loader` 夹取；`RebirthUnlockParagon=0` 也不能让未通关的档转生（仍要求 11-10 首通）。
- 改配置后，已获得的印记/等级不变；新公式只影响下一次转生结算。
- 2026-10-22 已把 `RebirthExpPerRank` 由 0.20 提到 0.60、`RebirthLegacyLevelPerRank` 由 2 提到 3（§14.4 复跑 82.6% / 47.1%）。

## 9. 平衡

### 9.1 目标

| 指标 | 目标 | 验收方式 |
|---|---|---|
| 首次转生门槛 | P100，约 1 小时终局（P200 约 2 小时纯战斗的基线） | `EndgamePacing` / 手动 |
| 第 2 世回到 11-10 的时间 | 第 1 世的 **75~85%** | `NewGameProgression` 加转生档 |
| 满轮回回到 11-10 的时间 | 第 1 世的 **35~50%** | 同上 |
| 满轮回 P1000 无尽首死中位 | **≤305 层**（当前 290） | `EndlessPacing` 50 种子 |
| 战役 11-10 胜率（满轮回 P200） | 80~90%，不能 100% | `BalanceRegression`/胜率表 |
| 五条升级满级 | 110 印记，约 5~6 轮 | 静态表/手动 |
| 金币折算 | P1000 约 13 点，满值约 29~30 | 静态表 |

2026-10-20 实测见 §14.4：第 2 世 92.4%、满轮回 63.5%、无尽中位 315，三项均未达标；P200 11-10 满轮回 90.5%。2026-10-22 按洗练 0.9 口径复测：第 1 世基线变慢，第 2 世 90.8%、满轮回 61.8%，仍不足；同日已落地 `RebirthExpPerRank=0.60` + `RebirthLegacyLevelPerRank=3`，复跑 82.6% / 47.1%（§14.4）。

### 9.2 数值判断

- **战斗 +5% 封顶**：P1000 满配的攻/防/血各 +5%，相当于 EHP 与 DPS 各 +5%，不是 +15%；按当前无尽曲线，预计 +5~10 层。若实测超过 305，优先把战斗改为「仅战役生效」或降到 +0.5%/级（max +2.5%），不要动经验/金币/掉落。
- **经验 +300%**：满级所需经验约 73.4 万（1→100 的 `30+15L+2L²` 求和），经验 ×4 后等效四分之一；但怪物经验随等级增长，实际升级时间不会严格减到 1/4。它是「下一世更快」的主引擎；2026-10-22 由每级 +20% 提到 +60%（满级 +300%）。
- **金币 +100%**：五槽满级后金币本来过剩，翻倍会进一步过剩；它的价值主要在**前期**更快强化/洗练，而不是终局。满级后多余金币由转生本身消耗。
- **掉落 +10%**：五槽 45% → 55%，期望掉落 +22%；会让装备成型更快，但装备评分上限没变，属于「更快到同一终点」，不是「更高天花板」。如果探针显示装备膨胀过快，降到 +1%/级（+5%）。
- **起始等级 +15**：满级从 Lv.16 开始，只跳过前 15 级（约 4,730 经验），是体验优化，不是主要强度来源；不要为了它加战斗数值。2026-10-22 由每级 +2 提到 +3。
- **起始金币 2500**：约 5 瓶药或一次低阶强化，避免转生后第一分钟无事可做。

### 9.3 探针改动

`NewGameProgression`：

- 新增可选参数 `RebirthProfile profile`（5 个 rank）；默认全 0，保持现有基线。
- 新增三组轨迹：
  - `cycle1`：全 0，现有基线。
  - `cycle2`：建议 `Growth=1, Fortune=1, Alchemy=1`（或按首次可得印记买得到的组合），验证 75~85%。
  - `max`：全 5，验证 35~50%。
- 输出每章末等级/阵亡/耗时；`CheckInvariants` 增加「转生档不得比原档更慢」「max 档不得 0 阵亡通关全部章节」之类的硬约束（具体阈值按实测定）。

`EndlessPacing`：

- 新增 `maxRebirth` 模板：P1000、Lv150、满倍率满词条、五条升级全 5；跑 50 种子。
- 记录首死中位/P10/P90/max；与 290 基线对比，目标中位 ≤305。
- 若 P90 >330 或 max 打满 300，说明战斗加成过强，按 §9.2 回退。

`BalanceRegression`：

- 静态哨兵加：印记表 6 个样例、费用表 5 条总费用、战斗乘区（1.00/1.01/1.05）、起始等级（1/3/11）、起始金币（0/500/2500）。
- 现有胜率表加一列「满轮回」，容差 0.5 个百分点；基线更新前先人工确认。

### 9.4 金币出口的边界

转生对 P1000 的 8.593 亿只折算 13 点，剩余金币被清空；这确实是一次大额消耗。但买满 110 点后，转生不再需要金币（仍可转生刷新纪录，但印记无用），金币会重新堆积。因此：

- 本设计把转生定位为「阶段性的金币出口」，不是「永久回收站」。
- README 的已知问题第 4 条不应标记为完全解决；应改为「转生提供了大额一次性出口，满级后仍需消耗型商店/其他出口」。
- 如果未来加金币商店，转生印记不要与商店货币互通，避免双重折算。

## 10. UI 细节

### 10.1 存档面板入口

- 按钮 `CreateButton(_savePanel, "RebirthEntry", "♾ 轮回", 20, 250, 120, 34, GameConfig.BtnHeader, out _rebirthButtonLabel)`。
- 状态文本 `CreateText(_savePanel, "RebirthStatus", "", 148, 250, 300, 34, 11, GameConfig.TextInfo, MiddleLeft)`。
- 关闭按钮仍在 586,250，不重叠。
- 存档卡不改变尺寸；`RefreshSavePanel` 只在打开/操作后重算状态，不每帧解析 JSON。

### 10.2 轮回面板坐标

面板 `CreatePanel(root, "RebirthPanel", 300, 90, 680, 540, GameConfig.OverlayBg, true)`。

| 控件 | 位置 | 尺寸 | 说明 |
|---|---|---|---|
| 标题 | 20,12 | 400×30 | `♾ 轮回`，19px 粗体 |
| 关闭 | 566,12 | 90×30 | `关闭` |
| 说明 | 20,44 | 640×20 | 一句规则 |
| 左摘要卡 | 20,70 | 310×78 | 印记/本世/金币折算 |
| 右摘要卡 | 340,70 | 300×78 | 历史巅峰/无尽最高/起始等级 |
| 五行 | 20,160+i×54 | 640×48 | 名称/效果/等级/费用按钮 |
| 本次可得 | 20,438 | 640×20 | `本次可获得 ...` |
| 保留行 | 20,460 | 640×18 | 10px 灰 |
| 重置行 | 20,480 | 640×18 | 10px 灰 |
| 主按钮 | 20,502 | 190×34 | `转生 · 获得 M 印记` |
| 原因 | 220,502 | 440×34 | 红色/灰色，自动换行 |

行内：

- 名称 12,5，15px 粗体；效果 12,24，11px 灰；等级 360,15，12px；费用按钮 512,9,112×30。
- 等级显示 `Lv r/5`；满级行整体压暗，按钮 `已满` 置灰。
- `传承`/`行囊` 的效果文本带「下一世」前缀；购买后当前面板的「起始等级」预览要更新（用 rank+1 计算），但本世等级不变。
- 主按钮不可用时，原因文本显示 `CanRebirth` 的第一条；可用时显示 `本次将消耗全部金币，确认后不可撤销`。
- 确认态：按钮文案 `确认转生（3）` → `确认转生（2）` → `确认转生（1）`；倒计时结束回 `转生 · 获得 M 印记`。
- 购买按钮点击后立即刷新行与摘要；不关闭面板。

### 10.3 文案

- 标题：`♾ 轮回`
- 说明：`转生消耗本世全部金币，按巅峰、无尽层数与金币折算轮回印记；印记可购买永久升级。`
- 左摘要：
  - `印记  {rebirthMarks}`
  - `本世 11-10  ✓` / `未通关`
  - `金币折算  +{gMarks}`
- 右摘要：
  - `历史最高巅峰  {rebirthBestParagon}`
  - `无尽最高  {endlessBestFloor} 层`
  - `转生后  Lv.{startLevel}`
- 保留行：`保留：印记与升级、成就与累计统计、设置、无尽最高纪录`
- 重置行：`重置：等级/巅峰/金币/药剂/关卡/首通/五件装备/技能/待选`
- 主按钮可用：`转生 · 获得 {M} 印记`
- 主按钮不可用：`转生`
- 原因示例：`未满足：巅峰 64/100`

## 11. 验收

### 11.1 静态与存档

- `RebirthCatalog` 样例：`MarksFor(100,0,100000)=2`、`(100,0,500000)=4`、`(200,45,5600000)=8`、`(1000,290,859000000)=29`、`(1000,500,2100000000)=30`
- 费用：战斗 3/4/5/6/7 总 25；历练 2/3/4/5/6 总 20；财运同；传承 4/5/6/7/8 总 30；行囊 1/2/3/4/5 总 15；总计 110。
- 加成：全 5 时攻击/防御/生命 ×1.05，经验 ×2，金币 ×2，掉落 +10%，起始 Lv11，5 药，2500 金。
- 存档：v8 旧档读入后 8 个字段为 0；v9 写入后逐字段相等；rank 99/-1 夹到 5/0；`rebirthCount=0` 但 rank>0 时清零；`Describe` 不迁移、不写盘。
- 切档：槽 0 转生后切槽 1，槽 1 的轮回字段不变；切回槽 0 数据正确。
- 删档：删当前槽后轮回归零；全局无尽纪录不变。
- 重开：`ResetGame` 后当前槽归零；其他槽不变。

### 11.2 Play 模式

1. 新档/未通关：存档面板显示 `♾ 轮回`，状态 `通关 11-10 解锁`；打开面板只读，主按钮灰。
2. 首通 11-10、P100：状态 `预计 N 印记`；打开面板，摘要/五行/明细正确。
3. 买一条升级：印记减少、rank 增加、立即写盘；关闭再开仍在；重启 Play 仍在。
4. 转生：第一次点击变 `确认转生（3）`；3 秒内再点执行；3 秒后自动还原；关闭面板取消确认。
5. 转生后：等级=起始等级、金币=起始金币、关卡 1-1、五装备空、技能空、满血、`IsRunning=false`；成就/累计统计/设置/无尽纪录保留。
6. 无尽：转生后入口仍可见但 `开始挑战` 置灰（Lv<100）；到 Lv100 后只能从第 1 层开始；重通 11-10 后恢复检查点。
7. 加成：经验/金币/掉落按倍率；战斗属性 +5%；装备评分随属性略升但同槽排序不变。
8. 边界：无尽进行中/结果面板/技能三选一/待选次数 >0 时禁止转生，原因正确；关闭面板/倒计时取消不执行。
9. UI：1280×720 下不越界、不压页脚；面板打开冻结战斗；横幅在轮回面板之上；日志有转生记录。
10. 控制台：`compilationFailed=false`、`consoleErrors=0`、`consoleWarnings=0`。

### 11.3 平衡验收

- `NewGameProgression`：cycle2 75~85%，max 35~50%；若偏差超过 10 个百分点，先调 `RebirthExpPerRank`/`RebirthLegacyLevelPerRank`，不要先加战斗。2026-10-22 已落地的组合把两项调进区间（82.6% / 47.1%），见 §14.4。
- `EndlessPacing`：满轮回 P1000 中位 ≤305；若 >305，按 §9.2 回退战斗。2026-10-20 实测 315，未达标（§14.4）。
- `BalanceRegression`：静态哨兵全过；胜率表更新后人工确认第 9~11 章没有变成 100%。

## 12. 分期

| 阶段 | 内容 | 预估 |
|---|---|---|
| 1 | `RebirthCatalog`、PlayerData 字段/派生、Save v9、`AddGold`/`AddExp`、配置键 | 0.5 天 |
| 2 | `GameController.Rebirth.cs`、重置流程、购买、无尽规则 | 1 天 |
| 3 | `GameUI.Rebirth.cs`、存档入口、倒计时确认、横幅/日志 | 0.5 天 |
| 4 | 探针（NewGameProgression/EndlessPacing/BalanceRegression）、README、平衡迭代 | 1 天 |
| 5 | Play 验收、坏档/切档/删档回归 | 0.5 天 |

总计约 3.5 个开发日，不含美术/音效；平衡迭代可能再加 1 天。

## 13. 风险与待定

### 13.1 已定决策

- 每存档槽独立；不做账号级转生。
- 每次转生都要重打 11-10 到 P100；不做「一次通关永久可转生」。
- 保留无尽最高层与成就/累计统计；清空本世装备/技能/关卡/金币。
- 印记按 P/F/G 三项对数/阶梯折算，金币全消耗；不做金币商店。
- 五条升级各 5 级，总 110 印记；战斗封顶 +5%。
- 实际存档 v9（成就顺延 v10）；版本按先落地的系统占用。
- UI 入口放存档面板，不新增左栏按钮；不拉长新手引导。

### 13.2 需要实测后定

> 2026-10-20 已用 50 种子探针实测，结果见 §14.4；两项核心目标都未达标：无尽首死中位 315（目标 ≤305），第 2 世 92.4%、满轮回 63.5%（目标 85% / 50%）。2026-10-22 按 0.9 洗练口径复测为 90.8% / 61.8%，随后落地 Exp 0.60 + Legacy 3 并复跑为 82.6% / 47.1%。以下保留原始问题，调参结论已落到 §14.4。

- **战斗 +5% 是否把无尽推过 305**：这是最大的平衡风险。回退方案：仅战役生效，或 +0.5%/级。
- **第 2 世 75~85% 是否达得到**：如果只有 90%，说明经验/起始等级收益不足；优先加 Growth/起始等级，不要加战斗。
- **掉落 +10% 是否让装备成型过快**：如果五槽毕业时间明显缩短，降到 +1%/级。
- **第一次转生门槛 P100 是否太晚**：如果玩家在 P60~80 就腻了，考虑降到 P50；但会加快印记获取，需要重算经济。**v2 未动**：P100 仍保留，理由见 §14.6（改门槛需重跑 §14.4 的 50 种子基线）。
- **是否保留一件装备**：**v2 已做（2026-10-25）**：保留评分最低槽位的一件，不是按卖价折印记；规则与实测见 §14.6。

### 13.3 不解决 / 后续

- 满级后金币仍会堆积；转生只是一次性出口。
- 没有转生专属内容/剧情；新鲜感来自更快的循环，不是新关卡。
- v1 没有转生排行榜/分享；v2 起有**本档内**最快轮回 Top-5（不是联网/账号级排行榜，仍无分享）。
- 成就系统若未落地，累计统计的保留无法验证；本轮先落转生（v9），成就顺延 v10。

## 附录 A：一轮完整算例

玩家当前：Lv100、P200、无尽最高 45 层、金币 560 万、已首通 11-10、无待选。

```
pMarks = min(10, 200/100) = 2
fMarks = min(5, 45/50) = 0
gMarks = floor(log2(5,600,000/100,000)) = floor(log2(56)) = 5
M = 1 + 2 + 0 + 5 = 8
```

假设已有 `rebirthMarks=0`，转生后：

```
rebirthCount = 1
rebirthMarks = 8
level = 1（传承 0）
gold = 0（行囊 0）
equipment = 全空
clearedStages = 空
endlessBestFloor = 45（保留）
```

买 `历练 1`（2）→ `Growth=1`，剩 6；买 `财运 1`（2）→ `Fortune=1`，剩 4；买 `行囊 1`（1）→ `Alchemy=1`，剩 3。下一世起始 1 瓶药 + 500 金，经验 +60%，金币 +20%，掉落 +2%。

## 附录 B：与成就的接口

- 成就 v10 的 lifetime 字段（累计击杀/掉落/金币/死亡等）不参与转生重置；`ResetSessionState` 只清本局计数器，不清 lifetime。
- 如果成就实现时把 lifetime 放在 `PlayerData`，转生只改本世字段，不 `new PlayerData()`，避免把 lifetime 一起丢掉。
- 成就「转生 N 次」「印记达到 M」「五条升级全满」可以在成就 v10 落地后追加；本设计不新增成就条目，避免和成就首发 42 条冲突。
- 成就 toast 与转生横幅同时出现时，toast 在顶部、横幅在中央，不重叠。
## 14. 实现记录（2026-10-20）

状态：**已实现、已编译、Play 验收通过**；平衡分布已于 2026-10-20 跑完（§14.4），当前数值**未达标**：满轮回无尽首死中位 315 层（目标 ≤305），第 2 世/满轮回回到 11-10 为第 1 世的 92.4% / 63.5%（目标 85% / 50%）。2026-10-22 按洗练 0.9 口径复测为 90.8% / 61.8%，同日落地加速参数（`RebirthExpPerRank=0.60`、`RebirthLegacyLevelPerRank=3`）并复跑为 82.6% / 47.1%（§14.4 末）；无尽首死中位 315 未变（战斗加成未动）。

### 14.1 落地文件

| 文件 | 内容 |
|---|---|
| `HelloWorldProject/Assets/Scripts/RebirthCatalog.cs` | `RebirthTrack`、五条升级的名称/效果/费用、印记折算与夹取；v2 追加 `RebirthRecord` / `RebirthHistory`（Top-5 排序、清洗、`FormatSeconds`） |
| `HelloWorldProject/Assets/Scripts/Models.PlayerData.cs` | 转生字段、五条派生倍率、`AddGold`、`AddExp` 经验倍率、战斗乘区最外层；v2 追加 `lifeSeconds` / `rebirthTop` |
| `HelloWorldProject/Assets/Scripts/SaveSystem.cs` | 转生存档 v9（v10 成就、v11 无尽榜、v12 计时与轮回榜）、`SlotInfo` 转生字段、`Describe` 摘要、坏档夹取 |
| `HelloWorldProject/Assets/Scripts/GameController.Rebirth.cs` | `CanRebirth`、面板开关、购买、二次确认、`PerformRebirth` 重置与结算；v2 追加 `FindRebirthKeep`、Top-5 插入、`LastRebirthSeconds` / `LastRebirthRecordRank` |
| `HelloWorldProject/Assets/Scripts/GameUI.Rebirth.cs` | 存档面板入口、轮回面板、五条升级行、保留/重置/原因、横幅；v2 面板 680×680、保留/纪录文案与 5 行最快轮回 |
| `HelloWorldProject/Assets/Scripts/GameController.cs` | v2：`Update` 在「运行中且无浮层」时按 `Time.unscaledDeltaTime` 累加本世用时 |
| `HelloWorldProject/Assets/Scripts/GameUI.Compare.cs`、`GameUI.Panels.Enhance.cs`、`GameUI.Panels.Reforge.cs` | v2：槽位未解锁但已有装备时显示「 · 保留件」 |
| `HelloWorldProject/Assets/Scripts/GameController.Endless.cs` | 转生后无尽解锁、本世重通 11-10 才恢复检查点、等级 100 才能开局 |
| `HelloWorldProject/Assets/Scripts/GameConfig.cs`、`GameConfigData.cs`、`GameConfig.Loader.cs`、`Assets/Resources/game_config.json` | 16 个 `Rebirth*` 键、默认值与夹取 |
| `HelloWorldProject/Assets/Editor/NewGameProgression.cs`、`EndlessPacing.cs`、`LateGameProbe.cs` | 探针接收五条 rank：新档构造转生起始角色，掉落/金币走转生倍率；无尽与 P200 胜率在技能挑选后套 rank |

### 14.2 与设计稿的差异

- v1 落地时存档实际为 **v9**（成就顺延 v10），不是原计划的 v10；v2 起为 **v12**（§14.6）。
- 存档面板实际 680×360；轮回入口在 20,250，状态 148,250，关闭按钮 586,306。
- v1 轮回面板 680×540；v2 抬到 **680×680**（y=20，底 700），原五行 y=160+i×54 与费用按钮 512,y+9,112×30 不变，底部加最快轮回 Top-5；主按钮仍是第一击上膛、3 秒内第二击确认。
- v1 转生后**一身空**，不发起始装备，设计 §5.4 的「裸装重开」按原样落地；**v2 起保留评分最低的一件**（§14.6.1），其余四件仍清空。
- 无尽入口仍显示「无尽」，但真正开局要求「已解锁 + 等级 100 + 无待处理选择」；本世未重通 11-10 时起始层强制第 1 层，并显示「本世重通 11-10 后恢复检查点」。
- 印记/面板文案用 `RebirthCatalog.MaxRank` 与 `bestFloorText`，不硬编码 5 或「第 0 层」。

### 14.3 已验证（v1 口径，当时尚未做保留件）

- 首次转生（Lv100/P169/金币 1,773,385/首通 11-10）：预计 6 印记（巅峰 1 + 无尽 0 + 金币 4 + 基础 1）；`PerformRebirth()` 后 count=1、marks=6、bestP=169、Lv1、金币 0、药剂 0、第 1 章 1-1、首通 0、五装备空、技能清空、`endlessBestFloor=11` 保留。
- 购买：`marks 6→3` 买战斗 rank 1；`RebirthBattleMultiplier=1.01`；下级费用正确递增；`marks=0` 时购买返回 false。
- 第二次转生：先卖待选（Lv100 传说 `SellPrice=4832`）再算印记，金币跨过 800,000 档，marks 97→102；传承/行囊生效，下一世 Lv3、金币 500、药剂 1。
- 倍率：`gold=0` 时 `AddGold(100)` 到账 120；Lv3 `AddExp(10)` 得 exp=12。
- 夹取：`SetRebirthRank(Battle,99)→5`、`Fortune,-5→0`；五条 `TotalCost` 合计 110；`MarksFor` 样例 2/4/8/29/30 全对。
- 迁移：v8 旧档带伪造转生字段读入全 0；v9 且 `count=0` 但 rank/marks 非 0 时整组清零；`bestParagon=9999`→1000，ranks 99/-5/99/-1/99→5/0/5/0/5。
- 阻挡：`CanRebirth` 依次给出「需要等级 100（当前 Lv.X）」「需要巅峰 100」「本世通关 11-10 后才能轮回」「先把技能三选一选完」「无尽模式进行中，先撤退或结束本局」。
- 无尽：转生后 Lv1 时 `CanEnterEndless=False`，面板开始按钮禁用并提示「需要等级 100 才能进入无尽模式（当前 Lv.1）」，直接调 `StartEndlessRun` 被守卫拦下；Lv100 且本世首通 11-10 后 `EndlessSuggestedStartFloor` 恢复检查点 7。
- 编译：`compilationFailed=false`、`consoleErrors=0`；Play 打开轮回面板无 NRE。
- 探针冒烟：默认参数 `NewGameProgression.Run(12345, 30, false, 0.80f)` 全程通关，13.7min / 2 阵亡 / 结束时 P41 / 金币 8,755；`BalanceRegression.RunStaticOnly()` 返回 `PASS — 静态哨兵与基线一致`。五条 rank 全 5 的单种子冒烟：起始 Lv.11 / 金币 2,500 / 药剂 5，7.7min / 0 阵亡 / 结束时 P63 / 金币 24,348（当时传承每级 +2；2026-10-22 改为 +3，满轮回起始 Lv.16；后来的 50 种子复跑见 §14.4 末）。单种子只验证接线，不代表成功率分布。

### 14.4 平衡分布（2026-10-20）

三组分布都用 NewGameProgression / EndlessPacing / LateGameProbe 的五条 rank 入口；默认全 0 保持旧基线。**先套 rank 再选技能会改变三选一评分**，所以两个满装探针都在技能挑选之后才套 rank。

#### 新档（50 种子）

条件：seed=20261019..20261068，maxAttemptsPerStage=30，reforgeQuality=0.80。

| 档位 | 到达 11-10 | 全程通关 | 中位到终局 | 通关局中位总时长 | 中位阵亡 | 中位结束 P |
|---|---|---|---|---|---|---|
| 第 1 世（全 0） | 50/50 | 40/50 | 743.7s | 787.4s | 9.5 | 43 |
| 第 2 世（G1/F1/A1） | 50/50 | 46/50 | 702.0s | 747.3s | 8 | 49 |
| 满轮回（全 5） | 50/50 | 49/50 | 474.1s | 491.3s | 0 | 63 |

相对第 1 世的中位到终局比：第 2 世 0.924（n=50），满轮回 0.635（n=50）；通关局总时长比 0.930（n=38）与 0.621（n=39）。目标分别是 75~85% 与 35~50%，实测 92.4% / 63.5%，**加速不足**。时间大头在终局，经验/金币/起始等级只能压缩前半程；优先调 RebirthExpPerRank / RebirthLegacyLevelPerRank，不要先加战斗。

#### 无尽（50 种子）

条件：P1000、Lv150、affixQuality=1、maxGear=true、maxFloor=400，seed=20261019+i*7919。README 里旧的 300 层上限基线（P90 296 / max 300）不是同一截断，下表只和本轮同种子全 0 对比。

| ranks | min | P10 | 中位 | P90 | max |
|---|---|---|---|---|---|
| 全 0（本轮同种子） | 255 | 272 | 290 | 305 | 320 |
| 全 5（满轮回） | 272 | 300 | 315 | 330 | 341 |

满轮回模板：攻 4,650 / 防 7,093 / 血 73,537 / 速 122 / 暴击 95% / 暴伤 368% / 穿甲 34.9%，15 技能。50 个种子全部阵亡，0 个打满 400 层；层通关率 300 层 42/45（93%）、305 层 35/38（92%）、315 层 22/26（85%）、330 层 5/6（83%）、335 层 1/5（20%）。

目标中位 ≤305；实测 315，**未达标**。P90 330 正好在「>330 才回退」的边界，max 341 说明有种子明显超过 300 层。战斗 +5% 把中位从 290 推到 315，约 +25 层，比设计预估的 +5~10 层更强。

临时把 RebirthBattlePerRank 从 0.01 调到 0.005（其余全 5，技能槽复制 baseline）后：min 261 / P10 275 / 中位 302 / P90 315 / max 320，中位回到目标内；测试后已恢复 0.01。若要求 max 也不超过 300，只降战斗还不够，需要同时压无尽曲线或给战斗加成加「仅战役」限制。

#### 战役 11-10 胜率（P200）

条件：LateGameProbe，perType=30、trials=12、resetHp=true、smartPick=true、Lv110 关底 BOSS（11-10）。

| ranks | 加权胜率 | 技能数总和（均值） |
|---|---|---|
| 全 0 | 80.83% | 180（15） |
| 全 5 | 90.54% | 180（15） |

目标 80~90%；baseline 在区间内，满轮回 90.54% 在上沿略超。这里同样是在技能挑选之后套 rank；如果先套，三选一会偏向被加成的属性，构筑与正常玩家不可比。

#### 结论

1. **战斗 +5% 偏强**：无尽中位 290→315。建议 RebirthBattlePerRank=0.005（满 +2.5%）或改成仅战役生效；不要先加经验/金币。
2. **第 2 世/满轮回加速不足**：92.4% / 63.5%，离 85% / 50% 还有距离；应加 Growth/传承或压缩终局刷怪，不要用战斗补。
3. **P200 战役胜率**：满轮回 90.5%，略高于 90% 上沿，可接受但不宜再高。
4. 以上只是测量与建议，**没有改产品数值**；RebirthBattlePerRank 仍是 0.01。是否按建议调参由后续决定。

EndlessPacing.BuildFullPlayer 与 LateGameProbe.Build 都已在技能挑选后套 rank；此前「rank 在技能前」跑出的满轮回无尽中位 125 层是探针伪影（丢掉钢铁之躯），不要引用。

#### 2026-10-22 复测、落地与复跑（洗练 0.9 口径）

2026-10-21 把 `ReforgeCostGrowth` 从 0.6 提到 0.9 后，第 1 世终局的反复洗练变贵，基线整体变慢；旧比值不能直接沿用，因此用同一批种子（20261019..20261068，maxAttemptsPerStage=30，reforgeQuality=0.80）重跑。

| 档位 | 到达 11-10 | 全程通关 | 中位到终局 | 比值 | 中位阵亡 | 中位结束 P |
|---|---|---|---|---|---|---|
| 第 1 世（全 0） | 50/50 | 38/50 | 805.75s | 1.000 | 10 | 44 |
| 第 2 世（G1/F1/A1，Legacy rank0，起始 Lv1） | 50/50 | 47/50 | 731.4s | 0.908 | 8 | 49 |
| 满轮回（全 5，起始 Lv11） | 50/50 | 50/50 | 498.25s | 0.618 | 0 | 63 |

与 0.6 口径的旧表（743.7s / 702.0s / 474.1s，0.924 / 0.635）相比，第一世被拉慢约 62s，两个比值都略差。落地前先在同一批种子上预跑（第二世的 Legacy 必须 rank0，起始 Lv1）：

| 候选参数 | 第 2 世中位 / 比值 | 第 2 世通关 | 满轮回中位 / 比值 | 满轮回通关 | 满轮回起始 Lv |
|---|---|---|---|---|---|
| Exp 0.60 only | 665.4s / **0.826** | 47/50 | 413.0s / 0.513 | 50/50 | 11 |
| Exp 0.65 only | 651.2s / **0.808** | 50/50 | 405.3s / 0.503 | 50/50 | 11 |
| Exp 0.70 only | 651.9s / **0.809** | 49/50 | 405.35s / 0.503 | 50/50 | 11 |
| **Exp 0.60 + Legacy 3** | 665.4s / **0.826** | 47/50 | 379.75s / **0.471** | 50/50 | 16 |

为了定位满轮回的瓶颈，另跑了几组单点（这些组把第二世的 Legacy 也设成 rank1，起始 Lv3~5，与 G1/F1/A1 口径不符，**第二世列作废**，只取满轮回列）：Exp 0.45 + Legacy 4 → 380.7s / 0.472；Exp 0.40 + Legacy 3 → 421.5s / 0.523；Exp 0.40 + Legacy 4 → 398.65s / 0.495；Exp 0.45 only → 446.55s / 0.554；Legacy 4 only → 449.85s / 0.558。

结论与落地：

1. **第 2 世只能靠 Exp**：Legacy rank0 时 `RebirthStartLevel` 仍是 1，传承完全不参与。+20%（旧默认）实测 0.908；+60% 到 0.826，进入 75~85%；再加到 +65%/+70% 是 0.808/0.809，边际已经很小。
2. **满轮回需要 Exp 与起始等级一起加**：Exp 单独到 0.70 仍卡在 0.503，说明后半程瓶颈不是经验而是章节推进/起始等级；Legacy 单独（rank4）只有 0.558。两者叠加才到 0.471，而且 Exp 0.40 + Legacy 3（0.523）反而不如 Exp 0.60 + Legacy 3，说明经验不能为起始等级让路。
3. **已落地并复跑（2026-10-22）**：`RebirthExpPerRank` 0.20 → **0.60**、`RebirthLegacyLevelPerRank` 2 → **3**，三处同步（`GameConfig.cs:687/696`、`GameConfigData.cs:158/161`、`game_config.json:139/142`）。落地后用同一批 50 种子复跑：

| 档位 | 中位到 11-10 | 比值 | 通关 | 中位阵亡 | 结束 P | 起始 Lv |
|---|---:|---:|---:|---:|---:|---:|
| 第 1 世（全 0） | 805.75s | 1.000 | 38/50 | 10 | 44 | 1 |
| 第 2 世（G1/F1/A1） | 665.40s | **0.826** | 47/50 | 0 | 56 | 1 |
| 满轮回（全 5） | 379.72s | **0.471** | 50/50 | 0 | 85 | 16 |

复跑与预跑逐项一致（满轮回 379.75 → 379.72 是四舍五入），静态回归 `BalanceRegression.RunStaticOnly()` 仍为 PASS。

4. 副作用：满轮回开局 Lv16 会跳过前几级；Exp 满级 +300% 意味着后期轮回升级极快、前几章进一步缩短。`EndlessPacing` / `LateGameProbe` 的模板是构造好的满装角色，本次只改经验与起始等级，这两个探针没有复跑；战斗加成未动，满轮回 P1000 首死中位 315 的结论不因这次改动变化。

### 14.5 尚未验证 / 后续

- 平衡分布与转生加速都已跑完并落地（§14.4，2026-10-22 复跑 82.6% / 47.1%）；掉落 +10% 对五槽毕业时间的影响已于 2026-10-25 量化（§14.8，无显著效果，保持 +2%/级），P100 门槛也已按 50 种子复评（§14.7，保持 100）。旧稿的「300 层 ≤5% / 250 层 25~45%」与 §9.1 的「首死中位 ≤305」口径不一致，本次按 §9.1 的首死分布验收，层通过率一并记录。
- **v2（2026-10-25）没有动任何平衡数值**：保留一件、本世计时、最快轮回 Top-5 都只在转生瞬间读写状态，不改变战斗/经验/金币/掉落/门槛，所以**没有重跑 50 种子转生加速与印记产出**；§14.4 的 82.6% / 47.1% 仍适用。探针仍从空身起造人，不模拟「保留一件」，因此探针基线也不需要更新。
- v2 的排行榜只记「最快 5 世」，不记每世完整历史、不记最慢/最近，也没有跨存档或联网榜；想回看全部轮回需要另做日志。
- 满级金币仍会堆积；转生只是每次循环的一次性出口。
- v1 没有转生专属内容/剧情/排行榜；v2 只补了本档最快轮回 Top-5，仍无转生专属剧情/联网榜/分享。
- 成就系统顺延 v10；生命周期统计的保留要等成就落地后再回归（成就已于 v10 落地，见 docs/achievements.md）。

### 14.6 v2 实现记录（2026-10-25：保留一件 + 最快轮回 Top-5）

状态：**已实现、已编译、Play 验收通过**；v2 落在存档 v12，当前存档为 v13（B2 成就兑换），v13 不碰转生字段。改动范围覆盖 roadmap B4 的 (a)(b)；(c) P100→P50 不塞进 v2，已单独作为 B4c 复评，见 §14.7。

#### 14.6.1 保留规则

- 在 `GameController.Rebirth.PerformRebirth()` 里，**先卖待选、算印记之前**挑保留件：此时等级/巅峰/配装都还是本世结束时的高值，`EquipmentScore.Of` 的增量评分才代表「这件装备现在值多少」。
- `FindRebirthKeep(out EquipmentSlot slot)` 遍历 `EquipmentSlots.All`（武器/衣服/头盔/鞋/饰品），对每件非空装备调 `EquipmentScore.Of(Player, candidate, candidateSlot)`，取分数最低的一件；全空返回 null。
- 清空五槽后只把这一件 `SetEquipment` 放回；其余四件按 v1 直接清空，**不退款、不折金币/印记**。待选装备仍在卖店流程里换成金币，不参与保留。
- 保留件若落在小件槽（`EquipmentSlots.IsMinor`：头盔/鞋/饰品），同步置 `slotStarterMask` 的对应位：表示该槽的「解锁赠礼」已经用过，解锁时不会再发一件稀有起步装；若保留件在武器/衣服，本来就没有 starter bit。
- `Player.FillHp()` 安排在重新装备之后，保留衣服/头盔时会按新的 MaxHp 满血。
- 三个面板（对比 / 强化 / 洗练）的槽位页签在「槽位未解锁但已有装备」时显示 **「 · 保留件」**，避免出现「装备生效却标未解锁」的自相矛盾；解锁判定本身没有改。
- 轮回面板文案：左卡第三行「本世用时 mm:ss · 保留最低分 1 件」；keep 行「保留：印记 … · 无尽最高 … · 成就 / 纪录 / 1 件装备」；reset 行「装备只清 4 件」。

#### 14.6.2 最快轮回 Top-5

- 数据结构在 `RebirthCatalog.cs`（不新建 .cs，避开 Unity 编译输入表问题）：`[Serializable] RebirthRecord { int life; int seconds; int paragon; int floor; int marks; }` + `static RebirthHistory`。
- 排序：`seconds` 升序 → `marks` 降序 → `life` 升序；`Insert` 就地排序并裁到 5 条，返回 1 起的名次（掉榜返回 0）。`CopyTop` / `FromSave` 会清洗 null、负值、超界字段；`Clamp` 把 seconds 夹到 0..30 天、paragon/floor/marks 夹到各自上限。
- 记录字段：`life` = 转生前的 `rebirthCount + 1`（第几世）、`seconds` = 本世用时、`paragon` = 转生时巅峰、`floor` = 转生时无尽最高层、`marks` = 本次获得的印记。
- 只有本世用时 **≥1 秒**才插入纪录，防探针/脚本在 00:00 刷出假纪录；不足 1 秒的转生不写榜单。
- 控制器暴露 `LastRebirthSeconds` / `LastRebirthRecordRank`；转生日志写「♾ 本世用时 mm:ss，轮回纪录第 N 名」与「♾ 保留装备：…（评分最低槽位）」。
- 轮回面板从 680×540 抬到 **680×680**（`y=20`，绝对底 700），原 0..536 的子控件坐标不动；y=544 标题「轮回纪录 · 最快 5 世（本世用时）」，y=566+i*20 共 5 行，第 1 名用金色，无纪录时显示「还没有纪录 —— 每次转生会自动记下本世用时」。

#### 14.6.3 本世计时口径

- `PlayerData.lifeSeconds` 是「本世真实秒数」，不是游戏内加速后的秒数。
- 累加点在 `GameController.Update`：`if (!IsRunning) return;` 与整串浮层冻结 return **之后**，按 `Time.unscaledDeltaTime` 累加；因此暂停、设置/成就/存档/强化/洗练/关卡/无尽/轮回/关于/显示等任何浮层打开时都不计时，战斗、等待、商店、三选一等待都计时；2×/4× 速度不改变计时。
- 上限 `RebirthHistory.MaxSeconds`（30 天），防挂机溢出；转生时 `Mathf.RoundToInt` 后夹取，再清零。
- 计时只用于本档最快轮回榜，不参与战斗、评分、成就指标或经济。

#### 14.6.4 存档 v12

- `CurrentSaveVersion` 11 → **12**；`SaveData` 新增 `float lifeSeconds` 与 `List<RebirthRecord> rebirthTop`，`Save` 写 `Mathf.Clamp` 后的值与 `RebirthHistory.CopyTop`。
- `Load`：`version >= 12` 才读两字段；旧档（v11 及更早）得到 `lifeSeconds=0` / 空榜；NaN/Infinity/负数归 0；`rebirthTop` 经 `FromSave` 清洗。
- `rebirthCount <= 0` 的旧档清理块会一并清空 `rebirthTop`，防手改档在没有转生次数时伪造榜单；正常 v12 档（rebirthCount>0）不受影响。
- 导出/导入沿用 v12 的整段 `SaveData` JSON；关于页与导出说明动态读 `SaveSystem.SaveVersion`，不需要改文案。

#### 14.6.5 为什么不做 P100→P50

- roadmap B4 写的是「三件里选做」；改门槛会直接作废 §14.4 的 82.6% / 47.1% 基线（第 2 世与满轮回的到达时间、印记产出、P100 体验都要重算），属于需要单独拍板的平衡决策。
- 本次只做体验向的 (a)(b)，P100 门槛原样保留；如果之后决定降到 P50，必须按 roadmap 验收口径重跑 50 种子转生加速与印记产出，并回填 §14.4。2026-10-25 已按 B4c 补测 P50/P100 断面，结论仍是保持 P100，见 §14.7。

#### 14.6.6 验收

- Play 中构造 Lv100 / P100 / 已首通 11-10 / `lifeSeconds=123.4` / 金币 1000，五件装备评分 7320.8（武器）、2896.8（衣服）、751.7（头盔）、995.3（鞋）、**306.2（普通饰品，最低）**；`PerformRebirth()` 返回 true，转生后只剩普通饰品，`slotStarterMask=16`，`rebirthTop[0] = 第1世 / 123秒 / P100 / 无尽0层 / +2印记`，名次 1；`LastRebirthSeconds=123`。
- 强化面板页签读回「武器 | 衣服 | 头盔 · 未解锁 | 鞋 · 未解锁 | 饰品 · 保留件」。
- `new PlayerData()` + `SaveSystem.Load` 回读 `v=12 lvl=1 top=1 life=0 acc=普通饰品 mask=16`，v12 字段完整。
- 纯逻辑：插入 7 条 300/100/200/100/50/700/400 秒后只剩 5 条，顺序 50/L5、100/L4、100/L2、200/L3、300/L1（100 秒并列按 marks 降序）；`FormatSeconds(123)=02:03`、`FormatSeconds(3723)=1:02:03`。
- 静态回归 `BalanceRegression.RunStaticOnly()` PASS；`AchievementRegression.RunCore()` 644 通过 / 0 失败；`SaveVersion` 读回 12。截图 `HelloWorldProject/Assets/Screenshots/b4_rebirth_records.png`。

#### 14.6.7 已知限制

- 榜单只保留「最快 5 世」，不是完整轮回历史；转生次数本身仍只在 `rebirthCount` 里，不能从榜单反推总世数。
- 本世用时不含开面板/暂停时间，故意如此；如果玩家靠长时间挂机刷资源，榜单不会惩罚挂机时长。
- 保留件不参与探针的起始构造，所以探针的转生加速基线仍按「空身重生」口径；两者不要混用。

### 14.7 B4c：P100 转生门槛复评（2026-10-25）

状态：**已测完，结论保持 `RebirthUnlockParagon = 100`，没有改任何数值**。

#### 14.7.1 口径

- 探针：`AutoBattle.EditorTools.EndgamePacing.Run(seed, 20000, false, targetParagon, 0.8f)`；50 个连续种子 20261018..20261067。
- `BuildFresh100` 已解锁全章与饰品槽，起始为 Lv100 + 传说五件套 Lv100 + 0 巅峰，在 11-10 循环 5 小怪 + 轮换 BOSS，自动强化/洗练（0.8 质量门槛）。
- 断面取「首次通关 11-10」「P50」「P100」三处；耗时含刷怪等待与 BOSS 前商店，不含玩家看面板/犹豫。

#### 14.7.2 50 种子实测

| 断面 | 挑战数中位（IQR，min..max） | 含刷怪+商店分钟中位（IQR，min..max） |
|---|---|---|
| 首次通关 11-10 | 12（5..21.5，2..47） | 1.75（0.8..2.58） |
| P50 | 34.5（31.25..37，25..42） | 7.7（6.83..8.57） |
| P100 | 159（152..165，145..197） | 30.7（28.32..32.08，25..41.6） |

- 旧口径（未解锁饰品）为 P50 35 / 7.7min、P100 157 / 29.8min、首通 12，差异在噪声内；解锁饰品不影响这条结论。
- 首通到 P50 约 +6 分钟，首通到 P100 约 +29 分钟。门槛从 100 降到 50，等于把「首通后必须多磨半小时」压到「多磨 6 分钟」，转生循环会快约 4 倍。

#### 14.7.3 决策

- **保持 100**。理由不是「P100 很好玩」，而是：没有证据表明 30 分钟终局不可接受；一旦降到 50，§14.4 的 82.6% / 47.1% 门槛基线、印记产出与「第 2 世 vs 满轮回」节奏全部作废，必须重跑 50 种子，不能只改常量。
- 若以后要缩短终局，优先做终局玩法/金币出口（B1）或在 UI 明示「P100 后转生」，而不是把门槛砍半后重做整套基线。
- 本次未改 `GameConfig.RebirthUnlockParagon`、未改存档、未改探针默认参数。

### 14.8 D3：财运掉落 +2%/级对五槽毕业的影响（2026-10-25）

状态：**已测完，结论保持 `RebirthDropPerRank = 0.02f`（+2%/级），没有改数值**。

#### 14.8.1 为什么不能直接比 rank 0 vs 5

- rank 0 vs rank 5 的「五槽集齐」中位是 71 vs 55.5 次（−22%），但 rank 5 同时带 `RebirthGoldPerRank=0.20` 的 +100% 金币；金币会买强化/洗练，掉落只是搭头。把差异归给掉落是错的。
- 因此固定 `rebirthFortuneRank=5`，只改 `GameConfig.RebirthDropPerRank` 做 0% / +5% / +10% 三组 50 种子对照（同种子 20261018..20261067，targetParagon=200，maxStages=20000，reforgeQuality=0.8）。

#### 14.8.2 50 种子实测（五槽集齐 = 每槽首次见到 Lv110 传说掉落）

| 掉落加成 | 挑战数 min / Q1 / 中位 / Q3 / max / mean | 含刷怪+商店分钟 min / Q1 / 中位 / Q3 / max / mean |
|---|---|---|
| +0%（0.00） | 32 / 47.5 / 60.5 / 76.25 / 153 / 65.68 | 7.0 / 9.8 / 12.75 / 15.67 / 28.4 / 13.84 |
| +5%（0.01） | 24 / 50.25 / 65 / 80.75 / 151 / 68.96 | 5.0 / 10.5 / 13.55 / 16.18 / 28.0 / 14.15 |
| +10%（0.02，现行） | 28 / 44.75 / 55.5 / 73.75 / 113 / 61.08 | 5.7 / 9.2 / 11.6 / 15.22 / 24 / 12.59 |

- +10% vs 0% 配对差：中位 7.5 次、均值 4.60 次；95% bootstrap CI −7..+16 次；21 个更快 / 28 个更慢 / 1 平；符号检验双侧 p=0.39。
- 三组非单调（60.5 / 65 / 55.5），没有统计显著效果。

#### 14.8.3 机制与结论

- `RebirthDropBonus` 只进普通怪的 30% 掉落判定（`NewGameProgression.cs:482`）；BOSS 固定掉 2 件（`GameConfig.Tables.cs:154`，史诗 80% / 传说 20%），完全不吃 drop bonus。普通怪 `dropLevel=p.level=100`，Lv110 只能从 BOSS 出——掉落加成对「Lv110 五槽集齐」没有直接因果，只能通过额外普通掉落的金币/RNG 流间接影响。
- 所以 **保持 +2%/级**；降到 +1%/级对终局五槽毕业没有可测收益，只会削弱前中期普通掉落。若以后觉得财运太强，真正的杠杆是 `RebirthGoldPerRank`（每级 +20%，满级 +100%），不是掉落。
- 口径注意：自动换装毕业（五件 Lv110 传说被 `TryAutoEquip` 穿上）永远不达成——Lv100 套已强化 +15，新 Lv110 件 +0，`EquipmentScore.Of` 用 Enhanced 属性，Lv110 +0 往往低于 Lv100 +15。因此指标只能用「五槽集齐」，不能写「已装备毕业」；真人需要手动换装/重新强化。

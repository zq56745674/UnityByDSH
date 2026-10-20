---
id: "1066008a-f3e2-496e-8317-4c7895e2397d"
title: "Unity AutoBattle 成就设计复审：v10 存档、批次结算与统计接线（未实现）"
description: "docs/achievements.md 完成项目对照修订后的实现契约与审查证据：42条/805点、加载不发钱、累计跨转生、事件去重、无阵亡终局提前返回陷阱及待验收范围。"
status: "active"
created_at: "2026-10-20T13:30:31.033Z"
updated_at: "2026-10-20T13:30:31.033Z"
content_hash: "73379ddfacf297bdabfb6643a29186cf7d1b1b8a6460bc68578b0dcebd31f3c5"
source_paths:
  - "docs/achievements.md"
  - "HelloWorldProject/Assets/Scripts/GameController.cs"
  - "HelloWorldProject/Assets/Scripts/GameController.Endless.cs"
  - "HelloWorldProject/Assets/Scripts/GameController.Rebirth.cs"
session_ids:
  - "24691129-440f-46a5-8ffa-244673db332b"
memory_body_ids:
  []
---

# 成就系统设计复审与实现交接

## 状态与依据

完成了对 `docs/achievements.md` 的项目对照审查与修订，收尾文件为634行；静态校验通过：42个唯一成就ID、805点、43,400名义金币、7个称号。**仅修改设计文档，未实现成就代码、未修改存档实现或Unity资源、未运行Unity或数值探针。** 新接口和伪代码都是计划，不能当作现有API。README成就路线图应保持未完成，待实现及验收通过再更新。

相关历史文档：项目总览 `42b8694e-f097-43db-9f58-9d606e02cfd1`（旧进度不能代替本次审查状态）；关卡机制 `418948ff-876f-4f76-90af-50e55a5c8727`；经济/商店提案 `bc1ba543-3f2f-449c-8d11-7e4cb238d13a`（仍是独立提案，不是本次实施内容）。

## 存档与结算契约

- 转生已占用v9，成就计划使用**v10**，保留现有转生字段及旧迁移链。成就归属单个存档槽，跨转生保留。
- 保留HUD八个旧本局计数器，新增持久化 `Player.stats`；相同业务点分别记录，不得混用或重复累计。转生清本局而不清累计。
- 不建立 `claimed` 第二账本。加载补标只补完成证据，不发金币、不弹toast，重复加载须幂等。区分 `EvaluationMode.Live` 与 `LoadReconcile`，`EvaluateBatch(EvaluationMode mode)` 不提供默认模式，防止加载误发钱。
- helper只标记metricsDirty，由最外层动作在业务状态全部更新后统一求值。先验证并计算结算计划，再一次修改unlocked、实际金币及goldEarned，标记保存dirty；仅Live排队toast。首发无累计金币成就，无须奖励递归求值。
- 成就奖励固定、不受转生财运影响，但复用余额封顶规则，统计和展示实际到账而非名义值。这是拟新增的明确例外，实施时须同步AddGold正向收入注释及转生文档。普通出售/首通/无尽奖励仍调用现有 `Player.AddGold`，使用返回actual；转生待选折现沿用不吃财运规则，按余额前后差计入。
- `ResolveStageClear`、`EnsureEquipmentSlotStarters`、`ResolveEndlessFloorClear` 现有内部保存请求须延迟到最外层结算完成，防止纪录已落盘、成就尚未结算。`SaveSystem.Load` 成功不等于迁移后已写回v10，加载协调流程须显式写回。
- 不承诺PlayerPrefs.Save崩溃原子性或防回档作弊。保存失败保留dirty并可重试。

## 已核对的业务入口与计数约束

下列行号是复审时位置，以方法和成功分支为准：

| 入口 | 接线约束 |
|---|---|
| `HelloWorldProject/Assets/Scripts/GameController.cs:1259` HandleMonsterKilled；`HelloWorldProject/Assets/Scripts/GameController.Endless.cs:166` HandleEndlessMonsterKilled | 各自实际击杀后计数，共享GrantKillReward不得再计一次；掉落、经验、装备状态完成后求值 |
| `HelloWorldProject/Assets/Scripts/GameController.cs:606` SellPending | 成功售出才计sold，金币取AddGold实际返回值 |
| `HelloWorldProject/Assets/Scripts/GameController.cs:1568-1583` ResolveDrops出售分支 | 计新掉落直接售出的那件，进待选不算出售 |
| `HelloWorldProject/Assets/Scripts/GameController.cs:1632-1641` TryAutoEquip(Equipment, out bool) | 旧待选被顶替时只计售出旧件；新件仍为待选，不计穿戴 |
| `HelloWorldProject/Assets/Scripts/GameController.Rebirth.cs:139-152` PerformRebirth | 每件待选折现sold+1，goldEarned取封顶后余额差，不抄原始售价 |
| `HelloWorldProject/Assets/Scripts/GameController.cs:590` EquipPending；`HelloWorldProject/Assets/Scripts/GameController.cs:1554-1559` ResolveDrops成功穿戴分支 | 手动/自动穿戴各计一次，不再于TryAutoEquip或WearEquipment重复记录；新档/迁移赠送初装及仅进待选不计 |
| `HelloWorldProject/Assets/Scripts/GameController.Endless.cs:200` ResolveEndlessFloorClear | 更新本槽endlessBestFloor与实际奖励后才有已清层证据，在外层无尽击杀批次末求值，不能任意击杀就加层数 |

技能：ChooseSkill必须在GrantSkill **及技能TryAutoEquip都完成**后求值；覆盖手动EquipSkill/UnequipSkill的实际槽位变化。技能与装备TryAutoEquip是不同重载，不可混淆。强化/洗练仅成功操作计次数，并更新历史峰值与实际三锁成功次数。

## 无阵亡挑战与终局陷阱

- ID `no_death_chapter` 不变，名称为“无阵亡通关”、称号“不屈”；允许受伤、补血与正常养成，并非零伤害。
- 仅第9–11章，从第1关完整起点进入，临时记录attemptChapter / nextRequiredStage / active，必须同一会话依次完整清1..10。使用推进前captured chapter/stage核对；乱序/重复清关不推进资格。
- 有效SelectStage即取消旧尝试，包括重选当前关；若重新完整开始合格章第1关可新开尝试。仅打开/取消面板不取消。阵亡、离章、进入无尽、转生、ResetGame、读档/切档均取消。普通暂停、模态面板、技能选择、强化/换装不取消。
- 第10关完成后持久化 `noDeathChapterMask` 的bit 8/9/10，并结束尝试。不持久化未完成挑战，重启在第1关中途也不能自动恢复资格，UI须明示读档需重试。
- **终局重打易漏事件**：`HelloWorldProject/Assets/Scripts/GameController.cs:1318` 存在 `if (SuppressFinalStageFarmKill()) return;`；`HelloWorldProject/Assets/Scripts/GameController.cs:1405-1415` 在IsFinalStage、已首通、达到killTarget时清零stageKillCount并返回true，绕过普通通关结算。已首通11-10后再做连续挑战，须在此提前返回前记录有效清关证据，不能只挂ResolveStageClear。收尾已补设计规则及对应验收，不代表代码已修。

## 指标、平衡与界面

- best_enhance、best_reforge_quality为持久化历史峰值；五件套指标读当前装备。80%词条质量可由天然装备达成，不强行解释为洗练次数或30小时终局目标；三锁只适用于有足够词条的大件，覆盖79.49%–80.00%边界测试。
- 战斗公式不加直接属性，但金币带来间接养成影响，不能称零数值影响；43,400只是名义总额。成就金币是收入，不解决终局金币出口。
- 无尽250层保留首发目标，不随理想模板数据追涨。正常可达性须用合法五槽装备、明确转生状态及采样限制验证，不拿不合法四词条小件或历史设计目标当当前实测上限。
- 1280×720面板居中位置修正为236,80；区分列表容器坐标与行本地坐标，防止双重偏移。成就面板加入GameController.Update冻结判断，toast避开怪物标题/血条。技能选择、设置、引导、对比、无尽结算、转生等浮层纳入互斥与冻结验收。

## 实施顺序与未验证范围

先v10数据/累计/迁移与dirty，再42条目录和模式明确的批次求值，再业务接线与连续挑战，随后UI，最后异常/经济/战斗回归和文档同步。验收须覆盖旧档与坏档、重复加载不发钱、保存失败重试、转生保留、事件不重计、终局重打、技能手动填六槽、无尽仅清层计入及UI门禁。

待实际运行：NewGameProgression.RunAssert、BalanceRegression.RunStaticOnly、转生/无尽回归；同种子同状态战斗结果不变而经济影响单独评估；早期强化节奏与转生印记影响；合法装备250层可达性；Unity截图检查中文长文、字体、toast/日志遮挡。以上均未由本次文档静态校验证明。

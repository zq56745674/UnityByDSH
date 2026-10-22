# 成就系统设计

状态：2026-10-20 **已实现**（存档 v10）。实现与验收记录见 §15；本文其余章节保留为设计依据，未逐条回改。
涉及存档：**v10**。`SaveSystem.CurrentSaveVersion = 10`；v9 已用于转生，v10 用于成就累计与解锁。
相关文件（已实现）：`Models.PlayerStats.cs` / `Models.Achievements.cs` / `AchievementCatalog.cs` / `AchievementSystem.cs` / `SaveSystem.cs` / `GameController.cs` / `GameController.Endless.cs` / `GameController.Rebirth.cs` / `GameUI.Achievements.cs` / `Assets/Resources/achievements.json`
参考：`README.md`（当前实现与基线）、`docs/equipment-reforge.md`（装备规则）、`docs/endless-mode.md`（无尽层数口径）、`docs/rebirth.md`（已实现转生与 v10 预留）。文中代码位置以类/方法名为锚点，行号仅为复审时参考。

## 0. 一句话

成就系统是一组**按存档槽独立、跨局保存、给展示奖励与一次性少量金币**的长期目标：把击杀、掉落、章节、装备、技能与无尽进度变成一张可查的清单，不提供任何战斗属性。

## 1. 为什么做

### 1.1 补齐长期记录，不替代已有长线玩法

README 路线图已完成 11/12 项：转生、无尽与成就已实现，只剩表现层。成就不是唯一的长期目标，而是给章节、装备、技能、巅峰与无尽提供统一的可查看里程碑；不新增战斗公式，也不宣称无尽存在固定的可打满上限。

### 1.2 长期统计与本局战绩分开

当前 `GameController` 的八个计数器（TotalKills / TotalDrops / TotalEquipped / TotalSold / TotalGoldEarned / TotalDeaths / EliteKills / BossKills）只在内存，`ResetSessionState()` 会清零。转生已通过该方法清理本世临时战绩。

新增按槽持久化的 `Player.stats` 作为成就累计统计，**不直接把现有 HUD 的本局计数器改成跨世计数器**。同一业务结算点分别更新本局与累计数据，成就只读取累计数据；阵亡、暂停、普通复活均不清累计，转生也保留累计。旧档无法恢复的历史不猜测。

### 1.3 新手需要一个「下一步做什么」的清单

新手前 30 分钟最容易迷路：点开始、看自动打、不知道该关注什么。现有新手引导讲的是界面位置，不是目标。成就用最轻的方式给出一串短目标：第一次击杀、第一件掉落、第一次换装、第一次强化、第一次洗练、通关第一章……每一条都在教一个系统。

### 1.4 它不解决什么

- **不解决终局金币软出口**。成就提供贯穿新手至首通的一次性金币（名义总量 43,400），其早期节奏影响仍须测试；成就奖励是金币来源，不是金币出口。金币出口仍应交给洗练/强化/已实现的转生或未来商店。
- **不替代转生**。转生是重置换长期倍率，成就是记录，不是重置。
- **不提供战斗属性**。不做「成就点 +攻击」这类设计，否则会推翻现有 P200 / 无尽平衡基线。

## 2. 目标与非目标

### 2.1 目标

- 首发 **42 个成就**，分 6 类，合计 **805 成就点**。
- 成就按**存档槽**独立保存；转生保留，删档清除。
- 解锁由事件驱动，不每帧轮询；旧档按可证明状态**静默补发**，不弹历史 toast、不补金币。
- 左栏有入口；面板可分类、分页、看进度；解锁有 toast。
- 奖励只有成就点、称号文本、一次性金币；不直接改战斗属性，但金币会间接影响养成。
- 存档从 v9 升到 v10，旧档可读；升级过程可重复执行且不重复发奖。

### 2.2 非目标（v1 不做）

- 账号级 / 跨存档成就、云同步、Steam 或平台成就。
- 每日 / 每周 / 赛季成就。
- 成就商店、成就点换属性 / 装备 / 掉落率。
- 隐藏成就的完整玩法。v1 的 42 条全部可见；schema 预留 `hidden` 字段，但首发不放隐藏条目，避免「完成度」被不可见条目卡住。
- 旧档累计击杀 / 掉落 / 卖出的追溯。旧档这些计数器从 0 开始，只补等级、章节、装备、技能、无尽这类**能从当前状态或已保存的转生历史证明**的成就（见 §5.3）。
- 多语言。v1 只做中文；名称与描述放 JSON，后续可换表。
- 称号佩戴 / 角色栏展示。v1 称号只在成就面板与 toast 里显示；佩戴入口放 v1.1，避免为了装饰再改角色栏布局。

## 3. 玩家看到什么

### 3.1 入口

左栏标题行：标题「角色属性」实际只占约 90px（`GameUI.cs:386` 的注释已说明），设置按钮在 220，存档在 300。中间 110~220 正好放一个 72px 按钮：

| 控件 | 位置 | 尺寸 | 文案 |
|---|---|---|---|
| 成就按钮 `_achievementsButton` | 140, 15 | 72×24 | `🏆 成就` |

设置（220,15,72×24）与存档（300,15,88×24）不动。字体 13，底色沿用 `GameConfig.BtnHeader`。

### 3.2 面板

面板 808×560、位置 236,80（1280×720 下居中）：比技能面板高 16px，给底部提示留出空间（`GameUI.Panels.cs` 的 `BuildSkillPanel` 同一套坐标与构建方式），整局冻结，和选关 / 技能管理及选择 / 存档 / 强化 / 洗练 / 设置 / 引导 / 对比 / 无尽及结算 / 转生互斥。

```
┌──────────────────────────────────────────────────────────────┐
│ 成就                                    已完成 12/42 · 180 点   [关闭] │
│ [全部] [新手] [进程] [战斗] [装备] [技能] [无尽]              │
│ ┌──────────────────────────┐ ┌──────────────────────────┐   │
│ │▌初出茅庐          +5  ✓  │ │▌百人斩            +5     │   │
│ │ 累计击杀 1 只怪物  1/1    │ │ 累计击杀 100 只   37/100 │   │
│ └──────────────────────────┘ └──────────────────────────┘   │
│  ... 2 列 × 8 行 = 16 条/页 ...                              │
│ [只看未完成]                              [上一页] 1/3 [下一页] │
│ 成就点只用于展示，不提供战斗属性。                              │
└──────────────────────────────────────────────────────────────┘
```

- 标题行：标题 20,12；进度文本“已完成 x/42 · 成就点 n”放在标题右侧或第二行；关闭按钮 698,12,90×30。
- 分类页签：7 个（全部 / 新手 / 进程 / 战斗 / 装备 / 技能 / 无尽），x=20+i×104，y=78，96×28；7 个占 720 宽，面板内右边界 x=740。
- 列表：2 列 × 8 行，每行 376×44，列间距 8，行间距 5；第一行 y=112，第二列 x=404。
- 行内容：左侧 4px 级别色条（青铜/白银/黄金/铂金）、名称（14px 粗体）、描述（11px 灰）、右侧进度（`37/100` 或 `已完成`）与点数（`+10`）。已完成整行压暗并显示 `✓`。
- 有称号的条目在描述后追加「称号：X」（10px 金色，与描述同一行）；超长时优先截断描述、保留称号。
- 底部：左侧“只看未完成”开关 20,508,120×28；右侧上一页 / 页码 / 下一页，翻页按钮 80×28。42 条 = 3 页（16/16/10）。
- 描述只排一行，超长用省略号或手工截断，不换行撑破行高。
- 面板打开时每 0.5 秒刷新一次进度文本；列表结构只在分类/页码/完成状态变化时重建。

### 3.3 解锁 toast

- 右侧日志区内，root 坐标 600,430，500×56；短暂遮住部分日志，不遮怪物标题、血条或主要按钮。所有子 Image/Text 的 `raycastTarget=false`，不拦点击。
- 内容：`🏆 成就解锁 · 初出茅庐  +5 成就点`；有金币时第二行 `+200 金币`，有称号时第二行追加 `称号「X」`（无金币则只显示称号）。
- 2.5 秒后淡出；多条同时解锁时排队，一条一条显示，不合并、不刷屏。
- 读档补发不弹 toast；只有游戏进行中的新解锁才弹。
- 任一模态浮层或通关横幅可见时，暂停 toast 的显示与计时，恢复后续播；切档/重置清队列。这里的非阻塞只指不吞点击，不承诺完全不遮画面。

## 4. 规则

### 4.1 归属与生命周期

| 事件 | 成就与累计统计 | 临时状态 |
|---|---|---|
| 新建档 / ResetGame / 删除当前档后新建 | 新建空状态，0/42、0 点；这不是复活 | 清 toast、连续挑战、旧绑定 |
| 切档 / 读档 | 读目标槽，静默核对；不混入来源槽 | 清来源槽 toast 与连续挑战 |
| 阵亡 / 复活 / 暂停继续 | 全部保留；阵亡累计 +1 | 阵亡取消连续挑战，暂停不取消 |
| 无尽撤退 / 阵亡 | 保留累计及本档最高已清层 | 结束无尽局，不回滚已解锁 |
| 转生（已经实现） | 保留 stats、achievements；已解锁永久有效、奖励不重发 | 重置本世装备/技能/关卡及连续挑战；重新绑定或确认引用仍有效 |
| 删非当前档 | 只删除目标槽 | 不打断当前槽 |

未解锁的状态类目标看**同一世当前状态**（五件同时达标、技能持有量、110 关首通），不能跨世拼接。累计击杀/卖出等跨世累加；单件最高强化与最高词条质量用持久化历史最高值。已解锁条目即使转生或换装也显示「已完成」，不退成未完成。

### 4.2 结算与顺序

- 业务先完成状态修改、累计统计与连续挑战推进，再统一求值。击杀链最多在外层结算结束求值一次；不在每个嵌套 helper 里即时扫描或存盘。
- 仅实际成功动作计数：强化/洗练失败、资金不足、全锁拒绝、无效技能选择、只打开面板均不计。
- 求值本身只读指标，不加计数、不消耗 Unity 随机数。重复求值不会重复发奖；**解锁 ID 去重不等于业务事件去重**，统计必须在唯一的成功结算入口写入。
- 目录数组顺序决定显示与同批 toast 顺序。同一击杀可同时完成击杀、掉落、穿戴、等级、关卡等多条，必须按一批处理。
- 打开面板只读状态。加载走明确的 `LoadReconcile` 模式，绝不靠 `silent=true` 猜是否发钱；正常动作走 `Live`。

### 4.3 奖励与去重

| 奖励 | 规则 |
|---|---|
| 成就点 | 青铜 5 / 白银 10 / 黄金 20 / 铂金 40，只展示，不消费 |
| 金币 | 每个 ID 每槽至多一次；固定表值，不吃转生财运。完整新档首轮全部达成名义总额 43,400；旧档补发及余额封顶会令实际到账更少 |
| 称号 | 7 个：通关者、巅峰、踏破者、传说、不屈、博学、无尽；v1 仅面板/toast 展示 |
| 战斗属性 | 不直接加攻击、防御、生命、暴击、攻速、穿甲、掉率或经验倍率；金币仍可间接加速养成 |

金币核算：新手 600；进程 500 + 2,000 + 5,000 + 10,000 + 20,000 + 5,000 = 42,500；战斗 300；其余 0，合计 **43,400**。这是贯穿新手到首通的奖励，不能把第 9/11 章奖励称为「仅早期」。

**v1 不做手动领取，不存第二份 claimed 列表。** `unlocked` 表示该条目已经结算：Live 下金币已计入同一内存快照，LoadReconcile 下金币被明确豁免。未解锁 → 已结算是唯一状态转移。不能根据「有 unlocked、没 claimed」推断历史欠款，更不能读档自动补钱。

### 4.4 完成度与边界

- 正常表为 42/42、805/805；UI 分母由有效目录计算，42/805 是内容验收断言，不散落硬编码。
- 无隐藏成就，无全成就元成就。累计值可大于目标，进度显示夹至 target；已解锁优先显示完成。
- 未知成就 ID 去重后保留，但不计分、不占可见完成数；后来恢复对应定义时仍视为已结算，不补钱。
- 累计计数使用非负 long，增加前用差额检查饱和到 long.MaxValue，不先溢出再 Clamp。金币余额仍遵守现有 int 上限，toast 显示**实际到账**；封顶截掉的奖励不记欠账。
- 仅负责本地一致性与重复回调防护，不承诺防手改档、防恢复旧备份刷取，也不把成就点重算说成防作弊系统。

## 5. 数据与存档

### 5.1 v10 新增结构

以下是拟新增结构，不是已经存在的代码：

```csharp
[Serializable]
public class PlayerStats
{
    public long kills, drops, equipped, sold, goldEarned;
    public long deaths, eliteKills, bossKills;
    public long enhances, reforges, legendaryDrops, threeLockReforges;
    public int bestEnhanceLevel;       // 已穿戴装备的历史最高强化，0..15
    public float bestAffixQuality;    // 已穿戴装备的历史最高词条质量，0..1
    public int noDeathChapterMask;    // 第 N 章 bit N-1；仅 bit 8..10 有效
}

[Serializable]
public class AchievementState
{
    public int schemaVersion;         // 0=未初始化；初始化/修复完成才设为1
    public List<string> unlocked = new List<string>();
}

// PlayerData 持有；SaveData 中新增同名字段，保存时复制独立快照。
public PlayerStats stats;
public AchievementState achievements;
```

- 点数、完成数、运行时 HashSet、目录索引均派生，不重复保存；JSON 列表为持久化真相。
- 不保存 toast、UI 分类/页码、连续挑战进度。这些不属于跨会话历史；已完成挑战的 bit 保留。
- 历史最高值在穿戴成功、强化成功、洗练成功和加载初始化时更新；卸装/低质量洗练/转生不降低。五件同时达标的 min 指标仍读当前五槽，不用五个历史最高拼凑。
- `goldEarned` 记录实际入账：卖出、首通、无尽清层、新成就奖励、转生前待选折现；不含初始/转生起始金币，不因支出减少。首发无金币累计成就。
- `legendaryDrops` 只计真实掉落，在自动卖出前记录；旧档当前传说不伪造成一次历史掉落，直接供 legendary_seen 求值。

### 5.2 迁移顺序（旧版 → v10）

1. 保留现有 v0..v9 迁移和校验，尤其 v8 五槽与 v9 转生；读取原始 version 与字段是否缺失，不能提前覆盖它们。v10 写入版本与成就 schema 是不同概念。
2. 在业务初始化上下文中挂好非空 stats / achievements，禁止现场奖励、业务累计与中途存盘。先修 PlayerData / 装备，再 `EnsureEquipmentSlotStarters()`；该方法当前会写盘，接入时要允许延迟到初始化结束，避免只写 v10 却漏成就迁移。
3. 原始 version < 10 时做一次**可证明下界**初始化：当前五槽强化等级之和作为 enhances 下界、reforgeCount 之和作为 reforges 下界；历史最高取当前最大；其它事件累计为 0。只初始化缺失历史，不在每次加载重新累加。
4. 绑定系统后用 LoadReconcile 全表核对，补解锁不发钱、不弹 toast、不写战斗日志。v9 转生历史可证明的补发见 §5.3；新建空档不当旧档迁移。
5. 设置 schemaVersion=1，统一写 v10 快照一次。保存失败保留内存结果与待保存标记，有限频率重试；不得假称已经落盘，不在同一会话重跑累计种子步骤。
6. 后续每次有效 v10 读档仍可静默核对一次（修复漏标或新增定义），不是仅首次迁移才核对；不重复种累计、不追发金币。

### 5.3 旧档补发证据

| 可用证据 | 可补范围 | 不得推断 |
|---|---|---|
| 当前 level / paragonLevel | 等级、当前巅峰门槛 | 历史击杀、游戏时长 |
| v9 rebirthCount > 0 / rebirthBestParagon | 曾到 Lv100、P100、通关 11-10；历史巅峰按 max(当前, rebirthBestParagon) | 曾到 P1000 必须有最高巅峰证据；不能由一次转生推断 |
| 当前 clearedStages | 明确的 1-1、章节末、110 个合法唯一关卡 | 转生清空的其它首通集合；maxChapterUnlocked 不是首通证据 |
| 当前装备 | 品质、单件/同时五件、强化和洗练次数的可证明下界 | 被卖装备、旧传说掉落数、三锁操作次数 |
| 当前技能持有位与槽 | 持有数量、主动/三系覆盖、六槽装备 | 转生前丢失的技能集合或选择历史 |
| 本槽 endlessBestFloor | 无尽已清层门槛 | 全局纪录属于本槽、首死层等于已清层 |

无阵亡整章的历史不可证明，从零开始挑战。强化/洗练下界是有依据的例外，不能笼统宣传「所有累计从 0」。已转生档的历史通关按 chapter_clear 的最高章定义补至 11，但**不补 all_stages 或 clear_1_1**，因为通关允许章内选关。

### 5.4 修复与存盘失败

- null 结构/列表补齐，ID 去重、去空，未知 ID 保留；计数非负，最高强化夹 0..15，质量先排除 NaN/Infinity 再夹 0..1；挑战位只保留 bit 8..10。
- v10 缺字段可按当前状态静默修复，但不重建不可证明历史、不按疑似缺失标记补金币；错误需诊断日志。
- 原存档版本 >10 或成就 schemaVersion >1 不按已知语义覆盖。保留备份并拒绝对此槽自动降级写入；必须显式重置/恢复可读档后才能恢复写入。
- 当前 `SaveSystem.Save(PlayerData)` 是 void，捕获异常后仅警告；`SetString + PlayerPrefs.Save` **不是本设计可保证的原子事务**。落地需提供可观测成功/失败的保存入口，保存失败保留 dirty，正常退出/暂停/切档前刷写；切档或重新加载若刷写失败，应保持当前档并提示，不静默丢弃已结算奖励。
- 金币、stats、unlocked 必须在同一 JSON 快照一起写，避免程序层拆写。突然断电/存储损坏可能丢最近进度，不能承诺必然回退到上一次完整档；强恢复保证需单独设计双快照/恢复协议，本版不声称已经具备。

### 5.5 转生与本局计数

- 保留现有八个本局计数器与 ResetSessionState 清零行为；新增累计统计不受该方法清理。避免无关改变现有 HUD/探针的「本局」语义。
- PerformRebirth：先处理待选折现（含 sold / 实际 goldEarned），在重置前记录历史最高并统一求值/结算，再计算印记并重置本世。保留 stats / achievements，清临时挑战，最后把新一世与历史成就一起保存。
- 成就固定金币若在转生结算前解锁，按正常余额参加印记计算；不吃财运，后续世不再发同一 ID。当前文档的「转生保留累计」是 v10 实现要求，不是现有 v9 已具备的能力。
- ResetGame 使用 new PlayerData，是彻底重开；不要与阵亡复活混称「重开本局」。切档必须重新绑定目标 PlayerData，清旧 toast 和 UI 缓存，删其它槽不得清当前状态。

## 6. 成就清单（42 个 / 805 点）

级别与点数：青铜 5 / 白银 10 / 黄金 20 / 铂金 40。  
“指标”是通用求值器读取的字段，见 §7.2；目标均为“指标 ≥ target”。

### 6.1 新手（6 个 / 30 点）

| ID | 名称 | 指标 ≥ 目标 | 级别/点 | 奖励 |
|---|---|---|---|---|
| `first_kill` | 初出茅庐 | `kills` ≥ 1 | 青铜 5 | 200 金币 |
| `first_drop` | 第一件战利品 | `drops` ≥ 1 | 青铜 5 | 200 金币 |
| `first_equip` | 换上新装 | `equipped` ≥ 1 | 青铜 5 | 200 金币 |
| `first_death` | 交学费 | `deaths` ≥ 1 | 青铜 5 | — |
| `first_enhance` | 第一次强化 | `enhances` ≥ 1 | 青铜 5 | — |
| `first_reforge` | 第一次洗练 | `reforges` ≥ 1 | 青铜 5 | — |

### 6.2 进程（9 个 / 205 点）

| ID | 名称 | 指标 ≥ 目标 | 级别/点 | 奖励 |
|---|---|---|---|---|
| `clear_1_1` | 迈出第一步 | `first_stage_cleared` ≥ 1 | 青铜 5 | 500 金币 |
| `clear_ch3` | 小有所成 | `chapter_clear` ≥ 3 | 白银 10 | 2,000 金币 |
| `clear_ch6` | 渐入佳境 | `chapter_clear` ≥ 6 | 黄金 20 | 5,000 金币 |
| `clear_ch9` | 身经百战 | `chapter_clear` ≥ 9 | 黄金 20 | 10,000 金币 |
| `clear_ch11` | 通关 | `chapter_clear` ≥ 11 | 铂金 40 | 20,000 金币 + 称号「通关者」 |
| `level_100` | 满级 | `level` ≥ 100 | 黄金 20 | 5,000 金币 |
| `paragon_100` | 巅峰初探 | `paragon` ≥ 100 | 白银 10 | — |
| `paragon_1000` | 巅峰极限 | `paragon` ≥ 1000 | 铂金 40 | 称号「巅峰」 |
| `all_stages` | 全境踏破 | `cleared_stages` ≥ 110 | 铂金 40 | 称号「踏破者」 |

`chapter_clear` 取“已首通第 10 关的最高章号”。例如通关 11-10 后，目标 3/6/9/11 都满足；`all_stages` 另要求 110 个关卡全部首通，避免只打章末跳关。

### 6.3 战斗（8 个 / 145 点）

| ID | 名称 | 指标 ≥ 目标 | 级别/点 | 奖励 |
|---|---|---|---|---|
| `kills_100` | 百人斩 | `kills` ≥ 100 | 青铜 5 | 300 金币 |
| `kills_10000` | 万夫莫开 | `kills` ≥ 10,000 | 黄金 20 | — |
| `kills_100000` | 杀戮机器 | `kills` ≥ 100,000 | 铂金 40 | — |
| `elite_100` | 精英猎手 | `elite_kills` ≥ 100 | 白银 10 | — |
| `elite_1000` | 精英终结者 | `elite_kills` ≥ 1,000 | 黄金 20 | — |
| `boss_50` | 关底常客 | `boss_kills` ≥ 50 | 白银 10 | — |
| `boss_500` | BOSS 克星 | `boss_kills` ≥ 500 | 黄金 20 | — |
| `no_death_chapter` | 无阵亡通关 | `no_death_chapter` ≥ 1 | 黄金 20 | 称号「不屈」 |

`no_death_chapter` 只要求无阵亡，不要求零受伤，因此名称改为「无阵亡通关」、称号改为「不屈」，ID 不变。

- 临时状态为 attemptChapter / nextRequiredStage / active；只有从第 9~11 章第 1 关的**完整关卡起点**进入才启动，nextRequiredStage=1。普通自然跨章进入或手动重新开始第 1 关均可；前 8 章不参与。
- 在 `ResolveStageProgress` 确认本关已清、调用 `ResolveStageClear` 之前，用本次 chapterIndex / stageIndex 核对：章号匹配且 stage==nextRequiredStage 才推进，必须依次完整清 1..10。必须先于 `SuppressFinalStageFarmKill()` 的提前返回记录挑战进度，否则已首通 11-10 的玩家再次完成连续挑战会漏记。该挑战钩子不补发首通金币、不恢复终局刷怪横幅或额外等待；重复/乱序清关不能推进资格，遇到不匹配按失效处理。
- 有效 `SelectStage` 请求即取消旧尝试，包含重选当前关重置进度；若确实从目标章第 1 关重新开始，可开启新尝试。仅打开/取消选关面板不算跳关。手动 9-1→9-10 或 9-1→9-5 都不能完成。
- 阵亡、离章、进入无尽、转生、ResetGame、读档/切档都取消未完成尝试；普通暂停、模态面板、技能选择、强化/换装不取消。无阵亡不禁止正常补血或养成。
- 连续清掉第 10 关时才设置 noDeathChapterMask（第 9/10/11 章 bit 8/9/10），然后结束尝试；此已完成证据持久保存。
- 不持久化进行中的挑战；重启后即使位于第 1 关中途也不自动获得资格，须重新选第 1 关或自然进入新的合格章。UI 明示「需在同一会话连续通关；中途读档需重试」。

### 6.4 装备（9 个 / 200 点）

| ID | 名称 | 指标 ≥ 目标 | 级别/点 | 奖励 |
|---|---|---|---|---|
| `first_legendary` | 第一件传说 | `legendary_seen` ≥ 1 | 白银 10 | — |
| `epic_4` | 史诗小队 | `epic_slots` ≥ 4 | 白银 10 | — |
| `five_legendary` | 五传说不朽 | `legendary_slots` ≥ 5 | 铂金 40 | 称号「传说」 |
| `enhance_15` | 极限强化 | `best_enhance` ≥ 15 | 黄金 20 | — |
| `all_enhance_15` | 五件+15 | `min_enhance` ≥ 15 | 铂金 40 | — |
| `reforge_80` | 精益求精 | `best_reforge_quality` ≥ 80 | 黄金 20 | — |
| `all_reforge_80` | 五件精工 | `min_reforge_quality` ≥ 80 | 铂金 40 | — |
| `lock_three` | 三锁定音 | `three_lock_reforges` ≥ 1 | 白银 10 | — |
| `sell_1000` | 废品回收 | `sold` ≥ 1,000 | 白银 10 | — |

口径：

- `legendary_seen` = `max(legendaryDrops, legendary_slots)`；掉落过传说但后来卖掉也算。
- `epic_slots` / `legendary_slots` 只数**当前穿着的五槽**；空槽不计，品质达到或超过即可（传说算史诗）。
- `best_enhance` = `stats.bestEnhanceLevel`（已穿戴历史最高）；`min_enhance` = 五槽都非空时的最小值，有空槽则为 0。
- `best_reforge_quality` = `stats.bestAffixQuality * 100`（已穿戴历史最高）；`min_reforge_quality` = 当前五槽质量最小值 ×100，任一空槽为 0。沿用指标 ID，但两条都是**词条质量成就**，天然掉落也可完成，不要求发生洗练、不限定品质或等级；不得宣传为必然终局目标。
- 判定用未舍入质量 ≥0.8；面板质量百分比向下截断到 0.1%，未达标不得显示 80.0%。现有洗练 UI 的整数四舍五入会把 79.6% 显示为 80%，实施时一并对齐质量展示规则，不改装备数值。
- `three_lock_reforges` 仅在实际成功洗练且有效锁定数为 3 时 +1，不能只数传入 mask。当前仅四词条大件传说能三锁留一条洗练；小件最多两词条，最多锁一条，锁满会被拒绝。
- `epic_4` 只要求四个槽位达到史诗：头盔（3-10）与鞋（6-10）解锁后即可完成，不等到饰品；这样它和终局的 `five_legendary`（五件全传说）拉开节奏。
- `sell_1000` 统计每件实际售出：手动 SellPending、自动折价分支、转生前待选折现均计一次；每条实际路径只在唯一结算点更新 sold 与实际到账 goldEarned，不能按函数名推定只有两种路径。

### 6.5 技能（4 个 / 90 点）

| ID | 名称 | 指标 ≥ 目标 | 级别/点 | 奖励 |
|---|---|---|---|---|
| `skills_6` | 技能成型 | `equipped_skills` ≥ 6 | 白银 10 | — |
| `skills_15` | 全技能 | `owned_skills` ≥ 15 | 铂金 40 | 称号「博学」 |
| `skill_triple_4` | 三系兼修 | `owned_each_kind_min` ≥ 4 | 黄金 20 | — |
| `all_active` | 主动全收集 | `owned_active` ≥ 4 | 黄金 20 | — |

- `equipped_skills` 数修复后六槽中有效、已拥有且不重复的技能数；`owned_skills` 仅统计 SkillCatalog 已定义 ID 对应的持有位，忽略越界位。
- `owned_active` 只数主动技能（SkillId 0~3，共 4 个）。
- `owned_each_kind_min` = min(主动已收集数, 被动已收集数, 触发已收集数)；当前主动 4 / 被动 7 / 触发 4，三类各 4 个需要第 12~15 次选择才可能完成；可能与 `all_active` 或 `skills_15` 同批解锁，不保证三个节点错开。
- **不单列“选择 N 次”**：`ChooseSkill` 每次成功都必定 `GrantSkill` 一个未拥有的技能，所以“选择 15 次”与“集齐 15 个”会在同一次解锁，做成两条只是重复。三系兼修要求类别覆盖，和全收集、全主动都不同。
- 15 个技能全部获得需要 15 次选择，对应巅峰约 150 级；这是中期目标，不是终局目标。

### 6.6 无尽（6 个 / 135 点）

| ID | 名称 | 指标 ≥ 目标 | 级别/点 | 奖励 |
|---|---|---|---|---|
| `endless_25` | 初入无尽 | `endless_best` ≥ 25 | 青铜 5 | — |
| `endless_50` | 深入无尽 | `endless_best` ≥ 50 | 白银 10 | — |
| `endless_100` | 百层 | `endless_best` ≥ 100 | 黄金 20 | — |
| `endless_150` | 一百五十层 | `endless_best` ≥ 150 | 黄金 20 | — |
| `endless_200` | 两百层 | `endless_best` ≥ 200 | 铂金 40 | — |
| `endless_250` | 无尽之巅 | `endless_best` ≥ 250 | 铂金 40 | 称号「无尽」 |

- 只认“已清完”的层：死在第 N 层不算第 N 层（与 `PlayerData.endlessBestFloor` 现有口径一致）。
- 保留 250 作为首发目标，不把它称为当前天花板。`docs/endless-mode.md` §12.5.3 已记录理想模板中位首死 290、随机五槽 3 种子中位 242；但理想模板的小件四词条与当前掉落最多两条存在口径疑点，不能直接用它证明正常配装可达性。上线前用合法五槽装备、明确转生状态和固定种子复测，再决定是否新增更高目标；不要追涨已有成就门槛。
- 不在 v1 做“单局无死 N 层”：无尽局内死亡即结束，这个条件与“到达 N 层”等价，没有额外信息量。

### 6.7 点数汇总

| 类别 | 条数 | 点数 |
|---|---:|---:|
| 新手 | 6 | 30 |
| 进程 | 9 | 205 |
| 战斗 | 8 | 145 |
| 装备 | 9 | 200 |
| 技能 | 4 | 90 |
| 无尽 | 6 | 135 |
| **合计** | **42** | **805** |

## 7. 技术设计

### 7.1 模块划分

| 文件/区域 | 职责 |
|---|---|
| Models.PlayerStats / Models.Achievements | §5 数据结构、定义、JSON 根；命名空间沿用项目 |
| AchievementCatalog | 加载 Resources/achievements.json、校验、ID 索引；保持数组顺序 |
| AchievementSystem | 绑定当前 Player、只读求值、批次结算、内存 toast 队列 |
| SaveSystem | v10 快照/迁移/修复、可观测保存失败；不吞掉失败状态 |
| GameController / Endless / Rebirth | 唯一业务统计入口、挑战状态机、外层批次边界、生命周期接线 |
| GameUI.Achievements | 面板、过滤/分页、进度和 toast |

成就条目是内容表，放 Resources 独立 JSON；不把 42 条参数塞进 GameConfig。级别颜色可集中在 UI 模块，不新增战斗参数。

### 7.2 指标适配器

计数指标用 long 与整数 target 精确比较；词条质量用有限 float 的 0..1 值与 target/100f 比较。不要把 long 累计转 float 后统一比较，也不要把 79.99% 四舍五入成达标。

| 指标 | 来源 |
|---|---|
| kills / drops / equipped / sold / gold_earned / deaths / elite_kills / boss_kills | stats 对应非负 long |
| enhances / reforges / legendary_drops / three_lock_reforges | stats 对应非负 long |
| level / paragon | 当前 level / paragonLevel；LoadReconcile 可按 §5.3 引入已验证转生证据 |
| cleared_stages | 当前 clearedStages 中合法 StageKey 的去重数量，只认章 1..11、关 1..10，不直接信未过滤的 Count |
| first_stage_cleared | 当前合法集合含 StageKey(1,1)，返回 0/1 |
| chapter_clear | 当前合法集合含章末第 10 关的最高章号；迁移历史证据见 §5.3，不能用 maxChapterUnlocked 替代 |
| epic_slots / legendary_slots | 当前五槽有效装备中品质 ≥Epic / ==Legendary 的数量 |
| legendary_seen | max(stats.legendaryDrops, legendary_slots) |
| best_enhance / min_enhance | stats.bestEnhanceLevel / 当前五槽最小强化（任一空槽为 0） |
| best_reforge_quality / min_reforge_quality | stats.bestAffixQuality / 当前五槽最小 AffixQuality；适配成百分比进度，原始值用于比较 |
| equipped_skills / owned_skills | 修复后的有效已装备技能数 / 有效目录 ID 的持有位数 |
| owned_active / owned_each_kind_min | 按 SkillCatalog 分类统计当前持有数；主动数 / 三类最小值 |
| no_death_chapter | (stats.noDeathChapterMask & 0x700) != 0 ? 1 : 0 |
| endless_best | 本槽 Player.endlessBestFloor，非全局纪录 |

首次迁移引入的历史证据只补 unlocked，不伪造当前等级、关卡或技能。42 条事件驱动全表扫描足够；Update 不轮询解锁，UI 定时刷新只读取结果。

### 7.3 事件与批次边界

| 成功结算点 | 先完成的更新 | 求值时机 |
|---|---|---|
| HandleMonsterKilled / HandleEndlessMonsterKilled；共享 ResolveDrops | kills / eliteKills / bossKills；实际每件 drops、传奇 legendaryDrops；经验等级和装备处理 | 最外层击杀结算结束 |
| 实际穿戴、自动穿戴 | equipped；历史强化/质量峰值；不计新档/迁移赠送初装 | 外层动作结束 |
| 手动卖出、自动折价、转生待选折现 | sold 与实际到账 goldEarned；无出售就不计 | 外层动作结束，转生重置前 |
| 普通/无尽阵亡 | deaths；取消连续挑战 | 死亡状态完整更新后 |
| EnhanceEquipment / ReforgeEquipment | 成功操作次数、历史峰值、实际三锁成功次数 | 返回成功后；失败不计 |
| ChooseSkill | GrantSkill **及 TryAutoEquip 都完成** | 完整选择结算后，不能仅紧跟 GrantSkill |
| EquipSkill / UnequipSkill | 实际槽位变化 | 成功后核对，手动填满六槽不可漏 |
| ResolveStageProgress / ResolveStageClear / Player.AdvanceStage | 本关已清时先记录连续挑战，再按既有流程更新首通集合与奖励 | 挑战钩子先于 SuppressFinalStageFarmKill；成就统一在外层击杀批次末尾求值 |
| SelectStage / StartEndlessRun / ResetGame / SaveSystem.Load / PerformRebirth | 按 §6.3 启动或取消挑战，按 §4.1 清理临时状态 | 生命周期规则优先，不能把加载当 Live |
| ResolveEndlessFloorClear | 本槽最高已清层、实际奖励 | 更新 best 与奖励后，外层无尽击杀批次结束时统一求值 |
| 其它独立升级/金币来源 | 等级/巅峰；按实际到账统计 | 外层动作末尾 |

嵌套 helper 只标记 metricsDirty，由最外层统一 EvaluateBatch；保留八个旧本局计数器并在相同业务点另写累计，不能重复累加。普通卖出/首通/无尽等奖励仍调用现有 Player.AddGold（含财运，返回 actual）；转生前待选折现保留既有不吃财运的规则，以余额前后差记实际入账；成就新增明确命名的固定奖励入口，绕过财运但复用余额封顶规则。该入口是拟新增，需同步修订 AddGold 的「所有正向来源」注释与转生文档的例外说明。

#### 已核对的统计入口

以下路径相对项目根目录，行号为本次复审时位置；以方法名和成功分支为准。

| 业务 | 真实入口 | 计数约束 |
|---|---|---|
| 战役 / 无尽击杀 | HelloWorldProject/Assets/Scripts/GameController.cs:1259 的 HandleMonsterKilled；HelloWorldProject/Assets/Scripts/GameController.Endless.cs:166 的 HandleEndlessMonsterKilled | 两分支各自在实际击杀后记录；共享 GrantKillReward 不再重复计击杀 |
| 手动卖待选 | HelloWorldProject/Assets/Scripts/GameController.cs:606 的 SellPending | 成功售出才计 sold；金币取 AddGold 返回值 |
| 新掉落直接出售 | HelloWorldProject/Assets/Scripts/GameController.cs:1568-1583 的 ResolveDrops 出售分支 | 计新掉落的这件，不把进待选误记售出 |
| 旧待选被顶替 | HelloWorldProject/Assets/Scripts/GameController.cs:1632-1641 的 TryAutoEquip(Equipment, out bool) | 只计被顶替的旧件；新件仍是待选，不算穿戴 |
| 转生待选折现 | HelloWorldProject/Assets/Scripts/GameController.Rebirth.cs:139-152 的 PerformRebirth | 每件待选 sold+1；不吃财运，goldEarned 按封顶后余额差，不照抄当前原始售价累计 |
| 手动 / 自动换装 | HelloWorldProject/Assets/Scripts/GameController.cs:590 的 EquipPending；HelloWorldProject/Assets/Scripts/GameController.cs:1554-1559 的 ResolveDrops 成功穿戴分支 | 各计一次；不要同时在 TryAutoEquip 或 WearEquipment 再计；赠送初装和仅进待选均不计 |
| 无尽清层 | HelloWorldProject/Assets/Scripts/GameController.Endless.cs:200 的 ResolveEndlessFloorClear | 更新 endlessBestFloor 和实际奖励后才有新清层证据；任意击杀不能提前增加层数 |

可抽取拟新增 RecordSold / RecordEquipped 统一更新累计与标记 dirty，但不能改变上述成功条件，也不能在调用方和 helper 两头计数。装备 TryAutoEquip 与技能 TryAutoEquip 是不同重载，接线不能混淆。

现有 ResolveStageClear、EnsureEquipmentSlotStarters、ResolveEndlessFloorClear 含直接保存调用；接入批次后必须把这些内部保存请求延迟至最外层结算完成，不能在新纪录已写入而成就尚未结算时提前落盘。迁移后的写回同样由加载协调流程显式执行，当前 SaveSystem.Load 返回成功本身不代表已保存 v10。

### 7.4 批次结算（设计伪代码）

```csharp
enum EvaluationMode { Live, LoadReconcile }

void EvaluateBatch(EvaluationMode mode) // 不提供容易误发钱的默认参数
{
    var matched = CollectSatisfiedUnsettledInCatalogOrder(mode);
    if (matched.Count == 0) return;

    // Unity 主线程内一次完成；验证和计算先于修改，不在中途保存或回调求值。
    var plan = BuildValidatedSettlementPlan(matched, mode);
    ApplyPlanToPlayer(plan); // unlocked、实际金币、goldEarned 一起更新内存
    MarkSaveDirty();
    if (mode == EvaluationMode.Live) EnqueueToasts(plan);
}
```

- helper 为拟实现接口，不是现有方法。Collect 只读；同批 snapshot 中固定目录顺序。首发没有金币累计成就，不需奖励递归触发下一轮。
- BuildPlan 在 long 中核算固定奖励与余额可用空间，为每条计算 actual；LoadReconcile 的 actual 一律为 0、不生成 toast；点数从目录与 unlocked 派生。
- ApplyPlan 同时维护去重 HashSet 与持久化列表；不得引入 claimed/points 双份持久账。验证失败不部分修改内存；磁盘写入失败处理见 §5.4，不能称为原子磁盘事务。
- 批次结束再统一请求保存一次，而非每条成就一次。普通统计也标 dirty，即使没有新成就；纳入现有自动存档、暂停/退出/切档刷写。初始化和转生期间阻止内部 helper 提前写半成品快照。
- 保存重试只重写当前快照，不再执行金币结算；同一会话多次 Notify/重复回调不能重发。进程崩溃的持久性边界与整个存档一致，不另作零损失承诺。

### 7.5 加载与绑定

- 启动/切档/彻底重开绑定当前 Player；不在 ResetSessionState 里新建或清空累计状态。转生保留历史对象，清 toast/挑战和 UI 缓存。
- 加载严格执行 §5.2；EnsureEquipmentSlotStarters 后再求值，所有加载路径显式传 LoadReconcile。新档初装生成不算掉落/穿戴事件，预期 0/42。
- 目录仅加载一次；失败记录错误，游戏可继续但面板显示「成就配置不可用」，不要显示误导性的 0/0 已完成。关闭成就发奖；不覆盖已存 ID。

### 7.6 配置校验

ID 非空唯一；category/tier/metric 属于白名单；target 有限且 >0（计数目标须整数）；points 与 tier 对应 5/10/20/40；gold 为非负 int；name/desc 非空；v1 hidden=false。正常表断言 42 条、805 点、43,400 金币、7 个称号。

重复 ID 属于目录致命歧义，禁用本次目录并报错；单条字段非法可以跳过并展示配置错误，分母按有效目录计算，不静默伪装成完整 42 条。有效定义按 JSON 数组稳定顺序，不另做级别重排。

## 8. 配置格式

`Assets/Resources/achievements.json`：

```json
{
  "version": 1,
  "achievements": [
    {
      "id": "first_kill",
      "category": "starter",
      "tier": "bronze",
      "name": "初出茅庐",
      "desc": "累计击杀 1 只怪物。",
      "metric": "kills",
      "target": 1,
      "points": 5,
      "gold": 200,
      "title": "",
      "hidden": false
    },
    {
      "id": "clear_ch11",
      "category": "progression",
      "tier": "platinum",
      "name": "通关",
      "desc": "首通 11-10，完成当前全部章节。",
      "metric": "chapter_clear",
      "target": 11,
      "points": 40,
      "gold": 20000,
      "title": "通关者",
      "hidden": false
    }
  ]
}
```

- `hidden` 只是预留；v1 全为 false。
- `title` 为空表示不给称号。
- 数组顺序就是面板顺序；同类别内按数组顺序排。
- 不在 JSON 里写“当前进度”；进度是运行时指标。

## 9. 平衡与节奏

### 9.1 直接属性与间接经济分开验收

不修改战斗公式，也不消耗战斗/掉落 RNG。**相同存档、装备、技能、转生状态和随机种子**的纯战斗探针应保持结果；但自然养成模拟可能因成就金币更早强化/洗练而变化，不能承诺「零影响」。保存方式变化还需覆盖转生和无尽回归。

### 9.2 金币影响

奖励表总额 43,400，600 来自新手、42,500 来自进程、300 来自百杀。不能用终局百万余额占比证明新手阶段无影响。首次击杀/掉落/换装各 200，1-1 首通 500；开发后测这些奖励是否提前关键强化、首次洗练与首通时间，并观察转生前奖励计入余额对印记的影响。

旧档 LoadReconcile 不补历史金币，完整获得表中总额仅适用于从新档正常逐条解锁且未被余额封顶的情况。

### 9.3 目标分布与待测节奏

| 目标组 | 当前可证明的节点 | 待测内容 |
|---|---|---|
| 新手事件、1-1 | 首次发生即解锁 | 早期奖励是否过密、toast 是否挤占日志 |
| 章节、等级、巅峰 | 由关卡/等级门槛决定 | 正常玩家与加速模式实际耗时，不能凭门槛标 10~20 小时 |
| 三系 / 全技能 | 第 12~15 次选择；15 技能约 P150 | 允许同批解锁，避免错误承诺严格错开 |
| 质量 80% | 天然装备也可能达成，与品质/词条数有关 | 五槽解锁前不可全套，解锁后不保证属于终局 |
| P1000 / 五件+15 / 无尽250 | 长线目标 | 合法配装、各转生阶段、固定种子样本验证；不给未经测试的小时数 |

### 9.4 为什么不用 100% 质量

AffixQuality 是实际存在副词条区间百分位的平均，并消除等级与小件缩放。100% 要求各条极值，不适合必做目标；80% 相对可达，但**并不必然需要洗练**，单词条天然普通装也可达。v1 保留现有门槛和 ID，将它们定义为装备质量里程碑；洗练玩法由首次洗练和三锁条目覆盖。未来如要追加高品质/高等级门槛，应新增条目而非偷偷更改已发出的成就。

## 10. UI 细节与坐标

### 10.1 入口按钮

```
_achievementsButton = CreateButton(panel, "AchievementsButton", "🏆 成就", 140, 15, 72, 24,
    GameConfig.BtnHeader, out _achievementsButtonLabel);
_achievementsButtonLabel.fontSize = 13;
_achievementsButton.onClick.AddListener(OnAchievementsClicked);
```

- 不改设置/存档按钮位置。
- 标题“角色属性”仍是 16,10；它实际只占左边约 90px，不会和 140 的按钮重叠。

### 10.2 面板构建

`BuildAchievementsPanel(root)` 放在 `BuildReforgePanel` 之后、`BuildBanner` 之前；`BuildAchievementToast(root)` 放在 `BuildBanner` 之后（toast 最上层）。

- `CreatePanel(root, "AchievementsPanel", 236, 80, 808, 560, OverlayBg, true)`
- 标题 20,12,360×30，字号 20。
- 进度 20,44,500×22，字号 13，颜色 `TextInfo`。
- 关闭 698,12,90×30。
- 页签 7 个：`20 + i*104, 78, 96, 28`；选中用 `BtnPrimary`，未选用 `BtnHeader`。
- 列表容器相对面板为 20,112,768×388；行作为容器子物体，每行 376×44，**本地**列 x=0 / 384、行 y=row*49。换算到面板为 x=20/404、y=112+row*49；不要重复加容器偏移。第八行到 y=499，底部控件自 y=508 起。
- 行底衬用 `CreateButton` 或 `CreatePanel` + 透明按钮；点击行不要求有动作，v1 只展示，不做详情弹窗。
- 左侧级别条 4×44，颜色：青铜 `#B07A4A` / 白银 `#B8C2CC` / 黄金 `#D9A441` / 铂金 `#7FD4E8`（具体取现有色或在成就 UI 模块集中定义四个常量；不要硬编码到每个行里）。
- 底部：过滤 20,508,120×28；上一页 560,508,80×28；页码 648,508,60×28；下一页 712,508,76×28。
- 提示 20,536,768×20，字号 11，灰色：“成就点只用于展示，不提供战斗属性。”

### 10.3 刷新

- `_achievementsPanel` 字段 + `_achievementsPanelWasOpen` + `_achievementsDirty`。
- `GameController` 加 `AchievementsOpen` / `ToggleAchievementsPanel()` / `CloseAchievementsPanel()`：打开前判断 `ChoicePending`，不能关闭正在等待的技能选择后绕过选择；再 `CloseAllPanels()`。该方法加入 `AchievementsOpen = false`，所有既有面板打开路径保持互斥；切档/重置/转生关闭。
- **冻结不是遮罩的副作用**：必须在 `GameController.Update()` 现有面板 OR/return 名单（当前 208~210 行）加入 `AchievementsOpen`，且位于战斗/恢复/商店 Tick 前；不阻止 AutoSaveTick。
- `RefreshHud.modalOpen` 加 `IsOpen(_achievementsPanel)`，保持现有技能管理和技能选择遮罩判断；显示、遮罩、输入门禁、游戏冻结四处一起验收。
- `RefreshPanels` 里调用 `RefreshAchievementsPanel()`，位置在 `RefreshSettingsPanel` / `RefreshComparePanel` 附近。
- 打开时重置页码、重建行；之后每 0.5 秒只读刷新。过滤后将页码夹回有效范围；无条目显示空态，翻页均禁用，不出现 1/0。
- 切换页签/翻页/过滤：`_achievementsDirty = true`，下一帧重建。
- 解锁时：若面板打开，`_achievementsDirty = true`；toast 独立刷新。

### 10.4 Toast

- BuildAchievementToast(root)：500×56，root 坐标 600,430，落在右侧日志区内；不采用会遮住怪物标题/血条的 390,20。允许短暂覆盖日志，实机截图验证后再微调。
- 所有子 Image/Text 的 raycastTarget=false；无透明大遮罩拦截操作。
- 队列随当前槽绑定；每条显示 2.5 秒后淡出 0.2 秒，使用 unscaledDeltaTime，不随战斗倍速加快。
- 任一模态面板或通关横幅可见则隐藏当前 toast、暂停计时，恢复后继续；切档/重置/转生清除旧提示，持久成就不受影响。
- 展示每条本次实际到账金币，不用 def.gold 冒充到账；封顶未到账时标明「余额已满，金币未到账」，不留下待领债务。

## 11. 验收清单

以下为**实施后的验收要求**，本次文档审查未执行 Unity 编译、运行或数值探针。

### 11.1 功能与统计

- [x] 目录 42 条、805 点、43,400 名义金币、7 称号；新档 0/42、0 点，初始装备不误计掉落/穿戴（RunCore 目录段 + §16.5 新档 0/42）。
- [x] 首击杀/掉落/穿戴、死亡、强化/洗练、百杀、各章节和等级逐条达成；失败操作不计；同一击杀多条解锁按数组顺序只结算一次（RunCore 42 条指标边界 + Play 实战计数见 §16.5）。
- [x] 本局计数与累计独立：转生清本局不清累计；阵亡/复活不清累计；彻底重开清空；普通/无尽全部事件路径计数一次（RunCore 转生/统计段 + §15.2 真实转生回归）。
- [x] 手卖、自动折价、转生待选折现均记 sold；普通收入按财运后实际到账累计；固定成就金币在财运>1 时仍不放大，接近 int.MaxValue 时不溢出，重试存盘不再次发钱（RunCore 金币段：财运例外、`int.MaxValue - 50` 封顶、幂等重存）。
- [x] ChooseSkill 自动装满及手动 EquipSkill 装满六槽都即时解锁；主动、三系、全技能允许同批完成；未知位/重复槽不多计（RunCore 指标边界含六槽/三系/全技能；Play 技能选择路径见 §16.3）。
- [x] 五槽品质、历史单件峰值、当前五件同时达标、三锁、千次卖出逐条验证；天然高质量也计；卸装或转生不降低历史峰值，跨世不能拼五件套/技能/110关（RunCore 指标边界 + 质量边界）。
- [x] 词条质量 79.49/79.50/79.99/80.00% 边界：前三者不解锁且不显示达标，80.00% 解锁；空槽 min=0；小件两词条不能做三锁（RunCore 质量边界段）。
- [x] 合法 StageKey 去重：重复/非法关卡不补齐 all_stages；章内跳关允许 chapter_clear 达最高章，但不误发 110 关全通（RunCore 无阵亡段 + v8 迁移 62 关回填）。
- [x] 无阵亡：9/10/11章完整自然连续1..10成功；直接章末、1→5/10跳关、重选当前关重置进度、死亡、离章、进无尽、转生、读档均取消；暂停/技能选择可续；重新从第1关完整起点可重试；前8章不计（RunCore 无阵亡段；挑战隔离见 §16.2）。
- [x] 已首通11-10后，从11-1开始连续无阵亡清完11-10仍能完成挑战；SuppressFinalStageFarmKill 不吞挑战记录，且不重发首通金币、不恢复刷怪横幅或额外等待（RunCore 无阵亡段 + 终局重复结算修复记录）。
- [x] 无尽25/50/100/150/200/250仅清层完成计入，死在N层不算N；全局纪录不混入槽纪录（RunCore 指标边界 + 无尽 Top-5 槽/全局分离回归）。

### 11.2 持久化与异常

- [x] v0..v8代表性旧档及v9未转生/已转生档均迁移至v10；保留五槽与转生字段；仅可证明状态静默补发，无金币/toast。
- [x] 强化/洗练当前装备下界仅初始化一次；无历史证据的累计从0；已转生档只补 §5.3 允许的历史，不凭 rebirthCount 补 P1000/全关卡/旧技能（v8 迁移断言 enhances=5 / reforges=2 / bestEnhance=5；v9 断言迁移出空 stats 与允许的静默补发）。
- [x] v10反复读档不重复种数或发钱；缺结构、重复/空/未知ID、负计数、非法mask、非有限质量可修复；点数仅派生，未知ID保留不计分。
- [x] 成就 schema>1 或存档 version>10 不自动降级覆盖；可提示并保持原数据。
- [x] 锁定同一批金币/stats/unlocked 快照：模拟保存失败能观察 dirty与提示，重试不重复结算；未成功保存时切档/重载被阻止；初始化/转生无中途半快照写入（2026-10-22 已补测，见 §16.4）。
- [x] 槽A/B切换独立，删非当前槽不影响当前（已验证）；删当前槽/ResetGame 显式重建、来源槽 toast 与挑战隔离已重跑（2026-10-22，见 §16.2）。
- [x] 实测满档（v11，Lv100 / 15 技能 / 无尽榜）JSON 3,977 字符；Windows 下 `PlayerPrefs` 单值 64KB / 256KB / 1MB / 4MB / 16MB / 64MB 往返全部通过（§16.5），4KB 存档离上限极远。

### 11.3 UI与回归

- [x] 1280×720入口/页签/16条分页/过滤/关闭无溢出；空筛选不出现1/0；字体能显示图标，否则回退文字「成就」。
- [x] 打开成就时血量、攻击、恢复与商店计时冻结；自动保存/UI计时正常；关闭恢复；与技能选择、设置、引导、对比、无尽结果和转生全部互斥。
- [x] 模态/横幅暂缓显示已验证；五条连续 toast、点击穿透与遮挡已全测（2026-10-22，见 §16.1）。
- [x] 编译无新增错误/警告；NewGameProgression.RunAssert、BalanceRegression.RunStaticOnly、转生/无尽相关现有回归通过。
- [x] 同状态同种子的纯战斗结果已逐帧比对：262 帧逐帧一致、首处差异 -1，换种子即分叉（2026-10-22，见 §16.3）；冷启动首战偏差已定位到命中音的音高随机并修复（§16.6），现在无需热身。自然养成经济变化已单独记录。合法五槽/明确转生状态的无尽探针 250 层 4/10、260 层 1/10（§15.2），属数值口径，不随本次比对改变。

## 12. 分阶段实现

| 阶段 | 内容 | 完成判据 |
|---|---|---|
| 1. 数据/持久化 | v10、独立累计、迁移、保存结果与dirty处理 | v9及更旧档可迁移；重复加载幂等、失败可重试、转生字段保留 |
| 2. 目录/求值 | 42条JSON、类型安全指标、Live/LoadReconcile批次 | 表总数/点数/金币正确；加载永不发钱；重复调用不重结算 |
| 3. 业务接线 | 普通/无尽/转生、技能手装、连续挑战 | 唯一事件计数、正确结算顺序及生命周期 |
| 4. UI | 入口、分页/过滤、冻结/遮罩、非阻塞toast | 1280×720截图与交互验收 |
| 5. 回归/同步 | 异常档、经济/战斗探针、README及相关说明 | §11通过，明确财运例外和实际存档v10 |

先稳定数据与结算再做面板；所有新接口和伪代码均为计划，不能当作已经存在的 API。

## 13. 设计决定与剩余风险

### 13.1 本稿采用

按槽、跨世保留；42条/805点/43,400固定名义金币；旧档补标不补钱；无直接属性；不做隐藏条目、商店或称号佩戴；无阵亡须第9~11章连续十关；质量80%允许天然装备；250层保持不追涨。保留核心内容，只修实现契约与误导表述。

### 13.2 实施前后需验证

1. **经济节奏**：固定奖励不吃财运是本稿明确的特例，落地需同步转生金币入口说明；早期强化时间和转生印记影响待测。
2. **无尽可达性**：使用合法装备与明确转生状态，验证现有250目标；文档里的理想模板数据不能替代正常玩法证据。
3. **质量目标强度**：保持原门槛意味着可较早完成，不再把它包装为必然30小时终局目标；如需要更难目标，应另行内容评审。
4. **存储可靠性**：本地PlayerPrefs不提供本设计可证明的崩溃原子性；强恢复方案超出首发，不声称防回档作弊。
5. **界面可读性**：坐标只是静态方案；需Unity截图检查中文长文、图标字体、toast日志遮挡和所有浮层门禁。

## 14. 与项目文档的对应

- README 已在 2026-10-20 把成就标为 ✅（42 条 / 805 点 / 存档 v10），路线图改为 11/12；§11 中已验证项与未跑项见 §15。
- 转生占用 v9，成就占用 v10；转生保留成就累计、解锁与称号，固定成就金币不吃财运，不改其它正向收入规则。
- 无尽文档已有五槽复测数据，引用时注明模板合法性与采样限制；不把历史设计目标当当前实测上限。
- 成就金币是收入，不解决终局金币出口。2026-10-20 已实现并接入存档与 Unity 资源；实现偏差与验证范围见 §15。

## 15. 实现状态（2026-10-20）

### 15.1 已落地

- 数据与存档：`Models.PlayerStats.cs`（跨世累计 9 个计数 + 无阵亡章掩码）、`Models.Achievements.cs`、`AchievementCatalog.cs`、`AchievementSystem.cs`、`Assets/Resources/achievements.json`；`SaveSystem` 升到 v10，保存 `achievements` 与 `stats`，v9 及更旧档自动补齐字段。
- 运行时：`GameController.Achievements.cs`（击杀/掉落/卖出/换装/强化/洗练/阵亡记账、无阵亡连续挑战、即时求值）；普通战斗、无尽、转生、技能手动装配与三选一均已接求值。
- UI：`GameUI.Achievements.cs` 入口、面板、7 页签、2×8 行、3 页（16/16/10）、只看未完成、分页；`GameUI.cs` 入口按钮与构建顺序；打开时冻结战斗并与其它浮层互斥。
- Toast：右侧 500×56，2.5 秒淡出，多条排队；任一模态或通关横幅可见时隐藏并暂停计时，恢复后续播；切档/重置清队列。
- 旧档：按可证明状态 `LoadReconcile` 静默补发，不弹 toast、不补金币；实测旧档补 14 条，入口显示「成就 14」。

### 15.2 验收记录

- 编译：`compilationFailed=false`、`consoleErrors=0`、`consoleWarnings=0`。
- 目录：42 条全部加载，总点数 805、名义金币 43,400、7 个称号。
- 面板：分类筛选、只看未完成、三页（16/16/10）、称号行、完成行压暗均截图验证。
- Toast：排队、模态隐藏/恢复续播、横幅隐藏/清除后续播均实机验证。
- 互斥：成就面板与设置/对比/引导/选关/技能等互斥，关闭后冻结解除。
- 无阵亡：第 9 章从第 1 关推进到第 10 关后 `noDeathChapterMask` 置 bit 9（256），`no_death_chapter` 解锁；9/10/11 章起点与严格 1..10 推进的逻辑已验。
- 存档：测试污染已清理，slot0 恢复 14 条、stats 全 0、Lv.100 11-10、金币 1,773,385；slot2 恢复 7 条、stats 全 0、Lv.100 1-1；slot1 为独立测试槽，已重建为默认 Lv.1 空档。槽 1/2 独立、删槽 2 不影响槽 1 已在 Play 中实机验证并还原。
- 回归：`NewGameProgression.RunAssert()` PASS（Lv.100 巅峰 41、13.7min、阵亡 2）；`BalanceRegression.RunStaticOnly()` PASS（静态哨兵与基线一致，说明成就接入没有改战斗数值）。
- §11 全量回归（2026-10-20，`Assets/Editor/AchievementRegression.cs`）：
  - 核心 `RunCore()` PASS，644 项 / 0 失败（2026-10-22 复跑；633 是当时数，之后补了存档写失败 11 项）；覆盖 42 条指标上/下界、质量 80% 边界、固定金币与幂等、财运例外、统计记账与溢出、无阵亡 9~11 章、转生印记/五条 rank、v0/v5/v8/v9/v10 迁移、v11/schema>1/坏 JSON 拒读与原档备份、异常字段清洗、重复加载幂等。
  - 回归中发现并修复：JsonUtility 把 null 的 `List<int>` 读成空列表，旧 v8 档 `clearedStages=null` 时原逻辑不会按 `maxChapterUnlocked` 回填首通记录。改为解析后列表为空即走 fallback（转生档本来就清空进度，仍回填 0）。
  - 真实转生（Play，独立槽）：Lv.100 / P1000 / 无尽 250 / 金币 100 万，`PerformRebirth()` 成功；成就、stats（123 杀 / 7 死 / mask 256）、无尽最高层保留，等级/巅峰/金币/关卡清空，印记 +19（巅峰 10 + 无尽 5 + 金币 3 + 基础 1）；落盘重载后一致。
  - 经济：新档 13.7min / P41 / 阵亡 2 / 金币 8,755；注入成就金币上界 +43,400 后 12.3min / P43 / 阵亡 8 / 金币 8,964；满转生模板 7.7min / P63 / 阵亡 0 / 金币 24,348。
  - 终局 P200：1028 次挑战 / 6144 击杀 / 24 阵亡 / 含刷怪 164.8min / 余额 5,864,020；首通 11-10 第 14 次。
  - 无尽：P200 中位 65；P1000（Lv110/80%/满转生）中位 120；合法五槽 P1000（Lv150/85%/满转生/10 seeds）中位 240，250 层通过 4/10、260 层 1/10，50 层均金币 6,390,723。250 可达但不稳定；290 是 maxGear 理想模板口径。

### 15.3 与设计的偏差

1. 称号接在**名称行**（`名称 「称号」`），不接描述后：描述只有 17 字，接称号会截掉系统说明（设计 §89 原方案）。
2. 入口标签为「成就」/「成就 N」，toast 标题不含 🏆：当前中文字体不带该 emoji，实际只显示数字。
3. 模态/横幅可见时 toast **整条隐藏**并暂停，而不是留在原地只暂停计时；恢复后从原剩余时间续播（§564 的“暂缓显示”落地口径）。
4. 成就面板打开时每 0.5 秒只刷新进度文本，不重建整页；翻页/切页签/切「只看未完成」才重建。

### 15.4 未完成 / 未验证

- 经济探针为模板注入（+43,400 是上界），不代表真人手动换装/犹豫。
- 只验证了**可注入的** `Save()` 失败路径（§16.4）；没有模拟进程被强杀/断电这类不抛托管异常的中途写入，`PlayerPrefs` 仍不提供强一致保证。
- 42 条成就的视觉遍历改为「数值溢出检查 126 行 + 8 张分页截图」（§16.5），没有做到每条长文案单独截图；已完成/未完成两种行样式都覆盖到了。
- 性能样本是本机 Editor 内的纯记账计时（§16.5），不含渲染与真机低端设备帧率。
- ~~指标边界使用合成数据；没有真实长跑 10,000/100,000 击杀的性能样本~~：2026-10-22 已补，见 §16.5。
- ~~未模拟 PlayerPrefs 写入失败/中途崩溃的原子性~~：写入失败已补测（§16.4）；崩溃路径仍见上一条。
- ~~42 条成就的完整视觉遍历未逐条截图~~：已用刻度化遍历替代，见 §16.5。
- ~~真实 v10 存档约 3,635 字节；未测目标平台 PlayerPrefs 上限~~：满档 3,977 字符，64MB 单值往返通过（§16.5）。

## 16. 补验记录（2026-10-22，Play 模式 1280×720）

> 补的是 §11.2「槽A/B切换独立…」、§11.3「五条连续toast…」与「同状态同种子的纯战斗结果」三处遗留。全部在 Play 模式、当前槽为「存档 1」时执行，没有改任何玩法数值。

### 16.1 Toast：连续播放、点击穿透、遮挡

- 反射 `OnAchievementUnlocked` 连推 5 条（`first_kill` / `first_drop` / `first_equip` / `first_death` / `first_enhance`，gold=100）。每秒 15 次采样：队列 4→3→2→1→0，标题顺序「初出茅庐 → 第一件战利品 → 换上新装 → 交学费 → 第一次强化」与调用顺序一致；最后一条播完 `activeSelf=False`、`_achToastActive=False`，无重叠、无跳播。
- 穿透：toast 面板实测 (600,430,500,56)、`CanvasGroup.blocksRaycasts=False`；取样点 `RectangleContainsScreenPoint` 为 True（确实落在面板内），同点 `EventSystem.RaycastAll` 只命中背后的 `LogTickerHit`（战斗日志 ticker 的透明按钮）→ **toast 不拦点击**。
- 遮挡：面板底边 y=486，商店顶边 492，留 6px；不压血条、属性、技能栏与底部按钮。唯一遮到的是舞台底部 ticker 的右侧（x 600..758）。判定**可接受并保持原位**：ticker 只是最近两条摘要，完整日志可展开，toast 2.8s 自动消失，设计稿 §10.4 本来就允许短暂覆盖日志。

### 16.2 删除当前槽 / ResetGame

1. 反射 `TryOpenNoDeathAttempt(9,1)` 置 `_noDeathActive=True`，再推 3 条 toast → 队列 3。
2. 删除前采样 `q=2 toast=True`（确认播放中）。
3. `game.DeleteSlot(1)`（删当前槽）→ PlayerData 重建：`slot=1 lv=1 exp=0 gold=0 hasSave=False`。
4. 删除后采样：`q=0 toast=False noDeath=False ach=0 kills=0 deaths=0 endlessRunNull=True running=False`。
5. 槽隔离：`SwitchSlot(0)` 回到 `lv=100 / gold 2,304,177 / 成就 21 条 / 历史榜 2 条`，与删槽前一致。

→ 显式重建、`NotifyContextReset()` 清空并隐藏 toast 队列、无阵亡挑战清零、统计清零、无尽局置空、磁盘槽已清。测试用的槽 1 原本就是 Lv.1 空档（备份文件因目录不存在未写成），**现在仍是空档**，没有数据损失。

### 16.3 同状态同种子逐帧比对

- 方法：`JsonUtility.ToJson(game.Player)` 快照，在 eval 里克隆独立 `PlayerData`，手工构造同一只怪物（近战 / 精英 / Lv110 / 40000HP / 护盾 312 / 攻 3000 / 防 5100 / 速 95 / 暴击 5% / 1.5×），`UnityEngine.Random.InitState(seed)` 后以 1/60 秒固定步长 `Tick`，逐帧记录 `主角HP|怪物HP|主角护盾|怪物护盾|日志条数`。
- 结果：同种子两次 **262 帧逐帧完全一致**（`firstDiff=-1`、63 条日志、结束于 `MonsterKilled`）；换 `seed=777` 立即分叉（`sameAC=False`），证明比对不是恒等。
- ~~冷启动例外：同一 eval 内第 1 次跑与之后不同~~ → **根因已查明并修复（§16.6）**：`BattleSystem` 命中时调 `GameAudio.PlayHit`，`GameAudio.cs:105` 用 `UnityEngine.Random.Range(pitchMin, pitchMax)` 抖音高，播一次命中音就吃掉 1 个玩法随机数。表现层已全部改用独立的 `GameRandom`，修复后带音频跑 3 次都是 54 draws，冷启动不再偏差，写战斗探针也不再需要热身。
- 250 层无尽探针的不稳定与这条无关（同一进程里跑多次，早已热身），4/10、260 层 1/10 的分布是数值方差，口径见 §15.2 与 `_balance/README.md`。

### 16.4 存档写入失败与切档保护

- 代码：`SaveSystem.Save()` 的 try 首行支持一次性注入 `FailNextSaveForTest`（仅 `#if UNITY_EDITOR`，抛 `InvalidOperationException` 走真实 catch 分支）；`GameController.SwitchSlot` 在写盘失败时打警告并 `return`，不切槽、不覆盖内存进度。
- 回归：`SaveFailureTests()` 11 项（基线写入 / 静默补发后的 dirty / 注入失败返回 false / 旧档未被覆盖 / dirty 保留 / 重试成功 / dirty 清除 / 内容更新 / 成就落盘 / 无残留标记），`RunCore()` 由 633 升到 **644 项 / 0 失败**。
- Play：`FailNextSaveForTest=true` 后 `SwitchSlot(1)` → `before=0 after=0 same=True`，日志出现「⚠ 存档写入失败，已取消切换存档槽」；注入关闭后 `SwitchSlot` 仍能 0→1→0，无残留。
- 边界：这里只覆盖**抛异常**的写失败；进程被强杀/断电不在此列（见 §15.4）。

### 16.5 成就面板遍历、击杀性能与 PlayerPrefs 上限

- 视觉遍历改为刻度化检查：对 7 个页签（全部 + 6 分类）×「只看未完成」开/关 × 全部分页，逐行读取名称/描述/进度/点数四个 Text 的 `preferredWidth` 与 `rect.width` 比对（容差 0.5px），共 **17 页 / 126 行，溢出 0 处**。行样式覆盖已完成（灰）与未完成（亮）两种；当前档 21/42，未完成的 21 条分布在各分类里都被渲染过。
- 截图：8 张（7 个页签 + 只看未完成空态）合成 `docs/shots/achievements-sweep.png`，页面统一 1280×720。
- 击杀记账性能（Editor 内，纯 C#，含 `Evaluate` 的 42 条指标扫描与装备峰值捕获）：
  - 10,000 次：54ms，5.38µs/杀；
  - 100,000 次：589ms，5.89µs/杀（期间自然解锁 4 条）；
  - 最坏情况 100,000 次强制全表重扫：631ms，6.31µs/杀。
  - 结论：即使每秒 10 杀也只用掉 0.006% 的单帧预算，不需要为成就记账做增量缓存。
- 存档与 PlayerPrefs：满档 JSON **3,977 字符**；`Save` 0.08ms、`Load` 0.17ms（各 30 次平均）。`PlayerPrefs` 单值往返 64KB / 256KB / 1MB / 4MB / 16MB / 64MB 全部成功，4KB 级存档离上限极远；测试键用完即删。

### 16.6 冷启动随机数消耗（首战不可复现）根因

- 现象：同一 eval 内第一次战斗与之后差一个 `UnityEngine.Random`，曾被误判为「Tick 调用链的一次性静态初始化」。
- 根因：`BattleSystem.cs:304` / `:393` 在命中时调用 `GameAudio.PlayHit(crit)`，`GameAudio.cs:105` 的 `Random.Range(pitchMin, pitchMax)` 用的是玩法随机流；`PlayHit` 有 0.045s 节流，整局通常只多消耗 1 个 → 正好是一次性偏差。
- 量测（`TempRandomProbe`，预生成 20,000 个 float 反查下标）：无音频 3 次都是 `frames=372 draws=54`；挂上 `GameAudio` 后第一次 `draws=55`、后两次 54。
- 修复：新增 `Assets/Scripts/GameRandom.cs`（`System.Random`，种子固定 `0x5EA17`）专供表现层，随机数不再倒吸玩法随机流。改动点：`GameAudio.cs:105` 音高、`GameUI.Core.cs:106/281/287/288/299/300` 飘字速度与落点、`GameUI.Portrait.cs:65/66/128/143` 呼吸/眨眼、`GameUI.Stage.cs:788` 特效抖动；`GameConfig.cs:346` 的掉落品质仍属玩法，保持 `UnityEngine.Random`。
- 复测：修复后挂 `GameAudio` 连跑 3 次都是 `draws=54`，逐帧比对首战即一致；`Assets/Scripts` 内 `Random.` 现在只剩掉落品质那一处。
- 影响：这是**回归修复**，不改变战斗数值路径；§15.2 的经济与无尽探针结论不受影响。


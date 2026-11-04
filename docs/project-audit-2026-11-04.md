# 项目逻辑、性能与玩法审计

日期：2026-11-04。范围：战斗、技能、转生、存档、无尽契约/遗物、UI刷新及回归工具。

## 总结与证据边界

项目已形成战役、巅峰技能、无尽契约、转生的成长循环。累计契约分的保存、加载、历史记录与跨转生保留路径已接通，本轮未确认独立的v16必现积分错误。但应先修状态边界和回归可信度，再继续扩内容。

本报告区分运行复现、静态确认、条件性缺陷、待量测风险。运行验证仅包含Editor纯内存战斗探针，以及缺失基线返回值和CI判定谓词的组合验证；没有进行完整Play交互验证、写盘故障注入或Profiler采样。本轮未修改游戏源码/场景/资源，未写玩家存档，未commit/push，仅新增本报告。行号对应审计时源码。

## 一、逻辑与状态缺陷

### L1 · P1：战斗胜负依赖Tick步长【运行复现】

证据：[BattleSystem.cs:301–367](../HelloWorldProject/Assets/Scripts/BattleSystem.cs#L301-L367)。先推进技能、结算玩家全部追帧攻击，怪物死亡立即返回，然后才推进怪物计时和攻击。

最小反例：玩家HP=1；怪物HP=1、攻击1000、暴击率0，怪物攻击计时0.01秒，玩家攻击计时0.08秒；相同随机种子101，总推进0.1秒：
- 单步0.1秒：MonsterKilled，玩家HP1、怪物HP0，玩家/怪物命中1/0。
- 每步0.02秒：PlayerDied，玩家HP0、怪物HP1，玩家/怪物命中0/1。

探针finally恢复UnityEngine.Random.state，未使用玩家存档。两方循环都有MaxCatchUpAttacks上限，问题是事件顺序，不是无限循环。

建议：按事件到期时间推进，或采用明确累计器和过载策略的固定模拟步；同步定义技能、持续效果和同刻事件顺序，不能仅交换两个循环。验收需比较同总时长、同随机流的不同分帧，覆盖同刻致死、技能到期及追帧上限。

### L2 · P1：装卸技能刷新主动与保命冷却【静态确认】

证据：[GameController.cs:1089–1136](../HelloWorldProject/Assets/Scripts/GameController.cs#L1089-L1136)、[BattleSystem.cs:140–152](../HelloWorldProject/Assets/Scripts/BattleSystem.cs#L140-L152)、[skills.json:354–369](../HelloWorldProject/Assets/Resources/skills.json#L354-L369)。

装卸调用ResetSkillState，清除主动、公共和LastStand冷却；保命配置为60秒。技能面板暂停战斗，形成不推进游戏时间的刷新路径，尚未做Play操作复现。

建议：冷却绑定技能身份，换槽/装卸不清零；新战斗、死亡、转生等真正重置边界单独定义。验收：触发保命后装卸任意技能、换槽、重开面板，均不能立即再次触发。

### L3 · P1：转生遗漏流派重置【静态确认】

证据：[GameController.Rebirth.cs:253–265](../HelloWorldProject/Assets/Scripts/GameController.Rebirth.cs#L253-L265)、[GameController.Skills.cs:15–48](../HelloWorldProject/Assets/Scripts/GameController.Skills.cs#L15-L48)、[Models.PlayerData.cs:957–964](../HelloWorldProject/Assets/Scripts/Models.PlayerData.cs#L957-L964)。

实际转生清技能与六槽，却不清doctrine；提示与选择入口遇HasDoctrine即退出。现有ResetSkillsForRebirth会清doctrine和pendingLegacyDoctrineGrant，但未用于该路径。一级因此保留旧流派核心，P10也不能重选。

建议：审查并统一使用完整重置函数，保留skillEverMask等永久记录。验收：两个流派分别转生后HasDoctrine=false，P10可以重选，永久发现记录仍保留。

### L4 · P2：读档在永久生命加成恢复前裁血【静态确认】

证据：[SaveSystem.cs:818–819](../HelloWorldProject/Assets/Scripts/SaveSystem.cs#L818-L819)、[SaveSystem.cs:854–863](../HelloWorldProject/Assets/Scripts/SaveSystem.cs#L854-L863)、[Models.PlayerData.cs:509–565](../HelloWorldProject/Assets/Scripts/Models.PlayerData.cs#L509-L565)。

新PlayerData加载时先按MaxHp裁currentHp，后恢复rebirthBattleRank；满血且永久战斗升级大于0时可能被裁到无该升级的上限。

建议：全部影响MaxHp的字段加载、迁移、归一化之后最后裁血。验收：战斗升级等级0至上限，满血与伤血往返保存/加载、切槽，合法HP不减少，超上限输入仍被裁切。

### L5 · P1（数据安全）：不兼容导入先覆盖旧槽、随后拒载【静态确认】

证据：[SaveSystem.cs:427–443](../HelloWorldProject/Assets/Scripts/SaveSystem.cs#L427-L443)、[SaveSystem.cs:699–709](../HelloWorldProject/Assets/Scripts/SaveSystem.cs#L699-L709)、[GameController.cs:957–981](../HelloWorldProject/Assets/Scripts/GameController.cs#L957-L981)。

顶层v16、合法等级、achievements.schemaVersion=2的JSON可通过导入，但Load拒绝子schema大于1。原槽已经覆盖；备份保存的是不兼容导入内容而非原档；控制器加载失败后新建游戏，仍显示导入成功。此问题依赖导入不兼容文件，不是正常运行无条件丢档；因原档覆盖风险列P1。

建议：抽取无写盘副作用的完整兼容性/结构验证，在覆盖前执行，保留旧原文用于恢复；只有真正可用才提示成功。PlayerPrefs不是事务存储，不能承诺SetString/Save具备原子替换保证。

验收：用内存存储替身或隔离槽测试新版子schema、畸形输入和正常旧档；失败时原字符串逐字不变，正常迁移成功。

### L6 · P2：取消切槽不能恢复已结束的无尽局【条件性缺陷】

证据：[GameController.cs:866–874](../HelloWorldProject/Assets/Scripts/GameController.cs#L866-L874)、[GameController.Endless.cs:537–586](../HelloWorldProject/Assets/Scripts/GameController.Endless.cs#L537-L586)。

先EndEndlessRun(SlotChanged)清遗物/契约等状态，再保存。保存失败只取消槽号切换，原局已不可继续，SlotChanged又不给契约累计分。本轮未注入存储失败。

建议：重排可失败与不可逆操作，或完整快照回滚，明确结算顺序以免重复发奖。验收：隔离环境用FailNextSaveForTest，取消切换后槽号、局内状态、待选遗物和契约均保留，重试不重复奖励。

### L7 · P1（验证可信度）：缺失基线被CI谓词判为通过【运行验证】

证据：[AutoBattle.CI.cs:43–56](../HelloWorldProject/Assets/Editor/AutoBattle.CI.cs#L43-L56)、[BalanceRegression.cs:395–403](../HelloWorldProject/Assets/Editor/BalanceRegression.cs#L395-L403)。

对随机生成且未创建的临时路径调用RunStaticOnly，返回“还没有基线，先跑一次完整版”。套入CI实际谓词：detail非空且不含FAIL，得到wrapperWouldPass=true。此为返回值与谓词的组合验证，不是完整RunAllQuick执行；读取失败/空基线也有同类静态风险。

建议：结构化Passed/Failed/Skipped/Error，必要基线缺失必须失败，不以自然语言关键词判断。验收：不存在、无法解析、空、匹配、偏离五类基线；前三类不得计PASS。

## 二、性能：源码热点不是实测瓶颈

1. **技能面板优先量测**：[GameUI.Panels.SkillBar.cs:169–258](../HelloWorldProject/Assets/Scripts/GameUI.Panels.SkillBar.cs#L169-L258)打开时逐帧处理9卡，推荐开启时每帧最多27次SkillAdvice.Score；[Skills.cs:375–381](../HelloWorldProject/Assets/Scripts/Skills.cs#L375-L381)的KitOf创建数组。建议按技能/属性/推荐版本刷新，冷却显示单独更新。
2. **装备摘要重复格式化**：[GameUI.Refresh.Log.cs:248–275](../HelloWorldProject/Assets/Scripts/GameUI.Refresh.Log.cs#L248-L275)每帧先格式化五槽再比较字符串。装备评分已有脏缓存，应在格式化前判断版本，而不是重复优化评分缓存。
3. **关闭日志仍处理详情**：[GameUI.Refresh.Log.cs:156–181](../HelloWorldProject/Assets/Scripts/GameUI.Refresh.Log.cs#L156-L181)有LogVersion门控，但仍在详情关闭时构造16KB StringBuilder及最多180行；[GameUI.Stage.Art.cs:235–250](../HelloWorldProject/Assets/Scripts/GameUI.Stage.Art.cs#L235-L250)仍赋值和测高度。建议折叠仅更摘要，展开按版本补建。
4. **派生属性重复计算**：[GameUI.Core.cs:263–286](../HelloWorldProject/Assets/Scripts/GameUI.Core.cs#L263-L286)在签名前读派生属性；[Models.PlayerData.cs:318–351](../HelloWorldProject/Assets/Scripts/Models.PlayerData.cs#L318-L351)重复扫装备。缓存必须兼容[EquipmentScore.cs:52–65](../HelloWorldProject/Assets/Scripts/EquipmentScore.cs#L52-L65)临时换槽比较，避免陈旧结果。
5. **自动保存尾延迟待测**：[GameController.cs:2029–2035](../HelloWorldProject/Assets/Scripts/GameController.cs#L2029-L2035)按5秒周期保存，暂停仍执行；[SaveSystem.cs:566–644](../HelloWorldProject/Assets/Scripts/SaveSystem.cs#L566-L644)复制、序列化并写PlayerPrefs。先测p50/p95/p99再考虑脏版本/合并保存，不能降低关键操作持久化保证。

10Hz textTick不是所有面板的全局节流。音乐预览的Resources.Load有缓存，只能列首次加载/驻留风险，不能称每帧磁盘读取。旧成就文档的Save微基准不代表当前帧率或尾延迟。

建议在战役稳定战斗、技能面板开/关、日志折叠/展开、无尽密集结算、自动保存五类场景采集CPU帧耗时分位数、GC Alloc/帧、文本/布局耗时。Editor与目标构建分开记录；未采样前不承诺优化能提升多少FPS。

**2026-11-04 已采样本节第 1、4、5 条**（Editor Play，`Stopwatch` 探针）：技能面板关闭 0.86 µs/帧、`RefreshCharacter` 1.3 µs/次、整帧 UI 刷新 30–37 µs、10 Hz 文本刷新 65.5 µs、自动存档 p50 0.08–0.14 ms / p95 0.25 ms（4.6 KB）。三项均为噪声级，不构成优化项，详见下文「性能 · 第 2、3 条已修；第 1、4、5 条已采样」。

## 三、玩法与设计一致性

### G1：决绝风险与收益可能不匹配

[ContractCatalog.cs:195–201](../HelloWorldProject/Assets/Scripts/ContractCatalog.cs#L195-L201)决绝系数0.9、不改变战斗属性；[计分:253–258](../HelloWorldProject/Assets/Scripts/ContractCatalog.cs#L253-L258)与死亡结算允许清160层获304分。对于本来就打到死的策略，放弃主动撤离几乎无额外成本，却获得1.9倍基础分。

尚未证明全局最优分/分钟。先对照无契约、单决绝与相当实战难度的其他契约，测分/分钟、死亡率、耗时分布，再决定降低系数、加入真实失败成本或明确其低门槛定位。

### G2：深层遗物突破旧文档预算

[gameplay-expansion.md:145–150](gameplay-expansion.md#L145-L150)规定普通件≤4%、6槽≤18%/8槽≤22%；[RelicCatalog.cs:189–262](../HelloWorldProject/Assets/Scripts/RelicCatalog.cs#L189-L262)二/三档有无代价伤害+6%/+8%。合法六件0/9/11/12/18/23的原始伤害倍率约1.612578，同时生命0.94、承伤1.1448，不能等同净战力+61.26%。

需明确旧预算是否继续约束深层版本：坚持则调平衡；有意提高则修订分档预算及验收。不得只为迁就代码删除约束。旧文档“满槽必须替换”也与当前SkipRelicChoice不一致。

### G3：无尽探针对照条件失配

[EndlessPacing.cs:537](../HelloWorldProject/Assets/Editor/EndlessPacing.cs#L537)固定SlotsFor(0)，[639](../HelloWorldProject/Assets/Editor/EndlessPacing.cs#L639)默认tier1抽签；[713–754](../HelloWorldProject/Assets/Editor/EndlessPacing.cs#L713-L754)满槽策略遍历全部三级池，掉落遗漏正式逻辑RelicDropBonus。上限策略可作理论参照，不能与固定低档自然获取比较后证明深层平衡。

建议按累计分0/300/800/1500/3000建立合法槽位/池子的配对种子矩阵，复用正式抽签和奖励接口，覆盖替换、跳过、契约组合和掉落收益，报告区分自然获取与越级理论上限。

### G4：转生奖励诱导囤余额、复用历史成绩

[RebirthCatalog.cs:98–130](../HelloWorldProject/Assets/Scripts/RebirthCatalog.cs#L98-L130)余额1600000给4枚金币印记，1599999给3枚；[GameController.Rebirth.cs:191–195](../HelloWorldProject/Assets/Scripts/GameController.Rebirth.cs#L191-L195)取历史endlessBestFloor，历史250层以后每世继续贡献5枚，P100时基础1+巅峰1+历史层5至少7枚。

若永久里程碑本就奖励后续轮回，应明确说明；若期望每世重新参与经济/无尽，则考虑本世贡献/成绩。未经真实时间策略对照，不能称最快刷印记路径。

## 四、处理顺序与交付门槛

1. 先修L5数据保护、L3转生、L4加载裁血、L2冷却；L1单独设计时间推进并补确定性回归。
2. 修L7回归结果与G3探针口径，避免用假绿或失配测试给数值背书。
3. 修L6故障恢复；复核转生/无尽结算忽略Save返回的同源风险，不另算成独立必现缺陷。
4. 先量测再做UI脏更新/可见性门控，复测CPU、GC和显示正确性。
5. 用合法条件下的时长/收益证据决定G1/G2/G4，再同步设计约束和玩家提示。

写盘故障、导入覆盖和转生测试必须先用存储替身或隔离档并备份真实存档，不直接拿当前玩家档试验。本报告不包含这些测试的通过声明；修复验收还须覆盖正常路径和旧档迁移。

## 五、处置记录（2026-11-04 修复后回填）

> 上方原文与行号保持审计时原样；本节是修复后的结论，行号指修复后的位置。所有改动都在 `HelloWorldProject/Assets/` 下，未 commit / push。

### L1 · 已修：Tick 改固定子步

- 根因确认：原 `Tick` 是「先结算技能与玩家追帧攻击、死亡即 `return`，再推进怪物计时与攻击」，同一总时长切成不同帧数会得到不同胜负。
- 改动 `Scripts/BattleSystem.cs`：新增 `public const float MaxTickStep = 1f / 120f;`（:319）、`private const int MaxSubStepsPerFrame = 32;`（:322）、`private float _carrySeconds;`（:325）。`Tick(player, monster, dt, log)` 现在只累加时间、取整步数，超过 32 步直接丢弃积压（:337–340，长卡顿/后台挂起不补算），再循环调用新的 `private BattleTickResult TickStep(...)`（原函数体整体搬入，:350）；`ResetSkillState()` 首行清 `_carrySeconds`（:142）。
- 未改事件语义：同刻顺序仍是「技能 → 玩家 → 怪物」，只把「一帧推进多少」固定成 1/120 秒。
- 验收：重录平衡基线（见下），并新增「同总时长不同分帧结果一致」的判据来源——帧率只决定一次 `Update` 跑几个子步，不影响子步内部顺序。
- **副作用必须记账**：数值基线变了，`_balance/baseline.json` 已重录，旧基线备份在 `_balance/baseline.before-l1.json`。winrate 8 档 [0.000,0.054,0.190,0.427,0.590,0.696,0.859,0.886] → [0.001,0.064,0.171,0.455,0.609,0.802,0.813,0.934]；chapterWinrate 末段 ch10 精英 0.552→0.735、ch10 Boss 0.221→0.061、ch11 精英 0.150→0.0、ch11 Boss 0.0→0.0。**这是步进语义变化的必然结果，不是平衡改动**。

### L2 · 已修：冷却随技能走

- `Scripts/BattleSystem.cs:184` 新增 `public void CarryActiveCooldown(int fromSlot, int toSlot)`：两槽取较大的剩余冷却。`Scripts/GameController.cs:1134` 在 `EquipSkill` 换槽后调用；`UnequipSkill` 不再清冷却。
- 保留的合法重置边界（4 处 `ResetSkillState`）：`GameController.cs:435`（进入关卡）、`GameController.cs:1038`（重置局内）、`GameController.Endless.cs:307`（无尽开局）、`:375`（二周目无尽开局）。所以「新战斗 / 死亡 / 转生」仍会重置，装卸与换槽不会。

### L3 · 已修：转生走完整重置

- `Scripts/GameController.Rebirth.cs:255` 改调 `Player.ResetSkillsForRebirth()`（`Models/PlayerData.cs:958`）：清 `doctrine` / `ownedSkillMask` / `pendingSkillPicks` / `pendingLegacyDoctrineGrant` / 六槽，保留 `skillEverMask`（永久发现记录）。
- 结果：转生后 `HasDoctrine=false`，P10 可重新选流派，永久技能记录仍在。

### L4 · 已修：裁血放到加载最后

- `Scripts/SaveSystem.cs:985` 在 `Load` 返回前统一 `player.currentHp = Mathf.Clamp(data.currentHp, 1, Mathf.Max(1, player.MaxHp));`，此时 `rebirthBattleRank` 等永久加成字段已读入；删除加载中段按当时 `MaxHp` 的早裁两行。
- 行为：合法 HP 不再被永久生命加成之前的旧上限砍掉；超过上限的输入仍被裁切。

### L5 · 已修：导入先验证、失败回滚

- `Scripts/SaveSystem.cs:454`：`TryImport` 增加 `data.achievements.schemaVersion > CurrentAchievementSchema` 的**写盘前拒绝**（错误文案「成就状态版本 N 高于当前支持的 1，已拒绝导入（原存档未改动）」）。
- `Scripts/SaveSystem.cs:391` 新增 `public static string SnapshotSlot(int slot)`、`:394` 新增 `public static void RestoreSlotSnapshot(int slot, string json)`（空串表示删键）；`Scripts/GameController.cs:962` 在导入前取快照，`:988` 在 `Load` 失败时回滚并提示「导入失败：内容读不回来，已恢复导入前的存档」。
- 边界：PlayerPrefs 没有原子替换保证，这里承诺的是「同一槽位前后不丢数据」，不做跨键事务。

### L6 · 已修：先存盘再结束局

- `Scripts/GameController.cs` 的 `SwitchSlot` 改成 Save → 成功才 `EndEndlessRun(SlotChanged)` → 再 `SelectSlot`；Save 失败保留原槽与原局并提示。校验顺序上不再出现「局结束了但槽没换」。

### L7 · 已修：回归判定结构化

- `Assets/Editor/AutoBattle.CI.cs:94` 改成 `private static string RunStep(int index, out bool ok)`：静态哨兵要求 detail 以 `PASS` 开头（:101）、成就核心要求 `AchievementRegression.LastFailCount == 0`（:109，属性在 `AchievementRegression.cs:29`）、无尽冒烟要求文本含「首死」、异常一律 `ok=false`。
- `Assets/Editor/BalanceRegression.cs:400/404/405` 三条缺失/读不出/空基线的文案改成 `FAIL — ...`；:385/:417 的偏离基线也已是 `FAIL —` 开头。缺失基线不会再被「非空且不含 FAIL」的谓词放行。

### 性能 · 第 2、3 条已修；第 1、4、5 条已采样（噪声级，不改）

- 第 2 条（装备摘要重复格式化）：`Scripts/GameUI.Refresh.Log.cs` 的 `RefreshEquipmentLines` 改成先算 64 位哈希（`GameUI.Core.cs:200` 的 `private struct Sig`，float 用 `Add(x, 100000f)`）再比较，不再每帧拼 300+ 字符串；`GameUI.cs:220` 的 `_scoreCacheSignature` 由 `string` 改为 `ulong`。命中缓存在 :300–301。
- 第 3 条（关闭日志仍处理详情）：`RefreshLog()` 在 `!_battleLogOpen` 时直接返回（首次收起清空一次 `_battleLogDetail.text`，`_logDetailCleared` 在 `GameUI.cs:222`）；展开时 `ToggleBattleLog()` 把 `_lastLogVersion` 置 −1 强制重建，不会显示旧内容。
- 验证：recompile `completed`，`console` groundTruth `{"compilationFailed":false,"consoleErrors":0,"consoleWarnings":0}`；截图 `Assets/_shots/perf1.png`（装备卡 Lv/倍率/评分正常）、`perf2.png`（点 LogToggle 后「战斗日志 · 共 2 条」正常展开）。
- 第 1 / 4 / 5 条（技能面板逐帧、派生属性重复计算、自动保存尾延迟）**2026-11-04 已采样，三项都在噪声级，不改代码**。口径：Editor Play（Unity 6000.6.5f1 / Mono / 1280×720 / 79 draw calls / `cpuMain 2.2 ms` / `cpuFrameTime 4.8 ms`），探针 `HelloWorldProject/_shots/probe/perf_ui_20261104.cs` 与 `perf_ui3_20261104.cs`（`eval_file`，结果走 `Debug.Log` → `unity command console`）。
  | 项 | 测法 | 实测 | 折成 16.7 ms 一帧 |
  |---|---|---|---|
  | 第 1 条 技能面板 | `RefreshSkillPanel` 关 / 开各计时 | 关闭 **0.86 µs/帧**；`SkillAdvice.Score` 0.55–0.91 µs/次（9 卡 × 2 = 10–16 µs，仅面板打开时）；打开态含 `SetActive` 101–306 µs/帧 | 关 0.005%；开 0.6–1.8%（且打开时战斗暂停） |
  | 第 4 条 派生属性 | `RefreshCharacter` 反射调用 2000 次（签名不变路径）+ 20 个属性读一遍 | **1.3 µs/次**（其中 20 个属性读取 0.5 µs）；整帧 `Refresh(false)` 30–37 µs | 0.008%；整帧 UI 0.18–0.22% |
  | 第 5 条 自动保存 | `SaveSystem.Save` 连跑 12 次 | p50 **0.08–0.14 ms**、p95 `0.21–0.25 ms`、max `0.25 ms`；JSON 4,661 字符 ≈ **4.6 KB**；12/12 成功 | 每 5 秒一次，占单帧 0.6–1.5% |
  另测 10 Hz 文本刷新 `Refresh(true)` = **65.5 µs/次**（≈ 每 10 帧多 0.4% 一帧）。
- 结论：**第 1 条原判「每帧最多 27 次 SkillAdvice.Score」不成立**——关面板时只付 0.86 µs 早退，真实成本只在面板打开时（而那时战斗暂停）；第 4 条「签名前读派生属性」是真实成本但只有 0.5 µs，整帧 UI 刷新 30–37 µs；第 5 条每 5 秒**无条件**存档（没有脏判）确实浪费 CPU，但尾延迟 0.25 ms，不构成卡顿。**三条都不需要优化**；若日后想省电，唯一有意义的是给自动存档加脏判，平均帧耗收益约 0.00002%，性价比低于维护风险。Player 构建只会更快，Editor 数字可作量级上界。

### G1 · 决策：保留决绝 0.9 系数，定位写清楚（不改数值）

- 决绝的成本是**放弃主动撤退**，不是战斗难度；它的作用是给「本来就打算打到死」的玩家一条不需要纪律门槛的契约。要它承担真实失败成本，等于新增第九条惩罚契约，与「选难度」的定位冲突；降到更低系数会让它在八条里彻底没有存在意义。
- 记账：决绝的期望分/分钟未实测。若日后要调，先按「无契约 / 单决绝 / 单狂躁 Ⅲ」三组跑分/分钟、死亡率与耗时分布，再决定——不要凭感觉改系数。

### G2 · 已修（修订预算）：见 `docs/gameplay-expansion.md` §4.2 / §4.3

- 旧「普通件 ≤4%、6 槽 ≤18%、8 槽 ≤22%」是 **tier1 时代的加总口径**，遇到双刃件（伤害 ↑ 生命 ↓）无法相加，且约束不到无代价的 +6% / +8% 深层件。修订为分档预算：tier1 单件 ≤4%（双刃 ≤9%）、tier2 ≤6%、tier3 ≤9%；满槽改成可复算的**净战力倍率**（DPS × √有效生命）验收：6 槽 ≤ ×1.55、8 槽 ≤ ×1.70。
- 实算（模板暴击 37.5% / 暴伤 627% / 攻速 +43.1%，取伤害轴最优组合）：6 槽 T1 ×1.304、T1+T2 ×1.385、T1~T3 ×1.530；8 槽 ×1.324 / ×1.430 / ×1.658——全部在限内，且深层档位的代价真实存在（T3 组合承伤 ×1.145、生命 ×0.94）。
- 旧文档「满槽必须替换」与代码不符：实际三选一永远可以整次跳过，只有选择替换时才要求先舍弃一件（`GameUI.Endless.Relics.cs:201` 的「跳过本次 / 取消替换」）。§4.1 已改成「有代价的选择」。
- **已验收（2026-11-04）**：五档 50 种子配对矩阵跑完，见 G3 与 `docs/gameplay-expansion.md` §4.5 第 7 条。8 槽 T3 上限 **+40.4 ± 5.6 层** 与旧外推 +40 吻合；7 槽 T3 自然 **+32.6 ± 6.8** 与「+32」吻合（贪类改造后重跑值，见下条 G3）；**「6 槽 T3」档位不可达**（800 分即 7 槽，T3 要 1500 分），原外推中的那一行已改标为假想边界。

### G3 · 已修：探针口径对齐正式逻辑

- `Assets/Editor/EndlessPacing.cs` 的 `Simulate` 改用真实 `RelicCatalog.SlotsFor(contractScore)` 与 `ContractCatalog.RelicMaxTier(contractScore)`；三选一走 `RelicCatalog.RollChoices(seed, floor, relics, relicMaxTier)`；`BestUnheldRelic` 增加 `maxTier` 过滤（越级件不再进入上限策略）；掉落补 `p.RelicDropBonus`（`LootSystem.RollDrop(..., p.RebirthDropBonus + p.RelicDropBonus)`）。
- 报告文案：写死的 `SlotsFor(0)` 改成按模板契约分输出，并新增一行「遗物档位口径：模板契约分 X → 槽位 Y、三选一最高 Z 档」；策略 4 标签改「开局满槽（理论上限：全池最高分、无抽签成本）」，与「自然获取」区分。
- **已跑（2026-11-04）**：0 / 300 / 800 / 1500 / 3000 五档 × 50 种子 × 3 策略（`EndlessPacing.CompareRelicTiers`，菜单「Tools/测试/深渊遗物分档矩阵（5 档 × 3 策略，50 种子）」，输出 `HelloWorldProject/_shots/relic-tiers.txt`）；模板巅峰 1000 / 满倍率装备、`contract = null`、900 层。Δ（配对均值 ±95%CI）：T1@0 **+18.7±6.8**（上限 +20.6±5.6）、T2@300 **+31.4±6.8**（上限 +30.3）、T2@800 +28.9±6.8（上限 +33.4）、T3@1500 **+32.6±6.8**（上限 +29.0）、T3@3000 **+39.6±6.8**（上限 +40.4±5.6）。**这组数字是 2026-11-04 贪类三件补战斗副属性之后重跑的口径**；改前是 +18.6 / +31.4 / +28.6 / +33.0 / +39.8，差在噪声内。

### G4 · 决策：印记是「这一世的成绩单」，保持现状并写清楚

- 金币印记本来就是对数阶梯：`RebirthCatalog.GoldMarks` = ⌊log2(gold / 100000)⌋（`Scripts/RebirthCatalog.cs:111`，上限 14）。1,599,999 → 3 枚、1,600,000 → 4 枚只是「16 倍基数」这条阶梯的本义，不是断崖；囤余额的边际收益是每翻倍 +1 枚，不存在「卡在边界多拿一枚」的空间。
- 巅峰（/100，上限 10）、无尽（/50，上限 5）同理是**里程碑**：奖励「这一世达到过的最高水位」，跨世保留是设计意图，不要求每一世重新打。上限合计 `MaxMarks = 1 + 10 + 5 + 14 = 30`。
- 玩家可见性已有：`GameUI.Rebirth.cs:202` 把印记拆成「巅峰 X + 无尽 Y + 金币 Z + 基础 1」，本轮补的是文档口径，不改数值。

### 待办（从本报告衍生，未完成）

1. ~~G2 + G3 合并：五档 50 种子配对矩阵~~ **✅ 已完成（2026-11-04）**，写回 `docs/gameplay-expansion.md` §4.5 第 7 条，结果表在 `HelloWorldProject/_shots/relic-tiers.txt`。
2. ~~性能第 1 / 4 / 5 条：Profiler 采样后再决定~~ **✅ 已完成（2026-11-04）**：三项实测都在噪声级（技能面板关闭 0.86 µs/帧、`RefreshCharacter` 1.3 µs/次、存档 p95 0.25 ms），结论与数据见上文「性能 · 第 1 / 4 / 5 条已采样」，不改代码。
3. G1：决绝的分/分钟对照（无契约 / 单决绝 / 单狂躁 Ⅲ）。
4. 写盘故障注入与导入回滚的隔离测试（`FailNextSaveForTest`、内存存储替身），本轮只做了代码路径与静态回滚，没有做故障注入验收。

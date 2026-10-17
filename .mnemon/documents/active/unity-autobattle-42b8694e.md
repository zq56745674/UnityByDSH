---
id: "42b8694e-f097-43db-9f58-9d606e02cfd1"
title: "Unity AutoBattle 项目现状总览与接手指南（新会话入口）"
description: "HelloWorldProject 挂机自动战斗游戏的全局地图：代码文件职责、原始 12 步路线图（每步具体内容）与进度、关键设计约定与踩坑教训、既有 Mnemon 文档索引、当前待办。新会话开始开发前先读这份，再按索引跳到对应专题文档。"
status: "active"
created_at: "2026-10-15T18:23:22.330Z"
updated_at: "2026-10-17T12:07:38.060Z"
content_hash: "411dcf1db1346cb896705cbfd9bc3a550f6ab5fe81c048af8151f494f3a396f7"
source_paths:
  - "HelloWorldProject/Assets/Scripts/GameController.cs"
  - "HelloWorldProject/Assets/Scripts/GameUI.cs"
  - "HelloWorldProject/Assets/Scripts/Models.cs"
  - "HelloWorldProject/Assets/Scripts/Stages.cs"
  - "HelloWorldProject/Assets/Scripts/GameConfig.cs"
  - "HelloWorldProject/Assets/Scripts/SaveSystem.cs"
  - "HelloWorldProject/Assets/Editor/LateGameProbe.cs"
  - "HelloWorldProject/Assets/Resources/HeroPortrait.png"
session_ids:
  - "session-820fee2d-3ea4-4964-9f54-6440131b1026"
  - "session-1c2b95c0-d836-4a67-b085-d00b734a2069"
memory_body_ids:
  []
---

# Unity AutoBattle 项目现状总览

工程 `HelloWorldProject/`（E:\pytest\unitydsh1），Unity 6000.6.5f1，Universal 2D。全程序化搭建的挂机自动战斗 RPG，无美术资源依赖（唯一例外见「立绘」）。本文是**入口文档**：先读这份拿到全局，再按文末索引跳专题。

## 代码结构（`HelloWorldProject/Assets/Scripts/`）

| 文件 | 职责 |
|---|---|
| `Bootstrap.cs` | 极薄入口：`Awake` 里先 `AddComponent<GameController>()` 再 `AddComponent<GameUI>()`，最后 `ui.Initialize(controller)`。顺序不能反——`AddComponent` 立即触发 `Awake`。 |
| `GameController.cs` | 状态机与规则中枢（`State`：`Fighting` / `WaitingRespawn` / `Reviving` / `Shopping` / `SkillChoice`）。怪物刷新、击杀结算、掉落、关卡推进、存档节拍、面板开关状态都在这里。**不碰 UI**。 |
| `GameUI.cs` + `GameUI.Panels.cs` + `GameUI.Core.cs` | 全程序化构建 uGUI（无 Prefab）。已用 `partial` 拆三份：主文件放字段/生命周期/左右面板构建，`Panels` 放四个浮层面板，`Core` 放刷新循环与控件工厂。 |
| `Models.cs` + `Models.PlayerData.cs` | `Equipment`、`MonsterData`、`LogEntry` 与各枚举留在 `Models.cs`；`PlayerData`（存档根对象，约 490 行）已拆到独立文件。 |
| `BattleSystem.cs` | 战斗 tick：攻击间隔、暴击、技能触发、Buff 计时（`Tick(dt)`）。 |
| `Skills.cs` + `SkillCatalog.Loader.cs` | `SkillCatalog.All` 是代码侧基准（15 个技能）；`Loader` 从 `Assets/Resources/skills.json` 覆盖，**可热改不重编译**。坏档（JSON 解析失败 / 条目数为 0 / id 与枚举顺序不符）整表忽略并回退默认值。 |
| `LootSystem.cs` | 掉落生成、品质轮盘、词条掷点、装备评分入口。 |
| `EquipmentScore.cs` | 装备评分 = 增量期望 DPS + 有效生命/8。必须是**增量口径**（相对同槽位空着）。 |
| `Stages.cs` | `StageDef` / `StageCatalog`：11 章 × 10 关程序化生成，关卡推进与首通奖励公式的唯一来源。 |
| `SaveSystem.cs` | `PlayerPrefs` 存档，**3 个存档槽**（`SlotCount = 3`，槽 0 沿用旧 key），版本迁移按版本号分支（不用哨兵值）。 |
| `GameConfig.cs` | `static partial`：标量 + 公式（可热改）。 |
| `GameConfig.Tables.cs` | `static partial`：31 个数组表（**改 json 无效**，只有标量能热改）。 |
| `GameConfig.Loader.cs` | `static partial`：反射按名加载 json，名字对不上逐条警告。 |
| `Assets/Editor/LateGameProbe.cs` | 平衡探针（20 个入口）。历史实验集合，部分已过期。 |
| `Assets/Editor/BalanceRegression.cs` | 平衡回归测试（静态哨兵 + **新档过渡轨迹** + 巅峰胜率 + 章节胜率），菜单 `Tools/平衡回归/`；静态哨兵跑一次约 1.2 秒，过渡轨迹只占其中约 20ms。 |

**拆分后的体量分布**：`GameController.cs` 1250 行仍是最大单文件；`GameUI` 三份合计 1873 行（828 / 535 / 510）；`Models` 两份合计 964 行（470 / 494）。上帝类已拆，但 `GameController` 仍是单文件，未来若要拆同样用 partial。

## 原始 12 步路线图与进度

分五个阶段（P0–P4），按依赖顺序排列。每步的具体内容如下。

**P0 · 工程基础**

- **步骤 1：配置外部化** —— `GameConfig` 拆成 JSON，调平衡不用重编译。✅ 完成（拆成 3 个 partial）

**P1 · 核心循环闭环**

- **步骤 2：装备强化（+1~+15）** —— 金币出口，每级加主属性、消耗递增。✅ 完成（每级 +3%、上限 +15、满级 1.45×；**只加主属性，暴击/暴伤不受影响**；费用随品质递增）。专题文档 `bac0e7a8-2b43-4d5e-a78f-41ce570585c4`
- **步骤 3：装备洗练（重铸副词条）** —— 花金币重掷副词条，是唯一能让玩家主动追求装备的机制。实现提示：`LootSystem.RollAffixes(e, level)` 已是独立静态函数，直接复用。⬜ 未开始
- **步骤 4：关卡/章节推进** —— 每 10 级一章，击败关底解锁下一章，解决「无限刷同级怪、没有进度感」。✅ 完成（拆成 4a→4d）
- **步骤 5：技能三选一** —— 巅峰每 10 级不再随机发技能，改为三选一，收敛方差。✅ 完成

**P2 · 战斗可读性**

- **步骤 6：Buff/Debuff 图标栏** —— 狂暴、护盾余量、濒死爆发窗口。✅ 完成
- **步骤 7：伤害飘字** —— 暴击/技能/普攻用不同颜色。✅ 完成
- **步骤 8：实时 DPS 面板** —— 复用探针里的 `ExpectedDps()`。✅ 完成

**P3 · 长线目标（约 2~3 天）**

- **步骤 9：成就系统** —— 击杀数、满级、全技能集齐、无药通关等。⬜ 未开始
- **步骤 10：转生** —— 重置等级换永久加成，给满级玩家一个重开的理由。⬜ 未开始
- **步骤 11：无尽模式 + 最高层记录** —— 把现在「无限刷同级怪」改造成「无限加难度」。⬜ 未开始

**P4 · 表现层（需要美术资源）**

- **步骤 12：音效 / 2D 精灵 / 技能特效** —— 与前四个阶段性质不同：前四阶段纯代码能做完，这一步必须先有美术决策（用什么风格、要不要买素材包）。⬜ 未开始

**已顺手补做**（原路线图未单列）：护甲穿透属性、装备评分改增量期望 DPS、暴击伤害改乘算、章节差异化（族类名 + 属性偏向）、12 项代码质量优化、主角立绘、多存档槽、自动化平衡回归测试、装备强化。

**原路线图里提过、但仍未做的功能缺口**：装备只有武器+衣服两个槽位，深度不足（建议加头盔/鞋/饰品）。~~技能表未外置~~ 与 ~~中间章节胜率未测~~ 均已完成。

## 关卡/章节制（步骤 4，取代旧的「按击杀计数刷精英/Boss」）

- 11 章 × 10 关；第 c 章怪 **Lv.10(c−1)+1 ~ 10c**（1-1=Lv.1，11-10=Lv.110）。怪级**只由关卡决定，不跟主角等级**。
- 怪物经验随怪级线性抬高：`MonsterExpReward × ChapterExpFactor(lv) = 1 + (lv−1)×0.07`（Lv.110 约 8.6 倍）。没有这条，二次方升级曲线会让主角只到 Lv.66 就撞上 Lv.110 的墙。
- 击杀目标按章递增：1~3 章 3 只、4~6 章 4 只、7~9 章 5 只、10 章起 6 只。
- 普通关关底是**精英**，章末关（x-10）是 **Boss**；第 1 章章末也只给精英（`FirstBossChapter=2`）。
- 阵亡 = 本关重来（`stageKillCount` 归零），但按「已打掉血量占比」给该怪部分经验（`HandlePlayerDied` 里的 `partialExp`）。给全额会变成送死刷经验，不给则新档死锁。
- 首通奖励纯金币：`120 + 80 × (chapter-1)`，章末再 `+600`。公式唯一来源 `StageCatalog.FirstClearGold`。
- 章节差异化：每章有族类名（`ChapterMonsterNames`，如第 3 章骷髅卫士/弓手/法师）与属性偏向 `ChapterStatBias`（生命/攻击/防御/速度四列）。**第 1 章与第 11 章刻意全 1**——第 11 章是终局，已有的 Lv.110 巅峰胜率实测表就是按它算的，必须恒等；第 1 章是教学基准。中间章幅度压在 ±25% 内，不盖过等级主轴。
- 已知未解决：**关卡怪级固定、没有软上限**（用户明确决定不做）；玩家等级远超关卡怪级时几乎秒杀。

## 2026-10-17 新档可玩性修复（重要：改数值前先看）

新档 1-1 一度是**硬停**：1-1 的怪是 Lv.10，主角 Lv.1 空身，一只要打 42 击≈56 秒而自己 5.4 秒就死；阵亡不给经验、清空进度、复活刷满血怪 → 无自救路径。根因不是手感，是内容曲线与成长曲线错位（内容按章 10c 起，成长从 Lv.1 起）。

三层修复（均已落地并验证）：

1. **内容曲线**：`Stages.cs` 的怪级改成 `10(c−1)+s`，`GameConfig.ChapterOfMonsterLevel` 同步改成向上取整（否则 Lv.11 仍算第 1 章，怪物名与章节偏向全错）。
2. **经验补偿**：`ChapterExpScalePerLevel=0.07` + `ChapterExpFactor(monsterLevel)`，乘进 `MonsterExpReward`。
3. **阵亡保底**：按血量占比给部分经验。

**验证工具**：新增 `Assets/Editor/NewGameProgression.cs`（菜单 Tools/测试/新档进度探针，或 `NewGameProgression.Run(seed, maxAttempts, verbose)`）——从 `new PlayerData()` 起步逐关打到底，反馈「能不能玩」；`LateGameProbe` 只测各章稳态，天然看不见这类问题。改关卡强度/经验/掉落权重后两个探针都要跑。详细数据见 `_balance/README.md`（seed 12345：修复前卡 1-1，修复后 1-1 → 11-10 通关，第 11 章 26 次阵亡）。

**2026-10-17 补：它现在是回归门禁的一部分**——`NewGameProgression.Trace(seed, attempts)` 把逐章轨迹（`ch{章}|cleared|endLevel|attempts|deaths|seconds`）写进 `_balance/baseline.json` 的 `transition` 小节，`BalanceRegression.RunStaticOnly()` 每次都会按容差比对（`endLevel` ±2、`attempts`/`deaths` ±max(2/3,30%)、`seconds` ±35%）；`NewGameProgression.CheckInvariants(SimResult)` 另给一套**不依赖基线**的硬约束：不许卡死、每章末等级 ≥ 下一章怪级下限 `10(c-1)+1`、终局 Lv.≥95、阵亡 ≤60 次。整轮模拟 ~20ms，所以毫秒级哨兵带得起。设计原委见 `cc30b461-fc35-4515-a393-f03aacb0d948` 第四节。

## 装备强化（步骤 2）

- 每级 +3% 基础主属性，上限 **+15**，满级 **1.45×**。只作用于主属性：武器 = 攻击；衣服 = 防御 + 生命。**暴击率/暴伤等词条完全不受影响**。
- 费用 = `(EnhanceBaseCost 150 + EnhanceCostPerLevel 300 × 目标等级) × 品质系数`，品质系数 `{ 0.6, 0.8, 1.0, 1.3, 1.6 }`（普通→传说）。
- `Equipment` 的原始 `attack`/`defense`/`hp` **不被改写**，强化通过 `EnhancedAttack`/`EnhancedDefense`/`EnhancedHp` 派生暴露；`PlayerData.Attack/Defense/MaxHp` 与 `Equipment.StatLine` 全部读派生值。
- **坑**：`GameUI` 的装备评分缓存**按对象引用比较**，而强化是就地改 `enhanceLevel` —— 缓存键必须纳入 `enhanceLevel`，否则评分不刷新。

## 关键设计约定（改动时别破坏）

- **`CreateRect(Transform parent, string name, float x, float y, float w, float h)` 的 y 轴向下为正**，内部 `rt.anchoredPosition = new Vector2(x, -y)`。飘字等需要向上运动的元素要手工取负，这里出过 bug。
- **`JsonUtility` 不序列化 `HashSet`**，只用 `List<int>`。首通记录 `clearedStages` 就是 `List<int>` + `StageKey(chapter,stage) = chapter*100+stage`。
- **存档迁移按版本号判断**，不能只看字段值——`JsonUtility` 对缺失字段给 0，分不清旧档和真的在第 1 关。当前版本 **v6**。
- **配置热改只对 `GameConfig.cs` 的标量生效**；`GameConfig.Tables.cs` 的数组表改 json 无效（文档注释里已注明）。
- **立绘必须在 `BuildLeftPanel` 里最先建**，否则会盖在文字上面。
- **技能表已外置到 `Assets/Resources/skills.json`**（可热改）；代码默认值仍在 `SkillCatalog.All` 作兜底。
- **面板互斥统一走 `GameController.CloseAllPanels()`**（现有四个面板：技能/选关/存档/强化），新加面板要挂进去，否则会出现两个面板同时开着的状态漏洞。

## 立绘

主角是「蓝色鲸鱼娘」——DeepSeek 的拟人化同人形象（原型是 AI 原创角色「溟月」）。当前用的是 **Seedream（火山方舟）生成的透明底全身立绘**，`dsh-image-gen` 插件出图，`seedreamBackground: transparent` 直接给透明底、无需抠图。

- 成品：`HelloWorldProject/Assets/Resources/HeroPortrait.png`（透明底原图）与 `HeroPortraitFade.png`（边缘径向淡出，面板底纹用），803×1024。
- 接入点：`GameUI.cs` 的 `BuildHeroPortrait`，位置 `(226, 332, 169, 216)`，alpha 0.50。做左面板底纹，不是独立立绘框——左面板内容一直排到 y≈648，塞不下独立框。
- **立绘位置是被左面板控件挤出来的**：y 260~278 是整幅宽的经验条、y 303~329 是实心「强化」按钮，立绘原本跨在 y 226~476，被这两块横切。唯一能整块放下的空白是 y 332~548 这条右半带（上不碰经验条与强化按钮，下不碰金币行 y 516 与统计行 y 548）。改这个坐标前先核对左面板 y 占位表，动一下就会被切。
- 生成用的提示词与参考图留在 `E:\pytest\unitydsh1\_art\`（`gen_prompt.txt` / `ref_dafeiyu_sheet.png` / `whale_girl_stand_alpha.png`）。
- **用户不接受程序化代码绘制的人物立绘**（评价「太丑了」）。

## 操作教训（省时间）

- **改脚本后必须 `unity command editor_stop` 再 `AssetDatabase.Refresh`**：Play 模式下 refresh 不一定重编译，会误判「改动没生效」。
- 编辑 `.cs` 偶发 `Error: ReplaceFileW EIO (Win32 1175)`（Unity 正在写同名文件），等 4–6 秒重试。
- **判据用 `console_status` 的 `compilationFailed` / `consoleErrors`**，不要凭 `recompile` 的 `up_to_date` 下结论。
- **`unity status` 的 `state: ready` 才算连上**；Unity 没启动（掉进程）时命令会回「You can check available instances with: unity pipeline list」，先 `unity open HelloWorldProject` 再用 `unity status --until-ready --timeout N` 等（参数是 `--timeout` 不是 `--timeout_s`）。
- **`unity command eval` 的代码里只要出现 `if` 语句，就会报 `Unreachable code detected` 并整体失败**（Roslyn 脚本模式的误报，`gc.Log.Clear()`、`gc.Monster.hp = 1` 这类也触发过），而且它会盖住真实错误行号 —— 别按报错行去改，改走无分支写法（三元、连续赋值、直接调用），或干脆写菜单项。`Log` 是只读 `IReadOnlyList<LogEntry>`，`LogEntry` 的字段是 `message` 不是 `text`。
- **Unity 导入 PNG 默认 `spriteImportMode=Multiple`**，必须显式改 `Single`，否则 `Resources.Load<Sprite>` 拿不到完整贴图。
- **Editor 程序集（`Assets/Editor/`）里的 MenuItem 入口走 `unity command menu -- --path "Tools/..."`**，结果去 `unity command console` 读。用 `eval` 调它的**静态方法**是可行的（`typeof(AutoBattle.EditorTools.BalanceRegression).GetMethod("CollectFormulas", ...)` 有效），但 `System.Type.GetType("...Assembly-CSharp-Editor")` 找不到类型（Unity 懒加载 Editor 程序集），别用那条路。
- **`unity command refresh_assets` 不存在**（400 Command Not Found）。外部写入 .cs 后用 `eval` 跑 `UnityEditor.AssetDatabase.Refresh(UnityEditor.ImportAssetOptions.ForceUpdate)` 触发导入，再 `recompile`。
- **菜单 `Tools/平衡回归/跑一次比对` 已经同时覆盖巅峰胜率与章节胜率，约 2 分钟**；改完代码顺手跑用「只跑静态哨兵（毫秒级）」。读 console 结果要按 `timestampUtc` 取**最新**一条，旧日志会一起返回。

## 既有专题文档索引

- **技能表外置（缺口 1）+ 章节胜率探针（缺口 2）+ 颜色常量表 + 上帝类拆分**：`80f2ed5c-7d2a-42e0-81fd-72c0c442d3ae`
- **装备强化（步骤 2）**：`bac0e7a8-2b43-4d5e-a78f-41ce570585c4`
- **多存档槽 + 自动化平衡回归测试**：`cc30b461-fc35-4515-a393-f03aacb0d948`
- 4a 关卡/章节制改版（`StageCatalog` 规则、存档 v5、阵亡重打）：`418948ff-876f-4f76-90af-50e55a5c8727`
- 4b 选关面板（章节级解锁、`SelectStage`/`IsStageUnlocked`、面板冻结战斗）：`86f3fe99-d412-41f3-a55b-12811d5c3288`
- 4c 首通奖励（存档 v6、横幅、`StageClearDelay` 接线）：`763c8adf-09a9-48af-9ac6-607a7011a53c`
- 4d 章节进度总览（奖励公式收敛、已领金币按当前规则重算）：`cf559a4d-9cb4-48a2-9691-1776e16deaf8`
- 技能三选一：`bd713554-0919-4cdf-8426-557fc4ba4e4a`
- 技能获取与 100 级后胜率：`a68e8f3d-4799-4bff-8ae2-942a12bae043`

## 当前待办与开放问题

- **金币溢出部分缓解**：首通奖励池 63,800 金币，金币出口原有药水（320/瓶）；**步骤 2 装备强化已落地为第一个大额出口**（满级单件约 3.9 万金币）。**步骤 3（装备洗练）是下一个自然出口。**
- 关卡怪级无软上限（用户决定不做）。
- **中间章节胜率已补测并纳入基线**：探针新增 `LateGameProbe.RunChapterWinrate` / `RunChapterWinrateRow` / `BuildForChapter`，菜单 `Tools/平衡回归/章节胜率表（缺口 2）`。测出来两个问题：第 1~10 章精英 100%、Boss 98~99.9%（毫无挑战）；第 11 章断崖到精英 1.3% / Boss 0%。口径是零强化零巅峰裸配装，所以第 11 章是下界。基线新增 `chapterWinrate`（11 章 × 2 格，采样 6×20，容差 5pp）。
- **回归基线已覆盖强化**（原有缺口已补）：`_balance/baseline.json` 静态哨兵新增 57 条强化采样 —— `EnhanceBonusPerLevel` / `EnhanceMaxLevel` / `EnhanceBaseCost` / `EnhanceCostPerLevel` / `EnhanceQualityFactors[0..4]`、`EnhanceCost(lv,q)` 的 4 品质 × 4 档、`Enhanced*` 派生属性 5 档、满级边界两条。哨兵条目总数 52 → 109；后又加入章节胜率 22 格，**基线现为 9742 字节**。改强化参数会立刻被回归抓住（已用篡改基线法反向验证）。
- 12 项优化已全部完成（含拆 `GameConfig` 为 3 个 partial、UI 直接写状态 5 处收进控制器）。
- **12 项之外的代码质量遗留已全部清零**：`HandleMonsterKilled()` 已拆成 6 个方法；`GameUI` / `PlayerData` 上帝类已用 partial 拆开；169 处 `new Color` 已归并为 `GameConfig.Colors.cs` 的 64 条语义色；`HelloWorldSpinner.cs` 的日志已包进 `#if UNITY_EDITOR || DEBUG`。
- **版本控制已就位**：`E:\pytest\unitydsh1` 已是 git 仓库并推送到远程 `origin/main`。注意 SSL 需 `http.sslBackend schannel`（见 git 相关记录）。`.mnemon/` 运行时数据（`MEMORY.md`/`USER.md`/`memories.json`/`documents/index.json`）目前**仍被 git 跟踪**，如需排除要另行处理。
- **提交纪律**：用户要求「以后我让你提交你再提交」——未经明确许可不得执行 `git commit` / `git push`。
- **2026-10-17 全项目审查（未修项）**：除上面的新档硬停外，还核实了这些——终局 11-10 每杀一只都会重入通关分支（`AdvanceStage` 返回 false 导致 `stageKillCount` 不归零，每只怪都刷一次通关横幅 + 3 秒 `StageClearDelay`）；暂停/继续会无条件把状态机踢回 `WaitingRespawn`（从商店继续会开新商店并重置计时，三选一期间继续会吞掉这次选择）；`EquipmentScore.Of` 用「临时换上再还原」的方式算分，异常逃出就永久丢装备；UI 侧日志满 60 条后飘字与实时 DPS 永久失效（`GameUI.Core.cs` 用 `Log.Count` 差值判断新增，而 `TrimLog` 让它恒等于上限）、选关面板每帧重建、浮层不挡鼠标（空白处点击穿透到「购买」）、技能面板不在冻结白名单等。

---
id: "42b8694e-f097-43db-9f58-9d606e02cfd1"
title: "Unity AutoBattle 项目现状总览与接手指南（新会话入口）"
description: "HelloWorldProject 挂机自动战斗游戏的全局地图：代码文件职责、原始 12 步路线图（每步具体内容）与进度、关键设计约定与踩坑教训、既有 Mnemon 文档索引、当前待办。新会话开始开发前先读这份，再按索引跳到对应专题文档。"
status: "active"
created_at: "2026-10-15T18:23:22.330Z"
updated_at: "2026-10-16T03:30:06.010Z"
content_hash: "eba2aab0b7db0d059e9ebd3c17f7158477ffa444dd0316dd27dd8c175139d6be"
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
| `GameUI.cs` | 全程序化构建 uGUI（无 Prefab）。左面板角色属性/装备/金币/技能栏，右面板怪物/日志/商店。 |
| `Models.cs` | `Equipment`、`PlayerData`、`MonsterData`、`LogEntry` 与各枚举。`PlayerData` 是存档根对象。 |
| `BattleSystem.cs` | 战斗 tick：攻击间隔、暴击、技能触发、Buff 计时（`Tick(dt)`）。 |
| `Skills.cs` | `SkillCatalog.All`：15 个技能的硬编码定义（**技能表尚未外置到 json**，见「待办」）。 |
| `LootSystem.cs` | 掉落生成、品质轮盘、词条掷点、装备评分入口。 |
| `EquipmentScore.cs` | 装备评分 = 增量期望 DPS + 有效生命/8。必须是**增量口径**（相对同槽位空着）。 |
| `Stages.cs` | `StageDef` / `StageCatalog`：11 章 × 10 关程序化生成，关卡推进与首通奖励公式的唯一来源。 |
| `SaveSystem.cs` | `PlayerPrefs` 存档，版本迁移按版本号分支（不用哨兵值）。 |
| `GameConfig.cs` | `static partial`：标量 + 公式（可热改）。 |
| `GameConfig.Tables.cs` | `static partial`：31 个数组表（**改 json 无效**，只有标量能热改）。 |
| `GameConfig.Loader.cs` | `static partial`：反射按名加载 json，名字对不上逐条警告。 |
| `Assets/Editor/LateGameProbe.cs` | 平衡探针（20 个入口）。历史实验集合，部分已过期。 |

**三个文件占了一半体量**：`Models.cs` + `GameController.cs` + `GameUI.cs`。

## 原始 12 步路线图与进度

分五个阶段（P0–P4），按依赖顺序排列。每步的具体内容如下。

**P0 · 工程基础**

- **步骤 1：配置外部化** —— `GameConfig` 拆成 JSON，调平衡不用重编译。✅ 完成（拆成 3 个 partial）

**P1 · 核心循环闭环**

- **步骤 2：装备强化（+1~+15）** —— 金币出口，每级加主属性、消耗递增。实现提示：`Equipment` 加 `enhanceLevel` 字段，`EquipmentScore` 会自动把它算进评分，**自动换装逻辑不用改**。⬜ 未开始
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

**已顺手补做**（原路线图未单列）：护甲穿透属性、装备评分改增量期望 DPS、暴击伤害改乘算、章节差异化（族类名 + 属性偏向）、12 项代码质量优化、主角立绘。

**原路线图里提过、但未列入 12 步的功能缺口**：装备只有武器+衣服两个槽位，深度不足（建议加头盔/鞋/饰品）；多存档槽；自动化平衡回归测试（探针已有，可做成 headless 批量）。

## 关卡/章节制（步骤 4，取代旧的「按击杀计数刷精英/Boss」）

- 11 章 × 10 关；第 N 章怪 **Lv.10N**（1-1=Lv.10，11-10=Lv.110）。怪级**只由关卡决定，不跟主角等级**。
- 击杀目标按章递增：1~3 章 3 只、4~6 章 4 只、7~9 章 5 只、10 章起 6 只。
- 普通关关底是**精英**，章末关（x-10）是 **Boss**；第 1 章章末也只给精英（`FirstBossChapter=2`）。
- 阵亡 = 本关重来（`stageKillCount` 归零）。
- 首通奖励纯金币：`120 + 80 × (chapter-1)`，章末再 `+600`。公式唯一来源 `StageCatalog.FirstClearGold`。
- 章节差异化：每章有族类名（`ChapterMonsterNames`，如第 3 章骷髅卫士/弓手/法师）与属性偏向 `ChapterStatBias`（生命/攻击/防御/速度四列）。**第 1 章与第 11 章刻意全 1**——第 11 章是终局，已有的 Lv.110 巅峰胜率实测表就是按它算的，必须恒等；第 1 章是教学基准。中间章幅度压在 ±25% 内，不盖过等级主轴。
- 已知未解决：**关卡怪级固定、没有软上限**（用户明确决定不做）；玩家等级远超关卡怪级时几乎秒杀。

## 关键设计约定（改动时别破坏）

- **`CreateRect(Transform parent, string name, float x, float y, float w, float h)` 的 y 轴向下为正**，内部 `rt.anchoredPosition = new Vector2(x, -y)`。飘字等需要向上运动的元素要手工取负，这里出过 bug。
- **`JsonUtility` 不序列化 `HashSet`**，只用 `List<int>`。首通记录 `clearedStages` 就是 `List<int>` + `StageKey(chapter,stage) = chapter*100+stage`。
- **存档迁移按版本号判断**，不能只看字段值——`JsonUtility` 对缺失字段给 0，分不清旧档和真的在第 1 关。当前版本 **v6**。
- **配置热改只对 `GameConfig.cs` 的标量生效**；`GameConfig.Tables.cs` 的数组表改 json 无效（文档注释里已注明）。
- **立绘必须在 `BuildLeftPanel` 里最先建**，否则会盖在文字上面。
- **技能表的数值尚未外置**，全硬编码在 `Skills.cs` 的 `SkillCatalog.All`。

## 立绘

主角是「蓝色鲸鱼娘」——DeepSeek 的拟人化同人形象（原型是 AI 原创角色「溟月」）。当前用的是 **Seedream（火山方舟）生成的透明底全身立绘**，`dsh-image-gen` 插件出图，`seedreamBackground: transparent` 直接给透明底、无需抠图。

- 成品：`HelloWorldProject/Assets/Resources/HeroPortrait.png`（透明底原图）与 `HeroPortraitFade.png`（边缘径向淡出，面板底纹用），803×1024。
- 接入点：`GameUI.cs` 的 `BuildHeroPortrait`，位置 `(198, 226, 196, 250)`，alpha 0.50。做左面板底纹，不是独立立绘框——左面板内容一直排到 y≈648，塞不下独立框。
- 生成用的提示词与参考图留在 `E:\pytest\unitydsh1\_art\`（`gen_prompt.txt` / `ref_dafeiyu_sheet.png` / `whale_girl_stand_alpha.png`）。
- **用户不接受程序化代码绘制的人物立绘**（评价「太丑了」）。

## 操作教训（省时间）

- **改脚本后必须 `unity command editor_stop` 再 `AssetDatabase.Refresh`**：Play 模式下 refresh 不一定重编译，会误判「改动没生效」。
- 编辑 `.cs` 偶发 `Error: ReplaceFileW EIO (Win32 1175)`（Unity 正在写同名文件），等 4–6 秒重试。
- **判据用 `console_status` 的 `compilationFailed` / `consoleErrors`**，不要凭 `recompile` 的 `up_to_date` 下结论。
- **`unity status` 的 `state: ready` 才算连上**；Unity 没启动（掉进程）时命令会回「You can check available instances with: unity pipeline list」，先 `unity open HelloWorldProject` 再用 `unity status --until-ready --timeout N` 等（参数是 `--timeout` 不是 `--timeout_s`）。
- 探针脚本里 `gc.Log.Clear()`、`gc.Monster.hp = 1` 这类会报 `Unreachable code detected`，且会盖住真实错误行号。`Log` 是只读 `IReadOnlyList<LogEntry>`，`LogEntry` 的字段是 `message` 不是 `text`。
- **Unity 导入 PNG 默认 `spriteImportMode=Multiple`**，必须显式改 `Single`，否则 `Resources.Load<Sprite>` 拿不到完整贴图。

## 既有专题文档索引

- 4a 关卡/章节制改版（`StageCatalog` 规则、存档 v5、阵亡重打）：`418948ff-876f-4f76-90af-50e55a5c8727`
- 4b 选关面板（章节级解锁、`SelectStage`/`IsStageUnlocked`、面板冻结战斗）：`86f3fe99-d412-41f3-a55b-12811d5c3288`
- 4c 首通奖励（存档 v6、横幅、`StageClearDelay` 接线）：`763c8adf-09a9-48af-9ac6-607a7011a53c`
- 4d 章节进度总览（奖励公式收敛、已领金币按当前规则重算）：`cf559a4d-9cb4-48a2-9691-1776e16deaf8`
- 技能三选一：`bd713554-0919-4cdf-8426-557fc4ba4e4a`
- 技能获取与 100 级后胜率：`a68e8f3d-4799-4bff-8ae2-942a12bae043`

## 当前待办与开放问题

- **金币溢出未解决**：首通奖励池 63,800 金币，而金币唯一出口是药水（320/瓶），关卡制不解决溢出。**步骤 2（装备强化）/3（装备洗练）是金币出口的自然去处。**（2026-10-16 用户重新盘点剩余任务，此前记录的「用户否决回主线做步骤 2/3」已不再适用。）
- **工程没有版本控制**：`git rev-parse --is-inside-work-tree` 报 `fatal: not a git repository`。6500 行代码 + 全部进度无备份，建议尽快 `git init` + `.gitignore`（排除 `Library/`、`Temp/`、`obj/`、`Logs/`、`dsh-image-gen/`、`_archive_screenshots/`）。
- 关卡怪级无软上限（用户决定不做）。
- 技能表未外置 json。
- 中间章节（第 3–9 章）的胜率**从未测过**：现有平衡数据是「Lv.100 传说套打 Lv.100/Lv.110 怪」的终局快照，探针没有「按章节配装」的入口。
- 12 项优化已全部完成（含拆 `GameConfig` 为 3 个 partial、UI 直接写状态 5 处收进控制器）。
- 12 项之外的代码质量遗留：`HandleMonsterKilled()` 182 行未拆；`GameUI`（1450 行）与 `PlayerData` 上帝类未拆（建议玩法稳定后再做）；108 处 `new Color` 未抽常量；`HelloWorldSpinner.cs` 在生产构建里每秒 `Debug.Log`。

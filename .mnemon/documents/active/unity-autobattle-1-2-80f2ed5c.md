---
id: "80f2ed5c-7d2a-42e0-81fd-72c0c442d3ae"
title: "Unity AutoBattle 收尾三件套：技能表外置（缺口 1）、章节胜率探针（缺口 2）、上帝类拆分与颜色常量表"
description: "用户 m06081「完成功能缺口 1和2 + 代码质量遗留都处理好」的完整交付记录：skills.json 热改机制与三类坏档回退、LateGameProbe 章节胜率探针与第 1~11 章实测表（含第 11 章断崖）、GameConfig.Colors 64 条语义色、GameUI/PlayerData partial 拆分、HandleMonsterKilled 拆成 6 个方法、eval 的 CS0162 坑。改这些文件前先读本篇。"
status: "active"
created_at: "2026-10-16T15:29:49.216Z"
updated_at: "2026-10-16T15:29:49.216Z"
content_hash: "ba70cbccbb5291692db315fc46135c4d2873f1441737e8f002f75dfca29aa63f"
source_paths:
  - "HelloWorldProject/Assets/Scripts/SkillCatalog.Loader.cs"
  - "HelloWorldProject/Assets/Editor/SkillTableVerify.cs"
  - "HelloWorldProject/Assets/Editor/LateGameProbe.cs"
  - "HelloWorldProject/Assets/Editor/BalanceRegression.cs"
  - "HelloWorldProject/Assets/Scripts/GameConfig.Colors.cs"
  - "HelloWorldProject/Assets/Scripts/GameUI.cs"
  - "HelloWorldProject/Assets/Scripts/GameUI.Panels.cs"
  - "HelloWorldProject/Assets/Scripts/GameUI.Core.cs"
  - "HelloWorldProject/Assets/Scripts/Models.PlayerData.cs"
  - "HelloWorldProject/Assets/Resources/skills.json"
session_ids:
  - "session-820fee2d-3ea4-4964-9f54-6440131b1026"
memory_body_ids:
  []
---

# 收尾三件套：技能表外置 + 章节胜率 + 代码质量遗留清零

对应任务：用户 m06081 原话「1.完成功能缺口 1和2  2.代码质量遗留都处理好」。全部完成，平衡回归 PASS。

## 一、功能缺口 1：技能表外置

数值从 `Skills.cs` 的硬编码搬到 `HelloWorldProject/Assets/Resources/skills.json`，改数值不用重编译。

- **`HelloWorldProject/Assets/Scripts/SkillCatalog.Loader.cs`**：`ApplyJson(string)` 在 81 / 87 / 105 行分别报三类告警 —— JSON 解析失败 / 条目数为 0 / id 与枚举顺序不符。**三种情况都整表忽略、回退 `SkillCatalog.All` 的代码默认值**，不做部分应用。
- **`HelloWorldProject/Assets/Scripts/Skills.cs`** 的 `SkillCatalog.All` 仍是代码侧基准与兜底。
- **`HelloWorldProject/Assets/Editor/SkillTableVerify.cs`**（菜单 `Tools/技能表验证/跑一遍` 与 `导出当前技能表到 json`）。这是验证手段，不是给玩家用的。
- 自检输出全绿：`ConfigLoaded=True / ConfigSkillCount=15`，15 条与代码默认值逐条一致，`SkillDef 仍存在 extra 字段：False`，覆盖测试 `2.5 -> applied=15 -> 9.99`，非法结构 / 空数组 / id 错位三个异常用例均返回 0，`Reload：True`。
- **热改测试的坑（测试自身缺陷，不是功能 bug）**：首版用 `"value": 0.08,` 做探针，但迅捷与狂暴之心的值恰好都是 0.08，`string.Replace` 把两条一起改了，输出出现误导性的「狂暴之心 0.99（应仍为 0.08）」。**教训：热改测试探针必须挑在文件里唯一的数值。**

## 二、功能缺口 2：章节胜率探针

原探针只会「Lv.100 传说套打 Lv.100/Lv.110 怪」，中间章节从未测过。

- **`HelloWorldProject/Assets/Editor/LateGameProbe.cs`** 新增（已补 `using UnityEditor;`）：
  - `public static float[] RunChapterWinrateRow(int chapter, int perType, bool resetHp, int trials, out float avgQuality, out int[] accWin)` —— 返回两格胜率（`[0]` 精英 / `[1]` Boss），已除以 trials；`accWin[3]` 只累加 Boss 档的逐类型胜场。
  - `public static string RunChapterWinrate(int perType, bool resetHp, int trials)` / `RunChapterWinrateCell(...)`
  - `static PlayerData BuildForChapter(int chapter)`
  - `[MenuItem("Tools/平衡回归/章节胜率表（缺口 2）")]`
- **`BuildForChapter` 的口径**：玩家等级 = 该章怪级（`Mathf.Min(ChapterMonsterLevelStep * chapter, MaxLevel)`）、巅峰固定 0 级、装备按**精英掉落权重** `MonsterTierDropQualityWeights[(int)MonsterTier.Elite]` 抽武器与衣服各一件。用精英权重而非普通怪 `QualityWeights` 的理由：普通怪 62.5% 出普通品质，玩家可用装备其实来自精英与 Boss，用最保守权重会得出「所有中期章节都必败」，没有诊断价值。**注意不能用 `LootSystem.CreateEquipmentAtLevel`，它随机槽位。**
- 每章打精英 + Boss 两档，档内按 `MonsterType` 加权 战 45% / 弓 30% / 盗 25%（`float[] w = { 0.45f, 0.30f, 0.25f }`）。

### 实测全表（perType=30, resetHp=true, trials=12，约 150 秒）

```
章节  怪级   装备品质   精英胜率    Boss胜率    (Boss 逐类型)
   1    10       2.25      100.0%      98.0%     战 100.0% / 弓 100.0% / 盗 91.9%
   2    20       2.58      100.0%      98.7%     战 100.0% / 弓 100.0% / 盗 94.7%
   3    30       2.17      100.0%      99.0%     战 100.0% / 弓 100.0% / 盗 95.8%
   4    40       2.33      100.0%      99.4%     战 100.0% / 弓 100.0% / 盗 97.5%
   5    50       2.67      100.0%      99.9%     战 100.0% / 弓 100.0% / 盗 99.4%
   6    60       2.50      100.0%      99.7%     战 100.0% / 弓 100.0% / 盗 98.6%
   7    70       2.42      100.0%      99.9%     战 100.0% / 弓 100.0% / 盗 99.4%
   8    80       2.17      100.0%      99.9%     战 100.0% / 弓 100.0% / 盗 99.7%
   9    90       2.33      100.0%      99.9%     战 100.0% / 弓 100.0% / 盗 99.7%
  10   100       2.42      100.0%      99.8%     战 100.0% / 弓 100.0% / 盗 99.2%
  11   110       2.67        4.9%       0.0%     战 0.0% / 弓 0.0% / 盗 0.0%
```

**两个结论（未擅自改数值，留给产品判断）：**
1. **第 1~10 章玩家不会输**：精英全 100%、Boss 98.0~99.9%，逐类型最差也有 91.9%（第 1 章盗贼）。全程没有紧张感。
2. **第 11 章断崖**：从 99.8% 直接砸到精英 4.9% / Boss 0%，三类型全灭。口径是**零强化、零巅峰、零技能**的裸配装，所以这是**下界**而非实际值；但怪 Lv.110 超出玩家 `MaxLevel=100` 整 10 级，这个终局墙是设计意图还是过头，需产品判断。

### 纳入回归基线

- `BalanceRegression.Report` 新增 `public float[] chapterWinrate;`（11 章 × 2 格 = 22）、`public int chapterTrials;`、`public int chapterPerType;`。
- 常量：`ChapterTrials = 6`、`ChapterPerType = 20`、`ChapterWinrateTolerance = 0.05f`。每格只有 6 × 20 = 120 场，标准差约 4.6%，所以容差必须放到 5 个百分点 —— **它的职责不是逐点盯数值，而是抓住「某章从 0% 变成 50%」这种结构性变化。**
- `CollectChapterWinrate(perType, trials)` 每章 `UnityEngine.Random.InitState(DefaultSeed + ch * 31)`，理由与 `CollectWinrate` 相同：改一章不该让别的章报红。
- `DiffChapterWinrate` 对旧基线（无该字段）输出「基线里还没有这张表 —— 跑一次『把当前状态写成新基线』即可纳入」并返回 0（不误报）。
- `RunStaticOnly` 不受影响，仍是毫秒级。
- **`_balance/baseline.json` 现为 9742 字节**（加章节胜率前是 9284 字节，加强化前是 9089 字节）。

## 三、代码质量遗留清零

### 3.1 颜色常量表（原「108 处 `new Color` 未抽」）

实际家底是 **193 处字面量、139 个 distinct 值**，分布 `GameUI.cs` 123 / `GameController.cs` 48 / `Skills.cs` 15 / `GameConfig.Tables.cs` 5 / `BattleSystem.cs` 2。**真正的病因不是数量，而是同一语义有多个几乎不可区分的变体**：金黄色 9 个、警告橙 5 个、信息蓝 8 个、禁用灰 5 个。玩家分辨不出 `0.6,1,0.6` 与 `0.6,1,0.7` 的差异 —— 那是噪声不是信息。

- 新建 **`HelloWorldProject/Assets/Scripts/GameConfig.Colors.cs`**（`public static partial class GameConfig`），**64 条语义色常量**，分七组：容器 / 文字 / 条填充 / 按钮 / 强调 / 日志语义 / 飘字。取值原则：**每个语义取该族中出现次数最多的那个字面量**，归并后绝大多数位置像素不变。
- 替换统计：`GameUI.cs` 126 处、`GameController.cs` 43 处、`BattleSystem.cs` 4 处。
- 替换手法：PowerShell 精确字面量替换，先备份到 `E:\pytest\unitydsh1\_backup_colors\`。**坑：哈希表字面量不能有重复键**，首版脚本因 `'new Color(0.5f, 0.5f, 0.56f)'` 写了两次而 ParserError 整体不执行；改用 `@(@('0.5f, 0.5f, 0.56f','TextDisabled'), ...)` 的数组对形式通过。
- **刻意保留的字面量（都有说明注释）**：`Skills.cs` 的 15 个技能辨识色（技能独占色，两技能共用一色就失去辨认意义）、`GameConfig.Tables.cs` 的 `QualityColors`（按品质索引的数据表）、`GameController.cs` 的 `MonsterSpawnColor`/`MonsterTypeColor`（档位/类型辨识色）、`GameUI.cs` 5 处动态色（`def.color * 0.30`、按钮 `highlightedColor` = `1.1f * bgColor` 与 `pressedColor` = `0.8f * bgColor`）。
- 验证：截图与改前完全一致无视觉回归；日志区 12 条不同语义色层次分明。

### 3.2 `HelloWorldSpinner.cs` 的生产日志

它是模板残留的**孤儿脚本** —— guid `b84768d8a3046ca4b89a5bf0e17ebadd` 在 `SampleScene.unity` 里完全没有引用（场景根物体只有 Main Camera / Global Light 2D / AutoBattleDemo），从未挂载。两条 `Debug.Log` 包进 `#if UNITY_EDITOR || DEBUG`。**首版用 `#if UNITY_EDITOR || DEVELOPMENT_BUILD` 触发 warning UAC0009 —— Unity 6 已弃用该宏。**

### 3.3 `SkillDef.extra` 语义重载拆分

同一字段对狂暴是暴击率加成、对反击/连锁是触发概率。已拆成 `crit` / `hpTh` / `trig` 三个具名字段。`CollectSkills()` 的格式串随之从 `...|extra={4}|...` 变成 `...|crit={4}|hpTh={5}|trig={6}|...`，基线一次性重写（数值一一对应：`Berserk extra=0.2` → `crit=0.2`，`Execute extra=0.3` → `hpTh=0.3`）。

### 3.4 `HandleMonsterKilled()` 拆解（原 182 行）

拆成 6 个方法，`HandleMonsterKilled` 只留编排：`ResolveStageProgress` / `GrantKillReward` / `ResolveLevelUp` / `ResolveDrops` / `EnterRespawnWait`。纯搬移（语句序列逐行 diff 一致），Play 三轮运行时验证：`lv=30→32`、`exp=2279/2280→148`、`equipped=4`、`w=稀有 武器 Lv.20`、`cleared 1→12`、`deaths=27`、`state=Fighting`，零 console error。

### 3.5 上帝类拆分

用 `partial` 纯文件拆分，零行为变更：

| 原文件 | 拆成 | 行数 |
|---|---|---|
| `GameUI.cs`（1849） | `GameUI.cs`（字段/生命周期/左右面板与商店构建） | 510 |
| | `GameUI.Panels.cs`（技能三选一/技能/存档/强化/横幅/选关六个浮层 + 它们的事件与刷新） | 828 |
| | `GameUI.Core.cs`（刷新循环、飘字、DPS、日志解析、控件工厂 `CreateRect`/`CreateImage`/`CreatePanel`/`CreateText`/`CreateBar`/`CreateButton`） | 535 |
| `Models.cs`（957） | `Models.cs`（枚举 + `Equipment` + `MonsterData` + `LogEntry`） | 470 |
| | `Models.PlayerData.cs`（`PlayerData`，存档根对象） | 494 |

**切分后必须做**：`GameUI` 的类声明改成 `public partial class GameUI : MonoBehaviour`；每个新文件都要带全套 using（`System.Collections.Generic` / `UnityEngine` / `UnityEngine.UI` / `UnityEngine.EventSystems` / `UnityEngine.Events`）；`PlayerData` 的文档注释要跟着搬到新文件（首版漏了，`Models.cs` 留下孤儿注释块）。

验证：`GameUI=True methods=154`、`PlayerData=True members=135`、Play 截图与拆前完全一致、静态哨兵 PASS、完整回归 PASS。

## 四、环境教训（本轮新增）

- **`unity command eval` 的代码里只要出现 `if` 语句就会报 `Unreachable code detected` 并整体失败**（Roslyn 脚本模式误报），且会盖住真实错误行号 —— 别按报错行去改，改成无分支写法或干脆写菜单项。
- **`unity command refresh_assets` 不存在**（400 Command Not Found）。外部写入 .cs 后用 `eval` 跑 `UnityEditor.AssetDatabase.Refresh(UnityEditor.ImportAssetOptions.ForceUpdate)`，再 `recompile`。
- **`recompile` 报 `up_to_date`（"No scripts needed recompilation"）不代表新文件没被编译**；判据是 `console_status` 的 `compilationFailed` / `consoleErrors`，或 `eval` 里 `System.Type.GetType("AutoBattle.<类名>, Assembly-CSharp")` 看类型在不在。
- Editor 程序集的 MenuItem 走 `unity command menu -- --path "Tools/..."`，结果去 `unity command console` 读；**读结果要按 `timestampUtc` 取最新一条**，旧日志会一起返回（本轮因此误读过一次旧的 FAIL）。
- 用 `eval` 调 Editor 程序集的**静态方法**是可行的，但 `System.Type.GetType("..., Assembly-CSharp-Editor")` 找不到类型（Unity 懒加载 Editor 程序集）。
- 菜单 `Tools/平衡回归/跑一次比对` 现在同时覆盖巅峰胜率与章节胜率，约 2 分钟。

## 五、相关文档

- 项目总览（入口）：`42b8694e-f097-43db-9f58-9d606e02cfd1`
- 装备强化（步骤 2）：`bac0e7a8-2b43-4d5e-a78f-41ce570585c4`
- 多存档槽 + 平衡回归框架：`cc30b461-fc35-4515-a393-f03aacb0d948`

## 六、遗留

- 上帝类已拆，但 **`GameController.cs`（1250 行）仍是最大单文件**，未来要拆同样用 partial。
- **章节胜率测出的两个平衡问题未处理**：第 1~10 章无挑战、第 11 章断崖。留给产品判断。
- 装备仍只有武器 + 衣服两个槽位（原路线图缺口，未做）。
- 路线图未完成：步骤 3 装备洗练、步骤 9 成就、步骤 10 转生、步骤 11 无尽模式、步骤 12 表现层。

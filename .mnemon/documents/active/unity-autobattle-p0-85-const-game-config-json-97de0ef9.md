---
id: "97de0ef9-69eb-4411-aa2b-6829edf03482"
title: "Unity AutoBattle 配置外部化（P0）：85 个 const 迁到 game_config.json + 反射加载与自动重载"
description: "HelloWorldProject 的 P0 配置外部化执行记录：85 个 const 迁到 Assets/Resources/game_config.json，GameConfigData + GameConfig.Apply() 反射覆盖、双加载入口、AssetPostprocessor 自动重载与两个菜单项；含 8 项确定性回归断言、改文件不重编译实测、探针胜率回归，以及 SaveKey 保持 const、30 个 static readonly 数组不外部化的决策理由。"
status: "active"
created_at: "2026-10-14T16:34:03.130Z"
updated_at: "2026-10-14T16:34:03.130Z"
content_hash: "035cd41aaded6d827fe68a304598cfbf37b3c68112de9f057fa2176ada62f826"
source_paths:
  - "HelloWorldProject/Assets/Resources/game_config.json"
  - "HelloWorldProject/Assets/Scripts/GameConfigData.cs"
  - "HelloWorldProject/Assets/Scripts/GameConfig.cs"
  - "HelloWorldProject/Assets/Editor/GameConfigReloader.cs"
  - "HelloWorldProject/Assets/Editor/LateGameProbe.cs"
session_ids:
  - "93098946-8898-4605-bcef-5a39362e0698"
memory_body_ids:
  []
---

# Unity AutoBattle 配置外部化（P0）

工程 `HelloWorldProject/`（Unity 2D，Editor 6000.6.5f1）。本文记录「限制性 P0」的落地：把数值配置从代码常量搬到 JSON，使调数值**不再需要重新编译**，并保持原有游戏行为逐项一致。

相关既有记录（数值内容与调参结论）：第二轮技能平衡 `6231bd5c-ca9e-454e-8f30-c67546e352d7`、第一轮技能改动 `502f3c34-f9de-4354-93d1-ca0844fb51f1`、技能强度审计口径 `2ab3d6dc-ceb7-4405-b908-9cd6b792905d`。本文只讲**配置装载机制**，不重复数值。

## 1. 动机与范围

- 改前：`GameConfig.cs` 有 86 个 `public const`（标量）+ 30 个 `public static readonly` 数组，任何数值调整都要改代码并等 Unity 编译。
- 目标：标量数值外部化为运行时数据文件，改文件即生效；不改变任何既有数值语义。
- 实际外部化 **85 个标量**（86 减去 `SaveKey`，见第 5 节）。
- 30 个 `static readonly` 数组**未外部化**（见第 5 节）。

## 2. 四个文件

| 文件 | 规模 | 职责 |
|---|---|---|
| `HelloWorldProject/Assets/Resources/game_config.json` | 87 行 / 2366 字节 / 85 字段 | 唯一需要日常编辑的数值文件 |
| `HelloWorldProject/Assets/Scripts/GameConfigData.cs` | 99 行 | `[System.Serializable]` 数据载体，字段默认值＝原 `const` 值 |
| `HelloWorldProject/Assets/Scripts/GameConfig.cs` | 524 → 597 行 | 85 个 `const` 改为可覆盖 `static` 字段；新增 `Reload()` / `ApplyJson()` / `ExportJson()` |
| `HelloWorldProject/Assets/Editor/GameConfigReloader.cs` | 55 行 | `AssetPostprocessor` 监听 JSON 导入 → 自动重载；另附两个菜单项 |

`GameConfigData` 的**字段名必须与 `GameConfig` 的静态字段一一对应**，由 `GameConfig.Apply()` 反射覆盖；对不上的字段自动跳过，JSON 缺项时保留 C# 默认值（即原 `const` 值）。这份载体文件是用脚本从 `GameConfig.cs` 生成的（提取 85 字段），避免手抄 85 个字段出错。

加载入口是**双入口**：`static` 构造 + `RuntimeInitializeOnLoadMethod`，因此 Editor 内直接读字段（不进 Play）和 Play 运行时都能拿到配置。

菜单项：`AutoBattle/Reload Game Config`（手动重载）、`AutoBattle/Export Current Config To JSON`（用当前生效值生成新文件）。

## 3. 验证证据（改前后行为一致）

确定性断言——外部化前后 8 个跨系统公式输出完全相同：

| 断言 | 结果 |
|---|---|
| `ExpToNextLevel(99)` | 21117 |
| `AttackInterval(109)` | 0.3101s |
| `DamageReduction(896,110)` | 0.2804 |
| `SellPrice(100, 传说)` | 4832 |
| `PotionPrice(100)` | 520 |
| `TierMonsterLevel(100)` | 110 |
| `SkillLevel(25)` | 3 |
| `ParagonExpToNext(0)` | 2000 |

- **JSON 覆盖生效**：`eval` 读出 `reload=True fields=85 MaxLevel=100 MinAttackInterval=0.2 WeaponCritDamagePerLevel=0.00195 PotionMaxCount=5`。
- **改文件不重编译即生效**：临时把 JSON 改为 `MaxLevel=120 / MinAttackInterval=0.1333 / PotionMaxCount=9`，一次 `AssetDatabase.Refresh()` 后读回 `MaxLevel=120 MinAttackInterval=0.1333 PotionMaxCount=9 每秒上限=7.50`，**未触发脚本编译**；验证后已还原为默认值。
- **自动重载打通**：把 `EliteKillInterval` 由 `10` 改为 `7`，**不调用任何 API**，直接读字段得到 `7`（`AssetPostprocessor` 接住导入事件）；还原后读回 `EliteKillInterval=10 MaxLevel=100 loaded=True fields=85`。
- **Play 模式自动加载**：`editor_play` 后读得 `ConfigLoaded=True fields=85 MaxLevel=100`。
- **编译干净**：`compilationFailed: false`、`consoleErrors: 0`。
- **探针回归**：每格 40 场 / 类型 × 3 次独立随机技能组合（每场重置生命，加权 战45/弓30/盗25）——巅峰 0 → 100 的 Lv.110 Boss 胜率 **0.8% → 77.3%**，与 20 次抽样基线（1.5% → 100%）同形；巅峰 30 的 **41.2%** 与基线完全吻合。3 次抽样噪声使尾部偏低，结合上面 8 项确定性断言可判定行为未变。

## 4. 使用方式

改数值三步：编辑 `HelloWorldProject/Assets/Resources/game_config.json` → Unity 自动重载（或跑菜单 `AutoBattle/Reload Game Config`）→ 进 Play。用 `AutoBattle/Export Current Config To JSON` 可从当前生效值导出新文件。

## 5. 边界与决策理由（重要，勿轻易推翻）

- **`SaveKey` 仍是 `const`**（值 `"AutoBattleDemo.Save.v1"`）：存档键一旦可被配置修改，改错一个 JSON 就等于丢掉玩家存档。因此 `const` 计数从 86 降到 1。
- **30 个 `static readonly` 数组未外部化**：`MonsterTypeWeights`、`AffixStatFactors`、`QualityColors`、`SkillCatalog` 相关表等与**枚举索引强绑定**——改数组长度等于改枚举；且 `float[][]` / `Color[]` 走 `JsonUtility` 需额外包一层可序列化类并改动 3 处引用点，收益不抵风险。这些表在技能平衡两轮中一次都没动过。
- 若后续确实要调这些表，单独一轮把二维表**摊平成 `float[]`** 再外部化，不要直接在现有结构上硬塞。

## 6. 环境与工具注意（本轮踩到）

- `unity pipeline list` 的 `isRunning` 会误报；判定 Editor 死活看 `unity status` 的 `state`（`ready` 才算连上）。本轮 `pipeline list` 返回 `STATUS_NO_INSTANCES` / exit 1 时 Editor 实际可用。
- CLI 子命令（如 `console`）**没有** `--count` 之类的参数，参数名一律取该命令 schema（`unity command --query X`）。
- Editor 内验证建议流程：`eval` 读字段 → `AssetDatabase.Refresh()` → `eval` 复读 → `console_status` 看 `compilationFailed` / `consoleErrors`。

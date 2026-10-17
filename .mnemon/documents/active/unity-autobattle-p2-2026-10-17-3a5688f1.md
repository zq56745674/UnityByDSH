---
id: "3a5688f1-e1b1-48ea-8f2b-a0a4d0157734"
title: "Unity AutoBattle：P2 工程卫生清扫（2026-10-17）"
description: "记录 P2 工程卫生这一轮的改动：死代码（BattleSystem 三目、HelloWorldSpinner 删除）、注释漂移、game_config.json 补齐 4 个强化字段并在加载器里加缺键警告、_archive 归档与 gitignore、README 结构重写；以及仍需提交才能解决的未跟踪源码问题。"
status: "active"
created_at: "2026-10-17T14:05:27.872Z"
updated_at: "2026-10-17T14:05:27.872Z"
content_hash: "0d9d6e482434ae1823879c5b28d34d5183f93c30d9b83c439f13a9be7583fb06"
source_paths:
  - "README.md"
  - ".gitignore"
  - "HelloWorldProject/Assets/Scripts/GameConfig.Loader.cs"
  - "HelloWorldProject/Assets/Resources/game_config.json"
session_ids:
  - "session-1c2b95c0-d836-4a67-b085-d00b734a2069"
memory_body_ids:
  []
---

# P2 工程卫生（2026-10-17）

工程：`E:\pytest\unitydsh1`（Unity 6000.6.5f1）。总览文档：42b8694e-f097-43db-9f58-9d606e02cfd1。

## 这一轮改了什么

| 项 | 处理 |
| --- | --- |
| 死代码 ①：`BattleSystem.cs` 的 `string label = def.kind == SkillKind.Active ? def.name : def.name;` | 三目两分支相同，简化为 `string label = def.name;` |
| 死代码 ②：`Assets/Scripts/HelloWorldSpinner.cs`（模板残留的心跳日志 MonoBehaviour） | 场景/预制体里零引用（GUID `b84768d8a3046ca4b89a5bf0e17ebadd` 全工程搜不到），删掉 .cs 与 .meta |
| 注释漂移：`GameConfig.Tables.cs:253` 写「词条池共 4 种」 | 实际 `EquipAffixMax` 有 5 项，改成「5 种：暴击率 / 暴击伤害 / 攻速 / 全属性 / 护甲穿透」 |
| 配置双份维护：`game_config.json` 缺 `EnhanceBonusPerLevel` / `EnhanceMaxLevel` / `EnhanceBaseCost` / `EnhanceCostPerLevel` | 补进 JSON（0.03 / 15 / 150 / 300，与代码默认值一致）；JSON 键数 93 → 97，与 `GameConfigData` 的 97 个字段一一对应 |
| 缺键零反馈 | `GameConfig.Loader.cs` 新增 `ReportMissingKeys(json)`：`Reload()` 加载后逐个字段名在 JSON 文本里找，缺哪个就 `Debug.LogWarning` 点名（旧行为是 JsonUtility 静默用默认值） |
| 仓库根残留 `_backup_GameController.cs`、`_backup_colors/` | 移进 `_archive/legacy/`（`GameController.pre-split.cs`、`GameController.bak.cs`、`GameUI.bak.cs`） |
| `Assets/Screenshots/` 里 8 张一次性开发截图（colors-\* / enhance-\* / portrait-new / post-split） | 移进 `_archive/screenshots/`（连同 .meta 一起移出 Assets，Unity 不再导入） |
| `_archive/` 会不会被当成工程/资源 | `.gitignore` 新增 `_archive/`；`_archive/README.md` 说明每个文件的来历 |
| README 项目结构段与实际不符 | 重写：Scripts 19 个文件约 5950 行（补 GameUI 三份、Models.PlayerData、SkillCatalog.Loader），Editor 5 个文件约 2100 行（补 SkillTableVerify），Resources 补 skills.json |

## 验证

- `unity command recompile` → `recompile_status` = completed；`AutoBattle.GameConfig.ConfigFieldCount` = **97**、`ConfigLoaded` = True。
- `BalanceRegression.RunStaticOnly()` = **PASS**（战斗常数未动，过渡轨迹 0 漂移）。
- 30 秒 PlayMode 冒烟：跑得起来、升到 Lv.26，`_dpsText.text` = 「实时 DPS 345」；console 100 条全是 log，**0 warning / 0 error**，`[GameConfig] 已重载 game_config.json，覆盖 97 个字段`，没有触发缺键警告。
- `HelloWorldSpinner` 全工程引用数 0。

## 仍待办（需要用户点头才能做）

- **未提交**：按用户规矩，未经明确许可不得 `git commit`/`git push`。当前 HEAD 还是 `f2e4e5d`，工作区里 23 个已修改 + 22 个未跟踪（含 `skills.json`、`GameUI.Core.cs`、`GameUI.Panels.cs`、`Models.PlayerData.cs`、`GameConfig.Colors.cs`、`Editor/NewGameProgression.cs`、`Editor/SkillTableVerify.cs`）——**这些未跟踪文件是编译必需件，克隆下来直接编译不过**，建议尽快做一次提交。
- `_archive_screenshots/`（18 张，已跟踪）仍在仓库根，可以并进 `_archive/`，但要连着一次提交一起做才干净。
- `Assets/Screenshots/` 里还有 6 张 README 没引用的旧截图（chapter3/chapter9/droplog/save-panel/stage-fresh/stage-switch/stage/ui-skill-fix），继续当资源导入，暂未动。

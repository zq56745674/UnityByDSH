---
id: "5eb3ca99-0747-4bf2-86cb-a5231db91fdb"
title: "GameUI 28 文件拆分与 Unity GUID 污染坑（新脚本不进编译输入表）"
description: "GameUI 由 14 文件/7,289 行拆成 28 文件/7,560 行的纯搬代码重构：分组结构、四层验收证据（编译/成员无损/回归/Play 冒烟），以及 12 个新 .cs 因 AssetDatabase GUID 污染被排除编译输入（207 条只出现在老文件的 CS0103）的排查与换 GUID 修法。"
status: "active"
created_at: "2026-10-22T17:37:10.348Z"
updated_at: "2026-10-22T17:37:10.348Z"
content_hash: "f7fb42b7d280bf69a4672fd0abb74bdcc3d07cd40cd5fdc24c469159c42b873d"
source_paths:
  - "HelloWorldProject/Assets/Scripts/GameUI.cs"
  - "HelloWorldProject/Assets/Scripts/GameUI.Core.cs"
  - "HelloWorldProject/Assets/Scripts/GameUI.Endless.cs"
  - "HelloWorldProject/Assets/Scripts/GameUI.Stage.cs"
  - "HelloWorldProject/Assets/Scripts/GameUI.Panels.cs"
  - "README.md"
session_ids:
  - "a21c1bed-eea7-4024-b4fa-5ac7704e755a"
memory_body_ids:
  []
---

# GameUI 28 文件拆分（2026-10-23）+ Unity「新脚本不进编译输入表」GUID 污染坑

工程：`E:\pytest\unitydsh1\HelloWorldProject`（Unity 6000.6.5f1，com.unity.pipeline 端口 7800）。

## 目标与结果

`GameUI` 原本 14 个 partial 文件 / 7,289 行，最大单文件 1,342 行（浮层与列表）。本次是**纯搬代码、不改逻辑**的重排：

| 指标 | 拆分前 | 拆分后 |
|---|---|---|
| 文件数 | 14 | 28 |
| 总行数 | 7,289 | 7,560 |
| 最大单文件 | 1,342 | 533（`GameUI.Endless.cs`） |

动机来自 README「已知问题」：`GameUI`/`PlayerData` 职责偏多、重构风险高，原计划等玩法稳定再动。本次只做物理拆分，不改行为，所以不需要重新平衡。

## 拆分结构（分组 → 文件 / 行数）

- **构建与控件**：`GameUI.cs` 352（主壳）、`GameUI.Build.Left.cs` 291、`GameUI.Build.Right.cs` 149、`GameUI.Factory.cs` 121、`GameUI.Theme.cs` 218、`GameUI.Icons.cs` 178、`GameUI.Buttons.cs` 306
- **刷新与特效**：`GameUI.Core.cs` 366、`GameUI.Fx.cs` 267、`GameUI.Refresh.Log.cs` 226
- **面板**：`GameUI.Panels.cs` 258、`GameUI.Panels.Save.cs` 214、`GameUI.Panels.Enhance.cs` 233、`GameUI.Panels.Reforge.cs` 200、`GameUI.Panels.Banner.cs` 57、`GameUI.Panels.Stage.cs` 260、`GameUI.Panels.SkillBar.cs` 196、`GameUI.Endless.cs` 533、`GameUI.Achievements.cs` 511、`GameUI.Compare.cs` 322、`GameUI.Settings.cs` 503、`GameUI.Rebirth.cs` 270、`GameUI.Music.cs` 288、`GameUI.Portrait.cs` 153
- **战斗舞台**：`GameUI.Stage.cs` 299、`GameUI.Stage.Frame.cs` 234、`GameUI.Stage.Art.cs` 260、`GameUI.Stage.Events.cs` 295

`Models.PlayerData.cs`（720 行）判定为「存档模型、职责单一」，**不再拆**，已在 README 注明。

## 验收证据（可复用的四层验证法）

1. **编译层**：`groundTruth.compilationFailed=false`、`consoleErrors=0`；`CompilationPipeline` 里 Assembly-CSharp 的 GameUI 源文件计数 14 → 28，`Assembly-CSharp.dll` 已重建（275,456 bytes）。
2. **成员无损**：拆分前 4 个源文件的 **711 个成员声明名**逐个比对，28 个文件并集里一个不缺（探针原始输出：`orig decl names 711 current 1169 / missing: (none)`）。这是纯搬代码拆分最该证明的一条。
3. **回归**：`BalanceRegression.RunStaticOnly()` → `PASS — 静态哨兵与基线一致`；`AchievementRegression.RunCore()` → 644 通过 / 0 失败。
4. **Play 冒烟**：`set_autotick(interval_ms=16)` → `editor_play` → `wait_for` 帧计数 108→147（`framesObserved=2`，证明非假死）→ `capture_game_view --source screen --save_path Assets/Screenshots/ui_split_smoke.png` → `editor_stop`。界面完整（左面板立绘/属性/五槽/六技能、右面板关卡名、章节背景、商店行、开始按钮）。warning 仍是原有 4 条：`GameUI.cs(143,21) CS0414 _deleteConfirmSlot`、`GameUI.cs(157,22) CS0414 _enhancePanelDirty`，外加 3 条存档测试注入——拆分未引入新警告。

## 核心坑：AssetDatabase GUID 污染导致新 .cs 不进编译输入表

**症状**：12 个新拆出的文件被排除在编译输入之外。表现非常有误导性——报 **207 条 `CS0103 … does not exist in the current context`，但错误全部落在老文件里，新文件零报错**，且 `Assembly-CSharp.dll` 时间戳不动。

**根因**：这批路径的 AssetDatabase GUID 条目在第一次（失败的那次）导入时被污染，之后即使同 GUID 重写文件内容也不会被 `CompilationPipeline` 收进来。

**无效的补救**（都试过，全部失败）：`ImportAsset(ForceSynchronousImport)`、`RequestScriptCompilation()`、`unity recompile`。

**有效修法**：换 GUID 重建。删掉相关 14 个 `.cs` / `.cs.meta` → Refresh → 用**新随机 GUID** 同时写文件与 meta → Refresh，一次转绿。

**推广规则**：以后新增脚本若"编译器看不见"（新文件不报错、老文件却报 CS0103、dll 不更新），不要反复 Recompile，直接删掉重建并换 GUID。GUID 污染和「`recompile` 报 `up_to_date` 不代表没编译」是同一类假信号（后者见 AGENTS.md）。

## 收尾状态

- README 的「十四个文件 7,289 行」已改写为 28 文件 7,560 行的分组账 + 拆分日期 + 验收结论。
- `runInBackground` 本工程原本即 `true`，Play 结束后保持原值，无需还原（本工程 `editor_play` 没有 `run_in_background` 参数，按 skill 流程自己设/自己还原）。
- **未提交**：工作区留着 B7 的 8 个改动 + README + 4 个被拆旧文件（M）+ 14 个新文件及 .meta（??）；`_tmp_verify/` 未跟踪，勿提交。提交需用户明确要求。

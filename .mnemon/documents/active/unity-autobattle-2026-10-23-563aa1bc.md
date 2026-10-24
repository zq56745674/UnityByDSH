---
id: "563aa1bc-fa8f-487e-9d38-c7c55c3b8032"
title: "Unity AutoBattle 界面六项修复与键盘快捷键验证（2026-10-23）"
description: "界面审计六项修复的实现位置与实测数据、键盘快捷键（空格/1/2/4/Esc/R）双输入路径实现、Editor 失焦导致注入按键无效的发现与验证前置条件、重置按钮字号 18→15 的修复、回归结果与未提交状态。"
status: "active"
created_at: "2026-10-23T05:54:57.717Z"
updated_at: "2026-10-23T05:54:57.717Z"
content_hash: "f257b380d63802dd1de2e26850720199095b6cd3084c9b6b5a2cf6202d8d7cc1"
source_paths:
  - "HelloWorldProject/Assets/Scripts/GameUI.Core.cs"
  - "HelloWorldProject/Assets/Scripts/GameUI.Build.Right.cs"
  - "HelloWorldProject/Assets/Scripts/GameUI.Panels.cs"
  - "README.md"
session_ids:
  - "37e8f828-c695-4cdb-b2b4-706cf2b4a5a9"
memory_body_ids:
  []
---

# Unity AutoBattle 界面六项修复与键盘快捷键（2026-10-23）

上一轮 UI 审计提出六项问题（金币千分位、装备卡缺强化等级、倍率/评分无说明、转生购买键禁用无理由、设置缺音乐音量、零快捷键），本轮全部实现；随后用户切到前台要求验证快捷键，验证中又抓到并修掉一个字号 bug。

## 六项改动与证据

1. **金币千分位**：改在 `GameUI.Core.cs` 金币行与 `GameUI.Panels.cs:211-212`；左栏显示 `2,330,705`，商店「金币 2,330,705 / 价格 520」。
2. **装备卡补强化等级**：标题拼 `<size=9> +N</size>`。Text 需同时设 `horizontalOverflow=Overflow` 与 `bestFit`——**bestFit 只缩字不挡 wrap**，初版只加 bestFit 仍会折行。实测「武器 Lv.100 +5」prefW 77.3 / rectW 90.0，最坏「Lv.110 +15」82.0 / 90 仍单行。
3. **倍率 · 评分说明**：卡面挂 4 行左对齐动态 tooltip；同时修 `ShowTooltip` 的富文本宽度估算（跳过标签、按最长行计宽），否则多行内容会按单行排版而居中、贴屏。
4. **转生按钮禁用理由**：标签改为「缺 N 印记」，悬停提示「印记不足：需要 3，当前 0。轮回一次可以获得印记。」
5. **设置面板音乐音量**：面板 680×560 → 620，新增「音乐」第三行 10 格（与音乐浮层共用同一值），底部补一行快捷键说明。
6. **键盘快捷键**：`GameUI.Core.cs:114` 的 `TickShortcuts()`，每帧在 `GameUI.Core.cs:18` 调用。空格 开始/暂停、1/2/4（含小键盘）战斗速度、Esc 关浮层（无浮层则暂停）、R 重置。浮层判定 `AnyOverlayOpen()`（`GameUI.Core.cs:64-71`）列出全部 13 个面板；浮层开着时除 Esc 外一律不响应。实现走双路径：`#if ENABLE_INPUT_SYSTEM` 用 `Keyboard.current` + `KeyDown()`（`wasPressedThisFrame` 为主、`isPressed` 上升沿兜底），`#else` 退回 `Input.GetKeyDown`。先算完一帧所有按键再早退，避免在浮层里按住某键、出来时误触发一次。

## 关键技术发现：Editor 失焦时注入按键无效

窗口失焦时 Input System 会在 `Update` 之前清掉注入的按键状态：设备侧读 `isPressed=True`，`TickShortcuts` 里读到的却恒为 False；把 `backgroundBehavior` 设为 `IgnoreFocus` **无效**。因此自动化验证快捷键必须先让 Editor 窗口在前台（`isFocused=True`、`playing=True`），否则只能反射直接调处理器验证语义。

另一个工具细节：`unity simulate_key` 的 key 取 **Input System 键名**，`KeyR` 会报 `Unknown key 'KeyR'. Use an Input System Key name (e.g. Space, W, Enter).`，要用 `R`。

## 验证结果（2026-10-23，窗口有焦点）

- 空格：`running=False → True`，再按 `→ False`。
- `2` / 小键盘 `4` / `1`：`speedIdx 0→1 (×2) → 2 (×4) → 0 (×1)`，日志同时写 ⚡。
- Esc（设置面板开着）：`settingsOpen=True → False`，战斗不停；Esc（无浮层）：`running=True → False → True`。
- R：按钮「重置/15」→「确认重置？/15」，约 3 秒后 `TickConfirms`（`GameUI.Core.cs:47-57`）自动复原为「重置」。只按一次确认未清档：等级仍 100、金币 2,696,095 → 2,798,871。
- 回归：`BalanceRegression.RunStaticOnly()` PASS；`AchievementRegression.RunCore()` 644 通过 / 0 失败；`compilationFailed=false`、`consoleErrors=0`。

## 本轮抓到并修掉的真 bug：重置按钮字号跳变

创建时 `StyleBar(reset, _resetLabel, "frame_bar_notched", "glyph_reset", 28f, "重置游戏", 15)`（`GameUI.Build.Right.cs:83-85`）把标签定为 15 号，但两处复原代码写死 `fontSize = 18`：`GameUI.Build.Right.cs:137`（`OnResetClicked` 确认分支）与 `GameUI.Core.cs:53`（`TickConfirms` 超时复原）。结果是第一次确认循环后「重置」变粗一号。两处都改为 15，重编译复验：`重置/15 → 确认重置？/15 → 重置/15`，不再跳变。

## 状态与遗留

- 改动未提交（GameUI 六个文件 + README，叠加在本轮字号修正、上一批 B7 与 GameUI 28 文件拆分之上）；`git status` 还带 `_art/ui-audit/`、`_tmp_verify/` 等未跟踪目录。
- README 界面章节已补快捷键条目、设置面板三组音量条、装备卡 `+N` 与悬停说明，并把「十四个文件」改为拆后的 28 个。
- 临时诊断字段已全部删除（确认 `DbgUpdateTicks` 字段不存在）。

## 相关文档

- 总览与接手指南：`42b8694e-f097-43db-9f58-9d606e02cfd1`
- 防御性兜底六处防护（2026-10-18）：`b166ac7b-ec3f-4e85-9ecf-92e9770f3b7b`
- 成就与转生 §11 全量回归（2026-10-20）：`884032e9-f795-4eea-ae52-4a84277dc555`

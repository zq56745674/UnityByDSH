---
id: "b38d2252-8261-4d10-9804-388b724352ca"
title: "Unity AutoBattle P4 表现层落地：战斗舞台、事件驱动特效与可展开日志"
description: "HelloWorldProject 自动战斗游戏 P4 完成后的实现记录：GameUI.Stage 舞台定高与地面线、BattleFx 事件源、12 格特效池与节流、可展开日志（不透光面板）、静态哨兵验证、ToggleRunning 暂停纪律、未提交变更集与剩余美术 TODO。"
status: "active"
created_at: "2026-10-21T17:00:36.673Z"
updated_at: "2026-10-21T17:00:36.673Z"
content_hash: "65ba07b00dfb8e19361d19d6b1bff45a8a22a3edd17a16f4a3a8d1aba348bbb9"
source_paths:
  - "HelloWorldProject/Assets/Scripts/GameUI.Stage.cs"
  - "HelloWorldProject/Assets/Scripts/BattleSystem.cs"
  - "HelloWorldProject/Assets/Scripts/GameUI.cs"
  - "docs/sprites-and-fx.md"
  - "README.md"
session_ids:
  - "35df920f-4cba-4821-accc-3f4820b1d39b"
memory_body_ids:
  []
---

# Unity AutoBattle P4 表现层落地记录（舞台 / 事件特效 / 可展开日志）

> 状态：**已实现并实机验证**，2026-10-21（检查点时间 2026-10-22 01:00 +08:00）。原始 12 步路线图至此全部完成。
> 设计稿与方案对比见 Mnemon 文档 `d029b086-f8ff-438e-a87b-100aa336080c`；项目全局地图见 `42b8694e-f097-43db-9f58-9d606e02cfd1`。本文件只记录**落地后的实现事实**（设计稿里的 3 个待定决策此时已定：布局 A、先出 3 族 + 12 特效、Boss 单独美术）。

## 变更集（检查点结束时未提交，等你授权 commit/push）

- 新增 partial 类 `HelloWorldProject/Assets/Scripts/GameUI.Stage.cs`（战斗舞台全部逻辑），挂在既有 `GameUI` 上，不改 Bootstrap。
- `HelloWorldProject/Assets/Scripts/BattleSystem.cs`：新增 `enum BattleFx`（13 种）与 `event Visual`，在命中 / 暴击 / 技能 / 吸血 / 剧毒 / 护盾 / 狂暴 / 阵亡处抛出；UI 侧只订阅，**不回读日志文本**（避免视觉绑定文案格式）。
- 修改：`GameUI.cs`、`GameUI.Core.cs`、`GameUI.Achievements.cs`、`GameConfig.Tables.cs`。
- 新资源：`HelloWorldProject/Assets/Resources/Sprites/`（25 张，含 .meta）；`docs/sprites-and-fx.md` 新增 §11「已落地」；`README.md` P4 打勾。

## 舞台参数（1280×720 布局 A：808×332 日志卡被舞台替换）

- 定高标准：主角 190px、普通怪 192px、精英 206px、Boss 226px；按 sprite 宽高比定宽，**绕脚底缩放**，统一踩 `StageGroundY = 250`。
- 左上「第 11 章 · 龙裔」标签，右上 DPS 胶囊保留；`_battleHeroSprite` 懒加载 `Sprites/Hero/hero_battle`。
- 程序化动画（无逐帧图）：待机呼吸 `sin(t*1.7)`；出手前冲 0→18px（60ms）→0（220ms），超过 0.30s 归零——x4 挂机下每秒多次攻击，位移必须很小；受击抖动 `MoveTowards(dt*6.5)` + `sin(battleAnimTime*42)*6`；阵亡旋转淡出 0.4s。

## 特效池

- 12 格 `Image` 池复用（`_fxImages`），缩放 `Lerp(0.68, grow, sqrt(k))`，k>0.55 后淡出。
- 节流：普通特效 0.07s；「每次命中都触发」的吸血 / 狂暴 / 剧毒 / 护盾 用 0.45s——否则吸血鬼流会在主角身上糊一团消不掉的红光（实测踩坑）。
- 实战中同时启用的 Image 只有 2~6 个（池 12），池化充足。

## 可展开战斗日志

- 默认底部 2 行 ticker：单条日志里的换行压成空格、截到 78 字，否则掉落行会把 ticker 撑成三行。
- 点 ▲ 向上长成高 256 的滚动面板（最近 180 条，展开即滚到底），点 ▼ 收回；展开时隐藏伤害飘字层与 DPS 胶囊。
- **面板底色必须完全不透明**：alpha 0.985 会让底下怪物剪影透上来、文字叠在剪影上难读。实测改 `RGBA(0.020, 0.024, 0.040, 1.000)` 后取样 max = (0,0,13) 与 bg 一致（此前亮像素 9/44200 ≈ 0.02%）。

## 验证与操作纪律

- `unity command recompile` = completed；console 0 error / 0 warn（含 groundTruth 采样）。
- `AutoBattle.EditorTools.BalanceRegression.RunStaticOnly()` = **PASS — 静态哨兵与基线一致**：战斗数值未被表现层改动碰到。
- 命名空间坑：`eval` 里写 `AutoBattle.BalanceRegression` 会报 `The type or namespace name 'BalanceRegression' does not exist in the namespace 'AutoBattle'`，必须用全名 `AutoBattle.EditorTools.BalanceRegression`。
- **暂停必须用 `GameController.ToggleRunning()`，不能用 `Time.timeScale = 0`**：舞台动画走 Update 的 dt，dt=0 会让入场淡入停住、怪物看不见。恢复同样调 `ToggleRunning()`（实测 `running=True state=Shopping`）。

## 收尾与尚未覆盖的部分

- `Assets/_art/`（`capture_game_view` 的一次性产物目录及其 .meta）已删除；`_art/stage/` 已加入 `.gitignore`，调试截图留在本地 `_art/stage/shots/final1.png` / `final3_goblin.png` / `final4_skeleton.png` / `final6_log.png` / `final7_boss.png`。
- 待办（已写进 `docs/sprites-and-fx.md` 的 TODO）：
  1. 4~11 章暂无专属怪物美术，按 `(chapter - 1) % 3` 轮换复用哥布林 / 狗头人 / 骷髅三族。
  2. 精英 / Boss 目前只靠身高 226px + 血条金 / 红边框区分，档次光环与章节背景图未做。
  3. 只有重击 / 旋风斩 / 斩杀 / 反击有专属特效图，其余 11 个技能共用 `fx_heavy`。

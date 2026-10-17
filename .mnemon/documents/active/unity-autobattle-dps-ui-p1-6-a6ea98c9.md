---
id: "a6ea98c9-ec86-4719-9d50-46bdfd91f933"
title: "Unity AutoBattle：飘字/实时 DPS 修复与 UI 冒烟（P1-6）"
description: "记录 2026-10-17 定位的「日志两条写入路径导致飘字与实时 DPS 永久停摆」缺陷、修复点（TickFighting 补记计数器、飘字改真实时间、重置/删档二次确认）与冒烟验证方式，供后续改日志或 UI 时参考。"
status: "active"
created_at: "2026-10-17T12:31:53.264Z"
updated_at: "2026-10-17T12:31:53.264Z"
content_hash: "c41ae1e26f4f69d470bfa203f4a2c09ed06b3b9a8cfad58a56b64241184ab149"
source_paths:
  - "HelloWorldProject/Assets/Scripts/GameController.cs"
  - "HelloWorldProject/Assets/Scripts/BattleSystem.cs"
  - "HelloWorldProject/Assets/Scripts/GameUI.Core.cs"
session_ids:
  - "session-1c2b95c0-d836-4a67-b085-d00b734a2069"
memory_body_ids:
  []
---

# UI 冒烟：飘字/实时 DPS 修复（P1-6）

工程：`E:\pytest\unitydsh1\HelloWorldProject`（Unity 6000.6.5f1）。相关总览文档：42b8694e-f097-43db-9f58-9d606e02cfd1；平衡工具：cc30b461-fc35-4515-a393-f03aacb0d948。

## 现象

挂机 1~2 分钟后，战斗日志还在滚动，但：

- 顶栏「实时 DPS」永远是破折号（`实时 DPS —  期望 N`），不再出现数字；
- 屏幕上没有任何伤害飘字。

## 根因（2026-10-17 定位）

日志有**两条写入路径**：

1. `GameController.AddLog`（击杀/掉落/商店/升级）：`Log.Add` + `TotalLogEntries++` + `LogVersion++`。
2. `BattleSystem.Tick(player, monster, dt, log)`：把 `GameController.Log` 直接传进去，内部 8 处 `log.Add(new LogEntry(...))` **只进 List，不改两个计数器**。

`GameUI.ParseNewLogs` 用 `added = TotalLogEntries - _lastParsedLogTotal` 算「新增几条」，再取窗口 `i = Log.Count - added .. Log.Count - 1`。只统计 AddLog 行时，这个窗口永远落在击杀/掉落/升级那几行上，**伤害行虽然排在 Log 末尾却永远进不了窗口**。

顺带后果：战斗行不涨 `LogVersion`，日志面板（`GameUI.Core.cs` 里比较 `_game.LogVersion == _lastLogVersion`）也不会按帧刷新。

## 修复

- `GameController.TickFighting`：调 `_battle.Tick` 前后量 `Log.Count` 差值，`appended > 0` 时补 `TotalLogEntries += appended; LogVersion++;`（在 `TrimLog()` 之前算，不会把战斗行算丢）。
- `GameUI.Update`：`TickFloats(Time.deltaTime)` 改用**真实时间**。原来飘字和 DPS 共用一个 `dt = Time.deltaTime * SpeedMultiplier`，x4 下 1.15 秒的寿命被压成 0.29 秒，肉眼抓不到——而挂机默认就是 x4。DPS 窗口仍按游戏时间走，和「期望 DPS」同一基准。
- 「重置」与存档槽「删除」加二次确认：第一次点击只把按钮文字改成「确认重置？」/「确认？」，`ConfirmWindow = 3f` 秒内再点同处才执行，超时由 `GameUI.TickConfirms` 收尾复原；同时往日志写一条黄色警告。

## 验证

- 反射读 `_dpsText.text`：修复前恒为「实时 DPS —」，修复后为「实时 DPS 432    期望 351」这类数值；`_floatLife` 里存活数量 > 0。
- 截图：`_balance/shots/smoke_dps_ok.png`、`smoke_x4_floats2.png`（x4 下画面里能看到 210/219/211 这类伤害数字与 -41 受击数字）、`reset_confirm.png`（按钮显示「确认重置？」）。
- 静态哨兵 `BalanceRegression.RunStaticOnly()` = PASS（战斗常数没动，过渡轨迹 0 漂移）。
- console 无 exception / NullReference。

## 教训

- 修 UI 之前先确认**数据是怎么写进来的**：同一份日志两条写入路径、只有一条维护计数器，只看 UI 侧永远修不好。
- 手工调 `ParseNewLogs()` 会「成功」是因为手工把窗口对齐到了末尾 8 条（那里恰好是战斗行），掩盖了窗口本身选错区间——验证修复要复现**自然路径**，不要手动摆好输入。

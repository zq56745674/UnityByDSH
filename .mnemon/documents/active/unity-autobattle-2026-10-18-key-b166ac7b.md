---
id: "b166ac7b-ec3f-4e85-9ecf-92e9770f3b7b"
title: "Unity AutoBattle 防御性兜底（2026-10-18）：品质/掉落/评分/追帧/经验/首通 key 六处防护与验证"
description: "HelloWorldProject 2026-10-18 的六处低风险兜底实现记录：越界品质回落普通、掉落权重 sanitize、装备评分非法输入归零、战斗追帧 256 上限、经验 need<=0 停机、首通金币 key 校验，附 eval 验证数据、Play 冒烟结果与遗留项。"
status: "active"
created_at: "2026-10-18T15:30:34.855Z"
updated_at: "2026-10-18T15:30:34.855Z"
content_hash: "9f6ded9b5b9ca4c1ff6983b1788806cea191fd275a598ea4477306ef5eb07dbb"
source_paths:
  - "HelloWorldProject/Assets/Scripts/GameConfig.cs"
  - "HelloWorldProject/Assets/Scripts/LootSystem.cs"
  - "HelloWorldProject/Assets/Scripts/EquipmentScore.cs"
  - "HelloWorldProject/Assets/Scripts/BattleSystem.cs"
  - "HelloWorldProject/Assets/Scripts/Models.PlayerData.cs"
  - "HelloWorldProject/Assets/Scripts/Stages.cs"
  - "README.md"
session_ids:
  - "9f8be705-c5b7-4717-b743-d8a844492492"
memory_body_ids:
  []
---

# Unity AutoBattle 低风险防御性兜底（2026-10-18）

工作区 `E:\pytest\unitydsh1`，工程 `HelloWorldProject/`（Unity 6000.6.5f1）。本轮在既有玩法之上补了 6 处低风险兜底，都是小改、不改变正常路径行为，目的是让外部热改配置 / 坏输入不会把游戏带进非法状态。项目总览与接手指南见 `42b8694e-f097-43db-9f58-9d606e02cfd1`；上一轮健壮性（配置夹取、读档清洗、`ResetSessionState()`、评分缓存键）见 `README.md:172`。

## 六处防护

### 1. 品质越界 → 回落普通
- `HelloWorldProject/Assets/Scripts/GameConfig.cs:301` 新增 `ClampQuality()`：非法品质统一回落到 `Common`。
- 卖价、强化费、洗练费、品质倍率全部改用它索引品质表。
- **设计理由（踩过的坑）**：初版实现是夹到最高档，测试发现 `(EquipmentQuality)99` 会变成「传说」，等于一次坏输入白送传说；因此改为回普通。

### 2. 掉落权重与装备生成
- `HelloWorldProject/Assets/Scripts/LootSystem.cs:22`：权重表为 null / 空 / 全 0 / 含 NaN / 负值时不再返回非法品质；单个权重也做 sanitize。
- `CreateEquipment()` 强制 `level >= 1`、品质走 `ClampQuality()`；副词条等级缩放不再接受负等级。

### 3. 装备评分
- `HelloWorldProject/Assets/Scripts/EquipmentScore.cs:47`：非法槽位返回 0，不再误走「衣服」分支；`Dps` / `EffectiveHp` 对 null 角色返回 0；`PutOn` 只允许 `Weapon` / `Armor`（枚举本身只有这两个值，见 `Models.cs:9`）。

### 4. 战斗追帧上限
- `HelloWorldProject/Assets/Scripts/BattleSystem.cs:45` 新增 `MaxCatchUpAttacks = 256`；两个补算循环（`:221`、`:271`）都受它限制，极端 `dt` 不会卡死。
- 被截断后把负计时归位，避免下一帧继续补算。
- `PlayerAttackInterval(null)` 返回安全下限而非 NRE。

### 5. 经验升级死循环
- `HelloWorldProject/Assets/Scripts/Models.PlayerData.cs:369`：`AddExp` / `AddParagonExp` 在 `need <= 0` 时停止，热改经验公式为 0 或负数不会空转。

### 6. 首通金币 key 校验
- `HelloWorldProject/Assets/Scripts/Stages.cs:112`：`FirstClearGoldOfKey()` 拒绝章节 / 关卡越界的 key，避免脏首通记录被重复计入。

## 验证证据（2026-10-18）

编译与运行期：

- `compilationFailed=false`、`consoleErrors=0`、`consoleWarnings=0`。
- Play 模式冒烟：`ui=True gc=True cfg=True state=WaitingRespawn`，运行期 console 错误 0；结束后已 `editor_stop`，Editor `ready`。

静态验证（`eval` 注入）：

- 品质 99 / -5 → `Common`；合法传说保持不变。
- 非法品质卖价 / 强化费 / 洗练费：`32 / 630 / 1291`。
- 空权重、全 0 权重、负权重 → `Common`。
- `CreateEquipment(Weapon, 0, 99)` → `Lv.1 / Common / 普通`。
- 非法槽位评分 = 0，原衣服未被覆盖。
- 非法关卡 key 首通金币 = 0。
- `PlayerAttackInterval(null)=0.20`。
- 普通经验 `AddExp(1000)` 正常升 7 级；把 `ParagonExpBase` / `ParagonExpPerLevel` 都置 0 后 `AddParagonExp(1000)` 返回 `gained=0 lv=0 exp=1000 need=2000`，未卡死。

追帧实测 `dt=10000`：主角 / 怪物各结算 256 次，耗时约 3ms，计时器归位到 `1.333 / 1.455`。

## 状态与遗留（截至 2026-10-18）

- 改动未提交，HEAD 仍是 `9d0524c`；工作区还包含上一轮中优先级修复，`.mnemon/` 也在 diff 里。
- 原路线图剩余步骤 9 成就、10 转生、11 无尽模式、12 表现层，见 `README.md:151` 的进度表。
- 已知问题（`README.md:176`）：平衡探针不模拟洗练与真人犹豫；1~10 章满装仍 0 阵亡；终局金币是软出口；`GameUI` / `PlayerData` 偏胖。
- 历史文档标记但本轮未处理的遗留：`EliteUnlockLevel` / `BossUnlockLevel` 疑似死配置；关卡怪级固定、无软上限；`GameController.Update` 层没有统一夹 `dt`（`BattleSystem` 内已有 256 次上限，属于进一步防御）。引用时需以当前代码为准。

## 复现要点

- 验证这类防护不必进 Play：Edit 模式 `eval` 直接构造对象、临时把配置改成 0/负数，测完恢复原值即可。
- 判断编译与运行期健康看 `console_status` 的 `compilationFailed` / `consoleErrors`，不要只看 `recompile` 的 `up_to_date`。
- 改完脚本后进 Play 冒烟，再 `editor_stop` 收尾，避免 Editor 持有文件句柄导致外部覆写失败（`ReplaceFileW EIO (Win32 1175)`）。

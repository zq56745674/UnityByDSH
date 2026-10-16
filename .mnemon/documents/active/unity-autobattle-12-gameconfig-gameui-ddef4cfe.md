---
id: "ddef4cfe-e205-4218-89cc-f2275afb6ecd"
title: "Unity AutoBattle 代码审计落地（12 项）：存档正确性修复、GameConfig 三文件拆分、GameUI 每帧优化"
description: "HelloWorldProject 全量代码质量审计（14 项清单）中已批准落地的 12 项：P0 六项存档/装备正确性 bug、Apply 逐条警告、GameConfig 拆 partial 三文件、死代码与魔数清理、GameUI 每帧开销与 UI 越权写状态收拢，含拆分前后配置链路一致性证据与 Play 实测。"
status: "active"
created_at: "2026-10-15T05:54:08.043Z"
updated_at: "2026-10-15T05:54:08.043Z"
content_hash: "0074365192a9399e66fa4b89d55d0a3ef1b7e5404550784e93ce18d017dcd804"
source_paths:
  - "HelloWorldProject/Assets/Scripts/SaveSystem.cs"
  - "HelloWorldProject/Assets/Scripts/EquipmentScore.cs"
  - "HelloWorldProject/Assets/Scripts/GameConfig.cs"
  - "HelloWorldProject/Assets/Scripts/GameConfig.Tables.cs"
  - "HelloWorldProject/Assets/Scripts/GameConfig.Loader.cs"
  - "HelloWorldProject/Assets/Scripts/GameUI.cs"
  - "HelloWorldProject/Assets/Scripts/GameController.cs"
  - "HelloWorldProject/Assets/Scripts/Stages.cs"
session_ids:
  - "239fb6b1-35dc-4a06-a824-66b3597e14c0"
memory_body_ids:
  []
---

# Unity AutoBattle 代码审计落地（12 项）

工程 `HelloWorldProject/`（Universal 2D，Editor 6000.6.5f1）。本步先对全部 15 个脚本做了一轮代码质量审计（产出 14 项清单，按「小三档成本」分级），经用户批准后落地其中 12 项。编译 `compilationFailed: false`、`consoleErrors: 0`、`consoleWarnings: 0`，并在 Play 模式实机跑了第 3 章确认无回归。

关联文档：关卡/章节制与存档 v5 见 `418948ff-876f-4f76-90af-50e55a5c8727`；首通奖励、`clearedStages` 与存档 v6 见 `763c8adf-09a9-48af-9ac6-607a7011a53c`；技能平衡见 `502f3c34-f9de-4354-93d1-ca0844fb51f1`。本文只记本次修复的理由与证据，不重复上述规则。

## 成本分档（审计约定，后续沿用）

- **小** = 1~10 行、10 分钟、无需跑游戏；
- **中** = 20~100 行、半天、要跑一次验证；
- **大** = 搬职责/拆文件、1 天以上、有回归风险。

## P0 — 正在损害数据的正确性 bug（6 项）

| # | 问题 | 做法 | 位置 |
|---|---|---|---|
| 1 | v6 回填首通记录用错变量：判据若用 `chapterIndex`，玩家回头刷旧章时会把「早已整章通过的章」误判为「只打到当前关」，重刷**重复白拿首通奖励** | 判据改为 `maxChapterUnlocked`（已解锁章只由通关章末推进而来），并把原因写进注释 | `HelloWorldProject/Assets/Scripts/SaveSystem.cs:206-216` |
| 2 | 存档解析失败后不备份，调用方拿到 false 开新档，5 秒后的 AutoSave 把原档永久覆盖 | `catch` 里先把原始 JSON 原样挪到 `SaveKey + BackupSuffix` 再返回 false | `HelloWorldProject/Assets/Scripts/SaveSystem.cs:143-149` |
| 3 | `EquipmentScore.Of` 为算增量评分临时换装再还原，异常时永久丢装备 | 还原放进 `finally` | `HelloWorldProject/Assets/Scripts/EquipmentScore.cs:62-64` |
| 4 | 手改档或字段缺失可造出「当前章 > 已解锁章」，选关面板显示错乱且回填漏补 | 读档末尾补一致性兜底 | `HelloWorldProject/Assets/Scripts/SaveSystem.cs:192-194` |
| 5 | 无版本守卫：降级运行读更高版本档，`JsonUtility` 静默忽略未知字段、已知字段已被新语义改写 → 静默损坏 | `data.version > CurrentSaveVersion` 时备份原档并拒绝读取 | `HelloWorldProject/Assets/Scripts/SaveSystem.cs:154-164` |
| 6 | `Save()` 无 try/catch，PlayerPrefs 超限会把异常抛进 `OnApplicationQuit`，打断退出流程 | 包 try/catch，失败只记一条 `Debug.LogWarning` | `HelloWorldProject/Assets/Scripts/SaveSystem.cs:112-122` |

## P1 — 零反馈隐患（1 项）

7. `GameConfig.Apply()` 原本对「名字/类型对不上的配置项」静默跳过：新增一个配置项却漏加镜像字段，会完全无反馈。改为**逐条打警告**，末尾再报 `n/总数`，并把 `ConfigFieldCount` 的语义写进注释。位置 `HelloWorldProject/Assets/Scripts/GameConfig.Loader.cs:66-96`。

## P2 — 结构清理

### 8+13：`GameConfig` 拆成三个 partial 文件

审计结论是原文件同时承担 4 种职责（93 个 scalar 平衡数值 / 31 个表数组 / 19 个派生公式方法 / 反射加载），其中表数据「几乎不改，而且根本不参与 JSON 热改」。拆分后：

| 文件 | 行数 | 内容 |
|---|---|---|
| `HelloWorldProject/Assets/Scripts/GameConfig.cs` | 712 → **315** | 93 个 scalar + 对外转发门面 + 公式方法，可热改 |
| `HelloWorldProject/Assets/Scripts/GameConfig.Tables.cs` | **211** | 31 个数组/表字段，文件内注明**改 json 无效** |
| `HelloWorldProject/Assets/Scripts/GameConfig.Loader.cs` | **111** | 反射加载 |

保留 `GameConfig` 作为门面是为了不改动全工程 251 处引用。**未拆 `GameConfigFormulas`（原计划 4 个文件，实际 3 个）**：公式方法只有 19 个且与标量字段逐段交织（`AttackInterval` 紧挨 `MinAttackInterval`、`DamageReduction` 紧挨它的两个系数），硬拆会让每处改动都要跨文件跳。

### 9：删死代码

`LegacyScore`、`TierMonsterLevel`、`_buyButtonLabel`，以及 `ConfigLoaded` 的误导注释。

### 10：消重复与魔数

探针里 9 处硬编码 `110` → `GameConfig.MaxTierLevel`；四处章末判断 → `StageCatalog.IsChapterFinal`；`RollType` 参数改名。

### 11：注释数值漂移 5 处

一律改成指向常量，而不是把数字抄进注释——抄进去的数字在改平衡时必然漂移（`EquipmentScore.SurvivabilityDivisor` 的注释同步写了这条原则）。

### 12：`GameUI` 每帧浪费

`Refresh()` 每帧 2 次 `EquipmentScore.Of`（= 6 次改玩家字段 + 4 次属性重算）、`RefreshStagePanel` 每帧 11 次 `GetComponent<Image>()`、`RefreshBuffBar` 每帧 `new StringBuilder(320)`。做法：`EquipmentScore.Of` 加缓存（四个输入值没变就不重算）、`GetComponent<Image>` → `Button.image`、`StringBuilder` 提升为字段、`Refresh()` 补 `_game`/`Player` 判空（`HelloWorldProject/Assets/Scripts/GameUI.cs:1220-1225`，此前 `RefreshStagePanel` 自己判空而 `Refresh` 不判，两处空值策略不一致，Player 为 null 时每帧抛 NRE）。

### 14：UI 直接写游戏状态 5 处

全部收进控制器：`CloseStagePanel` / `OpenSkillPanel` / `CloseSkillPanel` / `ToggleAutoBuyPotion`。面板互斥规则从 2 份变 1 份。

## 验证证据

- **拆分后配置链路完全一致**：`Reload()=True`、`ConfigLoaded=True`、`applied=93/93`；表访问 `QualityNames[4]=传说`、`ChapterMonsterNames[10][0]=龙裔卫士`、`ChapterStatBias[9][0]=1.1`、`MonsterTypeWeights[2][2]=0.35`；公式 `MonsterNameOf(90,Ranged)=恶魔火法师`、`SellPrice(30,Common)=92`、`AttackInterval(100)=0.333`、`MaxLevel=100`、`StageClearDelay=3`、`MaxTierLevel=110`。
- **Play 实机**（第 3 章）：刷怪名「骷髅弓手／精英·骷髅卫士」证明章节族类表生效；通关 3-3 → 3-4（`★ 通关 3-3！进入 第 3 章 3-4`）；掉落与「弱于当前武器则卖出」正常；`SaveSystem.Clear()` 后 `hasSave=False`；已 `editor_stop`、`runInBackground=false`、Unity `state: ready`。
- 文件写工具偶发 `Error: ReplaceFileW EIO (Win32 1175)` 写失败，重试同一写入即成功；该工作区非 git 仓库（`git diff --stat` 报 `fatal: not a git repository`），改动无法靠 diff 复核，只能靠编译 + 实机验证。

## 未处理的开放项

- **31 个表字段既不参与 JSON、也无法热改**，`ExportJson` 也不会导出。当前处理方式是拆分后在 `GameConfig.Tables.cs` 里明确标注「表数据不可热改」；若以后要热改，需参考 P1 的思路在 `Apply()` 里显式跳过并计数，或补镜像字段。
- 审计清单里的第一项结论（`GameConfig.cs:383` 硬编码 110、`ConfigLoaded` 0 处读）已由本次第 9、10 项修复覆盖。

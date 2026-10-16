---
id: "cc30b461-fc35-4515-a393-f03aacb0d948"
title: "Unity AutoBattle 多存档槽与自动化平衡回归测试（BalanceRegression 框架与反向验证）"
description: "HelloWorldProject 两件工程化落地：SaveSystem 三存档槽（槽 0 兼容老档、零迁移）的设计与隔离实测；BalanceRegression 回归框架（固定种子逐位可复现、逐档种子、静态哨兵/胜率两层、0.5pp 容差）的三步反向验证证据、用法入口与仍未覆盖的中间章节缺口。"
status: "active"
created_at: "2026-10-16T04:28:26.308Z"
updated_at: "2026-10-16T04:28:26.308Z"
content_hash: "6ba745dde0f5583a33aae6a4c669134a7ef80934f60f038c3b51c7e9825ca292"
source_paths:
  - "HelloWorldProject/Assets/Scripts/SaveSystem.cs"
  - "HelloWorldProject/Assets/Scripts/GameController.cs"
  - "HelloWorldProject/Assets/Scripts/GameUI.cs"
  - "HelloWorldProject/Assets/Editor/BalanceRegression.cs"
  - "HelloWorldProject/Assets/Editor/LateGameProbe.cs"
  - "_balance/baseline.json"
session_ids:
  - "eedb56de-e60a-4b8d-8210-e69a21128a59"
memory_body_ids:
  []
---

# Unity AutoBattle 多存档槽与自动化平衡回归测试

执行日期 2026-10-16，工程 `HelloWorldProject`（Unity Editor 6000.6.5f1），workspace 根 `E:\pytest\unitydsh1`。本轮完成两件事：**多存档槽**、**自动化平衡回归测试**（后者是项目总览文档 `42b8694e-f097-43db-9f58-9d606e02cfd1` 里点名的功能缺口）。

相关文档：项目总览与接手指南 `42b8694e-f097-43db-9f58-9d606e02cfd1`；探针评分口径与胜率基线 `8bb7e4c1-5ba6-4738-8ab6-4384c4e62219`；存档正确性修复（本次在其基础上扩槽）`ddef4cfe-e205-4218-89cc-f2275afb6ecd`。

## 一、多存档槽（`SlotCount = 3`）

| 位置 | 内容 |
|---|---|
| `HelloWorldProject/Assets/Scripts/SaveSystem.cs` | `KeyOf(slot)` / `HasSaveIn` / `Describe` / `SelectSlot` / `RestoreCurrentSlot` / `ClearSlot`，`SlotCount = 3`。**槽 0 沿用原 key `AutoBattleDemo.Save.v1`**，因此老档自动成为槽 0，**零迁移成本** |
| `HelloWorldProject/Assets/Scripts/GameController.cs` | `SavePanelOpen`、`CloseAllPanels()`（面板互斥收拢到一处）、`ToggleSavePanel()`、`SwitchSlot(int)`、`DeleteSlot(int)`、`SaveSlot` |
| `HelloWorldProject/Assets/Scripts/GameUI.cs` | `BuildSavePanel` / `RefreshSavePanel`（约 `:610-668`），侧栏右上「存档 N」按钮，卡片显示 等级·巅峰 / 关卡 / 金币 / 首通数 |

关键设计决策与理由：

- **槽 0 复用旧 key** 而不是新建 `...v2`：老玩家存档直接落在槽 0，不需要写迁移代码，也不会出现「更新后存档消失」。
- **切槽会结束当前这局**，面板上已写明提示；空槽点进去就是新游戏。切槽要整体重置战斗状态，所以存档面板走**浮层**（打开时冻结整局），不做成侧栏常驻控件。
- 存档面板本身也是「面板互斥」的一部分，靠 `CloseAllPanels()` 统一收口，避免又出现两个面板同时开着的状态漏洞。

**隔离实测**（同一进程内来回切槽，直接验证槽间不串数据）：

```
slot0 lv=60 gold=4321
切到槽 2 → 设 lv=10 gold=999 → 存档
  d0.lv=60  d0.gold=4321      ← 槽 0 没被污染
  d1.lv=10  d1.gold=999
切回槽 1 → level=60 gold=4321  slot=0   ← 正确恢复
清槽 → exists=False
```

## 二、自动化平衡回归测试

### 文件与产物

| 文件 | 作用 |
|---|---|
| `HelloWorldProject/Assets/Editor/BalanceRegression.cs` | 新增，回归测试本体（静态哨兵 + 胜率表两层） |
| `E:\pytest\unitydsh1\_balance\baseline.json` | 基线快照（约 7KB）。**刻意放在工程目录之外** `_balance/`，避免被 Unity 当资源导入 |
| `HelloWorldProject/Assets/Editor/LateGameProbe.cs` | 把 `RunWinrateCell` 的计算抽成 `RunWinrateRow(...)`，并让 `Fight`（原 `LateGameProbe.cs:600` 附近的 `static bool`）被两者共用——探针与回归测试走同一条路径，**不可能算出两种结果** |

### 三个关键设计

**1. 固定种子，整轮模拟逐位可复现。** 战斗里的暴击、技能触发、伤害浮动、装备词条全走 `UnityEngine.Random`（`BattleSystem.cs:141/143/247/314`、`LootSystem.cs` 各掷点处），所以 `Random.InitState(seed)` 之后同一 seed 结果逐位一致。**这正是容差能收紧到 0.5 个百分点（±0.005）的原因**：否则 `perType=30` 的抽样噪声有 ±5pp，阈值宽到毫无检测意义。

**2. 逐档独立种子**（`DefaultSeed + 档位下标`）。改动巅峰 50 的数值只会让第 6 档报红，不会顺带搅乱其它档的判定——失败定位直接落到某个巅峰档，而不是「整表都变了」。

**3. 分两层，便宜的先跑、贵的后跑。**

| 层 | 内容 | 耗时 |
|---|---|---|
| 静态哨兵 | 15 张技能表、40 个公式采样、22 行关卡表、48 个怪物样本、装备评分自洽性 + `EquipmentScore.Of()` 无副作用 | **1.2 秒**（`RunStaticOnly` 端到端 2.4s） |
| 胜率表 | 8 个巅峰档 × 4 格 | **~25 秒** |

静态哨兵是**精确字符串比对**：红了就代表数值真的变了，没有噪声这一说。

### 反向验证（三步，证明它抓得住问题而不只是「总是 PASS」）

```
① 连跑两次                          → PASS（确定性成立，逐位一致）
② 篡改基线胜率 +0.5pp 再跑          → FAIL — 1 处偏离基线
                                      [胜率] 巅峰0 Lv100精英: 50.0% → 100.0%  (+50.0pp)
③ PotionPriceBase 25→124 再跑        → FAIL — 3 处偏离基线
                                      [公式采样] PotionPrice(1)=25 → 104 …
                                      [公式采样] PotionPrice(50)=270 → 349
                                      [公式采样] PotionPrice(100)=520 → 599
```

- ③ 的改值在同一个 eval 内先改、跑完立刻改回，未污染工程状态；随后确认已恢复 PASS。
- **踩坑记录**：第一次篡改把胜率改成 `0.9999`（与原值差 0.0001），结果 PASS——这是**正确行为**，差距落在 ±0.005 容差内，差点被误判成「检测失效」。做反向验证时篡改幅度必须**大于容差**才有意义。
- 篡改过程中工具自动留下了 `baseline.json.bak` 备份。

### 用法

```
静态哨兵（秒级，改完代码顺手跑）：
  unity command eval -- --code 'return AutoBattle.EditorTools.BalanceRegression.RunStaticOnly();'

完整比对：
  unity command eval -- --code 'return AutoBattle.EditorTools.BalanceRegression.Run(false, 12, 30);'

确认某次数值改动就是想要的，写成新基线：
  unity command eval -- --code 'return AutoBattle.EditorTools.BalanceRegression.Run(true, 12, 30);'
```

`Run(false, 12, 30)` 的 `12`/`30` 即 `trials` / `perType`，基线内记录的实参为 `seed=20261016, trials=12, perType=30`。Editor 菜单另有一份 `Tools/平衡回归/` 四个入口（跑一次比对 / 含胜率较慢版 / 只跑静态哨兵 / 写新基线）。

### 明确未覆盖的范围

探针固定用「Lv.100 传说套」打 Lv.100/110 怪，因此 **第 3~9 章的中间章节平衡仍然没人测**——章节属性偏向 `ChapterStatBias` 上线后，那几章的胜率一次都没验过。要覆盖必须再加一套「按章节配装」的探针入口：现有回归框架会自动跟着这个新入口受益。

## 三、顺手确认的工程事实

- `E:\pytest\unitydsh1` **不是 git 仓库**（`git` 报 `fatal: not a git repository`）。当时约 6500 行代码 + 二十多份文档处于**零版本控制**状态，被判定为当时最大的单点风险，待办是补 `git init`。
- 静态采集只要 1.2 秒、完整比对约 25 秒，这个成本差异就是「分两层」决策的量化依据。

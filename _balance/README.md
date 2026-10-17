# 平衡工具与基线

这个目录放在 Unity 工程外，免得被 AssetDatabase 当资源导入。

| 文件 | 内容 |
|---|---|
| `baseline.json` | 平衡回归基线：静态哨兵全文 + 新档过渡轨迹（`transition` 小节）+ 8 巅峰档 × 4 难度格胜率表（容差 0.5pp） |
| `shots/` | PlayMode 冒烟截图 |
| `README.md` | 本文件 |

## 两个探针的分工

- **`Assets/Editor/LateGameProbe.cs`** — 稳态探针。按 `p.level = chapter * 10` + 各抽一件该章精英品质装备造人，只测"这个阶段的玩家打不打得过"，不模拟从 Lv.1 一路成长的过程。
- **`Assets/Editor/NewGameProgression.cs`** — 过渡探针。从 `new PlayerData()`（Lv.1、空身、0 金币、0 药剂）起步，逐关先打小怪（血量继承）再回满血打关底，阵亡重试，直到通关或卡死。回答的是"新玩家能不能玩下去"。

稳态探针天然看不到"起点不可玩"这类问题——它假定你已经站在终点。

过渡探针现在不只是"手动跑一下看看"，而是**常驻门禁**（2026-10-17 补，见下）：
```
NewGameProgression.Trace(seed, attempts)         → 每章一行：ch{章}|cleared|endLevel|attempts|deaths|seconds
NewGameProgression.CheckInvariants(SimResult)    → 与基线无关的硬约束，任何一次运行都必须过
BalanceRegression.RunStaticOnly()                → 静态哨兵 + 过渡轨迹比对（全量约 20ms 的模拟）
BalanceRegression.WriteTransitionBaseline()      → 只把 transition 小节写进现有基线，不动胜率
```
容差：`endLevel`/`level` ±2、`attempts`/`deaths` ±max(2/3, 30%)、`seconds` ±35%、`kills` ±20%；`stuck`/`cleared`/`chapter`/`stage` 必须完全一致。

## 跑法

```powershell
# 新档进度（约 10~20 秒，--timeout 单位是毫秒，默认 600 必须显式放大）
unity command --format json eval -- --code 'return AutoBattle.EditorTools.NewGameProgression.Run(12345, 30, true);' --timeout 300000

# 一行判定（PASS/FAIL，含硬约束：不卡死、每章末等级、终局等级、阵亡上限）
unity command --format json eval -- --code 'return AutoBattle.EditorTools.NewGameProgression.RunAssert(12345, 30);' --timeout 300000

# 过渡轨迹（入库格式）
unity command --format json eval -- --code 'return string.Join("\n", AutoBattle.EditorTools.NewGameProgression.Trace(12345, 30));' --timeout 300000

# 平衡回归
unity command --format json eval -- --code 'return AutoBattle.EditorTools.BalanceRegression.RunStaticOnly();' --timeout 300000
unity command --format json eval -- --code 'return AutoBattle.EditorTools.BalanceRegression.Run(false);' --timeout 300000
```

## 2026-10-17 的修复与数据

### 问题

新档 1-1 必死，且无自救路径。1-1 的怪是 Lv.10（`Stages.cs` 里 `monsterLevel = 10 * chapter`，第 1 章整章 Lv.10），而主角是 Lv.1 空身：一只要打 42 击 ≈ 56 秒，自己 5.4 秒就被揍死；阵亡既不给经验也不给金币，还清空本关进度，复活后刷的是满血新怪 → 无限死亡循环。无装备能打死 Lv.10 普通怪的最低等级是 8，打掉关底精英要 12。

### 三处改动

1. **内容曲线**（`Stages.cs` / `GameConfig.ChapterOfMonsterLevel`）— 第 c 章改为 Lv.10(c−1)+1 ~ 10c，1-1 是 Lv.1，11-10 是 Lv.110。`LateGameProbe` 仍用 `10 * chapter` 造怪，等于章末档，`baseline.json` 语义不变（回归实测 PASS）。
2. **经验补偿**（`GameConfig.ChapterExpFactor`）— 怪物经验乘 `1 + (lv−1) × 0.07`（Lv.110 约 8.6 倍）。原因是升级成本二次、怪物经验线性，只改内容曲线时主角只到 Lv.66，满级/巅峰/技能卡全不可达。
3. **阵亡保底**（`GameController.HandlePlayerDied`）— 阵亡时按"已打掉的血量比例"给该怪经验，仍保留"本关进度作废"的惩罚。每次最多一只怪的全额经验，送死不是最优解。

### 探针结果（seed 12345，逐级重试上限 30）

| 阶段 | 结果 |
|---|---|
| 改之前 | 卡死 1-1，3 次重试全灭，0 击杀 |
| D1 之后 | 1-1 → 11-9 全通、0 阵亡；章末等级 10/17/23/29/35/40/46/51/56/61；卡 11-10（Lv.68，30 次全灭） |
| D2 之后 | 章末等级 11/21/32/43/53/64/76/88/100/100/100；仍卡 11-10（Lv.100 + 巅峰 93，549 击杀） |
| D3 之后 | 全程通关；第 11 章 36 次尝试 / 26 阵亡 / 2.7 分钟；总击杀 545、总阵亡 26、模拟 13.1 分钟，收尾 Lv.100 巅峰 102 |

第 11 章靠"磨"通过，与 `LateGameProbe` 记录的终局 Boss 胜率（Lv.110 Boss：par0 0% → par100 65.7%）一致，不是新的墙。

### 2026-10-17（补）过渡验证进门禁

问题不在数值，而在验证方法：两套工具都在"稳态假设"里打转——关卡表只记怪级，胜率表直接把玩家放在第 c 章、等级 10c、装备白送。那次新档死锁（Lv.1 打 Lv.10、复活无收益）**两套工具全绿**，因为没有任何一处真的从 Lv.1 走到第 2 章。

现在这条过渡是常驻哨兵，任何让成长曲线断档的改动都会立刻报红。实测：把 `ChapterExpScalePerLevel` 临时改回 0（等价于撤销经验补偿），

- `RunAssert` → FAIL 8 条：卡在 11-10（Lv.69），且第 4~10 章末等级全部低于下一章怪级下限；
- `RunStaticOnly` → FAIL 20 处，`[过渡]` 逐项列出 `ch3 endLevel: 32 → 23`、`ch4 seconds: 63 → 141` 这种漂移。

当前基线（seed 12345）：ch1~ch10 每章 10 次尝试 0 阵亡，章末等级 11/21/32/43/53/64/76/88/100/100；ch11 36 次尝试 26 阵亡；收尾 `end|stuck=0|level=100|paragon=102|kills=545|deaths=26`。

### 尚未覆盖

三个入口（稳态探针、过渡轨迹、胜率表）都不模拟技能三选一、手动换装、装备强化，也不模拟玩家操作失误；第 11 章的 26 次阵亡是"AI 只靠部分经验硬顶"的结果，真人会更早回头刷装备。改数值后建议两个探针都跑 + 开一局新档看到第 2 章。

### 2026-10-17（UI 冒烟，P1-6）

探针测不到界面，这轮 UI 修复是靠 PlayMode 跑一局 + 截图确认的，证据都留在 `shots/`：

| 截图 | 看到什么 |
|---|---|
| `smoke_ui_p1_6.png` / `smoke_fight_dps.png` | 修复前的反例：日志在滚，但「实时 DPS —」是破折号、屏幕上一个飘字都没有 |
| `smoke_dps_ok.png` / `dps_fixed.png` | 修复后：DPS 有数值（如 432 / 期望 351），怪身上能看到伤害数字 |
| `smoke_x4_floats2.png` | x4 速度下飘字依然可见（飘字改成按真实时间衰减，不再被 4 倍速压成 0.29 秒） |
| `reset_confirm.png` | 「重置」第一下只把按钮改成「确认重置？」，日志同时给出黄色警告 |

冒烟步骤：`set_autotick --interval_ms 30` → `editor_play` → eval `SetSpeedIndex(2)`（x4）→ 跑 30~60 秒 → `capture_game_view` → `editor_stop` → `set_autotick --enable false`。

---
id: "1a09f494-5502-49e1-9ca6-08f74205bdc1"
title: "Unity AutoBattle 巅峰上限 200→1000：配置漏改定位、P0→P1000 耗时实测与探针参数化"
description: "记录 MaxParagonLevel 被 game_config.json 覆盖为 200 的根因、P0→P1000 的经验总量与纯战斗/含等待两套时间（333 小时）、EndgamePacing 的 targetParagon 参数化与 WallSeconds 口径、放开上限后的属性/金币副作用，以及相关文档同步位置与探针口径陷阱。"
status: "active"
created_at: "2026-10-19T04:28:24.336Z"
updated_at: "2026-10-19T04:28:24.336Z"
content_hash: "30f0749329f368de6d9621a60a833ed15ab3a0dc4d48ac26a674dcbdc47848c4"
source_paths:
  - "HelloWorldProject/Assets/Editor/EndgamePacing.cs"
  - "HelloWorldProject/Assets/Scripts/GameConfig.cs"
  - "HelloWorldProject/Assets/Scripts/GameConfig.Loader.cs"
  - "HelloWorldProject/Assets/Scripts/GameConfigData.cs"
  - "HelloWorldProject/Assets/Resources/game_config.json"
  - "README.md"
  - "_balance/balance-analysis.md"
  - "_balance/README.md"
  - "docs/endless-mode.md"
session_ids:
  - "28fb2084-9a38-4ab2-b7cd-7588147d1dd9"
memory_body_ids:
  []
---

# Unity AutoBattle 巅峰上限 200→1000：配置漏改定位、P0→P1000 耗时实测与探针参数化

工程 `HelloWorldProject/`（Unity 2D，Editor 6000.6.5f1）。用户问「为什么巅峰上限不是 1000」与「升到 1000 要多久」。本文记录根因、实测数据、探针改动与文档同步位置，供后续调巅峰曲线或再跑探针时复用。

## 1. 根因：JSON 覆盖代码默认值（配置漂移，而非硬限制）

- `HelloWorldProject/Assets/Scripts/GameConfig.cs:526`：代码默认 `MaxParagonLevel = 1000`
- `HelloWorldProject/Assets/Scripts/GameConfigData.cs:94`：DTO 默认值同样 1000
- `HelloWorldProject/Assets/Scripts/GameConfig.Loader.cs:174`：加载器允许上限 1000，注释写明「2026-10-19 由 200 提到 1000」
- `HelloWorldProject/Assets/Resources/game_config.json:81`：实际写的是 `"MaxParagonLevel": 200`

加载时 JSON 覆盖代码默认，所以运行时一直是 200。**属于 JSON 漏改**，不是有意的 200 硬限制；此前「P200 无尽最高纪录」的结论都由这个值造成。改 JSON 为 1000 后运行时验证 `cap=1000`。

## 2. 经验曲线与累计量

- 曲线：`ParagonExpToNext(p) = 2000 + 200p + 20p²`
- `ParagonExpToNext(200) = 842,000`；`ParagonExpToNext(999) = 20,161,820`
- P0→P200 累计 **5,731 万**经验；P0→P1000 累计 **67.59 亿**经验

## 3. 实测时间（seed 20261018，最优玩家探针口径）

起点 Lv.100 + 传说装备 + 巅峰 0，在 11-10 反复「5 小怪 + 轮换 BOSS」，技能自动三选一、余钱全砸强化：

| 区间 | 纯战斗 | +刷怪等待（0.5s/只） | +商店（3s/BOSS） |
|---|---:|---:|---:|
| P0 → P200 | 114.0 min（1.9h） | 168.8 min（2.8h） | 225.4 min（3.8h） |
| P0 → P1000 | 7,996 min（133.3h / 5.6 天） | 13,991.8 min（233.2h / 9.7 天） | **19,989.3 min（333.2h / 13.9 天）** |
| P200 → P1000 增量 | 7,882 min（131.4h） | 13,823 min（230.4h） | 19,763.9 min（329.4h / 13.7 天） |

这一轮共 **119,955 次挑战 / 719,487 击杀 / 237 次阵亡**。1× 连续游玩约 333 小时即 13.9 天；4× 约 83 小时（3.5 天）。

## 4. 探针参数化（EndgamePacing.cs）

`HelloWorldProject/Assets/Editor/EndgamePacing.cs`：

- 签名改为 `Run(int seed = 20261018, int maxStages = 20000, bool verbose = false, int targetParagon = 200)`，内部 `targetParagon = Mathf.Clamp(targetParagon, 1, GameConfig.MaxParagonLevel)`
- 里程碑从固定 `int[] {1,30,50,100,150,200}` 改为列表 1/30/50/100/150/200/300/…/1000，只取 `<= targetParagon` 的档位，并保证终点档一定出现
- 新增 `WallSeconds() = seconds + kills * GameConfig.MonsterRespawnDelay + bossFights * GameConfig.ShopDuration`，同时输出「纯战斗」与「含刷怪+商店」两种时间；结束还打印口径说明
- 菜单改名：**Tools/测试/终局节奏探针（巅峰 0 → 目标）**
- 跑 P1000 用 `Run(20261018, 200000, false, 1000)`（`maxStages` 需明显调大）

## 5. 副作用与判断

放开上限后 `ParagonAttackPerLevel=1` / `ParagonHpPerLevel=50` 累计到 **+1000 攻 / +1000 防 / +50,000 血**，技能等级到 101，11-10 完全无压力；探针收尾兜里剩 **8.6 亿金币**，金币已无消耗出口。结论：1000 级是约 14 天连续刷同一关的长尾目标，不是两小时终局体验的自然延伸；要让它可玩，需改 `ParagonExpQuadratic`、加高层巅峰专属内容或加金币出口，而不是只改 cap。

## 6. 口径陷阱（重要）

- 三组满级时间不可混用：旧探针（不选卡不强化）+ q=12 = 136.6 / 110.3 min；决策层探针 + q=12 = 76.3 / 77.3 min；决策层探针 + q=20 = 108.0 / 113.7 min（纯战斗）。动技能表、强化费用或怪物属性时三个探针要一起重跑，只看一份会得出相反结论。
- `EndgamePacing` / `NewGameProgression` 的数字是「一次都不洗练」的玩家口径，不能当作洗练加速的证据（`ProbePolicy` 只管三选一与强化）。
- 表里的纯战斗时间与 `docs/endless-mode.md` 的 P200 无尽层数是旧上限口径；P1000 的无尽层数尚未重跑。

## 7. 验证与文档同步位置

- 运行时：`cap=1000`；编译 `compilationFailed=false`、console 0 error / 0 warn；`BalanceRegression.RunStaticOnly()` 返回 **PASS — 静态哨兵与基线一致**；`AssetDatabase.Refresh(ForceUpdate)` 成功。
- 已同步：`README.md:19`、`README.md:155-163`（探针段落）、`_balance/balance-analysis.md:33` 与 §4.1（P0→P1000 19,989.3min）、`_balance/README.md:120-121` 与 :140-142、`docs/endless-mode.md:943`（P200 旧上限说明）、`docs/endless-mode.md:968-970`（结论与待重跑）、`docs/equipment-reforge.md:307`、`HelloWorldProject/Assets/Scripts/GameConfig.cs:546`（曲线注释「5,731 万经验，满级回到约两小时，别随手往下调」）。

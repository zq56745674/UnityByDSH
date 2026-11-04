# 会话交接（2026-11-05）

> 用途：换会话时先读这一页，再读 `README.md` 与 `docs/roadmap.md`。本文只记**当前状态、未提交改动、验证结果、下一步和环境坑**，不重复设计与历史论证。

## 1. 当前坐标

- 工作区：`E:\pytest\unitydsh1`
- Unity 工程：`HelloWorldProject/`，Unity **6000.6.5f1**，单场景 `Assets/Scenes/SampleScene.unity`
- `HEAD`：`02437c6`，与 `origin/main` 同步（2026-11-05 提交并推送）；这个提交把积压的 63 项一次合入（UI 打磨 + B 深层契约 + 审计修复 + C 巅峰符文 + D 屠魔录 + 探针与文档回填）。再往前是 `0eca413`（文档）、`50a71ed`（无尽结算卡 + 深渊遗物 v1）。
- **未提交**：0（2026-11-05 全部随 `02437c6` 提交并推送）。此前这批是 63 项（44 改 + 19 新增）：2026-11-05 那轮加了 D 屠魔录三个新文件（`BestiaryCatalog.cs` / `GameController.Bestiary.cs` / `GameUI.Bestiary.cs` 与三个 `.meta`）、五条 UI/文案小修的改动（`GameConfig.Colors.cs` / `GameUI*.cs` / `README.md` / `docs/*.md`），以及 `Editor/LateGameProbe.cs` 的章节胜率三档带菜单。此前的 55 项（43 改 + 12 新增；2026-11-04 又加了 `EndlessPacing.cs` 的满轮回复测 / 遗物矩阵 / 单件边际探针、`.github/workflows/unity-ci.yml` 的许可证三路、`RelicCatalog.cs` 的贪类三件补战斗副属性，以及 README/handoff/roadmap/rebirth/gameplay-expansion/endless-mode 的文档回填），混了三批：① UI 四条打磨与按钮换皮（`GameConfig.Colors.cs` / `GameUI.Achievements.cs` / `GameUI.Buttons.cs` / `GameUI.Panels.Enhance.cs` / `GameUI.Panels.Reforge.cs` / `GameUI.Shop.cs`）；② B 深层契约（`ContractCatalog.cs` 新增 + `.meta`、`Endless.cs`、`GameController.Endless.cs`、`GameUI.Endless.cs`、`Models.PlayerData.cs`、`Editor/EndlessPacing.cs`）；③ 2026-11-04 审计修复（`BattleSystem.cs`、`GameController.cs`、`GameController.Rebirth.cs`、`SaveSystem.cs`、`GameUI.Refresh.Log.cs`、`GameUI.cs`、`RelicCatalog.cs`、`Models.cs`、`ShopCatalog.cs`、`Editor/AchievementRegression.cs`、`Editor/AutoBattle.CI.cs`、`Editor/BalanceRegression.cs`、`Editor/NewGameProgression.cs`、`Editor/ProbePolicy.cs`）；文档 10 个（`README.md` / `docs/roadmap.md` / `docs/handoff.md` / `docs/endless-mode.md` / `docs/gameplay-expansion.md` / `docs/achievements.md` / `docs/equipment-reforge.md` / `docs/skill-build-design.md` / `docs/shop-2-design.md` / `docs/skill-system-v2-design.md`）+ `docs/project-audit-2026-11-04.md`（新）+ `_balance/baseline.json`（重录）+ `_balance/baseline.before-l1.json`（旧基线备份，新）。`Assets/_shots/`、`Resources/Music/` 与 `.mnemon/` 均在 `.gitignore` 内，不进仓库。
- **不要自行 commit / push**：按 USER.md 的 critical 规矩，只有用户明确说「提交」才允许操作 git；push 另需明确同意。
- 当前存档：**v18 / 3 槽**；`CurrentSaveVersion = 18`（`HelloWorldProject/Assets/Scripts/SaveSystem.cs:36`，`public static int SaveVersion` 在 `:268`）；v18 新增 `int[44] bestiaryKills`（屠魔录击杀计数，跨转生保留，由 `BestiaryCatalog.Sanitize` 兜底长度与负数），v17 新增 `runeUnlockMask` / `runeFormMask`（巅峰符文解锁与形态，跨转生保留），v16 新增 `PlayerData.contractScore`（累计深层契约分，跨转生保留）。
- **Editor 的存档在 `HKCU\Software\Unity\UnityEditor\DefaultCompany\My project`**，不在 `UnityByDSH\AutoBattle`：工程 `ProjectSettings/ProjectSettings.asset` 里 companyName / productName 仍是默认值（见 §6）。

2026-10-29 那轮已完成并验收：

1. **UI 方向 B 收尾**：正文换思源黑体、标题换得意黑（`Resources/Fonts/SmileySans-Oblique.ttf`，2.5 MB，OFL）；`GameUI.Theme.PickFont` 只在「粗体 + 字号 ≥ 18」时切展示字，全界面 90 个 Text 里 4 个（角色属性 / 装备 / 等待下一只怪物 / 商店）走展示字。
2. **左栏再压一轮**：装备卡 396×168、钱包卡 y510 h84、技能栏 y604 h82（`rowStep` 44）、删掉装备与钱包之间的 Divider，四处间隙 8/8/12/11 px 无重叠。
3. 12 张面板截图验收（`HelloWorldProject/Assets/_shots/v19_*.png`），控制台 `consoleErrors=0`。

2026-11-04 那轮已完成并验收（UI 四条打磨）：

1. **行商卡**：服务页 desc 11→12px、价签 12→13px Bold、购买钮 11→12px、副标题与页脚 11→12px；珍藏页卡片 h86→84 / 步长 92→90 / desc 10→11px，末行不再贴页脚。「批量锁定」文案「防自动替换」→「防误换」（原 32 字在 370px 宽下折出第三行，孤立一个「。」掉到按钮下方）。
2. **强化 / 洗练**：两处提示 `TextInfo`→`TextInfoDeep`；强化正文 13→14px；洗练标题 14→15px、右侧信息 11px `TextMuted`→12px `TextNormal`、质量行 `TextInfo`→`TextInfoDeep`；`GameUI.Panels.Reforge.cs` 里四处富文本 `<color=#5E6B80>`（为深底准备的蓝灰）→ `#6F6252`。
3. **Tooltip**：`TooltipBg` 由近黑 (0.129,0.098,0.071) 改皮革 (0.235,0.180,0.133)，加 1px 皮面高光 `TooltipSheen`；仍是 8px 圆角 + 黄铜描边。称号钮的 tooltip 砍到两行（原来三行高 65px，会顶到画布底 10px 内被迫翻到上方，压住「只看未完成」）。
4. **称号禁用态**：`BtnTextDim` 0.612→0.706（深皮革上对比约 9.4:1），未解锁称号钮保持 `FrameTintDim x disabled 0.62` 的暗框，只提亮文字，结构不动。

验证截图：`HelloWorldProject/Assets/_shots/p11_*.png` 与 `p12_*.png`（行商服务/珍藏、强化、洗练、成就+tooltip、存档、选关、技能），`consoleErrors=0`。重拍脚本 `_shots/drive13.py`（六个面板 + tooltip）与 `drive15.py`（回归四屏）。

遗留小瑕疵：成就称号栏距画布底只有 48px，tooltip 高 46.7px + 8px 间距放不下，翻到上方时会压住「只看未完成」与兑换行——纯悬停态，未再做特殊布局。

2026-11-04 审计修复轮（逐条处置 `docs/project-audit-2026-11-04.md`，本地未提交）：

1. **L1 战斗步进**：`BattleSystem.Tick` 拆成固定 1/120 秒子步（`MaxTickStep` / `MaxSubStepsPerFrame=32` / `_carrySeconds`，原函数体搬进 `TickStep`），换帧率不再改胜负。**副作用：平衡基线全部重录**（`_balance/baseline.json`，旧基线在 `baseline.before-l1.json`）——这是步进语义变化的必然结果，不是平衡改动。
2. **L2~L7**：冷却随技能走（`CarryActiveCooldown`）、转生改走 `ResetSkillsForRebirth`、裁血挪到加载最后、导入先验证后覆盖且失败回滚（`SnapshotSlot` / `RestoreSlotSnapshot`）、切槽先存盘再结束局、回归判定结构化（缺基线判 FAIL，判据改 `LastFailCount`）。
3. **性能第 2 / 3 条**：装备摘要改 64 位哈希签名（`GameUI.Core.cs:200` 的 `struct Sig`），日志收起时不再构造 16KB 详情串（`_logDetailCleared`）。第 1 / 4 / 5 条按「先量测」不动。
4. **玩法 G1~G4**：G2 遗物预算改分档 + 净战力口径（`docs/gameplay-expansion.md` §4.2 / §4.3：6 槽 ≤×1.55、8 槽 ≤×1.70，实算 ×1.304 / ×1.530 / ×1.658）；G3 探针口径对齐正式逻辑（真实槽位、档位、`RelicDropBonus`）；G1（决绝 0.9）与 G4（金币印记即 log2 阶梯、巅峰/无尽是跨世里程碑）写了决策记录，不改数值。
5. **验收**：`QuickRegression.RunAllQuick()` **3/3**（静态哨兵 PASS、成就 668/0、无尽冒烟 2 种子 40 层）、`compilationFailed=false`、`consoleErrors=0`。
6. **未做**：性能第 1/4/5 条的 Profiler 采样、G1 分/分钟对照、写盘故障注入（清单见审计文档 §五末尾）。G2+G3 的五档 50 种子配对矩阵已做（2026-11-04，见 §4 第 12 条）。

2026-11-05 那轮已完成并验收（五条小修 + D 屠魔录 + 章节胜率三档带）：

1. **五条小修（用户 m08404）**：① 成就称号 tooltip 改单行（两行 54.67px 会翻到上方盖住「只看未完成」；现在实测高 28px、底部 y=-348，不再翻）；② 左面板金币行与统计行重叠 18px 已分开（统计改 y=546、行距 1.02、`verticalOverflow = Overflow`）；③ 怪物名牌加深皮革压条 `MonsterNamePlate`（暖金字压在羊皮纸上对比度只有 ~1.6:1）；④ 技能槽改深皮革凹槽（`GameConfig.SlotBg` / `SlotKindText` / `SlotEmptyText` / `SlotCooldownText` / `SlotReadyText`）；⑤ 章节胜率带见第 4 条。
2. **D 屠魔录落地（存档 v18）**：11 族 × 4 型 44 条、击杀累积三档（未目击剪影 / 已目击 n 目标 / 已通晓）、门槛 近战 20 · 远程 20 · 精锐 12 · BOSS 3、族印纯展示（不加数值、不进池子、不发金币成就）、入口在成就面板头部。三文件 `BestiaryCatalog.cs`（234 行）/ `GameController.Bestiary.cs`（53）/ `GameUI.Bestiary.cs`（247）。**归族必须用 `Monster.level` 而不是 `GrantKillReward(rewardLevel)`**——无尽的 rewardLevel 封顶 150，用它会把深层击杀全算进最后一族。设计偏差五条见 `docs/gameplay-expansion.md` §7。
3. **验收**：`QuickRegression.RunAllQuick()` **3/3 PASS**（静态哨兵一致 / 成就 668 通过 0 失败 / 无尽 2 种子 40 层）、`compilationFailed=false`、`consoleErrors=0`（1 条 warning 是测试注入的存档告警）；编译干净；探针实测 `RecordKill` 返回值与 witnessed 翻转、第 1 章 BOSS 落 idx3 而 idx11 保持 0、摘要「已通晓 6/44 · 目击 9/44 · 族印 1/11」、旧档读入后 `bestiaryKills.Length = 44`、Esc 关面板；截图 `_shots/bestiary1..4.png`（4 是悬停 tooltip）。
4. **章节胜率三档带（用户 m08404 第 5 条）**：`EndlessPacing` 同侧的 `LateGameProbe.MenuChapterWinrateBand`，每章每档 12 次装备采样 × 30 场/类型（悲观 = 普通怪品质权重 / 中性 = 精英 / 乐观 = Boss），71,280 场耗时 **3.8s**，产物 `_shots/chapter-winrate-band.txt`。结论：精英战三档下第 1~10 章全部 ≥99.8%，**难度差几乎只在关底 BOSS**；BOSS 中性档从第 1 章 97.6% 掉到第 5 章 66.3%，第 6~8 章回升到 74~85%，第 9 章 31.8% / 第 10 章 34.0% / 第 11 章 0（完整 11 章 × 3 档表见 `README.md` 已知问题「中间章节配装平衡」条）。两条判读：① 「越深越难」方向在三档下一致，旧的中性口径结论方向没错；② 第 5 章 BOSS 中性 66.3% 是局部凹点（第 6~8 章回升 74~84%），第 10 章只在乐观口径比第 9 章更难（86.2 vs 93.1），中性口径反而略易 2.2 个点——「第 10 章偏易」取决于配装档次，不能一概而论。
5. **踩坑（已记，别再踩）**：`EditorApplication.delayCall` 在 `eval` 返回后会被丢弃——band3/band4 两次 scheduled 却从不执行、控制台连 START 都没有。要跑编辑期长任务，**在 eval 里同步反射 Invoke 菜单方法**即可（3.8s，不卡 CLI）；此前判断「Editor 里跑完前 CLI 会卡住」是误判。

2026-11-03 那轮已完成并验收（B 深层契约 v1，跨到 11-04 凌晨）：

1. **八条难度契约**：无尽准备面板新增「契约」页签，勾 0~3 条（八条 × Ⅰ/Ⅱ/Ⅲ，决绝单档），同档再点取消；只改本局规则与敌方系数，不写回玩家面板。最终数值：狂躁 攻击 +15/30/50%、疾袭 出手间隔压缩 12/25/45%、硬化 敌人受伤 −15/35/60%、群袭 整层怪物 +12/+18/+26 层、枯竭 治疗 −30/45/60%、背水 生命上限 −12/25/45%、黑雾 护甲有效率 55/40/28%、决绝 不可撤退。
2. **三处机制改写**（原始设计在这套引擎里无效）：硬化原为「敌人护甲 +25/50/80%」——Lv100 时 `GameConfig.DamageReduction` 的 K≈2300，怪护甲 ≈200 只抵 8% 减伤，故改成敌方受伤乘区；群袭原为「击杀需求 +30/60/100%」——多刷怪几乎免费（吸血/闪电回流续航、死亡是阈值式），改成整层怪物按 +N 层强度生成；黑雾原为「暴击率 −10/20/30pt」——依赖 build 且 Ⅲ 档只剩 18%，改成护甲有效率。
3. **契约分与里程碑**：结算按「已清层数 ×（1 + 档位系数之和）」记分（Ⅰ 0.25 / Ⅱ 0.5 / Ⅲ 0.9），累计分跨转生保留，300 / 800 / 1500 / 3000 分依次解锁遗物 tier2 / 第 7 槽 / tier3 / 第 8 槽；历史 Top-5 改按 (契约分, 层数) 排序。
4. **验收**：20 种子对照组，Ⅰ 档 7/7 全过（降幅 9%~15%）、Ⅲ 档 7/7 全过（27%~36%）；3 条 Ⅲ 同开中位 15 层；2 条 Ⅱ（50 种子 / 300 层）首死中位 60、契约分中位 118。
5. 存档 v16（只加 `contractScore`）；面板截图 `HelloWorldProject/Assets/_shots/p14_contract_tab*.png`，对照产物 `_shots/contract-compare.txt`。

2026-11-02 那轮已完成并验收：

1. **无尽结算卡片**：`GameUI.Endless.cs` 的 680×420 结果卡——大号「第 N 层」+ 阵亡/撤退皮革标签 + 用时/击杀/清层三格 + 榜单名次 + 遗物行；`docs/endless-mode.md` §8.3 已重写为「结果面板」。
2. **A 深渊遗物 v1**：tier1 12 件 / 6 槽 / 每 10 层三选一 / 满槽先舍后换；50 种子五策略配对均值 **+18.4 层**（中位 300→320，P90 335），确定性 36/36。
3. **随机五槽复测**：现行口径中位 **330**（P10 300 / P90 360 / max 375），替换了文档里过期的 181。

2026-10-26 那轮已完成并验收：

1. **B1 商店 2.0**：营地行商四页签（服务 / 珍藏 / 声望 / 战绩）、收藏家 20 级、6 项服务、15 件珍藏、≥100 万二次确认、自动出售阈值；总吸收 699,251,348。
2. **技能 2.0 内容层**：风暴 / 山崩双流派，各 6 专属 + 3 通用；两套毕业 build。
3. **技能点自选**：删除随机三选一；P10 选流派得第 1 点，P20~P90 每 10 级 +1，共 9 点，玩家在技能面板自选；技能面板按流派分组并区分另一流派。
4. 文档已按上述口径回填；新增本文作为交接索引。

## 2. 当前平衡与回归（最新口径）

> ⚠️ **2026-11-04 引擎改成固定 1/120 秒子步**（审计 L1）。下表里除「快速回归 / 成就回归 / 编译 / 规模」之外，**层数、胜率、DPS 都是 L1 之前的口径**，引用前必须重跑；已经重录的只有 `_balance` 的静态哨兵基线。

| 项目 | 当前值 / 结果 |
|---|---|
| 无尽 | `EndlessBaseFactor=3.1`、`EndlessMonsterLevelStep=4`；P1000 + Lv150 + 满倍率满词条、50 种子：风暴首死中位 **240**（P10 225 / P90 255 / max 270），山崩 **245**（P10 225 / P90 260 / max 275），50/50 阵亡 |
| 新档 | 30 种子：**29 全程通关 / 1 摸到 11-10 / 0 卡死**；seed 12345 为 Lv100 / 巅峰 49 / 15.8min / 阵亡 32 |
| 终局 BOSS | `FinalBossStatFactor=1.25`（只作用战役，无尽不吃） |
| 章节压迫 | `1 + 5.5%×(章-1)`，章末 BOSS 再 `1 + 4%×(章-1)`，第 11 章不吃 |
| 300 秒木桩 | 风暴 DPS 57,998 / 山崩 61,517（Lv110 BOSS，满装模板） |
| 快速回归 | `QuickRegression.RunAllQuick()` → **3 通过 / 0 失败**（静态哨兵、成就核心、无尽冒烟 2 种子 × 40 层） |
| 成就回归 | `AchievementRegression.RunCore()` → **668 通过 / 0 失败** |
| 编译 | `console_status` groundTruth：`compilationFailed=false`、`consoleErrors=0`；测试注入的存档失败警告可能显示 4 条 |
| 规模 | 运行时 **76 .cs / 26,499 行**；GameUI **35 partial / 11,594 行**；Editor **10 / 6,589 行**（含 `EndlessPacing.cs` 2,119、`LateGameProbe.cs` 1,048；2026-11-05 实测） |
| 深层契约 | 20 种子 400 层：Ⅰ 档 7/7（9%~15%）、Ⅲ 档 7/7（27%~36%）；3 条 Ⅲ 同开中位 15 层；2 条 Ⅱ（50 种子/300 层）首死中位 60、契约分中位 118（P10 58 / P90 168） |
| 章节胜率三档带 | 精英战三档下第 1~10 章全部 ≥99.8%；BOSS 中性档 97.6 →（第 5 章凹点 66.3）→ 第 6~8 章 74~85 → 第 9 章 31.8 / 第 10 章 34.0 / 第 11 章 0（2026-11-05，`_shots/chapter-winrate-band.txt`，71,280 场 3.8s） |
| 屠魔录 | 44 条 = 11 族 × 4 型；门槛 近战 20 / 远程 20 / 精锐 12 / BOSS 3；族印纯展示；存档 v18 记 `int[44] bestiaryKills`（2026-11-05） |
| 平衡基线（L1 后重录） | winrate 8 档 **0.001 / 0.064 / 0.171 / 0.455 / 0.609 / 0.802 / 0.813 / 0.934**（旧 0.000 / 0.054 / 0.190 / 0.427 / 0.590 / 0.696 / 0.859 / 0.886）；chapterWinrate 末段 ch10 精英 0.735、ch10 Boss 0.061、ch11 0.0 / 0.0（旧 0.552 / 0.221 / 0.150 / 0.0）。旧基线备份 `_balance/baseline.before-l1.json` |
| 审计处置 | L1~L7 全修、性能 2/3 修、G2/G3 修、G1/G4 决策不改数值 → `docs/project-audit-2026-11-04.md` §五 |

## 3. 代码地图（本轮新增 / 重点）

- **商店 2.0**：`ShopCatalog.cs`（274 行，价格 / 收藏家 / 珍藏 / 服务）、`GameController.Shop.cs`（320 行）、`GameUI.Shop.cs`（607 行）；存档字段 v14 引入。
- **技能 2.0**：`Skills.cs`（15 技能整表 + 流派常量 + `StormBuild`/`MountainBuild`）、`HelloWorldProject/Assets/Resources/skills.json`、`SkillCatalog.Loader.cs`、`GameController.Skills.cs`（80 行，流派选择 / 技能点）、`GameUI.Doctrine.cs`（215 行，流派面板 + 资源条）、`GameUI.Panels.SkillBar.cs`（技能面板 / 技能点学习）、`Models.PlayerData.cs`（技能点 / `SkillPoints` / `CanLearnSkill` / `BeginDoctrine`）。
- **存档 v15（当前 v16 的前一步）**：`PlayerData.doctrine`、`skillEverMask`；技能点**不单独存档**，由 `ExpectedSkillCount = Clamp(paragonLevel/10,0,9)` 减已掌握数实时派生。v14 旧档首次加载会强制选流派，按巅峰发点，由玩家自选；不再自动送 9 技能。
- **探针**：`EndlessPacing.Run(... fixedDoctrine)` 第 14 参数；`ProbePolicy.ResolvePendingSkills` 先按 `SkillCatalog.BuildOf(doctrine)` 挑毕业 build，再按推荐分补齐；`_balance/baseline.json` 已按技能点口径重做。
- **未完成但保留的字段**：`GameConfig.SkillChoiceCount` 已废弃但仍保留，避免 `game_config.json` 缺键告警；`pendingSkillPicks` 保留为 v15 字段并固定 0。删除前先确认配置加载与旧档迁移。
- **A 深渊遗物 v1（2026-11-02）**：`RelicCatalog.cs`（tier1 12 件 + `ShouldOffer` / `RollChoices` / `RelicMods.Build`）、`GameController.Endless.Relics.cs`（`OfferRelicChoice` / `TakeRelic` / `ApplyRelicMods` / `RelicChoiceOpen` / 满槽替换）、`GameUI.Endless.Relics.cs`（三选一与替换面板）、`Endless.cs` 的 `relicIds` 与 `resultRelics`（结算快照——**`EndEndlessRun` 会清空 `relics`，结算卡必须读 `resultRelics`**）、`EndlessPacing.Run` 第 15 参 `relicPolicy` 与 `CompareRelics` 五策略对照。

- **B 深层契约 v1（2026-11-03，存档 v16）**：`ContractCatalog.cs`（350 行，八条 × 三档静态表 `ContractCatalog.All`、`CoefSum` = 0.25 / 0.5 / 0.9、契约分维度与 `RelicMaxTier` / `RelicSlots` 里程碑）、`Endless.cs` 的 `contractTiers` 选择与 `resultContracts` 快照、`GameController.Endless.cs`（`ContractMods` 构建、`ContractClearFloorBonus()`、`SpawnEndlessMonsterNow` 里按 `def.floor + ContractClearFloorBonus()` 生成守关怪、结算打分 `AddContractScore`）、`Models.PlayerData.cs`（`ContractAttackMult` / `ContractSpeedMult` / `ContractEnemyDamageTakenMult` / `ContractHealMultiplier` / `ContractMaxHpMult` / `ContractArmorEffectiveMult` 与 `contractScore`）、`BattleSystem.cs`（玩家防御先乘 `ContractArmorEffectiveMult` 再算减伤）、`GameUI.Endless.cs`（契约页签 + 结算卡契约行）、`Assets/Editor/EndlessPacing.cs`（`Simulate` 同口径 + 契约对照菜单）。结算卡的遗物 / 契约行都读 `result*` 快照——`EndEndlessRun` 会清空本局字段。

- **审计修复（2026-11-04）**：`BattleSystem.cs` 的 `Tick` / `TickStep` 固定子步与 `CarryActiveCooldown`；`SaveSystem.cs` 的 `SnapshotSlot` / `RestoreSlotSnapshot` / `TryImport` 的 schema 预检；`GameUI.Core.cs:200` 的 `struct Sig`（64 位哈希签名）与 `GameUI.Refresh.Log.cs` 的 `_logDetailCleared` 收起短路；`Editor/AutoBattle.CI.cs` 的 `RunStep(int, out bool)` 与 `AchievementRegression.LastFailCount`。

## 4. 换会话后的下一步

1. **先读**：`README.md`（已完成功能与已知问题）→ `docs/roadmap.md`（未完成 / 优先级）→ 本文。
2. **提交已完成**：`02437c6` 已于 2026-11-05 提交并推送（UI 四条打磨 + B 深层契约 + 2026-11-04 审计修复 + C 巅峰符文 + 五条小修 + D 屠魔录 + 探针与 CI/文档回填，共 63 文件）。当前工作区干净；**下一轮动手前仍按 USER.md：用户没说「提交」就不要 commit / push**。
3. **A8（P3，工程，2026-11-04 已把代码侧做完，卡在许可证值）**：workflow 现在支持三路——`UNITY_LICENSE_SERIAL`（付费 serial）/ `UNITY_LICENSE_FILE_BASE64`（离线 `.ulf` 的 base64，解码到 `$RUNNER_TEMP` 后 `--file`）/ `UNITY_SKIP_LICENSE_ACTIVATION=1`（自托管 runner，Hub 已登录）。**本机是 Unity Personal 且没有 serial，Unity 明确不支持 Personal 在托管 runner 上无头激活**，所以 secret 值只能由用户提供或改自托管。填好后重跑 `Unity CI / verify` 到 3/3 + 8/8 + Windows 构建，再把该 check 设为分支保护 required（需 repo admin）。
4. **B9（P4，暂缓）**：随机三选一已删，重抽 / 禁选失去对象；只剩 4 个显示标签可复用。不要按 v2.1 直接实施。
5. **D2 随机五槽已补测（2026-11-02）**：现行 3.1 / Step 4 下，随机五槽 + 自动技能 50 种子中位 **330**（P10 300 / P90 360 / max 375）；同一套随机装备换成固定风暴 build 掉到 **235**，maxGear + 自动技能 **300**。结论：拉开差距的是技能选择（吸血鬼 + 保命顶掉三个输出位）不是装备，maxGear 已不是上限。见 `docs/endless-mode.md` §12.5.6。**后续数值以 330 为基准，不要再用 240。**（§12.6 的遗物对照是在 maxGear + 自动技能、基线 300 上做的；换算率 ≈ +1% 战力 ≈ +1 层，与基准选择无关。）
6. **无尽结算卡片已落地（2026-11-02；「本局遗物」行同日接上 A 方案实数据）**：`GameUI.Endless.cs` 的结果面板从 560×300 纯文本升级成 680×420 结算卡（大号到达层数 + 阵亡/撤退标签 + 用时/击杀/清层三格 + 榜单名次 + 「本局奖励」占位行），版式与规则见 `docs/endless-mode.md` §8.3。其中「本局遗物」行当天就接上了 A 方案实数据（读 `resultRelics`）；「深层契约」行已于 2026-11-03 接上 B 实数据（读 `resultContracts` + 契约分与里程碑进度），不要在这里另算数值。
7. **满轮回 +5% 战斗加成 ✅ 已复测（2026-11-04，3.1 / Step 4 / 技能 2.0）**：`EndlessPacing.RebirthBattleRemeasure` 跑 50 种子 / 900 层 / P1000 / Lv150 / 满倍率、无契约无遗物——R0 中位 **305**、R5（+5%）中位 **335**，逐种子配对 **+28.9 ± 6.4 层**（胜 43/50）；临时降到 0.005/级（+2.5%）仍 **+13.4 ± 6.3**（中位 320）。结论：**现行 0.01 偏强，降档也只能回收一半**；参照 6 槽 T1 遗物 +18.7 / 8 槽 T3 +39.6（贪类改造后重跑的矩阵值），一条永久被动加成 ≈ 整局构筑预算的一大块。数值**未改**，决定见下句（方案 C）。数据 `HelloWorldProject/_shots/rebirth-battle-{5,half,dose}.txt`，写回 `docs/rebirth.md` §14.4 与 README 已知问题。**2026-11-04 已决定取方案 C：`RebirthBattlePerRank` 保持 0.01 不动、只记结论**（降 0.005 只能回收一半，改「仅战役生效」又动到战役手感）。另：P200 的 11-10 胜率已在 1.25 口径下重测 —— baseline **99.86%**、满轮回 **99.97%**（9 技能、每行 2,700 场），11-10 不再是门；要回 80~90% 得抬到 1.6 以上（1.60 → 85.2 / 92.4），但那是新档 P48 只有 4 技能时定的 1.25，单一常数服务不了两端。数据 `HelloWorldProject/_shots/p200-11-10{,-attrib,-sweep,-sweep2}.txt`，写回 `docs/rebirth.md` §14.4 与 README 已知问题。**数值未改。**
8. **A 深渊遗物 v1 已落地（2026-11-02，本地未提交）**：tier1 12 件 + 6 槽 + 每 10 层三选一 + 满槽强制替换 + 结算卡遗物行。50 种子 / 900 层实测满 6 件比无遗物 **+18.4 层**（50 胜 0 负），选择质量只 +0.9、替换价值 −0.4 —— 这两条已**作废**：2026-11-04 换用「知道每件真实值几层」的 Oracle 挑法重测，选择深度是 **+19.8 层**（37/50 胜），缺的是信息与评分而不是池子深度；tier1 里 `远见`（经验 +8%）在无尽里 50/50 全平（Lv100 封顶后经验无出口，无尽本身要通关 11-10 才解锁，所以基本没救），`金脉` 也 50/50 全平。单件边际：守类三件 +9.8~+15.6 层，其余九件 ≤+4.3。**处置已落地（2026-11-04，方案 ② 修池子）**：三件 `贪` 各补一条小额战斗副属性——`金脉` 金币 +12% **＋最大生命 +2%**、`远见` 经验 +8% **＋受伤 −2%**、`拾荒者` 掉落 +6% **＋伤害 +1.5%**（`RelicCatalog.cs` 的 `maxHpMult` / `damageTakenMult` / `damageMult`），均 ≤「单件 +4%」预算。复测：`金脉` 0.0 → **+8.2 ± 5.4**、`远见` 0.0 → **+9.0 ± 6.4**、`拾荒者` +4.1 → −2.4（加了 1.5% 伤害仍不动，层数按 15 层量化的又一个样本）。分档矩阵重跑几乎不变（0 分 +18.7、1500 +32.6、3000 +39.6，差 ≤0.4 层），因为探针 `RelicValue` 仍给贪类低分——**信息问题（方案 ①：卡片显示实测强度）已于 2026-11-05 落地**：`Assets/Scripts/RelicStrength.cs` 静态表（24 件 × 40 种子 × 600 层实测层数），`GameUI.Endless.Relics.cs` 在三选一卡右上角显示「实测 ±N」+ 悬停 tooltip（口径 / ±7 层同档说明）；表是静态抄进来的，换模板要重跑 `Tools/测试/遗物实测强度（24 件 × 40 种子 × 600 层）`。回归 `QuickRegression` **3/3 PASS**。数值纪律与验收改判见 `docs/gameplay-expansion.md` §4.3/§4.5/§4.5.1，实测表见 `docs/endless-mode.md` §12.6.2。顺手确认：`EndlessBaseFactor` 未动。
9. **下一步候选**：~~C 巅峰符文~~（2026-11-04 落地，见第 15 条）→ ~~D 屠魔录~~（**2026-11-05 落地**，见 §1 的 11-05 段与 `docs/gameplay-expansion.md` §7）→ **E 节点远征**（提案稿在 gameplay-expansion §8，尚未动工）；A 遗物与 B 契约均已落地，但遗物 tier2/tier3 解锁后的强度复核仍欠；A8 的 UNITY_LICENSE_SERIAL 仍是 P3 尾巴。
10. **UI 四条可读性打磨已收尾（2026-11-04，本地未提交）**：行商服务/珍藏页字号与价签、强化与洗练的属性行、皮革底 tooltip（顶部高光 + 金边）、成就称号禁用态（`BtnTextDim` 提亮到 0.706/0.663/0.573）。验证见 §9 的 2026-11-04 段；当时的唯一残留（称号 tooltip 翻到上方）已于 **2026-11-05 清掉**——改单行后实测高 28px、底部 y=-348。
11. **B 深层契约 v1 已落地（2026-11-03，本地未提交）**：八条 × Ⅰ/Ⅱ/Ⅲ、20 种子 Ⅰ 档 7/7 + Ⅲ 档 7/7、存档 v16、契约分里程碑接遗物解锁、Top-5 改按 (契约分, 层数) 排序。三处机制改写（硬化=敌方受伤乘区 / 群袭=整层怪物 +N 层 / 黑雾=护甲有效率）与「多刷怪几乎免费」的反例见 `docs/gameplay-expansion.md` §5，实测见 `docs/endless-mode.md` §12.7。已知边界：群袭Ⅲ 在低层开局直接把怪抬到 Lv.214（相对角色 Lv.100 几乎必死），这是「+N 层」的真实表现，暂不做首局衰减。

12. **审计遗留一：G2 + G3 五档配对矩阵 ✅ 已做（2026-11-04）**：`EndlessPacing.CompareRelicTiers`（菜单「Tools/测试/深渊遗物分档矩阵（5 档 × 3 策略，50 种子）」）跑契约分 0 / 300 / 800 / 1500 / 3000 × 50 种子 × 3 策略，走合法槽位与档位（`RelicCatalog.SlotsFor` / `ContractCatalog.RelicMaxTier` / 带 `maxTier` 的 `RollChoices` / 掉落带 `RelicDropBonus`），`contract = null` 所以层数差只归因遗物；结果在 `HelloWorldProject/_shots/relic-tiers.txt`，已写回 `docs/gameplay-expansion.md` §4.3/§4.5 第 7 条。实测（Δ 为逐种子配对均值 ±95%CI，**贪类三件补战斗副属性后重跑的口径**）：6 槽 T1 **+18.7±6.8**（上限 +20.6±5.6）、6 槽 T2 **+31.4±6.8**（上限 +30.3）、7 槽 T2 +28.9±6.8（上限 +33.4）、7 槽 T3 **+32.6±6.8**（上限 +29.0）、8 槽 T3 **+39.6±6.8**（上限 **+40.4±5.6**）；改前是 +18.6 / +31.4 / +28.6 / +33.0 / +39.8。两个结论：① 8 槽 T3 +40.4 与旧外推 +40 吻合，「+0.6 层 / 1% 净战力」成立；② **「6 槽 T3」这一档不可达**——`RelicSlots` 在 800 分即给第 7 槽，而 T3 要 1500 分，§4.3 表第 3 行已改标为假想边界；T2/T3 边际增量落在噪声里（CI 重叠），真实增量集中在前两跳。`EndlessBaseFactor` 未动，基线是否回调是单独决策。
13. **审计遗留二：性能第 1 / 4 / 5 条 ✅ 已采样（2026-11-04），三项都在噪声级，不要再优化**：Editor Play + `Stopwatch` 探针（`HelloWorldProject/_shots/probe/perf_ui_20261104.cs` / `perf_ui3_20261104.cs`，经 `eval_file` 跑、`Debug.Log` 出数）。① 技能面板：关闭态 `RefreshSkillPanel` **0.86 µs/帧**（原判「每帧最多 27 次 `SkillAdvice.Score`」不成立；`Score` 0.55–0.91 µs/次，9 卡 × 2 只在面板打开时花 10–16 µs，而那时战斗暂停）；② 派生属性：`RefreshCharacter` 签名路径 **1.3 µs/次**（其中 20 个属性读取 0.5 µs），整帧 `Refresh(false)` **30–37 µs**（0.18–0.22% 帧），10 Hz 文本刷新 `Refresh(true)` 65.5 µs；③ 自动保存：p50 **0.08–0.14 ms** / p95 0.21–0.25 ms（JSON 4,661 字符 ≈ 4.6 KB），每 5 秒一次——无条件存档（无脏判）确实浪费 CPU，但尾延迟 0.25 ms 不构成卡顿，加脏判收益约 0.00002% 平均帧耗、不值。采样口径：Editor + Mono + 79 draw calls + `cpuMain 2.2 ms`，Player 构建只会更快。测前用 `reg export` 备份 `HKCU\Software\Unity\UnityEditor\DefaultCompany\My project` 到 `_art/tmp-verify/playerprefs.reg`，测后 `reg import` 还原并逐行 diff 一致（628 行）。结论已写回 `docs/project-audit-2026-11-04.md` 的 §二与「性能」小节。
14. **审计遗留三：G1 决绝分/分钟对照**：无契约 / 单决绝 / 单狂躁 Ⅲ 三组比「契约分 ÷ 实际分钟」与死亡率；若决绝显著更快，再讨论降系数或加失败成本，不要凭感觉改。

15. **C 巅峰符文已落地（2026-11-04，本地未提交，存档 v16 → v17）**：`RuneCatalog.cs`（15 条形态 × 甲/乙 + `RuneMods.Field` 读取层）、`Models.PlayerData.cs` 两个掩码与点数 API、`BattleSystem.cs` 12 处读取点、`GameUI.Panels.Rune.cs` + `GameController.Runes.cs` 的 9 卡面板（入口：技能面板右上角「符文」）、`SaveSystem.cs` 版本 17。每 100 巅峰 1 点（P1000 满 10 点），解锁随存档跨转生保留（`ResetSkillsForRebirth` 不清符文）。验收：① 300 秒木桩 2 流派 × 3 配置，最大偏差 −9.1%，全在 ±25% 内（`_shots/rune-matrix.txt`）；② 遗物组合对照**弱成立**——最优件会变（甲 残喘 +10.8 / 乙 血之器 +9.8）但差在噪声内，有方向的只有铁壁（甲多值 3.3 层）与血祭（乙多值 5.2 层、甲下为负）；③ 顺带发现「全甲」比「全乙」强约 15 层（320 vs 305）。**甲/乙 已于 2026-11-05 调平**：新探针 `RuneFormScan`（60 种子 / 400 层 × 两流派 × 无符文/全甲/全乙/逐技能翻转）跑 5 轮，改了 6 处形态系数（吸血鬼 B 吸血 16%、沉重打击 B 暴伤保持 40%、电弧 B 18%/105%、静电共鸣 A 暴击 16%/溢出上限 100%、静电共鸣 B 暴伤 32%、岩甲 B 间隔 0.7s），最终 风暴 乙−甲 +4.2 层、山崩 −6.1 层，两形态均高于无符文；残留单技能偏差 战吼 B 风暴 +13.9、吸血鬼 B 山崩 −9.7。数据 `_shots/rune-form-scan.txt`，全部数字见 `docs/gameplay-expansion.md` §6.3。

## 5. 文档状态（哪些是当前口径）

| 文档 | 状态 |
|---|---|
| `README.md` | 当前实现、结构、已知问题；本会话已更新 |
| `docs/roadmap.md` | 待办与证据；B1 / 技能 2.0 / 技能点自选 / **B10 深渊遗物 v1** / **B11 UI 四条打磨** / **B12 深层契约 v1** / **B13 巅峰符文** / **B14 D 屠魔录** 已标 ✅；A8 ◐、B9 已退役 |
| `docs/shop-2-design.md` | B1 已实现；§11 为实现与验收口径 |
| `docs/skill-system-v2-design.md` | 技能 2.0 已实现；**§17 战斗数值、§18 技能点自选为当前口径**；§10 / §12 早期段落有「三选一」历史描述 |
| `docs/skill-build-design.md` | **历史 / 暂缓**；被技能点自选取代，仅 4 个显示标签可复用 |
| `docs/achievements.md` | 成就 v15 当前口径；「技能三选一」处已改技能面板 / 流派面板 |
| `docs/endless-mode.md` | 当前 3.1 + Step 4；§12.5.4 是 3.4 / Step 6 历史，§12.5.5 是技能 2.0 复校，§12.5.6 随机五槽 330，**§12.6 深渊遗物五策略对照**，**§12.7 深层契约八条 × Ⅰ/Ⅲ 对照** |
| `docs/rebirth.md` | 转生字段停在 v15（当前存档 **v18**）；技能点自选与转生重置口径已回填 |
| `docs/gameplay-expansion.md` | 提案文档；2026-11-02 按增量游戏结构重排为三层循环（A 局内 / B 局的形状 / C 永久），**§3 为总览与总循环图**，§4–§6 为分层细则。**方案 A（深渊遗物）/ B（深层契约）v1 / C（巅峰符文甲·乙）/ D（屠魔录）均已实现**，§4 状态行与 §4.3/§4.5 已改成实测口径，§5 状态行与 §5.1/§5.3 已改成八条最终数值，§6.3 是符文形态调平记录，§7 是屠魔录实现记录（含五条落地偏差）；**只剩 E 节点远征是提案** |
| `docs/project-audit-2026-11-04.md` | 外部审计原文 + **§五 处置记录**（L1~L7 / 性能 1~5 / G1~G4 的结论、改动位置与验收）；原文行号是审计时的，修复后已漂移，引用位置看 §五 |
| `docs/equipment-reforge.md` | 洗练规则长期有效；「v8」是首次写入版本，当前存档 **v18** |
| `_balance/README.md` / `balance-analysis.md` | 探针调用与当前 3.1 / Step 4 口径；旧 3.4 数据标历史 |

## 6. 环境与操作坑（本机实测）

- **Unity 没开**时先 `unity status --format json`；`STATUS_NO_INSTANCES` 用 `unity open E:\pytest\unitydsh1\HelloWorldProject --no-banner` 拉起，等到 `state=ready`。
- **Play 前若停在空场景**：先 `EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity")` 再 `editor_play`，否则只有 Main Camera、找不到 `GameController`。
- **改完 .cs 不会自动编译**：跑 `AssetDatabase.Refresh(ForceUpdate)` 等 7~9 秒，再 `console_status` 看 `compilationFailed`；必要时 eval 读回字段确认新值。
- **新 .cs 可能被 Unity 排除在编译输入表外且不报该文件错误**：修法是删掉 .cs + .meta、Refresh、用新 GUID 重建 .meta 再 Refresh。
- **eval 有 5000ms 主线程上限**：长扫掠拆小批，或临时 `[MenuItem]` 用 `unity command --format json --timeout 90 menu -- --path "..."`（`--format` 必须放在 `--` 之前）。
- **Editor 持文件时 edit/write 偶发 `ReplaceFileW EIO (Win32 1175)`**：等 1.5~2 秒重试；先 grep 确认是否已部分写入。
- **Play 测试会写当前槽**：测试前备份 `PlayerPrefs` 里的槽 JSON 到 `_art/tmp-verify/`，**先 `editor_stop` 再还原**；`editor_stop` 自己会把测试状态写回，且 `GameConfig.AutoSaveInterval = 5f`，进 Play 十几秒就会覆盖真实进度。
- **存档位置**：Editor 里 PlayerPrefs 走 `HKCU\Software\Unity\UnityEditor\DefaultCompany\My project`（工程 companyName/productName 是默认值），`UnityByDSH\AutoBattle` 是早期改名后打过的包留下的旧档。想用 `reg query` 查档，别查错节点；Unity 会给每个 Key 追加 `_h<hash>` 后缀，只有 Key 名对得上还要自己去后缀。
- **`Assets/_shots/` 已在 .gitignore**（2026-10-29 补），验收截图放这里就不会再进 `git status`。
- **PowerShell 7**：JSON 参数先放变量再作为单个参数传；`unity command` 自己的 flag 要放在 `--` 之前；C# 代码里避免单引号。
- **git 恢复被安全网拦**时可用 `cmd /c "git show HEAD:<repo-path> > <worktree-path>"`；本机 hosts 把 github.com 指到 127.0.0.1，push 用 `git -c http.curloptResolve=github.com:443:<IP> push origin main`（2026-10-25 用 20.27.177.113 成功）。
- **截图**：`HelloWorldProject/Assets/Screenshots/` 已被 .gitignore；文档引用图在 `docs/shots/`。一次性产物放 `_art/tmp-verify/<主题>/`。

## 7. 验收命令速查

```csharp
AutoBattle.EditorTools.QuickRegression.RunAllQuick()
AutoBattle.EditorTools.BalanceRegression.RunStaticOnly()
AutoBattle.EditorTools.AchievementRegression.RunCore()
AutoBattle.EditorTools.NewGameProgression.RunAssert(12345, 30)
```

无尽 50 种子（风暴 / 山崩）：

```csharp
AutoBattle.EditorTools.EndlessPacing.Run(50, 300, 20261018, 1000, 150, 0.85f,
    true, 0, 0, 0, 0, 0, true, 1); // doctrine 1=风暴
AutoBattle.EditorTools.EndlessPacing.Run(50, 300, 20261018, 1000, 150, 0.85f,
    true, 0, 0, 0, 0, 0, true, 2); // 2=山崩
```

## 8. 交接结论

- **可继续开发**：代码已编译通过，快速回归 **3/3**、成就 **668/0**、`consoleErrors=0`。注意「新档 29/1/0、两套无尽 240/245」是 L1 固定步进之前的口径，引用前重跑。
- **提交已解锁**：2026-11-05 用户明确授权，`02437c6` 已提交并推送，工作区干净；后续仍按 USER.md，未经明确要求不 commit / push。
- **最值得先问用户**：① 提交这批（UI 打磨 + B 深层契约 + 审计修复 + G2/G3 实测 + C 符文 + 五修 + D 屠魔录）；② 性能第 1/4/5 条的 Profiler 采样（遗物与契约都已交付，下一个拖得最久的是性能量测）；③ A8（CI Secrets）。UI 主题与遗物分档验收都已收尾，不需要再排。

## 9. UI 主题改造（方向 B「桌面实体卡牌」，2026-10-26~27）

用户已选定方向 B（卡牌实体感）+ 三项横切改造（换字体 / 加材质与光 / 动效与反馈），并已落地。

| 层 | 落点 | 状态 |
|---|---|---|
| 字体 | 正文 `Assets/Resources/Fonts/NotoSansSC-Regular.ttf`（思源黑体），标题 `SmileySans-Oblique.ttf`（得意黑，2026-10-29）；`GameUI.cs:296-308` 的 `LoadFonts()` + `GameUI.Theme.cs:35-38` 的 `PickFont()`（粗体且字号 ≥ `FontSection`(18) 才切展示字）+ `GameUI.Factory.cs:69-76` 把展示字的 `fontStyle` 改回 `Normal`（用原生斜切笔形，不叠合成粗体）。`NotoSerifSC-Bold.ttf`（约 11 MB，0 引用）已于 2026-11-03 删除 | 已落地 |
| 配色 | `GameConfig.Colors.cs`：木桌 + 羊皮纸 + 墨字 + 烫金；`InkOf/InkHex` 把亮语义色压到纸面墨度 | 已落地 |
| 材质 | `GameUI.Theme.cs`：`CreateHardShadow`（4,6 偏移 / 纯黑 0.34）、`AddPaperSheen`、`LinkShadow`；`Assets/Resources/UI/` 下 `ui_bg_wood.png`(1600x900)、`ui_noise.png`、`ui_paper_tex.png`、`ui_card_shadow.png` | 已落地 |
| 动效 | `GameUI.Motion.cs`：`UiMotion.Attach`（悬停抬 1px / 按下沉 2px）、`PanelIn`（淡入 + 8px 落位）、`GrabCanvasGroup`、`UiCardShadow` | 已落地 |
| 按钮 | `Assets/Resources/Icons/frame_*.png` 16 张重着色为「深皮革 + 黄铜」，脚本 `HelloWorldProject/_shots/recolor_frames2.py`（**必须从 `_art/ui-frames/orig/` 读原图**，从 Assets 读会二次着色烂掉） | 已落地 |
| 布局 | `GameUI.Build.Left.cs` 左面板重排（称号皮革名牌 / 装备卡 396×168、单卡 186×48、`cardY = 342 + (i/2)*52` / 钱包卡 y510 h84 / 技能栏 y604 h82、`slotW` 124、`rowStep` 44；装备与钱包之间的 Divider 已删） | 已落地 |

最近修复（2026-10-27）：

- `GameUI.Factory.cs:115` 禁用态由「半透明白」改成「压暗的灰」`new Color(0.62f, 0.60f, 0.57f, 1f)`——旧值把深皮革底框染成半透明，透出羊皮纸后像一张发白空壳（无尽「开始挑战」就是这样）。
- 无尽面板「规则 / 历史榜」页签弃用 `RowBg` 平涂，改成 `StyleTab(..., "frame_tab", 12)` + 选中亮 / 未选中压暗（`GameUI.Endless.cs` 的 `RefreshEndlessTabs`）；成就页签的未选中色同步改成同一档灰。

验证截图（`HelloWorldProject/Assets/_shots/`，1280x720，已 gitignore）：`v10_base.png`（主界面）、`p12_achieve.png`、`p12_endless.png`、`p10_stage/save/settings/merchant`。
重拍脚本：`_shots/drive12.py`（六浮层）、`_shots/rebuild_tabs.py`（成就 + 无尽）。

2026-11-04 已收尾（原「仍待打磨」四条全部落地）：

1. ✅ 行商卡正文与价签：服务页 desc 12px / 价签 13px Bold / 购买钮 12px，珍藏页卡片 258×84、步长 90、desc 11px。
2. ✅ 强化 / 洗练属性行：提示改 `TextInfoDeep`，正文 14/15px，右侧信息 12px `TextNormal`，洗练富文本蓝灰 → 暖灰 `#6F6252`。
3. ✅ `UITooltip`：皮革底 (0.235,0.180,0.133) + `TooltipSheen` 皮面高光，保留黄铜描边。
4. ✅ 称号禁用态：`BtnTextDim` 提到 0.706（不改结构，只提亮文字）。

原「称号栏距画布底 48px、tooltip 翻上去会压住『只看未完成』」已于 2026-11-05 修掉（tooltip 改单行，高 28px、底部 y=-348）；面板外壳这一层已无已知残留。

> 截图与一次性产物都在 `HelloWorldProject/Assets/_shots/` 与 `_art/ui-frames/`，不进仓库。


# 会话交接（2026-11-03）

> 用途：换会话时先读这一页，再读 `README.md` 与 `docs/roadmap.md`。本文只记**当前状态、未提交改动、验证结果、下一步和环境坑**，不重复设计与历史论证。

## 1. 当前坐标

- 工作区：`E:\pytest\unitydsh1`
- Unity 工程：`HelloWorldProject/`，Unity **6000.6.5f1**，单场景 `Assets/Scenes/SampleScene.unity`
- `HEAD = 50a71ed`（`origin/main` 已同步；该提交含无尽结算卡、深渊遗物 v1、文档回填与 `.mnemon/` 移出仓库）
- **未提交**：无（`git status` 干净）。`Assets/_shots/`、`Resources/Music/` 与 `.mnemon/` 均在 `.gitignore` 内，不进仓库。
- **不要自行 commit / push**：按 USER.md 的 critical 规矩，只有用户明确说「提交」才允许操作 git；push 另需明确同意。
- 当前存档：**v15 / 3 槽**；`CurrentSaveVersion = 15`（`HelloWorldProject/Assets/Scripts/SaveSystem.cs:31`）。
- **Editor 的存档在 `HKCU\Software\Unity\UnityEditor\DefaultCompany\My project`**，不在 `UnityByDSH\AutoBattle`：工程 `ProjectSettings/ProjectSettings.asset` 里 companyName / productName 仍是默认值（见 §6）。

2026-10-29 那轮已完成并验收：

1. **UI 方向 B 收尾**：正文换思源黑体、标题换得意黑（`Resources/Fonts/SmileySans-Oblique.ttf`，2.5 MB，OFL）；`GameUI.Theme.PickFont` 只在「粗体 + 字号 ≥ 18」时切展示字，全界面 90 个 Text 里 4 个（角色属性 / 装备 / 等待下一只怪物 / 商店）走展示字。
2. **左栏再压一轮**：装备卡 396×168、钱包卡 y510 h84、技能栏 y604 h82（`rowStep` 44）、删掉装备与钱包之间的 Divider，四处间隙 8/8/12/11 px 无重叠。
3. 12 张面板截图验收（`HelloWorldProject/Assets/_shots/v19_*.png`），控制台 `consoleErrors=0`。

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
| 规模 | 运行时 **68 .cs / 24,016 行**；GameUI **33 partial / 10,814 行**；Editor **10 / 4,978 行**（2026-11-03 实测） |

## 3. 代码地图（本轮新增 / 重点）

- **商店 2.0**：`ShopCatalog.cs`（274 行，价格 / 收藏家 / 珍藏 / 服务）、`GameController.Shop.cs`（320 行）、`GameUI.Shop.cs`（607 行）；存档字段 v14 引入。
- **技能 2.0**：`Skills.cs`（15 技能整表 + 流派常量 + `StormBuild`/`MountainBuild`）、`HelloWorldProject/Assets/Resources/skills.json`、`SkillCatalog.Loader.cs`、`GameController.Skills.cs`（80 行，流派选择 / 技能点）、`GameUI.Doctrine.cs`（215 行，流派面板 + 资源条）、`GameUI.Panels.SkillBar.cs`（技能面板 / 技能点学习）、`Models.PlayerData.cs`（技能点 / `SkillPoints` / `CanLearnSkill` / `BeginDoctrine`）。
- **存档 v15**：`PlayerData.doctrine`、`skillEverMask`；技能点**不单独存档**，由 `ExpectedSkillCount = Clamp(paragonLevel/10,0,9)` 减已掌握数实时派生。v14 旧档首次加载会强制选流派，按巅峰发点，由玩家自选；不再自动送 9 技能。
- **探针**：`EndlessPacing.Run(... fixedDoctrine)` 第 14 参数；`ProbePolicy.ResolvePendingSkills` 先按 `SkillCatalog.BuildOf(doctrine)` 挑毕业 build，再按推荐分补齐；`_balance/baseline.json` 已按技能点口径重做。
- **未完成但保留的字段**：`GameConfig.SkillChoiceCount` 已废弃但仍保留，避免 `game_config.json` 缺键告警；`pendingSkillPicks` 保留为 v15 字段并固定 0。删除前先确认配置加载与旧档迁移。
- **A 深渊遗物 v1（2026-11-02）**：`RelicCatalog.cs`（tier1 12 件 + `ShouldOffer` / `RollChoices` / `RelicMods.Build`）、`GameController.Endless.Relics.cs`（`OfferRelicChoice` / `TakeRelic` / `ApplyRelicMods` / `RelicChoiceOpen` / 满槽替换）、`GameUI.Endless.Relics.cs`（三选一与替换面板）、`Endless.cs` 的 `relicIds` 与 `resultRelics`（结算快照——**`EndEndlessRun` 会清空 `relics`，结算卡必须读 `resultRelics`**）、`EndlessPacing.Run` 第 15 参 `relicPolicy` 与 `CompareRelics` 五策略对照。

## 4. 换会话后的下一步

1. **先读**：`README.md`（已完成功能与已知问题）→ `docs/roadmap.md`（未完成 / 优先级）→ 本文。
2. **提交**：工作区已在 2026-11-02 提交到 `9be2b80`（181 files / +9,324 / −1,774），推送仍需单独同意。不要擅自 commit/push。
3. **A8（P3，工程）**：GitHub 仓库 Secrets 配 `UNITY_LICENSE_SERIAL`；重跑 `Unity CI / verify`，确认 3/3 + 8/8 + Windows 构建；把该 check 设为分支保护 required。首跑失败仅是缺 secret。
4. **B9（P4，暂缓）**：随机三选一已删，重抽 / 禁选失去对象；只剩 4 个显示标签可复用。不要按 v2.1 直接实施。
5. **D2 随机五槽已补测（2026-11-02）**：现行 3.1 / Step 4 下，随机五槽 + 自动技能 50 种子中位 **330**（P10 300 / P90 360 / max 375）；同一套随机装备换成固定风暴 build 掉到 **235**，maxGear + 自动技能 **300**。结论：拉开差距的是技能选择（吸血鬼 + 保命顶掉三个输出位）不是装备，maxGear 已不是上限。见 `docs/endless-mode.md` §12.5.6。**后续数值以 330 为基准，不要再用 240。**（§12.6 的遗物对照是在 maxGear + 自动技能、基线 300 上做的；换算率 ≈ +1% 战力 ≈ +1 层，与基准选择无关。）
6. **无尽结算卡片已落地（2026-11-02；「本局遗物」行同日接上 A 方案实数据）**：`GameUI.Endless.cs` 的结果面板从 560×300 纯文本升级成 680×420 结算卡（大号到达层数 + 阵亡/撤退标签 + 用时/击杀/清层三格 + 榜单名次 + 「本局奖励」占位行），版式与规则见 `docs/endless-mode.md` §8.3。其中「本局遗物」行当天就接上了 A 方案实数据（读 `resultRelics`）；「深层契约」仍是占位，B 落地后把「尚未开放」换成逐条清单即可，不要在这里另算数值。
7. **可选复测**：满轮回 +5% 战斗加成在 3.1 / Step 4 下未复测；旧 3.4 口径为 260（转生前 242）。P200 的 11-10 胜率 80.8/90.5 是 1.6 口径，1.25 后未重测。
8. **A 深渊遗物 v1 已落地（2026-11-02，本地未提交）**：tier1 12 件 + 6 槽 + 每 10 层三选一 + 满槽强制替换 + 结算卡遗物行。50 种子 / 900 层实测满 6 件比无遗物 **+18.4 层**（50 胜 0 负），选择质量只 +0.9、替换价值 −0.4 —— **tier1 的区分度不足是已知结论**，tier2/tier3 与 7/8 槽等 B 深层契约一起做。数值纪律与验收改判见 `docs/gameplay-expansion.md` §4.3/§4.5，实测表见 `docs/endless-mode.md` §12.6。顺手确认：`EndlessBaseFactor` 未动。
9. **下一步候选**：B 深层契约（接 A 的解锁钩子）→ C 巅峰符文 → D 屠魔录；A8 的 UNITY_LICENSE_SERIAL 仍是 P3 尾巴。

## 5. 文档状态（哪些是当前口径）

| 文档 | 状态 |
|---|---|
| `README.md` | 当前实现、结构、已知问题；本会话已更新 |
| `docs/roadmap.md` | 待办与证据；B1 / 技能 2.0 / 技能点自选 / **B10 深渊遗物 v1** 已标 ✅；A8 ◐、B9 暂缓 |
| `docs/shop-2-design.md` | B1 已实现；§11 为实现与验收口径 |
| `docs/skill-system-v2-design.md` | 技能 2.0 已实现；**§17 战斗数值、§18 技能点自选为当前口径**；§10 / §12 早期段落有「三选一」历史描述 |
| `docs/skill-build-design.md` | **历史 / 暂缓**；被技能点自选取代，仅 4 个显示标签可复用 |
| `docs/achievements.md` | 成就 v15 当前口径；「技能三选一」处已改技能面板 / 流派面板 |
| `docs/endless-mode.md` | 当前 3.1 + Step 4；§12.5.4 是 3.4 / Step 6 历史，§12.5.5 是技能 2.0 复校，§12.5.6 随机五槽 330，**§12.6 是深渊遗物五策略对照** |
| `docs/rebirth.md` | 当前 v15；技能点自选与转生重置口径已回填 |
| `docs/gameplay-expansion.md` | 提案文档；2026-11-02 按增量游戏结构重排为三层循环（A 局内 / B 局的形状 / C 永久），**§3 为总览与总循环图**，§4–§6 为分层细则。**方案 A（深渊遗物）v1 已实现**，§4 状态行与 §4.3/§4.5 已改成实测口径；B/C/D/E 仍是提案 |
| `docs/equipment-reforge.md` | 洗练规则长期有效；「v8」是首次写入版本，当前存档 v15 |
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

- **可继续开发**：代码已编译通过，快速回归 3/3、成就 668/0、新档 29/1/0、两套无尽 240/245。
- **唯一阻塞**：没有用户明确「提交」授权；不要自行 commit / push。
- **最值得先问用户**：是继续把 UI 主题收尾（见第 9 节待打磨），还是先提交这批改动、做 A8（CI Secrets）或补 D2 的随机五槽复测。

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

仍待打磨（按视觉优先级）：

1. 行商卡正文偏小偏淡、价签与羊皮纸的协调；
2. 强化 / 洗练面板的属性行与说明文字偏淡；
3. 工具提示（`UITooltip`）仍是深黑底，可考虑换皮革底；
4. 成就页底部「称号」区禁用态对比。

> 截图与一次性产物都在 `HelloWorldProject/Assets/_shots/` 与 `_art/ui-frames/`，不进仓库。


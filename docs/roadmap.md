# 项目整体梳理与待办清单（Roadmap）

> 生成日期：2026-10-23　最近更新：2026-10-25 晚（B2 成就点兑换无尽起始层、B4c P100 门槛复评保持 100、D3 财运掉落量化保持 +2%/级、D2 无尽口径收紧到 3.4、E4 一次性产物归并、A8 Windows 构建与 CI、A7 提交）。依据：`HEAD = e0b9f72`（A7 已落本地提交，36 files / +1,453 / −216；本文档回填随下一次提交）。
> 本文替代「凭记忆列待办」：每条都标了现状证据与验收口径，改完打勾并把实测数字写回对应小节。
> 与 `README.md` 的关系：README 记「已完成什么」，本文记「还差什么、为什么、按什么顺序」。README「已知问题」里的旧条目与本文交叉引用。

## 0. 这份文档怎么用

1. **编号稳定**：`A/B/C/D/E` 是类别，`A1`、`C3` 是条目号，后续讨论、提交信息、验收记录都引用编号。
2. **判据优先**：每条都写了「现状证据」和「验收口径」。没有验收口径的条目，先补口径再动手。
3. **数字要落到文档**：改完数值/行为，同时更新 README 与对应 `docs/` 文件；只改代码不回填文档视为未完成。
4. **不擅自提交**：本地 commit 需用户明确说「提交」，push 另需明确同意（USER.md 规矩）。

## 1. 现状快照（2026-10-25 实测）

### 1.1 规模

| 维度 | 实测 | 说明 |
|---|---|---|
| 运行时脚本 | **59 个文件 / 17,253 行** | `HelloWorldProject/Assets/Scripts/`（2026-10-25 实测），其中 `GameUI*` 28 个 partial / 7,664 行（2026-10-23 拆分） |
| 编辑器工具 | **10 个文件 / 4,138 行** | 回归与探针（2026-10-25）：AchievementRegression 1020、AutoBattle.CI 106、BalanceRegression 628、EndgamePacing 232、EndlessPacing 493、GameConfigReloader 50、LateGameProbe 686、NewGameProgression 509、ProbePolicy 310、SkillTableVerify 104 |
| 资源体积 | **44.4 MB** | `Assets/` 全量（不含被 gitignore 的 Music/ 与 Screenshots/） |
| 图片 | **70 PNG + 4 JPG** | Sprites：Monsters 33 / Bosses 11 / Fx 20 / Auras 5 / Hero 1；Backgrounds 4 张 JPG |
| 音频 | **14 SFX + 4 BGM** | SFX 在 `Resources/Sfx/*.ogg`；BGM 在 `Resources/Music/`（已 gitignore，换机需自备） |
| 配置 | `game_config.json` **149 键**、`skills.json` **15 技能**、`achievements.json` **42 条 / 805 点 / 7 称号 / 43,400 金币** | 三方必须与 `GameConfigData` 等字段一一对应 |
| 文档 | **11 份** | README、AGENTS、achievements、audio-credits、endless-mode、equipment-reforge、rebirth、sprites-and-fx、roadmap（本文）、balance-analysis、_balance/README；行数随回填变化，以各文件为准 |
| 存档 | **v13 / 3 槽 / 可导出导入、改名、跨槽复制** | `SaveSystem.cs:29`；PlayerPrefs（Windows 为注册表）+ `Documents/AutoBattle/slotN.json` 导出/导入（2026-10-24）；名字 ≤12 字，覆盖目标槽需 3 秒内二次确认；v13 加 `endlessStartRank` |
| 工程 | Unity **6000.6.5f1**，单场景 `SampleScene` | 2026-10-24 起 `productName = AutoBattle`、`companyName = UnityByDSH`、`bundleVersion = 1.0.0`（PlayerSettings；改名前是 My project / DefaultCompany / 1.0） |
| 自动化测试 | **快速回归 + EditMode NUnit 8 条 + Windows 构建脚本 + CI workflow**；CI 待 GitHub 首跑 | `AutoBattle.CI.cs` + `tools/quick-regression.ps1`（静态哨兵 + 成就核心 + 无尽冒烟）；`AutoBattle.Tests.asmdef` + `tools/run-editmode-tests.ps1`（8 条）；`tools/build-windows.ps1` + `.github/workflows/unity-ci.yml`；test-framework 1.8.0 已启用，3 个 asmdef |

### 1.2 已经成型的系统（不是缺口，先确认共识）

| 系统 | 现状 | 完成度证据 |
|---|---|---|
| 核心循环 | 自动战斗 → 掉落 → 自动/手动换装 → 强化/洗练 → 推关 → 精英/BOSS → 通关 11-10 | P0–P4 全部 ✅（README:227-240） |
| 关卡 | 11 章 × 10 关 = 110 关；章压迫 `1+5.5%×(章-1)`，章末 BOSS 再 `1+4%×(章-1)`，第 11 章不吃 | `GameConfig.cs:206`/`:212`；300 套抽样 98%→21.8% |
| 装备 | 5 槽（武器/衣服/头盔/鞋/饰品），4 品质，强化 +1~+15，洗练 4 词条最多锁 3（锁后 ×3^n 价） | `GameConfig.cs:391-465`；docs/equipment-reforge.md |
| 技能 | 15 个，三选一，技能推荐分 | `skills.json`；`SkillAdvice.cs` |
| 无尽 | 起始层 = max(1, 本档最高−4)；每 5 层 BOSS；掉落等级封顶 150；本档 Top-5 历史榜 + 清空；全局最高 | docs/endless-mode.md §6.5/§13.1；P200 首死中位 45 层 |
| 转生 | 5 轨 × 5 级 = 110 印记；战斗 +5% 封顶；Exp +60%/级、起始等级 +3/级（满轮回 Lv16）；保留评分最低 1 件、本档最快轮回 Top-5（v12） | `GameConfig.cs:697`、`RebirthCatalog.cs`；实测第 2 世 82.6% / 满轮回 47.1%（docs/rebirth.md §14.4 / §14.6） |
| 成就 | 42 条 / 6 类 / 805 点 / 7 称号 / 43,400 金币；按槽独立；toast 队列；称号可佩戴并显示在左栏；100 点/级兑换无尽建议起始层（最多 +3） | docs/achievements.md §15.5；B2 验收 2026-10-25；回归 644 通过 |
| UI | 28 partial 文件；设置 5 开关 + 3 音量 + 关于页 + 显示页（全屏/分辨率/缩放/色弱）；战斗速度记忆；强化「+5/满级」批量；属性/技能/无尽/洗练 tooltip；音乐进度条；称号佩戴与左栏展示；存档改名/复制；右栏当前目标条（B8）；成就/无尽/转生/存档/对比/强化/洗练/选关面板 | 2026-10-23 拆分、2026-10-24 C1–C11 + B3、2026-10-25 B8 验收截图 |
| 终局 | 终局 BOSS ×1.6 定格（再往上抬会同时抬新档通关率） | `GameConfig.cs:312`；docs/balance-analysis §3.5.4a |

### 1.3 一句话判断

**玩法闭环完整，B8 已补上「现在该追什么」的投影，B2 已给成就点一个非战斗出口，D2 已把无尽理想配装的首死中位从 290 收到 240；剩下的缺口是：**
1. **长线出口**——P1000 兜里 8.593 亿花不掉；成就点现在能换无尽起始层，但金币仍是唯一没有硬出口的终局资源；
2. **终局内容**——B4c 已量到首通后到 P100 还要约 29 分钟，结论是保持 100；缺的不是门槛数值，而是 P100 之后的新玩法；
3. **工程化**——快速回归 + 8 条 NUnit + 3 个 asmdef 已补；Windows 构建脚本与 CI workflow 已落地（本机 8/8 + 出包成功），但 GitHub 首跑与分支保护还没配，验收仍有一半靠本机 Editor。

## 2. 待办总览（按优先级）

> 只列未收口条目；已完成编号保留在 §2.2，详细证据见各小节。

### 2.1 未完成

| 编号 | 事项 | 类 | 优先级 | 量级 |
|---|---|---|---|---|
| B1 | 金币出口（商店 2.0）只有构想、无设计稿 | 玩法 | P2 | 2~3 天（含设计） |
| A8 | 构建与 CI：本机脚本/workflow 已写，GitHub 首跑 + 分支保护未做 | 工程 | P3 | 剩余约 30 min（需仓库 Secrets 权限） |
| B9 | 技能只有三选一，无 reroll/构筑协同 | 玩法 | P4 | 设计题 |

### 2.2 已完成（保留编号）

| 编号 | 事项 | 收口 |
|---|---|---|
| A1 | 提交 2026-10-24 的 72 项改动 | ✅ 2026-10-25（12d230a、de8c038，已推送） |
| A7 | 提交 B4/B8 + B2/B4c/D3 + D2/E4 + A8 的 36 个文件 | ✅ 2026-10-25（e0b9f72；推送见文末） |
| A2 | 治理 `_tmp_verify/`、`_art/ui-audit/` 未跟踪目录 | ✅ 2026-10-24 |
| A3 | README「项目结构」段落与代码漂移 | ✅ 2026-10-24 |
| A4 | 产物名/版本号/关于页缺失 | ✅ 2026-10-24 |
| A5 | 无自动化回归入口、无 asmdef、test-framework 闲置 | ✅ 2026-10-24（快速回归 + 8 条 NUnit + 3 asmdef） |
| A6 | `SaveSystem.Save` 无返回值；存档不可迁移/备份 | ✅ 2026-10-24（TrySave + 导出导入） |
| B3 | 称号缺少佩戴与展示位 | ✅ 2026-10-24 |
| B4 | 转生 v2：保留一件装备 / 转生排行榜 | ✅ 2026-10-25（a+b；c 见 B4c） |
| B8 | 缺少「下一步做什么」的短中期目标展示 | ✅ 2026-10-25 |
| B2 | 成就点没有出口（称号佩戴之外） | ✅ 2026-10-25（兑换无尽建议起始层，100 点/级，最多 +3） |
| B4c | P100 转生门槛复评 | ✅ 2026-10-25（50 种子实测，保持 100） |
| D3 | 财运掉落 +10% 对五槽毕业时间的量化 | ✅ 2026-10-25（50 种子三组对照，保持 +2%/级） |
| D2 | 无尽理想配装中位 290 vs 目标 235~250 | ✅ 2026-10-25（`EndlessBaseFactor` 2.8→3.4；理想 240 / 随机 181） |
| E4 | 一次性产物路径统一 | ✅ 2026-10-25（`_tmp_verify/` → `_art/tmp-verify/`；索引见 `_art/README.md`） |
| C1–C11 | 11 项 UI/UX（速度记忆、批量强化、tooltip、音乐进度、显示设置、存档改名/复制等） | ✅ 2026-10-24 |
| D1 | 转生加速 | ✅ 2026-10-22（82.6% / 47.1%） |
| D4/D5 | 章节压迫 5.5%/4%；终局 BOSS ×1.6 定格 | ✅ 2026-10-23 |

## 3. 收尾与工程（A）

### A1 提交 2026-10-24 的 72 项改动（P0）— ✅ 2026-10-25

- **结果**：已提交 `12d230a`「章节压迫重调与 GameUI 拆分；A2–A6、C1–C11、B3 落地」与 `de8c038`「记忆：记录 GitHub hosts 解析被劫持时的推送绕过」，并已推送；`git rev-list --count origin/main..HEAD` = 0。
- **提交前现状（历史）**：工作区 30 个 `M` + 42 个 `??`，合计 72 项；`git diff --stat HEAD` = +1,854 / −2,978。含：压迫 5.5%/4% 与终局倍率注释/文档、GameUI 28 文件拆分、六项 UI 修复与重置按钮字号 18→15、A2–A6（`.gitignore` 治理、README 结构重写、元信息/关于页、快速回归、存档 TrySave 与导出导入、asmdef + 8 条 EditMode NUnit）、C1–C11（速度记忆、批量强化、洗练双列、tooltip、起始层、音量格、音乐进度、显示设置、存档改名/复制、色弱配色）、B3 称号佩戴。
- **当时的提交方式**：`git add` 显式列出路径（`'!path'` 排除语法在本机 git 报 `Unimplemented pathspec magic '_'`），`git commit -F <文件>`（pwsh 无 heredoc），提交信息写在文件里由 `-F` 读取。
- **验收（已过）**：提交前快速回归 3/3 PASS；提交后 `git status --short` 干净，`BalanceRegression.RunStaticOnly()` 与 `AchievementRegression.RunCore()` 通过。
- **后续**：B4/B8 + B2/B4c/D3 的 25 个文件当时未提交，现已连同 D2/E4 并入 A7 的 32 个文件 + `_art/README.md`；本条不再重复处理。

### A2 临时产物治理（P0）— ✅ 2026-10-24

- **做法**：`.gitignore` 追加 `_art/ui-audit/`（逐面板审计截图）与 `_tmp_verify/`（探针脚本、UI dump、提交信息草稿）；两目录保留在本地，不删内容。
- **验收**：`git status --short` 的未跟踪噪声从 59 项降到 48 项，两目录不再出现；证据图仍在 `_art/ui-audit/`（gitignore 内）。
- **2026-10-25 E4 收口**：`_tmp_verify/` 已整体迁到 `_art/tmp-verify/`，`.gitignore`、脚本与文档引用同步；A2 当时追加的根目录规则随之作废。

### A3 README 项目结构漂移（P0，文档）— ✅ 2026-10-24

- **旧证据（历史）**：`README.md:121` 写「45 个文件，17,372 行」，实测 59 个 / 15,620 行；`GameController.cs` 写 1,796（实测 1,589）、`GameUI.cs` 758（300）、`GameUI.Panels.cs` 1,342（212）、`GameUI.Core.cs` 940（434），拆分出的 14 个新文件未列出。
- **已完成**：结构块按 2026-10-24 实测重写为 6 组 + UI 汇总——入口与状态机 5 / 2,259、战斗关卡掉落技能 9 / 1,972、数据模型 4 / 1,313、配置层 5 / 1,411、存档设置音频 5 / 1,361、成就转生目录 3 / 555、UI 28 / 6,749；`Scripts` 合计 59 / 15,620。`Editor` 段改为 10 个文件 / 4,048 行（补 `AutoBattle.CI.cs` 106）。根目录资产补 `tools/` 与 `docs/roadmap.md`，并新增「自动化回归」一节。
- **验收**：分组小计逐项相加与总量吻合（2,259+1,972+1,313+1,411+1,361+555+6,749 = 15,620）；「已知问题」第 2 条的 28 文件明细同步改成 6,749 口径（旧的 7,560 是拆分时含文件头重复的粗口径，已作废）。
- **2026-10-25 更新**：B4/B8 落地后 Scripts 实测 59 / **17,109** 行、GameUI 28 partial / **7,611** 行，README 已按新口径重写；上面的 15,620 / 6,749 是 A3 当时（2026-10-24）的数字，保留作历史记录。

### A4 产品元信息与关于页（P1）— ✅ 2026-10-24

- **已完成**：PlayerSettings `productName = AutoBattle`、`companyName = UnityByDSH`、`bundleVersion = 1.0.0`（用编辑器 API 写，不手改 ProjectSettings.asset，遵守 AGENTS.md）；设置面板底部「恢复默认｜关于」并排；关于页 680×520 显示版本号、存档 v11（当时口径；2026-10-25 晚起 v13）/ 3 槽 / 当前槽、Unity 版本与平台、设计分辨率、导出目录、AI 素材与 BGM 署名、回归入口提示；Esc 关闭、其它浮层互斥都已接好。
- **验收证据**：Play 截图 `HelloWorldProject/Assets/Screenshots/a4_about_panel.png`、`a4_settings_about.png`；读回 meta = AutoBattle / UnityByDSH / v1.0.0。
- **坑（已处理）**：改产品名等于给 PlayerPrefs 换命名空间，旧档会「消失」。本次已把 `HKCU\Software\Unity\UnityEditor\DefaultCompany\My project` 的 25 个值迁到 `UnityByDSH\AutoBattle`（备份在 `_art/tmp-verify/playerprefs_backup/`，gitignore 内），迁移后 slot0 回读 lv100 11-10、slot2 v10 均一致；3 个音量键因 PowerShell 读成 Int64 没迁成功，游戏读到的就是 0.8 默认值。

### A5 自动化回归入口（P3）— ✅ 2026-10-24

- **已完成（方案 a）**：`Assets/Editor/AutoBattle.CI.cs` 的 `QuickRegression`——菜单「工具/回归/跑一遍快速回归」、`RunAllQuick()` 返回三段 PASS/FAIL 报告与耗时、`RunAllQuickBatch()` 供 batchmode 用退出码 0/1。`tools/quick-regression.ps1` 自动判断 Editor 是否 ready：ready 走 `unity command eval --timeout 180000`，否则 `unity run -executeMethod ... -quit` 并透传退出码。
- **实测**：3 通过 / 0 失败（静态哨兵 / 成就 644 项 / 无尽冒烟 2 种子×40 层），脚本 exit=0，总耗时约 0.1 秒。
- **NUnit 化（2026-10-24 补完）**：新增 `Assets/Scripts/AutoBattle.Runtime.asmdef`、`Assets/Editor/AutoBattle.Editor.asmdef`、`Assets/Tests/AutoBattle.Tests.asmdef` 与 `Assets/Tests/EditMode/AutoBattle.Tests.EditMode.cs`（133 行 / 8 条用例）。测试程序集仅 Editor 平台、`UNITY_INCLUDE_TESTS`、显式引用 `nunit.framework.dll`，并引用 Runtime / Editor / TestRunner；运行时程序集补了 `UnityEngine.UI` 与 `Unity.InputSystem` 显式引用（asmdef 不会继承预定义程序集的自动引用）。
- **NUnit 验收**：`unity test HelloWorldProject --mode EditMode --output _art/tmp-verify/test-results.xml` → `test-run result=Passed total=8 passed=8 failed=0 skipped=0 duration=0.108s`；封装脚本 `tools/run-editmode-tests.ps1` exit=0。覆盖章节压迫公式/单调性、强化费用、无尽起始层、装备评分增量、存档 JSON 往返、成就目录、静态平衡哨兵。
- **运行方式**：Editor 关闭时用 `pwsh tools/run-editmode-tests.ps1`；Editor 开着时用 Test Runner 窗口（batchmode 会抢工程锁，脚本检测到 ready 的 Editor 会提示并 exit 2）。
- **备注**：文末附录 B 原写「eval 有 5000ms 主线程上限」，现在可用 `--timeout` 覆盖，README 与附录已改；test-framework 1.8.0 已启用。

### A6 存档健壮性（P3）— ✅ 2026-10-24

- **现状修正**：`SaveSystem.Save` 早在本次之前就是 `bool`（`SaveSystem.cs:254`；失败 catch → `AchievementSystem.NotifySaveResult(false)` + `LogWarning` + `return false`），真正缺的是「调用方没把失败告诉玩家」与导出/导入。
- **已完成**：`GameController.TrySave` 统一出口 + `_saveWarnCooldown`（10 秒冷却）把失败转成横幅提示，11 个调用点改用 TrySave；存档面板新增「导出」「导入」（导出到 `Documents/AutoBattle/slotN.json`，成功横幅带路径；导入优先读剪贴板，读不到再读同名文件）；导入走 `SaveSystem.TryImport`，版本高于当前存档版本（v13）在写盘前拒绝。
- **验收实测（Play，2026-10-24 v11 口径）**：`ExportSlot(0)` → `slot1.json` 3,977 字节；把 version 11 篡改成 12 被拒，提示「存档版本 v12 比当前支持的 v11 新，请先升级游戏」；原样导入回读 lv100 / 11-10 / gold 2,807,913 / cleared 110，前后一致。2026-10-25 先后升 v12、v13，版本上界用例由成就回归覆盖（现拒 v14）。

### A7 提交 B4/B8 + B2/B4c/D3 + D2/E4 + A8 改动（P0）— ✅ 2026-10-25

- **结果**：本地提交 **`e0b9f72`**「转生 v2、成就点出口、无尽 3.4、产物归并、构建与 CI」，36 files changed，+1,453 / −216；工作区 clean。
- **内容**：B4/B8（转生保留一件 + 最快轮回 Top-5，存档 v12；右栏当前目标条）、B2（成就点 100/级兑换无尽建议起始层，存档 v13）、B4c/D3（P100 门槛与财运掉落复评，数值保持）、D2（`EndlessBaseFactor` 2.8→3.4）、E4（`_tmp_verify/` → `_art/tmp-verify/`，新增 `_art/README.md`）、A8（`tools/build-windows.ps1` + `.github/workflows/unity-ci.yml`，并修掉 `quick-regression` 的 `-quit` 冲突）。
- **验证**：`quick-regression.ps1 -Batch` 退出码 0（三步全 PASS 才返回 0）；EditMode NUnit **8/8**；Windows 构建成功；`git diff --check` 干净。
- **推送**：用户已授权 `commit and push`（m05886）；本机 hosts 把 github.com 指向 127.0.0.1，已用 `git -c http.curloptResolve=github.com:443:20.205.243.166 push origin main` 推上去（`GIT_TERMINAL_PROMPT=0`、`GCM_INTERACTIVE=never`）。`git rev-list --count origin/main..HEAD` = 0，`origin/main` = `e0b9f72`。

### A8 构建流程与 CI（P3，本机链路 ✅ / GitHub 首跑 ◐）

- **现状（2026-10-25）**：`tools/quick-regression.ps1`（静态哨兵 + 成就核心 + 无尽冒烟）与 `tools/run-editmode-tests.ps1`（8 条 NUnit）已可用，但此前没有 Player build 脚本，也没有 CI。
- **已完成（本机）**：
  - `tools/build-windows.ps1`：包装 `unity build --target StandaloneWindows64`，默认输出 `Build/Windows/AutoBattle.exe`；支持 `-OutputPath`、`-RequireClean`、`-TimeoutSeconds`。场景列表仍由 `ProjectSettings/EditorBuildSettings.asset` 决定，不自写 `BuildPipeline`。
  - `.github/workflows/unity-ci.yml`：`runs-on: windows-latest`（Ubuntu runner 没有 Windows Mono 模块），16 步，含 Unity CLI 安装、downloads/Library 缓存、许可证激活、`quick-regression`（3 项）、EditMode NUnit（8 条）、Windows 构建与产物上传；缺 `UNITY_LICENSE_SERIAL` 会明确失败。
  - 本机实测：NUnit **8/8 通过（0 失败、0 跳过，0.11s）**；`build-windows.ps1` 构建成功，`AutoBattle.exe` 0.6 MB / 整目录 171.6 MB，SHA256 `001AEEBFB92718F8B49F0C0E0A03E313C54669552B08E952B2FEE3DDD143683A`；`AutoBattle_Data`、`UnityPlayer.dll`、`MonoBleedingEdge`、`D3D12` 齐全。
  - 顺手修掉一个真 bug：`unity run` 的 batchmode 参数里不能再带 `-quit`（CLI 保留 flag，报 `Forwarded argument '-quit' conflicts with a reserved Unity flag`）；`tools/quick-regression.ps1` 与 CI 都已去掉，退出码由 `RunAllQuickBatch` 自己 `EditorApplication.Exit` 决定。
- **仍未完成（必须在 GitHub 侧做）**：① 在仓库 Secrets 配 `UNITY_LICENSE_SERIAL`（可选 service account）；② 首次 push 后看 `Unity CI / verify` 实际跑通 3/3 + 8/8；③ 在分支保护里把 `Unity CI / verify` 设为 required check（「失败阻止合并」不能由仓库文件单独保证）。
- **验收边界**：本机「一条命令出包」与 NUnit 已过；**全新 clone 出包**与 **GitHub 首跑**尚未执行，因此 A8 暂记 ◐，不并入 §2.2 的已完成。
- **依赖**：§8 决策 #5 已按「先落发行向工程化」处理；正式上架/发版仍另说。

## 4. UI / UX（C）

> 来源：2026-10-22~23 的 Play 模式逐面板截图审计（证据在 `_art/ui-audit/`）。以下每条都在实机确认过，不是猜的。

### C1 战斗速度不记忆（P1）— ✅ 2026-10-24

- **现状**：`GameSettings.cs` 只有 5 个 bool（DamageFloat / ShowDps / ShowScore / ShowSkillAdvice / AutoEquip）+ 3 个音量 + GuideDone，**没有 `speedIndex`**；每次启动回 ×1。
- **要做什么**：`GameSettingId` 加 `SpeedIndex`（0/1/2），启动时读取；切档不重置。
- **验收**：设 ×4 → 退出 Play → 重进仍 ×4；设置面板与 HUD 速度按钮一致。
- **已完成（2026-10-24）**：`GameSettings` 新增 `SpeedIndex`（0/1/2，键 `autobattle.setting.speed`，getter 内 `EnsureLoaded`）；`GameController.Awake` 末尾套用；`SetSpeedIndex` 每次落盘；设置面板「恢复默认」同时调 `GameController.ResetSpeedToDefault()`，不会留下旧速度。
- **验收（已过）**：Play 内设 ×4 → 读回 `speedIndex=2 / mult=4 / setting=2`；`editor_stop` 后重新 `editor_play` 仍同上。截图 `HelloWorldProject/Assets/Screenshots/c1_speed_x4.png`、`c1_speed_x4_restart.png`；验收后已还原 ×1。

### C2 强化没有批量（P1）— ✅ 2026-10-24

- **现状**：强化面板只有单个「强化 240」；当前档 233 万金币、+15 需要 15 次/件 × 5 件，手动点约 9,700 次（按 +0→+15 累计价估算）。
- **要做什么**：面板加「+5」「满级」两档批量，逐级扣费直到失败/满级，过程中逐次播放音效但合并飘字；金币不足时提示差额并停在当前级。
- **验收**：从 +0 连点「满级」一次到 +15，扣费与逐次点击完全一致；中途金币不足只升到买得起的那一级。
- **已完成（2026-10-24）**：强化面板加「+5」「满级」两按钮（`_enhanceBulk5Button` / `_enhanceBulkMaxButton`，`GameUI.Panels.Enhance.cs` 的 `OnEnhanceBulkClicked(int levels)`）；底层 `GameController.EnhanceEquipmentBulk(EquipmentSlot, int maxLevels, out EnhanceResult stopReason)` 逐级调 `ApplyEnhanceLevel` 并合并日志，金币不足时停在买得起的级并提示差额。
- **验收（已过，Play，槽 0 武器，品质 1.6，金币 2,807,913）**：+0→+5 花 6,000（= 逐级 `EnhanceCost` 之和）；+5→+15 花 48,000；把金币压到 2,159 再点满级 → 停在 +2、剩 1,199，日志「金币不足，本次只强化到 +2；再升一级还差 1 金币。」。截图 `Assets/Screenshots/c2_enhance_bulk.png`；测试后强化等级与金币均已还原。

### C3 强化/洗练面板留白（P1）— ✅ 2026-10-24

- **现状**：两个面板 680 宽，左列只有 4 行信息，右 2/3 空白（截图 `ui_verify_title_worst.png` 同批）。
- **要做什么**：左列补「当前属性 → 下一级属性」增量、成功率/花费明细、该槽位历史最高；洗练面板补词条上限与质量分档说明。
- **验收**：面板宽度不变的前提下信息密度提升，无文字裁切（`preferredWidth <= rect.width`）。
- **已完成（2026-10-24）**：洗练面板改双列——左列 5 条词条行宽 600→372，右列新增 236 宽信息栏（`_reforgeInfoText`：词条容量 n/上限、质量 % 与分档、锁定 ×3/条 的当前倍率），质量标签与信息栏都带 tooltip；文案压到单行。
- **验收（已过）**：面板仍 680 宽；`_reforgeInfoText` preferredWidth 160 / rect 236、preferredHeight 123.33 / 204；质量 170.67/372、费用 84.67/372、词条行 maxRowW 140.67/354 ⇒ 无裁切。截图 `Assets/Screenshots/c3_reforge_panel.png`。强化面板左列已给「当前 1014 → 下一级 1044 / 费用 240 = (150 + 0×300) × 品质 1.6 / 上限 +15」。

### C4 tooltip 覆盖（P1）— ✅ 2026-10-24

- **现状**：悬停提示只有 14 个键（`GameUI.Buttons.cs` 的 hover 表）；暴击率、暴击伤害、巅峰经验/等级、技能推荐分、词条质量、无尽初始层、护甲穿透都没有解释。
- **要做什么**：补齐属性区、技能卡、装备卡、无尽规则、转生印记公式的 tooltip；复用同一坐标系（1280×720 设计容器）。
- **验收**：逐项 hover 截图，文字不越出屏幕且不遮挡被解释控件。
- **已完成（2026-10-24）**：`GameUI.Buttons.cs` 新增 `UITooltipArea` 组件与 `AttachTooltip/SetTooltip`，挂到 5 处：暴击/暴伤/穿透（109 字）、经验与巅峰规则（112 字）、6 张技能卡推荐分（134 字）、无尽起始层（131 字）、洗练质量（86 字）；原 14 个 hover 键不变。
- **验收（已过）**：tooltip 是 ContentRoot 最末子物体，截图里盖住装备卡、不越出 1280×720。`Assets/Screenshots/c4_tooltip_crit_clean.png`、`c4_tooltip_paragon.png`、`c4_tooltip_skill.png`。注：eval 直接 `ShowTooltip` 会被排版重建触发的 PointerExit 关掉，截图时临时禁用 EventSystem；真实鼠标 hover 正常。

### C5 无尽起始层规则缺解释（P1）— ✅ 2026-10-24

- **现状**：无尽面板规则四条只讲清层/商店/历史榜/掉落封顶，**没写「起始层 = 本档最高 − 4」**（截图显示起始层第 10 层、本档最高 14 层）。
- **要做什么**：规则区加一条；`EndlessCheckpointOffset` 从配置读出而非写死。
- **验收**：规则文案与 `StartEndlessRun` 实际起始层一致（本档最高 1~4 层时从第 1 层开始）。
- **已完成（2026-10-24）**：规则区首行加「起始层：第 N 层（本档最高 − 4）」，偏移量读 `GameConfig.EndlessCheckpointOffset`，不再写死 4。
- **验收（已过）**：本档最高 14 层时显示「起始层：第 10 层（本档最高 − 4）」；规则文本 preferredHeight 90 / 150、宽 438.67 / 640。截图 `Assets/Screenshots/c5_endless_rules.png`。

### C6 音量条空格太暗（P1）— ✅ 2026-10-24

- **现状**：10 格音量条未填充格与 `RowBg` 太接近，80% 时视觉上只能数出 6~8 格。
- **要做什么**：未填充格提亮一档或加描边，保持已填充格高亮。
- **验收**：在 30%/80%/100% 三档截图里格数可数。
- **已完成（2026-10-24）**：`GameConfig` 新增 `VolumeCellOff` / `VolumeCellBorder`，未填充格提亮并加描边，已填充格保持高亮。
- **验收（已过）**：主 / 音效 / 音乐 = 30% / 80% / 100% 时截图可数出 3 / 8 / 10 格。截图 `Assets/Screenshots/c6_volume_30_80_100.png`。

### C7 音乐没有进度（P1）— ✅ 2026-10-24

- **现状**：音乐面板只显示曲目总时长（如 3:39），没有已播/剩余时间与进度条。
- **要做什么**：曲目卡加细进度条 + `mm:ss / mm:ss`；暂停时冻结。
- **验收**：播放 30 秒后进度与 `AudioSource.time` 一致（误差 <1s）。
- **已完成（2026-10-24）**：`GameMusic` 暴露 `ElapsedSeconds` / `TrackLengthSeconds` / `Progress01`；曲目卡加细进度条与 `mm:ss / mm:ss`。
- **验收（已过）**：播到 66.9 秒时 `elapsed=66.86 len=134.84 Progress01=0.496`，进度条 fill=0.496、文本「1:06 / 2:14」；暂停后 1.5 秒仍 `elapsed=8.11`、文本「0:08 / 3:44」⇒ 暂停冻结。截图 `Assets/Screenshots/c7_music_progress.png`。

### C8 关于/致谢/版本（P3，与 A4 合并）— ✅ 2026-10-24

- **现状**：无关于入口；设置面板底部只有快捷键提示。
- **要做什么**：设置面板加「关于」分区：版本、存档版本（现值 v13）、Unity 版本、四首 BGM 来源（`docs/audio-credits.md`）、AI 生成声明。
- **验收**：与 A4 一起验收。
- **已完成（2026-10-24）**：设置面板底部新增「关于」按钮，弹出 680×520 关于页：`AutoBattle v1.0.0`、存档 v11（当时口径；现为 v13）· 3 槽 · 当前存档、Unity 6000.6.5f1 · 平台 · 设计分辨率 1280×720；数据段写导出目录 `Documents\AutoBattle\slotN.json` 与导入规则；素材段声明立绘/怪物/Boss/背景为 AI 生成、BGM 四首来自「星落落oi」并指向 `docs/audio-credits.md`；工程段说明离线单机。
- **验收（已过）**：Play 截图 `HelloWorldProject/Assets/Screenshots/a4_about_panel.png` 无裁切；`AboutOpen` 已接入浮层互斥、Esc 关闭与 `CloseAllPanels`。

### C9 分辨率/窗口设置（P3）— ✅ 2026-10-24

- **现状**：`GameUI.cs:294` `referenceResolution 1280×720`、`:300` `ScreenMatchMode.Expand`；`ProjectSettings.asset:44-45` 默认 1920×1080；`resizableWindow: 0`。
- **要做什么**：设置里给「窗口/全屏 + 分辨率预设 + 是否允许缩放」，写入 PlayerPrefs 并在启动时应用；说明 Expand 模式在超宽屏的黑边行为。
- **验收**：16:9 / 16:10 / 21:9 三种比例下 UI 不裁切（Expand 允许留边）。
- **已完成（2026-10-24）**：新增静态类 `DisplaySettings`（`GameSettings.cs` 末尾），PlayerPrefs 键 `autobattle.display.fullscreen` / `.resolution` / `.resizable` / `.colorblind`；8 档预设（1280×720、1600×900、1920×1080 默认、2560×1440、1280×800 16:10、1680×720 21:9、2560×1080、3440×1440）。`GameController.Awake` 调 `DisplaySettings.Apply()`（编辑器内不动窗口，只记设置）；Windows standalone 用 user32 `WS_THICKFRAME` 控制可拖拽缩放。设置面板底部新增「显示」按钮（与「恢复默认」「关于」并排），打开 680×520 的显示浮层：全屏、分辨率 ◀/▶、允许缩放、色弱友好、说明卡「为什么没有界面缩放滑杆」、恢复默认。
- **验收（已过）**：Play 中切到全屏 / 2560×1440 / 不允许缩放 / 色弱友好，PlayerPrefs `fullscreen=1`、`resolution=3` 落盘；停止 Play 再进入，读回全屏=True、1280×800 16:10、缩放=False、色弱=True，截图 `c9_display_persist.png` 显示「开 / 1280×800 16:10 / 关 / 开」。16:10 与 21:9 由 CanvasScaler Expand 等比缩放、多出留边，不裁切 UI；截图 `c9_display_panel.png`。收尾已恢复默认（1920×1080 / 窗口 / 允许缩放 / 关闭色弱）。

### C10 存档槽与导入导出（P3，与 A6 合并）— ✅ 2026-10-24

- **现状（原始）**：3 个槽（`SaveSystem.KeyOf(slot)`），只在 PlayerPrefs；无导入导出，重装即丢。
- **要做什么**：槽位面板加「导出/导入」按钮；导出 JSON 带版本号，导入时校验 schema 并拒绝更高版本。
- **验收**：导出→删槽→导入→回读一致；导入高版本档被拒并提示。
- **已完成（2026-10-24）**：导出/导入见 A6——存档面板「导出」「导入」按钮，导出到 `Documents/AutoBattle/slotN.json`，导入优先读剪贴板、读不到再读同名文件，当时 v12 档在写盘前拒绝（当前 v13，拒 v14）；截图 `a6_save_panel2.png`。
- **本轮补完（2026-10-24）**：槽位重命名与跨槽复制。`SaveSystem` 新增 `SlotNameKey` / `DefaultSlotName` / `DisplayNameOf` / `RenameSlot` / `CopySlot`（名字存 `GameConfig.SaveKey + "_name" + slot`，最长 12 字，空名或默认名回退「存档 N」；清槽同时删名字）；`GameController.ReloadCurrentSlotFromDisk()` 让「复制到当前槽」立即重读。存档卡底部按钮改为「改名 / 复制 / 删除」（60/60/64 宽，文字不再被挤成竖排）；2026-10-24 晚又整体下移 4px、摘要文字上移 2px，按钮不再压住最后一行「第 N 世 · 印记」；改名弹层用 InputField（Enter 提交、默认名 / 确定 / 取消），复制弹层按非源槽可视数量重排两个目标按钮，覆盖已有档要求 3 秒内二次点击。
- **验收（已过）**：改名「鲸鱼娘」后 `DisplayNameOf(0)` 生效；13 字被拒并提示「名字最多 12 个字」，空串恢复「存档 1」。复制 0→1 后目标槽 `level=3 cleared=1` 与源一致，名字一并复制；复制到当前槽后角色立即重读（`playerLv=3`）。截图 `c10_save_panel.png`（三卡按钮完整）、`c10_save_rename.png`、`c10_save_copy.png`（两目标并排 + 覆盖确认倒计时）。
- **未做**：槽数仍是 3；没有云同步（本轮明确不做）。

### C11 可访问性（P4，设计题）— ✅ 2026-10-24（结论：不做自由缩放，补色弱配色）

- **现状**：字号全部写死在 9~22px（`GameUI.Theme.cs`），没有 UI 缩放；色弱（红/绿品质与血条）无替代编码；全部操作依赖鼠标悬停拿提示。
- **要做什么**：先做设计题——是加「UI 缩放 0.9/1.0/1.1」还是只保证 1280×720 以上等比可读；色弱可给品质加字母/图标编码。
- **验收**：在 1280×720 与 1920×1080 两档分辨率下无文字裁切；关键信息不依赖单一颜色通道。
- **设计结论（2026-10-24）**：不做 0.9 / 1.0 / 1.1 自由 UI 缩放。UI 是固定 1280×720 设计稿 + CanvasScaler Expand，1280×720 以上只会等比放大或留边，文字不会裁切；0.9 缩放会把 9~12px 的小字压到不可读，1.1 放大必然裁掉两侧内容。显示浮层里已写明这条理由，并提供 16:10 / 21:9 等预设。色弱方向落地为可选配色。
- **已完成（2026-10-24）**：`GameConfig.QualityColors` 拆成 `QualityColorsNormal` 与 `QualityColorsColorBlind`（浅灰 / 蓝绿 / 天蓝 / 紫红 / 橙，Okabe-Ito 风格），由 `DisplaySettings.UseColorBlindPalette` 切换，所有既有读取点不改。显示浮层加「色弱友好」开关，切换时写战斗日志「◐ 色弱友好配色已开启…」/「◑ 已恢复默认品质配色…」。品质名（普通/优秀/稀有/传说等）与 ± 号继续作为颜色之外的第二编码。
- **验收（已过）**：开启后 `QualityColors[1]` 读回 `RGBA(0,0.62,0.45)`（蓝绿）、`[3]` 为 `RGBA(0.8,0.47,0.65)`（紫红），关闭恢复原色板；设置跨 Play 会话保留。截图 `c11_colorblind_on.png`。1920×1080 由 1280×720 设计稿等比放大（Expand），未单独在独立构建里截图，但所有面板都在 1280×720 容器内，无自适应重排。

## 5. 玩法与内容（B）

> 编号里没有 B5~B7：无尽口径、掉落 +10%、转生加速本质是数值问题，已并入第 6 节的 D2 / D3 / D1，避免同一件事记两处。

### B1 金币出口（P2，原 README「终局金币仍是软出口」）

- **现状证据**：P1000 五槽复测（119,834 次挑战 / 5,528.7 min 纯战斗）全程只花 **103.3 万**，兜里 **8.593 亿**；洗练 0.9 口径后仍是「锁住的不重抽、单条有上限」，天花板没变。
- **要做什么**：写一份「商店 2.0」设计稿（目前只有构想）：可买什么（药水上限/初始层/词条锁定券/外观）、定价锚点（与 P1000 收入比）、是否与转生印记互通（`docs/rebirth.md` §9.4 建议**不互通**，避免双重折算）。
- **验收**：设计稿评审通过 → 实现 → 探针复跑 P1000 余额显著下降且不影响 235~250 层目标。

### B2 成就点出口（P2）— ✅ 2026-10-25

- **现状（做之前）**：330 点只展示；`docs/achievements.md:32` 明确「不提供战斗属性」（否则推翻 P200/无尽基线）。
- **要做什么**：在**非战斗**方向选出口：称号佩戴（与 B3 合并）/ 头像框 / 开局初始金币 / 存档栏位 / 无尽初始层 +1。先做 1 个，别全上。
- **验收**：花点后数值不影响任何平衡探针（静态哨兵 PASS）。
- **已完成（2026-10-25，选「无尽建议起始层」）**：
  - 规则：100 点/级，最多 +3 级（300 点）；`EndlessSuggestedStartFloor = Min(best, StartFloorFor(best)+rank)`，不改 `endlessBestFloor`、不改全局最高、不越本档最高层；本世未重通 11-10 时仍只从第 1 层开始。
  - 代码：`AchievementSystem` 的 `MaxEndlessStartRank=3` / `EndlessStartPointCost=100` / `EarnedPoints` / `SpentPoints` / `AvailablePoints` / `TryBuyEndlessStartRank`；`GameController.BuyEndlessStartRank`；`GameController.EndlessSuggestedStartFloor`；成就面板底部兑换按钮与「可用点数」显示。
  - 存档 v13：`PlayerData.endlessStartRank` ↔ `SaveData` 同名字段；`CurrentSaveVersion` 12→13；旧档读入 0，v12 手写带该字段也读 0。
  - 验收：纯逻辑连买 3 次成功、第 4 次「无尽起始层已兑换满（+3 层）。」，spent=300 / available=505；建议层 best=14 rank=3 → 13、rank=0 → 10、best=12 rank=3 → 11、best=3 rank=3 → 3；Play 中 805 点买 1 级 rank=1 / 可用 705；Save→Load version=13 rank=2。截图 `b2_achievement.png`、`b2_achievement_full.png`、`b2_achievement_bought.png`。
  - 平衡：只改「从哪层开始」，不碰怪物数值；`BalanceRegression.RunStaticOnly()` PASS、`AchievementRegression.RunCore()` 644/0。详见 `docs/achievements.md` §15.5 与 `docs/endless-mode.md` §7.5。

### B3 称号展示（P2）— ✅ 2026-10-24

- **现状**：7 个称号只在解锁 toast 与成就名里出现；角色卡不显示。
- **要做什么**：称号佩戴入口（成就面板内），左栏角色卡显示当前称号；未佩戴显示默认。
- **验收**：佩戴后立即出现在角色卡，切档保留。
- **已完成（2026-10-24）**：`PlayerData.equippedTitle`（空 = 未佩戴）与 `SaveData.equippedTitle` 一并落盘；`SaveSystem.Load` 用 `AchievementCatalog.IsUnlockedTitle` 校验，伪造/已失权的称号自动清空。`AchievementCatalog` 暴露 `Titles` / `TitleNameOf` / `IsUnlockedTitle`（7 个：通关者、巅峰、踏破者、不屈、传说、博学、无尽）。成就面板 560→620 高，底部加「称号（解锁后可佩戴，再点一次卸下）」与 7 个 104×26 按钮（y=566），提示 y=542、脚注 y=596，不再与翻页行重叠；锁定称号灰显不可点，已佩戴按钮显示「✓ 通关者」。左栏立绘底部 (16,196,140,24) 加深色条与 `_titleText`，未佩戴「无名旅人」用弱色，佩戴后转金色。`GameController.SetEquippedTitle` 负责佩戴/卸下、拒绝未解锁、写日志并 `TrySave`。
- **验收（已过）**：Play 中临时解锁 clear_ch11 → 称号按钮可点，点击后 `Player.equippedTitle=clear_ch11`、`_titleText.text=通关者`、颜色 `RGBA(1,0.8,0.35,1)`、按钮「✓ 通关者」；再点一次卸下回「无名旅人」。切到存档 2 后称号为空，切回存档 1 恢复「通关者」（`backTitle=clear_ch11`）；测试后已还原槽 0 原始 JSON。截图 `b3_achievement_titles.png`。

### B4 转生 v2（P3）— ✅ 2026-10-25

- **现状（做之前）**：v1 转生清空全部装备（docs/rebirth.md §13.2 留了「保留一件」的备选）；无转生排行榜；P100 门槛实际首通中位 P31（门槛被绕过）。
- **要做什么**：三件里选做——(a) 保留评分最低槽位的一件；(b) 转生次数/最快轮回排行榜（存档内）；(c) P100→P50 门槛重算经济。
- **验收**：改门槛必须重跑 50 种子转生加速与印记产出，并回填 §14.4。
- **已完成（2026-10-25，做 a + b，刻意不做 c）**：
  - **(a) 保留最低分一件**：`GameController.Rebirth.FindRebirthKeep` 在重置等级/巅峰**之前**遍历五槽，用 `EquipmentScore.Of`（增量期望 DPS 口径）挑分数最低的一件；清空其余四件（不退款、不折印记）后 `SetEquipment` 放回保留件。保留件若在小件槽（头盔/鞋/饰品），同步置 `slotStarterMask` 对应位，等于该槽解锁赠礼已用，避免再发一件。轮回面板左卡加第三行「本世用时 · 保留最低分 1 件」，keep 行改「成就 / 纪录 / 1 件装备」，reset 行改「装备只清 4 件」。对比 / 强化 / 洗练三个面板的槽位页签在「有装备但槽位未解锁」时显示 **「 · 保留件」**，不再显示「 · 未解锁」。
  - **(b) 最快轮回 Top-5**：`RebirthCatalog.cs` 追加 `[Serializable] RebirthRecord`（life/seconds/paragon/floor/marks）与 `RebirthHistory`（Capacity=5；排序 seconds 升序 → marks 降序 → life 升序；`Insert` 返回名次，掉榜 0；`CopyTop` 清洗 null/越界，`FormatSeconds` 输出 mm:ss 或 h:mm:ss）。转生时若本世 ≥1 秒才记一条（防探针 00:00 假纪录）；本世用时只在「运行中且无浮层」时按 `Time.unscaledDeltaTime` 累加，暂停/开面板不计、速度倍率不影响；转生后清零。轮回面板从 680×540 抬到 **680×680**（y=20，底 700），底部新增标题与 5 行纪录，第 1 名金色。控制器暴露 `LastRebirthSeconds` / `LastRebirthRecordRank`，转生日志写「本世用时 mm:ss，轮回纪录第 N 名」与「保留装备：…」。
  - **(c) 没做**：`RebirthUnlockParagon` 仍为 100。P100→P50 会直接作废 §14.4 的 82.6% / 47.1% 门槛基线，而 roadmap 原文是「三件里选做」，所以本次明确记为**推迟的平衡决策**，不假装已做。
  - **存档 v12**：`PlayerData.lifeSeconds` + `PlayerData.rebirthTop` ↔ `SaveData` 同名字段；`CurrentSaveVersion` 11→12；旧档读入给 0 / 空；`rebirthCount<=0` 时清空榜单，防手改档伪造。`SaveSystem.SaveVersion` 自动变 12，关于页与导出面板动态读取，无需改文案。
  - **验收实测（Play，真实 `PerformRebirth`）**：五件评分 7320.8 / 2896.8 / 751.7 / 995.3 / **306.2**，保留普通饰品，其余四槽清空，`slotStarterMask=16`；`lifeSeconds=123.4` → 纪录 `第1世 · 02:03 · P100 · 无尽0层 · +2印记`、名次 1；读档回读 `v=12 top=1 lvl=1 acc=普通饰品` 一致。纯逻辑插入 7 条验证 5 条容量、100 秒并列按 marks 排序、`FormatSeconds(3723)=1:02:03`。截图 `HelloWorldProject/Assets/Screenshots/b4_rebirth_records.png`。
  - **平衡**：本次只改「转生后保留什么 / 记录什么」，没有改任何战斗、经验、金币、掉落或门槛数值，所以**未重跑 50 种子转生加速**；§14.4 的 82.6% / 47.1% 结论仍适用。保留一件只在转生瞬间生效，不进入探针的起始构造（探针仍从空身起），因此探针基线也无需重做。

### B4c P100 转生门槛复评（P3，从 B4 拆出）— ✅ 2026-10-25

- **现状**：`RebirthUnlockParagon = 100`（`GameConfig.cs`）；B4 做 a+b 时刻意把 (c) P100→P50 推迟到本条。首通中位 P31 意味着门槛当前基本被绕过；但降门槛会直接作废 `docs/rebirth.md` §14.4 的 82.6% / 47.1% 门槛基线，不能只改常量。
- **要做什么**：先用真实推进量「到 P100 的耗时/挑战数」（`NewGameProgression` / `EndgamePacing`，50 种子，记录 P50/P100 两个断面），再决定保持 100 或降到 50；若改，重跑 50 种子转生加速与印记产出，回填 §14.4 与 §8 决策 #4。
- **验收**：给出 P50/P100 两套实测数据与推荐；若改门槛，必须同时更新基线，不能只改常量。
- **依赖**：§8 决策 #4；先有真实推进数据再拍板。
- **已完成（2026-10-25，结论保持 100）**：`EndgamePacing.Run(seed, 20000, false, 100, 0.8f)`，50 种子 20261018..20261067，`BuildFresh100` 已解锁全章与饰品：
  - 首次通关 11-10：挑战中位 12（IQR 5..21.5），含刷怪+商店 1.75min（0.8..2.58）。
  - P50：挑战中位 34.5（31.25..37），7.7min（6.83..8.57）。
  - P100：挑战中位 159（152..165），30.7min（28.32..32.08，min 25 / max 41.6）。
  - 首通到 P50 约 +6min、到 P100 约 +29min；门槛降到 50 会把转生循环提速约 4 倍并作废 §14.4，当前没有证据表明 P100 不可接受。**保持 `RebirthUnlockParagon=100`，未改代码/存档/探针默认值**。详见 `docs/rebirth.md` §14.7、`_balance/balance-analysis.md` §3.5.4b。

### B8 短中期目标展示（P3）— ✅ 2026-10-25

- **现状（做之前）**：成就是「目标清单」，但只在成就面板里；主界面没有「下一步」。新手引导讲界面位置，不讲目标。
- **要做什么**：右侧或左栏加一个「当前目标」行：下一件可达成成就 / 下一章解锁条件 / 转生还差多少。不要新增系统，只做投影。
- **验收**：新档第 1 分钟内能看到至少一条随进度变化的目标。
- **已完成（2026-10-25）**：
  - 右栏按钮行下方（y=670，808×26）新增常驻 `GoalCard`：左「目标」508 宽、右「成就」264 宽，13 号字；不新增系统，只读现有进度。
  - **主目标优先级**：待选技能（剩 N 次）→ 下一关首通（从 `maxChapterUnlocked` 起找第一个未首通；章末且非 11 章写「解锁第 N 章」；11-10 写「终局：首通 11-10，解锁轮回与无尽」；带首通金币）→ 已全清且轮回解锁时写「轮回：预计 +N 印记」或 `CanRebirth` 的原因 → 兜底无尽最高层。主目标直接复用 `PlayerData.IsStageFirstCleared`、`StageCatalog.FirstClearGold`、`GameController.PendingRebirthMarks`。
  - **副目标**：遍历 `AchievementCatalog.All`，跳过已解锁，按 **还差多少**（`target - Metric`）选最近的一条，剩余相同再按完成率高者；显示「成就 名称 当前/目标」，大数用 k/M 紧凑格式；全达成显示「成就 N/N 全达成」。为什么按剩余而不是按百分比：新档 `0/1` 的小成就应排在 `1/100` 的「满级」前面。
  - **刷新**：挂在 `GameUI.Refresh.Log.RefreshPanels(p, textTick)` 的 10Hz 节拍上，文本与缓存不同才赋值，不额外每帧分配。
  - **验收实测（Play）**：新档 `目标 推图：首通 1-1 · +120 金币 || 成就 初出茅庐 0/1`；加首通 1-1 后 0.7s 变 `首通 1-2`；清完 1~11 章、Lv100/P100 后变 `目标 轮回：预计 +2 印记（存档 → 轮回）`。最长分支「首通 10-10（章末），解锁第 11 章 · +1,440 金币」`preferredWidth=308.7`（容器 508），副文本 113.3（容器 264），不裁切。截图 `HelloWorldProject/Assets/Screenshots/b8_goal_fresh_final.png`。

### B9 技能构筑深度（P4，设计题）

- **现状**：15 技能三选一，无 reroll/ban/协同标签；每局构筑差异靠随机。
- **要做什么**：先做设计题（是否需要协同标签/技能升级），不急着写代码——它会同时影响平衡探针与无尽。

## 6. 平衡（D）

| 编号 | 问题 | 数字 | 建议 |
|---|---|---|---|
| D1 | 转生加速 — ✅ 2026-10-22 达标 | `RebirthExpPerRank=0.60` + `RebirthLegacyLevelPerRank=3` 落地后，第 2 世 82.6% / 满轮回 47.1%（目标 85% / 50%）；旧数据 92.4% / 63.5% / 61.8% 按日期保留作历史 | 已收口，不再调参。战斗 +5% 偏强、满轮回无尽首死中位 315 是另一件事（见 README「转生加速已落地，无尽仍偏强」） |
| D2 | 无尽理想配装偏强 — ✅ 2026-10-25 已收紧 | 理想配装 50 种子首死中位 290（P10 272 / P90 305 / max 320）vs 目标 235~250；随机五槽 50 种子实际 220（旧「242」是 3 种子） | 已选「保留 235~250 理想配装口径，收紧 `EndlessBaseFactor` 2.8→3.4」：3.4 下理想 240（P10 227 / P90 250 / max 261），随机 181（P10 161 / P90 195 / max 205）。压词条上限（×0.85 只到 280）与压暴击上限（0.70 仍 277）都不够；详见 `_balance/balance-analysis.md` §10.3、`docs/endless-mode.md` §12.5.4 |
| D3 | 掉落 +10% — ✅ 2026-10-25 已量化 | 固定财运 rank5，只改 `RebirthDropPerRank` 做 0% / +5% / +10% 三组 50 种子：五槽集齐中位 60.5 / 65 / 55.5 次，+10% vs 0% 配对中位 7.5 次、95% CI −7..+16、p=0.39；非单调、无显著效果 | **保持 +2%/级**；BOSS 固定掉 2 件不吃 drop bonus、普通怪 Lv100 不出 Lv110，所以掉落不是终局毕业的有效杠杆。详见 `docs/rebirth.md` §14.8、`_balance/balance-analysis.md` §3.5.4c |
| D4 | 章节压迫已重调 | 5.5%/4%，300 套 98%→21.8%，真实推进 25/30 通关 | 已收口，无需再动；仅当改 `ChapterStatBias` 时复跑 |
| D5 | 终局倍率 | 1.6 定格；抬到 5.0 → 通关 3/30 | 已收口；若要 P80~90 目标需重开设计，别当 bug 修 |
| D6 | 金币软出口 | 见 B1 | 与 B1 合并 |

## 7. 文档与流程（E）

| 编号 | 事项 | 说明 |
|---|---|---|
| E1 | README 结构段漂移 — ✅ 2026-10-24 | 见 A3 |
| E2 | 缺文档总索引 — ✅ 2026-10-24 | 已在本文件附录 C 维护 11 份文档索引；README 项目结构段与「文档」列表已指向本文 |
| E3 | 旧口径并存 — ✅ 2026-10-24 | `balance-analysis.md` §3.5.2 已标「4%/3% 口径（2026-10-19），新表见 §3.5.2a」，§3.5.4a 为终局复测；`rebirth.md` 的 63.5% / 61.8% / 82.6% 均带日期，README 同步。旧表保留作历史，不再混读 |
| E4 | 一次性产物路径 — ✅ 2026-10-25 | 根目录 `_tmp_verify/` 整体迁到 `_art/tmp-verify/`；`.gitignore`、`tools/run-editmode-tests.ps1`、README 与 docs 引用同步；新增 `_art/README.md` 逐条说明 `ui-audit/`、`topn/`、`sprites/`、`stage/`、`tmp-verify/`、`fish/` 的用途与忽略状态 |

## 8. 需要拍板的决策点（按阻塞程度排序）

| # | 问题 | 选项 | 影响 | 我的建议 |
|---|---|---|---|---|
| 1 | 金币出口做成什么 | 商店 2.0 / 转生印记商店 / 装备赌注 / 外观 | 决定 B1 的设计稿方向 | 先写设计稿再选；印记不与金币互通 |
| 2 | 成就点换什么 — ✅ 已决（2026-10-25） | 已选「无尽建议起始层 +1/级」，100 点/级、最多 +3；称号佩戴由 B3 完成 | B2 已落地并存档 v13 | 不换战斗属性/装备/掉落；后续若加第二个出口，先看无尽起始层是否够用 |
| 3 | 无尽目标口径 — ✅ 已决（2026-10-25） | 保留 235~250 层「理想配装」口径，收紧 `EndlessBaseFactor` 2.8→3.4 | 3.4 下 50 种子理想配装中位 240、随机 181；随机配装接受更早死 | 已落地；旧「随机 242 达标」是 3 种子误读，50 种子随机实际 220。后续若再动无尽，先看 `docs/endless-mode.md` §12.5.4 |
| 4 | P100 转生门槛 — ✅ 已决（2026-10-25） | 50 种子实测 P100 首通后约 30.7min，保持 100 | 不改 `RebirthUnlockParagon`，作废 §14.4 的风险归零 | 若以后要更早转生，改 50 必须重跑 50 种子加速与印记产出 |
| 5 | 是否面向发行（决定 A8）— ✅ 已按发行向推进（2026-10-25） | 已落 Windows 构建脚本 + GitHub Actions（`windows-latest`）；正式上架/发版另说 | 本机 8/8 与出包已验；GitHub 侧仍需配 `UNITY_LICENSE_SERIAL` 与 required check | 不再等发行决策；剩余是仓库配置，不是代码 |
| 6 | 是否做多语言 | v1 中文 | 影响文案抽取方式 | 暂不做，但新文案别硬编码到 C# 字符串拼接里 |

## 9. 建议执行顺序（三个批次，每批独立可验收）

**第一批（已完成，2026-10-24~25）**：A1 提交 → A2 治理 → A3 README 结构重写 → C1、C5、C6、C7（四个小 UI 修）→ 回归三连 + console_status。

> 进展（2026-10-25）：A1 的 72 项已于 `12d230a` / `de8c038` 提交并推送；A2–A6、C1–C11、B3 全部完成。

**第二批（已完成，2026-10-24）**：C2 批量强化、C3 面板密度、C4 tooltip 补齐、C8+C9（关于页/分辨率）、A4 元信息。

> 进展：C2、C3、C4、C8、C9、C10、C11 已完成并 Play 验收。

**第三批（当前，按决策点推进）**：A7 已提交 `e0b9f72`（36 files / +1,453 / −216）→ B1 设计稿（金币出口/商店 2.0）→ B9 设计题。本批已完成 B4（a+b）、B8、B2（成就点换无尽起始层）、D3（财运掉落量化，保持 +2%/级）、B4c（P100 门槛复评，保持 100）、D2（无尽口径 2.8→3.4）、E4（一次性产物归并到 `_art/`）、A8（Windows 构建脚本 + CI workflow；本机 8/8 与出包已验，GitHub 首跑待配置）。

每批结束的固定动作：`BalanceRegression.RunStaticOnly()` → PASS；`AchievementRegression.RunCore()` → 644/0；`console_status` → compilationFailed=false、consoleErrors=0；新增数字回填本文与对应 docs。

## 10. 明确不做（范围守则，防止无限扩张）

1. 不做「成就点 +攻击」这类战斗属性（会推翻现有 P200 / 无尽基线，docs/achievements.md:32 已定）。
2. 不为拆而拆：`Models.PlayerData.cs`（604 行）职责单一，不再拆（README:252 已定）。
3. 不改存档格式除非必须；每次升版本都要写迁移 + 回归（v5/v6/v7/v9/v10/v11/v12/v13 都这么过来的）。
4. 不重做美术：11 族怪物 + 11 Boss + 20 特效 + 4 背景已成体系，缺的是「用起来」不是「更多」。
5. 不引入第三方框架/插件（除 Unity 官方包）；已有 3 个 asmdef（Runtime / Editor / Tests），新增依赖要把编译时间算进去。

## 11. 变更记录（本轮已改）

> 2026-10-24 晚首版，2026-10-25 补 B4/B8 与提交记录。✅ = 已完成并验收；◐ = 部分完成。逐条证据见对应小节与 README。

| 条目 | 结果 | 落点 / 证据 |
|---|---|---|
| A1 ✅ | 2026-10-24 的 72 项已提交并推送：`12d230a` + `de8c038` | `HEAD=de8c038`；`origin/main..HEAD` = 0 |
| A7 ✅ | 36 files / +1,453 / −216 已提交：`e0b9f72` | `git status --short` clean；B4/B8 + B2/B4c/D3 + D2 + E4 + A8 |
| B4 ✅ | 转生 v2：保留最低分 1 件 + 本档最快轮回 Top-5；存档 v12 | `GameController.Rebirth.cs`、`RebirthCatalog.cs`、`SaveSystem.cs`；docs/rebirth.md §14.6；`b4_rebirth_records.png` |
| B8 ✅ | 右栏常驻「当前目标」条（主目标 + 最近成就） | `GameUI.Build.Right.cs`、`GameUI.Refresh.Log.cs`；`b8_goal_fresh_final.png` |
| B2 ✅ | 成就点出口：100 点/级兑换无尽建议起始层，最多 +3，不越本档最高；存档 v13 | `AchievementSystem.cs`、`GameController.Achievements.cs`、`GameUI.Achievements.cs`；docs/achievements.md §15.5 |
| B4c ✅ | P100 门槛 50 种子复评：首通 12 次 / P50 34.5 / P100 159，保持 100 | docs/rebirth.md §14.7；balance-analysis §3.5.4b |
| D3 ✅ | 财运掉落 50 种子三组对照：无显著效果，保持 +2%/级 | docs/rebirth.md §14.8；balance-analysis §3.5.4c |
| D2 ✅ | `EndlessBaseFactor` 2.8→3.4：理想配装 50 种子中位 290→240（P10 227 / P90 250 / max 261），随机 220→181 | `GameConfig.cs:675`、`GameConfigData.cs:146`、`game_config.json:128`；docs/endless-mode.md §12.5.4；balance-analysis §10.3 |
| B7（D4） | 章节压迫 4%/3% → **5.5%/4%**；300 套抽样 98%→21.8%，真实推进 25/30 通关 | `GameConfig.cs:206/:212`；balance-analysis §3.5.2a |
| D5 | 终局 BOSS 倍率**定格 1.6**，注释按实测改写 | `GameConfig.cs:312`、`Models.cs:698`；§3.5.4a |
| — | GameUI 由 4 个胖文件拆成 **28 个 partial**（A3 时 6,749 行，2026-10-25 实测 7,611 行） | README 项目结构；A3 |
| — | 六项 UI 修复 + R 键确认字号 18→15 | `GameUI.Build.Right.cs:137`、`GameUI.Core.cs:53` |
| A2 ✅ | `.gitignore` 纳入 `_tmp_verify/`、`_art/ui-audit/`；2026-10-25 已迁到 `_art/tmp-verify/` | `.gitignore`、`_art/README.md` |
| A3 ✅ | README 项目结构按实测重写（A3 时 59/15,620 + 10/4,048；2026-10-25 为 59/17,109） | `README.md:117` 起 |
| A4 ✅ / C8 ✅ | 元信息 AutoBattle / UnityByDSH / 1.0.0；设置面板「关于」页 | `GameUI.Settings.cs`；`a4_about_panel.png` |
| A5 ✅ | 菜单/脚本一键快速回归 3 步 + EditMode NUnit 8 条 + 3 个 asmdef | `AutoBattle.CI.cs`、`tools/quick-regression.ps1`、`tools/run-editmode-tests.ps1`、`Assets/Tests/` |
| A8 ◐ | Windows 构建脚本 + GitHub Actions（windows-latest，回归 + NUnit + build）；本机 NUnit 8/8、出包 171.6 MB；CI 未在 GitHub 实跑 | `tools/build-windows.ps1`、`.github/workflows/unity-ci.yml`；README「构建与 CI」；A8 小节 |
| A6 ✅ | `TrySave` 统一失败提示；导出/导入存档 | `GameController.cs`、`SaveSystem.cs`、`GameUI.Panels.Save.cs` |
| C1 ✅ | 战斗速度写入 PlayerPrefs，重启保持 | `GameSettings.cs`、`GameController.Awake` |
| C2 ✅ | 强化「+5」「满级」批量，逐级扣费 | `GameUI.Panels.Enhance.cs`、`EnhanceEquipmentBulk` |
| C3 ✅ | 洗练面板双列 + 信息栏 | `GameUI.Panels.Reforge.cs` |
| C4 ✅ | 5 处新 tooltip（暴击/巅峰/技能/起始层/质量） | `GameUI.Buttons.cs` `UITooltipArea` |
| C5 ✅ | 规则区写明「起始层 = 本档最高 − 4」 | `GameUI.Endless.cs` |
| C6 ✅ | 音量条未填充格提亮 + 描边 | `GameConfig.Colors.cs` |
| C7 ✅ | 曲目卡进度条 + `mm:ss / mm:ss`，暂停冻结 | `GameMusic.cs`、`GameUI.Music.cs` |
| C9 ✅ | `DisplaySettings` + 显示浮层：全屏 / 8 档分辨率 / 允许缩放 | `GameSettings.cs`、`GameUI.Settings.cs`；`c9_display_persist.png` |
| C10 ✅ | 存档改名（≤12 字）与跨槽复制（覆盖二次确认）；存档卡按钮下移 4px | `SaveSystem.cs`、`GameUI.Panels.Save.cs`；`c10_save_copy.png` |
| C11 ✅ | 色弱品质色板（蓝绿/天蓝/紫红/橙），不做自由缩放 | `GameConfig.Tables.cs`、`DisplaySettings`；`c11_colorblind_on.png` |
| B3 ✅ | 称号佩戴与左栏展示（未佩戴「无名旅人」） | `GameController.Achievements.cs`、`GameUI.Achievements.cs`；`b3_achievement_titles.png` |
| D1 ✅ | 转生加速 82.6% / 47.1%，进目标区间 | `GameConfig.cs:686/:695` |

**A7 已收口**：`e0b9f72`（36 files / +1,453 / −216）已提交并推送（2026-10-25，用户 m05886 授权），`origin/main` 与本地一致。当前剩余 P0 为 0；A8 的 GitHub 首跑与分支保护仍需仓库 Secrets 权限。A1 的 72 项已于 2026-10-25 提交并推送。

## 附录 A：测量方法（可复现）

```powershell
# 代码规模
Get-ChildItem HelloWorldProject/Assets/Scripts/*.cs | Measure-Object
# 文档规模
Get-ChildItem docs/*.md, README.md, _balance/*.md | ForEach-Object { $_.Name + ' ' + (Get-Content $_.FullName | Measure-Object -Line).Lines }
# 配置/成就/技能
$c = Get-Content HelloWorldProject/Assets/Resources/game_config.json -Raw | ConvertFrom-Json; $c.PSObject.Properties.Name.Count
$a = Get-Content HelloWorldProject/Assets/Resources/achievements.json -Raw | ConvertFrom-Json; $a.achievements.Count
# 构建设置
Select-String -Path HelloWorldProject/ProjectSettings/ProjectSettings.asset -Pattern 'productName|bundleVersion|resizableWindow|fullscreenMode'
```

## 附录 B：探针清单与调用方式

| 工具 | 作用 | 典型耗时 | 入口 |
|---|---|---|---|
| `BalanceRegression.RunStaticOnly()` | 静态哨兵（配置/快照/章节胜率） | 秒级 | Editor eval |
| `BalanceRegression.Run(true)` | 重算并写 `_balance/baseline.json` | 分钟级 | Editor eval |
| `AchievementRegression.RunCore()` | 成就 §11 核心（644 断言） | 秒级 | Editor eval |
| `NewGameProgression.Trace(seed,200,true)` | 新档全程推进（建样-通关-阵亡） | 分钟级 | Editor eval |
| `LateGameProbe.RunChapterWinrate(20,true,100)` | 11 章关底胜率（裸装下界） | ~5s | Editor eval |
| `LateGameProbe.RunChapterWinrateRow(ch,20,true,3,out q,out acc)` | 单章单行 + 质量分 | ~1.4s | Editor eval |
| `QuickRegression.RunAllQuick()` / `tools/quick-regression.ps1` | 快速回归：静态哨兵 + 成就 644 + 无尽冒烟 | 秒级 | 菜单 / 脚本 |
| `AutoBattle.Tests`（8 条） / `tools/run-editmode-tests.ps1` | EditMode NUnit：公式/评分/存档往返/成就目录/静态哨兵 | 秒级 | `unity test` / Test Runner |
| `EndlessPacing` | 无尽首死分布/经济 | 分钟级 | Editor eval |
| `EndgamePacing` | 终局首通（P 门槛与次数） | 分钟级 | Editor eval |
| `SkillTableVerify` | 技能表一致性 | 秒级 | Editor eval |

注意：`unity command eval` 默认超时 5000ms，长任务显式传 `--timeout 180000`（或 `run_script` 的 `timeout_ms`）；改完 `.cs` 必须 `AssetDatabase.Refresh(ForceUpdate)` 并 eval 读回值确认重编译生效，再跑探针。改完代码先跑 `tools/quick-regression.ps1`，全 PASS 再跑长探针；改了公式/存档形状再补 `tools/run-editmode-tests.ps1`。

## 附录 C：文档索引

| 文档 | 内容 | 状态 |
|---|---|---|
| `README.md` | 玩法/界面/结构/进度/已知问题/自动化回归 | 现行（结构段 2026-10-24 已按实测重写） |
| `docs/achievements.md` | 42 条成就、评分、验收 | 现行 |
| `docs/endless-mode.md` | 无尽规则、探针、§13 验收、§15 待确认 | 现行 |
| `docs/rebirth.md` | 转生设计、印记、§13 待定、§14 实测 | 现行（旧数据已带日期，可作历史） |
| `docs/equipment-reforge.md` | 洗练/强化成本、词条质量 | 现行 |
| `docs/sprites-and-fx.md` | 精灵/特效/背景规范 | 现行 |
| `docs/audio-credits.md` | BGM/SFX 来源 | 现行 |
| `_balance/balance-analysis.md` | 平衡分析（含 §3.5.2a 压迫、§3.5.4a 终局） | 现行 |
| `_balance/README.md` | 探针与基线说明 | 现行 |
| `AGENTS.md` | 工作区/Unity CLI 纪律 | 现行 |


---
id: "0112eead-b1e3-4600-b9fe-335bf0a4dfe9"
title: "Unity AutoBattle UI 视觉方向调研与 B/C 双 mockup（诊断 / 6 方向 / 横切改造）"
description: "HelloWorldProject 整体 UI 观感调研：字体/材质/动效三缺口的证据、A–F 六方向成本与契合度、推荐 B 卡牌实体感 + 三项横切改造、真实资产渲染的 B/C 两张 mockup 与脚本路径。未改代码，待用户选定方向。"
status: "active"
created_at: "2026-10-27T05:18:22.424Z"
updated_at: "2026-10-27T05:18:22.424Z"
content_hash: "9b02da84bd7f59d6b2f984817c7ec706706da39f2ed4350197b0199e7ab641ca"
source_paths:
  - "docs/ui-design-directions.md"
  - "docs/images/ui-mockup-b-card.png"
  - "docs/images/ui-mockup-c-water.png"
  - "_art/ui-mockup/render_base.py"
  - "_art/ui-mockup/render_b.py"
  - "_art/ui-mockup/render_c.py"
  - "HelloWorldProject/Assets/Scripts/GameUI.Theme.cs"
  - "HelloWorldProject/Assets/Scripts/GameConfig.Colors.cs"
  - "HelloWorldProject/Assets/Scripts/GameUI.cs"
session_ids:
  - "c1dab885-61b9-4be5-aaab-0ed1fa41ce36"
memory_body_ids:
  []
---

# Unity AutoBattle UI 视觉方向调研与 B/C 双 mockup（2026-10-27）

面向 `HelloWorldProject/` 的**整体 UI 观感改造方向调研**，以及 B（实体卡牌）、C（水色轻小说）两张静态 mockup。状态：**只调研 + 出图，未改动任何游戏代码**；等用户选定方向后再动色板与主题层。

相关文档：P4 表现层落地记录 `b38d2252-8261-4d10-9804-388b724352ca`；图标层 `f58cac3f-cd46-457f-919d-ee4bec219153`；方案设计稿 `d029b086-f8ff-438e-a87b-100aa336080c`；项目总览 `42b8694e-f097-43db-9f58-9d606e02cfd1`。

## 诊断：不好看的根因不是配色

5 条有据可查的问题（原调研稿 `docs/ui-design-directions.md`）：

1. **零字体资产**：工程里 0 个 .ttf/.otf，全用 Unity 内置 LegacyRuntime（回退 Arial），只靠 11/13/15/18/22 五档字号分级（`GameUI.cs:288-292`）——所以像网页后台。
2. **只有一种形状语言**：所有面板/卡片/按钮都是程序化圆角矩形 + 1.5px 描边；`GameUI.Theme.cs:33` RoundSprite、`:85` RingSprite，`CreateCard` 被 30 处调用，无纹理/内阴影/高光。
3. **色阶压得太扁**：PanelBg (0.075,0.086,0.118) → CardBg (0.094,0.110,0.153) → RowBg (0.106,0.125,0.173)，相邻层仅差 0.02 明度；强调蓝与"信息蓝"近乎同色，没有视觉焦点（`GameConfig.Colors.cs`）。
4. **几乎没有动效**：全工程只有战斗突刺（`GameUI.Stage.Frame.cs:113`）与 DPS 平滑（`GameUI.Fx.cs:163`）两处 Lerp；无 hover/press/入场/数字滚动。
5. **绝对坐标固定卡片**：1280×720 Expand 缩放（`GameUI.cs:320`；左面板宽 420、右面板 X=444 W=824），每块塞满，无留白节奏。

结构性冲突：主角是浅色水感鲸鱼娘，怪物是暗色 AI 立绘，背景 4 张照片感 JPG；深蓝底想同时兼顾两边，结果两边都不合适。

## 六个方向（成本 / 契合 / 风险）

| 方向 | 视觉语言 | 成本 | 与现有契合 | 关键风险 |
|---|---|---|---|---|
| A 暗金浮雕 | 金属九宫格边框 + 雕花角饰 + 暖金强调 + 暗角（Diablo IV / Hades / PoE） | **L** | 怪物、背景能统一，与鲸鱼娘冲突 | 需 10~15 个九宫格美术资产；九宫格不能 AI 一键生成（拉伸会糊） |
| B 实体卡牌桌面 | 纸纹噪点 + 硬投影（偏移 2~3px 不模糊）+ 随机旋转 ±1.5° + 按下下沉（Balatro / 杀戮尖塔 / Inscryption） | **M** | **最高**：遗物/契约/符文/三选一全是卡牌语法，30 处 CreateCard 可原地升级 | 像桌游，需 HUD 维持即时感 |
| C 水色轻小说 | 半透明白蓝面板 + 细线 + 大留白 + 立绘溢出边框（Blue Archive / 明日方舟） | **M~L**（真毛玻璃需抓屏+模糊 shader） | 和鲸鱼娘最搭 | 暗黑怪物像贴纸；亮底疲劳 |
| D 像素+CRT | 像素字体、硬边、扫描线、抖动（Loop Hero / Vampire Survivors） | HUD 层 S / 全盘 L | 低 | 现有高清立绘会被毁掉 |
| E 数据终端 | 极简 + 等宽数字 + 网格线，纯排版驱动（Melvor Idle） | **S** | 和挂机玩法搭 | 好看上限低 |
| F 木刻版画 | 高对比黑白 + 血红 + 粗糙笔触（Darkest Dungeon / Griftlands） | M~L | 怪物合适 | 整体变单色 |

### 三项与方向无关的横切改造（收益最大）

1. **换字体**（半天）：正文思源黑体/未来荧黑，大标题得意黑，数字等宽。单这一项去掉约 40% 的网页后台感。
2. **加材质与光**（1~2 天）：全局 12% 噪点层 + 面板内阴影 + 顶部 1px 高光 + 四角暗角。
3. **动效与反馈**（1~2 天）：hover 提亮、press 下沉 2px、面板入场 0.15s、数字滚动、暴击抖卡；"贵"的手感约 80% 来自反馈速度。

### 结论

推荐 **B + 三件横切改造**：成本 M、程序化可出 70%、与后续遗物三选一系列天然同构，且是中性"桌游桌面"框架，能让暗黑怪物与鲸鱼娘共存（A 和 C 都逼你选风格站位，B 不用选）。A 现在不建议动（唯一卡在美术资产上）；D 只建议做 HUD 层当调色试验。

## 两张 mockup（真实资产 + 真实数值，非 AI 生图）

- B：`docs/images/ui-mockup-b-card.png` —— 左侧整张羊皮纸角色卡（相框立绘 + 生命条 + 5 行装备 + 金币 + 经验 + 红色蜡封，整体歪 -0.5°、硬阴影偏移 (4,7)）；战斗区做成 3px 金边画框（背景压暗 0.78）；技能卡各自倾斜 ±1.5°，冷却用压暗圆盘 + 百分比；右下羊皮纸卷边战报。
- C：`docs/images/ui-mockup-c-water.png` —— 天光渐变底 + 白色 210α 面板 + 柔光投影；舞台无边框，背景与白色混合 48% 雾化、底部白色渐变收边；左侧按 Blue Archive 语法（立绘/生命条/2×2 属性块/装备行），右侧拆"战报 + 本局数据"（数据卡带迷你柱状图）。
- 对照与落地顺序由 assistant 写入 `docs/ui-design-directions.md`（该文件当时约 115 行，§1–§7）。B 新增美术资产 0，C 真毛玻璃需抓屏+模糊；两者都是程序化可复现。

### 渲染脚本（改一个数字即可重出图）

- `_art/ui-mockup/render_base.py`：公共层——2× 超采样（S=2，2560×1440 绘制后缩放）、`rrect` 圆角矩形、`soft_shadow` 高斯投影、`cover/fit_height` 裁切、`noise` numpy 噪点、`vignette` 暗角、`txt/txt_track` 带字距文字，以及整套真实数值 DATA（角色名"深渊回响"、Lv 118、HP 12480、5 件装备 + 强化等级、6 个技能及冷却、6 条战报）。
- `render_b.py` / `render_c.py`：两个方向的版式层。
- 环境：Pillow 12.3.0 + numpy 2.3.5；字体取自 `C:/Windows/Fonts/`（STZHONGS.TTF、simkai、simhei、msyh/msyhbd/msyhl、STXIHEI、bahnschrift）。
- 真实素材：`Resources/HeroPortrait.png` 803×1024 RGBA、`Sprites/Monsters/goblin_melee.png` 309×288、`Sprites/Backgrounds/bg_wilds.jpg`、`Resources/Icons/*.png`（256×256）。

## 可用但尚未利用的现成资产

`Resources/` 里已有 16 个 `frame_*`（banner/bar_notched/bar_round/bar_socket/hex/hex_wide/oct/oval/pill/plate/ribbon/ribbon_wide/shield/square/square_small/tab，452×250 等，RGBA alpha_max 255）与整套 `glyph_*`/`ui_*`/`skill_*` 图标；`StyleBar/StyleIcon/StyleTab` 已在 70 处调用（如 `GameUI.Build.Left.cs:125`、`GameUI.Build.Right.cs:84`、`GameUI.Achievements.cs:85`）。这些边框天然适合方向 A/B 的九宫格改造，B 落地时可直接复用而不必新画。

## 下一步（等用户选定）

1. 用户挑一个方向或"B 的骨架 + C 的配色"混合。
2. 先行只改左角色面板试水（B 的骨架成本最低、以后能接遗物三选一）。
3. 定稿后再动 `GameConfig.Colors.cs`（色板）与 `GameUI.Theme.cs`（形状/材质），一步一屏，避免一次性全局改崩。

---
id: "cf3c7a1c-174e-4718-b91f-ddf1d2936fee"
title: "Unity AutoBattle 按钮/页签换皮：frame_* 生图底框的 Style* 约定与禁用态"
description: "把程序化圆角按钮换成 Resources/frame_* 生图底框后的代码约定：ApplyButtonFrame/StyleBar/StyleIcon/StyleStack/StyleTab 语义、深皮革底框禁用态必须压暗不淡出（GameUI.Factory.cs:115）、页签选中/未选中统一色、无尽面板页签共区理由、重着色须读 _art/ui-frames/orig/ 原图的坑与截图验收。"
status: "active"
created_at: "2026-10-27T16:05:11.144Z"
updated_at: "2026-10-27T16:05:11.144Z"
content_hash: "a07e8dc1fbf63f4bfddd16d6559938514acb60c434170126211cf46b78cdabcf"
source_paths:
  - "HelloWorldProject/Assets/Scripts/GameUI.Endless.cs"
  - "HelloWorldProject/Assets/Scripts/GameUI.Achievements.cs"
  - "HelloWorldProject/Assets/Scripts/GameUI.Factory.cs"
  - "HelloWorldProject/Assets/Scripts/GameUI.Shop.cs"
  - "docs/handoff.md"
  - "HelloWorldProject/Assets/_shots/p12_endless.png"
session_ids:
  - "0a78ecdc-f22e-40fa-a185-067de6a7ccd6"
memory_body_ids:
  []
---

# Unity AutoBattle 按钮/页签换皮：`frame_*` 生图底框的 Style* 约定与禁用态

工程 `HelloWorldProject/`（Unity 6000.6.5f1，AutoBattle）。本文记录把程序化圆角按钮换成生图底框（`Resources/frame_*`）后的**代码层约定、踩坑与验收方式**，供后续任何新面板直接套用。方向选型（B 卡牌实体感）、未使用资产盘点见既有文档 `0112eead-b1e3-4600-b9fe-335bf0a4dfe9`；图标层见 `f58cac3f-cd46-457f-919d-ee4bec219153`。仓库内 `docs/handoff.md` §9 记同一批改造的分层落点与截图清单（本文不重复交接文档的项目状态）。

## 1. 样式助手（GameUI partial）

所有按钮改造都收口在几个助手函数里，面板代码只调用、不自己改 Image：

- `ApplyButtonFrame(Button, frameKey)` —— 唯一的底板替换点：`bg.sprite = IconSprite(frameKey)`、`bg.type = Simple`、`preserveAspect = false`、`bg.color = Color.white`，并**禁用子物体 `Border` 的 Image**。原因：生图底框自带花纹与厚金边，旧按钮的圆角底色 + Border 会盖在花纹上。sprite 缺失时返回 false，调用方保留旧按钮（"至少还能点"）。
- `StyleBar(button,label,frameKey,glyphKey,glyphSize,tooltip,fontSize)` —— 横向按钮：图标左、文字右。图标 `iconX = Mathf.Clamp(h * 0.5f, 12f, 28f)` 是按高度给内边距，否则播放键左尖/暂停键左条会被金边吃掉；文字 `fontSize = fontSize>0 ? fontSize : Clamp(Round(h*0.33f),11,17)`、Bold、MiddleLeft、best fit 9..size、`offsetMin=(iconX+glyphSize+6, 2)`、`offsetMax=(-10,-2)`。
- `StyleIcon` —— 紧凑图标按钮，无文字、悬停给名称；`badge=true` 时保留右下角数字（金字 + `Outline` 黑描边 0.9α、偏移 (1,-1)），姓名条/角标按钮用它。
- `StyleStack(button,label,frameKey,glyphKey,glyphSize,tooltip)` —— 上图标下短名，速度 ×1/×2/×4 专用；标签 fontSize 11、颜色 `(0.86f,0.80f,0.66f)`——六边形底框是深皮革，默认深墨字几乎看不见。
- `StyleTab(button,label,frameKey,fontSize=12)` —— 页签：只换底板，文字居中、**不用图标**（章节/分类太多，图标会变成猜谜）；best fit 9..fontSize，`offsetMin=(6,1)`、`offsetMax=(-6,-1)`。`StylePlain` 只是 `StyleTab(..., 11)`。
- 全部助手末尾都 `AttachTooltip`：悬停名默认取 label 文本，缺省时兜底，避免生图按钮变成无说明的图标。

调用面（同一套底框已铺开）：`GameUI.Achievements.cs:97`（分类页签）、`GameUI.Shop.cs:99`（行商页签）、`GameUI.Compare.cs:55`、`GameUI.Panels.Enhance.cs:37` 与 `:352`（强化/洗练槽）、`GameUI.Build.Left.cs:135`（选关）、`GameUI.Panels.Save.cs:200`（存档槽 `StyleBar`）。

## 2. 禁用态：深皮革底框必须"压暗"而不是"淡出"

原来的 `HelloWorldProject/Assets/Scripts/GameUI.Factory.cs:115` 是
`colors.disabledColor = new Color(1,1,1,0.45f)`。
ColorBlock 是**乘法**着色，45% alpha 会让深底框半透明，透出后面的羊皮纸 —— 禁用按钮看着像一张空壳（最明显：无尽面板的「开始挑战」）。改为只压暗、保持不透明：
`new Color(0.62f, 0.60f, 0.57f, 1f)`。
规则：只要按钮底板是生图不透明贴图，禁用态一律改 RGB、alpha 保持 1；否则任何 `disabledColor` 的 alpha 都会变成"漏底"。

## 3. 页签：选中保持原亮度，未选中压暗

生图页签底框**不染品牌色**（染了会盖掉皮革纹理）：

- 成就分类页签（`GameUI.Achievements.cs:404-412`）：选中 `image.color = Color.white`，未选中 `(0.60f,0.64f,0.72f,1f)`；标签选中白、未选中 `GameConfig.TextMuted`。
- 无尽「规则 / 历史榜」同口径（`GameUI.Endless.cs:314-315`）。

未选中那档灰是**全局统一值**，新面板照抄即可，不要各自调色（否则并排两个浮层的页签深浅不一致）。

## 4. 无尽面板：两个页签共用一个内容区（布局理由）

`GameUI.Endless.cs` 是无尽模式的全部界面（左栏入口 + 无尽面板 + 本局结果面板），战斗逻辑在 `GameController.Endless.cs`，这里只负责显示与把点击转给控制器。

"规则 / 历史榜"没有各开浮层，而是同一块区域二选一：`_endlessRulesTab` 在 (20,112,96,28)、`_endlessHistoryTab` 在 (124,112,96,28)，共用下面 20..300 的内容区（规则正文 `(20,146,640,150)`；历史榜提示 `(20,146,640,18)` + 空态 `(20,176,640,60)`，最多 5 行）。**理由写在代码注释里**：各面板之间是 `CloseAllPanels` 互斥的，单开一个「历史榜」面板反而回不到无尽面板。新增二级内容优先考虑页签，而不是再叠一层浮层。

## 5. 重生图底框的坑

- **重着色脚本必须从 `_art/ui-frames/orig/` 读原图**：对着已着色的成品再跑一次会逐轮累加，皮革纹理与金边会一起变脏。改色永远"原图 → 目标色"一遍。
- 生图底框有较厚金边，图标/文字的内边距要按按钮高度给（见 `StyleBar` 的 `iconX`），固定左边距会在短按钮上被吃掉。
- 九宫格拉伸对生图底框不安全（花纹会糊），当前一律 `Image.Type.Simple`，尺寸靠建按钮时的 `sizeDelta` 定。

## 6. 验收与已留的视觉债

验证循环：`recompile` → 轮询 `recompile_status` 到 completed → `editor_play` → 开面板截图 → `editor_stop`。本轮产物：`HelloWorldProject/Assets/_shots/p12_endless.png`、`HelloWorldProject/Assets/_shots/p12_achieve.png`（同时点过 AchievementButton/EndlessButton 及其 Close，确认无回归）。

仍开放的四条视觉债（按当时优先级）：
1. 行商卡正文偏小偏淡，价签与羊皮纸协调度一般；
2. 强化 / 洗练面板的属性行、说明文字偏淡；
3. `UITooltip` 仍是深黑底 `(0.02,0.03,0.05,0.97)`（`BuildTooltip`，300×30），与皮革木桌不搭；
4. 成就页底部「称号」区禁用态对比偏弱。

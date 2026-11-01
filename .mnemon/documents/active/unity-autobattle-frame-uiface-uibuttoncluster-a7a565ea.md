---
id: "a7a565ea-f05b-4e42-8f7b-ad7d532344e3"
title: "Unity AutoBattle 按钮文字居中：frame_* 内芯偏移 UiFace/UiButtonCluster 与布局实测校准"
description: "v14 布局修正：设计坐标与屏幕约 12px 偏移的实测结论、frame_* 内芯偏移表 UiFace 与 UiButtonCluster 聚类居中（含降字号规则）、StyleTab/StyleBar 补偿式、技能栏/装备入口/页签具体数值，以及 eval_file 探针验证流程与 Assets 内探针脚本 CS8802 坑。"
status: "active"
created_at: "2026-10-27T16:24:54.631Z"
updated_at: "2026-10-27T16:24:54.631Z"
content_hash: "ad6b77a634468f484fbc8a83fcce877695846cf9f35fc473da836997fb7aa29e"
source_paths:
  - "HelloWorldProject/Assets/Scripts/GameUI.ButtonCluster.cs"
  - "HelloWorldProject/Assets/Scripts/GameUI.Build.Left.cs"
  - "HelloWorldProject/Assets/Scripts/GameUI.Buttons.cs"
session_ids:
  - "a6c77064-f760-4819-ad36-744ead90d230"
memory_body_ids:
  []
---

# Unity AutoBattle 按钮文字居中：`frame_*` 内芯偏移 `UiFace`/`UiButtonCluster` 与布局实测校准

工程 `HelloWorldProject/`（Universal 2D，Editor 6000.6.5f1，AutoBattle，1920×1080 Canvas）。本文记录 v14 一轮「四条布局问题」的根因、新机制与实测校验收敛方式。**这是对既有样式约定的修正与延伸**：按钮/页签换皮当时的 `Style*` 语义见 `cf3c7a1c-174e-4718-b91f-ddf1d2936fee`（该文记录的 `StyleTab offsetMin=(6,1)/offsetMax=(-6,-1)`、`StyleBar MiddleLeft` 硬编码偏移已被本轮内芯补偿取代）；图标键映射与 `CreateIconSlot` 见 `f58cac3f-cd46-457f-919d-ee4bec219153`。

## 1. 核心发现（测量结论）

1. **设计坐标 ≠ 屏幕坐标。** 用运行时 `GetWorldCorners` 实测，代码里的设计坐标映射到屏幕存在约 **12px** 纵向偏移。所以「按设计值算应该留 11px」这类推算不可信：上一版技能栏 `rowY 618 / rowStep 41` 算出来第二行底边距面板底 11px，实测只剩 1px，看起来仍贴边框。**任何边距结论都必须跑运行时探针复核。**
2. **按钮文字不居中（偏左/偏下）有两个独立根因：**
   - `StyleBar` 一直用 `MiddleLeft` 把文字顶在图标右侧，而图标固定在左侧 → 宽按钮文字明显偏左（重置按钮 148 宽时偏 **26px**）。
   - 底框图 PNG 的**内芯（皮革面）中心比图片矩形中心低约 2.5%、略右** → 纯矩形居中必然偏低。
3. **页签文字出界**：150×24 高度余量不足，叠加内芯偏移后文字顶到/越过皮革面边界。

## 2. 新机制：`GameUI.ButtonCluster.cs`

- `UiFace`：对 16 张 `Resources/frame_*.png` **逐张量出**内芯偏移并固化成表。不靠猜、不靠统一常量——每张底框的皮革面位置都不同。
- `UiButtonCluster`：在 `LateUpdate` 里把「图标 + 间距 + 文字」当成**一个整体**在皮革面内居中（而不是各自在图片矩形里居中）；文字过长时逐级降字号，**最小 9**；字号上限按按钮高度 `h*0.33` 自动确定。
- `StyleBar` 不再硬编码 offset；`StyleIcon`、`StyleStack`、`StyleTab` 各自加内芯补偿。页签补偿式：`offsetMin=(6+face.x, 1-face.y)`、`offsetMax=(-6+face.x, -1-face.y)`，实测墨迹中心相对皮革面中心偏差 **(+0.7, +0.7)** 设计像素。
- 删除 `Build.Right.cs` 中开始/暂停按钮手工固定的 `anchoredPosition=(60,-13)` 与 `offsetMin=(96,2)`——位置交给聚类居中。
- 结果：重置按钮文字偏左 26px → **6px**。剩余 6px 是 `glyph_reset` PNG 本身左侧透明边距，**不是布局问题**；要消除必须让图标按墨迹对齐，而不是按图片矩形对齐。

## 3. 本轮布局数值（`GameUI.Build.Left.cs`）

- `BuildSkillBar`：`slotW 126 / slotH 38 / stepX 132 / rowStep 41` 不变，`rowY 618 → 606`。6 槽 = 3 列（主动/被动/触发）× 2 行，槽内自带类别标签，不设列头。
- `_skillBarRoot`（引导高亮定位锚，不接收 raycast）：`16,614,388,86 → 16,600,388,94`，与技能栏同步上移收高。
- 改后实测：第二行底边距面板底约 **11 设计像素**，上方与雷印/岩层资源条留 **10px**。
- 装备分隔线 `16,294,274 → 16,294,250`（避开右侧按钮列）；Compare / Enhance / Reforge 入口钮 `34×34@y296 → 30×30@y290`（x = 278 / 316 / 354），底边到 320，装备卡顶在 322 → 留出可见缝隙。
- 页签 `150×24@y78 → 150×28@y76`。

## 4. 可复用的验证流程

- **探针脚本放 Assets 之外**：`HelloWorldProject/_shots/probe/*.cs`，用 `unity command eval_file -- --file <path>` 执行。
- **点按钮**：遍历 `ui.GetComponentsInChildren<UnityEngine.UI.Button>(true)` 按 `b.name` 匹配后 `b.onClick.Invoke()`，返回 `"ok"`/`"miss"`（比截图点坐标稳）。
- **量位置**：`GetWorldCorners` 取屏幕矩形；量文字居中则截图后按亮度/颜色阈值取墨迹质心，与按钮 rect 中心、label rect 中心分别比对（三者一起看才能区分「文字在 label 内偏移」还是「label 相对按钮偏移」）。
- **看编译与运行**：`editor_stop` → `AssetDatabase.Refresh()` → `recompile` → 轮询 `recompile_status` 到非 compiling → `editor_play` → `capture_game_view --save_path Assets/_shots/xxx.png --width 1920 --height 1080` → `console` 确认 `consoleErrors: 0`。

### 两个坑

- **Assets 下任何 .cs 都参与编译**：残留的 `Assets/_shots/ck102.cs … ck105.cs` 带顶层语句，累积触发 `CS8802: Only one compilation unit can have top-level statements`，进而 `All compiler errors have to be fixed before you can enter playmode!`（当轮 console 报 832 个 error，含大量历史残留）。探针要么放 Assets 外，要么每次用完删除。
- 开发中间态 `UiFace` 返回类型写错时曾报 `CS1061: 'RectTransform' does not contain a definition for 'sprite'` 与 `CS0029: Cannot implicitly convert type 'UnityEngine.UI.Image' to 'UnityEngine.RectTransform'`；编译错误会**阻塞进入 playmode**，改完必须重新 `recompile`，不要相信 `up_to_date`。

## 5. 遗留（未验证 TODO）

- 方向 B 的四条打磨项仍未动：行商卡正文偏淡、强化/洗练属性行偏淡、UITooltip 仍是深黑底（与羊皮纸不搭）、成就称号禁用态对比不足。
- ResetButton 剩余 6px 偏差需改为按图标墨迹对齐才能消除。

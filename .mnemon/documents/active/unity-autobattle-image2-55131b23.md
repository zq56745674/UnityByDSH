---
id: "55131b23-eefe-4397-ac2f-9ab9ad49f980"
title: "Unity AutoBattle 立绘待机动画与眨眼帧（image2 图生图对齐流程）"
description: "GameUI.Portrait 待机呼吸/浮动/随机眨眼的实现要点（unscaledDeltaTime、不改布局基准），以及用 image2 gpt-image-2 图生图生成闭眼帧、按 alpha 包围盒缩放对齐到 803×1024 画布（IoU 0.995）并做 Play 模式验证的可复用流程。"
status: "active"
created_at: "2026-10-18T16:38:54.272Z"
updated_at: "2026-10-18T16:38:54.272Z"
content_hash: "8332bf409ddba6999c9b46d230e3db0805bf8a02bd347a7843815830e9c0e5b7"
source_paths:
  - "HelloWorldProject/Assets/Scripts/GameUI.Portrait.cs"
  - "HelloWorldProject/Assets/Scripts/GameUI.cs"
  - "HelloWorldProject/Assets/Scripts/GameUI.Core.cs"
  - "HelloWorldProject/Assets/Resources/HeroPortraitBlink.png"
  - "HelloWorldProject/Assets/Screenshots/portrait-idle.png"
  - "HelloWorldProject/Assets/Screenshots/portrait-blink.png"
  - "README.md"
session_ids:
  - "48bb6df8-07f5-4a63-a556-96d89db5a6a0"
memory_body_ids:
  []
---

# Unity AutoBattle 立绘待机动画与眨眼帧（image2 图生图对齐流程）

工程 `HelloWorldProject/`（Unity 6000.6.5f1）。目标：给已接入的左面板主角立绘加待机呼吸/浮动/眨眼，且**不改变原有画风与构图**。已完成并在 Play 模式验证。

立绘本体（鲸鱼娘）的来源、生成与「以左面板背景水印接入」的决策见既有文档 `5bc504d7-b419-45c9-b38d-e9f8ca91893e`；项目全局见 `42b8694e-f097-43db-9f58-9d606e02cfd1`。本文只记录待机动画与眨眼帧生产/对齐的可复用流程。

## 源文件

- `HelloWorldProject/Assets/Scripts/GameUI.Portrait.cs`（153 行）——新增：呼吸缩放、轻微上下浮动、左右摇摆、随机眨眼（偶尔连眨两下）的状态机与帧切换。
- `HelloWorldProject/Assets/Scripts/GameUI.cs`——`BuildHeroPortrait` 的接入点未改，立绘、背光、地灯位置保持原样。
- `HelloWorldProject/Assets/Scripts/GameUI.Core.cs`——`Update` 每帧推进动画。
- `HelloWorldProject/Assets/Resources/HeroPortraitBlink.png`——闭眼帧，803×1024 透明底。
- `HelloWorldProject/Assets/Screenshots/portrait-idle.png`、`HelloWorldProject/Assets/Screenshots/portrait-blink.png`——游戏内截图（左睁眼、右眨眼）。
- `README.md`——补了立绘待机说明与两张截图。

## 动画实现要点

- 用 `Time.unscaledDeltaTime` 推进，而不是 `deltaTime`：战斗速度可调到 x2/x4，用缩放时间会让立绘跟着加速抽动。这是本次的关键 rationale。
- 眨眼是随机间隔 + 短保持（约 0.11 秒闭眼），并带低概率连眨两次；用帧切换（open/blink Sprite）而非序列帧。
- 不改变 UGUI 层级和 RectTransform 基准位置：动画只叠加局部 scale/anchoredPosition/rotation 的小幅偏移，避免与左面板既有 y 占位（经验条、强化按钮、金币/统计行）冲突。

## 眨眼帧生产：image2 图生图 + alpha 包围盒对齐

1. 参考图用现有睁眼立绘 `HeroPortrait.png`，提示词只要求「同一角色、同一构图、闭眼」，避免重绘角色。
2. 走 `dsh-image-gen` 的 `openai-compat` 通道：`gpt-image-2`，baseURL `https://d1api.xin/v1`，`quality=high`，图生图。模型输出 **912×1136**，与目标画布尺寸不同，必须自行对齐。
3. 用 alpha 包围盒（阈值 128）做几何对齐：
   - 原图（803×1024）bbox = (12, 12, 791, 1012)；新图 bbox = (13, 12, 878, 1123)。
   - 按 bbox 宽高求缩放 ≈ (0.90058, 0.90009)，再求平移偏移 ≈ (0.296, 1.196)，缩放后 821×1023。
   - 贴回 803×1024 透明画布后，轮廓 IoU = **0.9945（约 0.995）**，最终 bbox = (12, 12, 790, 1012)，与睁眼帧几乎完全重合。
4. 在深色 UI 背景上检查头发与尾巴边缘，确认没有白边/半透明脏边后再替换资源。
5. 覆盖 `HeroPortraitBlink.png` 后 `AssetDatabase.Refresh(ForceUpdate)`，用 `eval` 断言 Sprite 为 `803x1024 name=HeroPortraitBlink`。

判定标准：轮廓 IoU ≥ ~0.99 且边缘无白边，才算「只改闭眼、不换角色」；只看整图相似度不够。

## 验证流程（Unity 插件）

- 插件目标：`127.0.0.1:7800`，项目 `E:\pytest\unitydsh1\HelloWorldProject`。
- `editor_play` → 反射把 `_portraitImage.sprite` 强制设为 blink 帧、把 `_portraitBlinkHold` 拉长，`capture_game_view` 1280×720 截图；再切回 open 帧截第二张。
- `console_status`：`compilationFailed=false`、`consoleErrors=0` 才算通过。
- 测试完把 `autobattle.guide.done` 用 `PlayerPrefs.Save()` 恢复为 `false`，否则下次启动会跳过新手引导。
- `editor_stop` 退出 Play；本轮改动未提交 git。

## 通道状态与备选

- 用户充值后 image2（`gpt-image-2`）恢复可用：文生图与图生图均实测成功，1024×1024 `quality=low`；此前 403 `insufficient_user_quota` 已消失。
- 额度用完时的备选：Seedream（`doubao-seedream-5-0-pro-260628`，`seedreamBackground: transparent`）图生图也能只改闭眼；第一版眨眼帧就是 Seedream 产出，后被 image2 版替换。两条链路都要求输出透明底 PNG 并做同样的 bbox 对齐。

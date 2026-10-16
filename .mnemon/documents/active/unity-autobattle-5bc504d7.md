---
id: "5bc504d7-b419-45c9-b38d-e9f8ca91893e"
title: "Unity AutoBattle 主角立绘：代码生成鲸鱼娘与「背景水印」接入"
description: "HelloWorldProject 原创主角立绘的生产与接入记录：Pillow 代码生成（4× 超采样、双版本输出）、为何以左面板背景水印而非独立立绘框接入、Unity PNG 导入 Sprite 的坑、7 轮造型迭代的失败原因，以及待用户决策的正式立绘框两方案。"
status: "active"
created_at: "2026-10-15T12:23:31.343Z"
updated_at: "2026-10-15T12:23:31.343Z"
content_hash: "41330ab9d57332ac646e0ced9fbfce90f4ba843fcf67b7eef2e12f429433fbce"
source_paths:
  - "_art/make_hero.py"
  - "HelloWorldProject/Assets/Scripts/GameUI.cs"
  - "HelloWorldProject/Assets/Resources/HeroPortrait.png"
  - "HelloWorldProject/Assets/Resources/HeroPortraitFade.png"
  - "HelloWorldProject/Assets/Screenshots/hero.png"
session_ids:
  - "2e5baa72-31d6-4660-a1e7-d70bb41be6b6"
memory_body_ids:
  []
---

# Unity AutoBattle 主角立绘：代码生成鲸鱼娘与背景水印接入

工程 `HelloWorldProject/`（Universal 2D，Editor 6000.6.5f1）。造型已定稿并接入游戏，Play 模式实测通过。

相关既有文档（勿重复其内容）：`7c3b8ebf-3e86-4b45-8f70-ad53ec9188c5`（巅峰/技能系统与 UI 布局常量）、`7035e108-9fb2-4a33-8f12-8282b1db79d0`（GameUI 可读性层与坐标/刷新纪律）。

## 源文件

- `_art/make_hero.py`——Pillow 纯代码生成立绘。画布 2048×2048 画完再缩到 512（4× 超采样拿抗锯齿边缘）；每个部件一个函数，改造型只改对应函数。一次运行输出两版 PNG。
- `_art/hero_portrait.png`、`_art/hero_portrait_fade.png`——生成产物（135361 / 119633 字节）。
- `HelloWorldProject/Assets/Resources/HeroPortrait.png`（119633 字节）、`HelloWorldProject/Assets/Resources/HeroPortraitFade.png`（135361 字节）——放进 `Resources/` 才能运行时加载；`Assets/Art/` 目录已不再保留。
- `HelloWorldProject/Assets/Scripts/GameUI.cs`——通过 `Resources.Load<Sprite>("HeroPortraitFade")` 加载并作为左面板底纹。
- 截图：`HelloWorldProject/Assets/Screenshots/hero.png`。

## 为什么是原创绘制

用户最初问「能不能从网上找」。核查结论：没有可直接用的素材。OpenGameArt 的 Blue Whale 是鲸鱼不是鲸鱼娘；itch.io 的 CC0 角色包（570 个结果）里没有；中文素材站的「鲸鱼 Q 版图案」是素材站自身版权内容，「免费下载」不等于可商用；而「蓝色鲸鱼娘」这个具体形象基本都指向某个有版权的角色（VTuber / 游戏角色）。故改为原创：版权干净、配色造型可控。

## 造型要点

Q 版半身像，头占全高约 55%，大眼双高光。鲸鱼元素落在四处：头顶喷水柱、身后尾鳍、下颌两侧腹部分节纹、蓝白配色。

7 轮迭代中被否决的方案与真因：

| 现象 | 真因 / 处理 |
|---|---|
| 「脸小、头盔大」 | 刘海只画到 y≈250，把脸上半部吃掉了 |
| 耳鳍变成两面小旗子 | `polygon` 出不了弧线，这类形状必须用椭圆堆 |
| 后发成了「耳机」 | 后发多出的一圈本来就够当发缘，多余块状头发是负资产 |
| 刘海两侧总支楞出尖角 | 锯齿四边形的上角比半圆在该高度的半宽更宽；按圆方程内缩，`106*sqrt(1-(46/106)²)≈95`，x 必须落在 161..351 |
| 头顶高光 | 拆两道 → 像眉毛；淡横弧 → 像胶布/秃顶。三种都试过，最后**纯色刘海**最好，高光整块删掉 |

## 接入方式（关键决策）

**以左面板背景水印接入，不做独立立绘框。** 理由：左面板内容一直排到 y≈648（标题/等级/HP/属性/经验/装备/金币/统计/技能栏共九段），`LeftPanelW = 420f`、`PanelH = 696f` 下**没有一块无文字的空区域**，独立立绘框必然占位冲突。水印方案零布局冲突。

- 位置收在**装备区右半 `x 180~404`**：属性行与装备行文字都偏左，右侧留白最多。整幅铺满会压住装备文字、拉低对比度（已实测踩到）。
- `alpha = 0.30`（居中值）。
- 因此统一 Image 创建入口是 `GameUI.cs:1429` 的 `CreateImage(parent, name, x, y, w, h, color, raycast)`。

**要用淡出版而不是原图**：半透明硬边看起来像「贴纸穿帮」，边缘径向淡出后才像刻意的底纹，所以淡出版反而能用更高 alpha。产出维护两版：`HeroPortraitFade.png` 供游戏内水印，`HeroPortrait.png`（未淡出）留作以后正式立绘框。

## Unity 导入坑

PNG 进 Unity 默认 `spriteImportMode=Multiple`，那样 `LoadAssetAtPath<Sprite>` / `Resources.Load<Sprite>` 拿不到完整贴图（表现为尺寸/精灵异常）。必须显式改成 `SpriteImportMode.Single` 再 `SaveAndReimport()`，之后才能取到完整的 512×512 Sprite。`textureType=Sprite`、`alphaIsTransparency=true`、`mipmapEnabled=false`、`filterMode=Bilinear` 是本次采用的组合。改完资源要 `AssetDatabase.Refresh(ForceUpdate)` 才生效。

## 待用户决策（未实现）

现在只是「氛围底纹」，不是真正的立绘展示。做成正式立绘框有两个方向，**都要重排布局**，用户点哪个再做：

1. **左面板加宽**：420 → 560，右面板相应让出空间。
2. **屏幕底部加一条立绘带**。

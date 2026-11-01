---
id: "6b61deb5-a043-45a0-9c99-3fd664869725"
title: "Unity AutoBattle v16：深皮革底框可读性打磨与 UiFace 内芯偏移修正"
description: "v16 UI 打磨：UiFace 偏移表含烘焙投影导致文字偏右下（Gain 0.35/Lift 0.6 修正）、深皮革底框文字色令牌表（BtnText/BtnTextDim/BtnTextGold、FrameTintDim、TextGoldDeep）、技能栏与装备区实测数值、recompile 未完成导致 click 全 MISS 的验证坑与剩余面板清单。"
status: "active"
created_at: "2026-10-27T16:46:34.153Z"
updated_at: "2026-10-27T16:46:34.153Z"
content_hash: "b1c385f7e7a2c44312767d2d6b1d3e84e7d8b5ed9474c8031efa513acfdb6448"
source_paths:
  - "HelloWorldProject/Assets/Scripts/GameConfig.Colors.cs"
  - "HelloWorldProject/Assets/Scripts/GameUI.ButtonCluster.cs"
  - "HelloWorldProject/Assets/Scripts/GameUI.Build.Left.cs"
  - "HelloWorldProject/Assets/Scripts/GameUI.Shop.cs"
  - "HelloWorldProject/Assets/Scripts/GameUI.Achievements.cs"
session_ids:
  - "193713da-17be-4f62-9c68-f955610c4fac"
memory_body_ids:
  []
---

# Unity AutoBattle v16：深皮革底框可读性打磨与 `UiFace` 内芯偏移修正

工程 `HelloWorldProject/`（Universal 2D，Editor 6000.6.5f1，1920×1080 Canvas，AutoBattle）。本文记录 v16 一轮「四条 UI 观感问题 + 对比度遗留」的根因、新令牌规则与实测数值，是 `a7a565ea-f05b-4e42-8f7b-ad7d532344e3`（v14 内芯偏移表 + `UiButtonCluster` 聚类居中 + 探针验证流程）的**修正与延伸**，也与 `cf3c7a1c-174e-4718-b91f-ddf1d2936fee`（`Style*` 换皮约定、禁用态压暗）配套；图标键映射见 `f58cac3f-cd46-457f-919d-ee4bec219153`。验证方法论直接沿用 a7a565ea §4，不在此重复。

## 1. 按钮白字偏右下：`UiFace` 的偏移表被「全量套用」

- 根因：`GameUI.ButtonCluster.cs` 里 `UiFace` 的皮革内芯偏移是**从 PNG 像素量出来的**，而 `Resources/frame_*.png` 自带烘焙投影，把内芯重心往右下拽了 **2~3 倍**；v14 把这张表当成真实内芯偏移全量套用，于是所有按钮文字整体偏右下。
- 修法：给 `UiFace` 输出加增益与上抬——`Gain = 0.35f`、`Lift = 0.6f`（`HelloWorldProject/Assets/Scripts/GameUI.ButtonCluster.cs`）。即按侧偏量打 0.35 折后整体上抬 0.6 设计像素，而不是重新逐张量表。
- 实测（墨迹质心相对按钮矩形中心，设计像素）：成就页签 `(+1.7,+1.0) → (+0.7,0.0)`；行商页签 `(+0.7,-0.3)`；关卡按钮 `(+1.3,0.0)`——全部收敛到 1px 内。
- 启示：**任何「从生图资产量出来的几何量」都要先假设它含烘焙阴影**，直接当真实内芯用会系统性偏移；用增益/上抬这种一阶校正比逐张重测更省事，且不破坏原有逐图差异。

## 2. 深皮革底框上的文字色令牌（本轮确立的硬规则）

`GameConfig.Colors.cs` 里的 `TextNormal` / `TextMuted` 是给**羊皮纸**底用的深墨色（`TextNormal (0.227,0.180,0.137)`、`TextMuted (0.396,0.333,0.255)`），画在深皮革按钮/深墨面板上对比度 ≈ 2:1，等于隐形。规则：

| 场景 | 用色 |
| --- | --- |
| 深皮革按钮正文 | `BtnText` / 次要 `BtnTextDim` / 金色数字 `BtnTextGold` |
| 按钮未选中底框着色 | 暖中性 `FrameTintDim`（替换原冷蓝 `(0.60,0.64,0.72,1)`） |
| 羊皮纸/浅底正文 | `TextNormal` / `TextMuted`（仅此） |
| ≤16px 或压在 InsetBg 深纸上的金币数字 | `TextGoldDeep (0.478,0.318,0.055)`；否则 `TextGold (0.643,0.435,0.102)` 对比度只有 2.5 |
| 禁用文字 | `BtnDisabledText (0.792,0.769,0.722)`（深底）/ `TextDisabled (0.576,0.522,0.443)`（浅底） |

- 选中态仍用 `Color.white` 底框 + `Color.white` 文字；未选中统一 `FrameTintDim` + `BtnTextDim`。
- 命中点（本轮改完）：成就页签 `GameUI.Achievements.cs:411`、`_achFilterButton`/`_achFilterLabel`（`:414-418`）、成就称号三态（佩戴=浅金 / 已解锁未佩戴=暖米白 / 未解锁=压暗——原来「已解锁未佩戴」是深墨画深底=隐形）；行商页签 `GameUI.Shop.cs:317`、服务卡正文/价签（`:148-150`、`:183`、`:343`、`:377`）；强化槽位 `GameUI.Panels.Enhance.cs:112-120`、洗练槽位 `GameUI.Panels.Reforge.cs:42-50`；对比槽位 `GameUI.Compare.cs:134-137`；无尽页签 `GameUI.Endless.cs:320-321`。
- 注意 v14 遗留：解锁判定别写成二元——转生 v2 会把保留件留在未解锁槽位，标签应写 `" · 保留件"` 而不是 `" · 未解锁"`（Enhance/Reforge/Compare 三处同款注释）。

## 3. 布局数值（`GameUI.Build.Left.cs`）

- 技能栏：`_skillBarRoot` `606/88`、`rowY 610`。实测第一行 622–658（距资源条底 614 **8px**）、第二行 663–701（距面板底 708 **7px**），上下基本对称，不再顶边框。
- 装备区标题 `y=297` + 三枚入口钮 `296,30,30`：标题墨迹中心与三钮皮革面中心只差 **1px**；分隔线在 306–308，完全在三钮上方，不再穿过按钮。
- 悬停提示：`TooltipBg` 改深墨棕 + `TooltipText` 暖米白（原是近黑底 + 深墨字，等于看不见）；弹出后用强制截图确认可读。

## 4. 验证记录（可复现流程要点）

- `recompile` 返回 `compiling… Recompilation started. Poll recompile_status until completed.`（`compiled 0`）时**必须**轮询 `recompile_status` 到非 compiling；否则 `editor_play` 会 `PLAY TIMEOUT`，随后所有 `click <Name>` 全 `MISS`、截图停在旧画面（本轮第一次跑就撞上，耗时 2970ms 全 MISS）。
- 编译完成后再 `editor_play`：同一脚本变体 `click EnhanceEntry OK / shot p16_enhance` 等全部命中（单张耗时 0.9~1.6s），说明「MISS 一片」几乎总是「还没编译完/还没进 Play」而不是坐标问题。
- 点按钮优先按 `b.name` 遍历 `GetComponentsInChildren<Button>(true)` 后 `onClick.Invoke()`，比猜坐标稳；收尾 `editor_stop` 并确认 `Exited play mode`。
- 截图落点 `Assets/_shots/v16_main.png`、`p16_enhance|reforge|compare|achieve|merchant|endless.png`。

## 5. 本轮改动文件与遗留

改：`GameConfig.Colors.cs`、`GameUI.ButtonCluster.cs`、`GameUI.Build.Left.cs`、`GameUI.Buttons.cs`、`GameUI.Achievements.cs`、`GameUI.Panels.Enhance.cs`、`GameUI.Panels.Reforge.cs`、`GameUI.Compare.cs`、`GameUI.Shop.cs`、`GameUI.Endless.cs`。**代码未提交**（等用户说「提交」）。

仍未按新令牌过一遍（方向 B 面板级打磨剩余）：技能池面板、存档卡、设置滑块；以及成就面板里「关闭」以外的危险色斜纹按钮尚未统一。

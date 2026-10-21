---
id: "d029b086-f8ff-438e-a87b-100aa336080c"
title: "Unity AutoBattle 2D 精灵与技能特效美术方案（深海金潮风格与舞台布局 A）"
description: "HelloWorldProject 的 P4 美术设计：深海金潮风格 8 条规则与配色、808×332 舞台替换日志卡的布局 A、11 族怪物/12 特效资产清单与导入规格、代码补间与特效池化、Seedream 生成与白底/黑底抠图管线踩坑、待定的 3 个决策。"
status: "active"
created_at: "2026-10-21T13:03:54.286Z"
updated_at: "2026-10-21T13:03:54.286Z"
content_hash: "30272b25b298443b5ce52fdb7b281cd4dd28340a42af7639ab83b2a2c274b055"
source_paths:
  - "docs/sprites-and-fx.md"
  - "_art/sprites/_build_concept.py"
  - "HelloWorldProject/Assets/Scripts/GameUI.cs"
  - "HelloWorldProject/Assets/Scripts/BattleLog.cs"
  - "HelloWorldProject/Assets/Scripts/GameController.cs"
session_ids:
  - "c350b57a-e139-4d7d-9a44-2e9e4e00afdf"
memory_body_ids:
  []
---

# Unity AutoBattle 2D 精灵与技能特效美术方案（深海金潮）

工程：`HelloWorldProject/`（Universal 2D，Editor 6000.6.5f1）。设计于 2026-10-21，产出概念图与规格文档，`Assets/` 当时未改动、未提交。

## 目标与风格决策

用户要求「精灵与技能特效先设计一套看下，要求风格统一，适配游戏类型」。游戏是挂机自动战斗 + 数值成长，已有 85 张「深海军蓝 + 古金边框」图标，所以新精灵必须接住同一套色与光，而不是另起一套奇幻风。

定名 **深海金潮 / Deep-Sea Gilt**。8 条规则，核心三条：

1. **2 阶赛璐璐**：只有亮/暗两色阶，缩小到 200px 内仍清晰（挂机游戏精灵显示尺寸小）。
2. **主光固定在左上 + 右侧一道冷青轮廓光**：光源一致才像同一套资产，也让暗底舞台上的剪影可分。
3. **剪影优先**：11 族怪物靠体型（胖瘦/驼背/四臂/有角）区分，不靠花纹；因为同屏尺寸下细节读不出来。

配色（全部沿用图标体系）：深海军蓝 `#101627`、古金 `#E8B84B`、冷青 `#6FE3D2`、橙红 `#F2683C`、魔法紫 `#A58CFF`。

## 舞台布局：选 A（替换战斗日志卡）

1280×720 的 UI 里没有空地，精灵只能**从日志卡换出来**：一块 808×332 的舞台正好等于现有 `LogCard`（`HelloWorldProject/Assets/Scripts/GameUI.cs:648`），替换它不动任何其它节点，这是选 A 的核心理由。

- 主角在左：高 196px、x=165；怪物在右：高 208px、x=610；地面线 y=252。
- 精英 ×1.10、Boss ×1.25 缩放；暴击飘字、旋风/爆裂特效直接叠在舞台上。
- 代价：战斗日志默认只剩底部 **2 行 ticker**；补一个「战斗日志」按钮开模态看全量，内容仍是现有日志文本，**零新数据通路**。
- 备选 B：舞台压缩到 176 高（保守，保留更多日志）；备选 C：只加怪物头像（视觉收益最小）。均记录在 `docs/sprites-and-fx.md`。

## 资产清单与规格

| 类别 | 数量 |
|---|---|
| 主角 | 1 |
| 怪物（各族 3 类型） | 11（已出：哥布林/狗头人/骷髅各 3 + 食人魔 1 + 蛛魔 1） |
| 类型武器层（近战/远程/暴击） | 3 |
| 档次光环（精英/Boss） | 2 |
| 词缀光环（吸血/护盾/剧毒） | 3 |
| 技能/战斗特效 | 12 |
| 章节背景（可选） | 4 |

- 主角/怪物源图 320px 高（舞台内显示 208/196，留缩余量）；特效单帧 256px。
- 全部 `Sprite (Single)`、PPU 100、无 mipmap、不压缩；整包资源预计 **< 4MB**。
- 12 个特效：`fx_hit`、`fx_crit`、`fx_heavy`、`fx_whirlwind`、`fx_execute`、`fx_drain`、`fx_shield`、`fx_poison`、`fx_counter`、`fx_death`、`fx_levelup`、`fx_berserk`（对应 15 技能表里的主动/触发技能，见相关文档）。

## 动画与运行时

- **不逐帧**：待机 1.5px 呼吸、出手前冲 14px、受击闪白 0.08s、死亡下沉淡出，全部代码补间；只有旋风环、升级光柱是 2 帧序列。
- 特效池化 8 个 `Image`；时长走 `Time.deltaTime`，游戏的 ×4 加速自动跟着快，无需额外适配。
- **事件驱动**：设计 `BattleVisualKind`（约 10 个触发点）由战斗系统派发，**不解析日志文字**——日志是展示层文本，解析它会把视觉永久绑死在文案格式上。

## 素材生成与抠图管线（踩坑记录）

- 生成模型：Seedream `doubao-seedream-5-0-pro-260628`（火山方舟）与 `gpt-image-2` 兼容端点；怪物表用**纯白底**、特效表用**纯黑底**，分别生成。
- **不能用 4×3 网格硬切**：第一版样张把邻居的手/光环切了进来，整版作废。改用**连通域分割 + 就近吸收小块**，11 个怪物切图尺寸各不相同（如 500×482、621×533），比网格切更干净。
- **特效必须黑底亮度抠图 + 反预乘（un-premultiply）**，才能得到正确的可叠加发光层；白底角色走白底抠图后羽化 10px，边缘 alpha 拉到 0，避免舞台暗底上出现灰边。
- 脚本：`_art/sprites/_build_concept.py`（合成舞台 mockup、切图、羽化、并排对比）；概念图输出到 `_art/sprites/mockups/`：`style_board.png`、`stage_option_a.png`、`monster_lineup.png`。

## 待用户决定（截至本次设计）

1. 布局是否选 A（舞台替换日志卡 + 日志 2 行 ticker + 模态）？A 观感最好，但日志默认不再全量常驻。
2. 生产范围先做多大？建议先「1 主角 + 3 族（哥布林/狗头人/骷髅）+ 12 特效」跑通完整链路验证手感，再补剩下 8 族；直接上 11 族也不算贵，只是返工成本高。
3. Boss 是否单独美术？现方案是同一张精灵 ×1.25 + 金色光环，成本为零但「大」得有限；单独画会让 11 张变 22 张。

## 相关文件与文档

- 规格全文（资产清单、`BattleVisualKind` 设计、工作量、三条风险、5 条验收标准）：`docs/sprites-and-fx.md`。
- 战斗日志卡与 UI 布局：`HelloWorldProject/Assets/Scripts/GameUI.cs:648` 附近；日志数据源 `HelloWorldProject/Assets/Scripts/BattleLog.cs`、`GameController.cs`（日志游标 `LogVersion`）。
- 15 技能与接入点：Mnemon 文档 `7c3b8ebf-3e86-4b45-8f70-ad53ec9188c5`（巅峰等级与技能系统）。
- 项目全局地图：Mnemon 文档 `42b8694e-f097-43db-9f58-9d606e02cfd1`（项目现状总览与接手指南）。

## 仍然有效的约束

- 概念图与中间产物在本地 `_art/sprites/`，按 `_art/fish` 的先例加入 `.gitignore`，不入库（仓库体积考虑）。
- 本次只在 `.gitignore` 加条目并新增 `docs/sprites-and-fx.md`，没有提交，也没有改动 `Assets/`。

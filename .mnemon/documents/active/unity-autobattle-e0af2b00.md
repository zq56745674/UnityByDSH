---
id: "e0af2b00-883b-47fe-ad14-f22f8f78f565"
title: "Unity AutoBattle 攻速上限、护甲穿透与怪物速度表"
description: "HelloWorldProject 三项数值改动：攻速上限改为每秒 5 下（含副词条越限漏洞修复）、护甲穿透属性（数值范围/伤害公式/80% 上限/评分权重/词条池 4→5）、全等级各类型怪物速度与攻击间隔表；另含 UI 底部裁切根因（CanvasScaler.matchWidthOrHeight）与词条折行修复。"
status: "active"
created_at: "2026-10-12T17:12:21.757Z"
updated_at: "2026-10-12T17:12:21.757Z"
content_hash: "abe4cd1775889146813534482f6cd451fbcd8ec4816bfd67cd9fa1c8d3783c5c"
source_paths:
  - "HelloWorldProject/Assets/Scripts/GameConfig.cs"
  - "HelloWorldProject/Assets/Scripts/Models.cs"
  - "HelloWorldProject/Assets/Scripts/LootSystem.cs"
  - "HelloWorldProject/Assets/Scripts/BattleSystem.cs"
  - "HelloWorldProject/Assets/Scripts/GameUI.cs"
  - "HelloWorldProject/Assets/Screenshots/armorpen.png"
session_ids:
  - "f546e5b6-b8d8-4433-af52-1df603b54ca6"
memory_body_ids:
  []
---

# Unity AutoBattle 攻速上限、护甲穿透与怪物速度表

工程：`HelloWorldProject/`（Universal 2D，Editor 6000.6.5f1）。改动日期 2026-10-12，编译无错误并在 Play 模式实测。

**相关既有文档**：技能与巅峰系统见 `7c3b8ebf-3e86-4b45-8f70-ad53ec9188c5`；技能获取改版与 100 级后胜率见 `a68e8f3d-4799-4bff-8ae2-942a12bae043`（本文改动了攻速上限，会让该文档中 Lv.110 Boss 胜率结论偏乐观，需按新上限重测）。

## 1. 攻速上限：每秒 5 下

- `GameConfig.MinAttackInterval`：`0.25s → 0.2s`（`HelloWorldProject/Assets/Scripts/GameConfig.cs:88`）。
- 同时补掉一个漏洞：主角攻速副词条在 `AttackInterval` 之后**还会再做一次除法**，原下限夹不到这一步，装备堆多后实际能突破 4 次/秒。修法是在 `BattleSystem.PlayerAttackInterval` 末尾再夹一层 `MinAttackInterval`。
- 实测：攻速 +500% → 间隔 0.200s = **5.00 次/秒**。
- 终局影响：上限放宽对高等级怪是净加强——Lv.110 精英弓箭手、Boss 弓箭手、Boss 盗贼原本卡在 0.25s，现在都撞到 0.20s，**DPS 提高 25%**。

## 2. 护甲穿透（Armor Penetration）

| 项 | 取值 |
|---|---|
| 基础范围 | 2%~10%（武器/衣服副词条） |
| 等级缩放 | 与其他副词条一致：`×(1 + 等级×0.005)`，Lv.100 时 ×1.5 → 3%~15% |
| 总上限 | 武器 + 衣服合计夹在 **80%** |
| 评分权重 | `ScoreWeightArmorPen = 800` |
| 词条池 | 4 种 → **5 种**（暴击率 / 暴击伤害 / 攻击速度 / 全属性 / 护甲穿透） |
| 传说词条数 | 仍为 4 条不重复，故单件出现穿甲概率 4/5 |

**伤害公式**（先折扣防御，再代原减伤式）：

```
有效防御 = 防御 × (1 - 穿透)
减伤 = 有效防御 / (有效防御 + 100 + 20 × 防御方等级)
```

设计理由：先扣防御再算减伤，穿透收益天然递减，后期不会失控。
实测：防御 700 / Lv.100 时，无穿透减伤 **25.0%**，50% 穿透后降到 **14.3%**。
掉落实测：300 件 Lv.100 传说武器中 **242 件**带穿甲（80.7%，符合 4/5 预期）。

UI：属性面板新增「穿透 17.1%」；装备词条行显示「穿甲 +11.3%」（`Models.cs` 的 `AffixLine` / `AppendAffix`，值为 0 的词条不显示）。

## 3. 怪物速度表

```
速度 = (10 + 0.5 × (等级 - 1)) × 类型系数 × 档次系数
攻击间隔 = max(0.2, 2 / (1 + 速度 / 20))
```

类型系数：战士 ×0.75、弓箭手 ×1.45、盗贼 ×1.00；档次系数：精英 ×1.2、Boss ×1.5（无词缀）；`MaxTierSpeedFactor = 2.0`。

普通怪等级 = 主角当前等级；精英/Boss 走梯度等级。抽点（格式：速度（间隔））：

| 类型 | Lv.1 | Lv.50 | Lv.100 | Lv.110 |
|---|---|---|---|---|
| 普通 战士 | 7.5 (1.45s) | 25.9 (0.87s) | 44.6 (0.62s) | — |
| 普通 弓箭手 | 14.5 (1.16s) | 50.0 (0.57s) | 86.3 (0.38s) | — |
| 普通 盗贼 | 10.0 (1.33s) | 34.5 (0.73s) | 59.5 (0.50s) | — |
| 精英 弓箭手 | — | 60.0 (0.50s) | 103.5 (0.32s) | 224.5 (**0.20s**) |
| 精英 盗贼 | — | 41.4 (0.65s) | 71.4 (0.44s) | 154.8 (0.23s) |
| Boss 弓箭手 | — | 75.0 (0.42s) | 129.4 (0.27s) | 280.6 (**0.20s**) |
| Boss 盗贼 | — | 51.8 (0.56s) | 89.3 (0.37s) | 193.5 (**0.20s**) |
| Boss 战士 | — | 38.8 (0.68s) | 66.9 (0.46s) | 145.1 (0.24s) |

关键发现：**Lv.110 是断崖**——`MaxTierSpeedFactor = 2.0` 直接叠在梯度档上，速度相对 Lv.100 约翻倍（精英弓箭手 103.5 → 224.5）。

## 4. UI 两个显示修复

1. **副词条折行**：词条池扩到 5 种后 4 条必然超宽，折行位置还随数值长短乱跳。改为**一行 2 条**固定布局（`Models.cs` 的 `AppendAffix` 用两个空格分隔，`AffixLine` 每 2 条换行），每件装备稳定 4 行。
2. **技能栏贴底被裁切（根因）**：`GameUI.cs:114-123` 的 `CanvasScaler.uiScaleMode = ScaleWithScreenSize`、`referenceResolution = (1280, 720)`、`matchWidthOrHeight = 0.5`。Game 视图实际是 **1101×612**，0.5 折中只缩放 0.855，696 高的面板缩完仍高于 612，底部一直被切。改为 **`matchWidthOrHeight = 1`（高度适配）** 后面板完整（实测 710 高度下的世界坐标：屏幕 1101×612、面板 10~602、槽位 58~100）。
   - 诊断方法有用：先用 `GetWorldCorners` 量 RectTransform 的实际世界坐标，再判断是布局溢出还是视口/缩放问题——当时先误判为布局溢出并压缩了底部间距，真正原因是缩放模式。

## 实现注意

- 修改 `HelloWorldProject/Assets/Scripts/GameUI.cs` 时多次出现 `Error: ReplaceFileW EIO (Win32 1175)`（文件被 Unity 占用），**重试同一写入即成功**，不是内容错误。
- 验证走 Play 模式：`AssetDatabase.Refresh(ForceUpdate)` → `editor_play` → `eval` 设置主角等级/装备/速度档 → `capture_game_view` → `editor_stop`。

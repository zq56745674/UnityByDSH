---
id: "f58cac3f-cd46-457f-919d-ee4bec219153"
title: "Unity AutoBattle 图标层：生图、圆形切图与 GameUI.Icons 接线"
description: "HelloWorldProject 徽章图标系统落地记录：Seedream 图标表 → Pillow 去底圆形切图 → Resources/Icons 导入设置 → GameUI.Icons.cs 缓存/工厂/键映射与按钮图标偏移约定，含验证方式与 README/截图同步。"
status: "active"
created_at: "2026-10-20T18:14:51.778Z"
updated_at: "2026-10-20T18:14:51.778Z"
content_hash: "95163b5f78c498e4ca257ca397ac346e0cf37d45dd255560c42ceb93e76d44df"
source_paths:
  - "HelloWorldProject/Assets/Scripts/GameUI.Icons.cs"
  - "HelloWorldProject/Assets/Resources/Icons"
  - "HelloWorldProject/Assets/Screenshots/icons-main.png"
  - "README.md"
session_ids:
  - "22331eaf-19d5-43bf-978b-c29ea34b383e"
memory_body_ids:
  []
---

# Unity AutoBattle 图标层：生图 → 去底切图 → Resources 导入 → GameUI.Icons 接线

工程 `HelloWorldProject/`（Universal 2D，Editor 6000.6.5f1，AutoBattle）。本轮给全员代码搭建的 UI 补徽章图标，**主角立绘未改动**。相关既有文档（勿重复其内容）：`5bc504d7-b419-45c9-b38d-e9f8ca91893e`（立绘生成与 Unity PNG 导入坑）、`7035e108-9fb2-4a33-8f12-8282b1db79d0`（GameUI 可读性层与坐标/刷新纪律）。

## 生产链路

1. Seedream `doubao-seedream-5-0-pro-260628` 生 3 张 **2048×2048** 图标表（UI / 技能 / 属性），统一深海军蓝底 + 古金边。
2. Pillow 去白底 + 按**圆形遮罩**切图，输出 **36 个 256×256 透明 PNG** → `HelloWorldProject/Assets/Resources/Icons/`。
3. Unity 导入设为 Sprite / **Single**、无 mipmap、Uncompressed，再 `AssetDatabase.Refresh(ForceSynchronousImport)`；`Resources.Load<Sprite>` 全部验证可读。
4. 覆盖范围：UI 入口（存档/设置/成就/无尽/开始暂停/对比/强化/洗练）、分区标题、五槽装备、15 个技能、暴击/暴伤/穿甲/锁定。

命名即键：`ui_*`（如 `ui_attack`、`ui_speed`）与 `skill_*`（如 `skill_heavy_strike`）。放在 `Resources/` 才能运行时加载；这是与立绘文档同一条导入坑（默认 `spriteImportMode=Multiple` 会让 `Resources.Load<Sprite>` 拿不到完整贴图）。

## 代码接线（`HelloWorldProject/Assets/Scripts/GameUI.Icons.cs`，112 行 partial）

- `_iconCache` 缓存 `Resources.Load` 结果——理由：**技能池面板每帧刷新 15 张**，不能每帧走资源系统。
- 工厂：`CreateIcon(parent,key,x,y,size)`（固定图标）、`CreateIconSlot(...)`（技能栏/技能卡刷新时才知装哪个技能，先建空槽）、`SetIcon(image,key)`（换图）。
- **缺图约定**：sprite 为 null 时 `img.enabled=false`（不留白方块），布局照常占位。
- `AddButtonIcon(button,label,key,size,left)` 在按钮左侧挂图标，并把 `label.rectTransform.offsetMin.x` 抬到 `Mathf.Max(min.x, left+size+4f)`——文字起始位右移，否则文字压在图标上。size 建议 12~24：标题行按钮高 24，图标超过 16 会顶到圆角。
- 键映射集中在此文件，避免各面板散落 `Resources.Load` 字符串：`SkillIconKey(int skillId)` 15 条 switch；`SlotIconKey(EquipmentSlot)` = Weapon→`ui_attack`、Armor→`ui_defense`、Helmet→`ui_health`、Boots→`ui_speed`、Accessory→`ui_armor_pen`、default→`ui_compare`。
- 开始按钮图标在 `ui_attack` / `ui_time` 间切换；**关闭类小按钮保留纯文字**——24px 高度里塞图标后太难认。

## 本轮修掉的三个视觉问题

成就标题与概要重叠、技能栏图标压冷却条、装备卡图标偏小。

## 验证方式

- 编译与日志：`compilationFailed=false`、`consoleErrors=0`、`consoleWarnings=0`。
- Play 模式逐面板看过：主界面、设置、成就、技能池、技能三选一、对比、无尽、轮回、强化、存档。
- 按钮图标状态用**反射读私有字段**验证（`typeof(GameUI).GetField("_startButtonIcon", NonPublic|Instance)` 取 Image 后看 `sprite.name`），比截图更确定；结果 `running=False icon=ui_attack`。
- Editor eval 里 `FindAnyObjectByType` 必须全限定为 `UnityEngine.Object.FindAnyObjectByType<T>()`，否则报 `The name 'FindAnyObjectByType' does not exist in the current context`。

## 资产卫生与 README

- 临时种子截图与处理脚本统一归档到 `_archive/`，`Assets/Screenshots` 只留 README 引用的 `icons-main.png` 与 `icons-battle.png`。
- `README.md` 同步：主界面截图换成 `icons-main.png`、新增「徽章图标」对比表、结构树加入 `GameUI.Icons.cs` 与 `Resources/Icons/`、脚本数与行数更新（39 个文件 / 约 14,448 行，GameUI 主文件 745 行）。

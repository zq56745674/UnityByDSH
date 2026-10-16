---
id: "bd713554-0919-4cdf-8426-557fc4ba4e4a"
title: "Unity AutoBattle 技能获取改为三选一：State.SkillChoice、pendingSkillPicks 与候选面板"
description: "HelloWorldProject 技能发放由「每 10 级巅峰随机送一个」改为「累计待选次数 + 暂停战斗的三选一面板」：新增 State.SkillChoice、pendingSkillPicks 存档字段、SkillChoiceCount 配置、GameUI 居中候选面板，含 Play 模式实测证据、跨多级累加口径与 LateGameProbe 未同步导致旧胜率表只是下界的警告。"
status: "active"
created_at: "2026-10-14T16:51:08.798Z"
updated_at: "2026-10-14T16:51:08.798Z"
content_hash: "decce064a4464fad41ae90ef177aaca7cd40cef41f24733895f1c9e032a0a711"
source_paths:
  - "HelloWorldProject/Assets/Scripts/Models.cs"
  - "HelloWorldProject/Assets/Scripts/GameController.cs"
  - "HelloWorldProject/Assets/Scripts/GameUI.cs"
  - "HelloWorldProject/Assets/Scripts/SaveSystem.cs"
  - "HelloWorldProject/Assets/Scripts/GameConfig.cs"
  - "HelloWorldProject/Assets/Resources/game_config.json"
  - "HelloWorldProject/Assets/Screenshots/skill-choice.png"
session_ids:
  - "07d8e4ad-85dd-481d-bcd3-716d7b59c7bb"
memory_body_ids:
  []
---

# Unity AutoBattle 技能三选一改版（P1 步骤 5）

工程 `HelloWorldProject/`（Universal 2D，Editor 6000.6.5f1）。执行日期 2026-10-14，编译通过并在 Play 模式逐项实测。用户指令：「先执行P1步骤 5」。

**本文取代既有文档 `a68e8f3d-4799-4bff-8ae2-942a12bae043` 中「满级后每升 10 级巅峰由系统随机获得一个技能」这一条**：技能来源从「系统随机发」改为「玩家三选一」。技能池 15 个、巅峰/技能等级公式、战斗循环接入点、槽位上限（主动/被动/触发各 2，共 6 槽）、技能 CD 跨战斗保留等结论在该文档中仍然有效，此处不重复。数值平衡见 `6231bd5c-ca9e-454e-8f30-c67546e352d7`。

## 源文件与改动

- `HelloWorldProject/Assets/Scripts/Models.cs`——`PlayerData` 新增 `pendingSkillPicks`（待选次数，进存档）；新增 `RollSkillCandidates(int count)`（从「尚未拥有」的池里抽 N 个不重复候选）与 `GrantSkill(SkillId)`（三选一的落点）。既有的 `ownedSkillMask` / `IsSkillOwned` / 六个技能槽字段沿用。
- `HelloWorldProject/Assets/Scripts/GameController.cs`——新增状态 `State.SkillChoice`；`OpenSkillChoice()` / `ChooseSkill(int)`；只读 `SkillChoices` / `IsChoosingSkill` / `PendingSkillPicks`；删除旧的 `GrantOneSkill()`。
- `HelloWorldProject/Assets/Scripts/GameUI.cs`——居中三选一面板：候选卡显示名称/类别/描述，标题显示剩余待选次数；卡面类别文案用 `KindShort((int)def.kind)`。
- `HelloWorldProject/Assets/Scripts/SaveSystem.cs`——`pendingSkillPicks` 的存读字段。
- `HelloWorldProject/Assets/Scripts/GameConfig.cs`、`GameConfigData.cs`、`Assets/Resources/game_config.json`——新增 `SkillChoiceCount = 3`（候选个数，改 2/4 无需重编译）。
- 截图：`HelloWorldProject/Assets/Screenshots/skill-choice.png`、`skill-choice-after.png`。

## 机制要点与理由

1. **发放改为记账**：击杀结算不再直接送技能，而是累加待选次数，`owed = wantSkills - OwnedSkillCount - pendingSkillPicks`。一次跨多级（连续升级）会正确累加，不会因为「一次只能选一个」而漏发。
2. **`SkillChoice` 是暂停态**：无倒计时，必须选完；候选从「尚未拥有」中抽，不会重复。
3. **呼出点在 `CurrentState = WaitingRespawn` 之后书写**：选完自然落回等待重生，不需要额外的状态回滚逻辑。
4. 配置外置（`SkillChoiceCount`）与既有 GameConfig/JSON 体系一致，方便不用重编译调节候选数。

## 实测（Play 模式）

| 场景 | 结果 |
|---|---|
| 巅峰 9 → 击杀升 10 | `state=SkillChoice 待选=1 选择中=True 候选数=3 已获得=0` |
| 选一个 | `state=WaitingRespawn 已获得=1 待选=0 触发槽=13（反击）主动槽=2 拥有反击=True 候选数=0`，选完自动入槽 |
| 待选 2 次连选 | 第 1 次后 `state=SkillChoice 待选=1 候选=3 已获得=1`（自动抽下一组）；第 2 次后 `state=WaitingRespawn 待选=0 已获得=2` |
| 存档往返 | `pendingSkillPicks=2` 原样读回（`ok=True`） |
| 编译 | `compilationFailed: false`、`consoleErrors: 0`（收尾已 `editor_stop`、`runInBackground=false`） |

过程中修掉一处编译错：`GameUI.cs(418,70): error CS1503: Argument 1: cannot convert from 'AutoBattle.SkillKind' to 'int'`（`KindShort` 收 int，需 `(int)def.kind`）。面板尺寸由初版收紧到 244 高、卡牌 150 高，避免纵向裁切。

## 遗留与注意事项

- **`LateGameProbe.Build()` 没有跟着改**，仍是 `GrantRandomSkill()` 直接随机送技能。因此文档 `a68e8f3d-4799-4bff-8ae2-942a12bae043` 里的 Lv.110 Boss 胜率表应视为**「技能乱给」的下界**，真实玩家三选一后的胜率只会更高；若要贴真实的表，需把探针改成「每次挑当前边际收益最高的候选」再重跑。
- 本工作区编辑 Unity 脚本时出现过 `Error: ReplaceFileW EIO (Win32 1175)` 与 `file changed since it was read — re-read the file, then retry`（Unity 自动 import 与外部写入竞争），重读后再写即可成功，不是内容错误。
- P1 计划里的其余步骤（2 装备强化、3 装备洗练、4 关卡章节）本日未开始。

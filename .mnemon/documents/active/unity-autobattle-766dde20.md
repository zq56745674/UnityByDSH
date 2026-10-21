---
id: "766dde20-fd68-403f-b79b-68e7b5d9f22a"
title: "游戏音效方案与授权调研（Unity AutoBattle）"
description: "HelloWorldProject 音效选型结论：CC0 素材打底 + AI 只做特色音；各音源授权/费用对比、本游戏最小音效集映射、Unity 导入规范与 LICENSES.md 记录纪律。调研完成，尚未接入。"
status: "active"
created_at: "2026-10-21T04:35:04.692Z"
updated_at: "2026-10-21T04:35:04.692Z"
content_hash: "9dc7557834d8aace244d5432fbf5e05d0ed8931ce81906202aba2c5b94d30345"
source_paths:
  - "HelloWorldProject/Assets/Scripts/GameUI.Core.cs"
session_ids:
  - "65b269ba-5897-4f84-a236-fd7fe11aed22"
memory_body_ids:
  []
---

# 游戏音效方案与授权调研（Unity AutoBattle）

面向 `HelloWorldProject/` 的音效选型结论与落地流程。**当前状态：仅调研完成，尚未接入任何音频资源/代码**（结论于对话中给出，用户未确认下一步）。

相关文档：项目总览 `42b8694e-f097-43db-9f58-9d606e02cfd1`（代码职责、路线图与进度）。

## 核心结论

**基础高频短音效铺 CC0 素材库，AI 只做少量特色音。**

理由（来自调研）：AI 生成的短音效经常尾音偏长、带音乐感、有伪影，直接放进游戏会显得"脏"；UI 点击、命中、拾取这类高频短音，现成免费包质量更稳、授权更干净。反过来，Boss 吼叫、技能特色音这类"少而想独特"的位置适合 AI。

## 候选对比（含授权事实）

| 类型 | 候选 | 授权 / 费用（调研时点） | 适合 |
|---|---|---|---|
| AI 短音效 | ElevenLabs Sound Effects（https://elevenlabs.io/sound-effects） | 免费额度 10k credits/月，但**免费档无商用授权**；Starter $6/月起含 Commercial License | UI、命中、技能、Boss 吼 |
| AI 音乐+音效 | Stable Audio（https://stableaudio.com/） | 官方知识库明确：Free 计划生成物可用于游戏/应用等**含商业用途**（需遵守 ToS/AUP）；自托管开源模型另按 Stability AI Community License（年收入 < $1M 免费商用，超额需 Enterprise） | 长音效、氛围、BGM |
| 本地开源 | Meta AudioGen / AudioCraft（https://facebookresearch.github.io/audiocraft/model_cards/AUDIOGEN_MODEL_CARD.html） | 代码 MIT，**模型权重 CC-BY-NC 4.0（非商业）** → 只能做原型/离线实验 | 原型、离线实验 |
| 8-bit 生成器 | jsfxr（https://sfxr.me/） | 免费、可导出 WAV；**不是 AI** | 复古 UI、拾取、爆炸 |
| 免费素材库 | Kenney Audio（https://kenney.nl/assets/category:Audio） | 全部 **CC0，无需署名，可商用** | UI、接口、打击、RPG，最适合打底 |
| 免费大包 | Sonniss GDC 2026（https://gdc.sonniss.com/，归档 https://sonniss.com/gameaudiogdc/） | 约 7.47GB，官方称 royalty-free & commercially usable | 打击、whoosh、环境 |
| 社区音效库 | Freesound（https://freesound.org/help/faq/#licenses） | CC0 / CC-BY / CC-BY-NC **混合，逐条看** | 真实环境、特色音 |
| 社区素材 | OpenGameArt、itch.io、Pixabay | 逐条/逐包看；Pixabay 为自有许可，通常可商用免署名，但**不得单独再分发素材** | 补充 |

注意：`sonniss.com` / `gdc.sonniss.com` 的页面在本次会话里 fetch 失败（DeepSeek fetch endpoint 返回 HTTP 404），Sonniss 的授权描述来自搜索结果摘要而非页面原文，正式采用前应再核对一次许可条款。

## 本游戏的最小音效集映射

- **UI**：Kenney UI Audio / Interface Sounds（点击、悬停、开关面板）
- **战斗**：Kenney Impact Sounds + Sonniss；暴击用"打击声 + 金属短鸣"叠一层
- **拾取/金币**：jsfxr 或 Kenney Digital Audio
- **强化/洗练**：Sonniss 的铁砧、火焰
- **升级/成就**：Kenney Music Jingles 的短 stinger
- **技能/Boss**：AI 生成 2~3 个特色音即可，不必 15 个技能全做
- **BGM**：需要时再用 Stable Audio

## 处理与导入规范（Unity）

- 统一 **44.1kHz、单声道**；裁掉静音；**2~5ms 淡入、20~80ms 淡出**；峰值压到 **-3 ~ -6 dBFS**。
- 短音效用 **Decompress On Load** + Vorbis/ADPCM，**关闭 Load In Background**；用 **AudioSource 池**播放。
- 每个动作准备 **2~3 个变体**，播放时 **pitch ±5~10%**，避免连续点击像机关枪。
- 在仓库建 `LICENSES.md` 记录**来源、作者、许可证、下载日期**。原则：**"免费下载"不等于"可商用"，CC-BY-NC 不能进商业版**。

## 已提议的下一步（未执行）

按上述最小集接一套音频（按钮、命中、暴击、拾取、升级、Boss 出场），并把音量设置加进设置面板。

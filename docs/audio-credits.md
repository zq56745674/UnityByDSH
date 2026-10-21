# 音频来源与授权

音效来自 Kenney 的 CC0 素材包；背景音乐是由项目所有者用 AI 音频工具生成的本地曲目，没有第三方权利方。

- 作者：Kenney（https://kenney.nl）
- 授权：CC0 1.0 Universal（公共领域，可商用，无署名要求）
- 下载日期：2026-10-21

## 素材包

| 包 | 地址 | 用途 |
| --- | --- | --- |
| Interface Sounds | https://kenney.nl/assets/interface-sounds | UI 点击/开合、金币、升级、成就、购买、药剂 |
| Impact Sounds | https://kenney.nl/assets/impact-sounds | 命中、暴击、Boss 出场、阵亡、强化、洗练 |

## 文件映射

| 游戏内文件 | 原始文件 | 用途 |
| --- | --- | --- |
| ui_click.ogg | interface/Audio/click_001.ogg | 按钮点击 |
| ui_open.ogg | interface/Audio/open_001.ogg | 打开面板 |
| ui_close.ogg | interface/Audio/close_001.ogg | 关闭面板 |
| hit_light.ogg | impact/Audio/impactSoft_medium_000.ogg | 普通命中 |
| crit.ogg | impact/Audio/impactMetal_heavy_000.ogg | 暴击 |
| coin.ogg | interface/Audio/pluck_001.ogg | 金币/卖出 |
| level_up.ogg | interface/Audio/confirmation_003.ogg | 升级 |
| achievement.ogg | interface/Audio/confirmation_004.ogg | 成就解锁 |
| boss_spawn.ogg | impact/Audio/impactBell_heavy_000.ogg | Boss 出场 |
| death.ogg | impact/Audio/impactPunch_heavy_004.ogg | 主角阵亡 |
| enhance.ogg | impact/Audio/impactMetal_medium_000.ogg | 强化成功 |
| reforge.ogg | impact/Audio/impactMining_000.ogg | 洗练成功 |
| buy.ogg | interface/Audio/confirmation_001.ogg | 商店购买 |
| potion.ogg | interface/Audio/pluck_002.ogg | 使用生命药剂 |

## 背景音乐

四首 BGM 全部由项目所有者用 AI 音频工具生成，没有第三方权利人——不需要署名、不需要授权，可随游戏直接分发。文件为 `HelloWorldProject/Assets/Resources/Music/` 下的 MP3，按仓库体积考虑写进 `.gitignore`；克隆仓库后需要自行放入同名文件，否则音符面板会显示「缺少文件」并跳过该曲目。

| 游戏内文件 | 曲目 | 时长 | 用途 |
| --- | --- | --- | --- |
| let_me_go.mp3 | Let me go | 3:39 | 背景音乐，默认第 1 首 |
| big_fish_cat.mp3 | 大肥鱼人是猫 (Remix) | 2:15 | 背景音乐，第 2 首 |
| embrace.mp3 | 我会去拥抱他 | 2:47 | 背景音乐，第 3 首 |
| sadness_water.mp3 | 我的悲伤是水做的（大肥鱼版） | 3:45 | 背景音乐，第 4 首 |

四首都是 AI 生成曲目，随游戏分发不需要额外授权；游戏内显示的演奏者「星落落oi」只是 `MusicCatalog.Tracks` 里的展示名，可随时改。换曲时修改 `HelloWorldProject/Assets/Scripts/GameMusic.cs` 里 `MusicCatalog.Tracks` 的标题、演奏者、路径与时长即可。

> 如果以后替换成 Freesound、Sonniss、Pixabay 或 AI 生成音频，请在这个文件里逐条补上来源、作者、许可证与下载日期；CC-BY 需要署名，CC-BY-NC 不能用于商业版。

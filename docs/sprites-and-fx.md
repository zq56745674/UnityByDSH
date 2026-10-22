# 2D 精灵与技能特效设计（P4 表现层）

> 状态：**已落地**（2026-10-21）。设计稿见 §1~§10，实际接线与实机验收见 §11。
> 概念图（本地，`_art/sprites/` 已 gitignore）：
> - `_art/sprites/mockups/stage_option_a.png` — 战斗舞台在 1280×720 里的实际观感（左侧面板为示意）
> - `_art/sprites/mockups/monster_lineup.png` — 11 只怪物缩到舞台高度后的可读性检查
> - `_art/sprites/source/monsters_11_sheet.png` / `fx_12_sheet.png` / `hero_battle_edit.png` — 原始生成表

## 1. 现状与空间从哪来

战斗目前是**纯文本**的：`GameUI.cs:648` 的 `LogCard` 是一块 808×332 的日志卡，`GameUI.cs:661` 的 `_floatLayer` 已经在做伤害飘字，但场上没有任何角色/怪物贴图。

1280×720 画布里没有空闲区域（左面板 420 宽、右面板 824 宽全被占满），所以精灵只能**从日志卡的空间里换出来**：一块 808×332 的舞台正好等于日志卡，替换它不用动任何其它节点（怪物卡、Buff 条、商店、按钮区的 y 坐标全不变）。

```
右侧面板（824 宽）内部 y 坐标：
  6..114   怪物卡（名字 / 血条 / 属性）
  116..148 Buff 条
  154..486 日志卡 808×332  ← 换成战斗舞台
  492..608 商店
  612..668 按钮区
```

## 2. 美术方向：深海金潮（Deep-Sea Gilt）

风格锚点：**2 阶赛璐璐 + 强剪影 + 古金配件**。它要同时满足两件事——接住已有的 85 张“深海军蓝 + 古金边框”图标体系，又让角色和怪物在 200px 以内站得住。

### 2.1 色板（与 `GameConfig.Colors` 对齐）

| 用途 | Hex | 现有 UI 色（GameConfig.Colors） |
|---|---|---|
| 主背景 / 暗部 | `#101627` | PanelBg / InsetBg |
| 中间调 | `#1B2234` | RowBg / CardBg |
| 古金（边框、武器、光环） | `#E8B84B` | TextGold / TextTitle |
| 冷青（轮廓光、护盾、旋风） | `#6FE3D2` | AccentSlotReady 系 |
| 橙红（怪物血条、斩杀、狂暴） | `#F2683C` | FillMonsterHp |
| 魔法紫（升级、死亡、深渊） | `#A58CFF` | TextExp |
| 暴击粉 | `#FF9EC6` | TextCrit |

### 2.2 造型规则（生成时写进提示词，验收时逐条查）

1. 只保留**亮部 + 暗部**两个色阶，不做三阶以上过渡（缩小后不糊）。
2. 主光固定在**左上**，右侧剪影一道冷青边缘光——所有资产光源方向一致，这是“看起来是一套”的关键。
3. 极细深色描边（不是黑描边），描边吃掉锯齿，但不能变成剪纸风。
4. **剪影优先**：11 族怪物靠轮廓区分（胖/瘦/驼背/四臂/有角），不靠细节花纹。
5. 金色只出现在“贵重/危险”的地方：武器护手、腰带扣、王冠、光环。
6. 怪物一律**面朝左**，主角面朝右——精灵不做镜像翻转，朝向即阵营。
7. 不画地面投影、不画背景，投影由代码在舞台里画椭圆（方便按档次缩放）。
8. 特效只允许“发光叠加”一种质感：中心高亮、边缘碎屑，禁止写实烟雾/火焰纹理。

## 3. 为什么这套适配“挂机自动战斗”

| 约束 | 设计回应 |
|---|---|
| 一场战斗 3~10 秒，还有 ×4 加速 | 角色不靠逐帧动画，靠代码补间（呼吸/前冲/闪白）；特效总时长 0.12~0.30s |
| 屏幕小、信息密度高，数值才是主角 | 精灵只占舞台带 332px，血条/日志/商店不被遮挡；Boss 也只放大到 1.25 倍 |
| 11 章换族，玩家看的是“换了个东西” | 每章 1 个族的剪影，3 种类型共用身体 + 武器/徽标区分 |
| 同族有普通 / 精英 / Boss 三档 | 同一张精灵 + 缩放（×1.1 / ×1.25）+ 光环，**不做三套美术** |
| 词缀（吸血/护盾/剧毒）随机出现 | 词缀用颜色光环标识，不重画怪物 |
| 挂机场景通常关掉音效、静音挂后台 | 表现层不承担信息：所有关键信息仍在日志 ticker 和飘字里 |

## 4. 尺寸与文件规范

> 下表已按 2026-10-21 的落地实现更正；口径冲突时以 §11 为准。

| 项 | 落地值 |
|---|---|
| 舞台 | 808×332，圆角 8；实际是暗底 + 冷青光雾 + 地面带 + 1px 地线，没有用渐变贴图 |
| 地面线 | 舞台内 y=250（`StageGroundY`，世界坐标 y≈418；设计稿写 252） |
| 主角 | 高 190px、中心 x=178（设计稿 196 / +165）；源图 169×256 |
| 怪物 | 普通高 192px、中心 x=604（设计稿 208 / +610）；精英 206、Boss 226；源图 288×288（画布底部居中，保留各族相对体型） |
| 精英 / Boss | 身高区分（206/192、226/192）+ 脚下光环（§12.2）；Boss 另有 8 张独立立绘（358×358 源图） |
| 特效单帧 | 源图 240×240（新 8 张正方形）或 240 高、宽 250–372（旧 12 张），实际显示尺寸 92–200px（256 会整块盖住 192px 高的角色） |
| 导出 | PNG-24 + alpha，sRGB；导入为 Sprite(Single) + `alphaIsTransparency`，`Resources.Load<Sprite>` 直接可用；`maxTextureSize` 按类别设 512（怪物/Fx/光环）/ 1024（Boss）/ 2048（背景），`textureCompression: 0` |
| 存储预算 | 实测 74 张共 **1.88MB**（怪物 33 张 0.73 / Boss 11 张 0.36 / Fx 20 张 0.24 / 光环 5 张 0.10 / 背景 4 张 0.43 / 主角 0.02），在 4MB 预算内（2026-10-22 瘦身，见 §12.6） |

## 5. 资产清单

| 类别 | 数量 | 命名 | 说明 | 状态 |
|---|---|---|---|---|
| 主角战斗小人 | 1 | `Sprites/Hero/hero_battle` | 同一角色的 3/4 侧身持剑站姿 | ✅ 概念图已切 |
| 怪物身体 | 33 | `Sprites/Monsters/{族}_{melee,ranged,crit}` | 11 族 × 3 类型，一族一套剪影，类型由身体自带的武器区分 | ✅ 11 族全做（2026-10-22） |
| 类型武器层 | 0 | ~~`w_{sword,bow,dagger}`~~ | **取消**：身体图已按类型带武器，再叠一层会出两把武器（§12.3） | ❌ 改用类型分图 |
| 档次光环 | 2 | `Sprites/Auras/aura_elite`, `aura_boss` | 176 / 208px 宽的扁椭圆环，压在脚下 | ✅（2026-10-22） |
| 词缀光环 | 3 | `Sprites/Auras/affix_{drain,shield,poison}` | 84px 小环，叠在档次环内圈 | ✅（2026-10-22） |
| 技能特效 | 20 | `Sprites/Fx/fx_*` | 12 张战斗/状态特效 + 8 张技能专属（§12.4） | ✅ |
| 章节背景 | 4 | `Sprites/Backgrounds/bg_{cavern,wilds,crypt,citadel}` | 1440×592 JPG，Stretch 满舞台，按章映射 + 色温区分 | ✅（2026-10-22） |
| 类型徽标 | 3 | 复用 `Icons/glyph_*` 或新增 `glyph_bow`/`glyph_dagger` | 怪物名字前面 14px 小图标 | 部分可复用 |

**先做 11 族身体的理由**：11 章已经写死了 `ChapterMonsterNames`（`GameConfig.Tables.cs:81`，每章 3 个名字），不做 11 族就等于“11 章都在打同一只哥布林”。3 种类型（近战/远程/暴击）共用身体 + 武器层，是把 33 个名字压到 11 张图的唯一办法。

## 6. 动画与技术实现

### 6.1 角色（不逐帧）

| 状态 | 落地做法（全部程序化变换，无逐帧图） |
|---|---|
| 待机 | 缩放呼吸 ±0.006 / ±0.010（sin 1.7Hz）；没有光环旋转（光环未做） |
| 出手 | 向对面前冲 18px（60ms 去 / 220ms 回），不是设计稿的 14px |
| 受击 | 左右抖动 ±6px（主角）/ ±7px（怪物）；**没有闪白**（需要额外改材质或叠加亮图） |
| 死亡 | 0.4s 内旋转 −14°（主角）/ −18°（怪物）并淡出，同时补一发 `fx_death` |
| 升级 | 主角脚下 `fx_levelup` 播放一次 |
| 入场 | 怪物淡入 + 上浮 14px，阴影同步淡入 |

### 6.2 特效

- **静态图 + 补间**：`fx_hit` 随机旋转 0/90/180/270，缩 0.6→1.15→0.9，0.18s 淡出；`fx_crit` 1.0→1.4，0.22s。
- **两帧序列**：`fx_whirlwind` 环形刀光按 90° 分 3 段（对应旋风斩 3 次判定）；`fx_levelup` 光柱 2 帧。
- **池化**：12 个 `Image` 复用（`StageFxPool`），不 `Instantiate`；取最久未用的一个。
- **速度联动**：所有时长用 `Update` 的 dt 累计（游戏 ×4 是乘 dt 实现的，特效自动跟着快）；**暂停不要设 `Time.timeScale=0`**，dt=0 会让入场淡入卡住。
- **节制**：同屏最多 12 个（池容量）；普通特效 0.07s 节流，吸血/狂暴/剧毒/护盾这类每次都来的用 0.45s 节流，暴击/普通命中落点分开避免叠成一团。

### 6.3 触发点（要加的代码）

`BattleSystem` 原本只写日志（`BattleSystem.cs:240`、`:319` 等），表现层需要显式事件。最终落地的契约（设计稿的名字是 `BattleVisualKind`，实现改成 `BattleFx`）：

```csharp
public enum BattleFx { PlayerHit, PlayerCrit, SkillHeavy, SkillWhirlwind, SkillExecute, SkillCounter,
                       SkillOther, MonsterHit, MonsterCrit, Drain, Shield, Poison, Berserk }
public event System.Action<BattleFx, int> Visual;   // 订阅方：GameUI.EnsureBattleVisualHook()
```

在伤害结算、技能发动、死亡、升级、获得护盾/中毒的地方各发一次（`RaiseVisual`，实测 7 处 + 技能映射）。UI 侧不解析日志文字——日志是给玩家读的，不该当事件总线。

## 7. 布局：三个选项，建议 A

| | 做法 | 舞台尺寸 | 日志 | 改动面 |
|---|---|---|---|---|
| **A（已采用）** | 舞台替换日志卡；日志变舞台底部 2 行 ticker，点 ticker 右侧 ▲ 向上展开成 256 高滚动面板、点 ▼ 收回（**内嵌，不是模态**——GameController 的面板是互斥的，模态会和商店/设置抢开关） | 808×332 | ticker 2 行 + 展开面板 180 条 | 新增 `GameUI.Stage.cs`，改 `GameUI.cs:648` 一处，日志渲染改成 ticker/detail 双写 |
| B（保守） | 舞台 176 高插在 Buff 条和日志之间，日志压到 150 高（14 号约 8 行） | 808×176 | 8 行，仍常驻 | 舞台缩水，主角 120px、怪物 130px，舞台只剩“一条” |
| C（最小） | 不动布局，怪物卡右侧塞 96×96 怪物头像，主角立绘已在左面板；特效贴日志区 | 无 | 不变 | 视觉收益最小，像“加了个头像框” |

A 的代价是**日志默认只剩 2 行**。日志原本是核心反馈（`BattleLog` 单点写入、带游标），所以必须配一个模态：点「战斗日志」弹出既有的 808×560 浮层（`GameUI.Panels.cs` 已有浮层壳），内容仍是 `_logText` 的全量文本，不需要新数据通路。

## 8. 生成与切图管线（已跑通）

### 8.1 提示词骨架

统一风格段（每张表都要带）：

```
统一美术风格：2D 游戏美术，2 阶赛璐璐上色（只保留亮部和暗部两个色阶），主光来自左上方，
右侧剪影一道冷青色（#6FE3D2）边缘光；配色只允许深海军蓝 #101627 系、古金 #E8B84B、
冷青 #6FE3D2、暗红橙 #F2683C、魔法紫 #A58CFF；极细的深色描边；高对比剪影，细节克制，
缩小到 128 像素仍能一眼分辨；不出现文字、水印、边框。
```

- 主角：`edit_image`，以 `HeroPortrait.png` 为参考图，要求“同一角色改战斗小人、3/4 侧身面朝右、透明背景”——**不要**用纯文生图，否则脸会变。
- 怪物表：一张 2048² 排 11 只，**纯白背景**（`背景必须是纯净的白色 #FFFFFF，不画地面/阴影/网格线`）。
- 特效表：一张 2048² 排 12 个，**纯黑背景**（黑底才能用亮度抠图，并直接得到“叠加发光”的观感）。

### 8.2 切图（踩过的坑，已写进脚本）

- **不能按 4×3 网格硬切**：实测怪物没有对齐网格，硬切会把相邻怪物的手/光环切进来（第一版样张就是这样废掉的）。正确做法：白底 → 取“非背景”掩码 → 降采样 4 倍做 8 邻域连通域标记 → 按面积取身体、把邻近小块（武器/光环）并进最近的身体 → 按行优先排序。脚本 `_art/sprites/_build_concept.py` 里的 `segment_sheet()`。
- **边缘羽化**：每块裁切后对 18~22px 边缘做余弦羽化，防止“硬矩形”边（AI 画的光晕会被裁切面切出直角）。
- **黑底特效**：`alpha = max(R,G,B)` 并反预乘（`rgb / alpha`），再用格子边框的 18px 羽化，才能得到干净的叠加层。
- 主角源图自带 alpha 蒙版，先**压平到白底**再走白底流程，直接读 alpha 会留下半透明薄膜。

## 9. 工作量与风险

- 资产：3~4 张 2048² 生成表（约 ¥2/张）+ 1 个切图脚本（已写）。
- 代码：`GameUI.Stage.cs` 约 350 行 + `BattleSystem` 事件 10 处 + 动画组件 120 行 + 日志模态复用 80 行。
- 回归面：**纯表现层**，`BalanceRegression` / `NewGameProgression` 的数值结果必须逐字不变；成就 §11 回归要重跑（它会读 UI 状态）；效率测试走 ×4 加速 10 分钟看 GC。
- 风险 1：跨表风格漂移。缓解——每张表都带同一段风格段，出表后先拼成一张缩略图对照再切图。
- 风险 2：Boss 只放大 1.25 倍可能不够“大”。备选：Boss 单独生成 1 张（每族 1 张，11 张），成本翻倍；建议先跑一版看效果。
- 风险 3：日志变 2 行后会有人喊“看不到伤害明细”。缓解——ticker 用图标区分（命中/暴击/技能/死亡），并提供模态；日志数据本来就有游标，转模态几乎零成本。

## 10. 验收标准

1. 1280×720 与 1920×1080 下，怪物剪影 1 秒内可辨认，主角与 11 族怪物并排不串风格。
2. ×4 加速下特效不糊、不叠成一片；同屏 6 个特效时帧率不降。
3. 战斗结果、伤害数字、日志内容与改造前**逐字一致**（跑 `BalanceRegression` + 一场固定种子战斗对比）。
4. 舞台点击不拦截按钮；日志模态可开关，内容与改造前相同。
5. 全部精灵资源 < 4MB，首帧加载 < 100ms。

## 11. 已落地（2026-10-21）

> 上一节是设计稿；这一节记录**实际接进工程的样子**，与设计不同之处以这里为准。

### 11.1 改了哪些文件

| 文件 | 作用 |
|---|---|
| `HelloWorldProject/Assets/Scripts/GameUI.Stage.cs`（新增，partial class GameUI） | 战斗舞台 808×332、主角/怪物/阴影、程序化动画、12 个特效的图片池、可展开的战斗日志面板 |
| `HelloWorldProject/Assets/Scripts/BattleSystem.cs` | 新增 `enum BattleFx`（13 种，2026-10-22 扩到 21 种）、`public event System.Action<BattleFx,int> Visual`、`RaiseVisual(...)`，在命中/暴击/技能/吸血/剧毒/护盾/狂暴/死亡处抛出 |
| `HelloWorldProject/Assets/Scripts/GameConfig.Tables.cs` | 新增 `ChapterFamilyNames`（11 章族名，舞台左上角标签与族→贴图映射共用） |
| `HelloWorldProject/Assets/Scripts/GameUI.cs` | `BuildRightPanel` 里的日志卡整段换成 `BuildBattleStage(panel)`；`Initialize` 里 `EnsureBattleVisualHook()` |
| `HelloWorldProject/Assets/Scripts/GameUI.Core.cs` | `Update` 末尾 `TickBattleStage(Time.deltaTime)`；`RefreshLog` 改「ticker 两行 + 展开面板 180 条」双写；飘字落点改到怪物躯干；`RefreshDps` 展开日志时连 DPS 胶囊一起藏 |
| `HelloWorldProject/Assets/Scripts/GameUI.Achievements.cs` | `OnDestroy` 里 `UnhookBattleVisual()`（GameUI 只能有一个 OnDestroy） |

### 11.2 资源与实测尺寸

- 资源全在 `Assets/Resources/Sprites/`：`Hero/hero_battle.png`、`Monsters/{goblin,kobold,skeleton}_{melee,ranged,crit}.png`、`Bosses/{goblin,kobold,skeleton}_boss.png`、`Fx/fx_{hit,crit,heavy,whirlwind,execute,counter,drain,shield,poison,death,levelup,berserk}.png`，共 25 张，导入已是 Single sprite + alphaIsTransparency，`Resources.Load<Sprite>` 直接可用。
- 舞台内部坐标（原点左上、y 向下）：卡 808×332、地面 `StageGroundY=250`、主角 `cx=178`、怪物 `cx=604`；身高主角 190 / 普通怪 192 / 精英 206 / Boss 226（Boss 用单独生成的图，本身就比小怪宽厚，再叠加 226 的高度差，实机里「明显更强」）。
- 精灵按 `height * sprite.rect.width / sprite.rect.height` 定宽、`preserveAspect = true`，anchor(0,1) + pivot(0.5,0) 绕脚底缩放，脚底统一落在地面线上。
- **特效尺寸比设计的 256 缩了一档**（实测 256 会整块盖住 192px 的角色）：fx_hit 110/104、fx_crit 148/132、fx_heavy 168、fx_whirlwind 186、fx_execute 198、fx_counter 118、fx_drain 92、fx_shield 118、fx_poison 104、fx_berserk 148、fx_death 190、fx_levelup 200。

### 11.3 动画（全部程序化，没有逐帧图）

| 对象 | 动画 |
|---|---|
| 主角 | 呼吸（缩放 ±0.006/±0.010，sin 1.7Hz）、出手前冲 18px（60ms 去 / 220ms 回）、受击抖动 ±6px、阵亡 0.4s 内旋转 −14° 并淡出 |
| 怪物 | 呼吸（sin 1.45Hz，带相位偏移）、入场淡入 + 上浮 14px、受击抖动 ±7px、阵亡 0.42s 旋转 −18° 淡出 |
| 阴影 | `GlowSprite()` 压扁的椭圆，随入场淡入 |

- 特效池 12 个 `Image`，播放时取最久未用的一个：尺寸按 `Lerp(0.68, grow, sqrt(k))`、旋转 `spin*k`、0.55 后淡出。
- 两步节流：普通特效 `FxMinInterval=0.07s`，吸血/狂暴/剧毒/护盾这类「每次命中都来」的 buff 用 `BuffFxMinInterval=0.45s`（否则吸血鬼流会在主角身上糊一团永不消失的红光）。
- 舞台动画一律走 `Update` 的 dt，**暂停不要用 `Time.timeScale=0`**（dt=0 会让入场淡入停住、怪物看不见），用 `GameController.ToggleRunning()`。

### 11.4 战斗日志

- 收起：舞台底部两行 ticker（取最新 2 条，旧→新），一条日志内部的换行会被压成空格并截到 78 字，避免掉落行把 ticker 撑成 3 行。
- 展开：点右下角 ▲，面板从 ticker 上沿向上长到 256 高（`SmoothStep` 6.5/s），`ScrollRect + RectMask2D` 竖向滚动，内容是最近 180 条（旧在上、新在下，展开即跟到底）；点 ▼ 收回。
- 展开时把伤害飘字层和 DPS 胶囊一起隐藏；面板底色**必须完全不透明**（0.985 的 alpha 会让底下的怪物剪影透上来，文字叠在剪影上很难读）。

### 11.5 验收结果（2026-10-21，1280×720 实机）

1. 主角、哥布林/狗头人/骷髅三族、三族 Boss 换进舞台都不裁边、脚底对齐、比例合适；4~11 章当时暂无专属美术，按 `(chapter-1)%3` 轮换复用这三族（2026-10-22 已补齐，见 §12.1）。
2. ×4 加速下特效不重叠成一片；同屏最多 12 个特效池实测只占 2~6 个。
3. 战斗数值、飘字、日志内容与改造前一致（`BalanceRegression.RunStaticOnly()` = PASS）。
4. 舞台与日志面板点击不穿透到底下的按钮；▲/▼ 开关正常。
5. console 0 error / 0 warn；`unity command recompile` = completed。
6. 截图（本地，`_art/stage/shots/` 已 gitignore）：`final1.png` 普通战斗、`final3_goblin.png`/`final4_skeleton.png` 族贴图替换检查、`final6_log.png` 展开日志、`final7_boss.png` Boss 战。

### 11.6 还没做的（2026-10-21 时点）

- ~~4~11 章的专属怪物美术~~：2026-10-22 已补 8 族（§12.1）。
- ~~精英与 Boss 的档次光环/前缀特效~~：2026-10-22 已做（§12.2）。
- ~~章节背景图~~：2026-10-22 已做（§12.3）；武器层叠加已取消。
- ~~技能特效与技能的一一对应只做了 4 个专属~~：2026-10-22 补齐 15 个（§12.4）。

仍未做：主角阵亡/复活的专门表现、Boss 出场演出、背景的视差滚动。

## 12. 2026-10-22 扩充：11 族 / 光环 / 背景 / 15 技能特效

> 这一节是 §11 之后的第二轮表现层扩充，编号对应开发待办 18/19/20/21。所有数字都是 1280×720 实机截图核对过的。

### 12.1 怪物族别补齐（待办 18）

- 第 4~11 章新增 8 族 × 3 类型 = 24 张：`ogre` / `orc` / `spider` / `undead` / `gargoyle` / `demon` / `abyss` / `dragon`，每族 `_melee` / `_ranged` / `_crit`。
- 族映射从「三族轮换」改成按章查表：

  ```csharp
  private static readonly string[] StageFamilyKeys =
      { "goblin", "kobold", "skeleton", "ogre", "orc", "spider", "undead", "gargoyle", "demon", "abyss", "dragon" };
  int index = Mathf.Clamp(chapter, 1, StageFamilyKeys.Length) - 1;
  ```

  旧实现是 `Mathf.Abs(chapter - 1) % 3`，所以 4 章以后一直在打哥布林/狗头人/骷髅。
- 8 张 Boss 改成**每族单独生成**（`Bosses/{族}_boss.png`，源图 512×512）。上一版只有 3 族 Boss，4 章以后 Boss 是「精英怪放大 1.25 倍」。
- **教训**：先把 8 个 Boss 排成 4 张「一对」的 2048² 白底图再切，四张全部作废——两个角色的斧头/披风在 x 投影上重叠（最小列占用 148~345 px），硬切会把武器切断。多角色同图只适合「列之间有真空白」的怪物表（三列之间至少 40px 全空列），Boss 必须一张一只。

### 12.2 档次光环与词缀光环（待办 19）

| 文件 | 宽 | 用在 |
|---|---|---|
| `Sprites/Auras/aura_elite.png` | 176 | 精英怪脚下（金色火焰双环） |
| `Sprites/Auras/aura_boss.png` | 208 | Boss 脚下（外金符文环 + 内冷青环） |
| `Sprites/Auras/affix_drain.png` | 84 | 词缀「吸血」 |
| `Sprites/Auras/affix_shield.png` | 84 | 词缀「护盾」 |
| `Sprites/Auras/affix_poison.png` | 84 | 词缀「剧毒」 |

- **压成椭圆**：`AuraSquash = 0.42f`（俯视地面环），中心 `y = -(StageGroundY - width * AuraSquash * 0.32f)`，让环的下半沿压在地面线上。
- **层级靠创建顺序**：两个光环 Image 在主角/怪物 Image **之前**创建（UGUI 兄弟顺序即渲染顺序），所以环在脚下而不是盖在腿上；也在 StageFloor 之后，压在地面带上。
- **入场/死亡联动**：`alpha = Clamp01(_battleMonsterSpawn) * (1 - deathK)`，tier 再乘 0.92、affix 乘 0.86；`pulse = 1 + sin(_battleAnimTime * 2.2) * 0.045` 做慢脉动。怪物死亡动画结束那一帧把两个光环都关掉。
- 只有词缀里会改战斗节奏的三个（吸血/护盾/剧毒）配环；狂暴/坚甲/迅捷只改面板数值，不配环——同屏两圈金红叠在一起反而看不清档次。
- 普通怪不配档次环：192px 高的小怪上方一圈金光会让它看起来像精英。

### 12.3 章节背景（待办 20）

- 4 张 `Sprites/Backgrounds/bg_{cavern,wilds,crypt,citadel}.jpg`，1616×664：从 2048² 四宫格每格取 1024×421 横带（≈8:3.3，贴近舞台 808:332）再 LANCZOS 放大。
- 按章映射 (`BackdropFor`)：1/2/6 → 洞窟、3/7 → 陵墓、4/5 → 荒野、8~11 → 魔域。
- **同图不同章用色温区分** `BackdropTint(chapter)`：11 条 ±10% 级别的乘色（洞窟 ch1 冷青 / ch2 火把暖 / ch6 深蓝，陵墓 ch3 幽蓝 / ch7 更暗，荒野 ch4 黄昏 / ch5 偏赤，魔域 ch8 石青 / ch9 恶魔赤 / ch10 深渊紫 / ch11 龙裔金）。缓存键是 `图名:章`，否则同图换章不会重设颜色。
- Image 的 alpha 固定 0.68，背景在 `StageBackdrop` 纯色之后、`StageHaze` 冷青光雾之前；`preserveAspect = false` 直接拉满 808×332（源图比例已经接近，拉伸不可见）。
- **武器层叠加取消**：11 族的身体图已经按类型画好武器（剑盾 / 弓弩 / 匕首），再叠一层前景武器只会出现「两把剑」。这是把 33 个怪物名字压成 24 张图（而非 11 张身体 + 3 张武器）的直接原因。

### 12.4 15 个技能的特效一一对应（待办 21）

| 技能 | 特效图 | 尺寸 / 位置 | 触发点 |
|---|---|---|---|
| 重击 | `fx_heavy` | 168 @ 怪物躯干 | 主动技命中 |
| 旋风斩 | `fx_whirlwind` | 186 @ (主角右 40, 154) spin 120 | 主动技 3 段 |
| 狂暴 | `fx_berserk` | 148 @ 主角 | 主动技发动 |
| 斩杀 | `fx_execute` | 198 @ 怪物下沿 | 主动技命中 |
| 钢铁之躯 | `fx_iron_body` | 176 @ 主角 | **学会时**放一次 |
| 狂暴之心 | `fx_berserk_heart` | 176 @ 主角 | **学会时**放一次 |
| 致命打击 | `fx_lethal_strike` | 176 @ 主角 | **学会时**放一次 |
| 迅捷 | `fx_swiftness` | 176 @ 主角 | **学会时**放一次 |
| 巨人杀手 | `fx_giant_slayer` | 176 @ 怪物 spin −12 | 对精英/Boss 的**收尾击杀** |
| 不死之身 | `fx_undying` | 160 @ 主角 | 减伤真正生效（挨了致死量级的一击） |
| 反击 | `fx_counter` | 118 @ 主角 | 反弹伤害 |
| 暴击连锁 | `fx_crit_chain` | 150 @ 怪物躯干 | 追加伤害结算 |
| 濒死爆发 | `fx_near_death` | 200 @ 主角脚下 | 触发不死 3 秒 |
| 护盾生成 | `fx_shield` | 118 @ 主角 | 护盾刷新（0.45s 节流） |
| 吸血鬼 | `fx_drain` | 92 @ 主角 | 每次吸血（0.45s 节流） |

- 前 4 个是主动技，本来就是每个技能一张图；中间 4 个是**纯数值被动**（防御 +150%、暴击 +8%、暴伤 ×1.2、攻速 +8%），战斗里没有「发动」这个时刻，只在 `CheckStageSkillLearn()` 检测到 `ownedSkillMask` 新增时放一次，当「学会」反馈。
- 巨人杀手只在**收尾击杀**时亮：每次命中都亮金光会让 Boss 战一直糊着。
- `BattleFx` 枚举新增 8 项：`SkillCritChain` / `SkillNearDeath` / `SkillUndying` / `SkillGiantSlayer` / `PassiveIronBody` / `PassiveBerserkHeart` / `PassiveLethalStrike` / `PassiveSwiftness`；`SkillVisualOf` 里补了 `SkillId.CritChain → BattleFx.SkillCritChain`（之前落到 `SkillOther` = 通用的 `fx_heavy`）。

### 12.5 验收与遗留

- 2026-10-22 实机截图（`_art/sprites/shots/`，已 gitignore）：`ch1_cavern.png`（1 章洞窟 + 哥布林）、`ch3_crypt.png`（3 章陵墓 + 骷髅）、`ch9_demon.png`（9 章魔域赤 + 恶魔法师）、`aura_elite_poison.png`（8 章精英石像鬼 + 金精英环 + 毒环，4× 放大裁图核对过环压在脚下）、`fx_new.png`（巨人杀手 + 暴击连锁 + 不死之身 + 濒死爆发同屏）。
- `unity command recompile` = completed、`compilationFailed=false`、console 0 error。
- **预算曾是 2.9 倍**：刚落地时 74 张共 11.65MB（怪物 5.42 / Boss 3.07 / Fx 1.66 / 光环 0.78 / 背景 0.58 / 主角 0.15）。2026-10-22 已按 §12.6 压到 1.88MB，符合原定 4MB。
- 待办里**没做**的：Boss 出场演出、背景视差滚动、主角阵亡/复活专门表现。

### 12.6 体积瘦身（2026-10-22）

目标是把 74 张源图从 11.65MB 压回 §4 的 4MB 预算，同时不牺牲舞台上的观感（怪物实际只显示 192px 高、Boss 226px，源图 384/512 本来就过剩）。最终 **1.88MB**：

| 目录 | 压缩前 | 压缩后 | 处理 |
|---|---|---|---|
| Monsters 33 张 | 5.42MB | 0.73MB | 384² → 288²（×0.75） |
| Bosses 11 张 | 3.07MB | 0.36MB | 512² → 358²（×0.70） |
| Fx 20 张 | 1.66MB | 0.24MB | 320 → 240（×0.75） |
| Auras 5 张 | 0.78MB | 0.10MB | ×0.75（boss/elite 384²、affix 192²） |
| Backgrounds 4 张 | 0.58MB | 0.43MB | 1616×664 → 1440×592，JPG q84 progressive |
| Hero 1 张 | 0.15MB | 0.02MB | 255×384 → 169×256 |

（表内只算 74 个图片文件；算上 74 个 `.meta`，`Assets/Resources/Sprites` 在磁盘上合计 2.07MB。）

做法（脚本 `_art/sprites/shrink_b.py`，一次性、不进仓库）：

1. **预乘 alpha 再缩放**：`PIL` 直接对 RGBA 做 LANCZOS 会让透明区的白色 RGB 渗进边缘，形成 1px 白圈（和切图时踩过的同一个坑）。先把 RGB 乘 alpha、缩放、再用缩放后的 alpha 除回来，透明边缘才干净。
2. **FASTOCTREE 调色板量化 256 色**：`im.quantize(colors=256, method=Image.FASTOCTREE)` 是 Pillow 里唯一同时保留 alpha 的量化路径；导出成 8-bit 调色板 PNG，体积降到原图约 10–15%。发光特效靠 256 色 + 扩散抖动仍能保住渐变，肉眼在舞台尺寸下与 24-bit 无差（`_art/sprites/_cmp_shrink.png` 三列对照：原图 / 激进 160 色 / 最终 256 色）。
3. **背景只缩不量化**：JPG 已是有损，缩到 1440 宽（舞台 808 宽的 1.78 倍）+ q84 足够；再量化会出色带。

验收：`Resources.Load<Sprite>` 读回 288×288 / 358×358 / 240×240 / 384² / 1440×592 / 169×256，均非空；Play 模式截 `_art/sprites/shots/shrink_ch1.png`（1 章洞窟 + 哥布林 + 背景）、`shrink_boss.png`（11 章远古·龙裔 Boss + 双光环）确认无糊边、无白圈、无可见色带。`.meta` 的 `maxTextureSize`（512/1024/2048）与平台覆盖不需要改——文件本身已小于上限。


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
| 主角 | 高 190px、中心 x=178（设计稿 196 / +165）；源图 170×256 |
| 怪物 | 普通高 192px、中心 x=604（设计稿 208 / +610）；精英 206、Boss 226 |
| 精英 / Boss | 靠身高区分（206/192、226/192）；设计里的脚下光环**没做**，Boss 另有独立美术 |
| 特效单帧 | 源图 256×256，实际显示尺寸 92–200px（256 会整块盖住 192px 高的角色） |
| 导出 | PNG-24 + alpha，sRGB；导入为 Sprite(Single) + `alphaIsTransparency`，`Resources.Load<Sprite>` 直接可用；压缩与尺寸沿用导入默认，未逐张核对 |
| 存储预算 | 实测 25 张共 4.17MB，略超 4MB 预算（Boss 三张 265–323KB） |

## 5. 资产清单

| 类别 | 数量 | 命名 | 说明 | 状态 |
|---|---|---|---|---|
| 主角战斗小人 | 1 | `Sprites/Hero/hero_battle` | 同一角色的 3/4 侧身持剑站姿 | ✅ 概念图已切 |
| 怪物身体 | 11 | `Sprites/Monsters/m{章:02}_{族}` | 每章 1 族，面朝左 | 样张 3 族（哥布林/狗头人/骷髅）+ 食人魔、蛛魔 |
| 类型武器层 | 3 | `Sprites/Monsters/w_{sword,bow,dagger}` | 叠加在右手锚点，区分近战/远程/暴击 | 未做 |
| 档次光环 | 2 | `Sprites/Auras/aura_elite`, `aura_boss` | 168/200px 环，缓慢旋转 | 未做 |
| 词缀光环 | 3 | `Sprites/Auras/affix_{drain,shield,poison}` | 62px 小环，压在脚下 | 未做 |
| 技能特效 | 12 | `Sprites/Fx/fx_{hit,crit,heavy,whirlwind,execute,drain,shield,poison,counter,death,levelup,berserk}` | 每张 1 帧静态，代码做缩放/淡出；仅旋风、升级是 2 帧 | ✅ 概念图已切 |
| 章节背景 | 4（可选） | `Sprites/Backdrops/bg_{crypt,den,ruin,abyss}` | 808×332 剪影层，按章复用 | 未做 |
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
| `HelloWorldProject/Assets/Scripts/BattleSystem.cs` | 新增 `enum BattleFx`（13 种）、`public event System.Action<BattleFx,int> Visual`、`RaiseVisual(...)`，在命中/暴击/技能/吸血/剧毒/护盾/狂暴/死亡处抛出 |
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

1. 主角、哥布林/狗头人/骷髅三族、三族 Boss 换进舞台都不裁边、脚底对齐、比例合适；4~11 章暂无专属美术，按 `(chapter-1)%3` 轮换复用这三族（**TODO**）。
2. ×4 加速下特效不重叠成一片；同屏最多 12 个特效池实测只占 2~6 个。
3. 战斗数值、飘字、日志内容与改造前一致（`BalanceRegression.RunStaticOnly()` = PASS）。
4. 舞台与日志面板点击不穿透到底下的按钮；▲/▼ 开关正常。
5. console 0 error / 0 warn；`unity command recompile` = completed。
6. 截图（本地，`_art/stage/shots/` 已 gitignore）：`final1.png` 普通战斗、`final3_goblin.png`/`final4_skeleton.png` 族贴图替换检查、`final6_log.png` 展开日志、`final7_boss.png` Boss 战。

### 11.6 还没做的

- 4~11 章的专属怪物美术（现在是三族轮换复用）。
- 精英与 Boss 的档次光环/前缀特效（目前只靠 226px 身高 + 血条金/红边框区分）。
- 章节背景图、武器层叠加、主角阵亡/复活的专门表现。
- 技能特效与技能的一一对应只做了 4 个专属（重击/旋风斩/斩杀/反击），其余 11 个技能共用 `fx_heavy`。


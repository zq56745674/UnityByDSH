# 无尽模式与最高层记录设计

状态：2026-10-19 已实现并通过首轮平衡（README 路线图第 11 项「无尽模式 + 最高层记录」）
涉及存档：v11（v7 加 endlessBestFloor、v8 加五槽装备与 slotStarterMask、v9 转生、v10 成就、v11 加 Top-5 历史榜 endlessTop；旧档按版本分支迁移）
已新增：Assets/Scripts/Endless.cs、Assets/Scripts/GameController.Endless.cs、Assets/Scripts/GameUI.Endless.cs、Assets/Editor/EndlessPacing.cs
已修改：GameConfig.cs / GameConfigData.cs / game_config.json / GameConfig.Loader.cs / Models.PlayerData.cs / Models.cs / GameController.cs / SaveSystem.cs / GameUI.cs / GameUI.Core.cs / GameUI.Panels.cs / README.md
实测基线（2026-10-19，20 种子）：P200 满装模板首死中位 45 层，P10 30 / P90 50 / max 60；50 层累计金币约 37.2 万。见 §12.4。

> 2026-10-19 晚更新：装备扩展为五槽、攻速改软上限后，P1000 满配的首死中位升到 290 层（50 种子，§12.5.3）；随机五槽配装中位约 242 层。

## 0. 一句话

通关 11-10 后解锁的连续爬层模式：每层怪物更强，清完本层立刻进入下一层；死亡或主动撤退结束本局，只记录「已经清除的最高层」，并分别保存本档最高与全局最高。

## 1. 为什么做

### 1.1 终局缺少可重复的目标

当前 11-10 通关后，玩家剩下的循环是：刷装备 → 卖店 → 洗练 → 再刷。装备评分与洗练会逐渐收敛，金币也仍是软出口（README 已知问题第 4 条）。游戏没有一个「变强之后去哪里验证」的地方。

无尽模式把已有的战斗、掉落、商店、技能三选一、装备对比全部复用，只新增「层」这一条纵向目标：玩家不需要新角色、新货币或新装备槽，就能持续挑战自己的构筑。

### 1.2 最高层记录是长期目标，也是平衡反馈

「最高层」比「通关 11-10」更适合做长期目标：

- 它没有固定终点，强一点就能多推一层；
- 它天然可比较（本档 / 本机 / 不同构筑）；
- 它给平衡探针一个连续指标（首死层数、每层耗时），比「终局 BOSS 是否打得过」更细。

### 1.3 与路线图其他项解耦

本设计不依赖成就系统（第 9 项）、转生（第 10 项）或音效/特效（第 12 项）。它可以先独立上线：即使成就和转生还没做，无尽模式 + 最高层记录也已经是一个完整闭环。

## 2. 目标与非目标

### 2.1 目标

1. 通关 11-10 后出现一个清晰的「无尽」入口，不干扰战役流程。
2. 用程序化公式生成无限层，每 5 层一个 BOSS，层数越高越难，但单层时间可控。
3. 死亡、撤退、切档、删当前档、重置都能正确结束本局，不留下半局状态；删其他档不影响当前局。
4. 「最高层」语义唯一：已清除的最高层，死在第 N 层不会把 N 记成纪录。
5. 本档最高层随存档走，全局最高层跨三个存档保留。
6. 不引入装备等级膨胀：无尽掉落等级封顶 150，纪录只由构筑、决策和操作决定。
7. 所有数值可热改，坏配置有夹取，不出现除零、越界、死循环或负血量。
8. 可在 Play 模式完整验收，并给 EndlessPacing 探针提供稳定基线。

### 2.2 非目标

- 不做联网排行榜、赛季、赛季重置或服务器校验。
- 不做无尽专属角色、专属货币、专属装备槽。
- 不做「保留未完成的一局、下次继续」——退出即放弃本局，纪录已即时保存。
- 不做联网排行榜；本档 Top-5 历史榜已于 2026-10-22 落地（v11，§6.5），只存在本机存档里。
- 不与成就系统联动（以后可以在成就里引用「最高层 ≥ N」，但本设计不新增成就）。
- 不重画怪物/立绘，不新增音效；表现层沿用现有卡牌与横幅。
- 不改变战役的 11 章 × 10 关结构，不把无尽层塞进 StageCatalog。

## 3. 解锁与入口

### 3.1 解锁条件

    EndlessUnlocked =
        Player.IsStageFirstCleared(GameConfig.ChapterCount, GameConfig.StagesPerChapter)

也就是首次通关 11-10。判定只认首通记录 clearedStages，不看 maxChapterUnlocked，也不看「当前停在哪一关」——终局通关后 AdvanceStage() 返回 false，玩家会停在 11-10，当前关不能作为「已通关」的判据。

兼容处理：

- v6 及更早的存档如果已有 11-10 首通记录，直接解锁。
- 老档若 maxChapterUnlocked == 11 但缺少 11-10 首通记录（历史 bug 或手工改档），保持未解锁；需要重新通关一次 11-10。不做自动补记，避免把没打完的档算成通关。
- 未解锁时入口仍可见，打开只读说明面板，「开始挑战」置灰，提示「通关第 11 章 11-10 后解锁」。

### 3.2 首次解锁的提示

11-10 首通结算的横幅（ShowClearBanner）在原有「★ 第 11 章 通关！」下方追加一行：

    无尽模式已解锁 —— 左上角「无尽」进入

只在 firstClear && clearedChapter == ChapterCount && clearedStage == StagesPerChapter 时追加一次；重复通关不再刷。日志同步写一条「✦ 无尽模式已解锁」。

### 3.3 入口位置

左栏关卡行目前是：

    [ 关卡 11-10 · 怪 Lv.110 ]   (168, 76, 216×24)
    生命文本 y=104，血条 y=126

这一行右侧没有空间再加一个完整按钮。首版把这一行拆成两个按钮：

    [ 11-10 · Lv.110 ] [ 无尽 ]   (168,76,150×24) (324,76,60×24)

- 关卡按钮字号从 14 降到 13，文本去掉「关卡 / 怪」，保留 11-10 · Lv.110；选关面板里仍有完整标题。
- 「无尽」按钮宽 60、高 24，字号 13；未解锁时文字变灰，点击仍打开只读说明面板（「开始挑战」置灰，见 §8.2）。
- 进入无尽后，关卡按钮显示「第 N 层 · Lv.X」，「无尽」按钮显示「记录」，点它打开无尽面板（可继续/撤退/看纪录）。
- 不把入口放进设置或存档面板：它每天都会被点，应该在主界面第一屏。

备选方案（不采用，留作记录）：在选关浮层标题行加「无尽模式」按钮。好处是不动左栏布局，坏处是入口多一层，且选关浮层本身已经有 11 个章节按钮和 10 个关卡格，视觉上更像「战役专属」。首版采用左栏双按钮。

## 4. 核心循环

### 4.1 局的生命周期

    Campaign（战役）
       │ 首次通关 11-10
       ▼
    Unlocked（解锁，仍在战役）
       │ 点「无尽」→ 面板 → 开始挑战
       ▼
    EndlessRun（一局）
       │ 每层：击杀 N 只 → 最后一只精英/BOSS → 层通过
       │ 死亡 / 撤退 / 切档 / 删当前档 / 重置
       ▼
    Ended（本局结束；死亡/撤退显示结果面板，切档/重置由调用方直接复位）
       │ 再次挑战                 │ 返回战役
       ▼                         ▼
    EndlessRun                 Campaign

关键规则：

1. 一局从起始层开始，连续爬层，层与层之间不回到主菜单。
2. 死亡直接结束本局，不走战役的 1.5 秒自动复活。
3. 撤退也结束本局，但不算死亡，不增加 TotalDeaths。
4. 切档、删当前档、重置、退出游戏都结束本局（删其他档不影响当前局）；未完成的一局不写进存档，也不可恢复。
5. 本档最高层在每清一层时即时保存，所以即使下一层立刻死亡，上一层的纪录也不会丢。
6. 结果面板必须做出选择（再次挑战 / 返回战役），不能点空白关闭。

### 4.2 一局开始

    StartEndlessRun(startFloor):
      Mode = GameMode.Endless
      run = new EndlessRunState {
          floor = clamp(startFloor, 1, Max(1, Player.endlessBestFloor)),
          startFloor = floor,
          killCount = 0,
          runKills = 0,
          clearedThisRun = 0
      }
      _pendingTier = Normal
      _finalBossAttempt = 0
      _justClearedStage = false
      Monster = null
      _loadedFromSave = false
      IsRunning = true
      Player.FillHp()
      Player.shield = 0
      _battle.ResetSkillState()
      CurrentState = WaitingRespawn
      _respawnTimer = GameConfig.MonsterRespawnDelay
      CloseAllPanels()
      EndlessResultOpen = false
      Log: «◆ 无尽模式 · 从第 S 层开始（本档最高 B 层）»

起始层：

    StartFloorFor(best) = best <= 0 ? 1 : Max(1, best - EndlessCheckpointOffset)

默认 EndlessCheckpointOffset = 4。例：本档最高 13 层 → 起始第 9 层；最高 3 层 → 起始第 1 层。StartEndlessRun 还要把 startFloor 夹到 1..max(1, Player.endlessBestFloor)，避免调试/读档参数把玩家直接送到未达到的层。

面板上同时提供「从第 1 层开始」按钮（仅当 StartFloorFor(best) > 1 时显示），让玩家可以练手或刷低层金币。从第 1 层开始不会额外奖励，也不影响纪录语义。

### 4.3 一层的结构

每层由 EndlessFloorDef 描述：

| 字段 | 公式 | 说明 |
|---|---|---|
| floor | ≥1 | 层号 |
| monsterLevel | EndlessBaseMonsterLevel + (floor − 1) × EndlessMonsterLevelStep | 默认 110 起，每层 +6 |
| killTarget | Clamp(EndlessKillTargetBase + (floor − 1) / EndlessKillTargetEvery, 1, EndlessKillTargetMax) | 默认 3 起，每 5 层 +1，封顶 6 |
| clearTier | floor % EndlessBossEvery == 0 ? Boss : Elite | 本层最后一只 |
| BossType | (MonsterType)(((floor / EndlessBossEvery) − 1) % 3) | 仅 BOSS 层；按层确定，不掷骰 |

默认值下的前几层：

| 层 | 怪物等级 | 击杀目标 | 最后一只 | BOSS 类型 |
|---:|---:|---:|---|---|
| 1 | 110 | 3 | 精英 | — |
| 2 | 116 | 3 | 精英 | — |
| 5 | 134 | 3 | BOSS | 近战 |
| 10 | 164 | 4 | BOSS | 远程 |
| 15 | 194 | 5 | BOSS | 暴击 |
| 20 | 224 | 6 | BOSS | 近战 |
| 50 | 404 | 6 | BOSS | 近战 |
| 100 | 704 | 6 | BOSS | 远程 |

实现要点：

- 每层前 killTarget − 1 只按普通怪生成；当 EndlessKillsLeft == 1 时把 _pendingTier 设为 clearTier，下一只就是精英/BOSS。这套「最后一只」逻辑与战役的 AnnounceStageBossIfNext 同构，但不要复用战役的 Player.CurrentStage 和 stageKillCount。
- 还要覆盖 killTarget = 1 的热改配置：SpawnMonster() 在检查 _pendingTier != Normal 之前补一次 `if (EndlessActive && _pendingTier == MonsterTier.Normal && EndlessKillsLeft == 1) _pendingTier = EndlessCatalog.Get(EndlessRun.floor).clearTier;`。否则开局时 _pendingTier 还是 Normal，第一只会被当成普通怪，最后一只精英/BOSS 不会出现。
- BOSS 层沿用战役规则：BOSS 无词缀、战前回满血、商店在最后一只前开放。普通层的精英可以有词缀。
- BOSS 类型按层固定，避免「同一层反复重开掷到更简单的 BOSS」；普通怪类型仍可随机。
- 每层结束到下一层开始的等待用 EndlessFloorClearDelay = 2.0s（战斗时间，受 1×/2×/4× 速度档位影响；x4 下约 0.5 真实秒），比战役的 StageClearDelay = 3s 短一点，保持爬层节奏。

### 4.4 击杀与层通过

    HandleMonsterKilled(monster):
      TotalKills++
      run.runKills++
      run.killCount++
      GrantKillReward(monster)       // 只发经验；等级参数夹到 150，见 §5.5
      ResolveLevelUp()
      ResolveDrops(monster)          // 掉落等级夹到 150，见 §5.5
      if run.killCount < floorDef.killTarget:
          if EndlessKillsLeft == 1:
              _pendingTier = floorDef.clearTier
          EnterRespawnWait(MonsterRespawnDelay)
          return
      ResolveEndlessFloorClear()

    ResolveEndlessFloorClear():
      run.clearedThisRun++
      int cleared = Mathf.Min(run.floor, 9999)   // 记录上限；9999 层到顶后停在这一层
      bool newBest = cleared > Player.endlessBestFloor
      int floorGold = EndlessCatalog.FloorGoldOf(cleared)
      int firstGold = newBest ? EndlessCatalog.FirstClearGoldOf(cleared) : 0

      if newBest:
          Player.endlessBestFloor = cleared
          EndlessRecord.ReportGlobal(cleared)   // PlayerPrefs，见 §6
      Player.gold += floorGold + firstGold
      TotalGoldEarned += floorGold + firstGold
      if newBest:
          SaveSystem.Save(Player)               // 金币入账后再落盘，避免存档少一层金币
      Log/Banner:
          newBest ? «★ 新纪录 · 无尽第 N 层» : «第 N 层通过»
      run.floor = Mathf.Min(run.floor + 1, 9999)
      run.killCount = 0
      _pendingTier = Normal
      _justClearedStage = false
      CurrentState = WaitingRespawn
      _respawnTimer = GameConfig.EndlessFloorClearDelay

注意：

- 先记录，再进入下一层。不要在玩家死亡时才写纪录；否则崩溃/断电会丢一层。
- 层通过金币每次都发；首通奖金只在 newBest 时发。这样重复刷低层仍有收益，但不会无限刷首通。
- 9999 层是记录上限：到顶后层号停在 9999，仍可重复刷金币，但不再产生新纪录；避免 floor 无限增长导致金币/怪物数值溢出。
- 层通过横幅复用现有 BannerTitle/BannerSubtitle/BannerTimeLeft，不新建队列。
- 进入下一层后玩家保留当前 HP/护盾/技能冷却状态；BOSS 前回满血。这样层间有资源压力，BOSS 层是检查点。
- GrantKillReward / ResolveLevelUp / ResolveDrops 必须在 ResolveEndlessFloorClear 之前；后者会清 Monster，之后就拿不到经验/掉落。
- 和战役一样，HandleMonsterKilled 在状态复位之后检查 pendingSkillPicks > 0 并 OpenSkillChoice()；否则升级送的技能选择会被 WaitingRespawn 覆盖。选完技能后 ChooseSkill 回到 WaitingRespawn，层通过等待可能被重置为 MonsterRespawnDelay，首版接受，不影响层推进。

### 4.5 死亡

    HandlePlayerDied():
      TotalDeaths++
      if Mode == Endless:
          EndEndlessRun(EndlessEndReason.Death)
          return
      // 战役原逻辑：stageKillCount=0、Reviving、1.5s 后复活

无尽模式不调用 RevivePlayer，也不清 Player.stageKillCount（那是战役字段）。死亡时：

- 当前层没有清完，所以不会把当前层记为最高层；最高层只由已经清完的层推进；
- 如果本局从检查点开始且没有清掉高于旧纪录的层，纪录不变；本局成绩仍按实际清层数另算；
- 结果面板显示「第 N 层阵亡 / 本局清除 X 层 / 本档最高 B / 全局最高 G」，X 是本局实际清完的层数（从检查点开始时从 0 计）。

EndEndlessRun：

    EndEndlessRun(reason):
      run.endReason = reason
      run.reachedFloor = run.floor
      // clearedThisRun 在每层清完时累加，不能用 floor−1 反推：
      // 从检查点开始时，floor−1 会把检查点之前的层错误算进本局成绩。
      Mode = GameMode.Campaign        // 逻辑上已离开无尽；结果面板仍是模态
      IsRunning = false
      CurrentState = State.Ended
      Monster = null
      _pendingTier = Normal
      // 保留 EndlessRun 给结果面板读；不要在这里清空它。
      EndlessPanelOpen = false
      // 只有死亡/撤退弹结果面板；切档/重置由调用方继续 ResetSessionState，不弹结算。
      EndlessResultOpen = reason == EndlessEndReason.Death || reason == EndlessEndReason.Retreat
      Log: reason == Death
          ? «第 N 层阵亡，本局结束（本局清除 X 层）»
          : reason == Retreat
              ? «已撤退（第 N 层）（本局清除 X 层）»
              : «本局已结束（本局清除 X 层）»

这里引入一个新状态 State.Ended。原因：如果复用 WaitingRespawn，Update 会在计时结束后自动 SpawnMonsterNow()，结果面板下面会继续刷怪。State.Ended 下控制器只处理 UI/速度/面板，不推进战斗；结果面板关闭前不能开始新局。

实现注意：EndEndlessRun 不要调用 CloseAllPanels()——CloseAllPanels 会关掉 EndlessResult，把刚设好的结果面板一起清掉。正确顺序是先关其他面板/无尽面板，设置 EndlessRun、EndlessResultOpen 和 State.Ended，由 StartEndlessRun / ReturnToCampaign / ResetSessionState 去关结果面板。

### 4.6 撤退

无尽面板在局内显示「继续战斗」和「撤退」。撤退需要两次点击确认（3 秒窗口，和删档一致）：

- 第一次点「撤退」→ 按钮变「确认撤退？」；
- 3 秒内再点 → EndEndlessRun(Retreat)；
- 超时或点别处 → 复原。

撤退不增加 TotalDeaths，不改变 Player.endlessBestFloor（已经清过的层此前已保存）；结果面板标题为「已撤退」，不是「阵亡」。

### 4.7 再次挑战与返回战役

结果面板两个按钮：

| 按钮 | 行为 |
|---|---|
| 再次挑战 | StartEndlessRun(StartFloorFor(Player.endlessBestFloor))；回满血；从新的起始层开始 |
| 返回战役 | ReturnToCampaign()：Mode=Campaign、IsRunning=true、Monster=null、Player.FillHp()、Player.shield=0、State=WaitingRespawn、_respawnTimer=MonsterRespawnDelay、_pendingTier=Normal、_finalBossAttempt=0、EndlessRun=null、EndlessResultOpen=false；不改变战役 chapterIndex/stageIndex |

「再次挑战」不会从死亡的那一层原地重开。原地重开会把「死在第 N 层 → 重开 N 层」变成无限重试，纪录会失去意义；统一回到 best − EndlessCheckpointOffset 的检查点。

返回战役后，玩家仍在 11-10（或当前战役关）。由于 11-10 已经首通，选关面板可以正常选其他关。

## 5. 数值设计

### 5.1 怪物属性公式

无尽怪物不能直接调用 MonsterData.CreateForLevel：

    CreateForLevel(level, ...) 在 level >= MaxTierLevel(110) 时会：
      × MaxTierHp/Attack/Defense = 3
      速度 ×2
      BOSS 再 × FinalBossStatFactor = 1.6
      再乘 ChapterPressure（第 11 章 = 1）

默认配置下所有无尽层都 ≥110，如果复用，会把「第 1 层普通怪」直接变成终局档，BOSS 层更是 12 倍基础值（Boss 档次 2.5 × MaxTier 3 × FinalBoss 1.6），难度曲线会从第一层就崩掉。

新增：

    MonsterData.CreateForEndlessFloor(int floor, MonsterType type, MonsterTier tier, MonsterAffix affix)

内部复用 CreateForLevel 的基础属性 / 类型倍率 / 档次倍率 / 词缀计算，但：

- 不乘 MaxTierHp/Attack/Defense、MaxTierSpeed；
- 不乘 FinalBossStatFactor；
- 不乘 ChapterPressure，也不乘章节族类偏向 MonsterStatBiasOf（按中性 1 处理）；
- 额外乘 EndlessBaseFactor（默认 2.8），只作用于 HP / 攻击 / 防御，不作用于速度；
- 速度只由等级、类型、档次决定。

实现上建议把 CreateForLevel 的主体抽成 CreateCore(level, type, tier, affix, bool applyMaxTierAndFinalBoss, bool applyChapterBias)，CreateForLevel 传 true/true，CreateForEndlessFloor 传 false/false 再乘 EndlessBaseFactor；不要复制两份公式，避免以后改一处漏一处。

形式化：

    HP      = BaseHp(level)      × TypeHp      × TierHp      × AffixHp      × EndlessBaseFactor
    Attack  = BaseAttack(level)  × TypeAttack  × TierAttack  × AffixAttack  × EndlessBaseFactor
    Defense = BaseDefense(level) × TypeDefense × TierDefense × AffixDefense × EndlessBaseFactor
    Speed   = BaseSpeed(level)   × TypeSpeed   × TierSpeed

其中（沿用现有公式与字段名，steps = level − 1）：

    BaseHp(level)      = MonsterBaseMaxHp      + MonsterMaxHpPerLevel      × steps
    BaseAttack(level)  = MonsterBaseAttack     + MonsterAttackPerLevel     × steps
    BaseDefense(level) = MonsterBaseDefense    + MonsterDefensePerLevel    × steps
    BaseSpeed(level)   = MonsterBaseSpeed      + MonsterSpeedPerLevel      × steps

### 5.2 相对强度表

以 Lv.110 普通怪为 1；战役终局 BOSS 是 2.5（Boss 档次）× 3（MaxTier）× 1.6（FinalBoss）= 12 倍。无尽 BOSS 是 2.5（Boss 档次）× 2.8（EndlessBaseFactor）= 7.0 倍，再乘等级增长：

| 层 | 等级 | 最后一只 | 相对 Lv.110 基础（HP/攻/防） |
|---:|---:|---|---:|
| 1（普通） | 110 | 普通 | 1.00 |
| 1（最后精英） | 110 | 精英 | 1.50 |
| 5 | 134 | BOSS | 3.04 |
| 10 | 164 | BOSS | 3.71 |
| 20 | 224 | BOSS | 5.06 |
| 30 | 284 | BOSS | 6.40 |
| 40 | 344 | BOSS | 7.75 |
| 50 | 404 | BOSS | 9.09 |
| 100 | 704 | BOSS | 15.82 |

解读：

- 第 1 层普通怪只有 11-10 普通怪（3×）的三分之一，作为热身；
- 第 5 层 BOSS 约 3 倍，明显低于 11-10 BOSS 的 12 倍，玩家应该能过；
- 约第 70 层 BOSS 才追平 11-10 BOSS，之后才真正超过终局；
- 目标是让满装玩家在 35~50 层之间首次阵亡；2026-10-19 探针实测中位 45 层。

如果首版实测太简单/太难，优先调 EndlessBaseFactor 和 EndlessMonsterLevelStep，不要动战役的 MaxTier 或 FinalBossStatFactor。

### 5.3 击杀目标与节奏

默认 EndlessKillTargetBase=3、EndlessKillTargetEvery=5、EndlessKillTargetMax=6：

- 第 1~5 层：3 只（2 普通 + 1 精英/BOSS）
- 第 6~10 层：4 只
- 第 11~15 层：5 只
- 第 16 层起：6 只

按每只战斗 3~8 秒、间隔 0.5 秒估算，一层约 15~50 秒；50 层约 15~40 分钟。这个长度适合「一局」，不会像 100 层小怪那样变成体力活。若探针发现单层超过 60 秒，优先检查玩家伤害是否被防御曲线拖住，而不是继续加怪。

### 5.4 层通过金币

    EndlessFloorGold(floor)      = EndlessFloorGoldBase + EndlessFloorGoldPerFloor × (floor − 1)
    EndlessFirstClearGold(floor) = EndlessFirstClearGoldBase + EndlessFirstClearGoldPerFloor × (floor − 1)

初版：

| 参数 | 值 | 含义 |
|---|---:|---|
| EndlessFloorGoldBase | 300 | 每层基础金币 |
| EndlessFloorGoldPerFloor | 40 | 每层递增 |
| EndlessFirstClearGoldBase | 1,000 | 新纪录首通基础奖金 |
| EndlessFirstClearGoldPerFloor | 150 | 新纪录每层递增 |

示例：第 1 层通过 = 300；第 1 层新纪录 = 300 + 1,000 = 1,300；第 50 层通过 = 2,260；第 50 层新纪录 = 2,260 + 8,350 = 10,610。

FloorGoldOf / FirstClearGoldOf 内部用 long 计算，最后夹到 0..int.MaxValue；避免 floor 接近 9999 且配置为上限时 int 溢出。

这些金币相对于洗练/强化是小的，但稳定；它的作用不是取代终局金币出口，而是让「爬层」本身有即时反馈。探针要监控 50 层累计金币是否超过终局金币量级（P200 满级约 560 万，P1000 约 8.59 亿）；如果超过，降低 EndlessFloorGoldPerFloor，不要降低首通奖金（首通是纪录奖励）。

### 5.5 经验与掉落

- 击杀经验：沿用 GrantKillReward，但无尽分支用 Mathf.Min(Monster.level, EndlessDropLevelCap) 作为经验等级，封顶 150。理由：第 100 层怪物等级 704，如果直接按 704 发经验，会瞬间把经验曲线打穿；玩家在解锁无尽时通常已满级，经验本来也不是主要目标。击杀不发金币，金币主要来自掉落卖店与层奖励。
- 装备掉落：沿用 ResolveDrops 的必掉/品质权重规则。普通怪仍用 Player.level（≤ MaxLevel=100）；精英/BOSS 用 Mathf.Min(Monster.level, EndlessDropLevelCap)。品质仍按原概率与等级权重；只是等级封顶 150。这样高层仍可能掉出更好的词条/品质，但不会出现 Lv.300 装备把玩家强度推到失控。
- 首通奖励：只发金币，不发装备。避免「首通层数越高，装备越强」的正反馈把纪录变成装备等级竞赛。
- 待选/自动换装：完全沿用现有逻辑；无尽里掉落更强装备仍会进入待选/自动穿戴，锁定与对比面板可用。

## 6. 最高层记录

### 6.1 记录语义

| 概念 | 定义 | 存放 |
|---|---|---|
| 本档最高层 endlessBestFloor | 本存档已清除的最高层 | PlayerData → SaveData（v7） |
| 全局最高层 | 本机三个存档中已清除的最高层 | PlayerPrefs |
| 本局清除层数 | 本局实际清完的层数；从检查点开始时从 0 计 | 运行时 EndlessRunState，不存档 |
| 本局到达层 | 死亡/撤退时所在的层（可能没清完） | 运行时，仅结果面板显示 |
| 本档历史榜 endlessTop | 最近 5 局「打完」的成绩（层数/清除/巅峰/用时/结局） | PlayerData → SaveData（v11，§6.5） |

规则：

1. 只有清完一层才更新最高层。死在第 N 层不会把 N 记为最高层；撤退同理。最高层 = max(旧最高, 起始层 − 1 + 本局清除层数)。
2. 从检查点起始（best−EndlessCheckpointOffset）不会改变「最高层」语义：清掉比旧最高更高的层才算新纪录。
3. 平纪录不更新、不发首通奖金。
4. 新纪录即时 SaveSystem.Save(Player)，不依赖 5 秒自动存档。
5. 全局最高层在每次本档新纪录时更新；如果读档时发现本档最高 > 全局最高（例如 PlayerPrefs 被清），用本档值回填全局。
6. 退出游戏/断电：本局丢失，但已清层的纪录与首通金币已保存；下次启动不会恢复未完成的一局。

### 6.2 为什么不是「到达层」

如果记录「到达层」，玩家可以在检查点开始后故意冲进下一层、不打完就退出/送死，纪录虚高；而且「到达」无法区分「清完」和「见到」。用「已清除层」：

- 语义可验证：每一层都真的打完了；
- 与首通金币对齐：只有新纪录才发首通；
- 结果面板可以明确写「第 N 层阵亡 / 本局清除 X 层」；检查点开始时 X 从 0 计，不把检查点前的层算成本局成绩。

### 6.3 全局记录

    const string GlobalBestKey = GameConfig.SaveKey + ".Endless.Best";

EndlessRecord 静态类：

    public static int GlobalBest =>
        Mathf.Clamp(PlayerPrefs.GetInt(GlobalBestKey, 0), 0, 9999);

    public static void ReportGlobal(int floor)
    {
        floor = Mathf.Clamp(floor, 0, 9999);
        if (floor <= GlobalBest) return;
        PlayerPrefs.SetInt(GlobalBestKey, floor);
        PlayerPrefs.Save();
    }

    public static void SyncGlobal(int floor)
    {
        if (floor > GlobalBest) ReportGlobal(floor);
    }

- 全局记录是本机记录，跨三个存档，不随删档消失；重装游戏/清 PlayerPrefs 会丢。
- 删档只删本档最高；全局最高保留。
- 新建档本档最高 0，但面板仍显示全局最高，作为追赶目标。
- 不做服务器校验，PlayerPrefs 可被手工修改；这是单机游戏，接受这一点。

### 6.4 展示位置

1. 无尽面板：本档最高 / 全局最高 / 起始层 / 本局层数。
2. 存档卡：在「首通 x/110 关」后加一行：

       无尽 最高 12 层

   没有纪录时显示「无尽 最高 —」；未解锁时显示「无尽 未解锁」（用 Player.IsStageFirstCleared(ChapterCount, StagesPerChapter) 判断；不要用 clearedCount >= totalStages，缺关/手改档会误判）。存档卡内容区高度 114，当前 5 行，加一行仍能放下（字号 14，行高约 17）。
3. HUD：无尽进行中，左栏关卡按钮显示「第 N 层 · Lv.X」；击杀进度显示在怪物卡/状态行（沿用当前关卡击杀文本的位置）。
4. 横幅：新纪录时「★ 新纪录 · 无尽第 N 层」，副标题「本档最高 N 层 · +X 金币」；非新纪录只显示「第 N 层通过」。
5. 日志：「◆ 无尽第 N 层开始」「★ 第 N 层通过」「★ 新纪录：无尽第 N 层」「第 N 层阵亡，本局结束」「已撤退（第 N 层）」。

### 6.5 Top-5 历史榜（v11，2026-10-22）

`endlessBestFloor` 只回答「这档最强到哪」，回答不了「哪一局打得最好、什么时候、花了多久」。v11 起每档额外保存最近 5 局**打完的**成绩。

数据结构（Assets/Scripts/Endless.cs）：

```csharp
[Serializable] public sealed class EndlessScoreEntry
{
    public int floor;      // 到达层（可能没清完，等于结束时的 run.floor）
    public int cleared;    // 本局清完的层数（检查点开局从 0 计）
    public int kills;      // 本局击杀数
    public int paragon;    // 结束时巅峰等级
    public int rebirth;    // 结束时转生次数（界面显示为「第 rebirth+1 世」）
    public int seconds;    // 用时，结束那一刻锁定（EndlessRunState.finishSeconds）
    public long endedUtc;  // 结束时刻，DateTime.UtcNow.Ticks（本地显示为 MM-dd HH:mm）
    public int reason;     // EndlessEndReason 的整型值：1=Death 2=Retreat
}
```

`PlayerData.endlessTop` 是 `List<EndlessScoreEntry>`，最多 5 条（`EndlessHistory.Capacity`）。存档 v11；v10 及更旧档读入时得到空列表。

排序（`EndlessHistory.Compare`）：

1. `floor` 降序——比的是这一局把纪录推到哪；
2. `cleared` 降序——同层时清得多的更好（检查点开局的层数差异）；
3. `endedUtc` 升序——**先达成的站住**，后来者同成绩排后面；
4. `seconds` 降序——同层同清同刻（理论上不会发生）时给用时长的。

写入时机：只在 `EndEndlessRun` 收到 `Death` / `Retreat` 时调用 `RecordEndlessHistory`。

| 结束原因 | 进榜 | 理由 |
|---|---|---|
| Death（阵亡） | 是 | 一局真的打完了 |
| Retreat（撤退） | 是 | 主动收手，同样是一局的完整成绩 |
| SlotChanged（换档）/ Reset（重置） | 否 | 与 §4.1 第 4 条「退出游戏放弃本局」同义，中途掐掉不给成绩，也避免切档刷榜 |

细节：

- 上榜才 `SaveSystem.Save(Player)`；排到第 6 名的不写盘，避免无意义 IO。
- 名次写回 `EndlessRunState.historyRank`（1..5，0 = 没上榜），结果面板第三行据此显示「历史榜第 N 名 / 未进历史榜前 5」。
- 「◀ 本次」标记：`GameController.LastEndlessRecordUtc` 记住刚记录那一局的 `endedUtc`，历史榜行比对时间戳。**不能**用 `historyRank` 比对——点「返回战役」时 `EndlessRun` 已被置空。
- 用时必须在结束那一刻锁定（`finishSeconds`），否则结果面板开着时 `Seconds` 还会继续涨。
- `EndlessHistory.Clamp` 对读档/写入的每条做夹取：floor/cleared ∈ 0..9999、kills ∈ 0..1e8、paragon ∈ 0..1000、rebirth ∈ 0..1e6、seconds ∈ 0..30 天、reason 越界归 None、endedUtc < 0 归 0。坏档不会让面板崩。
- 删档/重置会重建 PlayerData，历史榜随档清空（与 endlessBestFloor 一致）；全局最高层仍在 PlayerPrefs 里保留。
- 老档不补历史：一条 bestFloor 无法反推单局成绩，宁可空着。

实测（2026-10-22，Play 模式）：两次真实无尽局（第 15 层 / 1m10s / 撤退、第 5 层 / 17s / 撤退）正确入榜并按 floor 排序；存档 JSON 出现 `version:11` 与 `endlessTop:[{floor:15,cleared:14,kills:59,paragon:177,rebirth:0,seconds:70,...}]`；v10 档（手工把版本改回 10 并删掉 endlessTop）读入后 `endlessTop.Count == 0`、等级与 bestFloor 不变。

### 6.6 Top-5 不上榜的边界

- 局内退出游戏：本局直接消失，连「未完成」都不记（与 §4.1 一致）。
- 换档/重置：`EndEndlessRun(SlotChanged|Reset)` 不记录，因此不能用「切档再切回」制造成绩。
- 同一局的重复结束：`EndEndlessRun` 开头有 `if (Mode != GameMode.Endless) return;`，第二次调用不会重复入榜。

## 7. 存档 v7（历史，当前 v11）

### 7.1 字段

SaveData 新增：

    public int endlessBestFloor;   // 0 = 还没有纪录

PlayerData 新增同名公开字段，SaveSystem.Save 复制，Load 读回：

    data.endlessBestFloor = Mathf.Clamp(data.endlessBestFloor, 0, 9999);
    player.endlessBestFloor = data.endlessBestFloor;

CurrentSaveVersion: 6 → 7。

### 7.2 迁移

- version < 7：endlessBestFloor = 0，其余字段按现有 v6 迁移逻辑处理。
- version >= 7：读字段并夹取；缺失（手工改 JSON/旧档）按 0。
- 不做首通补记：缺少 11-10 首通记录的旧档保持未解锁，需要重新通关一次。
- Load 结束时调用 EndlessRecord.SyncGlobal(player.endlessBestFloor)，用本档回填全局。
- 不需要给 Equipment 加字段，EquipmentFieldVersion 不变。

### 7.3 SlotInfo 与存档卡

SaveSystem.SlotInfo 新增：

    public int endlessBestFloor;

Describe(slot) 从 SaveData 读；空档/坏档填 0。

DescribeSlotText：

    Lv.100   巅峰 168
    第 11 章 11-10
    金币 1,234,567
    首通 42/110 关
    无尽 最高 12 层

最后一行在未解锁时显示「无尽 未解锁」，已解锁但 0 层显示「无尽 最高 —」。

### 7.4 重置 / 切档 / 删档

| 操作 | 本档最高 | 全局最高 | 当前无尽局 |
|---|---|---|---|
| 切档 | 保留各档自己的值 | 保留 | 先 EndEndlessRun(SlotChanged)（不弹结果），再切 |
| 删当前档 | 归 0（新 Player 默认） | 保留 | 先结束 |
| 删其他档 | 不影响当前档 | 保留 | 不影响 |
| 重置游戏 | 归 0 | 保留 | 先结束 |
| 重装/清 PlayerPrefs | 存档里仍有 | 丢失 | 无 |

ResetSessionState() 需要追加：

    Mode = GameMode.Campaign;
    EndlessRun = null;
    EndlessPanelOpen = false;
    EndlessResultOpen = false;

但不要清 Player.endlessBestFloor——它是存档字段，由 SwitchSlot/Load 或新 PlayerData 决定。

## 8. 界面

### 8.1 左栏入口（已在上文）

    [ 11-10 · Lv.110 ] [ 无尽 ]

- 未解锁：无尽按钮文字灰，点击打开只读说明面板，并写日志「通关第 11 章 11-10 后解锁无尽模式」。
- 已解锁但没在局内：文字正常。
- 局内：关卡按钮「第 12 层 · Lv.132」，无尽按钮「记录」。
- 按钮宽度只有 60，不放图标；「∞」符号在当前字体不一定有，统一用「无尽」。

### 8.2 无尽面板

尺寸 680×436，居中 (300,142)，与洗练/强化面板同一套 CreatePanel(..., OverlayBg, true) + 55% 黑遮罩。打开时冻结整局。

    ┌ 无尽模式 ──────────────────────────────────────── [关闭] ┐
    │ 本档最高  第 12 层          全局最高  第 18 层          │
    │ 起始层    第 8 层（本档最高 − 偏移）                      │
    │                                                          │
    │ [规则] [历史榜]                                          │
    │ · 每层清完立刻进入下一层；每 5 层一个 BOSS。              │
    │ · 死亡或撤退结束本局；最高层只记「已清除」的层。          │
    │ · 每局结束会记进本档历史榜（最多 5 局）；中途退出/换档/重置不算。│
    │ · 掉落等级封顶 150；层通过给金币，新纪录额外给首通金币。  │
    │                                                          │
    │ [开始挑战] [从第 1 层开始] [返回战役]                    │
    │ （局内变成 [继续战斗] [撤退]，撤退需点两次）              │
    │ 提示：退出游戏会放弃本局，但已清层的纪录会保留。          │
    └──────────────────────────────────────────────────────────┘

状态：

- 未解锁：开始按钮可见但置灰，点击只写日志；规则照常显示；提示「通关第 11 章 11-10 后解锁」。
- 已解锁、未开局：开始按钮可用；「从第 1 层开始」仅在起始层 > 1 时显示；「返回战役」可见。
- 局内：标题变成「无尽模式 — 第 N 层」；显示本局击杀进度；按钮变成「继续战斗」「撤退」；不显示「开始挑战」「返回战役」。
- 撤退按钮第一次点击后 3 秒内显示「确认撤退 x.xs」，再点一次才结束本局；关闭面板会清掉倒计时。
- 结果面板打开时：不再打开无尽面板（结果面板优先）。

实现上新增 GameUI.Endless.cs（第八个 partial），不要继续把逻辑塞进 GameUI.Panels.cs；该文件目前已经 1,263 行。

实际字段（不是早期草案）：

    RectTransform _endlessPanel, _endlessResultPanel;
    Text _endlessTitle, _endlessBestText, _endlessGlobalText, _endlessStartText,
         _endlessRulesText, _endlessHintText, _endlessResultTitle, _endlessResultBody;
    Button _endlessStartButton, _endlessFreshButton, _endlessRetreatButton, _endlessReturnButton;
    Text _endlessRetryLabel, _endlessResultReturnLabel;
    float _endlessRetreatConfirmUntil;

    // v11 历史榜页签（规则/历史榜共用同一块内容区）
    Button _endlessRulesTab, _endlessHistoryTab;
    Text _endlessRulesTabLabel, _endlessHistoryTabLabel;
    Text _endlessHistoryHint, _endlessHistoryEmpty;
    Image[] _endlessHistoryRows;        // 5 行卡片
    Text[] _endlessHistoryRowTexts;
    bool _endlessHistoryOpen;

历史榜态（§6.5）：

    ┌ 无尽模式 ──────────────────────────────────────── [关闭] ┐
    │ 本档最高  第 14 层          全局最高  第 250 层         │
    │ 起始层    第 10 层（本档最高 − 4）                        │
    │ [规则] [历史榜]                                          │
    │ 本档历史最好 5 局 · 按到达层数 → 本局清除 → 先达成排序    │
    │ 1  第 15 层   清 14 层 · P177 · 1m10s · 10-22 01:16 · 撤退│
    │ 2  第 5 层    清 4 层 · P177 · 17s · 10-22 01:17 · 撤退 ◀ 本次│
    │                                                          │
    │ [开始挑战] [从第 1 层开始] [返回战役]                    │
    └──────────────────────────────────────────────────────────┘

- 页签常驻显示，不因无数据隐藏；空态（「还没有完成的无尽局。阵亡或撤退结束一局后，这里会记下成绩（中途退出/换档不算）」）才是没有数据的唯一表达。
- 页签状态 `_endlessHistoryOpen` 跨开合保留：本次会话看过历史榜，下次打开还是历史榜。
- 「◀ 本次」只在本次会话刚打完的那一局上出现（比对 `LastEndlessRecordUtc`）。

### 8.3 结果面板

560×300，居中 (360,210)；模态，背景压暗。

    ┌ 本局结束 ─────────────────────────────────────┐
    │ 第 13 层阵亡                                   │
    │ 本局清除 12 层 · 本档最高 12 层 · 全局最高 18 层 │
    │ 用时 1m10s · 历史榜第 1 名                     │
    │                                                │
    │ [再次挑战（第 8 层）] [返回战役]               │
    └────────────────────────────────────────────────┘

- 标题按结束原因：「第 N 层阵亡」/「已撤退（第 N 层）」。切档/重置不弹结果面板，由调用方直接复位；不要为这两种原因显示「本局已结束」。
- 如果没有清除任何层：「尚未清除任何层」。
- 结果面板没有「关闭」按钮；必须选一个。切档/删档时由代码强制关闭并返回战役。
- 「再次挑战」按钮文本带起始层，让玩家知道会从哪开始。

### 8.4 HUD 与横幅

| 位置 | 战役 | 无尽 |
|---|---|---|
| 左栏关卡按钮 | 11-10 · Lv.110 | 第 N 层 · Lv.X |
| 左栏第二按钮 | 无尽 | 记录 |
| 击杀进度 | 击杀 x/y | 击杀 x/y（y 为本层目标） |
| 怪物卡标题 | 怪物名 + Lv. | 不变（等级显示 Lv.X） |
| 状态行 | 等待/战斗中 | 不变 |
| 横幅 | 关卡通过/首通 | 层通过/新纪录 |
| 日志 | 原有 | 无尽专属文案 |

不新增独立的「无尽 HUD 条」：左栏两个按钮 + 怪物卡已经能表达「第几层、打谁、还剩几只」，再加一条会挤压右栏日志。

### 8.5 面板互斥与冻结

沿用现有规则，但新增两个面板：

- GameController.Update 的冻结条件追加 EndlessPanelOpen || EndlessResultOpen；
- GameUI.RefreshHud 的 modalOpen 追加这两个；
- CloseAllPanels() 追加 CloseEndlessPanel() / CloseEndlessResult()；
- 局内点「记录」打开无尽面板时冻结战斗，点「继续战斗」关闭并恢复；
- 结果面板打开时，其他面板一律不能打开（优先处理结果）；
- 技能三选一、掉落待选、商店、强化、洗练、设置、对比在无尽中照常工作，打开时同样冻结。

## 9. 状态机与代码接口

### 9.1 类型

新增 Assets/Scripts/Endless.cs：

    namespace AutoBattle
    {
        public sealed class EndlessFloorDef
        {
            public int floor;
            public int monsterLevel;
            public int killTarget;
            public MonsterTier clearTier;
            public bool IsBossFloor;
            public MonsterType BossType;
        }

        public enum EndlessEndReason { Death, Retreat, SlotChanged, Reset }

        public sealed class EndlessRunState
        {
            public int floor;
            public int startFloor;
            public int killCount;
            public int runKills;
            public int clearedThisRun;   // 本局实际清完的层数，初始 0；每清一层 +1
            public EndlessEndReason endReason;
            public int reachedFloor;     // 死亡/撤退时所在层，仅结果面板显示
        }

        public static class EndlessCatalog
        {
            public static EndlessFloorDef Get(int floor);
            public static int MonsterLevelOf(int floor);
            public static int KillTargetOf(int floor);
            public static MonsterTier ClearTierOf(int floor);
            public static MonsterType BossTypeOf(int floor);
            public static int FloorGoldOf(int floor);
            public static int FirstClearGoldOf(int floor);
            public static int StartFloorFor(int best);
        }

        public static class EndlessRecord
        {
            public const string GlobalBestKey = GameConfig.SaveKey + ".Endless.Best";
            public static int GlobalBest { get; }
            public static void ReportGlobal(int floor);
            public static void SyncGlobal(int floor);
        }
    }

所有公开 API 的 floor 参数先 Mathf.Clamp(floor, 1, 9999)；EndlessCatalog.Get 内部再夹一次，避免调试/坏档传 0、负数或过大值。9999 层是实际纪录上限（0..9999），对玩家等同于无限。

GameMode 放在 GameController 内部或 Endless.cs，二选一；建议放 Endless.cs，避免 GameController 顶部再堆类型。

### 9.2 GameController

新增：

    public GameMode Mode { get; private set; }
    public EndlessRunState EndlessRun { get; private set; }
    public bool EndlessActive => Mode == GameMode.Endless && EndlessRun != null;
    public bool EndlessUnlocked =>
        Player.IsStageFirstCleared(GameConfig.ChapterCount, GameConfig.StagesPerChapter);
    public int EndlessFloor => EndlessActive ? EndlessRun.floor : 0;
    public int EndlessMonsterLevel => EndlessActive ? EndlessCatalog.MonsterLevelOf(EndlessRun.floor) : 0;
    public int EndlessKillsLeft => EndlessActive
        ? Mathf.Max(0, EndlessCatalog.KillTargetOf(EndlessRun.floor) - EndlessRun.killCount)
        : 0;
    public bool EndlessPanelOpen { get; private set; }
    public bool EndlessResultOpen { get; private set; }

    public void ToggleEndlessPanel();
    public void CloseEndlessPanel();
    public void StartEndlessRun(int startFloor);
    public void RetryEndlessRun();
    public void RetreatEndlessRun();          // 二次确认由 UI 负责
    public void ReturnToCampaign();
    private void HandleEndlessMonsterKilled();
    private void ResolveEndlessFloorClear();
    private void SpawnEndlessMonsterNow();
    private void EndEndlessRun(EndlessEndReason reason);

修改：

| 位置 | 改法 |
|---|---|
| Update | 增加 State.Ended：只更新 UI/速度/面板，不推进战斗；冻结条件加两个面板 |
| SpawnMonster / SpawnMonsterNow | SpawnMonster 在检查 _pendingTier 前补 killTarget=1 的 clearTier；SpawnMonsterNow 最前面分支：EndlessActive 时用 EndlessCatalog 的等级/档次/类型，清 _pendingTier 后调 CreateForEndlessFloor，再走原有 CurrentState/BeginBattle/日志；不要进入 IsFinalStage 分支 |
| HandleMonsterKilled | 最前面分支：TotalKills/档次统计 → run.killCount/runKills++ → GrantKillReward → ResolveLevelUp → ResolveDrops →（未清层）EnterRespawnWait(MonsterRespawnDelay) /（清层）ResolveEndlessFloorClear → pendingSkillPicks>0 时 OpenSkillChoice；不调 ResolveStageProgress，不碰 stageKillCount |
| HandlePlayerDied | 最前面分支：无尽 → TotalDeaths++ + EndEndlessRun(Death)；直接 return |
| GrantKillReward | 无尽分支用 Mathf.Min(Monster.level, EndlessDropLevelCap) 计算经验（AddLog 仍显示原 Monster.Title/等级）；击杀不发金币 |
| ResolveDrops | 无尽精英/BOSS 的 dropLevel 夹到 EndlessDropLevelCap；普通怪仍 Player.level；必掉/品质权重不变 |
| ResolveStageClear | 只在战役调用；无尽走 ResolveEndlessFloorClear |
| SelectStage | 无尽进行中或结果面板打开时拒绝并写日志「先撤退再选关」；从结果返回战役后才可切关 |
| ToggleStagePanel | 同上拒绝 |
| SwitchSlot / DeleteSlot / ResetGame | SwitchSlot 与 ResetGame 先 EndEndlessRun(SlotChanged/Reset)；DeleteSlot 只有在删当前档时才先 EndEndlessRun(SlotChanged)，删其他档不影响当前局。之后执行原有重置；结果面板强制关闭 |
| CloseAllPanels | 加 CloseEndlessPanel；结果面板故意不关（必须选「再次挑战」或「返回战役」），切档/重置由代码强制关闭 |
| ResetSessionState | 清运行时的无尽模式、局、面板；不清 Player.endlessBestFloor |
| EnterRespawnWait | 签名改为 EnterRespawnWait(float delay = -1f)：delay >= 0 时用它，否则按 _justClearedStage 选 StageClearDelay / MonsterRespawnDelay；无尽层通过传 EndlessFloorClearDelay（战斗秒），战役调用点不变 |
| ShowClearBanner | 11-10 首通时追加「无尽模式已解锁」 |

### 9.3 怪物生成

    if (EndlessActive)
    {
        EndlessFloorDef def = EndlessCatalog.Get(EndlessRun.floor);
        MonsterTier tier = _pendingTier;
        _pendingTier = MonsterTier.Normal;
        MonsterType type = def.IsBossFloor && tier == MonsterTier.Boss
            ? def.BossType
            : MonsterData.RollType(def.monsterLevel);   // 与战役一致：按本层怪级抽
        MonsterAffix affix = tier == MonsterTier.Boss ? MonsterAffix.None : MonsterData.RollAffix();
        if (tier == MonsterTier.Boss)
        {
            Player.FillHp();       // 与战役 BOSS 一致
            Player.shield = 0;
        }
        Monster = MonsterData.CreateForEndlessFloor(def.floor, type, tier, affix);
        CurrentState = State.Fighting;
        _battle.BeginBattle(Player);
        // 之后走原有的生成日志；不要提前 return
    }
    else if (IsFinalStage) { ... 原逻辑 ... }
    else { ... 原逻辑 ... }

注意：_pendingTier 初值 Normal；每层最后一只前设为 def.clearTier。BOSS 层的 clearTier = Boss，所以最后一只一定是 def.BossType，不会随机。

- 普通怪类型按 def.monsterLevel（本层怪级）抽，不按 Player.level；这与战役 SpawnMonsterNow 的 RollType(monsterLevel) 保持一致，避免满级玩家回到低层时抽到终局档类型。
- SpawnMonsterNow 的无尽分支要自己清 _pendingTier（与战役同），并在 CreateForEndlessFloor 之后继续走原有的 CurrentState = Fighting、_battle.BeginBattle(Player) 与生成日志；不要只创建 Monster 就返回。

### 9.4 UI

新增 GameUI.Endless.cs：

    BuildEndlessPanel(Transform root);
    BuildEndlessResult(Transform root);
    RefreshEndlessPanel();
    RefreshEndlessResult();
    OnEndlessEntryClicked();
    OnEndlessStartClicked();
    OnEndlessFreshClicked();
    OnEndlessRetreatClicked();
    OnEndlessRetryClicked();
    OnEndlessReturnClicked();

修改：

| 位置 | 改法 |
|---|---|
| GameUI.cs BuildLeftPanel | 关卡按钮宽度 216→150，新增 _endlessButton/_endlessButtonLabel；字号 13 |
| GameUI.cs BuildCanvas | 在 BuildReforgePanel 后、BuildBanner 前加 BuildEndlessPanel(root); BuildEndlessResult(root); |
| GameUI.Core.RefreshHud | 增加无尽分支：关卡文本、击杀进度、按钮文字；modalOpen 加两个面板 |
| GameUI.Core.RefreshPanels | 加 RefreshEndlessPanel(); RefreshEndlessResult(); |
| GameUI.Panels.RefreshStagePanel | 无尽进行中不刷新/不打开；签名加 Mode 以防切模式后文本不更新 |
| GameUI.Panels.DescribeSlotText | 加无尽最高层一行 |
| 存档卡 RefreshSavePanel | SlotInfo.endlessBestFloor 传入 DescribeSlotText |

## 10. 配置

### 10.1 新增字段

GameConfigData（当前 121 个 public 字段，其中本次新增 14 个）新增 14 个，GameConfig 同名 static 字段同步，game_config.json 补键：

| 字段 | 默认 | 夹取范围 | 说明 |
|---|---:|---|---|
| EndlessBaseMonsterLevel | 110 | 1..9999 | 第 1 层怪物等级 |
| EndlessMonsterLevelStep | 6 | 1..100 | 每层等级增量 |
| EndlessBossEvery | 5 | 1..100 | 每几层一个 BOSS |
| EndlessKillTargetBase | 3 | 1..100 | 每层基础击杀数 |
| EndlessKillTargetEvery | 5 | 1..100 | 每几层 +1 击杀 |
| EndlessKillTargetMax | 6 | ≥base..100 | 击杀数上限 |
| EndlessBaseFactor | 2.8 | 0.1..100 | 无尽怪物 HP/攻/防平坦倍率 |
| EndlessFloorClearDelay | 2.0 | 0..60 | 层通过等待（战斗秒） |
| EndlessFloorGoldBase | 300 | 0..1,000,000 | 每层通过基础金币 |
| EndlessFloorGoldPerFloor | 40 | 0..1,000,000 | 每层金币递增 |
| EndlessFirstClearGoldBase | 1,000 | 0..1,000,000 | 新纪录首通基础金币 |
| EndlessFirstClearGoldPerFloor | 150 | 0..1,000,000 | 新纪录首通金币递增 |
| EndlessDropLevelCap | 150 | 1..9999 | 掉落/奖励等级封顶 |
| EndlessCheckpointOffset | 4 | 0..100 | 起始层 = 最高 − 偏移 |

GameConfig.Loader.SanitizeValues 追加：

    ClampInt(ref EndlessBaseMonsterLevel, 1, 9999);
    ClampInt(ref EndlessMonsterLevelStep, 1, 100);
    ClampInt(ref EndlessBossEvery, 1, 100);
    ClampInt(ref EndlessKillTargetBase, 1, 100);
    ClampInt(ref EndlessKillTargetEvery, 1, 100);
    ClampInt(ref EndlessKillTargetMax, EndlessKillTargetBase, 100);
    ClampFloat(ref EndlessBaseFactor, 0.1f, 100f);
    ClampFloat(ref EndlessFloorClearDelay, 0f, 60f);
    ClampInt(ref EndlessFloorGoldBase, 0, 1000000);
    ClampInt(ref EndlessFloorGoldPerFloor, 0, 1000000);
    ClampInt(ref EndlessFirstClearGoldBase, 0, 1000000);
    ClampInt(ref EndlessFirstClearGoldPerFloor, 0, 1000000);
    ClampInt(ref EndlessDropLevelCap, 1, 9999);
    ClampInt(ref EndlessCheckpointOffset, 0, 100);

EndlessCatalog 仍要做防御性 Max(1, every)，不要只依赖配置校验。

### 10.2 配置加载

- GameConfigData 与 game_config.json 必须同时加键；否则 ReportMissingKeys 会警告，且运行时用字段默认值。
- 配置字段数从 97 变 111；README/文档里的数字以后要同步。
- 现有 GameConfig.Apply 是反射写回，新字段自动生效；不需要额外映射代码。
- GameConfig.Loader.AutoLoad/Reload 已有 try/catch 和 SanitizeValues；新字段纳入同一套。

## 11. 边界情况

| 情况 | 行为 |
|---|---|
| 未通关 11-10 | 点入口打开只读说明面板；开始按钮置灰并写解锁提示 |
| 从检查点开始 | 起始层 = max(1, best − EndlessCheckpointOffset)；可点「从第 1 层开始」覆盖 |
| 起始层 > 最高层 | 不允许；StartEndlessRun 夹到 1..max(1, best) |
| 死在第 N 层 | 本局结束；最高层不因死亡增加；TotalDeaths++；不自动复活 |
| 撤退 | 二次确认；最高层不变；不增加 TotalDeaths |
| 清完第 N 层 | 最高层 = max(旧, N)；金币；即时存档；进入 N+1 |
| 平纪录 | 不发首通金币；横幅只显示「第 N 层通过」 |
| 退出游戏 / 断电 | 本局丢失；已清层纪录/首通金币已保存；下次不恢复局 |
| 切档 | 先 EndEndlessRun(SlotChanged)（不弹结果），再切；新档读自己的最高层 |
| 删当前档 | 先结束；本档最高归 0；全局最高保留 |
| 删其他档 | 当前局不受影响；全局最高保留 |
| 重置游戏 | 先结束；本档最高归 0；全局最高保留 |
| 战役选关 | 无尽进行中拒绝；返回战役后才可切关 |
| 设置/商店/强化/洗练/对比/技能三选一 | 正常打开并冻结整局；关闭后继续 |
| 技能三选一未选 | 不会在无尽中死亡（该状态不推进战斗）；选择后继续 |
| 掉落待选 | 不因结束而清空；返回战役后仍可处理 |
| BOSS 层 | 最后一只固定 BOSS 类型；无词缀；战前回满血；商店提前开放 |
| 普通层最后一只 | 精英，可有词缀；不强制回血 |
| 高层掉落 | 装备等级 ≤ 150；品质/词缀仍可更好 |
| 高层经验/金币 | 击杀经验按 150 级封顶；击杀不发金币；层通过金币另算 |
| 1×/2×/4× 速度 | 只影响战斗计时；层通过等待也按战斗时间（x4 更短）；纪录不按时间排序 |
| 配置为 0/负/越界 | SanitizeValues 夹取；EndlessCatalog 再防御一次；不除零/不越界 |
| 坏档 endlessBestFloor=-5/999999999 | 夹到 0..9999；不崩溃 |
| v6 旧档 | 升 v7，最高层 0；已有 11-10 首通则解锁 |
| 全局最高 > 本档最高 | 面板显示全球纪录；起始层仍按本档最高算 |
| 本档最高 > 全局最高 | 读档时用本档值回填全局 |
| 结果面板打开时切档/重置 | 强制结束本局、关结果面板，再执行切档/重置 |
| 所有层都打不过 | 玩家可撤退回战役刷装备；不软锁 |

## 12. 平衡与探针

### 12.1 新探针 Assets/Editor/EndlessPacing.cs

目标：给出一组可重复的基线，而不是「感觉能打」。

输入：

- 满装玩家：Lv.100、巅峰 200、六个技能、传说 Lv.110 +15、词条质量约 80%；
- 自动换装开启、自动买药开启、1× 速度；
- 20 个随机种子；
- 最多模拟 100 层，或直到死亡。

输出：

| 指标 | 用途 |
|---|---|
| 首死层数分布（min / P10 / 中位 / P90 / max） | 主平衡指标 |
| 每层耗时（P50 / P90） | 检查单层是否过长 |
| 每层 HP 损失 / 药水消耗 | 检查 BOSS 检查点是否有效 |
| 50 层累计金币/经验 | 检查经济是否打穿 |
| 各层 BOSS 类型/击杀数 | 检查节奏 |

首版目标：

- 满装自动玩家首死中位数 35~50 层；
- P10 ≥ 25 层，P90 ≤ 70 层；
- 单层 P90 ≤ 60 秒；
- 第 1~10 层不出现死亡（热身区）；
- 50 层累计金币不超过终局量级（P200 约 560 万）的 30%。

这些是初始目标，不是硬门槛；探针跑完后按实测调整，不要为了数字改战役平衡。

### 12.2 调参顺序

1. 先调 EndlessBaseFactor（整体难度，最直观）。
2. 再调 EndlessMonsterLevelStep（曲线斜率）。
3. 再调 EndlessBossEvery（BOSS 频率；改这个会改层结构，谨慎）。
4. 最后调 EndlessKillTargetMax（单层长度，影响体验不影响单只强度）。

如果第 1 层就打不过：检查是否误用了 CreateForLevel 的终局倍率，而不是先降 EndlessBaseFactor。
如果 50 层后仍然一刀秒：检查 MaxTier/BOSS 倍率是否被重复乘了两次。
如果玩家在 20 层前必死：把 EndlessBaseFactor 从 2.8 降到 2.2 左右，或把 Step 从 6 降到 4（2026-10-19 实测 2.8/6 的首死中位为 45 层）。

### 12.3 经济监控

- 层通过金币 + 首通金币 + 掉落卖店，三者合计是无尽的主要金币来源。
- 首通金币只在新纪录发，重复刷低层只有层通过金币；这能防止「低层刷金币」比终局更快。
- 如果 50 层累计金币过高，先降 EndlessFloorGoldPerFloor，再考虑降 EndlessFloorGoldBase；不要动首通奖金，否则纪录激励变弱。
- 掉落等级封顶 150 后，卖店价格也按 150 级算；这会让高层金币增长主要来自层奖励，便于控制。

### 12.4 实测基线（2026-10-19）

探针：Tools/测试/无尽节奏探针（首死层数与耗时），20 个种子、最多 100 层、Random.InitState(baseSeed) 固定模板。

调参过程：

| EndlessBaseFactor / Step | 首死中位 | 结果 |
|---|---:|---|
| 2.5 / 2（原默认） | 无 | 20 局全部打满 100 层，难度过低 |
| 6 / 2 | 无 | 高防模板仍 100 层无死 |
| 8 / 2 | 15 | 过难 |
| 6.25 / 2 | 30 | 偏难 |
| 3 / 6 | 45 | 中位达标，但 0 局到 50 层 |
| **2.8 / 6（采用）** | **45** | 20 局全部阵亡，2 局到 50 层 |

最终基线（2.8 / 6）：

- 首死：min 30 / P10 30 / 中位 45 / P90 50 / max 60；
- 0 局打满 100 层，没有软锁；
- 单层耗时 P90 最大约 16.1 秒（远低于 60 秒目标）；
- 50 层经济（仅 2 局达到）：平均累计金币 371,940、经验 2,602,923；金币约为终局量级（P200 约 560 万）的 6%，仍偏宽松，但未打穿；
- 结论：落在首版目标 35~50 层的中位区间，采用 2.8 / 6。

### 12.5 最高数值上限实测（2026-10-19）

2026-10-19 之前，配置里的 `MaxParagonLevel` 还是 200（代码默认与加载器都允许到 1000，是 JSON 漏改）；现已改为 1000，并按 P1000 重测。下面 12.5.1 是两槽时代的 P1000 口径，12.5.2 是 P200 旧结果，12.5.3 是五槽 + 攻速软上限后的当前口径。

#### 12.5.1 P1000 两槽满配（历史口径）

下面这组是两槽装备时代的基线，保留用于对照；五槽 + 攻速软上限复测见 12.5.3。

`maxGear` 口径（`EndlessPacing.Run(..., maxGear: true)`）：传说 Lv.150、品质倍率取该品质上限 4.80、强化 +15、传说 4 条副词条全部取满值。武器副词条为暴击率 / 暴击伤害 / 攻击速度 / 全属性，衣服为暴击伤害 / 攻击速度 / 全属性 / 护甲穿透。技能由 `ProbePolicy` 自动选 6 个，不手工挑防御技能，因此这是「装备上限」口径，不是「手工最优技能」口径。

模板面板：Lv.100 / P1000 / 15 技能，攻 4,091 / 防 2,880 / 血 65,361 / 暴击 95.0% / 暴伤 276%。50 个固定种子、最多 300 层：

| 指标 | 结果 |
|---|---|
| 首死层数 | min 167 / P10 196 / 中位 221 / P90 235 / max 250 |
| 死亡分布 | 50 局全部阵亡，0 局打满 300 |
| 最高纪录 | 第 250 层 |
| 层通关率 | 1~165 层 100%；170 层 49/49；180 层 48/49；190 层 47/48；195 层 45/46；200 层 44/44；210 层 37/40（93%）；215 层 32/36（89%）；220 层 26/28（93%）；225 层 17/21（81%）；230 层 8/14（57%）；235 层 4/6（67%）；240 层 1/1；245 层 1/1；250 层 0/1 |
| 单层耗时 P90 | 50 层 7.8 秒、100 层 9.4 秒、150 层 13.8 秒、200 层 24.2 秒、230 层 24.5 秒 |
| 50 层经济 | 50 局到达，平均累计金币 4,764,593、经验 10,675,549；模板词条已满，洗练花费 0 |

结论：P1000 满配的无尽天花板约 **220~235 层**，最高纪录 250 层；230 层通过率 57%、235 层 67%（样本只剩 6 局）、250 层 0/1。200 层不再是软上限，但仍会死，不存在固定层数后必过。**近战 BOSS 仍是墙**：后期失败集中在近战层，远程/暴击层反而更容易；若要继续抬高天花板，优先检查近战 BOSS 的防御/生命倍率，而不是继续抬装备或巅峰上限。

#### 12.5.2 P200 旧上限对照（历史）

同口径、P200、200 个固定种子、最多 200 层：模板攻 3,291 / 防 2,080 / 血 25,361 / 暴击 95.0% / 暴伤 276%。首死 min 45 / P10 45 / 中位 60 / P90 60 / max 75，最高纪录第 74 层；45 层通过率 83%、60 层 7%、75 层 0%；50 层平均累计金币 411,234、经验 2,791,774。失败集中在第 60、75 层近战 BOSS（60 层 HP 104,328 / 防 12,490；75 层 HP 124,488 / 防 14,909），而远程 65 层、暴击 70 层反而 100% 通过。这组数据说明「近战 BOSS 高防高血卡攻击上限」的机制在 P200 就已经存在，P1000 只是把墙推到了 235 层附近。

#### 12.5.3 五槽 + 攻速软上限复测（2026-10-19 晚）

五槽（武器 / 衣服 / 头盔 / 鞋 / 饰品）与攻速软上限上线后，12.5.1 的两槽基线不再代表当前数值。

`maxGear` 口径：五件 Lv.150 传说、品质倍率取上限、强化 +15、传说 4 条副词条全部取满值；小件按 50% 词条缩放，且副词条池不出暴击率。模板面板：Lv.100 / P1000 / 15 技能，攻 4,429 / 防 6,755 / 血 70,035 / 速度 122 / 暴击 95.0% / 暴伤 368% / 穿甲 34.9%。50 个固定种子、最多 300 层：

| 指标 | 结果 |
|---|---|
| 首死层数 | min 255 / P10 273 / 中位 290 / P90 296 / max 300 |
| 死亡分布 | 28 局阵亡，22 局打满 300 |
| 层通关率（条件通过/到达该层） | 1~250 层 100%；255 层 49/50（98%）；270 层 48/49（98%）；275 层 45/46（98%）；280 层 41/43（95%）；290 层 34/36（94%）；295 层 26/29（90%）；300 层 22/25（88%） |
| 总体通过率（清层/50 局） | 250 层 100%；270 层 48/50（96%）；280 层 41/50（82%）；290 层 34/50（68%）；295 层 26/50（52%）；300 层 22/50（44%） |
| 单层耗时 P90 | 100 层 6.1 秒、150 层 9.2 秒、200 层 14.2 秒、250 层 12.3 秒、300 层 15.4 秒 |
| 50 层经济 | 50 局到达，平均累计金币 9,033,716、经验 14,390,475 |

作为对照，同参数但 `maxGear=false`、`affixQuality=0.85` 的随机五槽配装（3 种子）首死为 243 / 210 / 242，中位约 242 层。

结论：

- **理想配装的天花板升到 290 层**，高于原设计目标 235~250，也高于两槽时代的 221 层；
- **随机五槽配装的中位约 242 层**，仍落在原目标附近；
- 两者相差约 48 层，来自「五槽乘算 + 完美词条」；如果只按随机配装验收，目标仍成立，但「理论上限」已经越界。是否收紧无尽怪物成长、压缩完美词条收益，或接受双口径，待调；
- **整体通关率比中位层数更能说明尾部风险**：50 局里 44% 能打满 300 层，68% 能过 290 层，82% 能过 280 层；条件通过率在 280~300 层仍有 88%~95%，说明高层不是必死墙，而是逐层消耗后的概率衰减。
- 攻速软上限同时作用于怪物，后期近战 BOSS 的有效攻速被压到约 4.5 次/秒，对高层生存有正面影响，这也是理想配装变深的原因之一。

复现：

```powershell
unity command --format json eval -- --code 'return AutoBattle.EditorTools.EndlessPacing.Run(50, 300, 20261019, 1000, 150, 0.85f, true);'
```

复现（长任务用 `--detach`，避开 Pipeline 5 秒主线程限制）：

```powershell
unity command --detach --format json eval -- --code 'return AutoBattle.EditorTools.EndlessPacing.Run(50, 300, 20261019, 1000, 150, 1.0f, true);'
unity job wait <job-id>

unity command --detach --format json eval -- --code 'return AutoBattle.EditorTools.EndlessPacing.Run(200, 200, 20261019, 200, 150, 1.0f, true);'
unity job wait <job-id>
```

## 13. 验收清单

| # | 操作 | 预期 |
|---:|---|---|
| 1 | 新档未通关 11-10，点「无尽」 | 面板打开，开始按钮置灰，提示解锁条件 |
| 2 | 首通 11-10 | 横幅追加「无尽模式已解锁」；日志一条；入口可用 |
| 3 | 点「开始挑战」 | 进入第 1 层，HUD 显示「第 1 层 · Lv.110」，满血，击杀 0/3 |
| 4 | 清完第 1 层 | 最后一只为精英；层通过横幅；进入第 2 层；最高层 1 |
| 5 | 到第 5 层 | 最后一只为 BOSS，类型近战；BOSS 前商店开放、回满血 |
| 6 | 清第 5 层 | 最高层 5；首通金币；即时存档；重进游戏后仍为 5 |
| 7 | 第 6 层死亡 | 不复活；结果面板「第 6 层阵亡」「本局清除 5」；本档最高 5；TotalDeaths +1 |
| 8 | 点「再次挑战」 | 默认偏移下从第 1 层开始（best=5，5−4=1）；满血；新局 |
| 9 | 撤退 | 两次确认；结果「已撤退」；最高层不变；TotalDeaths 不变 |
| 10 | 返回战役 | 回到 11-10；满血；战役进度未变；可正常选关 |
| 11 | 无尽进行中点关卡/选关 | 被拒绝并提示先撤退 |
| 12 | 无尽进行中切档 | 当前局结束；新档读自己的最高层；全局最高保留 |
| 13 | 删除当前档 | 本档最高归 0；全局最高保留；无残留局 |
| 14 | 第 50 层掉落 | 装备等级 ≤150；品质/词缀正常；卖价按 150 级 |
| 15 | 第 50 层击杀经验/层通过金币 | 击杀经验按 150 级封顶；击杀不发金币；层通过金币另算 |
| 16 | v6 旧档 | 升 v7；最高层 0；已有 11-10 首通则解锁；其他进度不丢 |
| 17 | 坏档 endlessBestFloor=-5/999999999 | 夹到 0/9999；不崩溃 |
| 18 | 配置字段缺失/为 0/为负 | Loader 警告并夹取；无除零/越界/死循环 |
| 19 | 20 次探针 | 首死中位数在 35~50；单层 P90 ≤60s；无软锁 |
| 20 | 最终编译 | compilationFailed=false，consoleErrors=0，consoleWarnings=0 |

### 13.1 首轮验收结果（2026-10-19）

| # | 结果 | 说明 |
|---:|---|---|
| 1 | ✅ | 新档 slot2：未解锁时开始按钮置灰，提示「通关第 11 章 11-10 后解锁无尽模式。」 |
| 2 | ✅ | 新档造 109 关首通 + 11-10 5/6，BOSS 击杀后横幅「+1520 金币 · ✦ 无尽模式已解锁」、日志一条、入口可用 |
| 3 | ✅ | 反射 `StartEndlessRun(1)` 进入第 1 层，HUD/击杀数正常 |
| 4 | ✅ | 清层进第 2 层、新纪录横幅、最高层 +1 |
| 5 | ◐ | 第 5 层强制生成 BOSS 并清层成功；这次测试绕过了商店，商店开放未单独验 |
| 6 | ✅ | 等价验证：清到第 11 层后停止/重开 Play，best 仍为 11，落盘正确 |
| 7 | ✅ | 等价验证：反射 `HandlePlayerDied` 在第 12 层触发，结果面板、TotalDeaths、本档/全局最高、再次挑战层数均正确 |
| 8 | ✅ | best=11 时再次挑战从第 7 层开始 |
| 9 | ✅ | 撤退需二次确认；结果面板「已撤退」；最高层不变 |
| 10 | ✅ | 返回战役回到 11-10，mode=Campaign，可继续 |
| 11 | ✅ | 局内调用 `SelectStage(1,1)` 被拒，仍在无尽第 1 层 |
| 12 | ✅ | 局内切档：当前局结束、新档 best=0、全局最高 11 保留 |
| 13 | ✅ | 当前档 best 强写 7 后删除，归 0；全局最高保留 |
| 14 | ✅ | 强设第 50 层生成 BOSS：两件掉落均为 Lv.150（史诗），可正常卖出/自动穿戴 |
| 15 | ✅ | 同一次击杀经验 43,434（按 150 级算；404 级为 296,482）；击杀不发金币 |
| 16 | ✅ | v6 样本（无 endlessBestFloor，含 11-10 首通）：升 v7，best=0，Lv.80/1234 金币/109 关进度保留，已解锁 |
| 17 | ✅ | endlessBestFloor=-5→0、999999999→9999；同批坏档 quality 99→Common、level -10→1、enhance 999→15、技能槽 99→-1 |
| 18 | ✅ | 121 键热改塞 0/负值全部夹取（ParagonPassiveEvery=1、ChapterCount=11、AttackIntervalBase=0.01、EndlessDropLevelCap=1 等）；缺 1 键触发「缺 1 个配置项」警告 |
| 19 | ✅ | P200 20 种子探针：首死中位 45、P90 50、单层 P90 最大约 16s；P1000 + maxGear 50 种子：中位 221、P10 196 / P90 235、最高 250 层、230 层通过率 57% |
| 19a | ✅ | P1000 Play：主档临时改 P1000 后左栏「巅峰 1000 / 已达上限」、技能 15/15；第 215 层怪 Lv.1,394，战斗/DPS 正常（测试后已恢复主档） |
| 20 | ✅ | compilationFailed=false，consoleErrors/Warnings=0 |

Play 模式验收顺序建议：

1. 静态编译 + EndlessCatalog 单元检查（第 1/5/10/50 层公式）；
2. 新档/旧档迁移；
3. Play 第 1 层 → 第 5 层 BOSS；
4. 死亡/撤退/重试/返回；
5. 切档/删档/重置；
6. 跑 EndlessPacing，调参；
7. 截图：无尽面板、第 5 层 BOSS、结果面板、新纪录横幅；
8. 最后才更新 README 和截图。

## 14. 实现顺序

### 阶段 1：数据与配置

- 新建 Endless.cs：EndlessFloorDef、EndlessCatalog、EndlessRunState、EndlessEndReason、EndlessRecord。
- GameConfigData.cs / GameConfig.cs / game_config.json / GameConfig.Loader.cs 加 14 个字段与夹取。
- 单元检查：第 1/5/10/50/100 层的等级、击杀目标、BOSS 类型、金币公式。

### 阶段 2：存档 v7

- PlayerData.endlessBestFloor。
- SaveData.endlessBestFloor、CurrentSaveVersion=7、Load 迁移、Save、Describe、SlotInfo。
- 老档缺 11-10 首通记录时保持未解锁（不自动补记）。
- 单元检查：v6→v7、坏值夹取、三档互不影响、删档/重置。

### 阶段 3：怪物与控制器

- MonsterData.CreateForEndlessFloor（复用核心计算，跳过终局倍率，乘 EndlessBaseFactor）。
- GameController 的 Mode / EndlessRun / StartEndlessRun / EndEndlessRun / ResolveEndlessFloorClear / SpawnMonster / SpawnMonsterNow / HandleMonsterKilled / HandlePlayerDied / SelectStage / 切档删档重置分支。
- SpawnMonster 补 killTarget=1 的 clearTier 预判；SpawnMonsterNow 的无尽分支清 _pendingTier 后走 CreateForEndlessFloor，并用 else/return 隔离战役主体。
- GrantKillReward 经验等级封顶、ResolveDrops 的精英/BOSS 掉落等级封顶；EnterRespawnWait 改成可选 delay 参数（战役调用点不变）。
- State.Ended 与冻结条件。
- Play 模式跑通一层、五层、死亡、撤退。

### 阶段 4：UI

- GameUI.Endless.cs：入口按钮、无尽面板、结果面板、二次确认撤退。
- HUD 分支、存档卡无尽行、CloseAllPanels/RefreshPanels/RefreshHud。
- 截图与文案校对。

### 阶段 5：平衡与探针（已完成）

- EndlessPacing.cs：默认 20 个种子，输出首死分布/耗时/经济；P1000 满配用 50 个种子、最多 300 层。
- 按 §12.2 调参；已定为 EndlessBaseFactor=2.8、EndlessMonsterLevelStep=6，P200 首死中位 45 层；P1000 满配中位 221 层。
- 基线已记入 §12.4 / §12.5，并同步到 _balance/balance-analysis.md §10。

### 阶段 6：文档与收尾（已完成，2026-10-19）

- README：路线图第 11 项已勾选；已补玩法/界面/项目结构/平衡回归/截图行。
- 本文档状态已改为「已实现」，已补实测数字与 §13.1 首轮验收结果。
- 最终 console_status：compilationFailed=false，consoleErrors=0，consoleWarnings=0。
- 2026-10-19 补充边界验收：新档解锁横幅、v6 迁移、坏档夹取、配置缺失/0/负值、第 50 层经验与掉落封顶均通过，结果记在 §13.1。
- 2026-10-19 补 P1000 Play 验收与探针模拟洗练；P1000 无尽 50 种子，数据见 §12.5 与 README。
- 2026-10-21 五槽 P1000 终局复测（119,842 次挑战 / 6,081.6min 纯战斗 / 兜里 8.598 亿），数据见 `_balance/balance-analysis.md` §3.5.4。
- 按用户规矩：未明确说「提交」不得 commit/push。

## 15. 待确认问题（默认按推荐执行）

| 问题 | 推荐默认 | 备选 |
|---|---|---|
| 死亡是否直接结束本局 | 是 | 每局 1 次自动复活（会让纪录语义变复杂，首版不做） |
| 起始层 | max(1, 本档最高 − EndlessCheckpointOffset)，可切回第 1 层 | 永远从第 1 层开始（重复劳动多） |
| 最高层语义 | 已清除的最高层 | 到达的最高层（可用退出/送死刷纪录，不推荐） |
| 无尽掉落等级 | 封顶 150 | 跟随怪物等级（会装备膨胀，不推荐） |
| 本档/全局记录 | 两者都显示 | 只做本档（少了跨档目标） |
| 首通金币 | 新纪录才发 | 每层都发（金币通胀） |
| 未完成局是否存档 | 不存，退出即放弃 | 存当前层/击杀数（增加存档复杂度与 SL 空间） |
| Top-N 历史 | 已实现（v11，§6.5）：本档保存最近 5 局 | 只留一条最高层（无法回看单局成绩） |

以上默认均已按推荐实现（2026-10-19）；后续若调整无尽平衡，按 §12.2 的顺序改配置并重跑 EndlessPacing 探针。

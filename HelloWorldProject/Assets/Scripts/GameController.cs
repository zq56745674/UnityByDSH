using System;
using System.Collections.Generic;
using UnityEngine;

namespace AutoBattle
{
    /// <summary>
    /// 游戏主控制器：驱动「战斗 → 击杀 → 结算 → 重生」循环，并负责升级、
    /// 自动装备、自动存档、速度档位与重置（要求 1、14、16、17）。
    ///
    /// 本脚本只处理逻辑，不碰任何 UI；界面由 <see cref="GameUI"/> 读取这里的状态绘制。
    /// </summary>
    public class GameController : MonoBehaviour
    {
        /// <summary>战斗循环所处阶段。</summary>
        public enum State
        {
            /// <summary>已开始、正在与怪物交战。</summary>
            Fighting,

            /// <summary>怪物已死亡，等待 0.5 秒后重生（要求 1）。</summary>
            WaitingRespawn,

            /// <summary>主角被击败，等待自动复活（复活后继续战斗，不再需要玩家点「再战」）。</summary>
            Reviving,

            /// <summary>
            /// 商店开放中：精英 / Boss 出现前的短暂窗口，战斗暂停，供玩家买药。
            /// 到点自动开战（倒计时用真实时间，不受速度档位影响）。
            /// </summary>
            Shopping,

            /// <summary>
            /// 技能三选一中：巅峰升级发技能时战斗暂停，等玩家从候选里选一个。
            /// 和商店不同，这里没有倒计时——必须选完才继续。
            /// </summary>
            SkillChoice,
        }

        // ==================== 对外状态（UI 只读）====================

        public PlayerData Player { get; private set; }
        public MonsterData Monster { get; private set; }

        /// <summary>战斗系统实例：UI 用它读 Buff 剩余时间与技能冷却。</summary>
        public BattleSystem Battle => _battle;
        public State CurrentState { get; private set; } = State.WaitingRespawn;

        /// <summary>是否正在运行（开始/暂停按钮控制）。</summary>
        public bool IsRunning { get; private set; }

        /// <summary>当前游戏速度倍率（要求 16）。</summary>
        public float SpeedMultiplier { get; private set; } = GameConfig.SpeedOptions[0];

        /// <summary>当前速度档位下标，UI 用来高亮。</summary>
        public int SpeedIndex { get; private set; }

        /// <summary>战斗日志（要求 15），最新的在末尾。</summary>
        public BattleLog Log { get; } = new BattleLog();

        // ---- 统计，用于面板展示 ----

        public int TotalKills { get; private set; }
        public int TotalDrops { get; private set; }
        public int TotalEquipped { get; private set; }

        /// <summary>卖掉的闲置装备件数。</summary>
        public int TotalSold { get; private set; }

        /// <summary>卖装备累计获得的金币。</summary>
        public int TotalGoldEarned { get; private set; }

        /// <summary>阵亡次数（阵亡后自动复活，不再结束游戏）。</summary>
        public int TotalDeaths { get; private set; }

        /// <summary>精英怪击杀数。</summary>
        public int EliteKills { get; private set; }

        /// <summary>Boss 击杀数。</summary>
        public int BossKills { get; private set; }

        /// <summary>战斗日志有变化时自增，UI 以此判断是否需要重绘（避免每帧重建文本）。</summary>
        public int LogVersion => Log.Version;

        /// <summary>
        /// 累计写入过多少条日志（只增不减）。Log 会按上限裁剪，
        /// 所以「这一帧新增了几条」不能拿 Log.Count 的前后差去算——满 60 条之后差值恒为 0，
        /// 飘字与实时 DPS 曾因此永久失效。UI 请用本值做游标。
        /// </summary>
        public int TotalLogEntries => Log.TotalEntries;

        // ==================== 内部 ====================

        private readonly BattleSystem _battle = new BattleSystem();
        private float _respawnTimer;
        private float _reviveTimer;
        private float _autoSaveTimer;
        private bool _loadedFromSave;

        /// <summary>
        /// 待选掉落（按槽位下标存，每槽最多一件，仅本会话有效）。
        /// 只有两种情况会进来：当前装备上了锁，或设置里关掉了「自动换装」。
        /// 不随存档持久化——它是「等你这一会儿决定」的临时托盘，不是仓库。
        /// </summary>
        private readonly Equipment[] _pendingDrops = new Equipment[2];

        /// <summary>本局是否已经开始过（区分"第一次点开始"与"暂停后继续"）。</summary>
        private bool _runStarted;

        /// <summary>下一只要生成的档次（由关卡进度触发，生成后立刻复位为 Normal）。</summary>
        private MonsterTier _pendingTier = MonsterTier.Normal;

        /// <summary>终局 BOSS 的挑战次数：用来轮换怪物类型（见 SpawnMonsterNow）。</summary>
        private int _finalBossAttempt;

        /// <summary>当前关卡是不是终局（第 11 章最后一关）。</summary>
        private bool IsFinalStage =>
            Player != null && Player.CurrentStage.chapter >= GameConfig.ChapterCount
            && Player.CurrentStage.IsChapterFinal;

        /// <summary>生命药剂的剩余冷却（按游戏内时间计，受速度档位影响）。</summary>
        private float _potionCooldownTimer;

        /// <summary>商店剩余开放时间（真实秒）。</summary>
        private float _shopTimer;

        /// <summary>刚刚这只怪触发了通关，重生等待要用 StageClearDelay 而不是常规值。</summary>
        private bool _justClearedStage;

        // ==================== 生命周期 ====================

        private void Awake()
        {
            // 先还原「上次用的是哪个存档槽」，再读档——否则会从槽 0 读。
            SaveSystem.RestoreCurrentSlot();

            Player = new PlayerData();
            Player.FillHp();

            // 尝试恢复上次进度（要求 17）。
            _loadedFromSave = SaveSystem.Load(Player);
            if (_loadedFromSave)
            {
                AddLog($"已恢复存档：Lv.{Player.level}  经验 {Player.exp}/{Player.ExpToNextLevel}", GameConfig.LogSuccess);

                if (Player.paragonLevel > 0)
                {
                    AddLog($"✧ 巅峰等级 {Player.paragonLevel}（已获得技能 {Player.OwnedSkillCount}）",
                        GameConfig.LogParagon);
                }
            }
            else
            {
                // 100 级之前没有任何技能，所以新档六个槽位都是空的。
                AddLog("新游戏开始。点击「开始」进入自动战斗。", GameConfig.LogNeutral);
            }

            // 未开始时先不生成怪物，等玩家点开始。
            CurrentState = State.WaitingRespawn;
        }

        private void Update()
        {
            float realDt = Time.unscaledDeltaTime;
            AutoSaveTick(realDt);
            TickBanner(realDt);

            if (!IsRunning) return;

            // 选关 / 存档 / 强化 / 洗练 / 技能 / 设置 / 引导 / 对比面板打开时冻结整局（含商店倒计时），
            // 否则玩家挑关、挑档、挑强化目标、挑技能时怪会照打。
            // 技能面板原先不在名单里，理由是「它只在战斗停下的空档弹出」——但槽位按钮
            // 任何时刻都能点，战斗中点开就是一整块浮层盖住血条让怪白打，所以一并冻住。
            // 设置 / 引导 / 对比同理：它们都能从常驻按钮点开，必须和别的浮层一样有冻结权。
            if (StagePanelOpen || SavePanelOpen || EnhancePanelOpen || ReforgePanelOpen || SkillPanelOpen
                || SettingsOpen || GuideOpen || CompareOpen) return;

            // 游戏速度只影响战斗推进，不影响存档节奏（要求 16）。
            float dt = Time.deltaTime * SpeedMultiplier;

            TickPotion(dt);

            switch (CurrentState)
            {
                case State.Fighting:
                    TickFighting(dt);
                    break;

                case State.WaitingRespawn:
                    _respawnTimer -= dt;
                    if (_respawnTimer <= 0f) SpawnMonster();
                    break;

                case State.Reviving:
                    _reviveTimer -= dt;
                    if (_reviveTimer <= 0f) RevivePlayer();
                    break;

                case State.Shopping:
                    // 用真实时间倒计时，速度档位不影响买药窗口。
                    _shopTimer -= realDt;
                    if (_shopTimer <= 0f) SpawnMonsterNow();
                    break;

                case State.SkillChoice:
                    // 等玩家选技能：不推进战斗，等 ChooseSkill 里恢复。
                    break;
            }
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus) SaveSystem.Save(Player);
        }

        private void OnApplicationQuit()
        {
            SaveSystem.Save(Player);
        }

        // ==================== 对外操作 ====================

        /// <summary>
        /// 开始 / 暂停切换（要求 16）。
        /// 只负责开关时间推进：**暂停期间状态机原地冻结，继续时原样恢复**。
        /// 早先的实现会在"继续"时无条件把状态打回 WaitingRespawn，导致两个可利用的洞：
        /// 商店里暂停再继续会把 _shopTimer 充满（无限刷商店时间）、三选一里暂停再继续会把这次选择吞掉。
        /// </summary>
        public void ToggleRunning()
        {
            IsRunning = !IsRunning;

            if (!IsRunning)
            {
                AddLog("⏸ 已暂停", GameConfig.LogSystem);
                return;
            }

            if (_runStarted)
            {
                AddLog("▶ 继续", GameConfig.LogSuccess);
                return;
            }

            // 第一次开始：只在这一刻补一脚，把循环踢起来（等复活中则别抢状态）。
            _runStarted = true;
            if (Monster == null && CurrentState != State.Reviving)
            {
                CurrentState = State.WaitingRespawn;
                _respawnTimer = 0f;
            }
            AddLog("▶ 开始自动战斗", GameConfig.LogSuccess);
        }

        /// <summary>切换速度档位（要求 16）。</summary>
        public void SetSpeedIndex(int index)
        {
            SpeedIndex = Mathf.Clamp(index, 0, GameConfig.SpeedOptions.Length - 1);
            SpeedMultiplier = GameConfig.SpeedOptions[SpeedIndex];
            AddLog($"⚡ 速度 x{SpeedMultiplier:0}", GameConfig.LogSystem);
        }

        // ==================== 选关 ====================

        /// <summary>选关面板是否打开。打开时冻结战斗与商店倒计时，让玩家安心挑关。</summary>
        public bool StagePanelOpen { get; set; }

        /// <summary>已解锁的最高章节（章节级解锁：解锁某章即该章 10 关全可打）。</summary>
        public int MaxChapterUnlocked => Player != null ? Mathf.Max(1, Player.maxChapterUnlocked) : 1;

        /// <summary>
        /// 某关是否已解锁。关卡号越界也算未解锁——StageCatalog.Get 会把越界号夹回
        /// 合法范围而不是返回 null，所以这里必须自己查边界。
        /// </summary>
        public bool IsStageUnlocked(int chapter, int stage)
        {
            if (Player == null) return false;
            if (chapter < StageCatalog.FirstChapter || chapter > StageCatalog.ChapterCount) return false;
            if (stage < 1 || stage > StageCatalog.StagesPerChapter) return false;
            return chapter <= MaxChapterUnlocked;
        }

        /// <summary>
        /// 跳到指定关卡：作废本关进度、丢开当前怪、回满血重来。
        /// 已通关的关卡可以反复进（回头刷），未解锁的章节会被拒绝。
        /// </summary>
        public void SelectStage(int chapter, int stage)
        {
            if (Player == null) return;

            if (!IsStageUnlocked(chapter, stage))
            {
                AddLog($"🔒 第 {chapter} 章尚未解锁（当前最高 第 {MaxChapterUnlocked} 章）",
                    GameConfig.LogWarn);
                return;
            }

            StageDef def = StageCatalog.Get(chapter, stage);

            StageDef cur = Player.CurrentStage;
            if (cur != null && cur.chapter == chapter && cur.stage == stage)
            {
                // 同一关：只把本关进度推倒重来，不动战斗状态。
                Player.stageKillCount = 0;
                _pendingTier = MonsterTier.Normal;
                AddLog($"↻ {def.Title} 重新开始", GameConfig.LogInfo);
                StagePanelOpen = false;
                return;
            }

            Player.chapterIndex = chapter;
            Player.stageIndex = stage;
            Player.stageKillCount = 0;
            Player.FillHp();
            Player.shield = 0;

            Monster = null;
            _pendingTier = MonsterTier.Normal;
            _shopTimer = 0f;
            _respawnTimer = 0f;
            _reviveTimer = 0f;
            CurrentState = State.WaitingRespawn;
            _battle.ResetSkillState();

            StagePanelOpen = false;
            AddLog($"⇄ 进入 {def.Title}（怪物 Lv.{def.monsterLevel}，本关需击杀 {def.killTarget} 只）",
                GameConfig.LogInfo);
            SaveSystem.Save(Player);
        }

        // ---- 五个浮层（选关 / 技能 / 存档 / 强化 / 洗练）互斥，规则只在这一处 ----

        /// <summary>三选一还没选完时不许开任何浮层：候选卡会压在浮层上面，两层叠着没法看。</summary>
        private bool ChoicePending => CurrentState == State.SkillChoice || PendingSkillPicks > 0;

        /// <summary>关掉全部浮层。任何 Open 之前先走这里，保证同时只开一个。</summary>
        private void CloseAllPanels()
        {
            StagePanelOpen = false;
            SkillPanelOpen = false;
            SavePanelOpen = false;
            EnhancePanelOpen = false;
            ReforgePanelOpen = false;
            SettingsOpen = false;
            GuideOpen = false;
            CompareOpen = false;
        }

        /// <summary>打开 / 关闭选关面板。</summary>
        public void ToggleStagePanel()
        {
            if (ChoicePending) return;
            bool open = !StagePanelOpen;
            CloseAllPanels();
            StagePanelOpen = open;
        }

        /// <summary>关闭选关面板。</summary>
        public void CloseStagePanel() => StagePanelOpen = false;

        /// <summary>打开技能面板（同时关掉其它浮层）。</summary>
        public void OpenSkillPanel()
        {
            if (ChoicePending) return;
            CloseAllPanels();
            SkillPanelOpen = true;
        }

        /// <summary>关闭技能面板。</summary>
        public void CloseSkillPanel() => SkillPanelOpen = false;

        /// <summary>打开 / 关闭存档槽面板。</summary>
        public void ToggleSavePanel()
        {
            if (ChoicePending) return;
            bool open = !SavePanelOpen;
            CloseAllPanels();
            SavePanelOpen = open;
        }

        /// <summary>关闭存档槽面板。</summary>
        public void CloseSavePanel() => SavePanelOpen = false;

        // ---- 装备强化（步骤 2）----

        /// <summary>打开 / 关闭装备强化面板。</summary>
        public void ToggleEnhancePanel()
        {
            if (ChoicePending) return;
            bool open = !EnhancePanelOpen;
            CloseAllPanels();
            EnhancePanelOpen = open;
        }

        /// <summary>关闭装备强化面板。</summary>
        public void CloseEnhancePanel() => EnhancePanelOpen = false;

        /// <summary>
        /// 强化指定槽位的装备一级：扣金币、强化等级 +1，属性立即重算。
        /// 强化只作用于主属性（武器攻击 / 衣服防御与生命），不影响暴击、暴伤与副词条，
        /// 所以这里不需要动任何与暴击相关的状态。
        /// </summary>
        public EnhanceResult EnhanceEquipment(EquipmentSlot slot)
        {
            Equipment e = Player.GetEquipment(slot);
            if (e == null) return EnhanceResult.NoEquipment;
            if (!e.CanEnhance) return EnhanceResult.MaxLevel;

            int cost = e.EnhanceCost;
            if (Player.gold < cost) return EnhanceResult.NotEnoughGold;

            // 先记下旧的生命上限：衣服强化会抬高上限，不补这一截的话，
            // 强化完血量占比反而变小，看着像「强化掉血」。武器强化时增量为 0，此处无害。
            int oldMaxHp = Player.MaxHp;

            Player.gold -= cost;
            e.enhanceLevel++;
            Player.currentHp += Mathf.Max(0, Player.MaxHp - oldMaxHp);

            AddLog($"◆ 强化 {e.Title}  →  花费 {cost} 金币", GameConfig.QualityColors[(int)e.quality]);
            return EnhanceResult.Success;
        }

        // ---- 装备洗练（步骤 3）----

        /// <summary>打开 / 关闭装备洗练面板。</summary>
        public void ToggleReforgePanel()
        {
            if (ChoicePending) return;
            bool open = !ReforgePanelOpen;
            CloseAllPanels();
            ReforgePanelOpen = open;
        }

        /// <summary>关闭装备洗练面板。</summary>
        public void CloseReforgePanel() => ReforgePanelOpen = false;

        // ---- 设置 / 新手引导 / 装备对比（2026-10-18 新增）----

        /// <summary>设置浮层是否展开（UI 用）。和强化/洗练一样冻结战斗。</summary>
        public bool SettingsOpen { get; set; }

        /// <summary>新手引导浮层是否展开（UI 用）。引导期间整局冻结，玩家不会在教程里被打死。</summary>
        public bool GuideOpen { get; set; }

        /// <summary>装备对比浮层是否展开（UI 用）。</summary>
        public bool CompareOpen { get; set; }

        /// <summary>打开 / 关闭设置面板。</summary>
        public void ToggleSettingsPanel()
        {
            if (ChoicePending) return;
            bool open = !SettingsOpen;
            CloseAllPanels();
            SettingsOpen = open;
        }

        /// <summary>关闭设置面板。</summary>
        public void CloseSettingsPanel() => SettingsOpen = false;

        /// <summary>打开新手引导（首次启动自动调用，设置里也能重看）。</summary>
        public void OpenGuide()
        {
            if (ChoicePending) return;
            CloseAllPanels();
            GuideOpen = true;
        }

        /// <summary>关闭新手引导（走完或跳过时调用，标记由 UI 写进 GameSettings）。</summary>
        public void CloseGuide() => GuideOpen = false;

        /// <summary>打开 / 关闭装备对比面板。</summary>
        public void ToggleComparePanel()
        {
            if (ChoicePending) return;
            bool open = !CompareOpen;
            CloseAllPanels();
            CompareOpen = open;
        }

        /// <summary>关闭装备对比面板。</summary>
        public void CloseComparePanel() => CompareOpen = false;

        /// <summary>某槽位的待选掉落；没有则为 null。</summary>
        public Equipment PendingDrop(EquipmentSlot slot) => _pendingDrops[(int)slot];

        /// <summary>某槽位是否有待选掉落（左面板「对比」按钮的角标用）。</summary>
        public bool HasPendingDrop(EquipmentSlot slot)
        {
            int index = (int)slot;
            return index >= 0 && index < _pendingDrops.Length && _pendingDrops[index] != null;
        }

        /// <summary>是否有任意槽位存在待选掉落。</summary>
        public bool HasAnyPendingDrop => HasPendingDrop(EquipmentSlot.Weapon) || HasPendingDrop(EquipmentSlot.Armor);

        /// <summary>换档、重开、读档时清空临时托盘，避免把上一份存档的掉落带过来。</summary>
        private void ClearPendingDrops()
        {
            _pendingDrops[0] = null;
            _pendingDrops[1] = null;
        }

        /// <summary>
        /// 把待选掉落穿上（玩家在对比面板里点「换装」）。
        /// 手动选择天然覆盖「当前装备上锁」的限制：锁只挡自动替换，不挡你自己决定。
        /// </summary>
        public bool EquipPending(EquipmentSlot slot)
        {
            int index = (int)slot;
            Equipment pending = _pendingDrops[index];
            if (pending == null) return false;

            WearEquipment(pending);
            _pendingDrops[index] = null;
            TotalEquipped++;
            AddLog($"↑ 已换装 {pending.Title}（评分 {EquipmentScore.Of(Player, pending, pending.slot):0}）",
                GameConfig.LogSuccess);
            return true;
        }

        /// <summary>把待选掉落卖掉（对比面板里的「卖出」）。</summary>
        public bool SellPending(EquipmentSlot slot)
        {
            int index = (int)slot;
            Equipment pending = _pendingDrops[index];
            if (pending == null) return false;

            int gold = GameConfig.SellPrice(pending.level, pending.quality);
            Player.gold += gold;
            TotalSold++;
            TotalGoldEarned += gold;
            _pendingDrops[index] = null;

            AddLog($"$ 卖出待选 {pending.Title}  +{gold} 金币（共 {Player.gold}）", GameConfig.LogGold);
            return true;
        }

        /// <summary>
        /// 切换某槽位装备的上锁状态，返回切换后的状态。
        /// 锁定的意义：自动换装不会把它顶掉，更强的掉落会进待选等你比较。
        /// </summary>
        public bool ToggleEquipLock(EquipmentSlot slot)
        {
            Equipment e = Player.GetEquipment(slot);
            if (e == null) return false;

            e.locked = !e.locked;
            AddLog(e.locked
                    ? $"🔒 已锁定 {e.Title}，更强掉落将进入待选"
                    : $"🔓 已解锁 {e.Title}，恢复自动比较",
                e.locked ? GameConfig.LogGold : GameConfig.LogInfo);
            return e.locked;
        }

        /// <summary>
        /// 洗练指定槽位的装备：扣金币、重抽**没被锁住**的副词条、已洗次数 +1。
        /// 条数与其余字段都不变（见 LootSystem.ReforgeAffixes），唯一要额外处理的是生命上限——
        /// 「全属性」词条的数值被换掉后上限可能涨也可能跌，涨了补差额、跌了由 Min 夹住，
        /// 否则洗练完血量占比会跳变，看着像「洗练掉血」。
        /// <paramref name="lockedMask"/> 是 EquipmentAffix 的位掩码（哪几条锁住不动），
        /// 每多锁一条费用乘一次 GameConfig.ReforgeLockFactor；全锁直接拒绝，一分钱不扣。
        /// </summary>
        public ReforgeResult ReforgeEquipment(EquipmentSlot slot, int lockedMask = 0)
        {
            Equipment e = Player.GetEquipment(slot);
            if (e == null) return ReforgeResult.NoEquipment;
            if (!e.CanReforge) return ReforgeResult.NoAffix;

            int locked = e.LockedAffixCount(lockedMask);
            if (locked >= e.AffixCount) return ReforgeResult.AllLocked;

            int cost = e.ReforgeCostWithLocks(locked);
            if (Player.gold < cost) return ReforgeResult.NotEnoughGold;

            int oldMaxHp = Player.MaxHp;
            int oldQuality = Mathf.RoundToInt(e.AffixQuality * 100f);

            Player.gold -= cost;
            LootSystem.ReforgeAffixes(e, lockedMask);
            e.reforgeCount++;

            int newMaxHp = Player.MaxHp;
            Player.currentHp = Mathf.Min(newMaxHp, Player.currentHp + Mathf.Max(0, newMaxHp - oldMaxHp));

            int newQuality = Mathf.RoundToInt(e.AffixQuality * 100f);
            int delta = newQuality - oldQuality;
            string arrow = delta > 0 ? $"↑{delta}" : delta < 0 ? $"↓{-delta}" : "持平";
            string lockNote = locked > 0
                ? $"，锁 {locked} 条 ×{Equipment.ReforgeLockMultiplier(locked):0.#}"
                : "";

            AddLog($"◆ 洗练 {e.Title}  →  花费 {cost:N0} 金币（第 {e.reforgeCount} 次{lockNote}）", e.QualityColor);
            AddLog($"  词条质量 {oldQuality}% → {newQuality}%（{arrow}）    {e.AffixSummary}", GameConfig.LogInfo);
            return ReforgeResult.Success;
        }

        /// <summary>当前存档槽（0 起，UI 显示时 +1）。</summary>
        public int SaveSlot => SaveSystem.CurrentSlot;

        /// <summary>
        /// 切换存档槽：先把当前进度写回原槽，再读目标槽（空槽则开新档）。
        /// 战斗状态整体回到初始——换槽等于换一份存档从头玩，不做热插拔。
        /// </summary>
        public void SwitchSlot(int slot)
        {
            slot = Mathf.Clamp(slot, 0, SaveSystem.SlotCount - 1);
            if (slot == SaveSystem.CurrentSlot) return;

            // 当前进度先落盘，否则切走之后这一段就没了。
            SaveSystem.Save(Player);
            SaveSystem.SelectSlot(slot);

            Player = new PlayerData();
            Player.FillHp();

            bool loaded = SaveSystem.Load(Player);
            _loadedFromSave = loaded;
            ResetSessionState();

            if (loaded)
            {
                AddLog($"⇄ 切换到存档 {slot + 1}：Lv.{Player.level}  经验 {Player.exp}/{Player.ExpToNextLevel}  {Player.CurrentStage.Title}",
                    GameConfig.LogSuccess);
            }
            else
            {
                AddLog($"⇄ 切换到存档 {slot + 1}（空档，已开始新游戏）。", GameConfig.LogGold);
            }
            AddLog("点击「开始」进入自动战斗。", GameConfig.LogNeutral);
        }

        /// <summary>删除某个存档槽（当前槽被删则回到该槽的新游戏状态）。</summary>
        public void DeleteSlot(int slot)
        {
            slot = Mathf.Clamp(slot, 0, SaveSystem.SlotCount - 1);
            SaveSystem.ClearSlot(slot);

            if (slot == SaveSystem.CurrentSlot)
            {
                // 删的是当前槽：直接按新档重开，避免界面停在旧数据上。
                Player = new PlayerData();
                Player.FillHp();
                _loadedFromSave = false;
                ResetSessionState();
                AddLog($"🗑 存档 {slot + 1} 已删除，该槽已重置为新游戏。", GameConfig.LogWarn);
            }
            else
            {
                AddLog($"🗑 存档 {slot + 1} 已删除。", GameConfig.LogWarn);
            }
        }

        /// <summary>重置：清空存档与全部进度，回到初始状态（要求 16）。</summary>
        public void ResetGame()
        {
            SaveSystem.Clear();

            Player = new PlayerData();
            Player.FillHp();
            _loadedFromSave = false;
            ResetSessionState();
            AddLog("↺ 已重置，进度与存档已清空。", GameConfig.LogWarn);
        }

        /// <summary>
        /// 把一局内的临时状态、统计、浮层和待选全部归零。
        /// SwitchSlot / DeleteSlot / ResetGame 共用，避免漏清某一项（删档曾漏统计、待选和横幅）。
        /// </summary>
        private void ResetSessionState()
        {
            _battle.ResetSkillState();
            Monster = null;
            Player.shield = 0;
            TotalKills = 0;
            TotalDrops = 0;
            TotalEquipped = 0;
            TotalSold = 0;
            TotalGoldEarned = 0;
            TotalDeaths = 0;
            EliteKills = 0;
            BossKills = 0;
            _pendingTier = MonsterTier.Normal;
            _finalBossAttempt = 0;
            _justClearedStage = false;
            IsRunning = false;
            _runStarted = false;
            CloseAllPanels();
            ClearPendingDrops();
            CurrentState = State.WaitingRespawn;
            _respawnTimer = 0f;
            _reviveTimer = 0f;
            _autoSaveTimer = 0f;
            _potionCooldownTimer = 0f;
            _shopTimer = 0f;

            Log.Clear();
            BannerTitle = string.Empty;
            BannerSubtitle = string.Empty;
            BannerTimeLeft = 0f;
        }

        // ==================== 技能装配 ====================

        /// <summary>技能面板是否展开（UI 用）。</summary>
        public bool SkillPanelOpen { get; set; }

        /// <summary>存档槽面板是否展开（UI 用）。它和选关面板一样会冻结战斗。</summary>
        public bool SavePanelOpen { get; set; }

        /// <summary>装备强化面板是否展开（UI 用）。同样冻结战斗，避免边打边强化。</summary>
        public bool EnhancePanelOpen { get; set; }

        /// <summary>装备洗练面板是否展开（UI 用）。与强化面板一样冻结战斗。</summary>
        public bool ReforgePanelOpen { get; set; }

        /// <summary>某个主动槽的剩余冷却秒数（UI 进度条用）。</summary>
        public float SkillCooldown(int slot) => _battle.GetActiveCooldown(slot);

        /// <summary>该技能是否已通过巅峰获得（100 级前全部为未获得）。</summary>
        public bool IsSkillUnlocked(SkillId id) => Player.IsSkillOwned(id);

        /// <summary>
        /// 把技能装到指定槽位。槽位与技能类别必须匹配（0~1 主动、2~3 被动、4~5 触发）；
        /// 技能还需要已经通过巅峰获得（100 级前没有任何技能）。装配后会重置技能冷却。
        /// </summary>
        public bool EquipSkill(int slot, SkillId id)
        {
            if (slot < 0 || slot >= GameConfig.SkillSlotsPerKind * 3) return false;

            SkillDef def = SkillCatalog.Get(id);
            int slotKind = slot / GameConfig.SkillSlotsPerKind;

            if ((int)def.kind != slotKind)
            {
                AddLog($"◆ {def.name} 不能装在当前槽位", GameConfig.LogWarn);
                return false;
            }

            if (!Player.IsSkillOwned(id))
            {
                AddLog($"◆ {def.name} 尚未获得（100 级后每升 10 级巅峰随机获得一个技能）", GameConfig.LogWarn);
                return false;
            }

            // 同一技能已装在别的槽位时先摘掉，避免重复占用。
            int old = Player.FindSkillSlot((int)id);
            if (old >= 0) Player.SetSkillSlot(old, -1);

            Player.SetSkillSlot(slot, (int)id);
            _battle.ResetSkillState();

            AddLog($"◆ 已装备技能：{def.name}", def.color);
            Debug.Log($"[AutoBattle] 装备技能 {def.name} 到槽位 {slot}");
            return true;
        }

        /// <summary>卸下某个槽位的技能。</summary>
        public void UnequipSkill(int slot)
        {
            if (slot < 0 || slot >= GameConfig.SkillSlotsPerKind * 3) return;

            int id = Player.GetSkillSlot(slot);
            if (id < 0) return;

            Player.SetSkillSlot(slot, -1);
            _battle.ResetSkillState();

            AddLog($"◆ 已卸下技能：{SkillCatalog.All[id].name}", GameConfig.LogInfo);
        }

        /// <summary>新获得的技能自动放进同类空槽；同类已满则只加入「已获得」，由玩家在面板里替换。</summary>
        private void TryAutoEquip(SkillId id)
        {
            SkillDef def = SkillCatalog.Get(id);
            int baseSlot = (int)def.kind * GameConfig.SkillSlotsPerKind;

            for (int i = 0; i < GameConfig.SkillSlotsPerKind; i++)
            {
                if (Player.GetSkillSlot(baseSlot + i) < 0)
                {
                    Player.SetSkillSlot(baseSlot + i, (int)id);
                    AddLog($"  → 已自动装备到{KindName(def.kind)}槽位 {baseSlot + i + 1}", def.color);
                    return;
                }
            }

            AddLog($"  → {KindName(def.kind)}槽位已满，可在技能面板中替换", GameConfig.LogInfo);
        }

        // ==================== 技能三选一 ====================

        private readonly List<SkillId> _skillChoices = new List<SkillId>();

        /// <summary>当前待选的技能候选（空表表示没在选）。UI 只读。</summary>
        public IReadOnlyList<SkillId> SkillChoices => _skillChoices;

        /// <summary>是否正在等玩家选技能。</summary>
        public bool IsChoosingSkill => CurrentState == State.SkillChoice;

        /// <summary>还剩几次没选。</summary>
        public int PendingSkillPicks => Player != null ? Player.pendingSkillPicks : 0;

        /// <summary>抽出候选并暂停战斗，等玩家三选一。</summary>
        private void OpenSkillChoice()
        {
            _skillChoices.Clear();
            _skillChoices.AddRange(Player.RollSkillCandidates(GameConfig.SkillChoiceCount));

            if (_skillChoices.Count == 0)
            {
                // 技能已全部拥有：没有候选，清掉待选次数，别把状态卡死。
                Player.pendingSkillPicks = 0;
                AddLog("✧ 技能已全部获得", GameConfig.LogInfo);
                return;
            }

            CurrentState = State.SkillChoice;
        }

        /// <summary>玩家从候选里选一个（index 为候选下标），选完自动装备并恢复战斗。</summary>
        public void ChooseSkill(int index)
        {
            if (CurrentState != State.SkillChoice) return;
            if (index < 0 || index >= _skillChoices.Count) return;

            SkillId picked = _skillChoices[index];
            if (!Player.GrantSkill(picked)) return;

            Player.pendingSkillPicks = Mathf.Max(0, Player.pendingSkillPicks - 1);

            SkillDef def = SkillCatalog.Get(picked);
            AddLog($"✧ 获得技能：{def.name}（{KindName(def.kind)}）", def.color);
            Debug.Log($"[AutoBattle] 三选一获得技能 {def.name}");

            TryAutoEquip(picked);
            _skillChoices.Clear();

            // 还有待选次数就接着抽下一组。
            // 抽不到候选（技能池已全拥有）时 OpenSkillChoice 只清待选、不改状态，
            // 此时若直接 return 会把状态永久留在 SkillChoice 且候选为空（UI 空面板 + 战斗冻结），
            // 所以只有真的进了 SkillChoice 才返回，否则落到下面回 WaitingRespawn。
            if (Player.pendingSkillPicks > 0)
            {
                OpenSkillChoice();
                if (CurrentState == State.SkillChoice) return;
            }

            // 选完回到「等待重生」，让循环继续。
            CurrentState = State.WaitingRespawn;
            _respawnTimer = GameConfig.MonsterRespawnDelay;
        }

        private static string KindName(SkillKind kind)
        {
            switch (kind)
            {
                case SkillKind.Active: return "主动";
                case SkillKind.Passive: return "被动";
                default: return "触发";
            }
        }

        // ==================== 商店：生命药剂 ====================

        /// <summary>UI 上的商店提示（"金币不足" / "药剂已满"）；为空表示无提示。</summary>
        public string ShopMessage { get; private set; } = string.Empty;

        /// <summary>当前售价（随主角等级变化）。</summary>
        public int PotionPrice => GameConfig.PotionPrice(Player.level);

        /// <summary>商店是否开放（只在精英 / Boss 出现前开放）。</summary>
        public bool IsShopOpen => CurrentState == State.Shopping;

        /// <summary>商店剩余开放秒数；未开放时为 0。</summary>
        public float ShopTimeLeft => IsShopOpen ? Mathf.Max(0f, _shopTimer) : 0f;

        /// <summary>下一只怪物的刷新倒计时（秒）；空场等待时界面要显示这个，否则血条一片空白像卡住。</summary>
        public float RespawnCountdown => Mathf.Max(0f, _respawnTimer);

        /// <summary>自动购买开关。setter 会写进 Player.autoBuyPotion（存档字段）并记一条日志。</summary>
        public bool AutoBuyPotion
        {
            get => Player.autoBuyPotion;
            set
            {
                Player.autoBuyPotion = value;
                ShopMessage = string.Empty;
                AddLog(value ? "◆ 已开启自动购买" : "◆ 已关闭自动购买", GameConfig.LogInfo);
            }
        }

        /// <summary>
        /// 切换自动购买。UI 走这个方法而不是 `_game.AutoBuyPotion = !_game.AutoBuyPotion`：
        /// 那个属性直写会绕过控制器，而它改的是会被存档的字段。
        /// </summary>
        public void ToggleAutoBuyPotion() => AutoBuyPotion = !AutoBuyPotion;

        /// <summary>
        /// 手动购买一瓶（UI 按钮）。金币不足或已满时只设置提示文本，不扣钱。
        /// </summary>
        public void BuyPotion()
        {
            if (!IsShopOpen)
            {
                ShopMessage = "商店只在精英/Boss 出现前开放";
                return;
            }

            if (Player.potionCount >= GameConfig.PotionMaxCount)
            {
                ShopMessage = "药剂已满";
                return;
            }

            int price = PotionPrice;
            if (Player.gold < price)
            {
                ShopMessage = "金币不足";
                return;
            }

            Player.gold -= price;
            Player.potionCount++;
            ShopMessage = string.Empty;
            AddLog($"◆ 购买生命药剂（-{price} 金币，持有 {Player.potionCount}/{GameConfig.PotionMaxCount}）",
                GameConfig.LogInfo);
            Debug.Log($"[AutoBattle] 购买生命药剂 -{price} 金币");
        }

        /// <summary>
        /// 自动购买：进入商店时把药剂补到上限——能买几瓶买几瓶，直到满 10 或金币不够。
        /// </summary>
        private void AutoFillPotions()
        {
            int bought = 0;
            int spent = 0;

            while (Player.potionCount < GameConfig.PotionMaxCount && Player.gold >= PotionPrice)
            {
                Player.gold -= PotionPrice;
                Player.potionCount++;
                bought++;
                spent += PotionPrice;
            }

            if (bought > 0)
            {
                AddLog($"◆ 自动购买生命药剂 ×{bought}（-{spent} 金币，持有 {Player.potionCount}/{GameConfig.PotionMaxCount}）",
                    GameConfig.LogInfo);
            }
            else if (Player.potionCount < GameConfig.PotionMaxCount)
            {
                AddLog("◆ 自动购买：金币不足，未能补货", GameConfig.LogWarn);
            }
        }

        /// <summary>
        /// 自动喝药：生命低于 PotionAutoUseHpPercent 时消耗 1 瓶，冷却 PotionCooldown 秒。
        /// </summary>
        private void TickPotion(float dt)
        {
            if (_potionCooldownTimer > 0f) _potionCooldownTimer -= dt;

            if (!IsRunning || Monster == null || CurrentState != State.Fighting) return;
            if (Player.currentHp <= 0 || Player.potionCount <= 0) return;
            if (_potionCooldownTimer > 0f) return;
            if (Player.currentHp >= Player.MaxHp * GameConfig.PotionAutoUseHpPercent) return;

            UsePotion();
        }

        /// <summary>喝下一瓶生命药剂：按最大生命的比例回血，并进入冷却。</summary>
        private void UsePotion()
        {
            Player.potionCount--;

            int healAmount = Mathf.RoundToInt(Player.MaxHp * GameConfig.PotionHealPercent);
            int hpBefore = Player.currentHp;
            Player.currentHp = Mathf.Min(Player.MaxHp, Player.currentHp + healAmount);
            int healed = Player.currentHp - hpBefore;

            _potionCooldownTimer = GameConfig.PotionCooldown;

            AddLog($"♥ 使用生命药剂，恢复 {healed} 生命", GameConfig.LogHeal);
            Debug.Log($"[AutoBattle] 使用生命药剂 +{healed}（剩余 {Player.potionCount} 瓶）");
        }

        // ==================== 战斗循环 ====================

        private void TickFighting(float dt)
        {
            // 累计条数与版本号由 BattleLog 的写入实现自己推进，这里不需要再补记
            // （以前靠「前后 Count 差值」补记，满上限后差值恒为 0 会静默漏记）。
            BattleTickResult result = _battle.Tick(Player, Monster, dt, Log);

            if (result == BattleTickResult.MonsterKilled) HandleMonsterKilled();
            else if (result == BattleTickResult.PlayerDied) HandlePlayerDied();
        }

        /// <summary>
        /// 生成怪物入口：如果这一只是精英 / Boss，先开放商店（战斗暂停），
        /// 商店结束后再真正生成；普通怪直接生成。
        /// </summary>
        private void SpawnMonster()
        {
            if (_pendingTier != MonsterTier.Normal)
            {
                OpenShop(_pendingTier);
                return;
            }

            SpawnMonsterNow();
        }

        /// <summary>
        /// 精英 / Boss 出现前开放商店：暂停战斗 <see cref="GameConfig.ShopDuration"/> 秒供玩家买药，
        /// 开了自动购买则直接把药剂补满。
        /// </summary>
        private void OpenShop(MonsterTier tier)
        {
            CurrentState = State.Shopping;
            _shopTimer = GameConfig.ShopDuration;
            ShopMessage = string.Empty;

            string tierName = tier == MonsterTier.Boss ? "BOSS" : "精英怪";
            AddLog($"🛒 即将迎战{tierName}，商店开放 {GameConfig.ShopDuration:0.#} 秒", GameConfig.LogSystem);

            if (Player.autoBuyPotion) AutoFillPotions();
        }

        /// <summary>
        /// 真正生成怪物（要求 4）：先按主角等级档位抽出类型，再叠加档次与随机词缀。
        /// 档次从 _pendingTier 取，取完立刻复位。
        /// </summary>
        private void SpawnMonsterNow()
        {
            // 关卡制（步骤 4a）：怪物等级由关卡固定，不再跟随主角等级。
            // 这样低级关不会因为主角变强而变难，高级关也不会被主角等级拔高。
            int monsterLevel = Player.CurrentStage.monsterLevel;

            // 类型权重也按关卡怪级抽，而不是主角等级：难度既然由关卡钉住，
            // 类型也该跟着关卡走，否则主角练高之后回头刷第 1 章会刷出终局档位的怪。
            MonsterTier tier = _pendingTier;
            _pendingTier = MonsterTier.Normal;

            // 终局 BOSS 的类型按挑战次数轮换（近战 → 远程 → 暴击怪），不再掷骰：
            // 同样面板下盗贼 BOSS 的胜率只有远程的一半，"这把抽到谁"不该决定成败。
            // 其它怪仍然按关卡怪级的权重随机。
            MonsterType type = tier == MonsterTier.Boss && IsFinalStage
                ? (MonsterType)(_finalBossAttempt++ % 3)
                : MonsterData.RollType(monsterLevel);

            // Boss 不带词缀：Boss 面板本身已经很高，再叠词缀会让胜率失控。
            MonsterAffix affix = tier == MonsterTier.Boss ? MonsterAffix.None : MonsterData.RollAffix();

            // 关底 Boss 战前回满血：主角是连续作战走到这里的，不补给的话这场基本必输。
            // 精英默认不回（HealBeforeFight=false）：让「刷小怪 → 打精英」成为一条连续的消耗链，
            // 手一抖就真的会死在这里，而不是每一关都从头满血开始。
            bool healFirst = tier == MonsterTier.Boss || GameConfig.HealBeforeFight;
            if (healFirst)
            {
                Player.FillHp();
                AddLog("♥ 决战前已恢复全部生命", GameConfig.LogHeal);
            }

            Monster = MonsterData.CreateForLevel(monsterLevel, type, tier, affix);
            CurrentState = State.Fighting;
            _battle.BeginBattle(Player);

            // 生成日志体现档次与词缀（要求 4）。
            string shieldText = Monster.shield > 0 ? $"   护盾 {Monster.shield}" : "";
            string affixText = Monster.HasAffix ? $"   词缀：{Monster.AffixName}" : "";
            AddLog($"⚔ {Monster.Title} 出现   HP {Monster.maxHp}{shieldText}{affixText}",
                MonsterSpawnColor(tier, type));
        }

        /// <summary>
        /// 生成日志的颜色：Boss 红、精英金，普通怪按类型区分。
        /// 这是**档位 / 类型辨识色**，属于数据而非主题配色：它的唯一职责是让
        /// 玩家一眼分出生成了什么，不参与全局配色统一，所以刻意保留字面量。
        /// </summary>
        private static Color MonsterSpawnColor(MonsterTier tier, MonsterType type)
        {
            if (tier == MonsterTier.Boss) return new Color(1f, 0.42f, 0.30f);
            if (tier == MonsterTier.Elite) return new Color(1f, 0.85f, 0.30f);
            return MonsterTypeColor(type);
        }

        /// <summary>日志里按类型区分颜色：战士偏青、弓箭手偏黄绿、盗贼偏红。</summary>
        private static Color MonsterTypeColor(MonsterType type)
        {
            switch (type)
            {
                case MonsterType.Melee: return new Color(0.62f, 0.84f, 1f);
                case MonsterType.Ranged: return new Color(0.70f, 0.95f, 0.60f);
                default: return new Color(1f, 0.58f, 0.52f);
            }
        }

        // ==================== 首通奖励与横幅（步骤 4c） ====================

        /// <summary>
        /// 首通奖励：金币。章末关（打完一整章）额外再加一笔。
        /// 只给金币不给装备是有意的——掉落系统本来就在发装备，首通再塞一件就要回答
        /// 「自动装备还是卖掉」（现有流程里这两件事都绑在掉落上），金币则是干净的。
        /// 金额公式在 StageCatalog.FirstClearGold，界面预览用的是同一个函数。
        /// </summary>
        private int GrantFirstClearReward(int chapter, int stage)
        {
            bool chapterFinal = StageCatalog.IsChapterFinal(stage);
            int gold = StageCatalog.FirstClearGold(chapter, stage);

            Player.gold += gold;
            TotalGoldEarned += gold;

            AddLog(chapterFinal
                    ? $"  ★ 首通奖励：+{gold} 金币（含章节通关奖励 +{GameConfig.ChapterClearGoldBonus}）"
                    : $"  ★ 首通奖励：+{gold} 金币",
                GameConfig.LogGold);

            return gold;
        }

        /// <summary>横幅主标题。空串表示不显示。</summary>
        public string BannerTitle { get; private set; } = string.Empty;

        /// <summary>横幅副标题（可空）。</summary>
        public string BannerSubtitle { get; private set; } = string.Empty;

        /// <summary>横幅剩余时间（真实秒，受速度档位影响）。</summary>
        public float BannerTimeLeft { get; private set; }

        /// <summary>弹一条居中横幅。空标题等价于立刻收起。</summary>
        public void ShowBanner(string title, string subtitle = "")
        {
            BannerTitle = title ?? string.Empty;
            BannerSubtitle = subtitle ?? string.Empty;
            BannerTimeLeft = string.IsNullOrEmpty(BannerTitle) ? 0f : GameConfig.BannerDuration;
        }

        private void TickBanner(float realDt)
        {
            if (BannerTimeLeft <= 0f) return;
            BannerTimeLeft = Mathf.Max(0f, BannerTimeLeft - realDt);
            if (BannerTimeLeft <= 0f)
            {
                BannerTitle = string.Empty;
                BannerSubtitle = string.Empty;
            }
        }

        /// <summary>击杀结算：经验、升级、掉落、自动装备（要求 8、11、14）。</summary>
        private void HandleMonsterKilled()
        {
            // 1) 计数
            TotalKills++;

            // 档次统计：只有真正击杀精英 / Boss 才计数。
            if (Monster.tier == MonsterTier.Elite) EliteKills++;
            else if (Monster.tier == MonsterTier.Boss) BossKills++;

            // 关卡推进（步骤 4a）：普通怪和关底都计入本关击杀数，
            // 攒满 killTarget 即通关；下一关从普通怪重新开始。
            Player.stageKillCount++;
            ResolveStageProgress();

            // 2) 击杀回血与经验
            int reward = GrantKillReward();

            // 3) 升级结算（普通升级 / 满级后的巅峰升级）
            ResolveLevelUp(reward);

            // 4) 掉落与自动装备
            ResolveDrops();

            // 5) 进入重生等待
            EnterRespawnWait();

            // 技能三选一放在状态复位之后，让它覆盖 WaitingRespawn（选完再回到等待重生）。
            if (Player.pendingSkillPicks > 0) OpenSkillChoice();
        }

        /// <summary>
        /// 关卡推进的入口：先把「打满了」和「还差一只」分开，
        /// 具体结算与预告各自放在下面的小方法里。
        /// </summary>
        private void ResolveStageProgress()
        {
            if (Player.IsStageCleared)
            {
                ResolveStageClear();
                return;
            }

            AnnounceStageBossIfNext();
        }

        /// <summary>
        /// 通关结算：首通金币、章节解锁、横幅与日志。
        /// 终局（11-10）已经首通过时，只有「刚好又打满一轮」才结算；
        /// 之后继续杀的那几只只把计数清零继续刷，不再重复等待 StageClearDelay。
        /// </summary>
        private void ResolveStageClear()
        {
            if (SuppressFinalStageFarmKill()) return;

            _justClearedStage = true;
            int clearedChapter = Player.chapterIndex;
            int clearedStage = Player.stageIndex;
            bool chapterFinal = StageCatalog.IsChapterFinal(clearedStage);

            // 先记账再推进：AdvanceStage 会改 chapterIndex，之后就认不出刚打完哪一关了。
            bool firstClear = Player.MarkStageFirstCleared(clearedChapter, clearedStage);
            int prevMaxChapter = Player.maxChapterUnlocked;

            bool advanced = Player.AdvanceStage();

            // 终局没有下一关：把本关计数清零，继续刷同一关，不再触发通关结算。
            if (!advanced) Player.stageKillCount = 0;

            AddLog(
                advanced
                    ? $"★ 通关 {clearedChapter}-{clearedStage}！进入 {Player.CurrentStage.Title}（怪物 Lv.{Player.CurrentStage.monsterLevel}）"
                    : $"★ 通关 {clearedChapter}-{clearedStage}！已经是最后一关",
                GameConfig.LogGold);

            // 首通金币 + 章节解锁合成一条横幅。
            // 拆成两条时后者会把前者顶掉，玩家看不到自己拿了多少金币。
            int clearGold = firstClear ? GrantFirstClearReward(clearedChapter, clearedStage) : 0;
            bool unlocked = Player.maxChapterUnlocked > prevMaxChapter;
            int newMax = Player.maxChapterUnlocked;

            if (unlocked) AddLog($"✦ 新章节解锁：第 {newMax} 章", GameConfig.LogUnlock);

            if (firstClear || unlocked)
            {
                ShowClearBanner(clearedChapter, clearedStage, chapterFinal, firstClear, clearGold, unlocked, newMax);
            }

            if (unlocked) SaveSystem.Save(Player);
        }

        /// <summary>
        /// 终局已首通后的重复挑战：打满一轮就把击杀数清零继续刷，
        /// 不再进入通关结算（否则每杀一只都会多等一次 StageClearDelay 并刷通关日志）。
        /// 返回 true 表示本次击杀已被吞掉。
        /// </summary>
        private bool SuppressFinalStageFarmKill()
        {
            if (!IsFinalStage) return false;

            // 老存档可能存在「打满了但还没记首通」的终局进度：先让它正常结算一次，
            // 否则清零后永远卡在 0，首通奖励再也发不出来。
            if (!Player.IsStageFirstCleared(Player.chapterIndex, Player.stageIndex)) return false;

            if (Player.stageKillCount < Player.CurrentStage.killTarget) return false;
            Player.stageKillCount = 0;
            return true;
        }

        /// <summary>通关横幅：首通奖励与章节解锁合成一条，避免后一条把前一条顶掉。</summary>
        private void ShowClearBanner(int clearedChapter, int clearedStage, bool chapterFinal,
            bool firstClear, int clearGold, bool unlocked, int newMax)
        {
            string title = chapterFinal
                ? $"★ 第 {clearedChapter} 章 通关！"
                : $"★ 首通 {clearedChapter}-{clearedStage}";
            if (!firstClear) title = $"✦ 第 {newMax} 章 已解锁";

            string sub = firstClear ? $"+{clearGold} 金币" : "";
            if (unlocked)
            {
                string unlockText = $"第 {newMax} 章已解锁（怪物 Lv.{GameConfig.ChapterMonsterLevelStep * (newMax - 1) + 1}~{GameConfig.ChapterMonsterLevelStep * newMax}）";
                sub = string.IsNullOrEmpty(sub) ? unlockText : sub + " · " + unlockText;
            }
            ShowBanner(title, sub);
        }

        /// <summary>还差一只时提前揭示关底档次：先开商店，再生成（SpawnMonster 会拦下这次生成）。</summary>
        private void AnnounceStageBossIfNext()
        {
            if (Player.StageKillsLeft != 1) return;

            _pendingTier = Player.StageClearTier;
            string tierName = _pendingTier == MonsterTier.Boss ? "BOSS" : "精英怪";
            AddLog($"▼ 本关还剩 1 只（关底 {tierName}），击杀后通关", GameConfig.LogSystem);
        }

        /// <summary>
        /// 击杀回血与经验入账，返回本次获得的经验值。
        /// 回血按档次区分：普通 10%、精英 50%、Boss 回满。
        /// 真正让挂机不中断的仍是「阵亡后自动复活」，回血只是节奏缓冲。
        /// </summary>
        private int GrantKillReward()
        {
            int reward = GameConfig.MonsterExpReward(Monster.level, Monster.type, Monster.tier);
            AddLog($"☠ 击杀 {Monster.Title}，获得 {reward} 经验", GameConfig.LogGold);

            int healAmount = Mathf.RoundToInt(
                Player.MaxHp * GameConfig.MonsterTierHealOnKillPercent[(int)Monster.tier]);
            int hpBefore = Player.currentHp;
            Player.currentHp = Mathf.Min(Player.MaxHp, Player.currentHp + healAmount);
            int healed = Player.currentHp - hpBefore;
            if (healed > 0)
            {
                AddLog($"  ＋ 击杀回复 {healed} 生命", GameConfig.LogHeal);
            }

            return reward;
        }

        /// <summary>
        /// 升级结算。满级之后升的是巅峰等级：每级 +1 攻击 / +1 防御，
        /// 且每 ParagonPassiveEvery 级给一次「技能三选一」。
        /// </summary>
        private void ResolveLevelUp(int reward)
        {
            bool wasMax = Player.IsMaxLevel;
            int levelsGained = Player.AddExp(reward);
            if (levelsGained <= 0) return;

            if (wasMax)
            {
                AddLog($"✧ 巅峰升级！巅峰 {Player.paragonLevel}  攻击 {Player.Attack}  防御 {Player.Defense}",
                    GameConfig.LogParagon);
                Debug.Log($"[AutoBattle] 巅峰等级提升到 {Player.paragonLevel}");

                // 每 10 级给一次「技能三选一」；一次可能跨过多个节点，累加待选次数。
                int wantSkills = Mathf.Min(Player.paragonLevel / GameConfig.ParagonPassiveEvery,
                    SkillCatalog.All.Length);
                int owed = wantSkills - Player.OwnedSkillCount - Player.pendingSkillPicks;
                if (owed > 0)
                {
                    Player.pendingSkillPicks += owed;
                    AddLog($"✧ 获得 {owed} 次技能选择（待选 {Player.pendingSkillPicks} 次）",
                        GameConfig.LogParagon);
                }
            }
            else
            {
                // 升级补足新增的生命上限，避免突然回满。
                int hpGain = GameConfig.PlayerMaxHpPerLevel * levelsGained;
                Player.currentHp = Mathf.Min(Player.MaxHp, Player.currentHp + hpGain);

                AddLog($"★ 升级！Lv.{Player.level}  攻击 {Player.Attack}  防御 {Player.Defense}",
                    GameConfig.LogHeal);
                Debug.Log($"[AutoBattle] 升级到 Lv.{Player.level}");

                if (Player.IsMaxLevel)
                {
                    AddLog("🏆 已达 100 级，之后的经验转为巅峰等级。", GameConfig.LogGold);
                    Debug.Log("[AutoBattle] 达到满级 100");
                }
            }
        }

        /// <summary>
        /// 掉落判定（要求 11、12、13）：
        /// 普通怪走 30% 概率 + 常规品质权重；精英必掉 1 件、Boss 必掉 2 件，
        /// 且品质走各自固定的权重表（精英：稀有 70/史诗 25/传说 5；Boss：史诗 80/传说 20），
        /// 掉落等级取怪物自身等级（精英/Boss 是梯度等级，因此装备会比主角当前等级高）。
        /// </summary>
        private void ResolveDrops()
        {
            int dropCount = GameConfig.MonsterTierGuaranteedDrops[(int)Monster.tier];
            if (dropCount == 0 && LootSystem.RollDrop()) dropCount = 1;

            float[] fixedWeights = GameConfig.MonsterTierDropQualityWeights[(int)Monster.tier];
            int dropLevel = Monster.tier == MonsterTier.Normal ? Player.level : Monster.level;

            for (int i = 0; i < dropCount; i++)
            {
                TotalDrops++;
                Equipment dropped = fixedWeights != null
                    ? LootSystem.CreateEquipmentAtLevel(dropLevel, fixedWeights)
                    : LootSystem.CreateRandomEquipment(dropLevel);
                // 副词条必须一起打出来：只打主属性时，玩家会以为「传说武器没有副属性」。
                string dropLine = $"◆ 掉落 {dropped.Title}  {dropped.StatLine}";
                if (!string.IsNullOrEmpty(dropped.AffixLine)) dropLine += "\n    " + dropped.AffixLine;
                AddLog(dropLine, dropped.QualityColor);

                bool equipped = TryAutoEquip(dropped, out bool pending);
                if (equipped)
                {
                    TotalEquipped++;
                    AddLog($"  ↑ 评分更高，已自动穿戴（评分 {EquipmentScore.Of(Player, dropped, dropped.slot):0}）", GameConfig.LogSuccess);
                    Debug.Log($"[AutoBattle] 自动穿戴 {dropped.Title} {dropped.StatLine}");
                }
                else if (pending)
                {
                    // 进入待选不等于「不要了」：钱一分没进，装备留在托盘里等玩家比较。
                    // 之所以不当场卖，是锁/手动模式的意思就是「我要自己看」。
                    AddLog($"  ◈ 已放入{Player.GetEquipment(dropped.slot)?.SlotName ?? dropped.SlotName}待选（分数更高，等待对比）",
                        GameConfig.LogInfo);
                }
                else
                {
                    // 卖闲置装备换金币：不再直接丢弃，单价随装备等级与品质提升（GameConfig.SellPrice）。
                    Equipment current = Player.GetEquipment(dropped.slot);
                    int gold = GameConfig.SellPrice(dropped.level, dropped.quality);
                    Player.gold += gold;
                    TotalSold++;
                    TotalGoldEarned += gold;

                    AddLog($"  $ 弱于当前{current.SlotName}（{EquipmentScore.Of(Player, dropped, dropped.slot):0} < {EquipmentScore.Of(Player, current, current.slot):0}），卖出 +{gold} 金币（共 {Player.gold}）",
                        GameConfig.LogGold);
                    Debug.Log($"[AutoBattle] 卖出 {dropped.Title} +{gold} 金币");
                }
            }
        }

        /// <summary>
        /// 进入 0.5 秒重生等待（要求 1）。刚通关那一只多停一会儿（StageClearDelay）：
        /// 否则首通横幅还没看清，下一关的怪已经在打了——4a 加的这个配置项之前根本没接线。
        /// </summary>
        private void EnterRespawnWait()
        {
            float respawnDelay = _justClearedStage
                ? GameConfig.StageClearDelay
                : GameConfig.MonsterRespawnDelay;
            _justClearedStage = false;

            Monster = null;
            CurrentState = State.WaitingRespawn;
            _respawnTimer = respawnDelay;
        }

        /// <summary>
        /// 要求 14：掉落装备与当前同槽位装备比较评分，更强则自动穿戴。
        /// 2026-10-18 扩展：当前装备被锁，或设置里关掉「自动换装」时，更强的掉落不再直接穿，
        /// 而是放进 / 保留在待选托盘（<paramref name="pending"/> 为 true），等玩家在对比面板决定。
        /// 待选里已有更好的同槽装备时，新掉落的差件照旧卖掉，避免托盘被反复刷新。
        /// </summary>
        private bool TryAutoEquip(Equipment dropped, out bool pending)
        {
            pending = false;
            if (dropped == null) return false;

            Equipment current = Player.GetEquipment(dropped.slot);
            bool hold = current != null && (current.locked || !GameSettings.AutoEquip);
            float droppedScore = EquipmentScore.Of(Player, dropped, dropped.slot);

            if (hold)
            {
                // 锁住当前装备只挡「自动替换」，不意味着什么垃圾都要留在托盘里。
                // 比身上还差的掉落照旧直接卖店，否则待选角标会一直亮着，
                // 对比面板里躺的全是不该看的低分件。
                if (droppedScore <= EquipmentScore.Of(Player, current, current.slot)) return false;

                Equipment queued = _pendingDrops[(int)dropped.slot];
                if (queued != null && droppedScore <= EquipmentScore.Of(Player, queued, queued.slot)) return false;

                _pendingDrops[(int)dropped.slot] = dropped;
                pending = true;
                if (queued != null)
                {
                    // 旧的待选件被顶掉时不能凭空蒸发：按普通卖店价折现，日志里交代清楚。
                    int gold = GameConfig.SellPrice(queued.level, queued.quality);
                    Player.gold += gold;
                    TotalSold++;
                    TotalGoldEarned += gold;
                    AddLog($"  ↻ 待选更新（旧待选评分 {EquipmentScore.Of(Player, queued, queued.slot):0} → {droppedScore:0}，旧件折价 +{gold}）",
                        GameConfig.LogInfo);
                }
                return false;
            }

            if (current != null && droppedScore <= EquipmentScore.Of(Player, current, current.slot)) return false;

            WearEquipment(dropped);
            return true;
        }

        /// <summary>
        /// 穿上装备并把生命上限的增量补给血量：
        /// 衣服换强时上限会涨，不补这一截会出现「换了更好的衣服，血量占比反而掉了」。
        /// </summary>
        private void WearEquipment(Equipment e)
        {
            int oldMaxHp = Player.MaxHp;
            Player.SetEquipment(e);

            int delta = Player.MaxHp - oldMaxHp;
            if (delta > 0) Player.currentHp = Mathf.Min(Player.MaxHp, Player.currentHp + delta);
            else Player.currentHp = Mathf.Min(Player.currentHp, Player.MaxHp);
        }

        /// <summary>
        /// 主角阵亡：不再结束战斗，而是短暂等待后满血复活，继续自动挂机。
        /// 阵亡因此变成一个节奏事件，而不是需要玩家点「再战」的终局。
        /// </summary>
        private void HandlePlayerDied()
        {
            TotalDeaths++;

            // 阵亡不给经验（2026-10-18）：以前按打掉的血量返经验，结果「送死」成了
            // 升级最快的打法，终局那堵墙变成「死够次数自动过」。现在的循环回到设计意图：
            // 打小怪 → 打精英/Boss → 打不过 → 回本关刷小怪吃经验 → 再来。
            // 关卡制（步骤 4a）：阵亡 = 本关白打，击杀数归零、关底退回去重来。
            _pendingTier = MonsterTier.Normal;
            if (Player.stageKillCount > 0)
            {
                int lost = Player.stageKillCount;
                Player.stageKillCount = 0;
                AddLog($"✖ 本关进度作废（已击杀 {lost}/{Player.CurrentStage.killTarget}），{Player.CurrentStage.Title} 重新开始",
                    GameConfig.LogDanger);
            }

            AddLog($"✖ 主角阵亡（第 {TotalDeaths} 次），{GameConfig.PlayerReviveDelay:0.#} 秒后自动复活",
                GameConfig.LogDanger);
            Debug.Log($"[AutoBattle] 主角阵亡第 {TotalDeaths} 次，将自动复活");

            Monster = null;
            CurrentState = State.Reviving;
            _reviveTimer = GameConfig.PlayerReviveDelay;

            // 复活后刷的是本关的普通怪（_pendingTier 已复位），玩家要先把本关小怪刷过去，
            // 才能再见到关底——经验也正是从这些小怪身上来的。
        }

        /// <summary>自动复活：回满生命并立刻投入下一场战斗。</summary>
        private void RevivePlayer()
        {
            Player.FillHp();
            AddLog($"♥ 已复活，继续战斗（生命 {Player.currentHp}/{Player.MaxHp}）", GameConfig.LogHeal);

            // 复活与换怪同步，省掉一次多余的等待。
            SpawnMonster();
        }

        // ==================== 辅助 ====================

        /// <summary>
        /// 追加一条「系统/进度/商店」日志（裁剪到上限、累计条数都由 BattleLog 负责，要求 15）。
        /// 战斗过程行不走这里：BattleSystem 直接用 <see cref="BattleLog.AddCombat"/>，免得两类日志混在一起没法过滤。
        /// </summary>
        public void AddLog(string message, Color color) => Log.AddSystem(message, color);

        /// <summary>定时自动存档（要求 17）。用真实时间，暂停时也会存。</summary>
        private void AutoSaveTick(float realDt)
        {
            _autoSaveTimer += realDt;
            if (_autoSaveTimer < GameConfig.AutoSaveInterval) return;

            _autoSaveTimer = 0f;
            SaveSystem.Save(Player);
        }
    }
}

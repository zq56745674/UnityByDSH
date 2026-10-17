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
        public List<LogEntry> Log { get; } = new List<LogEntry>();

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
        public int LogVersion { get; private set; }

        /// <summary>
        /// 累计写入过多少条日志（只增不减）。Log 会被 <see cref="TrimLog"/> 裁到上限，
        /// 所以「这一帧新增了几条」不能拿 Log.Count 的前后差去算——满 60 条之后差值恒为 0，
        /// 飘字与实时 DPS 曾因此永久失效。UI 请用本值做游标。
        /// </summary>
        public int TotalLogEntries { get; private set; }

        // ==================== 内部 ====================

        private readonly BattleSystem _battle = new BattleSystem();
        private float _respawnTimer;
        private float _reviveTimer;
        private float _autoSaveTimer;
        private bool _loadedFromSave;

        /// <summary>本局是否已经开始过（区分"第一次点开始"与"暂停后继续"）。</summary>
        private bool _runStarted;

        /// <summary>下一只要生成的档次（由关卡进度触发，生成后立刻复位为 Normal）。</summary>
        private MonsterTier _pendingTier = MonsterTier.Normal;

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

            // 选关 / 存档 / 强化 / 技能面板打开时冻结整局（含商店倒计时），
            // 否则玩家挑关、挑档、挑强化目标、挑技能时怪会照打。
            // 技能面板原先不在名单里，理由是「它只在战斗停下的空档弹出」——但槽位按钮
            // 任何时刻都能点，战斗中点开就是一整块浮层盖住血条让怪白打，所以一并冻住。
            if (StagePanelOpen || SavePanelOpen || EnhancePanelOpen || SkillPanelOpen) return;

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

        // ---- 四个浮层（选关 / 技能 / 存档 / 强化）互斥，规则只在这一处 ----

        /// <summary>关掉全部浮层。任何 Open 之前先走这里，保证同时只开一个。</summary>
        private void CloseAllPanels()
        {
            StagePanelOpen = false;
            SkillPanelOpen = false;
            SavePanelOpen = false;
            EnhancePanelOpen = false;
        }

        /// <summary>打开 / 关闭选关面板。</summary>
        public void ToggleStagePanel()
        {
            bool open = !StagePanelOpen;
            CloseAllPanels();
            StagePanelOpen = open;
        }

        /// <summary>关闭选关面板。</summary>
        public void CloseStagePanel() => StagePanelOpen = false;

        /// <summary>打开技能面板（同时关掉其它浮层）。</summary>
        public void OpenSkillPanel()
        {
            CloseAllPanels();
            SkillPanelOpen = true;
        }

        /// <summary>关闭技能面板。</summary>
        public void CloseSkillPanel() => SkillPanelOpen = false;

        /// <summary>打开 / 关闭存档槽面板。</summary>
        public void ToggleSavePanel()
        {
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
            _battle.ResetSkillState();

            bool loaded = SaveSystem.Load(Player);
            _loadedFromSave = loaded;

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
            IsRunning = false;
            _runStarted = false;
            CloseAllPanels();
            CurrentState = State.WaitingRespawn;
            _respawnTimer = 0f;
            _reviveTimer = 0f;
            _autoSaveTimer = 0f;
            _potionCooldownTimer = 0f;
            _shopTimer = 0f;

            Log.Clear();
            LogVersion++;
            BannerTitle = string.Empty;
            BannerSubtitle = string.Empty;
            BannerTimeLeft = 0f;

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
                _battle.ResetSkillState();
                _loadedFromSave = false;
                Monster = null;
                Player.shield = 0;
                _pendingTier = MonsterTier.Normal;
                IsRunning = false;
                _runStarted = false;
                CurrentState = State.WaitingRespawn;
                _respawnTimer = 0f;
                _reviveTimer = 0f;
                _autoSaveTimer = 0f;
                _potionCooldownTimer = 0f;
                _shopTimer = 0f;
                Log.Clear();
                LogVersion++;
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
            IsRunning = false;
            _runStarted = false;
            CloseAllPanels();
            CurrentState = State.WaitingRespawn;
            _respawnTimer = 0f;
            _reviveTimer = 0f;
            _autoSaveTimer = 0f;
            _potionCooldownTimer = 0f;
            _shopTimer = 0f;

            Log.Clear();
            LogVersion++;
            BannerTitle = string.Empty;
            BannerSubtitle = string.Empty;
            BannerTimeLeft = 0f;
            AddLog("↺ 已重置，进度与存档已清空。", GameConfig.LogWarn);
        }

        // ==================== 技能装配 ====================

        /// <summary>技能面板是否展开（UI 用）。</summary>
        public bool SkillPanelOpen { get; set; }

        /// <summary>存档槽面板是否展开（UI 用）。它和选关面板一样会冻结战斗。</summary>
        public bool SavePanelOpen { get; set; }

        /// <summary>装备强化面板是否展开（UI 用）。同样冻结战斗，避免边打边强化。</summary>
        public bool EnhancePanelOpen { get; set; }

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
            if (Player.pendingSkillPicks > 0)
            {
                OpenSkillChoice();
                return;
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
            // 战斗系统（BattleSystem）是直接 Log.Add 的，不经过 AddLog，所以要在这里补记累计条数：
            // 飘字/实时 DPS 的解析游标（GameUI.Core.cs 的 _lastParsedLogTotal）与日志面板刷新
            // （GameUI.Core.cs 的 LogVersion 比较）都依赖这两个计数器，漏记会导致它们永远不动。
            int logCountBefore = Log.Count;
            BattleTickResult result = _battle.Tick(Player, Monster, dt, Log);
            int appended = Log.Count - logCountBefore;
            if (appended > 0)
            {
                TotalLogEntries += appended;
                LogVersion++;
            }

            if (result == BattleTickResult.MonsterKilled) HandleMonsterKilled();
            else if (result == BattleTickResult.PlayerDied) HandlePlayerDied();

            TrimLog();
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
            MonsterType type = MonsterData.RollType(monsterLevel);
            MonsterTier tier = _pendingTier;
            _pendingTier = MonsterTier.Normal;

            // Boss 不带词缀：Boss 面板本身已经很高，再叠词缀会让胜率失控。
            MonsterAffix affix = tier == MonsterTier.Boss ? MonsterAffix.None : MonsterData.RollAffix();

            // 关底战前回满血：主角是连续作战走到这里的，不补给的话这场基本必输。
            if (tier != MonsterTier.Normal)
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
        /// 关卡推进与结算：通关判定、首通金币、章节解锁横幅、关底预告。
        /// 拆出来是因为这一段有 50 多行且与「击杀后该干什么」的其它环节无关。
        /// </summary>
        private void ResolveStageProgress()
        {
            if (Player.IsStageCleared)
            {
                _justClearedStage = true;
                int clearedChapter = Player.chapterIndex;
                int clearedStage = Player.stageIndex;
                bool chapterFinal = StageCatalog.IsChapterFinal(clearedStage);

                // 先记账再推进：AdvanceStage 会改 chapterIndex，之后就认不出刚打完哪一关了。
                bool firstClear = Player.MarkStageFirstCleared(clearedChapter, clearedStage);
                int prevMaxChapter = Player.maxChapterUnlocked;

                bool advanced = Player.AdvanceStage();

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

                if (unlocked) SaveSystem.Save(Player);
            }
            else if (Player.StageKillsLeft == 1)
            {
                // 下一只就是关底：先开商店，再生成（SpawnMonster 会拦下这次生成）。
                _pendingTier = Player.StageClearTier;
                string tierName = _pendingTier == MonsterTier.Boss ? "BOSS" : "精英怪";
                AddLog($"▼ 本关还剩 1 只（关底 {tierName}），击杀后通关",
                    GameConfig.LogSystem);
            }
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

                bool equipped = TryAutoEquip(dropped);
                if (equipped)
                {
                    TotalEquipped++;
                    AddLog($"  ↑ 评分更高，已自动穿戴（评分 {EquipmentScore.Of(Player, dropped, dropped.slot):0}）", GameConfig.LogSuccess);
                    Debug.Log($"[AutoBattle] 自动穿戴 {dropped.Title} {dropped.StatLine}");
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

        /// <summary>要求 14：掉落装备与当前同槽位装备比较评分，更强则自动穿戴。</summary>
        private bool TryAutoEquip(Equipment dropped)
        {
            if (dropped == null) return false;

            Equipment current = Player.GetEquipment(dropped.slot);
            if (current != null &&
                EquipmentScore.Of(Player, dropped, dropped.slot) <= EquipmentScore.Of(Player, current, current.slot)) return false;

            int oldMaxHp = Player.MaxHp;
            Player.SetEquipment(dropped);

            // 生命上限可能因衣服变更而提高，同步补足增量，避免穿装备反而「掉血」。
            int delta = Player.MaxHp - oldMaxHp;
            if (delta > 0) Player.currentHp = Mathf.Min(Player.MaxHp, Player.currentHp + delta);
            else Player.currentHp = Mathf.Min(Player.currentHp, Player.MaxHp);

            return true;
        }

        /// <summary>
        /// 主角阵亡：不再结束战斗，而是短暂等待后满血复活，继续自动挂机。
        /// 阵亡因此变成一个节奏事件，而不是需要玩家点「再战」的终局。
        /// </summary>
        private void HandlePlayerDied()
        {
            TotalDeaths++;

            // 阵亡经验：按打掉的血量占比折算（见下面 partialExp 的说明）。
            // 必须在 Monster = null 之前取，之后就拿不到这只怪的数据了。
            int partialExp = 0;
            int dealtHp = 0;
            int monsterMaxHp = 0;
            if (Monster != null && Monster.maxHp > 0)
            {
                dealtHp = Mathf.Clamp(Monster.maxHp - Monster.currentHp, 0, Monster.maxHp);
                monsterMaxHp = Monster.maxHp;
                if (dealtHp > 0)
                {
                    int full = GameConfig.MonsterExpReward(Monster.level, Monster.type, Monster.tier);
                    partialExp = Mathf.RoundToInt(full * (float)dealtHp / Monster.maxHp);
                }
            }

            // 关卡制（步骤 4a）：阵亡 = 本关白打，击杀数归零、关底退回去重来。
            // 这就是关卡制下的「关卡墙」——打不过就卡在这一关反复重试，直到属性/装备跟上。
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

            // 阵亡不再是一无所获：按已打掉的血量占比给经验，最多拿到该怪全额。
            // 全额给经验会让「送死刷经验」成为最优解；一点不给则会把「打不过」变成永久死锁
            // （打不过 → 没经验 → 属性不涨 → 还是打不过）。按占比折算两头都堵上，
            // 也顺带把终局那堵墙变成「多磨几次能过去」。
            if (partialExp > 0)
            {
                AddLog($"✖ 未击杀，但打掉了 {dealtHp}/{monsterMaxHp} 血量，获得 {partialExp} 经验", GameConfig.LogInfo);
                ResolveLevelUp(partialExp);
            }
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

        /// <summary>追加一条战斗日志，并裁剪到上限（要求 15）。</summary>
        public void AddLog(string message, Color color)
        {
            Log.Add(new LogEntry(message, color));
            TotalLogEntries++;
            TrimLog();
            LogVersion++;
        }

        private void TrimLog()
        {
            int overflow = Log.Count - GameConfig.MaxLogEntries;
            if (overflow > 0)
            {
                Log.RemoveRange(0, overflow);
                LogVersion++;
            }
        }

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

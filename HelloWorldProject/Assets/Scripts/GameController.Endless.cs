using UnityEngine;

namespace AutoBattle
{
    /// <summary>
    /// 无尽模式逻辑（docs/endless-mode.md）。和战役共用战斗/掉落/升级，
    /// 只在「怪物怎么来、清层怎么算、死了怎么办」这三处走自己的分支。
    ///
    /// 约定：<see cref="EndlessRun"/> 是这一局的临时状态，不进存档；
    /// 只有 <see cref="PlayerData.endlessBestFloor"/>（每档最高清层）会落盘。
    /// </summary>
    public partial class GameController
    {
        /// <summary>本档无尽最高清层（未打过 = 0）。</summary>
        public int EndlessBestFloor => Player != null ? Player.endlessBestFloor : 0;

        /// <summary>
        /// 面板建议的起始层。转生后新一世在重通 11-10 之前只能从第 1 层开始；
        /// 重通后恢复按最高层计算的检查点。
        /// </summary>
        public int EndlessSuggestedStartFloor
        {
            get
            {
                if (Player == null) return 1;
                if (!Player.IsStageFirstCleared(GameConfig.ChapterCount, GameConfig.StagesPerChapter)) return 1;

                int best = Mathf.Max(0, Player.endlessBestFloor);
                if (best <= 0) return 1;

                // 成就点兑换的起始层加成只抬高建议值；不能越过本档最高层（StartEndlessRun 还会再夹一次）。
                int bonus = Mathf.Clamp(Player.endlessStartRank, 0, AchievementSystem.MaxEndlessStartRank);
                return Mathf.Min(best, EndlessCatalog.StartFloorFor(best) + bonus);
            }
        }

        /// <summary>能否真正开始一局无尽：已解锁、等级满级、且没有待处理的流派选择/结算。</summary>
        public bool CanEnterEndless =>
            EndlessUnlocked && Player != null
            && Player.level >= GameConfig.MaxLevel
            && !ChoicePending;

        /// <summary>打开 / 关闭无尽面板。结果面板开着时禁止，避免两层浮层叠在一起。</summary>
        /// <summary>刚记进历史榜的那一局的时间戳（0 = 本次会话还没打完过）；UI 用来标「本次」。</summary>
        public long LastEndlessRecordUtc { get; private set; }

        public void ToggleEndlessPanel()
        {
            if (ChoicePending || EndlessResultOpen) return;
            bool open = !EndlessPanelOpen;
            CloseAllPanels();
            EndlessPanelOpen = open;
        }

        public void CloseEndlessPanel() => EndlessPanelOpen = false;

        // ==================== 深层契约（B，docs/gameplay-expansion.md §5） ====================
        // 面板上勾 0~3 条，每条一个档位；开局时把档位拷进 EndlessRunState 并聚合成 ContractMods。
        // 这里的选择只活在本次会话里（不进存档），下一次开局面板里还是上次勾的那套，方便重复挑战。

        private readonly int[] _endlessContractTiers = new int[ContractCatalog.All.Length];

        /// <summary>面板当前勾选的契约档位（只读给 UI 画选中态）。</summary>
        public int[] EndlessContractTiers => _endlessContractTiers;

        /// <summary>当前勾了几条。</summary>
        public int EndlessContractCount => ContractCatalog.CountSelected(_endlessContractTiers);

        /// <summary>当前勾选的档位系数之和（面板显示本局倍率）。</summary>
        public float EndlessContractCoef => ContractCatalog.CoefSum(_endlessContractTiers);

        /// <summary>
        /// 点某一档：再点一次同一档 = 取消；点别的档 = 换档。决绝只有 Ⅰ 档。
        /// 已选满 <see cref="ContractCatalog.MaxSelected"/> 条时再选新的会被拒绝（返回 false）。
        /// </summary>
        public bool ToggleContractTier(ContractKind kind, int tier)
        {
            int i = (int)kind;
            if (i < 0 || i >= _endlessContractTiers.Length) return false;
            ContractDef def = ContractCatalog.Get(kind);
            if (def == null || tier <= 0 || tier > def.TierCount) return false;

            if (_endlessContractTiers[i] == tier)
            {
                _endlessContractTiers[i] = 0;
                return true;
            }

            bool already = _endlessContractTiers[i] > 0;
            if (!already && ContractCatalog.CountSelected(_endlessContractTiers) >= ContractCatalog.MaxSelected)
            {
                AddLog($"🔒 契约最多同时选 {ContractCatalog.MaxSelected} 条，先取消一条再选", GameConfig.LogWarn);
                return false;
            }

            _endlessContractTiers[i] = tier;
            return true;
        }

        /// <summary>清空面板上的契约勾选（不影响已入账的累计契约分）。</summary>
        public void ClearContractSelection()
        {
            for (int i = 0; i < _endlessContractTiers.Length; i++) _endlessContractTiers[i] = 0;
        }

        /// <summary>本层的击杀需求（群袭不改击杀数，只改守关阶段的精英只数）。</summary>
        private int ContractKillTarget(int floor)
        {
            return EndlessCatalog.KillTargetOf(floor);
        }

        /// <summary>
        /// 群袭：守关怪按「本层层数 + N」的强度生成（N = 契约档位，0 = 不加强）。
        /// 只加怪不加难度（引擎里杂兵几乎免费），所以难度落在这只关底怪的属性上。
        /// </summary>
        private int ContractClearFloorBonus()
        {
            return Player != null ? Mathf.Max(0, Player.ContractClearFloorBonus) : 0;
        }

        // ==================== 深渊遗物（v1，docs/gameplay-expansion.md §4） ====================
        // 一局无尽里每清 10 层弹一次三选一；遗物只在本局有效，撤退/阵亡即清空、不进存档。
        // 序列由「本局种子 + 层号」派生，不消耗 UnityEngine.Random（战斗随机流），
        // 所以同种子同层永远给出同一组牌，与战斗走到哪一步无关。

        /// <summary>本局是否启用深渊遗物：首局不弹（先让玩家把无尽本身跑一遍），第二局起才有。</summary>
        public bool RelicsEnabledThisRun =>
            Player != null && (Player.endlessBestFloor > 0
                || (Player.endlessTop != null && Player.endlessTop.Count > 0));

        /// <summary>本局持有的遗物 id（无局时返回 null，UI 只读）。</summary>
        public System.Collections.Generic.List<int> RunRelics =>
            EndlessRun != null ? EndlessRun.relics : null;

        /// <summary>满槽后已点选、等着挑一件舍弃的那张牌；-1 = 没有。</summary>
        public int RelicTakePending => EndlessRun != null ? EndlessRun.relicTakePending : -1;

        /// <summary>生成三张候选（排除本局已持有的，避免出现「已经有了还给你选」）。</summary>
        private void OfferRelicChoice(int clearedFloor)
        {
            int maxTier = ContractCatalog.RelicMaxTier(Player != null ? Player.contractScore : 0);
            System.Collections.Generic.List<int> picks = RelicCatalog.RollChoices(
                EndlessRun.relicSeed, clearedFloor, EndlessRun.relics, maxTier);
            if (picks == null || picks.Count == 0) return;

            EndlessRun.pendingRelics.Clear();
            EndlessRun.pendingRelics.AddRange(picks);
            EndlessRun.relicTakePending = -1;
            EndlessRun.relicPendingFloor = clearedFloor;

            AddLog($"✦ 深渊馈赠 · 第 {clearedFloor} 层：三选一（本局 {EndlessRun.relics.Count}/{EndlessRun.RelicSlots} 槽）",
                GameConfig.LogUnlock);
        }

        /// <summary>
        /// 选一张牌。没满槽立刻拿到；满槽则先记下要拿哪张，等玩家点一件旧遗物舍弃
        /// （规则：满 6 槽后必须先卖一件才能拿一件）。
        /// </summary>
        public void TakeRelic(int id)
        {
            if (EndlessRun == null || !RelicChoiceOpen) return;
            if (!EndlessRun.pendingRelics.Contains(id)) return;

            if (EndlessRun.RelicsFull)
            {
                EndlessRun.relicTakePending = id;
                return;
            }

            EndlessRun.relics.Add(id);
            ApplyRelicMods();
            FinishRelicChoice();

            RelicDef def = RelicCatalog.Get(id);
            AddLog($"◈ 获得遗物「{def.name}」（{def.ClassName}）：{def.text}", GameConfig.LogUnlock);
        }

        /// <summary>满槽替换：舍弃 <paramref name="ownedId"/>，换入刚点选的那张。</summary>
        public void ReplaceRelicWith(int ownedId)
        {
            if (EndlessRun == null || !RelicChoiceOpen) return;

            int take = EndlessRun.relicTakePending;
            if (take < 0) return;

            int index = EndlessRun.relics.IndexOf(ownedId);
            if (index < 0) return;

            EndlessRun.relics.RemoveAt(index);
            EndlessRun.relics.Add(take);
            ApplyRelicMods();
            FinishRelicChoice();

            RelicDef oldDef = RelicCatalog.Get(ownedId);
            RelicDef newDef = RelicCatalog.Get(take);
            AddLog($"◈ 舍弃「{oldDef.name}」，换入「{newDef.name}」：{newDef.text}", GameConfig.LogUnlock);
        }

        /// <summary>满槽替换中途反悔：退回三张候选，不结束这次三选一。</summary>
        public void CancelRelicTake()
        {
            if (EndlessRun == null) return;
            EndlessRun.relicTakePending = -1;
        }

        /// <summary>跳过这一次三选一（槽位不够又不舍得换时的出口）。</summary>
        public void SkipRelicChoice()
        {
            if (EndlessRun == null || !RelicChoiceOpen) return;
            AddLog($"◇ 放弃了第 {EndlessRun.relicPendingFloor} 层的深渊遗物", GameConfig.LogInfo);
            FinishRelicChoice();
        }

        private void FinishRelicChoice()
        {
            EndlessRun.pendingRelics.Clear();
            EndlessRun.relicTakePending = -1;
            EndlessRun.relicPendingFloor = 0;
            // 选完之后别让玩家干等整段清层延迟：压到 0.35 秒，下一只马上出来。
            _respawnTimer = Mathf.Min(_respawnTimer, 0.35f);
        }

        /// <summary>把本局遗物重新聚合成 <see cref="PlayerData.relic"/> 并夹住当前生命。</summary>
        private void ApplyRelicMods()
        {
            if (Player == null) return;
            Player.relic = RelicMods.Build(EndlessRun != null ? EndlessRun.relics : null);
            // 最大生命可能被「血祭」压低：夹一次，别让血条超过上限。
            if (Player.currentHp > Player.MaxHp) Player.currentHp = Player.MaxHp;
        }

        /// <summary>
        /// 开始一局无尽。startFloor 会被夹在 1..本档最高层：
        /// 最高层是「已经证明能打到的地方」，越过它开局等于跳关。
        /// </summary>
        public void StartEndlessRun(int startFloor)
        {
            if (!EndlessUnlocked)
            {
                AddLog("🔒 无尽模式尚未解锁（首通 11-10 后开启）", GameConfig.LogWarn);
                return;
            }

            if (Player == null) return;
            if (Player.level < GameConfig.MaxLevel)
            {
                AddLog($"🔒 无尽模式需要等级 {GameConfig.MaxLevel}（当前 Lv.{Player.level}）", GameConfig.LogWarn);
                return;
            }
            if (ChoicePending)
            {
                AddLog("🔒 先处理完流派选择或结算面板再进无尽", GameConfig.LogWarn);
                return;
            }

            // 新一世还没重通 11-10 时不让从旧纪录层开局，否则转生的重置进度形同虚设。
            bool canUseBest = Player.IsStageFirstCleared(GameConfig.ChapterCount, GameConfig.StagesPerChapter);
            int maxStart = canUseBest ? Mathf.Max(1, Player.endlessBestFloor) : 1;
            int floor = Mathf.Clamp(startFloor, 1, maxStart);
            EndlessRun = new EndlessRunState
            {
                floor = floor,
                startFloor = floor,
                reachedFloor = floor,
                killCount = 0,
                runKills = 0,
                clearedThisRun = 0,
                endReason = EndlessEndReason.None,
                startedUtc = System.DateTime.UtcNow.Ticks,
                historyRank = 0,
            };

            Mode = GameMode.Endless;
            // 进入无尽等于离开战役：正在进行的无阵亡挑战作废。
            ClearNoDeathAttempt();
            _pendingTier = MonsterTier.Normal;
            _finalBossAttempt = 0;
            _justClearedStage = false;
            Monster = null;
            _loadedFromSave = false;

            // 深层契约：把面板上的勾选夹成合法值、拷进本局、聚合出修正。
            // 契约分只换遗物档位/槽位，不改玩家面板，所以这里只算一次乘区。
            int[] tiers = ContractCatalog.Sanitize(_endlessContractTiers);
            System.Array.Copy(tiers, _endlessContractTiers, tiers.Length);
            ContractMods contractMods = ContractMods.Build(tiers);
            EndlessRun.contractTiers = tiers;
            EndlessRun.contract = contractMods;
            EndlessRun.contractCount = ContractCatalog.CountSelected(tiers);
            EndlessRun.relicSlots = RelicCatalog.SlotsFor(Player.contractScore);
            EndlessRun.killTarget = ContractKillTarget(floor);
            Player.contract = contractMods;

            // 深渊遗物：判定要不要在本局启用（首局不弹），并清掉上一局残留的加成。
            // 种子用系统时间而不是 UnityEngine.Random：后者是战斗随机流，动它会破坏逐帧可复现。
            bool relicsOn = RelicsEnabledThisRun;
            EndlessRun.relicsEnabled = relicsOn;
            EndlessRun.relicSeed = relicsOn ? (int)(System.DateTime.UtcNow.Ticks & 0x3FFFFFFF) : 0;
            EndlessRun.relics.Clear();
            EndlessRun.pendingRelics.Clear();
            EndlessRun.relicTakePending = -1;
            EndlessRun.relicPendingFloor = 0;
            Player.relic = null;

            Player.FillHp();
            Player.shield = 0;
            _battle.ResetSkillState();

            IsRunning = true;
            _runStarted = true;
            CurrentState = State.WaitingRespawn;
            _respawnTimer = GameConfig.MonsterRespawnDelay;
            _reviveTimer = 0f;
            _shopTimer = 0f;
            _potionCooldownTimer = 0f;

            EndlessPanelOpen = false;
            EndlessResultOpen = false;
            CloseAllPanels();

            AddLog($"◆ 进入无尽模式：从第 {floor} 层开始（本档最高 {Player.endlessBestFloor} 层）",
                GameConfig.LogUnlock);
            AddLog("  阵亡或撤退都会结束本局；每清一层立即记录最高层。", GameConfig.LogInfo);
            AddLog(relicsOn
                ? $"✦ 深渊遗物：每清 10 层三选一（最高第 {ContractCatalog.RelicMaxTier(Player.contractScore)} 档），" +
                  $"槽位 {EndlessRun.RelicSlots}；满槽后需先舍弃一件"
                : "✦ 深渊遗物将在下一局生效（首局先跑一遍无尽）。", GameConfig.LogUnlock);
            if (contractMods != null)
            {
                AddLog($"◈ 深层契约 {contractMods.count} 条：{ContractCatalog.Describe(tiers)}" +
                       $"（本局契约分倍率 ×{1f + ContractCatalog.CoefSum(tiers):0.00}，每层击杀 {EndlessRun.killTarget} 只）",
                    GameConfig.LogUnlock);
            }
        }

        /// <summary>结果面板的「再次挑战」：从检查点起步，重新开一局。</summary>
        public void RetryEndlessRun()
        {
            StartEndlessRun(EndlessSuggestedStartFloor);
        }

        /// <summary>主动撤退：不加阵亡数，但这一局同样结束。</summary>
        public void RetreatEndlessRun()
        {
            if (!EndlessActive) return;
            if (Player != null && Player.ContractNoRetreat)
            {
                AddLog("🔒 决绝契约：本局不能主动撤退（只能打到阵亡）", GameConfig.LogWarn);
                return;
            }
            AddLog($"◀ 已撤退（第 {EndlessRun.floor} 层）", GameConfig.LogWarn);
            EndEndlessRun(EndlessEndReason.Retreat);
        }

        /// <summary>结束无尽，回到战役的常态（不改 chapterIndex / stageIndex）。</summary>
        public void ReturnToCampaign()
        {
            // 不能只看 Mode：EndEndlessRun 已经把 Mode 改回 Campaign，
            // 结果面板上的「返回战役」正是从那个状态点进来的。
            if (Mode == GameMode.Endless || EndlessResultOpen || CurrentState == State.Ended)
            {
                Mode = GameMode.Campaign;
                EndlessRun = null;
                EndlessPanelOpen = false;
                EndlessResultOpen = false;

                Monster = null;
                _pendingTier = MonsterTier.Normal;
                _finalBossAttempt = 0;
                _justClearedStage = false;
                Player.relic = null;
                Player.contract = null;
                Player.FillHp();
                Player.shield = 0;
                _battle.ResetSkillState();

                IsRunning = true;
                _runStarted = true;
                CurrentState = State.WaitingRespawn;
                _respawnTimer = GameConfig.MonsterRespawnDelay;
                _reviveTimer = 0f;
                _shopTimer = 0f;

                CloseAllPanels();
                AddLog("⇄ 已结束无尽，返回战役进度。", GameConfig.LogInfo);
                SaveSystem.Save(Player);
            }
        }

        /// <summary>
        /// 每只怪死亡的结算。和战役版相比：
        /// 不加 stageKillCount、不走关卡通关；清层由 ResolveEndlessFloorClear 单独处理。
        /// </summary>
        private void HandleEndlessMonsterKilled()
        {
            TotalKills++;
            if (Monster.tier == MonsterTier.Elite) EliteKills++;
            else if (Monster.tier == MonsterTier.Boss) BossKills++;
            RecordKillStats(Monster.tier);

            EndlessRun.runKills++;
            EndlessRun.killCount++;

            // 经验与掉落都封顶：无尽层数会一直涨，不封顶的话第 300 层的装备
            // 会把主角的成长曲线整个甩到战役之外。
            int cap = Mathf.Max(1, GameConfig.EndlessDropLevelCap);
            int reward = GrantKillReward(Mathf.Min(Monster.level, cap));
            ResolveLevelUp(reward);
            ResolveDrops();

            if (EndlessRun.killCount >= EndlessRun.killTarget)
            {
                ResolveEndlessFloorClear();
            }
            else
            {
                // 还差一只且这只是关底：先定好档次，下一次 SpawnMonster 会开商店再生成。
                if (EndlessKillsLeft == 1)
                {
                    _pendingTier = EndlessCatalog.Get(EndlessRun.floor).clearTier;
                }
                EnterRespawnWait(GameConfig.MonsterRespawnDelay);
            }

            // 技能点自选：升级只提示，不弹三选一、不打断无尽节奏。
            // 层结算（endless_best 与金币）已经落地，再统一求值一次。
            EvaluateAchievements();
        }

        /// <summary>清层：记最高层、发金币、横幅，然后进下一层。</summary>
        private void ResolveEndlessFloorClear()
        {
            int cleared = Mathf.Clamp(EndlessRun.floor, 1, EndlessCatalog.MaxFloor);
            EndlessRun.clearedThisRun++;
            EndlessRun.reachedFloor = cleared;

            bool newBest = cleared > Player.endlessBestFloor;
            int floorGold = EndlessCatalog.FloorGoldOf(cleared);
            int firstGold = newBest ? EndlessCatalog.FirstClearGoldOf(cleared) : 0;
            int totalGold = floorGold + firstGold;

            if (newBest)
            {
                Player.endlessBestFloor = cleared;
                EndlessRecord.ReportGlobal(cleared);
            }

            totalGold = Player.AddGold(totalGold);
            TotalGoldEarned += totalGold;

            AddLog($"✦ 无尽第 {cleared} 层通过！+{totalGold:N0} 金币（本档最高 {Player.endlessBestFloor} 层）",
                newBest ? GameConfig.LogGold : GameConfig.LogSuccess);
            GameAudio.PlayCoin();
            Debug.Log($"[AutoBattle] 无尽第 {cleared} 层通过，金币 +{totalGold}，最高 {Player.endlessBestFloor}");

            ShowBanner(
                newBest ? $"★ 新纪录 · 无尽第 {cleared} 层" : $"✦ 第 {cleared} 层通过",
                $"本档最高 {Player.endlessBestFloor} 层 · +{totalGold:N0} 金币");

            // 每破一次纪录就落盘：玩家关游戏/崩溃时不该丢掉刚刷新的一层。
            if (newBest) SaveSystem.Save(Player);

            EndlessRun.floor = Mathf.Min(cleared + 1, EndlessCatalog.MaxFloor);
            EndlessRun.killCount = 0;
            EndlessRun.killTarget = ContractKillTarget(EndlessRun.floor);
            _pendingTier = MonsterTier.Normal;
            _justClearedStage = false;
            Monster = null;
            CurrentState = State.WaitingRespawn;
            _respawnTimer = GameConfig.EndlessFloorClearDelay;

            // 每清 10 层弹一次三选一。面板开着时整局冻结（RelicChoiceOpen 在冻结名单里），
            // 所以这里的 _respawnTimer 不会被白白消耗。
            if (EndlessRun.relicsEnabled && RelicCatalog.ShouldOffer(cleared))
                OfferRelicChoice(cleared);
        }

        /// <summary>无尽版的刷怪：等级/关底档次/类型都从层号推导。</summary>
        private void SpawnEndlessMonsterNow()
        {
            EndlessFloorDef def = EndlessCatalog.Get(EndlessRun.floor);

            MonsterTier tier = _pendingTier;
            _pendingTier = MonsterTier.Normal;

            // Boss 层的最后一只固定类型（按 BOSS 周期轮换），普通层仍按该层怪物等级掷骰。
            MonsterType type = tier == MonsterTier.Boss && def.isBossFloor
                ? def.bossType
                : MonsterData.RollType(def.monsterLevel);

            // Boss 不带词缀；精英带，和战役一致。
            MonsterAffix affix = tier == MonsterTier.Boss ? MonsterAffix.None : MonsterData.RollAffix();

            if (tier == MonsterTier.Boss)
            {
                // 决战前回满并清盾：无尽 BOSS 是层与层之间的硬门槛，
                // 不该让玩家带着上一层的残血和护盾进入（护盾按 maxHp 比例，跨层会失真）。
                Player.FillHp();
                Player.shield = 0;
                AddLog("♥ 决战前已恢复全部生命", GameConfig.LogHeal);
            }
            else if (GameConfig.HealBeforeFight)
            {
                Player.FillHp();
                AddLog("♥ 开战前已恢复全部生命", GameConfig.LogHeal);
            }

            // 群袭：整层怪物按更深的层数生成（小怪、精英、Boss 都吃）。
            int spawnFloor = def.floor + ContractClearFloorBonus();
            Monster = MonsterData.CreateForEndlessFloor(spawnFloor, type, tier, affix);
            Monster.ApplyContract(EndlessRun.contract);
            CurrentState = State.Fighting;
            _battle.BeginBattle(Player);

            string affixNote = Monster.affix != MonsterAffix.None
                ? $"  <color=#C6CEE0>词缀 {GameConfig.AffixNames[(int)Monster.affix]}</color>"
                : "";
            AddLog($"⚔ 第 {def.floor} 层 · {Monster.Title} 出现   HP {Monster.maxHp:N0}{affixNote}",
                tier == MonsterTier.Boss ? GameConfig.LogDanger : GameConfig.LogInfo);
            if (tier == MonsterTier.Boss) GameAudio.PlayBossSpawn();
        }

        /// <summary>
        /// 结束一局无尽。保留 EndlessRun 给结果面板读（层数/清层数），
        /// 但把战斗停到 State.Ended；只有阵亡/撤退才弹结果面板。
        /// </summary>
        private void EndEndlessRun(EndlessEndReason reason)
        {
            if (Mode != GameMode.Endless) return;

            EndlessRun.endReason = reason;
            EndlessRun.reachedFloor = EndlessRun.floor;
            EndlessRun.finishSeconds = EndlessRun.Seconds;

            // 契约分：先算本局再记账（历史榜条目要读 EndlessRun.contractScore）。
            // 换档/重置是把这一局掐掉，与「退出游戏放弃本局」同义，不给分。
            EndlessRun.contractScore = ContractCatalog.ScoreOf(
                EndlessRun.clearedThisRun, EndlessRun.contractTiers);
            if ((reason == EndlessEndReason.Death || reason == EndlessEndReason.Retreat)
                && Player != null && EndlessRun.contractScore > 0)
            {
                int before = Player.contractScore;
                Player.contractScore = ContractCatalog.AddToTotal(before, EndlessRun.contractScore);
                int gained = Player.contractScore - before;
                int milestonesBefore = ContractCatalog.MilestoneCount(before);
                int milestonesAfter = ContractCatalog.MilestoneCount(Player.contractScore);

                AddLog($"◈ 深层契约 {EndlessRun.contractCount} 条 → 契约分 +{gained:N0}" +
                       $"（本局 {EndlessRun.contractScore:N0}，累计 {Player.contractScore:N0}）",
                    GameConfig.LogUnlock);
                for (int i = milestonesBefore; i < milestonesAfter; i++)
                {
                    ContractCatalog.Milestone ms = ContractCatalog.Milestones[i];
                    AddLog($"🏅 契约里程碑「{ms.name}」解锁：{ms.desc}", GameConfig.LogUnlock);
                }
                if (gained > 0) SaveSystem.Save(Player);
            }

            // 只有「真的打完一局」（阵亡/撤退）才进历史榜。换档/重置是把这一局掐掉，
            // 与「退出游戏放弃本局」同一语义，不占榜单，也不落盘。
            if (reason == EndlessEndReason.Death || reason == EndlessEndReason.Retreat)
                RecordEndlessHistory(reason);

            // 遗物只在本局有效：无论阵亡、撤退还是掐局，都要把加成一起丢掉，
            // 否则回战役会带着一套深渊加成打普通怪。
            // 结算卡与历史榜要读「结束时拿到了几件」，所以清空之前先拍一张快照。
            EndlessRun.resultRelics.Clear();
            EndlessRun.resultRelics.AddRange(EndlessRun.relics);
            EndlessRun.relics.Clear();
            EndlessRun.pendingRelics.Clear();
            EndlessRun.relicTakePending = -1;
            EndlessRun.relicPendingFloor = 0;
            if (Player != null)
            {
                Player.relic = null;
                Player.contract = null;
            }

            Mode = GameMode.Campaign;
            IsRunning = false;
            CurrentState = State.Ended;
            Monster = null;
            _pendingTier = MonsterTier.Normal;
            _justClearedStage = false;
            EndlessPanelOpen = false;
            EndlessResultOpen = reason == EndlessEndReason.Death || reason == EndlessEndReason.Retreat;
        }

        /// <summary>
        /// 把这一局记进本档 Top-5 历史榜，名次写回 <see cref="EndlessRunState.historyRank"/>（0 = 没上榜）。
        /// 上榜才落盘：没上榜的局不改变存档内容，没必要多写一次 PlayerPrefs。
        /// </summary>
        private int RecordEndlessHistory(EndlessEndReason reason)
        {
            if (Player == null || EndlessRun == null) return 0;

            var entry = new EndlessScoreEntry
            {
                floor = Mathf.Clamp(EndlessRun.reachedFloor, 0, EndlessCatalog.MaxFloor),
                cleared = Mathf.Max(0, EndlessRun.clearedThisRun),
                kills = Mathf.Max(0, EndlessRun.runKills),
                paragon = Mathf.Clamp(Player.paragonLevel, 0, GameConfig.MaxParagonLevel),
                rebirth = Mathf.Max(0, Player.rebirthCount),
                seconds = EndlessRun.finishSeconds,
                endedUtc = System.DateTime.UtcNow.Ticks,
                reason = (int)reason,
                hasRelic = EndlessRun.relics.Count > 0,
                contractScore = Mathf.Max(0, EndlessRun.contractScore),
                contractCount = Mathf.Clamp(EndlessRun.contractCount, 0, ContractCatalog.MaxSelected),
            };

            int rank = EndlessHistory.Insert(Player.endlessTop, entry);
            EndlessRun.historyRank = rank;
            LastEndlessRecordUtc = entry.endedUtc;
            if (rank > 0)
            {
                SaveSystem.Save(Player);
                AddLog($"🏆 第 {entry.floor} 层 · 本档历史榜第 {rank} 名", GameConfig.LogUnlock);
            }
            return rank;
        }

        /// <summary>
        /// 清空本档 Top-5 历史榜。最高层 <see cref="PlayerData.endlessBestFloor"/> 是另一份数据：
        /// 清榜单不动它，否则清一次榜就会把检查点起始层与转生印记一起抹掉。
        /// </summary>
        public bool ClearEndlessHistory()
        {
            if (Player == null || Player.endlessTop == null || Player.endlessTop.Count == 0) return false;

            int removed = Player.endlessTop.Count;
            Player.endlessTop.Clear();
            LastEndlessRecordUtc = 0;
            SaveSystem.Save(Player);
            AddLog($"🗑 已清空本档无尽历史榜（{removed} 局）；最高层记录与全局纪录不受影响", GameConfig.LogWarn);
            return true;
        }
    }
}

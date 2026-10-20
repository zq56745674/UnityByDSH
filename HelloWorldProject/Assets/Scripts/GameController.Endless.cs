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
                return EndlessCatalog.StartFloorFor(Player.endlessBestFloor);
            }
        }

        /// <summary>能否真正开始一局无尽：已解锁、等级满级、且没有待处理的技能三选一/结算。</summary>
        public bool CanEnterEndless =>
            EndlessUnlocked && Player != null
            && Player.level >= GameConfig.MaxLevel
            && !ChoicePending;

        /// <summary>打开 / 关闭无尽面板。结果面板开着时禁止，避免两层浮层叠在一起。</summary>
        public void ToggleEndlessPanel()
        {
            if (ChoicePending || EndlessResultOpen) return;
            bool open = !EndlessPanelOpen;
            CloseAllPanels();
            EndlessPanelOpen = open;
        }

        public void CloseEndlessPanel() => EndlessPanelOpen = false;

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
                AddLog("🔒 先处理完技能三选一或结算面板再进无尽", GameConfig.LogWarn);
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
            };

            Mode = GameMode.Endless;
            // 进入无尽等于离开战役：正在进行的无阵亡挑战作废。
            ClearNoDeathAttempt();
            _pendingTier = MonsterTier.Normal;
            _finalBossAttempt = 0;
            _justClearedStage = false;
            Monster = null;
            _loadedFromSave = false;

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

            if (EndlessRun.killCount >= EndlessCatalog.KillTargetOf(EndlessRun.floor))
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

            if (Player.pendingSkillPicks > 0) OpenSkillChoice();

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
            Debug.Log($"[AutoBattle] 无尽第 {cleared} 层通过，金币 +{totalGold}，最高 {Player.endlessBestFloor}");

            ShowBanner(
                newBest ? $"★ 新纪录 · 无尽第 {cleared} 层" : $"✦ 第 {cleared} 层通过",
                $"本档最高 {Player.endlessBestFloor} 层 · +{totalGold:N0} 金币");

            // 每破一次纪录就落盘：玩家关游戏/崩溃时不该丢掉刚刷新的一层。
            if (newBest) SaveSystem.Save(Player);

            EndlessRun.floor = Mathf.Min(cleared + 1, EndlessCatalog.MaxFloor);
            EndlessRun.killCount = 0;
            _pendingTier = MonsterTier.Normal;
            _justClearedStage = false;
            Monster = null;
            CurrentState = State.WaitingRespawn;
            _respawnTimer = GameConfig.EndlessFloorClearDelay;
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

            Monster = MonsterData.CreateForEndlessFloor(def.floor, type, tier, affix);
            CurrentState = State.Fighting;
            _battle.BeginBattle(Player);

            string affixNote = Monster.affix != MonsterAffix.None
                ? $"  <color=#C6CEE0>词缀 {GameConfig.AffixNames[(int)Monster.affix]}</color>"
                : "";
            AddLog($"⚔ 第 {def.floor} 层 · {Monster.Title} 出现   HP {Monster.maxHp:N0}{affixNote}",
                tier == MonsterTier.Boss ? GameConfig.LogDanger : GameConfig.LogInfo);
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

            Mode = GameMode.Campaign;
            IsRunning = false;
            CurrentState = State.Ended;
            Monster = null;
            _pendingTier = MonsterTier.Normal;
            _justClearedStage = false;
            EndlessPanelOpen = false;
            EndlessResultOpen = reason == EndlessEndReason.Death || reason == EndlessEndReason.Retreat;
        }
    }
}

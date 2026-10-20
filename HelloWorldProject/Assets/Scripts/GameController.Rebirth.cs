using System;
using UnityEngine;

namespace AutoBattle
{
    /// <summary>
    /// 转生（轮回）逻辑：解锁判定、印记折算、购买永久升级、重置本世。
    /// 纯数值与规则在 RebirthCatalog，本文件只负责接线与写盘。
    /// </summary>
    public partial class GameController
    {
        public bool RebirthPanelOpen { get; private set; }

        /// <summary>
        /// 本档是否解锁过轮回：首通 11-10，或已经转过一次。
        /// 转生后 clearedStages 清空，但 rebirthCount > 0 仍保持入口可用。
        /// </summary>
        public bool RebirthUnlocked =>
            Player != null && (Player.rebirthCount > 0
                || Player.IsStageFirstCleared(GameConfig.ChapterCount, GameConfig.StagesPerChapter));

        /// <summary>当前这一世如果立刻转生，可以拿到多少印记。</summary>
        public int PendingRebirthMarks => Player == null
            ? 0
            : RebirthCatalog.MarksFor(Player.paragonLevel, Player.endlessBestFloor, Player.gold);

        public int PendingRebirthParagonMarks => Player == null ? 0 : RebirthCatalog.ParagonMarks(Player.paragonLevel);
        public int PendingRebirthFloorMarks => Player == null ? 0 : RebirthCatalog.FloorMarks(Player.endlessBestFloor);
        public int PendingRebirthGoldMarks => Player == null ? 0 : RebirthCatalog.GoldMarks(Player.gold);

        /// <summary>
        /// 是否可以立刻转生。返回 false 时 reason 给出第一条未满足的原因（UI 直接显示）。
        /// 顺序按「先解释为什么不能，再说进度」排列。
        /// </summary>
        public bool CanRebirth(out string reason)
        {
            reason = string.Empty;
            if (Player == null)
            {
                reason = "还没有角色数据";
                return false;
            }

            if (EndlessActive || EndlessResultOpen)
            {
                reason = "无尽模式进行中，先撤退或结束本局";
                return false;
            }

            if (ChoicePending)
            {
                reason = "先把技能三选一选完";
                return false;
            }

            if (!RebirthUnlocked)
            {
                reason = "通关 11-10 后解锁轮回";
                return false;
            }

            if (Player.level < GameConfig.MaxLevel)
            {
                reason = $"需要等级 {GameConfig.MaxLevel}（当前 Lv.{Player.level}）";
                return false;
            }

            if (Player.paragonLevel < GameConfig.RebirthUnlockParagon)
            {
                reason = $"需要巅峰 {GameConfig.RebirthUnlockParagon}（当前 P{Player.paragonLevel}）";
                return false;
            }

            if (!Player.IsStageFirstCleared(GameConfig.ChapterCount, GameConfig.StagesPerChapter))
            {
                reason = "本世需要先通关 11-10";
                return false;
            }

            return true;
        }

        public void ToggleRebirthPanel()
        {
            if (ChoicePending || EndlessResultOpen) return;
            if (!RebirthUnlocked)
            {
                AddLog("🔒 轮回尚未解锁（首通 11-10 后开启）", GameConfig.LogWarn);
                return;
            }

            bool open = !RebirthPanelOpen;
            CloseAllPanels();
            RebirthPanelOpen = open;
        }

        public void CloseRebirthPanel() => RebirthPanelOpen = false;

        /// <summary>买一级永久升级：立即扣印记并写盘（不弹二次确认）。</summary>
        public bool BuyRebirthRank(RebirthTrack track)
        {
            if (Player == null) return false;

            int rank = Player.GetRebirthRank(track);
            if (rank >= RebirthCatalog.MaxRank)
            {
                AddLog($"♾ {RebirthCatalog.NameOf(track)} 已满级", GameConfig.LogInfo);
                return false;
            }

            int cost = RebirthCatalog.CostOf(track, rank);
            if (Player.rebirthMarks < cost)
            {
                AddLog($"♾ 印记不足：{RebirthCatalog.NameOf(track)} 需要 {cost}，当前 {Player.rebirthMarks}",
                    GameConfig.LogWarn);
                return false;
            }

            Player.rebirthMarks -= cost;
            Player.SetRebirthRank(track, rank + 1);
            SaveSystem.Save(Player);
            AddLog($"♾ {RebirthCatalog.NameOf(track)} 升到 {rank + 1} 级（-{cost} 印记，余 {Player.rebirthMarks}）",
                GameConfig.LogUnlock);
            return true;
        }

        /// <summary>
        /// 执行转生。顺序很重要：先卖待选（计入金币），再算印记，最后才重置。
        /// 重置后本世的金币/等级/装备/技能/关卡全部归零，印记与历史纪录保留。
        /// </summary>
        public bool PerformRebirth()
        {
            if (!CanRebirth(out string reason))
            {
                AddLog($"♾ 暂时不能转生：{reason}", GameConfig.LogWarn);
                return false;
            }

            // 1) 待选不能凭空消失：按普通卖店价折现，且不吃财运（否则玩家会故意屯待选刷印记）。
            int pendingGold = 0;
            for (int i = 0; i < _pendingDrops.Length; i++)
            {
                Equipment pending = _pendingDrops[i];
                if (pending == null) continue;
                int gold = Player.AddGoldFixed(GameConfig.SellPrice(pending.level, pending.quality));
                pendingGold += gold;
                TotalSold++;
                TotalGoldEarned += gold;
                RecordSaleStats();
                _pendingDrops[i] = null;
            }

            // 2) 印记按「卖完待选之后」的金币算。
            int marks = PendingRebirthMarks;
            int paragonMarks = RebirthCatalog.ParagonMarks(Player.paragonLevel);
            int floorMarks = RebirthCatalog.FloorMarks(Player.endlessBestFloor);
            int goldMarks = RebirthCatalog.GoldMarks(Player.gold);
            int oldParagon = Player.paragonLevel;
            int oldBest = Player.rebirthBestParagon;

            // 3) 先记下本世成绩，再重置。用 long 夹一次，防手改档（int.MaxValue）自增溢出。
            Player.rebirthCount = (int)Math.Min(int.MaxValue, (long)Player.rebirthCount + 1);
            Player.rebirthMarks = (int)Math.Min(int.MaxValue, (long)Player.rebirthMarks + marks);
            Player.rebirthBestParagon = Mathf.Max(oldBest, oldParagon);

            int startLevel = Player.RebirthStartLevel;
            int startGold = Player.RebirthStartGold;
            int startPotions = Player.RebirthStartPotions;

            Player.level = startLevel;
            Player.exp = 0;
            Player.paragonLevel = 0;
            Player.paragonExp = 0;
            Player.gold = startGold;
            Player.potionCount = startPotions;

            Player.chapterIndex = 1;
            Player.stageIndex = 1;
            Player.stageKillCount = 0;
            Player.maxChapterUnlocked = 1;
            if (Player.clearedStages == null) Player.clearedStages = new System.Collections.Generic.List<int>();
            Player.clearedStages.Clear();

            Player.weapon = null;
            Player.armor = null;
            Player.helmet = null;
            Player.boots = null;
            Player.accessory = null;
            Player.slotStarterMask = 0;

            Player.ownedSkillMask = 0;
            Player.pendingSkillPicks = 0;
            Player.skillActive0 = -1;
            Player.skillActive1 = -1;
            Player.skillPassive0 = -1;
            Player.skillPassive1 = -1;
            Player.skillTrigger0 = -1;
            Player.skillTrigger1 = -1;

            // 4) 清本局临时状态、统计、浮层与日志；ResetSessionState 会 CloseAllPanels。
            ResetSessionState();
            Player.FillHp();
            Player.shield = 0;
            _loadedFromSave = false;
            _autoSaveTimer = 0f;

            // 5) 落盘后再写日志/横幅：即使这里崩溃，至少新一世已经存好。
            SaveSystem.Save(Player);

            if (pendingGold > 0)
            {
                AddLog($"♾ 转生时自动卖出待选：+{pendingGold:N0} 金币（不计财运）", GameConfig.LogGold);
            }
            AddLog($"♾ 转生完成：第 {Player.rebirthCount + 1} 世，获得 {marks} 印记"
                + $"（巅峰 {paragonMarks} / 无尽 {floorMarks} / 金币 {goldMarks} / 基础 {RebirthCatalog.BaseMarks}）",
                GameConfig.LogUnlock);
            AddLog($"  起始 Lv.{Player.level} · 金币 {Player.gold:N0} · 药剂 {Player.potionCount} · 印记余额 {Player.rebirthMarks}",
                GameConfig.LogInfo);

            ShowBanner($"✦ 第 {Player.rebirthCount + 1} 世 · 轮回 +{marks} 印记",
                $"Lv.{Player.level} 开始 · 印记余额 {Player.rebirthMarks} · 无尽纪录会保留");
            return true;
        }
    }
}

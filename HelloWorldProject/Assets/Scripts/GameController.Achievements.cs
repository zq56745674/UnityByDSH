using UnityEngine;

namespace AutoBattle
{
    /// <summary>
    /// 成就相关的统计记账与「无阵亡通关」会话挑战。
    /// 战斗/装备流程只调用这里的几个 Record* 与 EvaluateAchievements，
    /// 具体指标与解锁判定都在 <see cref="AchievementSystem"/> 里。
    /// </summary>
    public partial class GameController
    {
        /// <summary>无阵亡挑战只覆盖第 9/10/11 章（与成就目录的说明一致）。</summary>
        private const int NoDeathFirstChapter = 9;

        private bool _noDeathActive;
        private int _noDeathChapter;
        private int _noDeathNextStage;

        /// <summary>成就面板是否打开；和其他浮层一样由 CloseAllPanels 统一收起。</summary>
        public bool AchievementPanelOpen { get; private set; }

        /// <summary>左栏成就入口。</summary>
        public void ToggleAchievementPanel()
        {
            if (ChoicePending && !AchievementPanelOpen) return;
            bool open = !AchievementPanelOpen;
            CloseAllPanels();
            AchievementPanelOpen = open;
        }

        public void CloseAchievementPanel()
        {
            AchievementPanelOpen = false;
        }

        /// <summary>
        /// 佩戴/卸下称号（空字符串 = 卸下）。只影响左栏显示，不改任何战斗数值；
        /// 未解锁的称号拒绝佩戴，防止存档被手改成不存在的 id。
        /// </summary>
        public void SetEquippedTitle(string id)
        {
            if (Player == null) return;
            string next = string.IsNullOrEmpty(id) ? string.Empty : id;
            if (next.Length > 0 && !AchievementCatalog.IsUnlockedTitle(Player, next))
            {
                AddLog("<color=#FF8080>✖ 这个称号还没有解锁。</color>", GameConfig.LogWarn);
                return;
            }

            if (string.Equals(Player.equippedTitle ?? string.Empty, next)) return;
            Player.equippedTitle = next;

            if (next.Length == 0)
                AddLog("✦ 已卸下称号。", GameConfig.LogNeutral);
            else
                AddLog("✦ 已佩戴称号「" + AchievementCatalog.TitleNameOf(next) + "」。", GameConfig.LogGold);
            TrySave();
        }

        /// <summary>
        /// 用成就点兑换一级无尽起始层（100 点/级，最多 +3 层）。
        /// 成就点只换非战斗便利，不提供任何战斗属性。
        /// </summary>
        public bool BuyEndlessStartRank()
        {
            if (Player == null) return false;

            string message;
            if (!AchievementSystem.TryBuyEndlessStartRank(Player, out message))
            {
                AddLog("<color=#FF8080>✖ " + message + "</color>", GameConfig.LogWarn);
                return false;
            }

            AddLog("<color=#FFD966>✦ " + message + "</color>", GameConfig.LogGold);
            TrySave();
            return true;
        }

        // ==================== 无阵亡挑战（不存档） ====================

        private void ClearNoDeathAttempt()
        {
            _noDeathActive = false;
            _noDeathChapter = 0;
            _noDeathNextStage = 0;
        }

        /// <summary>
        /// 选关入口：任何「有效」的选关请求都先作废旧挑战（包括重选当前关），
        /// 再判断目标是不是合法起点。未解锁/无尽中/结果面板挡住的请求不算有效，不进来。
        /// </summary>
        private void ResetNoDeathAttemptForSelect(int chapter, int stage)
        {
            ClearNoDeathAttempt();
            TryOpenNoDeathAttempt(chapter, stage);
        }

        /// <summary>
        /// 从第 9/10/11 章第 1 关的完整起点开新挑战。
        /// 已经有一个进行中的挑战时不覆盖——自然跨章进入下一章第 1 关也要保住 9-1 开的那次。
        /// </summary>
        private void TryOpenNoDeathAttempt(int chapter, int stage)
        {
            if (_noDeathActive) return;
            if (stage != 1) return;
            if (chapter < NoDeathFirstChapter || chapter > GameConfig.ChapterCount) return;

            _noDeathActive = true;
            _noDeathChapter = chapter;
            _noDeathNextStage = 1;
        }

        /// <summary>
        /// 确认本关已清后、结算之前调用：必须严格按 1..10 的顺序推进。
        /// 乱序/跨章立刻作废；清掉第 10 关才写 mask 并结束挑战。
        /// 调用点放在终局重复刷关的提前返回之前，所以已首通 11-10 再重打一整章也能记。
        /// </summary>
        private void AdvanceNoDeathAttempt()
        {
            if (!_noDeathActive) return;

            if (Player.chapterIndex != _noDeathChapter || Player.stageIndex != _noDeathNextStage)
            {
                ClearNoDeathAttempt();
                return;
            }

            if (_noDeathNextStage < StageCatalog.StagesPerChapter)
            {
                _noDeathNextStage++;
                return;
            }

            int bit = 1 << (_noDeathChapter - 1);
            Player.stats.noDeathChapterMask |= bit;
            ClearNoDeathAttempt();
        }

        // ==================== 统计记账 ====================

        /// <summary>战役与无尽共用：每只被击杀的怪只记一次，精英/Boss 另记档次。</summary>
        private void RecordKillStats(MonsterTier tier)
        {
            if (Player == null || Player.stats == null) return;

            PlayerStats s = Player.stats;
            s.kills = PlayerData.AddStat(s.kills, 1);
            if (tier == MonsterTier.Elite) s.eliteKills = PlayerData.AddStat(s.eliteKills, 1);
            else if (tier == MonsterTier.Boss) s.bossKills = PlayerData.AddStat(s.bossKills, 1);
        }

        /// <summary>每次实际产出的掉落都算（含自动穿戴、待选、直接卖店的）。</summary>
        private void RecordDropStats(Equipment dropped)
        {
            if (Player == null || Player.stats == null) return;

            PlayerStats s = Player.stats;
            s.drops = PlayerData.AddStat(s.drops, 1);
            if (dropped != null && dropped.quality == EquipmentQuality.Legendary)
                s.legendaryDrops = PlayerData.AddStat(s.legendaryDrops, 1);
        }

        private void RecordSaleStats()
        {
            if (Player == null || Player.stats == null) return;
            Player.stats.sold = PlayerData.AddStat(Player.stats.sold, 1);
        }

        private void RecordEquipStats()
        {
            if (Player == null || Player.stats == null) return;
            Player.stats.equipped = PlayerData.AddStat(Player.stats.equipped, 1);
        }

        private void RecordEnhanceStats(Equipment e)
        {
            if (Player == null || Player.stats == null || e == null) return;

            PlayerStats s = Player.stats;
            s.enhances = PlayerData.AddStat(s.enhances, 1);
            s.bestEnhanceLevel = Mathf.Max(s.bestEnhanceLevel, e.enhanceLevel);
        }

        private void RecordReforgeStats(Equipment e, int lockedCount)
        {
            if (Player == null || Player.stats == null || e == null) return;

            PlayerStats s = Player.stats;
            s.reforges = PlayerData.AddStat(s.reforges, 1);
            s.bestAffixQuality = Mathf.Max(s.bestAffixQuality, e.AffixQuality);
            if (lockedCount >= 3) s.threeLockReforges = PlayerData.AddStat(s.threeLockReforges, 1);
        }

        private void RecordDeathStats()
        {
            if (Player == null || Player.stats == null) return;
            Player.stats.deaths = PlayerData.AddStat(Player.stats.deaths, 1);
        }

        /// <summary>同一批动作的最后一步：结算成就、发金币并推 toast。</summary>
        private void EvaluateAchievements()
        {
            if (Player != null) AchievementSystem.Evaluate(Player, true);
        }
    }
}

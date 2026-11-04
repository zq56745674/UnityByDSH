using UnityEngine;

namespace AutoBattle
{
    /// <summary>
    /// 巅峰符文（2026-11-04，方案 C）：每 100 巅峰 1 点，花 1 点解锁某个技能的「形态选择权」，
    /// 解锁后两个互斥形态可在局外免费来回切换。符文由巅峰等级派生、不额外存档，但解锁与形态存在存档里
    /// （转生清巅峰、留解锁，见 PlayerData.ResetSkillsForRebirth）。
    /// </summary>
    public partial class GameController
    {
        /// <summary>符文面板是否打开。和别的浮层一样冻结整局。</summary>
        public bool RunePanelOpen { get; private set; }

        /// <summary>打开符文面板（同时关掉其它浮层）；还没选流派时改开流派面板。</summary>
        public void OpenRunePanel()
        {
            if (ChoicePending) return;
            if (Player == null) return;
            if (!Player.HasDoctrine)
            {
                OpenDoctrinePanel();
                return;
            }
            CloseAllPanels();
            RunePanelOpen = true;
            GameAudio.PlayUiOpen();
        }

        /// <summary>关闭符文面板。</summary>
        public void CloseRunePanel() => RunePanelOpen = false;

        /// <summary>花 1 点解锁某个技能的符文（默认甲形态）。</summary>
        public void UnlockRune(SkillId id)
        {
            if (Player == null) return;
            if (!Player.CanUnlockRune(id))
            {
                GameAudio.PlayUiClick();
                return;
            }

            string name = RuneCatalog.FormName(id, RuneForm.A);
            if (!Player.UnlockRune(id)) return;

            AddLog($"◈ 符文解锁：{SkillCatalog.Get(id).name} · {name}（剩余 {Player.RunePoints} 点）",
                GameConfig.LogParagon);
            GameAudio.PlayUiOpen();
            EvaluateAchievements();
            TrySave();
        }

        /// <summary>切换某个技能的两个形态。已解锁才生效，免费、不消耗点数。</summary>
        public void SwitchRuneForm(SkillId id)
        {
            if (Player == null) return;
            if (!Player.HasRuneUnlocked(id)) return;

            Player.ToggleRuneForm(id);
            RuneForm form = Player.RuneFormOf(id);
            AddLog($"◈ 符文切换：{SkillCatalog.Get(id).name} → {RuneCatalog.FormName(id, form)}",
                GameConfig.LogParagon);
            GameAudio.PlayUiOpen();
            TrySave();
        }

        /// <summary>清空全部符文解锁（退还点数用，主要用于测试与误点回收）。</summary>
        public void ResetRunes()
        {
            if (Player == null) return;
            if (Player.RuneUnlockCount == 0) return;

            int refunded = Player.RuneUnlockCount;
            Player.ResetRunes();
            AddLog($"◈ 符文已清空，退回 {refunded} 点", GameConfig.LogParagon);
            GameAudio.PlayUiOpen();
            TrySave();
        }
    }
}

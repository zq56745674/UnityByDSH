using UnityEngine;

namespace AutoBattle
{
    /// <summary>
    /// 技能 2.0 的流派流程（2026-10-26）：P10 二选一并本世锁定，旧 v14 档在读档后补选一次。
    /// 技能点与技能池的流派过滤在 PlayerData / GameUI 里，这里只负责「什么时候弹、选完做什么」。
    /// </summary>
    public partial class GameController
    {
        /// <summary>流派面板是否打开。它属于不可绕过的一层。</summary>
        public bool DoctrinePanelOpen { get; private set; }

        /// <summary>P10 起（或旧档迁移）第一次需要选流派时把面板顶上来。</summary>
        private void TryPromptDoctrine()
        {
            if (Player == null || Player.HasDoctrine)
            {
                DoctrinePanelOpen = false;
                return;
            }

            if (DoctrinePanelOpen) return;
            if (Player.pendingLegacyDoctrineGrant || Player.paragonLevel >= 10)
                OpenDoctrinePanel();
        }

        /// <summary>打开流派面板。打开期间整局冻结，且不能按 Esc 关掉。</summary>
        public void OpenDoctrinePanel()
        {
            if (Player == null || Player.HasDoctrine || DoctrinePanelOpen) return;

            // 先清掉别的浮层；CloseAllPanels 会顺手把 DoctrinePanelOpen 归零，所以之后再置 true。
            CloseAllPanels();
            DoctrinePanelOpen = true;
            GameAudio.PlayUiOpen();
        }

        /// <summary>
        /// 确认流派。doctrine：1 = 风暴，2 = 山崩。
        /// 选完只记流派并给出本世应得的技能点；技能由玩家在技能面板里自选，不再自动送。
        /// v14 旧档迁移同样按 P100 给 9 点，由玩家自己选满 9 个。
        /// </summary>
        public void ChooseDoctrine(int doctrine)
        {
            if (!DoctrinePanelOpen) return;
            if (doctrine != 1 && doctrine != 2) return;
            if (Player == null || Player.HasDoctrine) return;

            bool legacy = Player.pendingLegacyDoctrineGrant;
            if (!Player.BeginDoctrine(doctrine)) return;
            if (legacy) Player.pendingLegacyDoctrineGrant = false;

            DoctrinePanelOpen = false;

            string school = SkillCatalog.SchoolName(doctrine);
            int points = Player.SkillPoints;
            AddLog($"✧ 流派已定：{school}（本世锁定，转生后可重选）", SkillCatalog.SchoolColor(doctrine));
            if (legacy)
            {
                AddLog($"  → 旧档迁移完成：获得 {points} 技能点，请自行选满本流派 9 个技能",
                    GameConfig.LogParagon);
                ShowBanner($"流派已定：{school}", $"旧档迁移：获得 {points} 技能点，打开技能面板自选");
            }
            else
            {
                AddLog($"  → 获得 {points} 技能点（P20 起每 10 级 +1），在技能面板自选", GameConfig.LogParagon);
                ShowBanner($"流派已定：{school}", $"获得 {points} 技能点；P20 起每 10 级 +1");
            }

            EvaluateAchievements();
            TrySave();
            GameAudio.PlayUiOpen();

            // 选完流派立刻打开技能面板，让玩家花掉第一点；面板打开时战斗保持冻结。
            OpenSkillPanel();
            Debug.Log($"[AutoBattle] 选择流派 {school}（legacy={legacy}），技能点 {points}");
        }
    }
}

using UnityEngine;

namespace AutoBattle
{
    /// <summary>
    /// 屠魔录（方案 D）：面板开关与击杀记账。
    /// 记账入口只有一个——<see cref="GameController.GrantKillReward"/>，
    /// 战役与无尽都走同一个击杀结算，所以这里不需要各自补一遍。
    /// </summary>
    public partial class GameController
    {
        /// <summary>屠魔录面板是否打开；和其他浮层一样由 CloseAllPanels 统一收起、并有停战权。</summary>
        public bool BestiaryPanelOpen { get; private set; }

        /// <summary>击杀计数版本号：面板只在它变化时重建 44 张卡，避免每帧重排。</summary>
        public int BestiaryVersion { get; private set; }

        public void ToggleBestiaryPanel()
        {
            if (ChoicePending && !BestiaryPanelOpen) return;
            bool open = !BestiaryPanelOpen;
            CloseAllPanels();
            BestiaryPanelOpen = open;
        }

        public void CloseBestiaryPanel()
        {
            BestiaryPanelOpen = false;
        }

        /// <summary>
        /// 每杀一只记一次。只是统计：不写任何战斗属性，也不触发成就点。
        /// 返回跨过「通晓」门槛的条目（没有就什么都不打，免得日志被刷屏）。
        /// </summary>
        private void RecordBestiaryKill(int monsterLevel, MonsterType type, MonsterTier tier)
        {
            if (Player == null) return;

            int index = BestiaryCatalog.RecordKill(Player, monsterLevel, type, tier);
            BestiaryVersion++;

            if (index < 0) return;

            BestiaryEntry entry = BestiaryCatalog.Get(index);
            if (entry == null) return;

            AddLog("📖 屠魔录 · 通晓：" + entry.entryName, GameConfig.LogGold);

            if (BestiaryCatalog.IsFamilyComplete(Player, entry.family))
                AddLog("✦ " + entry.familyName + " 族印点亮（四条全部通晓）", GameConfig.LogGold);
        }
    }
}

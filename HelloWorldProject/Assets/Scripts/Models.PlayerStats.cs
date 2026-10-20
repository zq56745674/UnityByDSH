using System;

namespace AutoBattle
{
    /// <summary>
    /// 跨世保留的累计统计（存档 v10）。它记录「玩家做过什么」，不是本局临时统计；
    /// 转生只重置本世进度，不清这里。所有字段都只由实际成功的动作写入。
    /// </summary>
    [Serializable]
    public class PlayerStats
    {
        public long kills;
        public long drops;
        public long equipped;
        public long sold;
        public long goldEarned;
        public long deaths;
        public long eliteKills;
        public long bossKills;
        public long enhances;
        public long reforges;
        public long legendaryDrops;
        public long threeLockReforges;

        /// <summary>历史强化到的最高等级（0~15）。</summary>
        public int bestEnhanceLevel;

        /// <summary>历史洗练出的最高词条质量（0~1，未舍入）。</summary>
        public float bestAffixQuality;

        /// <summary>无阵亡通关过的章节位掩码，只使用 bit 8 / 9 / 10（第 9/10/11 章）。</summary>
        public int noDeathChapterMask;
    }
}

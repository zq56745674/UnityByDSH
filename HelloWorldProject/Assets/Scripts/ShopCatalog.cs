using System;
using UnityEngine;

namespace AutoBattle
{
    /// <summary>
    /// 营地行商（B1 / docs/shop-2-design.md）：20 级收藏家 + 6 项服务 + 15 件珍藏。
    /// 纯静态目录，不依赖场景；所有条目都不加战斗属性，只改外观与操作。
    ///
    /// 2026-10-26 实现口径：
    /// - 服务 1「批量强化」做成「全槽 +5」：单槽 +5/满级在强化面板里本来就免费，
    ///   商店不能重复卖已有功能，所以只卖「一次点五个槽」。
    /// - 服务 2「连续洗练」做成 +10 连，保留锁定词条、逐次扣费。
    /// - 服务 3「装备收藏锁」做成「全锁/全解」：锁本身免费，服务只卖一键批量。
    /// - 服务 5「装备预设 3 套」改为「装备图鉴」：当前模型只有五槽、没有背包，
    ///   替换下来的装备会立刻卖掉，预设无法忠实换装；这里改成只读的装备历史面板。
    /// - 服务 6「战斗统计」解锁战绩页的完整统计。
    /// </summary>
    public static class ShopCatalog
    {
        public const int MaxCollectorRank = 20;
        public const int ServiceCount = 6;
        public const int AutoSellServiceIndex = 3;
        public const int CosmeticCount = 15;
        public const int ConfirmThreshold = 1000000;
        public const float ConfirmWindow = 3f;

        public static readonly int[] ServicePrices = { 150000, 300000, 200000, 250000, 300000, 200000 };
        public static readonly string[] ServiceNames =
        {
            "全套强化",
            "连续洗练",
            "批量锁定",
            "自动出售规则",
            "装备图鉴",
            "战斗统计",
        };
        public static readonly string[] ServiceDescs =
        {
            "强化面板出现「全槽+5」：一次把五个槽位各强化 5 级，逐级扣费，钱不够就停。",
            "洗练面板出现「+10 连」：连洗 10 次，锁住的词条不动，逐次扣费。",
            "强化面板出现「全锁/全解」：一键锁定或解锁当前五件，防误换。",
            "待选掉落按品质自动卖出；可在服务页切换 关 / 普通 / 优秀 / 稀有。",
            "战绩页解锁装备历史：最高强化、最佳词条、传说掉落与卖出总数。",
            "战绩页解锁完整统计：击杀、阵亡、精英、Boss、掉落与金币累计。",
        };

        public enum CosmeticKind
        {
            Frame = 0,
            Backdrop = 1,
            Damage = 2,
            Accent = 3,
        }

        public static readonly int[] CosmeticPrices =
        {
            500000, 1000000, 2000000, 3000000, 5000000, 8000000, // 头像框
            1000000, 1000000, 1000000, 1000000,                   // 背景色调
            1500000, 1500000, 1500000,                            // 伤害数字
            2500000, 2500000,                                     // UI 强调色
        };

        public static readonly string[] CosmeticNames =
        {
            "头像框·青铜", "头像框·白银", "头像框·湛蓝", "头像框·紫晶", "头像框·翠玉", "头像框·赤金",
            "背景色调·暖阳", "背景色调·寒霜", "背景色调·暮色", "背景色调·极光",
            "伤害数字·银白", "伤害数字·青碧", "伤害数字·赤金",
            "UI 强调色·鎏金", "UI 强调色·霜蓝",
        };

        public static readonly string[] CosmeticDescs =
        {
            "立绘外圈换成青铜色。", "立绘外圈换成银白色。", "立绘外圈换成湛蓝色。",
            "立绘外圈换成紫晶色。", "立绘外圈换成翠玉色。", "立绘外圈换成赤金色。",
            "战斗背景整体偏暖。", "战斗背景整体偏冷。", "战斗背景偏暮紫。", "战斗背景偏极光青。",
            "普通命中飘字改为银白。", "普通命中飘字改为青碧。", "普通命中飘字改为赤金。",
            "标题与商店高亮改为鎏金。", "标题与商店高亮改为霜蓝。",
        };

        private static readonly Color[] CosmeticColors =
        {
            new Color(0.72f, 0.49f, 0.26f), new Color(0.80f, 0.84f, 0.90f), new Color(0.35f, 0.62f, 0.95f),
            new Color(0.62f, 0.42f, 0.92f), new Color(0.35f, 0.82f, 0.62f), new Color(0.95f, 0.76f, 0.30f),
            new Color(1.08f, 1.00f, 0.90f), new Color(0.88f, 0.97f, 1.08f), new Color(1.06f, 0.92f, 0.95f), new Color(0.86f, 1.05f, 1.04f),
            new Color(0.90f, 0.95f, 1.00f), new Color(0.45f, 0.92f, 0.95f), new Color(1.00f, 0.78f, 0.35f),
            new Color(0.95f, 0.78f, 0.35f), new Color(0.45f, 0.72f, 1.00f),
        };

        /// <summary>收藏家第 level 级（1..20）的单级价格；越界返回 0。</summary>
        public static int RankCost(int level)
        {
            if (level < 1 || level > MaxCollectorRank) return 0;
            double raw = 100000.0 * Math.Pow(1.5, level - 1);
            return (int)Math.Round(raw, MidpointRounding.AwayFromZero);
        }

        /// <summary>从 0 升到 rank 级的累计价格（逐级取整后相加）。</summary>
        public static int RankTotal(int rank)
        {
            rank = Mathf.Clamp(rank, 0, MaxCollectorRank);
            long sum = 0;
            for (int i = 1; i <= rank; i++) sum += RankCost(i);
            return sum > int.MaxValue ? int.MaxValue : (int)sum;
        }

        /// <summary>升下一级要花的钱；已满级返回 0。</summary>
        public static int NextRankCost(PlayerData p)
        {
            if (p == null) return 0;
            int next = Mathf.Clamp(p.collectorRank, 0, MaxCollectorRank) + 1;
            return RankCost(next);
        }

        public static bool IsMaxRank(PlayerData p) => p != null && p.collectorRank >= MaxCollectorRank;

        public static bool HasService(PlayerData p, int index)
        {
            if (p == null || index < 0 || index >= ServiceCount) return false;
            return (p.shopServiceMask & (1 << index)) != 0;
        }

        public static bool HasCosmetic(PlayerData p, int index)
        {
            if (p == null || index < 0 || index >= CosmeticCount) return false;
            return (p.shopCosmeticMask & (1 << index)) != 0;
        }

        public static CosmeticKind KindOf(int index)
        {
            if (index < 6) return CosmeticKind.Frame;
            if (index < 10) return CosmeticKind.Backdrop;
            if (index < 13) return CosmeticKind.Damage;
            return CosmeticKind.Accent;
        }

        public static Color CosmeticColor(int index)
        {
            return index >= 0 && index < CosmeticColors.Length ? CosmeticColors[index] : Color.white;
        }

        public static int EquippedCosmetic(PlayerData p, CosmeticKind kind)
        {
            if (p == null) return -1;
            switch (kind)
            {
                case CosmeticKind.Frame: return p.equippedFrame;
                case CosmeticKind.Backdrop: return p.equippedBackdrop;
                case CosmeticKind.Damage: return p.equippedDamage;
                default: return p.equippedAccent;
            }
        }

        public static void SetEquippedCosmetic(PlayerData p, CosmeticKind kind, int index)
        {
            if (p == null) return;
            switch (kind)
            {
                case CosmeticKind.Frame: p.equippedFrame = index; break;
                case CosmeticKind.Backdrop: p.equippedBackdrop = index; break;
                case CosmeticKind.Damage: p.equippedDamage = index; break;
                default: p.equippedAccent = index; break;
            }
        }

        /// <summary>收藏家奖励给出的兜底外观，返回 -1 表示没有。</summary>
        public static int CollectorFallbackFrame(PlayerData p)
        {
            if (p == null) return -1;
            if (p.collectorRank >= 15) return 5; // 赤金
            if (p.collectorRank >= 10) return 1; // 白银
            if (p.collectorRank >= 5) return 0;  // 青铜
            return -1;
        }

        public static int CollectorFallbackBackdrop(PlayerData p)
        {
            if (p == null) return -1;
            if (p.collectorRank >= 20) return 9; // 极光
            if (p.collectorRank >= 10) return 6; // 暖阳
            return -1;
        }

        public static int CollectorFallbackDamage(PlayerData p)
        {
            if (p == null) return -1;
            return p.collectorRank >= 15 ? 12 : -1; // 赤金
        }

        /// <summary>实际生效的外观：玩家自己装备的优先，其次收藏家奖励。</summary>
        public static int EffectiveFrame(PlayerData p)
        {
            int own = p != null ? p.equippedFrame : -1;
            return own >= 0 ? own : CollectorFallbackFrame(p);
        }

        public static int EffectiveBackdrop(PlayerData p)
        {
            int own = p != null ? p.equippedBackdrop : -1;
            return own >= 0 ? own : CollectorFallbackBackdrop(p);
        }

        public static int EffectiveDamage(PlayerData p)
        {
            int own = p != null ? p.equippedDamage : -1;
            return own >= 0 ? own : CollectorFallbackDamage(p);
        }

        public static int EffectiveAccent(PlayerData p) => p != null ? p.equippedAccent : -1;

        public static Color AccentColor(PlayerData p)
        {
            int id = EffectiveAccent(p);
            return id >= 0 ? CosmeticColor(id) : GameConfig.TextGold;
        }

        public static Color DamageColor(PlayerData p)
        {
            int id = EffectiveDamage(p);
            return id >= 0 ? CosmeticColor(id) : GameConfig.TextNormal;
        }

        /// <summary>背景色调倍率；未装备返回白色（乘算不变）。</summary>
        public static Color BackdropMultiplier(PlayerData p)
        {
            int id = EffectiveBackdrop(p);
            return id >= 0 ? CosmeticColor(id) : Color.white;
        }

        /// <summary>收藏家称号前缀/称号：Lv20 直接给「黄金旅人」，Lv1 起加「行商」前缀。</summary>
        public static string CollectorTitle(PlayerData p, string baseTitle)
        {
            if (p == null || p.collectorRank <= 0) return baseTitle;
            if (p.collectorRank >= MaxCollectorRank) return "黄金旅人";
            string prefix = "行商";
            return string.IsNullOrEmpty(baseTitle) ? prefix : prefix + " · " + baseTitle;
        }

        /// <summary>
        /// 收藏家等级奖励换算成珍藏位掩码。达到等级即免费授予，购买时直接 OR 进去，
        /// 这样「奖励」在珍藏页里就是已拥有，不需要再区分「兜底生效」和「真的买了」。
        /// </summary>
        public static int CollectorRewardMask(int rank)
        {
            int mask = 0;
            if (rank >= 5) mask |= 1 << 0;   // 头像框·青铜
            if (rank >= 10) mask |= 1 << 1;  // 头像框·白银
            if (rank >= 10) mask |= 1 << 6;  // 背景色调·暖阳
            if (rank >= 15) mask |= 1 << 5;  // 头像框·赤金
            if (rank >= 15) mask |= 1 << 12; // 伤害数字·赤金
            if (rank >= 20) mask |= 1 << 9;  // 背景色调·极光
            return mask;
        }

        public static string AutoSellLabel(int quality)
        {
            switch (quality)
            {
                case 0: return "自动出售：普通及以下";
                case 1: return "自动出售：优秀及以下";
                case 2: return "自动出售：稀有及以下";
                default: return "自动出售：关";
            }
        }

        public static bool AutoSellApplies(PlayerData p, Equipment e)
        {
            if (p == null || e == null) return false;
            if (!HasService(p, 3)) return false;
            int threshold = p.autoSellQuality;
            return threshold >= 0 && (int)e.quality <= threshold;
        }
    }
}

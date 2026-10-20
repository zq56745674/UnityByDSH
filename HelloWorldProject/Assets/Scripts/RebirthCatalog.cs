using UnityEngine;

namespace AutoBattle
{
    /// <summary>转生的五条永久升级。顺序与 UI 行、存档字段一致，不要插到中间。</summary>
    public enum RebirthTrack
    {
        Battle = 0,
        Growth = 1,
        Fortune = 2,
        Legacy = 3,
        Alchemy = 4,
    }

    /// <summary>
    /// 转生的纯计算层：费用、印记折算、文案。不碰 PlayerData / 存档 / UI，
    /// 探针与编辑器测试可以直接调用。
    /// </summary>
    public static class RebirthCatalog
    {
        public const int MaxRank = 5;
        public const int TrackCount = 5;
        public const int MaxParagonMarks = 10;
        public const int MaxFloorMarks = 5;
        public const int MaxGoldMarks = 14;
        public const int BaseMarks = 1;
        public const int MaxMarks = BaseMarks + MaxParagonMarks + MaxFloorMarks + MaxGoldMarks;

        public static readonly RebirthTrack[] All =
        {
            RebirthTrack.Battle, RebirthTrack.Growth, RebirthTrack.Fortune,
            RebirthTrack.Legacy, RebirthTrack.Alchemy,
        };

        public static string NameOf(RebirthTrack track)
        {
            switch (track)
            {
                case RebirthTrack.Battle: return "战斗本能";
                case RebirthTrack.Growth: return "历练";
                case RebirthTrack.Fortune: return "财运";
                case RebirthTrack.Legacy: return "传承";
                case RebirthTrack.Alchemy: return "行囊";
                default: return "?";
            }
        }

        /// <summary>该条升级在 rank 级时的效果文案（rank 可以是 0）。</summary>
        public static string EffectText(RebirthTrack track, int rank)
        {
            rank = Mathf.Clamp(rank, 0, MaxRank);
            switch (track)
            {
                case RebirthTrack.Battle:
                    return $"攻击 / 防御 / 生命 +{rank * GameConfig.RebirthBattlePerRank * 100f:0.#}%";
                case RebirthTrack.Growth:
                    return $"经验获取 +{rank * GameConfig.RebirthExpPerRank * 100f:0.#}%";
                case RebirthTrack.Fortune:
                    return $"金币 +{rank * GameConfig.RebirthGoldPerRank * 100f:0.#}% · 掉落 +{rank * GameConfig.RebirthDropPerRank * 100f:0.#}%";
                case RebirthTrack.Legacy:
                    return $"下次转生从 Lv.{1 + rank * GameConfig.RebirthLegacyLevelPerRank} 开始";
                case RebirthTrack.Alchemy:
                    return $"下次转生 {rank * GameConfig.RebirthAlchemyPotionPerRank} 瓶药剂 · {rank * GameConfig.RebirthAlchemyGoldPerRank:N0} 金币";
                default:
                    return "";
            }
        }

        public static int BaseCostOf(RebirthTrack track)
        {
            switch (track)
            {
                case RebirthTrack.Battle: return GameConfig.RebirthBattleCostBase;
                case RebirthTrack.Growth: return GameConfig.RebirthGrowthCostBase;
                case RebirthTrack.Fortune: return GameConfig.RebirthFortuneCostBase;
                case RebirthTrack.Legacy: return GameConfig.RebirthLegacyCostBase;
                case RebirthTrack.Alchemy: return GameConfig.RebirthAlchemyCostBase;
                default: return 1;
            }
        }

        /// <summary>从当前 rank 升下一级的费用 = 基数 + 当前 rank。</summary>
        public static int CostOf(RebirthTrack track, int currentRank)
        {
            return BaseCostOf(track) + Mathf.Clamp(currentRank, 0, MaxRank);
        }

        /// <summary>从 0 升满的总费用（用于审计，五条合计应为 110）。</summary>
        public static int TotalCost(RebirthTrack track)
        {
            int sum = 0;
            for (int r = 0; r < MaxRank; r++) sum += CostOf(track, r);
            return sum;
        }

        public static int ParagonMarks(int paragonLevel)
        {
            int step = Mathf.Max(1, GameConfig.RebirthMarkParagonStep);
            return Mathf.Min(MaxParagonMarks, Mathf.Max(0, paragonLevel) / step);
        }

        public static int FloorMarks(int endlessBestFloor)
        {
            int step = Mathf.Max(1, GameConfig.RebirthMarkFloorStep);
            return Mathf.Min(MaxFloorMarks, Mathf.Max(0, endlessBestFloor) / step);
        }

        /// <summary>floor(log2(gold / base))，夹 0..14；整数循环避免浮点误差。</summary>
        public static int GoldMarks(int gold)
        {
            int baseGold = Mathf.Max(1, GameConfig.RebirthMarkGoldBase);
            if (gold < baseGold) return 0;

            long value = gold / baseGold;
            int marks = 0;
            while (value >= 2 && marks < MaxGoldMarks)
            {
                value /= 2;
                marks++;
            }
            return marks;
        }

        /// <summary>本次转生可得印记 = 基础 1 + 巅峰 + 无尽 + 金币。</summary>
        public static int MarksFor(int paragonLevel, int endlessBestFloor, int gold)
        {
            return Mathf.Min(MaxMarks,
                BaseMarks + ParagonMarks(paragonLevel) + FloorMarks(endlessBestFloor) + GoldMarks(gold));
        }
    }
}

using System;
using System.Collections.Generic;
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

    /// <summary>
    /// 存档 v12：一世结束时的成绩单。只记展示需要的字段，不参与任何数值计算。
    /// 榜单按「用时最短」优先，用时相同看印记，再看世数。
    /// </summary>
    [Serializable]
    public sealed class RebirthRecord
    {
        public int life;
        public int seconds;
        public int paragon;
        public int floor;
        public int marks;
    }

    /// <summary>
    /// 转生纪录榜（存档内 Top-5）。纯计算 + 清洗，不碰 PlayerData / 存档 / UI，
    /// 探针与编辑器测试可以直接调用。
    /// </summary>
    public static class RebirthHistory
    {
        public const int Capacity = 5;

        /// <summary>单世上限 30 天；同时用于本世计时钳制，防手改档写爆。</summary>
        public const int MaxSeconds = 86400 * 30;

        public const int MaxLife = 1000000;
        public const int MaxParagon = 1000000;
        public const int MaxFloor = 9999;

        /// <summary>与 RebirthCatalog.MaxMarks 一致，单独写死避免循环引用。</summary>
        public const int MaxMarks = RebirthCatalog.MaxMarks;

        /// <summary>排序：用时升序 → 印记降序 → 世数升序。null 排最后。</summary>
        public static int Compare(RebirthRecord a, RebirthRecord b)
        {
            if (ReferenceEquals(a, b)) return 0;
            if (a == null) return 1;
            if (b == null) return -1;

            int c = a.seconds.CompareTo(b.seconds);
            if (c != 0) return c;
            c = b.marks.CompareTo(a.marks);
            if (c != 0) return c;
            return a.life.CompareTo(b.life);
        }

        /// <summary>把一条纪录夹到合法范围；null / 用时 &lt;= 0 返回 null（不记无效纪录）。</summary>
        public static RebirthRecord Clamp(RebirthRecord record)
        {
            if (record == null || record.seconds <= 0) return null;
            return new RebirthRecord
            {
                life = Mathf.Clamp(record.life, 1, MaxLife),
                seconds = Mathf.Clamp(record.seconds, 1, MaxSeconds),
                paragon = Mathf.Clamp(record.paragon, 0, MaxParagon),
                floor = Mathf.Clamp(record.floor, 0, MaxFloor),
                marks = Mathf.Clamp(record.marks, 0, MaxMarks),
            };
        }

        /// <summary>
        /// 插入一条纪录，返回名次（1 起）；没进前 5 返回 0。
        /// 就地排序并裁到 5 条，调用方负责落盘。
        /// </summary>
        public static int Insert(List<RebirthRecord> top, RebirthRecord record)
        {
            RebirthRecord clean = Clamp(record);
            if (top == null || clean == null) return 0;

            top.RemoveAll(entry => entry == null);
            top.Add(clean);
            top.Sort(Compare);
            int rank = top.IndexOf(clean) + 1;
            if (top.Count > Capacity) top.RemoveRange(Capacity, top.Count - Capacity);
            return rank <= Capacity ? rank : 0;
        }

        /// <summary>读档 / 写档共用的清洗：逐条夹取、丢掉无效项、排序、裁到 5 条，返回新列表。</summary>
        public static List<RebirthRecord> CopyTop(List<RebirthRecord> source)
        {
            var top = new List<RebirthRecord>(Capacity);
            if (source != null)
            {
                for (int i = 0; i < source.Count; i++)
                {
                    RebirthRecord clean = Clamp(source[i]);
                    if (clean != null) top.Add(clean);
                }
            }

            top.Sort(Compare);
            if (top.Count > Capacity) top.RemoveRange(Capacity, top.Count - Capacity);
            return top;
        }

        /// <summary>旧档没有这份榜，空榜即可；保留别名让读档路径与无尽榜写法一致。</summary>
        public static List<RebirthRecord> FromSave(List<RebirthRecord> source) => CopyTop(source);

        /// <summary>面板显示：不足 1 小时 mm:ss，否则 h:mm:ss。</summary>
        public static string FormatSeconds(int seconds)
        {
            int s = Mathf.Clamp(seconds, 0, MaxSeconds);
            int hours = s / 3600;
            int minutes = (s % 3600) / 60;
            int secs = s % 60;
            return hours > 0
                ? string.Format("{0}:{1:00}:{2:00}", hours, minutes, secs)
                : string.Format("{0:00}:{1:00}", minutes, secs);
        }
    }
}

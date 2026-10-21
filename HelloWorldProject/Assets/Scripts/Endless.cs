using System;
using UnityEngine;

namespace AutoBattle
{
    /// <summary>当前这局跑的是战役还是无尽。</summary>
    public enum GameMode
    {
        Campaign,
        Endless,
    }

    /// <summary>无尽一局结束的原因。结果面板据此换文案。</summary>
    public enum EndlessEndReason
    {
        None,
        Death,
        Retreat,
        SlotChanged,
        Reset,
    }

    /// <summary>
    /// 无尽某一层的静态定义。每层只由层号推导，不读战役进度、不吃章节族类偏向，
    /// 所以同一层在今天和明天生成的怪完全一样（随机项只有类型/词缀）。
    /// </summary>
    public sealed class EndlessFloorDef
    {
        public int floor;
        public int monsterLevel;
        public int killTarget;
        public MonsterTier clearTier;
        public bool isBossFloor;
        public MonsterType bossType;

        public bool IsBossFloor => isBossFloor;

        public string Code => $"E{floor}";
    }

    /// <summary>
    /// 无尽层的公式。所有数值都走 GameConfig 的 Endless* 配置项，方便热改与探针调参。
    /// 层号统一夹在 1..<see cref="MaxFloor"/>：9999 是存档记录的上限，也是「永远打不完」的占位。
    /// </summary>
    public static class EndlessCatalog
    {
        public const int MaxFloor = 9999;

        public static EndlessFloorDef Get(int floor)
        {
            int f = Mathf.Clamp(floor, 1, MaxFloor);
            var def = new EndlessFloorDef
            {
                floor = f,
                monsterLevel = MonsterLevelOf(f),
                killTarget = KillTargetOf(f),
                clearTier = ClearTierOf(f),
                isBossFloor = IsBossFloor(f),
            };
            def.bossType = BossTypeOf(f);
            return def;
        }

        public static int MonsterLevelOf(int floor)
        {
            int f = Mathf.Clamp(floor, 1, MaxFloor);
            // 层数越高怪越强，但等级本身也夹一下：MonsterData 的整型面板在 9999 级已经非常夸张，
            // 再往上只是溢出风险，没有玩法意义。
            return Mathf.Clamp(GameConfig.EndlessBaseMonsterLevel + (f - 1) * GameConfig.EndlessMonsterLevelStep,
                1, 9999);
        }

        public static int KillTargetOf(int floor)
        {
            int f = Mathf.Clamp(floor, 1, MaxFloor);
            int every = Mathf.Max(1, GameConfig.EndlessKillTargetEvery);
            return Mathf.Clamp(GameConfig.EndlessKillTargetBase + (f - 1) / every,
                1, Mathf.Max(1, GameConfig.EndlessKillTargetMax));
        }

        /// <summary>本层最后一只的档次：BOSS 层给 BOSS，其余层给精英。</summary>
        public static MonsterTier ClearTierOf(int floor)
        {
            return IsBossFloor(floor) ? MonsterTier.Boss : MonsterTier.Elite;
        }

        public static bool IsBossFloor(int floor)
        {
            int f = Mathf.Clamp(floor, 1, MaxFloor);
            return f % Mathf.Max(1, GameConfig.EndlessBossEvery) == 0;
        }

        /// <summary>BOSS 层的最后一只固定类型，按层的「第几个 BOSS 周期」轮换。</summary>
        public static MonsterType BossTypeOf(int floor)
        {
            int f = Mathf.Clamp(floor, 1, MaxFloor);
            int every = Mathf.Max(1, GameConfig.EndlessBossEvery);
            int cycle = f / every - 1;
            if (cycle < 0) cycle = 0;
            return (MonsterType)(cycle % 3);
        }

        /// <summary>清层金币：只跟层号有关，越往后越多。</summary>
        public static int FloorGoldOf(int floor)
        {
            int f = Mathf.Clamp(floor, 1, MaxFloor);
            long gold = (long)GameConfig.EndlessFloorGoldBase
                + (long)GameConfig.EndlessFloorGoldPerFloor * (f - 1);
            return (int)Math.Clamp(gold, 0L, int.MaxValue);
        }

        /// <summary>新纪录（第一次清到这一层）的额外金币。</summary>
        public static int FirstClearGoldOf(int floor)
        {
            int f = Mathf.Clamp(floor, 1, MaxFloor);
            long gold = (long)GameConfig.EndlessFirstClearGoldBase
                + (long)GameConfig.EndlessFirstClearGoldPerFloor * (f - 1);
            return (int)Math.Clamp(gold, 0L, int.MaxValue);
        }

        /// <summary>
        /// 这一局从哪层开始：有纪录就从「最高层 − 检查点回退」起步，否则第 1 层。
        /// 回退是为了别让玩家每一局都从刚死的那层重来——那通常意味着卡住，
        /// 应该往回退几层刷金币/装备再上。
        /// </summary>
        public static int StartFloorFor(int bestFloor)
        {
            int best = Mathf.Clamp(bestFloor, 0, MaxFloor);
            if (best <= 0) return 1;
            return Mathf.Clamp(best - Mathf.Max(0, GameConfig.EndlessCheckpointOffset), 1, MaxFloor);
        }
    }

    /// <summary>
    /// 一局无尽的运行时状态。不进存档：中途退出就等于放弃这一局，
    /// 只有「最高层」是存档数据（见 PlayerData.endlessBestFloor）。
    /// </summary>
    public sealed class EndlessRunState
    {
        public int floor = 1;
        public int startFloor = 1;
        public int killCount;
        public int runKills;
        public int clearedThisRun;
        public int reachedFloor;
        public EndlessEndReason endReason;

        /// <summary>开局时的 UTC ticks（0 = 未知）。历史榜用它算本局用时。</summary>
        public long startedUtc;

        /// <summary>本局在历史榜上的名次（1 起）；0 = 没上榜。只在结束那一帧写一次。</summary>
        public int historyRank;

        /// <summary>结束那一刻锁定的用时（秒）。结果面板开着的期间 Seconds 还会涨，不能用它显示。</summary>
        public int finishSeconds;

        public int KillsLeft => Mathf.Max(0, EndlessCatalog.KillTargetOf(floor) - killCount);

        /// <summary>本局用时（秒）。中途改系统时间会得到离谱值，所以夹在 0..30 天。</summary>
        public int Seconds
        {
            get
            {
                if (startedUtc <= 0) return 0;
                long delta = DateTime.UtcNow.Ticks - startedUtc;
                if (delta <= 0) return 0;
                return (int)Mathf.Clamp(delta / TimeSpan.TicksPerSecond, 0, MaxSeconds);
            }
        }

        public const int MaxSeconds = 86400 * 30;
    }

    /// <summary>
    /// 最高层记录：除每个存档槽自带的纪录外，另存一份全局纪录。
    /// 全局纪录的用途是「删档/新档不会把玩家打过的层数一起抹掉」——它是本机最好成绩，
    /// 不参与任何玩法解锁，只用于面板展示与跨档比较。
    /// </summary>
    public static class EndlessRecord
    {
        private const int MaxRecord = EndlessCatalog.MaxFloor;

        private static string GlobalKey => GameConfig.SaveKey + ".Endless.Best";

        public static int GlobalBest => Mathf.Clamp(PlayerPrefs.GetInt(GlobalKey, 0), 0, MaxRecord);

        /// <summary>记录一次新的本机最高层；不会降级已有纪录。</summary>
        public static int ReportGlobal(int floor)
        {
            int best = Mathf.Max(GlobalBest, Mathf.Clamp(floor, 0, MaxRecord));
            PlayerPrefs.SetInt(GlobalKey, best);
            PlayerPrefs.Save();
            return best;
        }

        /// <summary>读档后把本档纪录回填到全局纪录（老档第一次进游戏时用）。</summary>
        public static int SyncGlobal(int floor)
        {
            int local = Mathf.Clamp(floor, 0, MaxRecord);
            return local > GlobalBest ? ReportGlobal(local) : GlobalBest;
        }
    }

    /// <summary>
    /// 一局无尽的成绩单（存档 v11 起的历史榜条目）。只记展示需要的量，
    /// 层数以「本局到达的最高层」为准：检查点开局本来就允许从高层起步，
    /// 榜单比的是这一局把记录推到了哪，而不是从第 1 层爬了多少层。
    /// </summary>
    [Serializable]
    public sealed class EndlessScoreEntry
    {
        /// <summary>本局到达过的最高层（阵亡/撤退时站的那一层）。</summary>
        public int floor;

        /// <summary>本局真正清完的层数。</summary>
        public int cleared;

        /// <summary>本局击杀总数。</summary>
        public int kills;

        /// <summary>结束时巅峰等级。</summary>
        public int paragon;

        /// <summary>结束时是第几世（转生次数，0 = 第 1 世）。</summary>
        public int rebirth;

        /// <summary>本局用时（秒）。</summary>
        public int seconds;

        /// <summary>结束时间（UTC ticks，0 = 未知）。</summary>
        public long endedUtc;

        /// <summary>结束原因，存 <see cref="EndlessEndReason"/> 的整数值。</summary>
        public int reason;

        /// <summary>是不是玩家自己按的撤退（而不是阵亡）。</summary>
        public bool IsRetreat => reason == (int)EndlessEndReason.Retreat;
    }

    /// <summary>
    /// Top-N 历史榜：存每档最好的一小串局，榜首规则是
    /// 「到达层数高 → 本局清除层数多 → 更早做到（老纪录先站住）」。
    /// 纯数据操作，不碰 UI 也不碰存档，方便在编辑器的探针里直接跑。
    /// </summary>
    public static class EndlessHistory
    {
        /// <summary>留几局。榜单只用于展示，留多了面板放不下。</summary>
        public const int Capacity = 5;

        /// <summary>排序：层数降序 → 清除层数降序 → 结束时间升序（先做到的在前）。</summary>
        public static int Compare(EndlessScoreEntry a, EndlessScoreEntry b)
        {
            if (a == null && b == null) return 0;
            if (a == null) return 1;
            if (b == null) return -1;

            int c = b.floor.CompareTo(a.floor);
            if (c != 0) return c;
            c = b.cleared.CompareTo(a.cleared);
            if (c != 0) return c;
            c = a.endedUtc.CompareTo(b.endedUtc);
            if (c != 0) return c;
            return b.seconds.CompareTo(a.seconds);
        }

        /// <summary>
        /// 插一局进榜，返回名次（1 起）；掉出前 <see cref="Capacity"/> 名时返回 0。
        /// 会就地排序并裁掉多出来的尾部，调用方不需要再整理。
        /// </summary>
        public static int Insert(System.Collections.Generic.List<EndlessScoreEntry> list, EndlessScoreEntry entry)
        {
            if (list == null || entry == null) return 0;

            Clamp(entry);
            list.Add(entry);
            list.Sort(Compare);

            int rank = list.IndexOf(entry) + 1;
            if (list.Count > Capacity) list.RemoveRange(Capacity, list.Count - Capacity);
            return rank <= Capacity ? rank : 0;
        }

        /// <summary>存档用：拷一份干净的 Top-N（不改动源列表）。</summary>
        public static System.Collections.Generic.List<EndlessScoreEntry> CopyTop(
            System.Collections.Generic.List<EndlessScoreEntry> source)
        {
            var result = new System.Collections.Generic.List<EndlessScoreEntry>();
            if (source == null) return result;
            for (int i = 0; i < source.Count && result.Count < Capacity; i++)
            {
                EndlessScoreEntry entry = source[i];
                if (entry == null) continue;
                var copy = new EndlessScoreEntry
                {
                    floor = entry.floor,
                    cleared = entry.cleared,
                    kills = entry.kills,
                    paragon = entry.paragon,
                    rebirth = entry.rebirth,
                    seconds = entry.seconds,
                    endedUtc = entry.endedUtc,
                    reason = entry.reason,
                };
                Clamp(copy);
                result.Add(copy);
            }
            return result;
        }

        /// <summary>读档用：丢掉 null、夹住越界值，重新按同一规则排序并裁到 Capacity。</summary>
        public static System.Collections.Generic.List<EndlessScoreEntry> FromSave(
            System.Collections.Generic.List<EndlessScoreEntry> source)
        {
            var result = new System.Collections.Generic.List<EndlessScoreEntry>();
            if (source == null) return result;
            for (int i = 0; i < source.Count; i++)
            {
                EndlessScoreEntry entry = source[i];
                if (entry == null) continue;
                var copy = new EndlessScoreEntry
                {
                    floor = entry.floor,
                    cleared = entry.cleared,
                    kills = entry.kills,
                    paragon = entry.paragon,
                    rebirth = entry.rebirth,
                    seconds = entry.seconds,
                    endedUtc = entry.endedUtc,
                    reason = entry.reason,
                };
                Clamp(copy);
                result.Add(copy);
            }
            result.Sort(Compare);
            if (result.Count > Capacity) result.RemoveRange(Capacity, result.Count - Capacity);
            return result;
        }

        /// <summary>手改档/异常值兜底：每一项都夹回合法区间。</summary>
        public static void Clamp(EndlessScoreEntry entry)
        {
            if (entry == null) return;
            entry.floor = Mathf.Clamp(entry.floor, 0, EndlessCatalog.MaxFloor);
            entry.cleared = Mathf.Clamp(entry.cleared, 0, EndlessCatalog.MaxFloor);
            entry.kills = Mathf.Clamp(entry.kills, 0, 100000000);
            entry.paragon = Mathf.Clamp(entry.paragon, 0, GameConfig.MaxParagonLevel);
            entry.rebirth = Mathf.Clamp(entry.rebirth, 0, 1000000);
            entry.seconds = Mathf.Clamp(entry.seconds, 0, EndlessRunState.MaxSeconds);
            if (entry.reason < (int)EndlessEndReason.None || entry.reason > (int)EndlessEndReason.Reset)
                entry.reason = (int)EndlessEndReason.None;
            // 时间戳异常（0 / 未来）统一当「未知」，UI 显示成 —。
            if (entry.endedUtc < 0) entry.endedUtc = 0;
        }
    }
}

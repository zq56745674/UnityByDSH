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

        public int KillsLeft => Mathf.Max(0, EndlessCatalog.KillTargetOf(floor) - killCount);
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
}

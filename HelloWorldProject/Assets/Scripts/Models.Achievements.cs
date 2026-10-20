using System;
using System.Collections.Generic;

namespace AutoBattle
{
    /// <summary>
    /// 一个成就的静态定义。整张表放在 Assets/Resources/achievements.json，
    /// 便于调整文案与数值而不改代码；加载与校验见 AchievementCatalog。
    /// </summary>
    [Serializable]
    public class AchievementDef
    {
        public string id;
        public string category;
        public string tier;
        public string name;
        public string desc;

        /// <summary>指标名，见 AchievementSystem.Metric。</summary>
        public string metric;

        /// <summary>达成阈值；指标值大于等于它即解锁。</summary>
        public int target;

        /// <summary>成就点数（只展示，不提供战斗属性）。</summary>
        public int points;

        /// <summary>解锁时发放的固定金币（不吃转生财运）。</summary>
        public int gold;

        /// <summary>解锁后获得的称号；空字符串表示没有称号。</summary>
        public string title;

        /// <summary>预留：隐藏成就。当前表全部为 false。</summary>
        public bool hidden;
    }

    [Serializable]
    public class AchievementTableData
    {
        public int version;
        public AchievementDef[] achievements;
    }

    /// <summary>
    /// 每个存档槽独立的成就状态（存档 v10）。unlocked 只存成就 id，
    /// 顺序不影响求值；求值永远按目录顺序走。
    /// </summary>
    [Serializable]
    public class AchievementState
    {
        /// <summary>状态结构版本。当前为 1；高于 1 的存档拒绝读取，避免降级覆盖。</summary>
        public int schemaVersion = 1;

        public List<string> unlocked = new List<string>();

        public bool IsUnlocked(string id) => unlocked != null && unlocked.Contains(id);
    }
}

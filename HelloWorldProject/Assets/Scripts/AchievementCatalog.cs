using System;
using System.Collections.Generic;
using UnityEngine;

namespace AutoBattle
{
    /// <summary>
    /// 成就目录：从 Assets/Resources/achievements.json 读取整张表并做结构校验。
    /// 与 GameConfig 不同，成就是内容表而不是数值配置，所以代码里不留一份容易漂移的副本；
    /// JSON 缺失或校验失败时目录为空，UI 显示空列表并打警告。
    /// </summary>
    public static class AchievementCatalog
    {
        public const string ResourcePath = "achievements";
        public const int ExpectedCount = 42;
        public const int ExpectedPoints = 805;
        public const int ExpectedGold = 43400;
        public const int ExpectedTitles = 7;

        private static AchievementDef[] _all = new AchievementDef[0];
        private static readonly Dictionary<string, AchievementDef> _byId =
            new Dictionary<string, AchievementDef>(StringComparer.Ordinal);

        /// <summary>按 JSON 数组顺序排列；面板也按这个顺序展示。</summary>
        public static IReadOnlyList<AchievementDef> All => _all;

        public static bool Loaded { get; private set; }

        public static int Count => _all.Length;
        public static int TotalPoints { get; private set; }
        public static int TotalGold { get; private set; }
        public static int TitleCount { get; private set; }

        /// <summary>页签顺序：全部 + 六个类别。</summary>
        public static readonly string[] CategoryKeys =
            { "all", "starter", "progression", "combat", "equipment", "skill", "endless" };

        static AchievementCatalog()
        {
            try { Reload(); }
            catch (Exception e) { Debug.LogWarning("[AchievementCatalog] 初始加载失败：" + e.Message); }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AutoLoad()
        {
            try { Reload(); }
            catch (Exception e)
            {
                Loaded = false;
                Debug.LogWarning("[AchievementCatalog] 自动加载失败：" + e.Message);
            }
        }

        /// <summary>重新读取 achievements.json；返回是否成功载入并通过校验。</summary>
        public static bool Reload()
        {
            TextAsset asset = Resources.Load<TextAsset>(ResourcePath);
            if (asset == null)
            {
                Loaded = false;
                Debug.LogWarning("[AchievementCatalog] 找不到 Resources/achievements.json，成就目录为空。");
                return false;
            }

            try
            {
                ApplyJson(asset.text);
                Loaded = true;
                return true;
            }
            catch (Exception e)
            {
                Loaded = false;
                Debug.LogWarning("[AchievementCatalog] achievements.json 校验失败，成就目录为空：" + e.Message);
                return false;
            }
        }

        /// <summary>解析并校验整张表；任何结构性问题都让整表被拒，避免半张表悄悄生效。</summary>
        public static void ApplyJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) throw new ArgumentException("成就表为空");

            AchievementTableData table = JsonUtility.FromJson<AchievementTableData>(json);
            if (table == null || table.achievements == null) throw new ArgumentException("缺少 achievements 数组");
            if (table.version != 1)
                throw new ArgumentException($"不支持的成就表版本 {table.version}（当前只认 1）");
            if (table.achievements.Length != ExpectedCount)
                throw new ArgumentException($"条目数 {table.achievements.Length}，应为 {ExpectedCount}");

            var byId = new Dictionary<string, AchievementDef>(StringComparer.Ordinal);
            int points = 0, gold = 0, titles = 0;
            for (int i = 0; i < table.achievements.Length; i++)
            {
                AchievementDef def = table.achievements[i];
                if (def == null) throw new ArgumentException($"第 {i} 条为空");
                if (string.IsNullOrWhiteSpace(def.id)) throw new ArgumentException($"第 {i} 条缺少 id");
                if (byId.ContainsKey(def.id)) throw new ArgumentException($"id 重复：{def.id}");
                if (!IsCategory(def.category)) throw new ArgumentException($"{def.id} 的类别非法：{def.category}");
                if (!IsTier(def.tier)) throw new ArgumentException($"{def.id} 的级别非法：{def.tier}");
                if (string.IsNullOrWhiteSpace(def.metric)) throw new ArgumentException($"{def.id} 缺少 metric");
                if (def.target <= 0) throw new ArgumentException($"{def.id} 的 target 必须为正");
                if (def.points <= 0) throw new ArgumentException($"{def.id} 的 points 必须为正");
                if (def.gold < 0) throw new ArgumentException($"{def.id} 的 gold 不能为负");
                if (def.name == null) def.name = string.Empty;
                if (def.desc == null) def.desc = string.Empty;
                if (def.title == null) def.title = string.Empty;

                byId.Add(def.id, def);
                points += def.points;
                gold += def.gold;
                if (!string.IsNullOrEmpty(def.title)) titles++;
            }

            if (points != ExpectedPoints)
                throw new ArgumentException($"总点数 {points}，应为 {ExpectedPoints}");
            if (gold != ExpectedGold)
                throw new ArgumentException($"总金币 {gold}，应为 {ExpectedGold}");
            if (titles != ExpectedTitles)
                throw new ArgumentException($"称号数 {titles}，应为 {ExpectedTitles}");

            _all = table.achievements;
            _byId.Clear();
            foreach (var pair in byId) _byId[pair.Key] = pair.Value;
            TotalPoints = points;
            TotalGold = gold;
            TitleCount = titles;
        }

        public static bool Contains(string id) =>
            !string.IsNullOrEmpty(id) && _byId.ContainsKey(id);

        public static AchievementDef Get(string id)
        {
            AchievementDef def;
            return !string.IsNullOrEmpty(id) && _byId.TryGetValue(id, out def) ? def : null;
        }

        public static bool IsCategory(string category) =>
            category == "starter" || category == "progression" || category == "combat"
            || category == "equipment" || category == "skill" || category == "endless";

        public static bool IsTier(string tier) =>
            tier == "bronze" || tier == "silver" || tier == "gold" || tier == "platinum";

        public static string CategoryLabel(string key)
        {
            switch (key)
            {
                case "starter": return "新手";
                case "progression": return "进程";
                case "combat": return "战斗";
                case "equipment": return "装备";
                case "skill": return "技能";
                case "endless": return "无尽";
                default: return "全部";
            }
        }

        public static string TierLabel(string tier)
        {
            switch (tier)
            {
                case "bronze": return "青铜";
                case "silver": return "白银";
                case "gold": return "黄金";
                case "platinum": return "铂金";
                default: return tier ?? string.Empty;
            }
        }

        public static Color TierColor(string tier)
        {
            switch (tier)
            {
                case "bronze": return new Color(0.690f, 0.478f, 0.290f);  // #B07A4A
                case "silver": return new Color(0.722f, 0.761f, 0.800f);  // #B8C2CC
                case "gold": return new Color(0.851f, 0.643f, 0.255f);    // #D9A441
                case "platinum": return new Color(0.498f, 0.831f, 0.910f); // #7FD4E8
                default: return Color.gray;
            }
        }
    }
}

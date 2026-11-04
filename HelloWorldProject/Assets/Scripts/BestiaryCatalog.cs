using System.Collections.Generic;
using UnityEngine;

namespace AutoBattle
{
    /// <summary>
    /// 屠魔录的一条记录：11 族 × 4 型（近战 / 远程 / 精锐 / BOSS）= 44 条。
    /// 索引固定为 family * KindCount + kind，存档里的击杀数组也按这个顺序存，
    /// 所以**族序与型序都不能改**（改了要做存档迁移）。
    /// </summary>
    public sealed class BestiaryEntry
    {
        public int index;
        public int family;          // 0..10，对应 GameConfig.ChapterFamilyNames
        public int kind;            // 0 近战 / 1 远程 / 2 精锐 / 3 BOSS
        public string familyKey;    // goblin
        public string familyName;   // 哥布林
        public string kindName;     // 近战
        public string entryName;    // 哥布林 · 近战
        public string spritePath;   // Resources 路径（小怪 / BOSS 分开）
        public int chapter;         // 出现章节（1 起）
        public int target;          // 通晓所需击杀数
    }

    /// <summary>
    /// 屠魔录目录与记账规则。纯数据 + 纯函数：不碰 UI、不碰存档 IO，
    /// 编辑器探针可以直接造 PlayerData 调 RecordKill 验算。
    ///
    /// 设计口径（docs/gameplay-expansion.md §7）：
    /// - 解锁分两级：击杀 ≥1 = **目击**（去剪影、显示名字）；击杀 ≥target = **通晓**（金印）。
    /// - 奖励**只给解锁型**，不给任何战斗加成（反例③「被动加成型图鉴」明确不做）。
    ///   一族的四条全部通晓 = 该族「族印」点亮，只进面板与成就文案，不改进数值。
    /// - 族序与 GameUI.Stage 的 StageFamilyKeys / GameConfig.ChapterFamilyNames 一致。
    /// </summary>
    public static class BestiaryCatalog
    {
        public const int FamilyCount = 11;
        public const int KindCount = 4;
        public const int EntryCount = FamilyCount * KindCount;

        /// <summary>目击门槛：杀过 1 只就算见过。</summary>
        public const int WitnessThreshold = 1;

        /// <summary>单条击杀数封顶：防手改档把它写成 int.MaxValue 之后自增溢出。</summary>
        public const int KillCap = 999999;

        /// <summary>型序固定：小怪三型 + BOSS。</summary>
        public static readonly string[] KindKeys = { "melee", "ranged", "crit", "boss" };

        public static readonly string[] KindNames = { "近战", "远程", "精锐", "BOSS" };

        /// <summary>族序 = 章节序，与 GameConfig.ChapterFamilyNames 一一对应。</summary>
        public static readonly string[] FamilyKeys =
        {
            "goblin", "kobold", "skeleton", "ogre", "orc", "spider",
            "undead", "gargoyle", "demon", "abyss", "dragon"
        };

        private static readonly BestiaryEntry[] _entries = Build();

        public static IReadOnlyList<BestiaryEntry> Entries => _entries;

        public static BestiaryEntry Get(int index)
        {
            if (index < 0 || index >= _entries.Length) return null;
            return _entries[index];
        }

        public static int IndexOf(int family, int kind)
        {
            family = Mathf.Clamp(family, 0, FamilyCount - 1);
            kind = Mathf.Clamp(kind, 0, KindCount - 1);
            return family * KindCount + kind;
        }

        /// <summary>章号（1 起）→ 族序（0 起）；超出章节数的等级按最后一族算（无尽深层）。</summary>
        public static int FamilyOfMonsterLevel(int monsterLevel)
        {
            return Mathf.Clamp(GameConfig.ChapterOfMonsterLevel(monsterLevel) - 1, 0, FamilyCount - 1);
        }

        /// <summary>怪物 → 型序。BOSS 优先于类型；精英仍然是它原本的小怪型（精英是 tier 不是族）。</summary>
        public static int KindOf(MonsterType type, MonsterTier tier)
        {
            if (tier == MonsterTier.Boss) return 3;
            switch (type)
            {
                case MonsterType.Ranged: return 1;
                case MonsterType.Crit: return 2;
                default: return 0;
            }
        }

        /// <summary>某型的通晓门槛。小怪 20 / 精锐 12 / BOSS 3：一章 10 关（每关 4 杀）就能把小怪型刷满，
        /// BOSS 一章只有章末一次，所以要复刷或走无尽。</summary>
        public static int TargetOf(int kind)
        {
            switch (kind)
            {
                case 2: return 12;
                case 3: return 3;
                default: return 20;
            }
        }

        public static int KillsOf(PlayerData player, int index)
        {
            if (player == null || player.bestiaryKills == null) return 0;
            if (index < 0 || index >= player.bestiaryKills.Length) return 0;
            return Mathf.Max(0, player.bestiaryKills[index]);
        }

        public static bool IsWitnessed(PlayerData player, int index)
        {
            return KillsOf(player, index) >= WitnessThreshold;
        }

        public static bool IsMastered(PlayerData player, int index)
        {
            BestiaryEntry entry = Get(index);
            return entry != null && KillsOf(player, index) >= entry.target;
        }

        public static int WitnessedCount(PlayerData player)
        {
            int count = 0;
            for (int i = 0; i < EntryCount; i++) if (IsWitnessed(player, i)) count++;
            return count;
        }

        public static int MasteredCount(PlayerData player)
        {
            int count = 0;
            for (int i = 0; i < EntryCount; i++) if (IsMastered(player, i)) count++;
            return count;
        }

        /// <summary>该族四条是否全部通晓（= 族印点亮）。</summary>
        public static bool IsFamilyComplete(PlayerData player, int family)
        {
            for (int kind = 0; kind < KindCount; kind++)
            {
                if (!IsMastered(player, IndexOf(family, kind))) return false;
            }
            return true;
        }

        public static int CompleteFamilyCount(PlayerData player)
        {
            int count = 0;
            for (int family = 0; family < FamilyCount; family++)
                if (IsFamilyComplete(player, family)) count++;
            return count;
        }

        /// <summary>
        /// 记一次击杀，返回「这一只把哪条从目击/未达成推到了通晓」的索引；
        /// 没跨过门槛返回 -1（UI 只在真正通晓时打日志/弹提示）。
        /// </summary>
        public static int RecordKill(PlayerData player, int monsterLevel, MonsterType type, MonsterTier tier)
        {
            if (player == null) return -1;

            player.bestiaryKills = Sanitize(player.bestiaryKills);

            int index = IndexOf(FamilyOfMonsterLevel(monsterLevel), KindOf(type, tier));
            BestiaryEntry entry = _entries[index];
            int before = player.bestiaryKills[index];
            if (before >= KillCap) return -1;

            int after = before + 1;
            player.bestiaryKills[index] = after;

            return before < entry.target && after >= entry.target ? index : -1;
        }

        /// <summary>把存档里的数组补齐/截断到 44 条并夹到合法范围；null 视为全 0。</summary>
        public static int[] Sanitize(int[] source)
        {
            var result = new int[EntryCount];
            if (source != null)
            {
                int count = Mathf.Min(source.Length, EntryCount);
                for (int i = 0; i < count; i++) result[i] = Mathf.Clamp(source[i], 0, KillCap);
            }
            return result;
        }

        /// <summary>面板与 tooltip 用的一句话进度：「3/20」/「已通晓」/「未目击」。</summary>
        public static string ProgressText(PlayerData player, int index)
        {
            BestiaryEntry entry = Get(index);
            if (entry == null) return string.Empty;

            int kills = KillsOf(player, index);
            if (kills >= entry.target) return "已通晓";
            if (kills < WitnessThreshold) return "未目击";
            return kills + "/" + entry.target;
        }

        private static BestiaryEntry[] Build()
        {
            var entries = new BestiaryEntry[EntryCount];
            for (int family = 0; family < FamilyCount; family++)
            {
                string familyKey = family < FamilyKeys.Length ? FamilyKeys[family] : "goblin";
                string familyName = family < GameConfig.ChapterFamilyNames.Length
                    ? GameConfig.ChapterFamilyNames[family] : "未知";

                for (int kind = 0; kind < KindCount; kind++)
                {
                    bool boss = kind == 3;
                    int index = family * KindCount + kind;
                    entries[index] = new BestiaryEntry
                    {
                        index = index,
                        family = family,
                        kind = kind,
                        familyKey = familyKey,
                        familyName = familyName,
                        kindName = KindNames[kind],
                        entryName = familyName + " · " + KindNames[kind],
                        spritePath = boss
                            ? "Sprites/Bosses/" + familyKey + "_boss"
                            : "Sprites/Monsters/" + familyKey + "_" + KindKeys[kind],
                        chapter = family + 1,
                        target = TargetOf(kind),
                    };
                }
            }
            return entries;
        }
    }
}

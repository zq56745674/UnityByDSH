using UnityEngine;

namespace AutoBattle
{
    /// <summary>
    /// 深层契约的八条（docs/gameplay-expansion.md §5）。契约只改规则与敌人，
    /// 不动玩家面板：加成走 <see cref="ContractMods"/> 注入生成怪与结算，不进技能/装备加算池。
    /// </summary>
    public enum ContractKind
    {
        /// <summary>狂躁：敌人攻击更高。</summary>
        Frenzy = 0,
        /// <summary>疾袭：敌人出手更快。</summary>
        Swift = 1,
        /// <summary>硬化：敌人更耐打（受到的伤害降低）。</summary>
        Hardened = 2,
        /// <summary>群袭：守关怪按更深层数生成。</summary>
        Swarm = 3,
        /// <summary>枯竭：治疗与药水效果被压。</summary>
        Drain = 4,
        /// <summary>背水：生命上限被压低。</summary>
        LastStand = 5,
        /// <summary>黑雾：暴击率被扣百分点。</summary>
        BlackFog = 6,
        /// <summary>决绝：本局不能撤退（单档）。</summary>
        NoRetreat = 7,
    }

    /// <summary>一条契约的静态定义：三档效果文本、数值与契约分系数。</summary>
    public sealed class ContractDef
    {
        public ContractKind kind;
        public string name;
        /// <summary>一句话规则说明（面板与结算卡用）。</summary>
        public string text;
        /// <summary>一格内的一行紧凑效果（三档数值写在一起，如「攻击 +15/30/50%」）。</summary>
        public string shortText;
        /// <summary>三档的效果文本，下标 0=Ⅰ / 1=Ⅱ / 2=Ⅲ。</summary>
        public string[] tierText;
        /// <summary>三档的数值，含义由 <see cref="kind"/> 决定。</summary>
        public float[] values;
        /// <summary>三档的契约分系数（与档位一起乘已清层数）。</summary>
        public float[] coef;

        /// <summary>可选档数：决绝只有一档。</summary>
        public int TierCount => tierText != null ? tierText.Length : 0;

        public string CoefText(int tier)
        {
            if (tier <= 0 || tier > TierCount) return string.Empty;
            return $"+{coef[tier - 1] * 100f:0}%";
        }
    }

    /// <summary>
    /// 深层契约目录与结算数学。纯数据 + 纯函数，编辑器的种子对照探针可以照原样调用。
    ///
    /// 契约分 = 本局已清层数 ×（1 + 所选档位系数之和）。系数 Ⅰ/Ⅱ/Ⅲ = 0.25 / 0.5 / 0.9，
    /// 决绝只有一档、按 0.9 计。累计契约分到 <see cref="Milestones"/> 的门槛解锁遗物档位与槽位。
    /// </summary>
    public static class ContractCatalog
    {
        /// <summary>一局最多同时选几条契约。</summary>
        public const int MaxSelected = 3;

        /// <summary>累计契约分到这么多以后，之后每次结算只计一半（softcap）。</summary>
        public const int SoftcapScore = 3000;

        /// <summary>softcap 之后的记账系数。</summary>
        public const float SoftcapFactor = 0.5f;

        /// <summary>一个里程碑：累计契约分达标后解锁的东西。</summary>
        public struct Milestone
        {
            public int score;
            public string name;
            public string desc;
        }

        /// <summary>里程碑按分数升序：遗物 tier2 → 第 7 槽 → 遗物 tier3 → 第 8 槽。</summary>
        public static readonly Milestone[] Milestones =
        {
            new Milestone { score = 300, name = "遗物 · 第二档", desc = "三选一开始出现第二档遗物" },
            new Milestone { score = 800, name = "第七槽", desc = "遗物槽位 6 → 7" },
            new Milestone { score = 1500, name = "遗物 · 第三档", desc = "三选一开始出现第三档遗物" },
            new Milestone { score = 3000, name = "第八槽", desc = "遗物槽位 7 → 8" },
        };

        /// <summary>当前累计契约分能达到的里程碑数量（0~4）。</summary>
        public static int MilestoneCount(int score)
        {
            int count = 0;
            for (int i = 0; i < Milestones.Length; i++)
                if (score >= Milestones[i].score) count++;
            return count;
        }

        /// <summary>下一个里程碑；全部达成时返回 null。</summary>
        public static string NextMilestoneText(int score)
        {
            for (int i = 0; i < Milestones.Length; i++)
            {
                if (score < Milestones[i].score)
                    return $"距「{Milestones[i].name}」还差 {Milestones[i].score - score} 分";
            }
            return "全部里程碑已达成";
        }

        /// <summary>某一档遗物在多少契约分时进入三选一（tier1 恒开放）。</summary>
        public static int RelicTierUnlockScore(int tier)
        {
            switch (tier)
            {
                case 2: return 300;
                case 3: return 1500;
                default: return 0;
            }
        }

        /// <summary>按累计契约分算「三选一里最高能出现哪一档」。</summary>
        public static int RelicMaxTier(int score)
        {
            if (score >= RelicTierUnlockScore(3)) return 3;
            if (score >= RelicTierUnlockScore(2)) return 2;
            return 1;
        }

        /// <summary>按累计契约分算本局遗物槽位（6 起，800 第 7、3000 第 8）。</summary>
        public static int RelicSlots(int score)
        {
            if (score >= Milestones[3].score) return 8;
            if (score >= Milestones[1].score) return 7;
            return 6;
        }

        /// <summary>八条契约的静态表。数值顺序对应 Ⅰ/Ⅱ/Ⅲ 三档；决绝只有一档。</summary>
        public static readonly ContractDef[] All =
        {
            new ContractDef
            {
                kind = ContractKind.Frenzy, name = "狂躁",
                text = "敌人的攻击更高", shortText = "攻击 +15/30/50%",
                tierText = new[] { "敌人攻击 +15%", "敌人攻击 +30%", "敌人攻击 +50%" },
                values = new[] { 0.15f, 0.30f, 0.50f },
                coef = new[] { 0.25f, 0.5f, 0.9f },
            },
            new ContractDef
            {
                kind = ContractKind.Swift, name = "疾袭",
                text = "敌人的出手更快", shortText = "攻速 +12/25/45%",
                tierText = new[] { "敌人攻速 +12%", "敌人攻速 +25%", "敌人攻速 +45%" },
                values = new[] { 0.12f, 0.25f, 0.45f },
                coef = new[] { 0.25f, 0.5f, 0.9f },
            },
            new ContractDef
            {
                kind = ContractKind.Hardened, name = "硬化",
                text = "敌人皮糙肉厚，吃伤害更少", shortText = "敌人受伤 −15/35/60%",
                tierText = new[] { "敌人受到伤害 -15%", "敌人受到伤害 -35%", "敌人受到伤害 -60%" },
                values = new[] { 0.15f, 0.35f, 0.60f },
                coef = new[] { 0.25f, 0.5f, 0.9f },
            },
            new ContractDef
            {
                kind = ContractKind.Swarm, name = "群袭",
                text = "整层怪物都被深渊推高了层数", shortText = "怪物 +12/+18/+26 层",
                tierText = new[] { "每只怪按 +12 层强度", "每只怪按 +18 层强度", "每只怪按 +26 层强度" },
                values = new[] { 12f, 18f, 26f },
                coef = new[] { 0.25f, 0.5f, 0.9f },
            },
            new ContractDef
            {
                kind = ContractKind.Drain, name = "枯竭",
                text = "治疗与药水效果被压", shortText = "治疗 −30/45/60%",
                tierText = new[] { "治疗与药水 -30%", "治疗与药水 -45%", "治疗与药水 -60%" },
                values = new[] { 0.30f, 0.45f, 0.60f },
                coef = new[] { 0.25f, 0.5f, 0.9f },
            },
            new ContractDef
            {
                kind = ContractKind.LastStand, name = "背水",
                text = "生命上限被压低", shortText = "生命上限 −12/25/45%",
                tierText = new[] { "生命上限 -12%", "生命上限 -25%", "生命上限 -45%" },
                values = new[] { 0.12f, 0.25f, 0.45f },
                coef = new[] { 0.25f, 0.5f, 0.9f },
            },
            new ContractDef
            {
                kind = ContractKind.BlackFog, name = "黑雾",
                text = "黑雾腐蚀护甲，减伤大幅缩水", shortText = "护甲有效率 55/40/28%",
                tierText = new[] { "护甲减伤只剩 55%", "护甲减伤只剩 40%", "护甲减伤只剩 28%" },
                values = new[] { 0.45f, 0.60f, 0.72f },
                coef = new[] { 0.25f, 0.5f, 0.9f },
            },
            new ContractDef
            {
                kind = ContractKind.NoRetreat, name = "决绝",
                text = "本局不能撤退（单档）", shortText = "不可主动撤退",
                tierText = new[] { "本局不能主动撤退" },
                values = new[] { 0f },
                coef = new[] { 0.9f },
            },
        };

        public static ContractDef Get(ContractKind kind)
        {
            int i = (int)kind;
            return i >= 0 && i < All.Length ? All[i] : null;
        }

        /// <summary>把档位数组（长度 8，0=未选）夹成合法值：删掉越界、决绝只留 Ⅰ、最多 3 条。</summary>
        public static int[] Sanitize(int[] tiers)
        {
            var clean = new int[All.Length];
            if (tiers == null) return clean;

            int selected = 0;
            for (int i = 0; i < clean.Length && i < tiers.Length; i++)
            {
                ContractDef def = All[i];
                int tier = tiers[i];
                if (tier <= 0 || tier > def.TierCount) continue;
                if (selected >= MaxSelected) break;
                clean[i] = tier;
                selected++;
            }
            return clean;
        }

        /// <summary>已选条数。</summary>
        public static int CountSelected(int[] tiers)
        {
            if (tiers == null) return 0;
            int n = 0;
            for (int i = 0; i < tiers.Length; i++) if (tiers[i] > 0) n++;
            return n;
        }

        /// <summary>档位系数之和（0 = 没选契约）。</summary>
        public static float CoefSum(int[] tiers)
        {
            if (tiers == null) return 0f;
            float sum = 0f;
            for (int i = 0; i < tiers.Length && i < All.Length; i++)
            {
                int tier = tiers[i];
                if (tier <= 0 || tier > All[i].TierCount) continue;
                sum += All[i].coef[tier - 1];
            }
            return sum;
        }

        /// <summary>本局契约分：已清层数 ×（1 + 系数之和）。没选契约返回 0。</summary>
        public static int ScoreOf(int clearedFloors, int[] tiers)
        {
            float sum = CoefSum(tiers);
            if (sum <= 0f) return 0;
            return Mathf.Max(0, Mathf.RoundToInt(clearedFloors * (1f + sum)));
        }

        /// <summary>把本局契约分记进累计值；累计已过 softcap 时只计一半。</summary>
        public static int AddToTotal(int totalScore, int runScore)
        {
            if (runScore <= 0) return Mathf.Max(0, totalScore);
            int gain = totalScore >= SoftcapScore
                ? Mathf.RoundToInt(runScore * SoftcapFactor)
                : runScore;
            return Mathf.Clamp(totalScore + Mathf.Max(0, gain), 0, 1000000000);
        }

        /// <summary>面板上的一行摘要：「狂躁Ⅱ · 疾袭Ⅰ」；没选返回「未选契约」。</summary>
        public static string Describe(int[] tiers)
        {
            if (tiers == null) return "未选契约";
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < tiers.Length && i < All.Length; i++)
            {
                int tier = tiers[i];
                if (tier <= 0 || tier > All[i].TierCount) continue;
                if (sb.Length > 0) sb.Append(" · ");
                sb.Append(All[i].name);
                sb.Append(tier == 1 ? "Ⅰ" : tier == 2 ? "Ⅱ" : "Ⅲ");
            }
            return sb.Length > 0 ? sb.ToString() : "未选契约";
        }
    }

    /// <summary>
    /// 一局契约聚合出来的修正。null 或 IsEmpty 表示没有契约（战役、或没勾任何一条）。
    /// 只作用于敌人与规则，不并入玩家的技能/装备加算池。
    /// </summary>
    public sealed class ContractMods
    {
        /// <summary>敌人攻击乘数（狂躁）。</summary>
        public float enemyAttackMult = 1f;
        /// <summary>敌人攻速乘数（疾袭，1.2 = +20% 出手频率）。</summary>
        public float enemyAttackSpeedMult = 1f;
        /// <summary>敌人受到伤害的乘数（硬化，&lt; 1 = 更耐打）。</summary>
        public float monsterDamageTakenMult = 1f;
        /// <summary>群袭：所有怪物按「层数 + N」的强度生成；0 = 不加强。</summary>
        public int clearFloorBonus;
        /// <summary>治疗与药水的乘数（枯竭，0 = 完全不回血）。</summary>
        public float healMult = 1f;
        /// <summary>生命上限乘数（背水）。</summary>
        public float maxHpMult = 1f;
        /// <summary>玩家护甲有效率乘区（黑雾，&lt; 1 = 护甲减伤缩水）。</summary>
        public float playerArmorEffectiveMult = 1f;
        /// <summary>本局不能主动撤退（决绝）。</summary>
        public bool noRetreat;
        /// <summary>选了几条契约。</summary>
        public int count;

        public bool IsEmpty => count <= 0;

        /// <summary>按 8 条档位聚合；不合法档位直接忽略。</summary>
        public static ContractMods Build(int[] tiers)
        {
            int n = ContractCatalog.CountSelected(tiers);
            if (n <= 0) return null;

            var mods = new ContractMods { count = n };
            for (int i = 0; i < tiers.Length && i < ContractCatalog.All.Length; i++)
            {
                int tier = tiers[i];
                ContractDef def = ContractCatalog.All[i];
                if (tier <= 0 || tier > def.TierCount) continue;
                float v = def.values[tier - 1];

                switch (def.kind)
                {
                    case ContractKind.Frenzy: mods.enemyAttackMult *= 1f + v; break;
                    case ContractKind.Swift: mods.enemyAttackSpeedMult *= 1f + v; break;
                    case ContractKind.Hardened: mods.monsterDamageTakenMult *= 1f - v; break;
                    // 群袭：整层怪物按更深的层数生成。只「多刷几只怪」在这套引擎里几乎免费
                    // （玩家有吸血续航，决定生死的是属性），所以难度必须落在怪物的数值上。
                    case ContractKind.Swarm: mods.clearFloorBonus += Mathf.RoundToInt(v); break;
                    case ContractKind.Drain: mods.healMult *= 1f - v; break;
                    case ContractKind.LastStand: mods.maxHpMult *= 1f - v; break;
                    case ContractKind.BlackFog: mods.playerArmorEffectiveMult *= 1f - v; break;
                    case ContractKind.NoRetreat: mods.noRetreat = true; break;
                }
            }

            mods.healMult = Mathf.Clamp01(mods.healMult);
            mods.maxHpMult = Mathf.Clamp(mods.maxHpMult, 0.1f, 1f);
            mods.count = n;
            return mods;
        }
    }
}

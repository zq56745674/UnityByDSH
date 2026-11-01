using System.Collections.Generic;
using UnityEngine;

namespace AutoBattle
{
    /// <summary>
    /// 技能推荐分。它不是「标准答案」，而是把战斗公式里已有的量
    /// （攻击、攻速、暴击率/暴伤、生命、防御）折算成两个 0~1 的系数：
    /// 输出增益与生存增益，再合成 35~99 的整数分，给新手一个比较起点。
    ///
    /// 技能 2.0 起只对「本流派 + 通用」给出正常分数；另一流派的专属技能返回低分，
    /// UI 会把它灰显。分数只用于排序与提示，不参与任何战斗计算。
    /// </summary>
    public static class SkillAdvice
    {
        /// <summary>0~99 的推荐分，越高越契合当前角色面板。</summary>
        public static int Score(PlayerData player, SkillId id)
        {
            if (player == null) return 50;

            SkillDef def = SkillCatalog.Get(id);
            if (def == null) return 50;
            if (!SkillCatalog.IsAvailableTo(player.doctrine, id)) return 30;

            float interval = Mathf.Max(0.25f, BattleSystem.PlayerAttackInterval(player));
            float critMult = Mathf.Max(1f, 1f + player.CritChance * (player.CritDamage - 1f));
            float defense = Mathf.Max(1f, player.Defense);

            float offense = 0f; // 相对 DPS 增益
            float survival = 0f; // 相对有效生命/续航增益

            switch (id)
            {
                case SkillId.ThunderRush:
                    offense = def.value * interval / Mathf.Max(0.5f, def.cooldown)
                              + def.value3 * def.duration / Mathf.Max(0.5f, def.cooldown);
                    break;

                case SkillId.ThunderGod:
                    // 按平均 4 层雷印估算；满层爆发属于上限，不计入期望。
                    offense = (def.value + 4f * def.value2) * interval / Mathf.Max(0.5f, def.cooldown);
                    break;

                case SkillId.MountainSlam:
                    // 按平均 2 层岩层估算，再计入破甲带来的后续收益。
                    offense = (def.value + 2f * def.value2) * interval / Mathf.Max(0.5f, def.cooldown) * 1.15f;
                    break;

                case SkillId.Bulwark:
                    survival = def.value * 1.4f;
                    offense = def.value2 * def.duration2 / Mathf.Max(0.5f, def.cooldown) * 0.5f;
                    break;

                case SkillId.WarCry:
                    offense = def.value * def.duration / Mathf.Max(0.5f, def.cooldown);
                    survival = def.value2 * def.duration / Mathf.Max(0.5f, def.cooldown);
                    break;

                case SkillId.StaticResonance:
                    offense = (def.critBonus * (player.CritDamage - 1f) + player.OverflowCritDamage) / critMult;
                    break;

                case SkillId.StormHaste:
                    offense = def.value + def.value2 * 3f;
                    break;

                case SkillId.MountainBody:
                    survival = 0.5f * (def.value * player.BaseDefense) / defense;
                    offense = Mathf.Min(player.EquipmentDefense * def.value2, player.AttackBeforeMountain * def.value3)
                              / Mathf.Max(1f, player.AttackBeforeMountain);
                    break;

                case SkillId.HeavyImpact:
                    offense = def.value * (player.CritDamage - 1f) / critMult
                              + def.value3 * 0.55f
                              + def.value2; // 攻速为负，直接计入
                    break;

                case SkillId.Vampire:
                    survival = def.value * 0.6f;
                    break;

                case SkillId.Arc:
                    offense = player.CritChance * def.triggerChance * def.value * interval;
                    break;

                case SkillId.ThunderPunish:
                    offense = 0.25f * def.value * interval;
                    break;

                case SkillId.Aftershock:
                    offense = def.triggerChance * def.value * 0.45f;
                    break;

                case SkillId.RockArmor:
                    survival = def.triggerChance * def.value * 2.2f;
                    break;

                case SkillId.LastStand:
                    survival = 0.22f + Mathf.Min(0.12f, player.MaxHp / 200000f);
                    break;
            }

            float score = 55f + offense * 120f + survival * 70f;
            return Mathf.Clamp(Mathf.RoundToInt(score), 35, 99);
        }

        /// <summary>一句话评价，贴在推荐分后面。</summary>
        public static string Tip(SkillId id)
        {
            switch (id)
            {
                case SkillId.ThunderRush: return "叠印起手";
                case SkillId.ThunderGod: return "满印核弹";
                case SkillId.MountainSlam: return "破甲重击";
                case SkillId.Bulwark: return "护盾增伤";
                case SkillId.WarCry: return "通用爆发";
                case SkillId.StaticResonance: return "暴击转化";
                case SkillId.StormHaste: return "越暴越快";
                case SkillId.MountainBody: return "越肉越痛";
                case SkillId.HeavyImpact: return "低频重击";
                case SkillId.Vampire: return "战斗续航";
                case SkillId.Arc: return "暴击补刀";
                case SkillId.ThunderPunish: return "闪电收官";
                case SkillId.Aftershock: return "崩山回声";
                case SkillId.RockArmor: return "受击护盾";
                case SkillId.LastStand: return "免死容错";
                default: return string.Empty;
            }
        }

        /// <summary>分档标签：用于卡片上的颜色语义。</summary>
        public static string TierLabel(int score)
        {
            if (score >= 80) return "核心";
            if (score >= 66) return "好用";
            if (score >= 52) return "一般";
            return "凑数";
        }

        public static Color TierColor(int score)
        {
            if (score >= 80) return GameConfig.LogSuccess;
            if (score >= 66) return GameConfig.TextInfo;
            if (score >= 52) return GameConfig.TextMuted;
            return GameConfig.TextDisabled;
        }

        /// <summary>在候选列表中找推荐分最高的一项；并列取靠前的，空列表返回 -1。</summary>
        public static int BestIndex(PlayerData player, IReadOnlyList<SkillId> ids)
        {
            if (ids == null || ids.Count == 0) return -1;
            int best = 0;
            int bestScore = Score(player, ids[0]);
            for (int i = 1; i < ids.Count; i++)
            {
                int s = Score(player, ids[i]);
                if (s > bestScore)
                {
                    best = i;
                    bestScore = s;
                }
            }
            return best;
        }

        /// <summary>卡片上的短文本：推荐 78 · 核心。</summary>
        public static string ShortTag(PlayerData player, SkillId id)
        {
            int score = Score(player, id);
            return $"推荐 {score} · {TierLabel(score)}";
        }
    }
}

using System.Collections.Generic;
using UnityEngine;

namespace AutoBattle
{
    /// <summary>
    /// 技能推荐分。它不是「标准答案」，而是把战斗公式里已有的量
    /// （攻击、攻速、暴击率/暴伤、生命、防御）折算成两个 0~1 的系数：
    /// 输出增益与生存增益，再合成 35~99 的整数分，给新手一个比较起点。
    ///
    /// 主动技能按「每次冷却能多打多少」估算：命中数 × 倍率 × 攻击间隔 ÷ 冷却，
    /// 就是相对普通攻击的每秒伤害提升比例。斩杀因为只在低血线释放，乘 0.55 折价。
    /// 被动/触发不经过战斗循环，直接按属性公式估算相对增益。
    ///
    /// 分数只用于排序与提示，不参与任何战斗计算；调数值不会改变战斗结果。
    /// </summary>
    public static class SkillAdvice
    {
        /// <summary>0~99 的推荐分，越高越契合当前角色面板。</summary>
        public static int Score(PlayerData player, SkillId id)
        {
            if (player == null) return 50;

            SkillDef def = SkillCatalog.Get(id);
            if (def == null) return 50;

            float interval = Mathf.Max(0.25f, BattleSystem.PlayerAttackInterval(player));
            float critMult = Mathf.Max(1f, 1f + player.CritChance * (player.CritDamage - 1f));
            float defense = Mathf.Max(1f, player.Defense);

            float offense = 0f; // 相对 DPS 增益
            float survival = 0f; // 相对有效生命/续航增益

            switch (id)
            {
                case SkillId.HeavyStrike:
                    offense = def.hits * def.value * interval / Mathf.Max(0.5f, def.cooldown);
                    break;

                case SkillId.Whirlwind:
                    offense = def.hits * def.value * interval / Mathf.Max(0.5f, def.cooldown);
                    break;

                case SkillId.Berserk:
                    // 持续/冷却 × (攻击加成 + 暴击率加成换算的暴击收益)
                    offense = def.duration / Mathf.Max(0.5f, def.cooldown)
                              * (def.value + def.critBonus * (player.CritDamage - 1f));
                    break;

                case SkillId.Execute:
                    // 处决是最强单体倍率，但只在怪物残血时释放，按 55% 折价。
                    offense = def.hits * def.value * interval / Mathf.Max(0.5f, def.cooldown) * 0.55f;
                    break;

                case SkillId.IronBody:
                    // 防御加成相对当前防御的比例；有效生命近似线性，再折半。
                    survival = 0.5f * (def.value * player.BaseDefense) / defense;
                    break;

                case SkillId.BerserkHeart:
                    offense = def.value * (player.CritDamage - 1f) / critMult;
                    break;

                case SkillId.LethalStrike:
                    offense = player.CritChance * (player.CritDamage * def.value) / critMult;
                    break;

                case SkillId.Vampire:
                    survival = def.value * 0.6f;
                    break;

                case SkillId.Swiftness:
                    offense = def.value;
                    break;

                case SkillId.GiantSlayer:
                    // 只对精英/Boss 生效，按战斗中约 1/4 的占比折算。
                    offense = def.value * 0.25f;
                    break;

                case SkillId.Undying:
                    survival = def.value * 0.5f;
                    break;

                case SkillId.Counter:
                    survival = def.triggerChance * def.value * 0.4f;
                    break;

                case SkillId.CritChain:
                    offense = player.CritChance * def.triggerChance * def.value * interval;
                    break;

                case SkillId.NearDeathBurst:
                    offense = def.value * def.duration / Mathf.Max(0.5f, def.cooldown);
                    survival = 0.15f;
                    break;

                case SkillId.ShieldGen:
                    // 每 0.5 秒给 10% 最大生命的护盾，是稳定的减伤而非爆发。
                    survival = def.value * 1.6f;
                    break;
            }

            float score = 45f + offense * 140f + survival * 70f;
            return Mathf.Clamp(Mathf.RoundToInt(score), 35, 99);
        }

        /// <summary>一句话评价，贴在推荐分后面。</summary>
        public static string Tip(SkillId id)
        {
            switch (id)
            {
                case SkillId.HeavyStrike: return "稳定单体爆发";
                case SkillId.Whirlwind: return "多段清场";
                case SkillId.Berserk: return "长线增伤";
                case SkillId.Execute: return "终局斩杀";
                case SkillId.IronBody: return "前排耐力";
                case SkillId.BerserkHeart: return "暴击流核心";
                case SkillId.LethalStrike: return "暴伤流核心";
                case SkillId.Vampire: return "战斗续航";
                case SkillId.Swiftness: return "攻速流核心";
                case SkillId.GiantSlayer: return "精英/Boss 特攻";
                case SkillId.Undying: return "低血保命";
                case SkillId.Counter: return "受击反伤";
                case SkillId.CritChain: return "暴击联动";
                case SkillId.NearDeathBurst: return "残血反打";
                case SkillId.ShieldGen: return "护盾续航";
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

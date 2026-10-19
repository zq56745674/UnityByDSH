using UnityEngine;

namespace AutoBattle
{
    /// <summary>
    /// 装备评分：算「换上这件装备后，对标准怪的期望 DPS + 生存折算」，而不是线性加权求和。
    ///
    /// 为什么换口径：实战 DPS ∝ 攻击 × 攻速 × (1 + 暴击率 × (暴击倍率 - 1))，是连乘关系；
    /// 原来的线性权重（暴击率 ×1000、暴击伤害 ×100 …）表达不了这种乘积，也不能反映
    /// 「装备属性要和角色当前属性一起算」——角色暴击伤害 717% 时 +50% 暴击率很值钱，
    /// 角色暴击伤害掉回 ~200% 时就廉价。实测 40 把随机传说武器：
    /// 旧权重与「木桩实测伤害」排序的 Spearman 相关 0.27（权重再怎么搜也只到 0.40），
    /// 换成期望 DPS 口径后 0.91。复现脚本见 Assets/Editor/LateGameProbe.cs 的
    /// ScoreAudit / ScoreRankAudit / ScoreDummyAudit / ScoreWeightSearch。
    /// </summary>
    public static class EquipmentScore
    {
        static MonsterData _standard;

        /// <summary>
        /// 标准化对手：固定一只 Lv.100 普通近战怪。评分必须落在同一个对手上，
        /// 否则减伤率会随对手等级漂移，不同装备的分数就没法互比。
        /// </summary>
        public static MonsterData Standard
        {
            get
            {
                if (_standard == null)
                {
                    _standard = MonsterData.CreateForLevel(
                        GameConfig.ScoreStandardLevel, MonsterType.Melee, MonsterTier.Normal, MonsterAffix.None);
                }
                return _standard;
            }
        }

        /// <summary>
        /// 装备评分 = 这件装备相对「同槽位空着」带来的增量：
        ///   Δ(对标准怪的期望 DPS) + Δ(有效生命) ÷ SurvivabilityDivisor。
        ///
        /// 用增量而不是「穿上后的总战力」：总战力里含着另一件装备和角色的全部属性，
        /// 武器、衣服会算出同一个数（都是「这个角色的战力」），既不显示也没法比。
        /// 增量口径下武器基本只吃 DPS 项、衣服基本只吃生存项，同槽位两件装备可以直接比大小
        /// （自动换装依赖这一点）。注意分数依赖角色当前属性，这是有意的——
        /// 角色暴伤 717% 时 +8% 暴击率的武器确实比角色暴伤 200% 时更值钱。
        /// </summary>
        public static float Of(PlayerData player, Equipment candidate, EquipmentSlot slot)
        {
            if (player == null || candidate == null) return 0f;
            if (slot != candidate.slot) return 0f;

            Equipment old = player.GetEquipment(slot);
            try
            {
                PutOn(player, slot, null);
                float bare = Total(player);
                PutOn(player, slot, candidate);
                return Total(player) - bare;
            }
            finally
            {
                // PutOn 直接改 player 的五个装备槽（不是拷贝），异常逃出去就等于永久丢装备，
                // 所以还原必须放在 finally 里。
                PutOn(player, slot, old);
            }
        }

        /// <summary>
        /// 生存折算系数：有效生命 ÷ 这个数之后才和期望 DPS 相加。
        /// 取 8 是照着当前数值平衡定的标——除以它之后，各槽位的增量评分大致同量级，
        /// 玩家跨槽位看评分不会误以为头盔/鞋「差一截」。它不影响同槽位比较的结果：
        /// 单个槽位的换装对 DPS 与有效生命的影响各自固定，所以这一项在同槽位内是常数。
        /// （具体量级别再往注释里抄数字：改平衡时它们必然漂移，看代码里的常量即可。）
        /// </summary>
        public const float SurvivabilityDivisor = 8f;

        /// <summary>角色当前的总战力：期望 DPS + 有效生命 ÷ SurvivabilityDivisor。</summary>
        static float Total(PlayerData p) => Dps(p, Standard) + EffectiveHp(p, Standard) / SurvivabilityDivisor;

        /// <summary>
        /// 期望 DPS：攻击 × (1 - 减伤) × (1 + 暴击率 × (暴击倍率 - 1)) ÷ 攻击间隔。
        /// 攻击间隔带每秒出手上限（GameConfig.MinAttackInterval），所以攻速在这个口径里会自然饱和。
        /// </summary>
        public static float Dps(PlayerData p, MonsterData m)
        {
            if (p == null || m == null) return 0f;
            float reduction = GameConfig.DamageReduction(m.defense, m.level, p.ArmorPenetration);
            float crit = Mathf.Min(p.CritChance, GameConfig.MaxCritChance);
            float perHit = p.Attack * (1f - reduction) * (1f + crit * (p.CritDamage - 1f));
            return perHit / BattleSystem.PlayerAttackInterval(p);
        }

        /// <summary>有效生命 = 最大生命 ÷ (1 - 自身减伤率)，把防御折算成「总共能扛住多少伤害」。</summary>
        public static float EffectiveHp(PlayerData p, MonsterData m)
        {
            if (p == null) return 0f;
            float reduction = Mathf.Clamp(GameConfig.DamageReduction(p.Defense, p.level, 0f), 0f, 0.95f);
            return p.MaxHp / (1f - reduction);
        }

        /// <summary>PlayerData.SetEquipment 会忽略 null，但评分要能还原「原本空槽」的状态。</summary>
        static void PutOn(PlayerData player, EquipmentSlot slot, Equipment e)
        {
            switch (slot)
            {
                case EquipmentSlot.Weapon: player.weapon = e; break;
                case EquipmentSlot.Armor: player.armor = e; break;
                case EquipmentSlot.Helmet: player.helmet = e; break;
                case EquipmentSlot.Boots: player.boots = e; break;
                case EquipmentSlot.Accessory: player.accessory = e; break;
            }
        }
    }
}

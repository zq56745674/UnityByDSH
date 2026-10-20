using UnityEngine;

namespace AutoBattle
{
    /// <summary>
    /// 掉落系统（要求 11、12、13）：击杀后按概率掉装备，品质按权重抽取，
    /// 属性由「装备基础值 × 等级成长 × 品质倍率」计算得出。
    /// </summary>
    public static class LootSystem
    {
        /// <summary>
        /// 是否掉落装备：基础 30%，每多解锁一个槽位 +5%（4 槽 40%、5 槽 45%）。
        /// bonusChance 来自转生「财运」（+2% / 级，加算）。旧调用方不传参时行为不变。
        /// </summary>
        public static bool RollDrop(int unlockedSlots = 2, float bonusChance = 0f)
        {
            int extra = Mathf.Max(0, unlockedSlots - 2);
            float chance = GameConfig.DropChance + GameConfig.DropChancePerExtraSlot * extra
                + (float.IsNaN(bonusChance) ? 0f : bonusChance);
            return Random.value < Mathf.Clamp01(chance);
        }

        /// <summary>高品质 = 稀有及以上；精英/Boss 的爆率加成只作用于这三档。</summary>
        public static bool IsHighQuality(EquipmentQuality quality) => quality >= EquipmentQuality.Rare;

        /// <summary>
        /// 按权重抽取品质（要求 12：62.5/25/9/3/0.5）。
        /// highQualityBonus 把稀有及以上的权重整体放大（精英 +10%、Boss +20%）后重新归一化，
        /// 所以普通/优秀的实际占比会相应下降。
        /// </summary>
        public static EquipmentQuality RollQuality(float highQualityBonus = 0f)
        {
            // 爆率加成来自配置或探针，NaN / 负数都不该把整张权重表搅成负值。
            float boosted = 1f + highQualityBonus;
            if (float.IsNaN(boosted) || float.IsInfinity(boosted) || boosted < 0f) boosted = 1f;

            float total = 0f;
            for (int i = 0; i < GameConfig.QualityWeights.Length; i++) total += Weight(i, boosted);

            if (total <= 0f || float.IsNaN(total)) return EquipmentQuality.Common;

            float roll = Random.value * total;
            float cumulative = 0f;

            for (int i = 0; i < GameConfig.QualityWeights.Length; i++)
            {
                cumulative += Weight(i, boosted);
                if (roll < cumulative) return (EquipmentQuality)i;
            }

            // 浮点累加误差兜底。
            return (EquipmentQuality)Mathf.Clamp(
                GameConfig.QualityWeights.Length - 1, 0, GameConfig.QualityNames.Length - 1);
        }

        /// <summary>权重只接受有限非负值；其它情况按 0 处理，避免整次抽取落到非法档位。</summary>
        private static float SanitizeWeight(float w)
        {
            return float.IsNaN(w) || float.IsInfinity(w) || w <= 0f ? 0f : w;
        }

        /// <summary>取第 index 档品质的权重，高品质档乘上爆率加成。</summary>
        private static float Weight(int index, float boosted)
        {
            float w = SanitizeWeight(GameConfig.QualityWeights[index]);
            return IsHighQuality((EquipmentQuality)index) ? w * boosted : w;
        }

        /// <summary>
        /// 随机槽位并生成一件装备。player 不为空时只在已解锁槽位里抽，
        /// 并且有 SmartDropChance 的概率把这一件硬塞给「空槽 / 当前评分最低的槽」，
        /// 避免五槽时代玩家永远刷不到缺的那一件。
        /// </summary>
        public static Equipment CreateRandomEquipment(int playerLevel, float highQualityBonus = 0f,
            PlayerData player = null)
        {
            EquipmentSlot slot = RollSlot(player);
            return CreateEquipment(slot, playerLevel, RollQuality(highQualityBonus));
        }

        /// <summary>抽一个掉落槽位：智能掉落优先空槽，其次当前评分最低的槽。</summary>
        private static EquipmentSlot RollSlot(PlayerData player)
        {
            if (player == null) return EquipmentSlots.All[Random.Range(0, EquipmentSlots.Count)];

            var unlocked = new System.Collections.Generic.List<EquipmentSlot>(EquipmentSlots.Count);
            for (int i = 0; i < EquipmentSlots.Count; i++)
            {
                EquipmentSlot s = EquipmentSlots.All[i];
                if (player.IsEquipmentSlotUnlocked(s)) unlocked.Add(s);
            }
            if (unlocked.Count == 0) return EquipmentSlot.Weapon;
            if (unlocked.Count <= 2 || Random.value >= GameConfig.SmartDropChance)
                return unlocked[Random.Range(0, unlocked.Count)];

            // 空槽优先：设计目标是「缺哪件补哪件」，不是按最低分去堆已经很强的那件。
            var empty = new System.Collections.Generic.List<EquipmentSlot>();
            for (int i = 0; i < unlocked.Count; i++)
                if (player.GetEquipment(unlocked[i]) == null) empty.Add(unlocked[i]);
            if (empty.Count > 0) return empty[Random.Range(0, empty.Count)];

            EquipmentSlot weakest = unlocked[0];
            float weakestScore = float.MaxValue;
            for (int i = 0; i < unlocked.Count; i++)
            {
                EquipmentSlot s = unlocked[i];
                Equipment cur = player.GetEquipment(s);
                float score = cur == null ? 0f : EquipmentScore.Of(player, cur, s);
                if (score < weakestScore)
                {
                    weakestScore = score;
                    weakest = s;
                }
            }
            return weakest;
        }

        /// <summary>
        /// 按给定权重抽取品质（数组按 EquipmentQuality 顺序给出，允许某几档为 0）。
        /// 精英 / Boss 的保底掉落用它：精英「稀有 70 / 史诗 25 / 传说 5」、Boss「史诗 80 / 传说 20」。
        /// </summary>
        public static EquipmentQuality RollQualityWithWeights(float[] weights)
        {
            // 调用方来自保底掉落表；空表 / 全 0 / 负权重时不能返回 -1 或越界品质，
            // 否则后面的 GameConfig.RollQualityMultiplier 会直接索引坏档。
            int count = weights == null ? 0 : Mathf.Min(weights.Length, GameConfig.QualityNames.Length);
            float total = 0f;
            for (int i = 0; i < count; i++) total += SanitizeWeight(weights[i]);

            if (total <= 0f || float.IsNaN(total)) return EquipmentQuality.Common;

            float roll = Random.value * total;
            float cumulative = 0f;

            for (int i = 0; i < count; i++)
            {
                cumulative += SanitizeWeight(weights[i]);
                if (roll < cumulative) return (EquipmentQuality)i;
            }

            return (EquipmentQuality)Mathf.Clamp(count - 1, 0, GameConfig.QualityNames.Length - 1);
        }

        /// <summary>按指定等级与品质权重生成一件装备（精英 / Boss 的保底掉落，等级取怪物自身等级）。</summary>
        public static Equipment CreateEquipmentAtLevel(int level, float[] qualityWeights, PlayerData player = null)
        {
            EquipmentSlot slot = RollSlot(player);
            return CreateEquipment(slot, level, RollQualityWithWeights(qualityWeights));
        }

        /// <summary>
        /// 生成指定槽位/品质的装备（要求 13）。
        /// 武器加攻击与暴击率/暴击伤害；衣服加防御和生命，**不加暴击**（要求 10）；
        /// 头盔加生命与防御（小衣服），鞋加速度与攻速（速度不吃随机倍率），
        /// 饰品加暴击伤害与护甲穿透。小件副词条数值减半且永久排除暴击率。
        /// 属性倍率每次生成时在品质区间内随机抽取，所以同品质的装备也有好坏之分。
        /// </summary>
        public static Equipment CreateEquipment(EquipmentSlot slot, int level, EquipmentQuality quality)
        {
            // 公开入口也当边界处理：直接构造的装备/探针可能传 0 级或越界品质，
            // 在这里夹好，后面的品质表与等级成长就都不用再各写一份兜底。
            level = Mathf.Max(1, level);
            quality = GameConfig.ClampQuality(quality);
            int steps = level - 1;
            float multiplier = GameConfig.RollQualityMultiplier(quality);

            var e = new Equipment
            {
                slot = slot,
                quality = quality,
                level = level,
                qualityMultiplier = multiplier,
                affixScale = EquipmentSlots.IsMinor(slot) ? GameConfig.MinorAffixScale : 1f,
            };

            switch (slot)
            {
                case EquipmentSlot.Weapon:
                {
                    float atk = GameConfig.WeaponBaseAttack + GameConfig.WeaponAttackPerLevel * steps;
                    e.attack = Mathf.Max(1, Mathf.RoundToInt(atk * multiplier));

                    // 主词条除攻击外，再随机给「暴击率」或「暴击伤害」其中一个（各 50%，不会同时出现）。
                    // 数值沿用原公式：基础 + 每级成长，再乘品质倍率。
                    float critChance = (GameConfig.WeaponBaseCritChance + GameConfig.WeaponCritChancePerLevel * steps) * multiplier;
                    float critDamage = (GameConfig.WeaponBaseCritDamage + GameConfig.WeaponCritDamagePerLevel * steps) * multiplier;

                    if (Random.value < 0.5f) e.critChance = critChance;
                    else e.critDamage = critDamage;
                    break;
                }
                case EquipmentSlot.Armor:
                {
                    float def = GameConfig.ArmorBaseDefense + GameConfig.ArmorDefensePerLevel * steps;
                    float hp = GameConfig.ArmorBaseHp + GameConfig.ArmorHpPerLevel * steps;
                    e.defense = Mathf.Max(1, Mathf.RoundToInt(def * multiplier));
                    e.hp = Mathf.Max(1, Mathf.RoundToInt(hp * multiplier));
                    break;
                }
                case EquipmentSlot.Helmet:
                {
                    // 头盔是「小衣服」：生命 25%、防御 12% 的衣服数值，定位有效生命。
                    float def = (GameConfig.ArmorBaseDefense + GameConfig.ArmorDefensePerLevel * steps)
                              * GameConfig.HelmetDefenseFactor;
                    float hp = (GameConfig.ArmorBaseHp + GameConfig.ArmorHpPerLevel * steps)
                             * GameConfig.HelmetHpFactor;
                    e.defense = Mathf.Max(1, Mathf.RoundToInt(def * multiplier));
                    e.hp = Mathf.Max(1, Mathf.RoundToInt(hp * multiplier));
                    break;
                }
                case EquipmentSlot.Boots:
                {
                    // 速度按品质档位加，不乘随机倍率（否则传说鞋能凭空 +35 速度）。
                    int q = (int)quality;
                    e.speed = Mathf.Max(0, Mathf.RoundToInt(
                        GameConfig.BootsBaseSpeed + GameConfig.BootsSpeedPerLevel * steps
                        + GameConfig.BootsSpeedPerQuality * q));
                    e.haste = (GameConfig.BootsBaseHaste + GameConfig.BootsHastePerLevel * steps)
                            * (1f + GameConfig.BootsHastePerQuality * q);
                    break;
                }
                case EquipmentSlot.Accessory:
                {
                    e.critDamage = (GameConfig.AccessoryBaseCritDamage
                                  + GameConfig.AccessoryCritDamagePerLevel * steps) * multiplier;
                    e.armorPen = (GameConfig.AccessoryBaseArmorPen
                                + GameConfig.AccessoryArmorPenPerLevel * steps) * multiplier;
                    break;
                }
            }

            RollAffixes(e, level);
            return e;
        }

        /// <summary>
        /// 按品质给装备挂副词条：条数在 EquipAffixCountMin ~ Max 之间随机
        /// 大件按 EquipAffixCountMin~Max（普通 1 / 优秀 1~2 / 稀有 2 / 史诗 3 / 传说 4），
        /// 小件固定更少（普通 1 / 优秀 1~2 / 稀有以上 2），再按该条数抽内容。
        /// </summary>
        private static void RollAffixes(Equipment e, int level)
        {
            int q = (int)GameConfig.ClampQuality(e.quality);
            int count;
            if (EquipmentSlots.IsMinor(e.slot))
            {
                // 小件：普通 1 条、优秀 1~2 条、稀有以上 2 条。词条少而数值减半，
                // 五件套加起来才不会把暴击/攻速/穿甲全部顶满。
                count = q <= 0 ? 1 : q == 1 ? Random.Range(1, 3) : 2;
            }
            else
            {
                // Random.Range 的 int 重载上界不含，所以 +1 才能取到上限。
                count = Random.Range(GameConfig.EquipAffixCountMin[q], GameConfig.EquipAffixCountMax[q] + 1);
            }
            RollAffixes(e, level, count);
        }

        /// <summary>
        /// 按指定条数抽副词条：从词条池里洗牌抽取，所以同一件装备内不会重复；
        /// 数值 = 基础随机值 × (1 + 装备等级 × EquipAffixLevelScale)，**不乘品质倍率**。
        /// 掉落与洗练共用这一段，保证两者分布完全一致——洗练不能掷出比掉落更强的词条，
        /// 否则「洗练」就成了绕过掉落天花板的第二条成长轴。
        /// </summary>
        private static void RollAffixes(Equipment e, int level, int count, int excludedMask = 0)
        {
            if (count <= 0) return;

            int poolSize = GameConfig.EquipAffixNames.Length;
            bool minor = EquipmentSlots.IsMinor(e.slot);

            // 池子里先剔掉 excludedMask 上的种类（锁住的词条），再洗牌。
            // 小件永久排除暴击率：五槽都给暴击率会让暴击率溢出到 100% 以上，
            // 副词条池的其他项反而没人要。
            int[] pool = new int[poolSize];
            int available = 0;
            for (int i = 0; i < poolSize; i++)
            {
                if ((excludedMask & (1 << i)) != 0) continue;
                if (minor && i == (int)EquipmentAffix.CritChance) continue;
                pool[available++] = i;
            }
            if (available <= 0) return;

            // Fisher-Yates 洗牌后取前 count 个，天然不重复。
            for (int i = available - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                int tmp = pool[i];
                pool[i] = pool[j];
                pool[j] = tmp;
            }

            float levelScale = 1f + Mathf.Max(0, level) * GameConfig.EquipAffixLevelScale;

            for (int i = 0; i < count && i < available; i++)
            {
                int kind = pool[i];
                float baseValue = Random.Range(GameConfig.EquipAffixMin[kind], GameConfig.EquipAffixMax[kind]);
                // 小件数值按 affixScale 缩放；AffixQuality 会再除回来，质量百分位仍然可读。
                SetAffix(e, (EquipmentAffix)kind,
                    baseValue * levelScale * Mathf.Max(0.01f, e.affixScale));
            }
        }

        /// <summary>按词条种类写值（掉落、洗练、洗练前的清零共用一处 switch）。</summary>
        private static void SetAffix(Equipment e, EquipmentAffix kind, float value)
        {
            switch (kind)
            {
                case EquipmentAffix.CritChance: e.affixCritChance = value; break;
                case EquipmentAffix.CritDamage: e.affixCritDamage = value; break;
                case EquipmentAffix.AttackSpeed: e.affixAttackSpeed = value; break;
                case EquipmentAffix.AllStats: e.affixAllStats = value; break;
                case EquipmentAffix.ArmorPen: e.affixArmorPen = value; break;
            }
        }

        /// <summary>洗练：重抽全部副词条（不锁词条的旧口径，等价于 lockedMask = 0）。</summary>
        public static void ReforgeAffixes(Equipment e) => ReforgeAffixes(e, 0);

        /// <summary>
        /// 洗练：重抽**没被锁住**的副词条，锁住的那几条原样保留。
        /// 条数取当前实际条数（不是品质区间），所以优秀品质的 1~2 条不会被洗少；
        /// 主属性、武器主词条、品质、强化等级都不动——那些是强化轴与品质身份，不属于洗练。
        /// <paramref name="lockedMask"/> 是 EquipmentAffix 的位掩码（见 Equipment.AffixBit）；
        /// 只会清空「存在且未锁」的字段，所以全锁时这里什么都不做（调用方本该先拦住）。
        /// </summary>
        public static void ReforgeAffixes(Equipment e, int lockedMask)
        {
            if (e == null || !e.CanReforge) return;

            int present = e.PresentAffixMask;
            int locked = lockedMask & present;
            int freeCount = e.AffixCount - e.LockedAffixCount(locked);
            if (freeCount <= 0) return;

            for (int i = 0; i < GameConfig.EquipAffixNames.Length; i++)
            {
                int bit = 1 << i;
                if ((present & bit) == 0 || (locked & bit) != 0) continue;
                SetAffix(e, (EquipmentAffix)i, 0f);
            }

            RollAffixes(e, e.level, freeCount, locked);
        }
    }
}

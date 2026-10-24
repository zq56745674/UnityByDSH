using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace AutoBattle
{
    /// <summary>
    /// 装备槽位。前两个是旧存档就有的「大件」，后三个是 2026-10 新增的小件。
    /// 枚举值必须保持 Weapon=0 / Armor=1 不动，否则旧存档的 slot 会整体错位。
    /// </summary>
    public enum EquipmentSlot
    {
        Weapon = 0,
        Armor = 1,
        Helmet = 2,
        Boots = 3,
        Accessory = 4,
    }

    /// <summary>槽位的顺序 / 数量 / 分类，UI、存档与掉落共用同一份定义。</summary>
    public static class EquipmentSlots
    {
        public const int Count = 5;

        public static readonly EquipmentSlot[] All =
        {
            EquipmentSlot.Weapon,
            EquipmentSlot.Armor,
            EquipmentSlot.Helmet,
            EquipmentSlot.Boots,
            EquipmentSlot.Accessory,
        };

        /// <summary>小件（头盔 / 鞋 / 饰品）：副词条只有 2 条、数值按 MinorAffixScale 缩放。</summary>
        public static bool IsMinor(EquipmentSlot slot) =>
            slot == EquipmentSlot.Helmet || slot == EquipmentSlot.Boots || slot == EquipmentSlot.Accessory;

        public static string Name(EquipmentSlot slot)
        {
            switch (slot)
            {
                case EquipmentSlot.Weapon: return "武器";
                case EquipmentSlot.Armor: return "衣服";
                case EquipmentSlot.Helmet: return "头盔";
                case EquipmentSlot.Boots: return "鞋";
                case EquipmentSlot.Accessory: return "饰品";
                default: return "装备";
            }
        }
    }

    /// <summary>
    /// 强化一次的结果。做成枚举而不是 bool，是为了让 UI 能对「为什么没强化成功」
    /// 给出具体提示（金币不足和已达上限是两种完全不同的处境），
    /// 而不是笼统地不响应点击。
    /// </summary>
    public enum EnhanceResult
    {
        /// <summary>强化成功，金币已扣、等级已升。</summary>
        Success,

        /// <summary>该槽位没有装备。</summary>
        NoEquipment,

        /// <summary>已经强化到上限 <see cref="GameConfig.EnhanceMaxLevel"/>。</summary>
        MaxLevel,

        /// <summary>金币不够支付本次费用。</summary>
        NotEnoughGold,
    }

    /// <summary>
    /// 洗练一次的结果。与 <see cref="EnhanceResult"/> 同理：UI 要能区分
    /// 「没装备」「没有词条可洗」「金币不够」，而不是笼统地不响应点击。
    /// 洗练没有次数上限，所以没有 MaxLevel 一档。
    /// </summary>
    public enum ReforgeResult
    {
        /// <summary>洗练成功，金币已扣、副词条已重抽。</summary>
        Success,

        /// <summary>该槽位没有装备。</summary>
        NoEquipment,

        /// <summary>装备没有任何副词条（理论上不会发生，普通品质也保底 1 条）。</summary>
        NoAffix,

        /// <summary>金币不够支付本次费用。</summary>
        NotEnoughGold,

        /// <summary>全部词条都被锁住了——没有可重抽的词条，拒绝扣钱。</summary>
        AllLocked,
    }

    /// <summary>装备品质，顺序必须与 GameConfig 里的权重/倍率/名称数组一致（要求 12）。</summary>
    public enum EquipmentQuality
    {
        Common,     // 普通 62.5%
        Uncommon,   // 优秀 25%
        Rare,       // 稀有 9%
        Epic,       // 史诗 3%
        Legendary,  // 传说 0.5%
    }

    /// <summary>
    /// 装备副词条类型。顺序必须与 GameConfig.EquipAffixNames / EquipAffixMin / EquipAffixMax 一致。
    /// 同一件装备内不会出现重复词条（见 LootSystem.CreateEquipment 的洗牌抽签）。
    /// </summary>
    public enum EquipmentAffix
    {
        /// <summary>暴击率：基础 1% ~ 8%。</summary>
        CritChance,

        /// <summary>暴击伤害：数值范围见 GameConfig.EquipAffixMin / EquipAffixMax 的第 2 项。</summary>
        CritDamage,

        /// <summary>攻击速度：基础 2% ~ 10%，按比例缩短出手间隔。</summary>
        AttackSpeed,

        /// <summary>全属性：基础 2% ~ 8%，按比例同时加成攻击、防御、生命。</summary>
        AllStats,

        /// <summary>护甲穿透：基础 2% ~ 10%，按比例削减目标防御的有效值（见 BattleSystem.ApplyDefense）。</summary>
        ArmorPen,
    }

    /// <summary>
    /// 怪物类型（要求 3）。顺序必须与 GameConfig 里的权重、系数、名称数组一致。
    /// <para>
    /// 这里只定义面板风格，具体显示名按章节取自 GameConfig.ChapterMonsterNames——
    /// 同一类是「哥布林战士」还是「骷髅卫士」，取决于它在第几章。
    /// </para>
    /// </summary>
    public enum MonsterType
    {
        /// <summary>近战：高生命高防御，攻击低、速度慢；非常耐打但输出低，基础怪。</summary>
        Melee,

        /// <summary>远程：低生命低防御，攻击高、速度快；爆发高但很脆。</summary>
        Ranged,

        /// <summary>暴击怪：生存中庸、普攻一般，但暴击率与暴击伤害极高，难度最高。</summary>
        Crit,
    }

    /// <summary>
    /// 怪物档次。顺序必须与 GameConfig 里的档次倍率、前缀、掉落数组一致。
    /// </summary>
    public enum MonsterTier
    {
        /// <summary>普通怪：参与击杀计数。</summary>
        Normal,

        /// <summary>精英怪：全属性 ×1.5，经验 ×2，必掉 1 件装备。</summary>
        Elite,

        /// <summary>Boss：属性倍率见 GameConfig.MonsterTierStatFactor 末项，经验倍率见 MonsterTierExpFactor 末项，必掉 2 件装备。</summary>
        Boss,
    }

    /// <summary>
    /// 怪物词缀。顺序必须与 GameConfig.AffixNames / AffixStatFactors 一致。
    /// </summary>
    public enum MonsterAffix
    {
        /// <summary>无词缀。</summary>
        None,

        /// <summary>狂暴：攻击 +30%，防御 -20%。</summary>
        Berserk,

        /// <summary>坚甲：防御 +50%，速度 -20%。</summary>
        Armored,

        /// <summary>迅捷：速度 +40%，生命 -20%。</summary>
        Swift,

        /// <summary>吸血：造成伤害的 30% 转化为自身生命。</summary>
        Lifesteal,

        /// <summary>护盾：开场获得 30% 最大生命的护盾。</summary>
        Shield,

        /// <summary>剧毒：每次攻击附加目标最大生命 2% 的伤害。</summary>
        Poison,
    }

    /// <summary>
    /// 一件装备。武器带攻击、暴击率、暴击伤害；衣服带防御和生命（要求 10）。
    /// 属性在生成时算好并固化，之后不再随等级变化，便于存档与比较。
    /// </summary>
    [Serializable]
    public class Equipment
    {
        public EquipmentSlot slot;
        public EquipmentQuality quality;

        /// <summary>掉落时主角的等级，用于展示与属性溯源。</summary>
        public int level;

        /// <summary>
        /// 生成时抽到的品质浮动倍率（取自 GameConfig.QualityMultiplierMin / Max）。
        /// 只用于展示「这件装备在同品质里偏强还是偏弱」，属性本身已固化，不再重复相乘。
        /// </summary>
        public float qualityMultiplier = 1f;

        public int attack;
        public int defense;
        public int hp;

        /// <summary>鞋的主属性：速度（只加在角色基础速度上，不乘全属性）。</summary>
        public int speed;

        /// <summary>鞋的主属性：攻击速度加成（0.035 表示 +3.5%）。</summary>
        public float haste;

        /// <summary>饰品的主属性：护甲穿透（0.064 表示无视目标 6.4% 防御）。</summary>
        public float armorPen;

        /// <summary>
        /// 副词条数值缩放。大件 1.0；头盔/鞋/饰品这些「小件」是 0.5——
        /// 五件套如果都给满额词条，暴击率、攻速、穿甲会全部溢出。
        /// 旧存档没有这个字段，JsonUtility 会给 0，FixupEquipment 会兜底成 1。
        /// </summary>
        public float affixScale = 1f;

        /// <summary>
        /// 强化等级，0 ~ <see cref="GameConfig.EnhanceMaxLevel"/>。
        /// 强化**只提升主属性**（武器攻击 / 衣服防御与生命，见 <see cref="EnhancedAttack"/>
        /// 等），既不碰下面这两个武器主词条（暴击率/暴击伤害），也不碰任何副词条。
        /// 这样暴击流不会因为「有钱就能无限堆主属性」而失控，评分与自动换装的
        /// 比较口径也保持稳定。
        /// </summary>
        public int enhanceLevel;

        /// <summary>
        /// 武器主词条之一：暴击率加成（0.05 表示 +5%）。
        /// 武器主词条是「攻击 + 暴击率/暴击伤害随机一个」，所以这件装备上它和
        /// <see cref="critDamage"/> 只会有一个非 0。衣服恒为 0。
        /// </summary>
        public float critChance;

        /// <summary>武器主词条之一：暴击伤害加成（0.25 表示 +25%，叠加在 150% 基准之上）。同上。</summary>
        public float critDamage;

        // ---- 副词条（0 表示这件装备没有该词条；条数由品质决定，同一件内不重复）----

        /// <summary>副词条：暴击率加成。</summary>
        public float affixCritChance;

        /// <summary>副词条：暴击伤害加成。</summary>
        public float affixCritDamage;

        /// <summary>副词条：攻击速度加成（按比例缩短出手间隔）。</summary>
        public float affixAttackSpeed;

        /// <summary>副词条：全属性加成（按比例同时加成攻击、防御、生命）。</summary>
        public float affixAllStats;

        /// <summary>副词条：护甲穿透（0.1 表示无视目标 10% 防御）。</summary>
        public float affixArmorPen;

        /// <summary>
        /// 已经洗练过几次。只影响费用（每次 +60%）与展示，不参与评分与战斗计算。
        /// 步骤 3 新增字段：旧存档里没有它，JsonUtility 会给 0，等价于「没洗过」，
        /// 所以不需要升存档版本。
        /// </summary>
        public int reforgeCount;

        /// <summary>
        /// 玩家手动上锁。锁住的装备不会被更高评分的掉落自动替换，
        /// 掉落会转进「待选」由玩家在对比面板决定；换档/重开时只有这件装备本身在存档里，
        /// 所以锁也随装备一起持久化。旧存档没有这个字段，JsonUtility 给 false。
        /// </summary>
        public bool locked;

        // 读档会兜底，但运行时仍可能有越界的 quality（测试直接构造），展示层再夹一次。
        public string QualityName => GameConfig.QualityNames[Mathf.Clamp((int)quality, 0, GameConfig.QualityNames.Length - 1)];

        public Color QualityColor => GameConfig.QualityColors[Mathf.Clamp((int)quality, 0, GameConfig.QualityColors.Length - 1)];

        public string SlotName => EquipmentSlots.Name(slot);

        /// <summary>形如「倍率 x2.34」，用来展示同品质内的强弱浮动。</summary>
        public string MultiplierText => $"倍率 x{qualityMultiplier:0.00}";

        // ---- 强化（步骤 2）：只作用于主属性 ----

        /// <summary>
        /// 强化倍率 = 1 + 每级加成 × 强化等级。0 级时为 1（完全不改变原属性）。
        /// 乘算而非加算：同品质两件装备的强弱差距在强化后不会被绝对值稀释掉。
        /// </summary>
        public float EnhanceMultiplier => EnhanceMultiplierAt(enhanceLevel);

        /// <summary>指定强化等级下的倍率；面板用它预览「下一级」的属性。</summary>
        public float EnhanceMultiplierAt(int level) =>
            1f + GameConfig.EnhanceBonusPerLevel * Mathf.Max(0, level);

        /// <summary>强化后的攻击（武器主属性）。未强化时等于 <see cref="attack"/>。</summary>
        public int EnhancedAttack => Mathf.RoundToInt(attack * EnhanceMultiplier);

        /// <summary>强化后的防御（衣服主属性）。未强化时等于 <see cref="defense"/>。</summary>
        public int EnhancedDefense => Mathf.RoundToInt(defense * EnhanceMultiplier);

        /// <summary>强化后的生命（衣服 / 头盔主属性）。未强化时等于 <see cref="hp"/>。</summary>
        public int EnhancedHp => Mathf.RoundToInt(hp * EnhanceMultiplier);

        /// <summary>强化后的速度（鞋主属性）。</summary>
        public int EnhancedSpeed => Mathf.RoundToInt(speed * EnhanceMultiplier);

        /// <summary>强化后的攻击速度加成（鞋主属性）。</summary>
        public float EnhancedHaste => haste * EnhanceMultiplier;

        /// <summary>强化后的护甲穿透（饰品主属性）。</summary>
        public float EnhancedArmorPen => armorPen * EnhanceMultiplier;

        /// <summary>
        /// 强化后的暴击伤害。只有饰品的暴伤吃强化；武器主词条按设计保持不强化，
        /// 所以调用方要按槽位区分，别把它当成通用的暴伤读取口。
        /// </summary>
        public float EnhancedCritDamage => critDamage * EnhanceMultiplier;

        /// <summary>是否还能继续强化。</summary>
        public bool CanEnhance => enhanceLevel < GameConfig.EnhanceMaxLevel;

        /// <summary>把当前等级再强化一级所需的金币；已满级返回 0。</summary>
        public int EnhanceCost => CanEnhance ? GameConfig.EnhanceCost(enhanceLevel, quality) : 0;

        /// <summary>形如「+3」的强化后缀；未强化时返回空串，便于直接拼进标题。</summary>
        public string EnhanceText => enhanceLevel > 0 ? $" +{enhanceLevel}" : string.Empty;

        /// <summary>强化后的主属性展示（形如「攻击 12 → 14」）；未强化时只给当前值。</summary>
        public string EnhanceGainText => EnhanceGainTextAt(enhanceLevel);

        /// <summary>
        /// 指定强化等级下的主属性展示。+0 只给当前值，之后都给「原值 → 该等级值」；
        /// 强化面板用它同时显示当前与下一级，不必复制一份每槽位的属性公式。
        /// </summary>
        public string EnhanceGainTextAt(int level)
        {
            level = Mathf.Clamp(level, 0, GameConfig.EnhanceMaxLevel);
            float mul = EnhanceMultiplierAt(level);
            int atk = Mathf.RoundToInt(attack * mul);
            int def = Mathf.RoundToInt(defense * mul);
            int life = Mathf.RoundToInt(hp * mul);
            int spd = Mathf.RoundToInt(speed * mul);
            float hastePct = haste * 100f * mul;
            float penPct = armorPen * 100f * mul;
            float critDmgPct = critDamage * 100f * mul;

            switch (slot)
            {
                case EquipmentSlot.Weapon:
                    return level > 0 ? $"攻击 {attack} → {atk}" : $"攻击 {attack}";
                case EquipmentSlot.Boots:
                    return level > 0
                        ? $"速度 {speed} → {spd}   攻速 {haste * 100f:0.#}% → {hastePct:0.#}%"
                        : $"速度 {speed}   攻速 {haste * 100f:0.#}%";
                case EquipmentSlot.Accessory:
                    return level > 0
                        ? $"暴击伤害 {critDamage * 100f:0.#}% → {critDmgPct:0.#}%   穿甲 {armorPen * 100f:0.#}% → {penPct:0.#}%"
                        : $"暴击伤害 {critDamage * 100f:0.#}%   穿甲 {armorPen * 100f:0.#}%";
                default:
                    return level > 0
                        ? $"防御 {defense} → {def}   生命 {hp} → {life}"
                        : $"防御 {defense}   生命 {hp}";
            }
        }

        /// <summary>形如「稀有 武器 Lv.12 +3」。</summary>
        public string Title => $"{QualityName} {SlotName} Lv.{level}{EnhanceText}";

        /// <summary>
        /// 形如「攻击 +24」或「防御 +8  生命 +60」。
        /// 用强化后的主属性：角色属性面板读的就是 Enhanced*，装备明细若还写原始值，
        /// 强化完会出现「总攻击涨了、下面拆出来的装备攻击没变」的矛盾观感。
        /// 未强化时 Enhanced* 与原值相等，所以这里对掉落展示（LootSystem 的掉落日志、
        /// 装备自检）没有任何影响。
        /// </summary>
        public string StatLine
        {
            get
            {
                var sb = new StringBuilder();
                void Add(string text)
                {
                    if (string.IsNullOrEmpty(text)) return;
                    if (sb.Length > 0) sb.Append("  ");
                    sb.Append(text);
                }

                Add(attack > 0 ? $"攻击 +{EnhancedAttack}" : string.Empty);
                Add(defense > 0 ? $"防御 +{EnhancedDefense}" : string.Empty);
                Add(hp > 0 ? $"生命 +{EnhancedHp}" : string.Empty);
                Add(speed > 0 ? $"速度 +{EnhancedSpeed}" : string.Empty);
                Add(haste > 0f ? $"攻速 +{EnhancedHaste * 100f:0.#}%" : string.Empty);
                Add(armorPen > 0f ? $"穿甲 +{EnhancedArmorPen * 100f:0.#}%" : string.Empty);
                Add(critChance > 0f ? $"暴击率 +{critChance * 100f:0.#}%" : string.Empty);
                Add(critDamage > 0f ? $"暴击伤害 +{(slot == EquipmentSlot.Accessory ? EnhancedCritDamage : critDamage) * 100f:0.#}%" : string.Empty);
                return sb.ToString();
            }
        }

        /// <summary>
        /// 副词条展示（形如「◆ 攻击速度 +5.2%  全属性 +3.1%」）；没有副词条时返回空串。
        /// 每 2 条词条换一行：单行最多挤得下约 2.5 条，词条池扩到 5 种后
        /// 传说装备的 4 条词条必然超宽折行，折行位置还会随数值长短乱跳。
        /// 固定「2 条一行」后，装备最多占 4 行（标题 + 主属性 + 2 行词条），高度可预期。
        /// </summary>
        public string AffixLine
        {
            get
            {
                var parts = new List<string>();
                CollectAffix(parts, affixCritChance, "暴击率");
                CollectAffix(parts, affixCritDamage, "暴击伤害");
                CollectAffix(parts, affixAttackSpeed, "攻击速度");
                CollectAffix(parts, affixAllStats, "全属性");
                CollectAffix(parts, affixArmorPen, "穿甲");
                if (parts.Count == 0) return string.Empty;

                var sb = new StringBuilder("◆ ");
                for (int i = 0; i < parts.Count; i++)
                {
                    if (i > 0) sb.Append(i % 2 == 0 ? "\n    " : "  ");
                    sb.Append(parts[i]);
                }
                return sb.ToString();
            }
        }

        // ---- 洗练（步骤 3）：重抽副词条，条数不变 ----

        /// <summary>当前拥有的副词条条数。洗练保持这个数不变，所以它也是「洗出来的容量」。</summary>
        public int AffixCount
        {
            get
            {
                int n = 0;
                if (affixCritChance > 0f) n++;
                if (affixCritDamage > 0f) n++;
                if (affixAttackSpeed > 0f) n++;
                if (affixAllStats > 0f) n++;
                if (affixArmorPen > 0f) n++;
                return n;
            }
        }

        /// <summary>某条词条类型对应的位标记（锁词条用位掩码表达「哪几条被锁」，位序同 EquipmentAffix）。</summary>
        public static int AffixBit(EquipmentAffix kind) => 1 << (int)kind;

        /// <summary>这件装备当前拥有哪些词条（位掩码，位 = EquipmentAffix）。</summary>
        public int PresentAffixMask
        {
            get
            {
                int mask = 0;
                if (affixCritChance > 0f) mask |= AffixBit(EquipmentAffix.CritChance);
                if (affixCritDamage > 0f) mask |= AffixBit(EquipmentAffix.CritDamage);
                if (affixAttackSpeed > 0f) mask |= AffixBit(EquipmentAffix.AttackSpeed);
                if (affixAllStats > 0f) mask |= AffixBit(EquipmentAffix.AllStats);
                if (affixArmorPen > 0f) mask |= AffixBit(EquipmentAffix.ArmorPen);
                return mask;
            }
        }

        /// <summary>一条词条的当前数值；这件装备没有该词条时返回 0。</summary>
        public float AffixValue(EquipmentAffix kind)
        {
            switch (kind)
            {
                case EquipmentAffix.CritChance: return affixCritChance;
                case EquipmentAffix.CritDamage: return affixCritDamage;
                case EquipmentAffix.AttackSpeed: return affixAttackSpeed;
                case EquipmentAffix.AllStats: return affixAllStats;
                case EquipmentAffix.ArmorPen: return affixArmorPen;
                default: return 0f;
            }
        }

        /// <summary>形如「暴击率 +11.9%」，洗练面板的锁词条行与日志共用。</summary>
        public static string AffixText(EquipmentAffix kind, float value) =>
            $"{GameConfig.EquipAffixNames[(int)kind]} +{value * 100f:0.#}%";

        /// <summary>是否还有副词条可以洗练。</summary>
        public bool CanReforge => AffixCount > 0;

        /// <summary>
        /// 掩码里「已锁且这件装备真的有」的词条条数。
        /// 用 &amp; PresentAffixMask 裁掉脏位：锁定状态活在界面里，换装后可能残留旧位，
        /// 不清掉会让费用按不存在的词条算钱。
        /// </summary>
        public int LockedAffixCount(int mask)
        {
            int locked = mask & PresentAffixMask;
            int n = 0;
            while (locked != 0)
            {
                n += locked & 1;
                locked >>= 1;
            }
            return n;
        }

        /// <summary>洗练一次所需金币；没有副词条时返回 0。</summary>
        public int ReforgeCost => CanReforge ? GameConfig.ReforgeCost(level, quality, reforgeCount) : 0;

        /// <summary>锁住 lockedCount 条词条时的洗练费用；没有副词条时返回 0。</summary>
        public int ReforgeCostWithLocks(int lockedCount) =>
            CanReforge ? GameConfig.ReforgeCost(level, quality, reforgeCount, lockedCount) : 0;

        /// <summary>锁住 lockedCount 条词条时的单价倍率（×1 / ×3 / ×9 / ×27 …），用于展示。</summary>
        public static float ReforgeLockMultiplier(int lockedCount) =>
            Mathf.Pow(GameConfig.ReforgeLockFactor, Mathf.Max(0, lockedCount));

        /// <summary>形如「已洗练 6 次」。</summary>
        public string ReforgeCountText => reforgeCount > 0 ? $"已洗练 {reforgeCount} 次" : "未洗练";

        /// <summary>
        /// 词条质量 0~1：每条副词条在各自区间里的百分位的平均。
        /// 等级缩放对同一条词条是常数，折算时约掉，所以不同等级的装备质量可比。
        /// 只用于展示「这次比上次好还是差」，**不参与评分与战斗计算**。
        /// </summary>
        public float AffixQuality
        {
            get
            {
                // 小件的词条数值是缩放的，质量要把缩放也除掉，否则会显示成 0% 质量。
                float levelScale = (1f + level * GameConfig.EquipAffixLevelScale) * Mathf.Max(0.01f, affixScale);
                float sum = 0f;
                int n = 0;
                AccumulateQuality(ref sum, ref n, affixCritChance, EquipmentAffix.CritChance, levelScale);
                AccumulateQuality(ref sum, ref n, affixCritDamage, EquipmentAffix.CritDamage, levelScale);
                AccumulateQuality(ref sum, ref n, affixAttackSpeed, EquipmentAffix.AttackSpeed, levelScale);
                AccumulateQuality(ref sum, ref n, affixAllStats, EquipmentAffix.AllStats, levelScale);
                AccumulateQuality(ref sum, ref n, affixArmorPen, EquipmentAffix.ArmorPen, levelScale);
                return n == 0 ? 0f : Mathf.Clamp01(sum / n);
            }
        }

        /// <summary>把一条副词条折算成 0~1 的百分位并累加。</summary>
        private static void AccumulateQuality(ref float sum, ref int count, float value,
            EquipmentAffix kind, float levelScale)
        {
            if (value <= 0f) return;
            int k = (int)kind;
            float min = GameConfig.EquipAffixMin[k];
            float max = GameConfig.EquipAffixMax[k];
            float baseValue = value / Mathf.Max(0.0001f, levelScale);
            sum += Mathf.Clamp01(max > min ? (baseValue - min) / (max - min) : 1f);
            count++;
        }

        /// <summary>单行副词条摘要（无「◆」前缀、无换行），给日志与洗练面板用。</summary>
        public string AffixSummary
        {
            get
            {
                var parts = new List<string>();
                CollectAffix(parts, affixCritChance, "暴击率");
                CollectAffix(parts, affixCritDamage, "暴击伤害");
                CollectAffix(parts, affixAttackSpeed, "攻击速度");
                CollectAffix(parts, affixAllStats, "全属性");
                CollectAffix(parts, affixArmorPen, "穿甲");
                return string.Join("  ", parts);
            }
        }

        /// <summary>把一个非 0 副词条按「名称 +值%」收进列表。</summary>
        private static void CollectAffix(List<string> parts, float value, string name)
        {
            if (value <= 0f) return;
            parts.Add($"{name} +{value * 100f:0.#}%");
        }
    }

    /// <summary>
    /// 怪物数据。等级由所在关卡决定（第 c 章 Lv.10(c-1)+1 ~ 10c），不再跟随主角等级。
    /// 面板按三层连乘得出：等级基础 × 类型系数 × 精英/Boss 系数 × 词缀系数。
    /// </summary>
    [Serializable]
    public class MonsterData
    {
        /// <summary>怪物类型：决定面板风格、暴击属性与经验倍率。</summary>
        public MonsterType type;

        /// <summary>档次：普通 / 精英 / Boss，决定属性、经验、掉落与名称前缀。</summary>
        public MonsterTier tier;

        /// <summary>词缀：30% 概率附加，效果与类型、档次叠加。</summary>
        public MonsterAffix affix;

        public int level;
        public int maxHp;
        public int currentHp;

        /// <summary>护盾：受到的伤害先扣护盾再扣生命（护盾词缀开场给 30% 最大生命）。</summary>
        public int shield;

        public int attack;
        public int defense;
        public float speed;

        /// <summary>暴击率：近战/远程 5%，暴击怪 45%（取自 GameConfig.MonsterCritChanceByType）。</summary>
        public float critChance;

        /// <summary>暴击伤害倍率：见 GameConfig.MonsterCritDamageByType（近战/远程/暴击怪各一项）。</summary>
        public float critDamage;

        /// <summary>距离下次攻击的倒计时（秒）。</summary>
        public float attackTimer;

        /// <summary>血条只反映生命，护盾单独显示。</summary>
        public float HpProgress => maxHp <= 0 ? 0f : Mathf.Clamp01((float)currentHp / maxHp);

        public bool IsDead => currentHp <= 0;

        /// <summary>按怪物所属章节取的族类名（第 3 章是骷髅、第 9 章是恶魔）。</summary>
        public string TypeName => GameConfig.MonsterNameOf(level, type);

        public string TierName => GameConfig.MonsterTierPrefixes[(int)tier];

        public string AffixName => GameConfig.AffixNames[(int)affix];

        public bool HasAffix => affix != MonsterAffix.None;

        /// <summary>形如「精英·哥布林盗贼 Lv.37」。</summary>
        public string Title => $"{TierName}{TypeName} Lv.{level}";

        /// <summary>
        /// 按主角等级抽一个怪物类型（要求 2）：权重随等级分档，
        /// 等级越高越容易刷出远程与暴击怪。
        /// </summary>
        public static MonsterType RollType(int playerLevel)
        {
            float[] weights = GameConfig.MonsterTypeWeights[GameConfig.MonsterTypeTier(playerLevel)];
            // 本文件同时 using System 与 UnityEngine，必须写出完整限定名以避开 Random 歧义。
            float roll = UnityEngine.Random.value;
            float cumulative = 0f;

            for (int i = 0; i < weights.Length; i++)
            {
                cumulative += weights[i];
                if (roll < cumulative) return (MonsterType)i;
            }

            // 浮点累加误差兜底。
            return (MonsterType)(weights.Length - 1);
        }

        /// <summary>按 AffixChance 概率抽一个词缀；没抽中返回 None。</summary>
        public static MonsterAffix RollAffix()
        {
            if (UnityEngine.Random.value >= GameConfig.AffixChance) return MonsterAffix.None;

            // 从 1 开始抽，跳过 None。
            return (MonsterAffix)UnityEngine.Random.Range(1, GameConfig.AffixNames.Length);
        }

        /// <summary>按等级、类型、档次与词缀生成一只怪物（要求 4）。</summary>
        public static MonsterData CreateForLevel(int level, MonsterType type, MonsterTier tier, MonsterAffix affix)
        {
            // 战役：Lv.110 起吃 MaxTier/终局 BOSS 强化，也吃章节族类偏向与章节压迫。
            return CreateCore(level, type, tier, affix,
                applyMaxTierAndFinalBoss: true, applyChapterBias: true, extraStatFactor: 1f);
        }

        /// <summary>
        /// 无尽模式的怪物：等级直接由层号决定，但不吃 MaxTier/终局 BOSS/章节偏向/章节压迫，
        /// 改成统一乘一个 EndlessBaseFactor。这样无尽强度只由「层号 + 一个倍率」控制，
        /// 不会在第 110 级、第 11 章这些战役拐点上产生跳变。
        /// </summary>
        public static MonsterData CreateForEndlessFloor(
            int floor, MonsterType type, MonsterTier tier, MonsterAffix affix)
        {
            int level = EndlessCatalog.MonsterLevelOf(floor);
            return CreateCore(level, type, tier, affix,
                applyMaxTierAndFinalBoss: false, applyChapterBias: false,
                extraStatFactor: Mathf.Max(0.01f, GameConfig.EndlessBaseFactor));
        }

        private static MonsterData CreateCore(
            int level, MonsterType type, MonsterTier tier, MonsterAffix affix,
            bool applyMaxTierAndFinalBoss, bool applyChapterBias, float extraStatFactor)
        {
            int lv = Mathf.Max(1, level);
            int steps = lv - 1;
            int t = (int)type;
            float tierFactor = GameConfig.MonsterTierStatFactor[(int)tier];
            float tierSpeedFactor = GameConfig.MonsterTierSpeedFactor[(int)tier];
            float[] af = GameConfig.AffixStatFactors[(int)affix];

            // 等级基础面板（与类型、档次、词缀都无关）
            float baseHp = GameConfig.MonsterBaseMaxHp + GameConfig.MonsterMaxHpPerLevel * steps;
            float baseAttack = GameConfig.MonsterBaseAttack + GameConfig.MonsterAttackPerLevel * steps;
            float baseDefense = GameConfig.MonsterBaseDefense + GameConfig.MonsterDefensePerLevel * steps;
            float baseSpeed = GameConfig.MonsterBaseSpeed + GameConfig.MonsterSpeedPerLevel * steps;

            // 三层连乘：类型 × 精英/Boss × 词缀
            float hpFactor = GameConfig.MonsterHpFactorByType[t] * tierFactor * af[0];
            float attackFactor = GameConfig.MonsterAttackFactorByType[t] * tierFactor * af[1];
            float defenseFactor = GameConfig.MonsterDefenseFactorByType[t] * tierFactor * af[2];
            // 速度走单独的档次倍率（更保守），不吃 MonsterTierStatFactor。
            float speedFactor = GameConfig.MonsterSpeedFactorByType[t] * tierSpeedFactor * af[3];

            // 梯度最高档（Lv.110，主角满级时遇到）再叠一层终局强化。
            if (applyMaxTierAndFinalBoss && lv >= GameConfig.MaxTierLevel)
            {
                hpFactor *= GameConfig.MaxTierHpFactor;
                attackFactor *= GameConfig.MaxTierAttackFactor;
                defenseFactor *= GameConfig.MaxTierDefenseFactor;
                speedFactor *= GameConfig.MaxTierSpeedFactor;

                // 终局 BOSS 再叠一层：让 11-10 成为明确的关卡门，而不是「七次挑战的运气」。
                // 实测首通落在巅峰 P15~P58（中位 P31），不是 P100；再往上抬会把新档通关率砍半
                // （30 种子 25/30 → 1.8 的 17/30），所以定格 1.6。只作用于关底 Boss。
                if (tier == MonsterTier.Boss)
                {
                    hpFactor *= GameConfig.FinalBossStatFactor;
                    attackFactor *= GameConfig.FinalBossStatFactor;
                    defenseFactor *= GameConfig.FinalBossStatFactor;
                }
            }

            // 最后叠章节族类偏向：第 3 章骷髅脆而凶、第 4 章食人魔肉而慢。
            // 第 1 章（教学）与第 11 章（终局）的偏向是全 1，所以这两头的数据不受影响。
            if (applyChapterBias)
            {
                float[] bias = GameConfig.MonsterStatBiasOf(lv);
                hpFactor *= bias[0];
                attackFactor *= bias[1];
                defenseFactor *= bias[2];
                speedFactor *= bias[3];
            }

            // 章节压迫：第 1~10 章逐章加码，第 11 章不吃（终局档由 MaxTier* 负责）。
            // 只压生命/攻击/防御，不压速度——速度对攻击间隔是非线性的，压一点就翻倍。
            if (applyChapterBias)
            {
                float pressure = GameConfig.ChapterPressure(
                    GameConfig.ChapterOfMonsterLevel(lv), tier == MonsterTier.Boss);
                hpFactor *= pressure;
                attackFactor *= pressure;
                defenseFactor *= pressure;
            }

            // 无尽倍率：只放大生命/攻击/防御，速度不动（同样因为攻击间隔是非线性的）。
            float extra = Mathf.Max(0.01f, extraStatFactor);
            hpFactor *= extra;
            attackFactor *= extra;
            defenseFactor *= extra;

            var m = new MonsterData
            {
                type = type,
                tier = tier,
                affix = affix,
                level = lv,
                maxHp = Mathf.Max(1, Mathf.RoundToInt(baseHp * hpFactor)),
                attack = Mathf.Max(1, Mathf.RoundToInt(baseAttack * attackFactor)),
                defense = Mathf.Max(0, Mathf.RoundToInt(baseDefense * defenseFactor)),
                speed = baseSpeed * speedFactor,
                critChance = GameConfig.MonsterCritChanceByType[t],
                critDamage = GameConfig.MonsterCritDamageByType[t],
            };

            // 护盾词缀：开场即获得 30% 最大生命的护盾。
            if (affix == MonsterAffix.Shield)
                m.shield = Mathf.Max(1, Mathf.RoundToInt(m.maxHp * GameConfig.ShieldAffixPercent));

            m.currentHp = m.maxHp;
            m.attackTimer = GameConfig.AttackInterval(m.speed);
            return m;
        }
    }

    /// <summary>战斗日志的一行。</summary>
    public struct LogEntry
    {
        public string message;
        public Color color;

        public LogEntry(string message, Color color)
        {
            this.message = message;
            this.color = color;
        }
    }
}

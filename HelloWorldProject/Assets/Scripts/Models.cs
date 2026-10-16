using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace AutoBattle
{
    /// <summary>装备槽位：只做武器和衣服，各 1 件（要求 9）。</summary>
    public enum EquipmentSlot
    {
        Weapon,
        Armor,
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

        public string QualityName => GameConfig.QualityNames[(int)quality];

        public Color QualityColor => GameConfig.QualityColors[(int)quality];

        public string SlotName => slot == EquipmentSlot.Weapon ? "武器" : "衣服";

        /// <summary>形如「倍率 x2.34」，用来展示同品质内的强弱浮动。</summary>
        public string MultiplierText => $"倍率 x{qualityMultiplier:0.00}";

        /// <summary>形如「稀有 武器 Lv.12」。</summary>
        public string Title => $"{QualityName} {SlotName} Lv.{level}";

        /// <summary>形如「攻击 +24」或「防御 +8  生命 +60」。</summary>
        public string StatLine
        {
            get
            {
                var sb = new StringBuilder();
                if (attack > 0) sb.Append($"攻击 +{attack}");
                if (defense > 0)
                {
                    if (sb.Length > 0) sb.Append("  ");
                    sb.Append($"防御 +{defense}");
                }
                if (hp > 0)
                {
                    if (sb.Length > 0) sb.Append("  ");
                    sb.Append($"生命 +{hp}");
                }
                if (critChance > 0f)
                {
                    if (sb.Length > 0) sb.Append("  ");
                    sb.Append($"暴击率 +{critChance * 100f:0.#}%");
                }
                if (critDamage > 0f)
                {
                    if (sb.Length > 0) sb.Append("  ");
                    sb.Append($"暴击伤害 +{critDamage * 100f:0.#}%");
                }
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

        /// <summary>把一个非 0 副词条按「名称 +值%」收进列表。</summary>
        private static void CollectAffix(List<string> parts, float value, string name)
        {
            if (value <= 0f) return;
            parts.Add($"{name} +{value * 100f:0.#}%");
        }
    }

    /// <summary>
    /// 主角数据（要求 2）。基础属性由等级推导，最终属性 = 基础 + 装备加成。
    /// </summary>
    [Serializable]
    public class PlayerData
    {
        public int level = 1;
        public int exp = 0;

        /// <summary>金币总数：卖掉弱于当前装备的掉落累积，单价见 GameConfig.SellPrice。</summary>
        public int gold;

        /// <summary>持有的生命药剂数量（上限见 GameConfig.PotionMaxCount）。</summary>
        public int potionCount;

        /// <summary>是否开启「自动购买」：持有数低于阈值且金币足够时自动补 1 瓶（交战时不补）。</summary>
        public bool autoBuyPotion = true;

        /// <summary>巅峰等级：满级（Lv.100）后继续累积经验提升，每级 +1 攻击 / +1 防御。</summary>
        public int paragonLevel;

        /// <summary>巅峰经验（只在满级后累积）。</summary>
        public int paragonExp;

        /// <summary>
        /// 已获得的技能位掩码（第 N 位对应 SkillId N）。
        /// 100 级前恒为 0（完全没有技能）；满级后每升 10 级巅峰获得一次「三选一」。
        /// </summary>
        public int ownedSkillMask;

        /// <summary>
        /// 还没兑现的技能三选一次数。巅峰升级时按里程碑差值补上，玩家每选一个减一。
        /// 存进存档：中途退出不会丢应得的技能。
        /// </summary>
        public int pendingSkillPicks;

        // ==================== 关卡 / 章节进度（步骤 4a） ====================

        /// <summary>当前章节号，1 起。关卡怪物等级 = ChapterMonsterLevelStep × chapterIndex。</summary>
        public int chapterIndex = 1;

        /// <summary>当前章节内的关卡号，1 起。</summary>
        public int stageIndex = 1;

        /// <summary>本关已击杀的怪物数（主角阵亡重试时归零）。</summary>
        public int stageKillCount;

        /// <summary>历史解锁到的最高章节，选关界面用它判断锁定。</summary>
        public int maxChapterUnlocked = 1;

        // ==================== 首通记录（步骤 4c） ====================

        /// <summary>
        /// 已经拿过首通奖励的关卡，元素是 chapter * 100 + stage 的编码。
        /// 用 List&lt;int&gt; 而不是 HashSet&lt;int&gt;：JsonUtility 不序列化 HashSet。
        /// </summary>
        public List<int> clearedStages = new List<int>();

        /// <summary>把关卡编码成 clearedStages 里的整数。章节 &lt; 100，所以两位关卡号放得下。</summary>
        public static int StageKey(int chapter, int stage) => chapter * 100 + stage;

        /// <summary>
        /// 编码方式的解码侧。和 StageKey 放在一起，改编码时只需看这两行
        /// （原先 Stages.cs 里直接写着 / 100 和 % 100，改一处就会静默错位）。
        /// </summary>
        public static int ChapterOfKey(int stageKey) => stageKey / 100;

        /// <summary>见 ChapterOfKey。</summary>
        public static int StageOfKey(int stageKey) => stageKey % 100;

        /// <summary>这一关是否已经拿过首通奖励。</summary>
        public bool IsStageFirstCleared(int chapter, int stage)
        {
            return clearedStages != null && clearedStages.Contains(StageKey(chapter, stage));
        }

        /// <summary>标记首通。返回 true 表示这是头一回（调用方据此发奖励）。</summary>
        public bool MarkStageFirstCleared(int chapter, int stage)
        {
            if (clearedStages == null) clearedStages = new List<int>();
            int key = StageKey(chapter, stage);
            if (clearedStages.Contains(key)) return false;
            clearedStages.Add(key);
            return true;
        }

        /// <summary>已首通的关卡总数。</summary>
        public int ClearedStageCount => clearedStages != null ? clearedStages.Count : 0;

        /// <summary>
        /// 已经领到的首通金币总额（步骤 4d）。逐关按 StageCatalog 的公式累加，
        /// 所以改了首通奖励配置之后这个数字会跟着变——它是「按规则重算」，
        /// 不是另记一笔流水。
        /// </summary>
        public int TotalFirstClearGoldClaimed()
        {
            if (clearedStages == null) return 0;
            int sum = 0;
            for (int i = 0; i < clearedStages.Count; i++)
            {
                sum += StageCatalog.FirstClearGoldOfKey(clearedStages[i]);
            }
            return sum;
        }

        /// <summary>该章已经首通的关卡数。</summary>
        public int ClearedCountInChapter(int chapter)
        {
            if (clearedStages == null) return 0;
            int n = 0;
            for (int s = 1; s <= StageCatalog.StagesPerChapter; s++)
            {
                if (clearedStages.Contains(StageKey(chapter, s))) n++;
            }
            return n;
        }

        /// <summary>本关是不是头一回打（还没拿过首通奖励）。</summary>
        public bool IsCurrentStageFirstClear => !IsStageFirstCleared(chapterIndex, stageIndex);

        /// <summary>当前关卡的定义（章节 / 关卡号会被夹到合法范围）。</summary>
        public StageDef CurrentStage => StageCatalog.Get(chapterIndex, stageIndex);

        /// <summary>本关还差几只怪通关。</summary>
        public int StageKillsLeft => Mathf.Max(0, CurrentStage.killTarget - stageKillCount);

        /// <summary>本关是否已经打满击杀数。</summary>
        public bool IsStageCleared => stageKillCount >= CurrentStage.killTarget;

        /// <summary>
        /// 直接把进度放到某关（选关 / 读档 / 关卡结束推进都走这里）。
        /// 会推进 maxChapterUnlocked，但不会回退它。
        /// </summary>
        public void EnterStage(int chapter, int stage)
        {
            chapterIndex = Mathf.Clamp(chapter, 1, GameConfig.ChapterCount);
            stageIndex = Mathf.Clamp(stage, 1, GameConfig.StagesPerChapter);
            stageKillCount = 0;
            if (chapterIndex > maxChapterUnlocked) maxChapterUnlocked = chapterIndex;
        }

        /// <summary>
        /// 通关推进：本章还有下一关就去下一关，否则进入下一章第一关。
        /// 返回 false 表示已经是最后一关（终局），进度不动。
        /// </summary>
        public bool AdvanceStage()
        {
            int c = chapterIndex;
            int s = stageIndex;
            if (!StageCatalog.Advance(ref c, ref s)) return false;
            EnterStage(c, s);
            return true;
        }

        /// <summary>本关关底怪的档次（普通关精英、章末关 Boss）。</summary>
        public MonsterTier StageClearTier => CurrentStage.clearTier;

        // ---- 六个技能槽（-1 表示空）：0~1 主动、2~3 被动、4~5 触发 ----

        public int skillActive0 = -1;
        public int skillActive1 = -1;
        public int skillPassive0 = -1;
        public int skillPassive1 = -1;
        public int skillTrigger0 = -1;
        public int skillTrigger1 = -1;

        /// <summary>当前生命。载入后由 GameController 夹到 MaxHp 范围内。</summary>
        public int currentHp;

        /// <summary>当前护盾（「护盾生成」技能给的）。运行时状态，不存档；受击时优先于生命被消耗。</summary>
        [System.NonSerialized] public int shield;

        public Equipment weapon;
        public Equipment armor;

        // ---- 基础属性（仅由等级决定）----

        public int BaseMaxHp => GameConfig.PlayerBaseMaxHp + GameConfig.PlayerMaxHpPerLevel * (level - 1);
        public int BaseAttack => GameConfig.PlayerBaseAttack + GameConfig.PlayerAttackPerLevel * (level - 1);
        public int BaseDefense => GameConfig.PlayerBaseDefense + GameConfig.PlayerDefensePerLevel * (level - 1);
        public int BaseSpeed => GameConfig.PlayerBaseSpeed + GameConfig.PlayerSpeedPerLevel * (level - 1);

        // ---- 最终属性（基础 + 装备）----

        /// <summary>副词条汇总：攻击速度加成（武器 + 衣服）。</summary>
        public float EquipAttackSpeedBonus => (weapon != null ? weapon.affixAttackSpeed : 0f)
                                             + (armor != null ? armor.affixAttackSpeed : 0f);

        /// <summary>副词条汇总：全属性加成，按比例同时放大攻击、防御、生命。</summary>
        public float AllStatsBonus => (weapon != null ? weapon.affixAllStats : 0f)
                                    + (armor != null ? armor.affixAllStats : 0f);

        /// <summary>副词条汇总：暴击率。</summary>
        public float AffixCritChance => (weapon != null ? weapon.affixCritChance : 0f)
                                      + (armor != null ? armor.affixCritChance : 0f);

        /// <summary>副词条汇总：暴击伤害。</summary>
        public float AffixCritDamage => (weapon != null ? weapon.affixCritDamage : 0f)
                                      + (armor != null ? armor.affixCritDamage : 0f);

        /// <summary>
        /// 副词条汇总：护甲穿透（0.1 表示无视目标 10% 防御）。
        /// 上限 80% —— 与暴击率 95% 上限同理，避免「完全无视防御」把防御属性变成废纸。
        /// </summary>
        public float ArmorPenetration => Mathf.Min(0.8f,
            (weapon != null ? weapon.affixArmorPen : 0f) + (armor != null ? armor.affixArmorPen : 0f));

        // ==================== 巅峰与技能汇总 ====================

        /// <summary>当前技能等级（由巅峰等级推导：每 10 级 +1）。</summary>
        public int SkillLevel => GameConfig.SkillLevel(paragonLevel);

        /// <summary>巅峰提供的攻击加成（纯加算，不参与百分比乘区）。</summary>
        public int ParagonAttackBonus => paragonLevel * GameConfig.ParagonAttackPerLevel;

        /// <summary>巅峰提供的防御加成。</summary>
        public int ParagonDefenseBonus => paragonLevel * GameConfig.ParagonDefensePerLevel;

        /// <summary>巅峰提供的生命加成（每级 +50，纯加算）。</summary>
        public int ParagonHpBonus => paragonLevel * GameConfig.ParagonHpPerLevel;

        /// <summary>该技能是否已装在任意槽位。</summary>
        public bool HasSkill(SkillId id)
        {
            int v = (int)id;
            return skillActive0 == v || skillActive1 == v || skillPassive0 == v
                || skillPassive1 == v || skillTrigger0 == v || skillTrigger1 == v;
        }

        /// <summary>取某个被动按技能等级放大后的数值；没装则返回 0。</summary>
        public float PassiveValue(SkillId id)
        {
            if (!HasSkill(id)) return 0f;
            return GameConfig.ScaleSkillValue(SkillCatalog.Get(id).value, SkillLevel);
        }

        // ---- 被动技能带来的属性 ----

        /// <summary>钢铁之躯：防御 +50%。</summary>
        public float SkillDefenseBonus => PassiveValue(SkillId.IronBody);

        /// <summary>狂暴之心：暴击率 +8%。</summary>
        public float SkillCritChanceBonus => PassiveValue(SkillId.BerserkHeart);

        /// <summary>致命打击：暴击伤害乘区（基础 +20%，随技能等级放大）。</summary>
        public float SkillCritDamageBonus => PassiveValue(SkillId.LethalStrike);

        /// <summary>吸血鬼：吸血 +5%。</summary>
        public float Lifesteal => PassiveValue(SkillId.Vampire);

        /// <summary>迅捷：攻击速度 +8%。</summary>
        public float SkillAttackSpeedBonus => PassiveValue(SkillId.Swiftness);

        /// <summary>巨人杀手：对精英 / Boss 的伤害加成。</summary>
        public float GiantSlayerBonus => PassiveValue(SkillId.GiantSlayer);

        /// <summary>不死之身：低血时的减伤比例。</summary>
        public float UndyingReduction => PassiveValue(SkillId.Undying);

        /// <summary>不死之身的触发血线（未装备返回 0）。</summary>
        public float UndyingThreshold => HasSkill(SkillId.Undying) ? SkillCatalog.Get(SkillId.Undying).extra : 0f;

        /// <summary>攻击速度总加成 = 装备副词条 + 迅捷被动。</summary>
        public float AttackSpeedBonus => EquipAttackSpeedBonus + SkillAttackSpeedBonus;

        // ---- 最终属性 ----

        // 全属性作用在「基础 + 装备」之和上；防御再吃一次钢铁之躯；巅峰加成是纯加算。
        public int MaxHp => Mathf.RoundToInt((BaseMaxHp + (armor != null ? armor.hp : 0)) * (1f + AllStatsBonus))
                          + ParagonHpBonus;

        public int Attack => Mathf.RoundToInt((BaseAttack + (weapon != null ? weapon.attack : 0)) * (1f + AllStatsBonus))
                           + ParagonAttackBonus;

        public int Defense => Mathf.RoundToInt(
                                Mathf.RoundToInt((BaseDefense + (armor != null ? armor.defense : 0)) * (1f + AllStatsBonus))
                                * (1f + SkillDefenseBonus))
                            + ParagonDefenseBonus;

        /// <summary>速度本身不受全属性影响；攻速词条走独立的「攻击间隔乘区」。</summary>
        public int Speed => BaseSpeed;

        /// <summary>暴击率 = 初始 5% + 武器主属性 + 副词条 + 狂暴之心，并夹在 MaxCritChance 以内。</summary>
        public float CritChance => Mathf.Min(
            GameConfig.MaxCritChance,
            GameConfig.PlayerBaseCritChance + (weapon != null ? weapon.critChance : 0f)
            + AffixCritChance + SkillCritChanceBonus);

        /// <summary>暴击伤害倍率 =（初始 150% + 武器主属性 + 副词条）× 致命打击乘区。</summary>
        public float CritDamage => (GameConfig.PlayerBaseCritDamage
                                 + (weapon != null ? weapon.critDamage : 0f) + AffixCritDamage)
                                 * (1f + SkillCritDamageBonus);

        /// <summary>当前等级升到下一级所需经验；满级返回 0（要求 3）。</summary>
        public int ExpToNextLevel => level >= GameConfig.MaxLevel ? 0 : GameConfig.ExpToNextLevel(level);

        public bool IsMaxLevel => level >= GameConfig.MaxLevel;

        /// <summary>进度条填充比例 0~1：满级前是普通经验，满级后转为巅峰经验进度。</summary>
        public float ExpProgress
        {
            get
            {
                if (IsMaxLevel)
                {
                    if (IsMaxParagon) return 1f;
                    int needParagon = ParagonExpToNext;
                    return needParagon <= 0 ? 1f : Mathf.Clamp01((float)paragonExp / needParagon);
                }

                int need = ExpToNextLevel;
                return need <= 0 ? 0f : Mathf.Clamp01((float)exp / need);
            }
        }

        public float HpProgress => MaxHp <= 0 ? 0f : Mathf.Clamp01((float)currentHp / MaxHp);

        /// <summary>把当前生命补齐到上限（新开档或升级后调用）。</summary>
        public void FillHp() => currentHp = MaxHp;

        public Equipment GetEquipment(EquipmentSlot s) => s == EquipmentSlot.Weapon ? weapon : armor;

        public void SetEquipment(Equipment e)
        {
            if (e == null) return;
            if (e.slot == EquipmentSlot.Weapon) weapon = e;
            else armor = e;
        }

        /// <summary>当前巅峰等级升到下一级所需经验；满巅峰返回 0。</summary>
        public int ParagonExpToNext => IsMaxParagon ? 0 : GameConfig.ParagonExpToNext(paragonLevel);

        public bool IsMaxParagon => paragonLevel >= GameConfig.MaxParagonLevel;

        /// <summary>
        /// 增加经验并处理升级。满级（Lv.100）之前走普通等级；满级之后经验转为巅峰经验。
        /// 返回本次实际升了几级（含巅峰），供日志使用。
        /// </summary>
        public int AddExp(int amount)
        {
            if (amount <= 0) return 0;

            if (IsMaxLevel)
            {
                // 已满级：全部转成巅峰经验。
                return AddParagonExp(amount);
            }

            exp += amount;
            int gained = 0;

            // 用 while 支持一次击杀连升多级。
            while (!IsMaxLevel && exp >= ExpToNextLevel)
            {
                exp -= ExpToNextLevel;
                level++;
                gained++;
            }

            if (IsMaxLevel) exp = 0; // 满级后清空经验条
            return gained;
        }

        /// <summary>加巅峰经验并处理升级；返回本次升了几级巅峰。</summary>
        public int AddParagonExp(int amount)
        {
            if (IsMaxParagon || amount <= 0) return 0;

            paragonExp += amount;
            int gained = 0;

            while (!IsMaxParagon && paragonExp >= ParagonExpToNext)
            {
                paragonExp -= ParagonExpToNext;
                paragonLevel++;
                gained++;
            }

            if (IsMaxParagon) paragonExp = 0;
            return gained;
        }

        /// <summary>该技能是否已获得（100 级前一个都没有）。</summary>
        public bool IsSkillOwned(SkillId id) => (ownedSkillMask & (1 << (int)id)) != 0;

        /// <summary>已获得的技能数量。</summary>
        public int OwnedSkillCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < SkillCatalog.All.Length; i++)
                    if ((ownedSkillMask & (1 << i)) != 0) count++;
                return count;
            }
        }

        /// <summary>
        /// 随机获得一个尚未拥有的技能（巅峰每 10 级触发一次）。
        /// 返回获得的技能编号；十五个技能全部拥有时返回 (SkillId)(-1)。
        /// </summary>
        public SkillId GrantRandomSkill()
        {
            var pool = new List<SkillId>();
            for (int i = 0; i < SkillCatalog.All.Length; i++)
                if ((ownedSkillMask & (1 << i)) == 0) pool.Add((SkillId)i);

            if (pool.Count == 0) return (SkillId)(-1);

            SkillId pick = pool[UnityEngine.Random.Range(0, pool.Count)];
            ownedSkillMask |= 1 << (int)pick;
            return pick;
        }

        /// <summary>
        /// 从「尚未拥有」的技能里随机抽 count 个作为候选（互不重复）。
        /// 剩余不足 count 个时返回全部剩余，全拥有时返回空表。
        /// </summary>
        public List<SkillId> RollSkillCandidates(int count)
        {
            var pool = new List<SkillId>();
            for (int i = 0; i < SkillCatalog.All.Length; i++)
                if ((ownedSkillMask & (1 << i)) == 0) pool.Add((SkillId)i);

            var result = new List<SkillId>();
            for (int i = 0; i < count && pool.Count > 0; i++)
            {
                int k = UnityEngine.Random.Range(0, pool.Count);
                result.Add(pool[k]);
                pool.RemoveAt(k);   // 抽走就移出池子，保证候选不重复
            }
            return result;
        }

        /// <summary>授予指定技能（三选一的落点）。已拥有或编号非法时返回 false。</summary>
        public bool GrantSkill(SkillId id)
        {
            int index = (int)id;
            if (index < 0 || index >= SkillCatalog.All.Length) return false;
            if ((ownedSkillMask & (1 << index)) != 0) return false;

            ownedSkillMask |= 1 << index;
            return true;
        }

        /// <summary>按精英 / Boss 档次判断是否为「巨人杀手」的目标。</summary>
        public static bool IsEliteOrBoss(MonsterTier tier) => tier != MonsterTier.Normal;

        // ---- 技能槽读写（0~1 主动、2~3 被动、4~5 触发；-1 表示空）----

        public int GetSkillSlot(int index)
        {
            switch (index)
            {
                case 0: return skillActive0;
                case 1: return skillActive1;
                case 2: return skillPassive0;
                case 3: return skillPassive1;
                case 4: return skillTrigger0;
                case 5: return skillTrigger1;
                default: return -1;
            }
        }

        public void SetSkillSlot(int index, int skillId)
        {
            switch (index)
            {
                case 0: skillActive0 = skillId; break;
                case 1: skillActive1 = skillId; break;
                case 2: skillPassive0 = skillId; break;
                case 3: skillPassive1 = skillId; break;
                case 4: skillTrigger0 = skillId; break;
                case 5: skillTrigger1 = skillId; break;
            }
        }

        /// <summary>某个技能当前装在哪个槽位；没装返回 -1。</summary>
        public int FindSkillSlot(int skillId)
        {
            for (int i = 0; i < 6; i++) if (GetSkillSlot(i) == skillId) return i;
            return -1;
        }
    }

    /// <summary>
    /// 怪物数据。等级始终等于主角等级（要求 4）。
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
            if (lv >= GameConfig.MaxTierLevel)
            {
                hpFactor *= GameConfig.MaxTierHpFactor;
                attackFactor *= GameConfig.MaxTierAttackFactor;
                defenseFactor *= GameConfig.MaxTierDefenseFactor;
                speedFactor *= GameConfig.MaxTierSpeedFactor;
            }

            // 最后叠章节族类偏向：第 3 章骷髅脆而凶、第 4 章食人魔肉而慢。
            // 第 1 章（教学）与第 11 章（终局）的偏向是全 1，所以这两头的数据不受影响。
            float[] bias = GameConfig.MonsterStatBiasOf(lv);
            hpFactor *= bias[0];
            attackFactor *= bias[1];
            defenseFactor *= bias[2];
            speedFactor *= bias[3];

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

using UnityEngine;

namespace AutoBattle
{
    /// <summary>
    /// 全部可调数值与公式的唯一来源（要求 18）。
    /// 调整玩法平衡只需要改这个文件，其它脚本不出现魔法数字。
    ///
    /// 拆成了三个 partial 文件，按「改它的理由」分：
    /// <list type="bullet">
    /// <item>本文件 —— 可热改的标量数值 + 派生公式。改完可以只改
    /// <c>Assets/Resources/game_config.json</c>（同名同类型字段会被反射覆盖），不必重编译。</item>
    /// <item><c>GameConfig.Tables.cs</c> —— 31 个固定长度的数组（怪物类型系数、章节族类名、
    /// 品质权重……）。**这些改 json 无效**，必须改代码。</item>
    /// <item><c>GameConfig.Loader.cs</c> —— 反射加载与导出。</item>
    /// </list>
    /// </summary>
    public static partial class GameConfig
    {
        // ==================== 等级与经验 ====================

        /// <summary>主角等级上限（要求 3）。</summary>
        public static int MaxLevel = 100;

        /// <summary>升到下一级所需经验：30 + 15*L + 2*L²（要求 7），L 为当前等级。</summary>
        public static int ExpToNextLevel(int level) => 30 + 15 * level + 2 * level * level;

        /// <summary>
        /// 关卡制下的经验补偿系数：每高一级怪多给这么多经验。
        /// 基础经验只随怪物等级线性增长，而升级成本是二次的（ExpToNextLevel = 30+15L+2L²），
        /// 不补的话主角等级会被关卡越拉越远——实测只靠推进到 11 章只有 Lv.66，打不过 Lv.110 的怪。
        /// 目标：每章末主角等级 ≈ 该章末尾的怪物等级。
        /// </summary>
        public static float ChapterExpScalePerLevel = 0.07f;

        /// <summary>关卡经验补偿：Lv.1 → 1.0，Lv.110 → 约 8.6。</summary>
        public static float ChapterExpFactor(int monsterLevel) =>
            1f + (Mathf.Max(1, monsterLevel) - 1) * ChapterExpScalePerLevel;

        /// <summary>
        /// 击杀怪物获得的经验（要求 8）：基础值 (10 + 5*等级) × 类型倍率 × 档次倍率 × 关卡经验补偿，
        /// 四舍五入取整。
        /// </summary>
        public static int MonsterExpReward(int monsterLevel, MonsterType type, MonsterTier tier) =>
            Mathf.RoundToInt((10 + 5 * monsterLevel)
                             * MonsterExpFactorByType[(int)type]
                             * MonsterTierExpFactor[(int)tier]
                             * ChapterExpFactor(monsterLevel));

        // ==================== 战斗 ====================

        /// <summary>
        /// 百分比减伤公式的常数项：K = DamageReductionBase + DamageReductionPerLevel × 防御方等级。
        /// 减伤率 = 防御 / (防御 + K)，防御收益递减且永不免疫。
        /// 旧公式「攻击 - 防御×0.5」已废弃。
        /// </summary>
        public static float DamageReductionBase = 100f;

        /// <summary>K 的等级成长：等级越高，同样的防御值提供的减伤率越低。</summary>
        public static float DamageReductionPerLevel = 20f;

        /// <summary>伤害随机浮动下限（0.9 ~ 1.1）。</summary>
        public static float DamageRandomMin = 0.9f;

        /// <summary>伤害随机浮动上限。</summary>
        public static float DamageRandomMax = 1.1f;

        /// <summary>伤害下限：无论减伤多高，每次攻击至少造成这么多伤害。</summary>
        public static int MinDamage = 1;

        /// <summary>
        /// 由防御与防御方等级换算减伤率（0 ~ 1）。
        /// 关卡制下怪物等级不再恒等于主角等级（见 ChapterMonsterLevelStep），
        /// 所以越级打怪时这个公式里的等级项才真正起作用。
        /// armorPen 是攻击方的护甲穿透（0.1 = 无视 10% 防御），在代入公式**之前**先折扣防御值，
        /// 这样穿透的收益天然也是递减的，不会在后期失控。
        /// </summary>
        public static float DamageReduction(int defense, int defenderLevel, float armorPen = 0f)
        {
            if (defense <= 0) return 0f;
            float effective = defense * (1f - Mathf.Clamp01(armorPen));
            if (effective <= 0f) return 0f;
            return effective / (effective + DamageReductionBase + DamageReductionPerLevel * defenderLevel);
        }

        /// <summary>怪物死亡到下一只出现的间隔秒数（要求 1）。</summary>
        public static float MonsterRespawnDelay = 0.5f;

        /// <summary>主角阵亡后自动复活的等待秒数。</summary>
        public static float PlayerReviveDelay = 1.5f;

        /// <summary>攻击间隔基准值，速度参与换算（要求 5）。</summary>
        public static float AttackIntervalBase = 2.0f;

        /// <summary>速度换算除数：基础攻速 = (1 + Speed / Divisor) / Base。</summary>
        public static float AttackSpeedDivisor = 20f;

        /// <summary>
        /// 攻速软上限（次/秒）：低于它的攻速完全线性；超过后改走渐近曲线，
        /// 只继续收窄到 <see cref="AttackSpeedHardCap"/>。这样高速度 / 多攻速词条
        /// 不会像旧公式那样在 5 次/秒处硬撞墙，也不会让「多一条攻速」变成零收益。
        /// </summary>
        public static float AttackSpeedSoftCap = 3.5f;

        /// <summary>攻速渐近上限（次/秒）：软上限之后永远到不了这个值，但可以无限接近。</summary>
        public static float AttackSpeedHardCap = 5.0f;

        /// <summary>
        /// 攻击间隔绝对下限。正常情况下由攻速硬上限（1/5=0.2s）决定；
        /// 保留这个字段是给极端配置兜底，避免浮点误差把间隔压到 0。
        /// </summary>
        public static float MinAttackInterval = 0.2f;

        /// <summary>
        /// 由速度与攻速加成换算每秒出手次数：
        /// 先算线性部分 raw = (1 + Speed/Divisor) / Base × (1 + 攻速加成)，
        /// 低于软上限直接使用；超过后按 1-exp 渐近到硬上限。
        /// 攻速加成必须在软上限之前乘进去，不能先压基础攻速再乘。
        /// </summary>
        public static float AttacksPerSecond(float speed, float haste = 0f)
        {
            float baseInterval = Mathf.Max(0.01f, AttackIntervalBase);
            float divisor = Mathf.Max(0.01f, AttackSpeedDivisor);
            float raw = (1f + Mathf.Max(0f, speed) / divisor) / baseInterval
                * (1f + Mathf.Max(0f, haste));

            float soft = Mathf.Max(0.1f, AttackSpeedSoftCap);
            float hard = Mathf.Max(soft + 0.01f, AttackSpeedHardCap);
            if (raw <= soft) return raw;
            return soft + (hard - soft) * (1f - Mathf.Exp(-(raw - soft) / (hard - soft)));
        }

        /// <summary>由速度与攻速加成换算单次攻击间隔（要求 5：速度影响攻击间隔）。</summary>
        public static float AttackInterval(float speed, float haste = 0f)
        {
            float rate = AttacksPerSecond(speed, haste);
            float interval = 1f / Mathf.Max(0.01f, rate);
            return Mathf.Max(Mathf.Max(0.01f, MinAttackInterval), interval);
        }

        // ==================== 主角成长 ====================

        public static int PlayerBaseMaxHp = 100;
        public static int PlayerBaseAttack = 10;
        public static int PlayerBaseDefense = 5;
        public static int PlayerBaseSpeed = 10;

        /// <summary>
        /// 每级增加的最大生命：100 + 35*(等级-1)。
        /// 配合百分比减伤公式（防御收益变弱、主角承受的伤害变高）上调，保证生存曲线跟得上。
        /// </summary>
        public static int PlayerMaxHpPerLevel = 35;
        public static int PlayerAttackPerLevel = 3;
        public static int PlayerDefensePerLevel = 2;
        public static int PlayerSpeedPerLevel = 1;

        /// <summary>主角初始暴击率 5%：不随等级成长，只有武器能加成。</summary>
        public static float PlayerBaseCritChance = 0.05f;

        /// <summary>主角初始暴击伤害 150%（暴击打出 1.5 倍伤害）：不随等级成长，只有武器能加成。</summary>
        public static float PlayerBaseCritDamage = 1.5f;

        /// <summary>暴击率上限：由 75% 放宽到 95%（副词条加入后 75% 很容易被顶满，留出成长空间）。</summary>
        public static float MaxCritChance = 0.95f;

        // ==================== 怪物成长 ====================

        // 以下数值在「百分比减伤」公式下调高：防御不再是线性抵扣，
        // 怪物需要更高的生命/防御与攻击成长才能形成压力。
        public static int MonsterBaseMaxHp = 55;
        public static int MonsterMaxHpPerLevel = 20;
        public static int MonsterBaseAttack = 9;
        public static float MonsterAttackPerLevel = 3.0f;
        public static int MonsterBaseDefense = 4;
        public static float MonsterDefensePerLevel = 2.4f;
        public static int MonsterBaseSpeed = 10;
        /// <summary>
        /// 怪物速度的每级成长。由 0.5 降到 0.3：0.5 的成长会让 Lv.100 的弓箭手
        /// 基础速度冲到 86（间隔 0.38s），叠上精英/Boss 与 Lv.110 的档次倍率后
        /// 直接撞 0.2s 上限，怪物出手频率和主角一样快，终局完全靠运气。
        /// </summary>
        public static float MonsterSpeedPerLevel = 0.3f;

        // ==================== 怪物类型 ====================

        /// <summary>按等级取权重档位下标（1~30 / 31~70 / 71~100）。表见 GameConfig.Tables.cs。</summary>
        public static int MonsterTypeTier(int level) =>
            level <= 30 ? 0 : (level <= 70 ? 1 : 2);

        // ==================== 关卡 / 章节（步骤 4a） ====================

        /// <summary>章节总数。第 N 章的怪物等级 = ChapterMonsterLevelStep×(N-1)+1 ~ ChapterMonsterLevelStep×N。</summary>
        public static int ChapterCount = 11;
        /// <summary>
        /// 第 1~10 章的整体压迫：每章再乘一档生命/攻击/防御。
        /// 之前这十章的稳态胜率是 100%，推章没有任何风险；加这一层让它变成「会死人、但不会卡死」。
        /// 第 11 章不吃这一层——它有自己的 ×3 终局档，再加会盖过 MaxTier 的调参口径。
        /// </summary>
        public static float ChapterPressureBase = 1.0f;

        /// <summary>
        /// 章节压迫的逐章增量（每章 +5.5%）。2026-10-23 由 4% 上调：五装备槽把裸装下界的
        /// 第 5~8 章抬到 87~92%、几乎与第 1 章持平，压迫线被削平了；上调后裸装下界恢复
        /// 「2~4 章 90~91% → 5~8 章 79/80/68/61% → 9~10 章 34/22%」的下降阶梯（300 次建样均值）。
        /// </summary>
        public static float ChapterPressurePerChapter = 0.055f;

        /// <summary>
        /// 章末 Boss 在章节压迫之上再乘的额外压迫（每章 +4%，2026-10-23 由 3% 上调），
        /// 把关底做成真正的检查点：裸装下界第 10 章章末 22%，真实推进（带强化/洗练）仍能过。
        /// </summary>
        public static float ChapterBossPressurePerChapter = 0.04f;

        /// <summary>章节压迫倍率：1~10 章随章节线性上升，第 11 章恒为 1。</summary>
        public static float ChapterPressure(int chapter, bool isBoss)
        {
            if (chapter >= ChapterCount) return 1f;
            float steps = Mathf.Max(1, chapter) - 1;
            float p = ChapterPressureBase + ChapterPressurePerChapter * steps;
            if (isBoss) p *= 1f + ChapterBossPressurePerChapter * steps;
            return p;
        }

        /// <summary>每章的关卡数。</summary>
        public static int StagesPerChapter = 10;

        /// <summary>章节之间的怪物等级步长（第 1 章 Lv.1~10、第 11 章 Lv.101~110）。</summary>
        public static int ChapterMonsterLevelStep = 10;

        /// <summary>
        /// 怪物等级 → 章节号。第 N 章的怪物等级落在 ChapterMonsterLevelStep×(N-1)+1 ~ ChapterMonsterLevelStep×N，
        /// 所以按「向上取整」从等级反推章节，不用把章节号一路传进怪物工厂。
        /// </summary>
        public static int ChapterOfMonsterLevel(int monsterLevel)
        {
            // ChapterMonsterLevelStep=0 会在这里除零，ChapterCount 越界会撞 ChapterMonsterNames 的固定长度。
            int step = Mathf.Max(1, ChapterMonsterLevelStep);
            int chapters = Mathf.Max(1, ChapterCount);
            return Mathf.Clamp(((Mathf.Max(1, monsterLevel) - 1) / step) + 1, 1, chapters);
        }

        /// <summary>怪物在它所属章节里的显示名（按类型取该章的族类名）。</summary>
        public static string MonsterNameOf(int monsterLevel, MonsterType type)
        {
            if (ChapterMonsterNames == null || ChapterMonsterNames.Length == 0) return "怪物";
            int chapterIndex = Mathf.Clamp(ChapterOfMonsterLevel(monsterLevel) - 1, 0, ChapterMonsterNames.Length - 1);
            string[] names = ChapterMonsterNames[chapterIndex];
            if (names == null || names.Length == 0) return "怪物";
            return names[Mathf.Clamp((int)type, 0, names.Length - 1)];
        }

        /// <summary>怪物在它所属章节里的属性偏向（生命 / 攻击 / 防御 / 速度）。</summary>
        public static float[] MonsterStatBiasOf(int monsterLevel)
        {
            if (ChapterStatBias == null || ChapterStatBias.Length == 0) return new[] { 1f, 1f, 1f, 1f };
            int chapterIndex = Mathf.Clamp(ChapterOfMonsterLevel(monsterLevel) - 1, 0, ChapterStatBias.Length - 1);
            return ChapterStatBias[chapterIndex];
        }

        /// <summary>第 1 章每关的击杀目标基数。</summary>
        public static int StageKillTargetBase = 3;

        /// <summary>击杀目标随章节增加的上限（每 3 章 +1，最多 +3）。</summary>
        public static int StageKillTargetMaxBonus = 3;

        /// <summary>从第几章开始章末关是 Boss（之前的章末关只给精英）。</summary>
        public static int FirstBossChapter = 2;

        /// <summary>通关结算的停留时间（秒，游戏内时间）。</summary>
        public static float StageClearDelay = 3f;

        // ==================== 首通奖励（步骤 4c） ====================

        /// <summary>首通第 1 章某关的金币奖励。</summary>
        public static int FirstClearGoldBase = 120;

        /// <summary>首通金币随章节的增量（第 N 章 = base + step × (N-1)）。</summary>
        public static int FirstClearGoldPerChapter = 80;

        /// <summary>首次通关某个章末关（打完一整章）的额外金币奖励。</summary>
        public static int ChapterClearGoldBonus = 600;

        /// <summary>首通奖励横幅在屏幕上停留的秒数（真实时间）。</summary>
        public static float BannerDuration = 3.2f;

        // ==================== 最高档（Lv.110 精英 / Boss） ====================

        /// <summary>梯度最高档的等级（主角满级 100 时精英 / Boss 的等级）。</summary>
        public static int MaxTierLevel = 110;

        /// <summary>
        /// 最高档（Lv.110）的额外倍率：叠加在档次倍率（精英 ×1.5 / Boss ×2.5）之上。
        /// 三项统一 ×3，也就是「全属性 ×3」。拆成三个常量只是为了以后能单独调
        /// （例如只压低攻击倍率，给 Boss 战留容错空间）。
        /// </summary>
        public static float MaxTierHpFactor = 3.0f;
        public static float MaxTierAttackFactor = 3.0f;
        public static float MaxTierDefenseFactor = 3.0f;

        /// <summary>
        /// 终局 BOSS（Lv.110 章末关底）额外的全属性倍率：战斗探针实测，只叠 MaxTier ×3 时
        /// 传说装备 + 巅峰 26 的玩家平均 7 次挑战就能打下来——终局只硬一分钟。
        /// 这一层再抬 60%，把 11-10 变成一个明确的「关卡门」（不是巅峰门槛）。
        /// 2026-10-23 复测（EndgamePacing，种子 20261018~20261022）：首通第 2~62 次挑战、
        /// 巅峰 P15~P58（中位 P31）、战斗 0.2~9.9 min，不是原注释说的「巅峰 100 之后」——
        /// 这个说法在两槽时代（2026-10-19）实测就是 P23，从未成立。
        /// 抬倍率能把首通推到 1.8→P44 / 3.0→P50~P71 / 5.0→P75~P90，但同一件 BOSS 也是
        /// 新档 11-10 的门：30 种子真实推进的通关率 1.6→25/30、1.8→17、2.2→16、3.0→9、5.0→3，
        /// 所以定格 1.6；终局的门由巅峰曲线 + 无尽/转生承担。详见 _balance/balance-analysis.md §3.5.4a。
        /// 只作用于 Boss，不碰 11-1~11-9 的精英。
        /// </summary>
        public static float FinalBossStatFactor = 1.6f;

        /// <summary>最高档的额外速度倍率（速度对攻击间隔是非线性的，所以单独给，且比全属性保守）。</summary>
        public static float MaxTierSpeedFactor = 2.0f;

        // ==================== 怪物词缀（要求 3） ====================

        /// <summary>生成怪物时附加 1 个随机词缀的概率：30%。</summary>
        public static float AffixChance = 0.30f;

        /// <summary>吸血词缀：造成伤害的 30% 转化为自身生命。</summary>
        public static float LifestealAffixPercent = 0.30f;

        /// <summary>护盾词缀：开场护盾 = 最大生命 × 30%。</summary>
        public static float ShieldAffixPercent = 0.30f;

        /// <summary>剧毒词缀：每次攻击附加目标最大生命 × 2% 的伤害。</summary>
        public static float PoisonAffixPercent = 0.02f;

        // ==================== 掉落（要求 11、12） ====================

        /// <summary>击杀后掉落装备的概率：30%。</summary>
        public static float DropChance = 0.30f;

        /// <summary>每多一个已解锁装备槽，普通怪掉落率增加多少（2 槽 0.30、4 槽 0.40、5 槽 0.45）。</summary>
        public static float DropChancePerExtraSlot = 0.05f;

        /// <summary>掉落时强制指向「空槽或评分最弱槽」的概率（智能掉落），其余均匀随机。</summary>
        public static float SmartDropChance = 0.30f;

        /// <summary>
        /// 把品质收进固定表范围。正常掉落/读档已经清洗过品质，但公开 API
        /// 仍可能收到直接构造的越界值；越界时回落到普通品质（而不是夹到最高档，
        /// 否则一次坏输入就会白送传说）。
        /// </summary>
        public static EquipmentQuality ClampQuality(EquipmentQuality quality)
        {
            if (QualityNames == null || QualityNames.Length == 0) return EquipmentQuality.Common;
            int i = (int)quality;
            if (i < 0 || i >= QualityNames.Length) return EquipmentQuality.Common;
            return quality;
        }

        /// <summary>按品质抽取一个浮动属性倍率（含上限，均匀分布）。</summary>
        public static float RollQualityMultiplier(EquipmentQuality quality)
        {
            int i = (int)ClampQuality(quality);
            return Random.Range(QualityMultiplierMin[i], QualityMultiplierMax[i]);
        }

        // ==================== 金币（卖装备所得） ====================

        /// <summary>卖价的等级基数。</summary>
        public static int SellBaseGold = 5;

        /// <summary>卖价的等级成长：装备每高 1 级，卖价多这么多金币。</summary>
        public static float SellGoldPerLevel = 3f;

        /// <summary>
        /// 卖出一件装备所得金币：先按装备等级线性成长，再乘品质倍率。
        /// 例：Lv.30 普通 = (5 + 3*29) × 1 = 92；同等级传说 = 92 × 16 = 1472。
        /// </summary>
        public static int SellPrice(int level, EquipmentQuality quality) =>
            Mathf.Max(1, Mathf.RoundToInt(
                (SellBaseGold + SellGoldPerLevel * Mathf.Max(0, level - 1)) *
                SellQualityMultipliers[(int)ClampQuality(quality)]));

        // ==================== 装备强化（步骤 2） ====================
        // 强化只提升主属性：武器加攻击，衣服加防御与生命。
        // 明确**不**碰武器主词条里的暴击率/暴击伤害，也不碰任何副词条——
        // 那两类已经由「品质决定条数 + 掉落随机」在管，再挂一个可无限投入的
        // 成长轴，暴击流会失去上限，评分与自动换装的口径也会跟着失稳。
        //
        // 每级 +3% 走的是**乘算**（相对这件装备自己的主属性），不是加算。
        // 乘算的好处是同品质强弱不同的两件装备，强化后强弱关系不会翻转：
        // 加算会把「强装备的领先量」按绝对值稀释掉，越强化越趋同。
        // +15 满级 = ×1.45，单级涨幅小、看得到、不破坏现有平衡档位。

        /// <summary>强化每级的主属性加成（乘算，0.03 表示 +3%）。</summary>
        public static float EnhanceBonusPerLevel = 0.03f;

        /// <summary>强化等级上限。</summary>
        public static int EnhanceMaxLevel = 15;

        /// <summary>强化费用的基础值（从 +0 强化到 +1 的基准价，未乘品质系数）。</summary>
        public static int EnhanceBaseCost = 150;

        /// <summary>强化费用的等级成长：当前强化等级每高 1 级，本次费用多这么多。</summary>
        public static int EnhanceCostPerLevel = 300;

        /// <summary>
        /// 从 enhanceLevel 强化到 enhanceLevel+1 所需金币：
        /// (EnhanceBaseCost + EnhanceCostPerLevel × 当前等级) × 品质系数。
        /// 例：普通 +0→+1 = 150 × 0.6 = 90；传说 +14→+15 = (150 + 300×14) × 1.6 = 6,960。
        /// 单件从 +0 强化到 +15 的累计花费：普通约 20,250，传说约 54,000。
        /// 这个量级是有意的——强化是金币的主要出口，装备掉落卖钱源源不断，
        /// 若费用太低，中期开始金币就会溢出一地而没有用途。
        /// </summary>
        public static int EnhanceCost(int enhanceLevel, EquipmentQuality quality) =>
            Mathf.Max(1, Mathf.RoundToInt(
                (EnhanceBaseCost + EnhanceCostPerLevel * Mathf.Max(0, enhanceLevel)) *
                EnhanceQualityFactors[(int)ClampQuality(quality)]));

        // ==================== 装备洗练（步骤 3） ====================
        // 洗练只重抽**副词条**：条数不变，种类与数值按掉落同分布重掷。
        // 主属性、武器主词条、品质、强化等级都不动——它们各自已经有成长轴（强化）
        // 或身份（品质 / 主词条），再让洗练去碰等于给一条曲线开两个口子。
        //
        // 条数保持不变是硬约束：优秀品质的条数是 1~2 随机，如果连条数一起重抽，
        // 一件 2 条的优秀装备会被洗成 1 条，花金币反而掉一条词条，没人会接受。
        //
        // 费用随「同一件洗过几次」线性上涨（1 + 0.9n），不是指数。
        // 指数的效果等于给系统设一个隐形次数上限；而这个系统的价值恰恰在
        // 「接近上限的最后几口最贵、但确实吃得到」，线性上涨让玩家自己决定停在哪。
        // 首次费用刻意低于「强化 +1 级」，让人愿意试一次；
        // 31 次累计约 124 万（Lv.110 传说），把终局那笔没处花的金币吃掉一截。
        // 递增系数 2026-10-21 由 0.6 提到 0.9：只压「反复洗同一件」这条路，首洗价不变。
        // 依据见 docs/equipment-reforge.md §5.6（五槽复测：新档洗练已吃掉过半金币收入，
        // 端局一套洗到 86% 平均 96.3 万 → 133.7 万）。

        /// <summary>洗练费用的基础值（Lv.1 的基准价，未乘品质系数）。</summary>
        public static int ReforgeBaseCost = 200;

        /// <summary>洗练费用的等级成长：装备等级每高 1 级，本次基价多这么多。</summary>
        public static int ReforgeCostPerLevel = 14;

        /// <summary>同一件装备每洗一次，本次费用在基价上多这么多比例（0.9 表示 +90%）。</summary>
        public static float ReforgeCostGrowth = 0.9f;

        /// <summary>
        /// 锁词条的单价倍率：每多锁一条，本次费用乘一次它（默认 3 → ×3 / ×9 / ×27）。
        /// 用指数而不是加价：锁 k 条会把要重抽的词条从 4 条压到 4−k 条，
        /// 达到同一质量的中位次数掉 10~30 倍（见 docs/equipment-reforge.md §10.6），
        /// 线性加价根本压不住「锁得越多越划算」，锁词条会变成没有代价的默认操作。
        /// </summary>
        public static float ReforgeLockFactor = 3f;

        /// <summary>
        /// 洗练一次的费用（不锁词条）：
        /// (ReforgeBaseCost + ReforgeCostPerLevel × (level - 1)) × 品质系数 × (1 + 递增 × 已洗次数)。
        /// 例：传说 Lv.110 第 1 次 = 1,726 × 1.6 = 2,762；第 11 次 = 2,762 × (1 + 0.9×10) = 27,616。
        /// 品质系数直接复用 EnhanceQualityFactors，不再造第二套表。
        /// </summary>
        public static int ReforgeCost(int level, EquipmentQuality quality, int reforgeCount) =>
            ReforgeCost(level, quality, reforgeCount, 0);

        /// <summary>
        /// 洗练一次的费用，带锁词条溢价：锁 n 条再乘 ReforgeLockFactor^n。
        /// 锁定条数由调用方保证不超过实际词条条数（Models 的 LockedAffixCount 已裁掉无效位）。
        /// </summary>
        public static int ReforgeCost(int level, EquipmentQuality quality, int reforgeCount, int lockedAffixes) =>
            Mathf.Max(1, Mathf.RoundToInt(
                (ReforgeBaseCost + ReforgeCostPerLevel * Mathf.Max(0, level - 1)) *
                EnhanceQualityFactors[(int)ClampQuality(quality)] *
                (1f + ReforgeCostGrowth * Mathf.Max(0, reforgeCount)) *
                Mathf.Pow(ReforgeLockFactor, Mathf.Max(0, lockedAffixes))));

        // ==================== 装备基础值（要求 9、10、13） ====================

        /// <summary>
        /// 武器主词条 = 攻击 + （暴击率 或 暴击伤害，随机二选一）；
        /// 攻速 / 全属性 仍然只走副词条。衣服主词条是防御 + 生命。
        /// </summary>
        public static int WeaponBaseAttack = 5;
        public static float WeaponAttackPerLevel = 2.0f;

        /// <summary>武器主暴击率的候选值：基础 +1%，每级 +0.1%，最后乘品质倍率。</summary>
        public static float WeaponBaseCritChance = 0.01f;
        public static float WeaponCritChancePerLevel = 0.001f;

        /// <summary>
        /// 武器主暴击伤害的候选值：基础 +10%，每级 +0.195%，最后乘品质倍率。
        /// 每级成长由 1% 压到 0.195%，让 Lv.110 传说（倍率上限 4.8）落在约 150%。
        /// 原公式（每级 1%）Lv.110 传说能给到 ~576%，配上 52% 的暴击率上限后
        /// 「+1% 暴击率」的边际 DPS 会是「+1% 暴击伤害」的十几倍，副词条池直接失衡。
        /// </summary>
        public static float WeaponBaseCritDamage = 0.10f;
        public static float WeaponCritDamagePerLevel = 0.00195f;

        /// <summary>衣服：加防御和生命。</summary>
        public static int ArmorBaseDefense = 3;
        public static float ArmorDefensePerLevel = 1.2f;
        public static int ArmorBaseHp = 20;
        public static float ArmorHpPerLevel = 8f;

        // ==================== 头盔 / 鞋 / 饰品（2026-10 五槽） ====================
        // 小件主属性按「同等级衣服」的比例给，并统一乘品质倍率；
        // 鞋的速度刻意不乘品质倍率，否则传说鞋一件就能给 +35 速度，把攻速曲线整条冲掉。

        /// <summary>头盔生命 = 该系数 × 衣服生命基础值。</summary>
        public static float HelmetHpFactor = 0.25f;

        /// <summary>头盔防御 = 该系数 × 衣服防御基础值。</summary>
        public static float HelmetDefenseFactor = 0.12f;

        /// <summary>鞋的基础速度（+0 普通）。</summary>
        public static int BootsBaseSpeed = 3;
        public static float BootsSpeedPerLevel = 0.02f;
        public static float BootsSpeedPerQuality = 0.75f;

        /// <summary>鞋的攻速加成 = 基础 + 每级成长，再乘品质档位系数；与速度一样不乘品质倍率。</summary>
        public static float BootsBaseHaste = 0.012f;
        public static float BootsHastePerLevel = 0.00005f;
        public static float BootsHastePerQuality = 0.15f;

        /// <summary>饰品暴击伤害 = 基础 + 每级成长，再乘品质倍率。</summary>
        public static float AccessoryBaseCritDamage = 0.012f;
        public static float AccessoryCritDamagePerLevel = 0.0002f;

        /// <summary>饰品护甲穿透 = 基础 + 每级成长，再乘品质倍率。</summary>
        public static float AccessoryBaseArmorPen = 0.005f;
        public static float AccessoryArmorPenPerLevel = 0.00005f;

        /// <summary>小件副词条的数值缩放（大件 1.0，小件 0.5）。</summary>
        public static float MinorAffixScale = 0.5f;

        /// <summary>头盔 / 鞋 / 饰品解锁所需通关的章节（3-10 / 6-10 / 11-10）。</summary>
        public static int HelmetUnlockChapter = 3;
        public static int BootsUnlockChapter = 6;
        public static int AccessoryUnlockChapter = 11;

        // ==================== 装备副词条 ====================

        /// <summary>
        /// 副词条的等级缩放：最终值 = 基础随机值 × (1 + 装备等级 × 该系数)。
        /// 由 0.01 下调到 0.005（满级从 ×2 降到 ×1.5）；并且**不再乘品质倍率**——
        /// 品质已经通过「条数」体现（普通 0 条 … 传说 4 条），再乘一次会数值爆炸。
        /// </summary>
        public static float EquipAffixLevelScale = 0.005f;

        // ==================== 装备评分 ====================
        // 评分口径已从「线性加权求和」换成「期望 DPS + 生存折算」，实现在 Scripts/EquipmentScore.cs：
        //   评分 = Δ(对标准怪的期望 DPS) + Δ(有效生命) ÷ 8（增量口径：相对同槽位空着多出来的战力）
        // 换口径的原因：实战 DPS ∝ 攻击 × 攻速 × (1 + 暴击率 × (暴击倍率 - 1)) 是连乘关系，
        // 线性权重表达不了乘积；也表达不了「装备属性必须和角色当前属性一起算」——
        // 角色暴伤 717% 时 +50% 暴击率很值钱，角色暴伤掉回 ~200% 时就廉价。
        // 实测 40 把随机传说武器：旧线性权重与木桩实测伤害排序的 Spearman 相关 0.27，
        // 期望 DPS 口径 0.91。这组数是 2026-10-17 的审计实验测出来的；当时读的那批
        // ScoreWeight* 权重常量（以及 LateGameProbe 里的 ScoreAudit / ScoreRankAudit /
        // ScoreDummyAudit / ScoreWeightSearch）已在 2026-10-21 一并删除，结论留在
        // Scripts/EquipmentScore.cs 的类注释里。

        /// <summary>评分用的标准对手等级：固定 Lv.100 普通近战怪，保证不同装备的分数可比。</summary>
        public static int ScoreStandardLevel = 100;

        // ==================== 商店：生命药剂 ====================

        /// <summary>精英战前是否回满血。关底 Boss 永远回满；精英默认不回——
        /// 让「打小怪 → 打精英」变成一条连续的消耗链，中途倒下就是真的阵亡（2026-10-18）。</summary>
        /// <summary>开战前（精英 / Boss）是否回满血。默认 false：整关是一条连续的消耗链，
        /// 打小怪掉的血要一直带到关底——这才是「打不过就回去刷小怪」的循环。
        /// 想恢复旧手感（每场关底满血）把它改成 true。</summary>
        public static bool HealBeforeFight = false;
        /// <summary>生命药剂的持有上限。</summary>
        public static int PotionMaxCount = 5;

        /// <summary>喝一瓶恢复的最大生命比例（30%）。</summary>
        public static float PotionHealPercent = 0.30f;

        /// <summary>药剂价格 = PotionPriceBase + PotionPricePerLevel × 主角等级。</summary>
        public static int PotionPriceBase = 20;
        public static int PotionPricePerLevel = 5;

        /// <summary>按主角等级算出的当前售价。</summary>
        public static int PotionPrice(int playerLevel) => PotionPriceBase + PotionPricePerLevel * playerLevel;

        /// <summary>自动喝药的血线：生命低于最大生命的该比例时喝一瓶。</summary>
        public static float PotionAutoUseHpPercent = 0.40f;

        /// <summary>喝药后的冷却秒数（按游戏内时间计，受速度档位影响）。</summary>
        public static float PotionCooldown = 3f;

        /// <summary>
        /// 商店开放时长（秒，**真实时间**，不受速度档位影响）。
        /// 商店只在精英 / Boss 出现前开放，到点自动开战。
        /// </summary>
        public static float ShopDuration = 3f;

        // ==================== 巅峰等级（满级后的成长线） ====================

        /// <summary>巅峰等级上限的代码默认值；实际值以 game_config.json 为准（2026-10-19 已设为 1000）。</summary>
        public static int MaxParagonLevel = 1000;

        /// <summary>每级巅峰提供的攻击 / 防御 / 生命加成。</summary>
        public static int ParagonAttackPerLevel = 1;
        public static int ParagonDefensePerLevel = 1;
        public static int ParagonHpPerLevel = 50;

        /// <summary>满级后升一级巅峰所需经验 = ParagonExpBase + ParagonExpPerLevel × 当前巅峰等级。</summary>
        public static int ParagonExpBase = 2000;
        public static int ParagonExpPerLevel = 200;

        /// <summary>
        /// 巅峰经验的二次项：每级额外 +q×p²。
        /// 线性项只会让「越升越快」——怪给的经验是常数，需求只涨 200/级，
        /// 一只 Lv.110 Boss 就够升一级。加上二次项后 P100 一级约 22k+200k，
        /// 越往后越慢，整条 P0→P200 的纯战斗时间才撑得起「约两小时」的终局（含刷怪/商店约 3.8 小时）。
        ///
        /// 系数是实测校准出来的，不是拍的：EndgamePacing 用「会挑技能、会把余钱砸满强化」的
        /// 最优玩家口径跑 11-10 循环，q=12 时 P0→P200 只要 76~77 分钟（2026-10-18 实测；
        /// 更早的探针不强化、只有 6 张随机技能，才把同一件事量成 110~137 分钟）。
        /// 按 1.57× 提到 20 后总需求 5,731 万经验，满级回到约两小时（108 / 114 分钟）。别随手往下调。
        /// </summary>
        public static float ParagonExpQuadratic = 20f;

        public static int ParagonExpToNext(int paragonLevel) =>
            ParagonExpBase + ParagonExpPerLevel * paragonLevel
            + Mathf.RoundToInt(ParagonExpQuadratic * paragonLevel * paragonLevel);

        /// <summary>每多少级巅峰解锁一个技能（只影响数量，不影响技能强度）。</summary>
        public static int ParagonPassiveEvery = 10;

        /// <summary>技能等级：由巅峰等级推导，每 ParagonPassiveEvery 级 +1。</summary>
        public static int SkillLevel(int paragonLevel) =>
            1 + Mathf.Max(0, paragonLevel) / Mathf.Max(1, ParagonPassiveEvery);

        // ==================== 技能 ====================

        /// <summary>主动 / 被动 / 触发 三类各自最多装备的数量（合计 6 个槽位）。</summary>
        public static int SkillSlotsPerKind = 2;

        /// <summary>
        /// 巅峰每 ParagonPassiveEvery 级触发一次「技能三选一」时的候选数量。
        /// 由「随机直接送一个」改成三选一：随机送技能会让同巅峰的强度方差极大
        /// （实测巅峰 20 的 Lv.110 Boss 胜率在 0.4%~24% 之间跳），玩家也没有任何决策空间。
        /// </summary>
        public static int SkillChoiceCount = 3;

        /// <summary>主动技能的公共冷却（秒，游戏内时间）。</summary>
        public static float SkillGlobalCooldown = 1f;

        /// <summary>技能等级对数值的增幅：实际效果 = 基础值 × (1 + (等级-1) × 该系数)。</summary>
        public static float SkillLevelScale = 0.0f;

        /// <summary>把技能基础值按技能等级放大。</summary>
        public static float ScaleSkillValue(float baseValue, int skillLevel) =>
            baseValue * (1f + (skillLevel - 1) * SkillLevelScale);

        // ==================== 无尽模式 ====================

        /// <summary>无尽第 1 层的怪物等级。默认从终局档（Lv.110）起步。</summary>
        public static int EndlessBaseMonsterLevel = 110;

        /// <summary>每往下一层怪物等级 +N。默认 +6：第 50 层是 Lv.404（探针基线，见 docs/endless-mode.md §12）。</summary>
        public static int EndlessMonsterLevelStep = 6;

        /// <summary>第 1 层需要击杀的怪物数。</summary>
        public static int EndlessKillTargetBase = 3;

        /// <summary>每多少层，击杀目标 +1。</summary>
        public static int EndlessKillTargetEvery = 5;

        /// <summary>击杀目标上限（层数再高也不会更久）。</summary>
        public static int EndlessKillTargetMax = 6;

        /// <summary>每多少层一个 BOSS 层。</summary>
        public static int EndlessBossEvery = 5;

        /// <summary>
        /// 无尽怪物的额外属性倍率（只乘生命/攻击/防御，不乘速度）。
        /// 战役里 Lv.110 的终局档已经含 MaxTier×3 与章节压迫；无尽不吃那两层，
        /// 改用这一个显式倍率，强度才可单独调。
        /// 2026-10-25 D2：2.8→3.4。50 种子满倍率模板首死中位 296→240（P10 227 / P90 250），
        /// 随机五槽 220→181；235~250 的目标按「理想配装」口径保留，随机配装按设计更早死。
        /// </summary>
        public static float EndlessBaseFactor = 3.4f;

        public static int EndlessFloorGoldBase = 300;
        public static int EndlessFloorGoldPerFloor = 40;
        public static int EndlessFirstClearGoldBase = 1000;
        public static int EndlessFirstClearGoldPerFloor = 150;

        /// <summary>清层后到下一层怪物出现前的停顿（游戏内秒）。</summary>
        public static float EndlessFloorClearDelay = 2.0f;

        /// <summary>重新挑战时从「最高层往回退几层」起步。</summary>
        public static int EndlessCheckpointOffset = 4;

        /// <summary>无尽掉落与击杀经验使用的等级上限。</summary>
        public static int EndlessDropLevelCap = 150;

        // ==================== 转生（docs/rebirth.md） ====================

        /// <summary>解锁轮回所需的巅峰等级。</summary>
        public static int RebirthUnlockParagon = 100;

        /// <summary>战斗本能每级：攻击 / 防御 / 最大生命 +1%。</summary>
        public static float RebirthBattlePerRank = 0.01f;

        /// <summary>历练每级：经验获取 +60%（2026-10-22 由 20% 提到 60%，把第 2 世压进 75~85%）。</summary>
        public static float RebirthExpPerRank = 0.60f;

        /// <summary>财运每级：正向金币 +20%。</summary>
        public static float RebirthGoldPerRank = 0.20f;

        /// <summary>财运每级：掉落率 +2%（加算）。</summary>
        public static float RebirthDropPerRank = 0.02f;

        /// <summary>传承每级：下次转生起始等级 +3（2026-10-22 由 2 提到 3，满轮回起始 Lv16）。</summary>
        public static int RebirthLegacyLevelPerRank = 3;

        /// <summary>行囊每级：下次转生起始药剂 +1。</summary>
        public static int RebirthAlchemyPotionPerRank = 1;

        /// <summary>行囊每级：下次转生起始金币 +500。</summary>
        public static int RebirthAlchemyGoldPerRank = 500;

        public static int RebirthBattleCostBase = 3;
        public static int RebirthGrowthCostBase = 2;
        public static int RebirthFortuneCostBase = 2;
        public static int RebirthLegacyCostBase = 4;
        public static int RebirthAlchemyCostBase = 1;

        /// <summary>每多少巅峰折 1 枚印记（上限 10）。</summary>
        public static int RebirthMarkParagonStep = 100;

        /// <summary>无尽每多少层折 1 枚印记（上限 5）。</summary>
        public static int RebirthMarkFloorStep = 50;

        /// <summary>金币印记的基准：每翻一倍多 1 枚（上限 14）。</summary>
        public static int RebirthMarkGoldBase = 100000;

        // ==================== 存档（要求 17） ====================

        public const string SaveKey = "AutoBattleDemo.Save.v1";

        /// <summary>自动存档间隔（秒）。</summary>
        public static float AutoSaveInterval = 5f;

        // ==================== 战斗日志 ====================

        /// <summary>战斗日志最多保留的条数。</summary>
        public static int MaxLogEntries = 60;
    }
}

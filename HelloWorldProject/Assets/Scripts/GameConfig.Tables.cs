using UnityEngine;

namespace AutoBattle
{
    /// <summary>
    /// GameConfig 的表数据部分（31 个固定长度的数组）。
    ///
    /// 为什么单独一个文件：
    /// <para>
    /// 这些数组**不参与外部配置加载**——<c>game_config.json</c> 走的是
    /// <see cref="GameConfig.Apply"/> 的逐字段反射，只认 GameConfigData 里声明过的
    /// 标量字段，数组/二维数组既没有镜像字段也不会被 ExportJson 导出。
    /// 也就是说：改这里的数字必须改代码重编译，改 json 是没用的。
    /// </para>
    /// <para>
    /// 把它们和可热改的标量分开放，是为了让「调平衡到底该改哪个文件」一眼可见——
    /// 混在一起时，很容易对着 <c>MonsterTierStatFactor</c> 改 json 然后疑惑为什么没生效。
    /// </para>
    /// <para>
    /// 字段之间没有初始化顺序依赖（都是字面量），且静态构造函数会在所有字段
    /// 初始化完成后才执行，所以 partial 拆文件不改变初始化结果。
    /// </para>
    /// </summary>
    public static partial class GameConfig
    {
        // ==================== 战斗：击杀回复 ====================

        /// <summary>
        /// 击杀后回复的最大生命比例，索引与 MonsterTier 一致（普通 / 精英 / Boss）。
        ///
        /// 普通怪刻意设低（10%）：配合「阵亡后自动复活」，死亡是节奏事件而不是终局，
        /// 回血只负责延长单条命。精英 50%、Boss 100%（回满）——
        /// 精英 / Boss 本身是硬仗，打完给一次实打实的补给，才不会赢完就残血等死。
        /// </summary>
        public static readonly float[] MonsterTierHealOnKillPercent = { 0.10f, 0.50f, 1.00f };

        // ==================== 怪物类型（要求 3）====================

        /// <summary>
        /// 类型出现权重，列顺序同为 MonsterType（近战 / 远程 / 暴击怪）。
        /// 行按等级分档：0 = 1~30 级，1 = 31~70 级，2 = 71~100 级。等级越高，越容易遇到难缠的类型。
        /// <para>
        /// 关卡制下传进来的是**关卡怪级**，不是主角等级——难度既然已经由关卡钉住，
        /// 类型权重也该跟着关卡走，否则主角练高之后回头刷第 1 章，会刷出终局档位的怪。
        /// </para>
        /// </summary>
        public static readonly float[][] MonsterTypeWeights =
        {
            new float[] { 0.60f, 0.25f, 0.15f }, // 1~30：  60 / 25 / 15
            new float[] { 0.45f, 0.30f, 0.25f }, // 31~70： 45 / 30 / 25
            new float[] { 0.35f, 0.30f, 0.35f }, // 71~100：35 / 30 / 35
        };

        /// <summary>
        /// 三类怪物的属性系数（乘在基础怪物属性上），顺序同为 MonsterType。
        /// 近战：厚血厚防、攻击低速度慢；远程：脆皮但高攻高速；暴击怪：面板中庸、暴击极高。
        /// </summary>
        public static readonly float[] MonsterHpFactorByType = { 1.60f, 0.55f, 1.00f };
        public static readonly float[] MonsterDefenseFactorByType = { 1.60f, 0.45f, 1.00f };
        public static readonly float[] MonsterAttackFactorByType = { 0.75f, 1.45f, 1.00f };
        public static readonly float[] MonsterSpeedFactorByType = { 0.75f, 1.45f, 1.00f };

        /// <summary>怪物暴击率：近战/远程 5%，暴击怪 45%（固定值，不随等级成长）。</summary>
        public static readonly float[] MonsterCritChanceByType = { 0.05f, 0.05f, 0.45f };

        /// <summary>怪物暴击伤害：近战/远程 150%，暴击怪 200%（由 300% 下调，原值让盗贼 Boss 几乎无解）。</summary>
        public static readonly float[] MonsterCritDamageByType = { 1.50f, 1.50f, 2.00f };

        /// <summary>类型经验倍率：近战 1.0（基础怪）、远程 1.15、暴击怪 1.35——越难打给得越多。</summary>
        public static readonly float[] MonsterExpFactorByType = { 1.00f, 1.15f, 1.35f };

        // ==================== 关卡 / 章节（步骤 4a） ====================

        /// <summary>
        /// 每章怪物族类的显示名。列顺序同 MonsterType（近战 / 远程 / 暴击怪），行 = 章节（第 1 章起）。
        /// <para>
        /// 关卡制把难度钉死在关卡等级上，如果 11 章都是同一批哥布林，玩家推章时只会看到数字变大。
        /// 每章换一族：第 3 章骷髅、第 6 章蛛魔、第 9 章恶魔，推进新章时名字和手感一起变。
        /// </para>
        /// </summary>
        public static readonly string[][] ChapterMonsterNames =
        {
            new string[] { "哥布林战士",   "哥布林弓箭手",   "哥布林盗贼"   }, // 第 1 章  Lv.10
            new string[] { "狗头人步兵",   "狗头人投石手",   "狗头人潜行者" }, // 第 2 章  Lv.20
            new string[] { "骷髅卫士",     "骷髅弓手",       "骷髅法师"     }, // 第 3 章  Lv.30
            new string[] { "食人魔蛮兵",   "食人魔猎手",     "食人魔暴徒"   }, // 第 4 章  Lv.40
            new string[] { "兽人步兵",     "兽人弩手",       "兽人狂战士"   }, // 第 5 章  Lv.50
            new string[] { "蛛魔守卫",     "蛛魔喷丝者",     "蛛魔毒牙"     }, // 第 6 章  Lv.60
            new string[] { "亡灵骑士",     "亡灵射手",       "亡灵巫师"     }, // 第 7 章  Lv.70
            new string[] { "石像鬼卫兵",   "石像鬼掷矛手",   "石像鬼暗杀者" }, // 第 8 章  Lv.80
            new string[] { "恶魔卫士",     "恶魔火法师",     "恶魔刺客"     }, // 第 9 章  Lv.90
            new string[] { "深渊守卫",     "深渊咏者",       "深渊魔影"     }, // 第 10 章 Lv.100
            new string[] { "龙裔卫士",     "龙裔焰术士",     "龙裔猎杀者"   }, // 第 11 章 Lv.110
        };

        /// <summary>
        /// 每章场地/族类的书面名，给战斗舞台的左上角标签用（和 <see cref="ChapterMonsterNames"/> 一一对应）。
        /// <para>
        /// 单独列一份「族名」而不是从怪物名里截字符串：怪物名是「族名 + 兵种」拼的
        /// （哥布林战士 / 哥布林弓箭手），靠截断猜族名在「食人魔蛮兵」这种名字上就会猜错。
        /// </para>
        /// </summary>
        public static readonly string[] ChapterFamilyNames =
        {
            "哥布林", "狗头人", "骷髅", "食人魔", "兽人", "蛛魔",
            "亡灵", "石像鬼", "恶魔", "深渊", "龙裔",
        };

        /// <summary>
        /// 每章族类的属性偏向。行 = 章节，列 = 生命 / 攻击 / 防御 / 速度，乘在
        /// 「等级基础 × 类型系数 × 档次系数 × 词缀系数」之外，让每章的怪打起来手感不同：
        /// 骷髅脆而凶、食人魔肉而慢、蛛魔快而毒。
        /// <para>
        /// 第 1 章和第 11 章刻意保持全 1：第 1 章是教学基准，第 11 章是终局——
        /// 现有的 Lv.110 巅峰胜率实测表就是按第 11 章算出来的，这里必须中性，
        /// 否则那张表得整体重测。中间章节的幅度都控在 ±25% 以内，不会盖过等级主轴
        /// （每章 +10 级 ≈ 基础生命 +200、基础攻击 +30、基础防御 +24）。
        /// </para>
        /// </summary>
        public static readonly float[][] ChapterStatBias =
        {
            new float[] { 1.00f, 1.00f, 1.00f, 1.00f }, // 第 1 章  基准
            new float[] { 0.95f, 1.05f, 0.95f, 1.08f }, // 第 2 章  狗头人：略快略脆
            new float[] { 0.85f, 1.15f, 0.88f, 0.95f }, // 第 3 章  骷髅：脆、凶、稍慢
            new float[] { 1.25f, 0.88f, 1.12f, 0.90f }, // 第 4 章  食人魔：厚血、慢、低攻
            new float[] { 1.12f, 1.05f, 1.08f, 0.95f }, // 第 5 章  兽人：全面偏厚
            new float[] { 0.88f, 1.12f, 0.85f, 1.15f }, // 第 6 章  蛛魔：快、毒、脆
            new float[] { 1.08f, 1.00f, 1.05f, 1.00f }, // 第 7 章  亡灵：偏韧
            new float[] { 1.05f, 0.95f, 1.25f, 0.88f }, // 第 8 章  石像鬼：高防、慢
            new float[] { 0.90f, 1.20f, 0.90f, 1.10f }, // 第 9 章  恶魔：高攻、快、脆
            new float[] { 1.10f, 1.10f, 1.05f, 0.92f }, // 第 10 章 深渊：全面强化、稍慢
            new float[] { 1.00f, 1.00f, 1.00f, 1.00f }, // 第 11 章 龙裔：中性，保护终局平衡数据
        };

        // ==================== 怪物档次 ====================

        /// <summary>
        /// 档次的生命 / 攻击 / 防御倍率，索引与 MonsterTier 一致（普通 / 精英 / Boss）。
        /// Boss 由 ×3 下调到 ×2.5：×3 时实测胜率为 0。
        /// </summary>
        public static readonly float[] MonsterTierStatFactor = { 1.0f, 1.5f, 2.5f };

        /// <summary>
        /// 档次的速度倍率，索引与 MonsterTier 一致。
        /// 速度单独一套且更保守：速度对攻击间隔是非线性的（间隔 = 2/(1+速度/20)），
        /// 原先跟其他属性一样 ×1.5 / ×2.5，会把精英/Boss 的实际 DPS 推到约 ×5。
        /// </summary>
        public static readonly float[] MonsterTierSpeedFactor = { 1.0f, 1.2f, 1.5f };

        /// <summary>档次经验倍率，索引与 MonsterTier 一致。</summary>
        public static readonly float[] MonsterTierExpFactor = { 1.0f, 2.0f, 5.0f };

        /// <summary>档次保底掉落件数（普通怪为 0，即仍走 30% 概率），索引与 MonsterTier 一致。</summary>
        public static readonly int[] MonsterTierGuaranteedDrops = { 0, 1, 2 };

        /// <summary>
        /// 精英 / Boss 的保底掉落品质权重，索引与 MonsterTier 一致（普通怪为 null，仍走 QualityWeights）。
        /// 精英：稀有 70% / 史诗 25% / 传说 5%；
        /// Boss：史诗 80% / 传说 20%。
        /// </summary>
        public static readonly float[][] MonsterTierDropQualityWeights =
        {
            null,
            new float[] { 0f, 0f, 0.70f, 0.25f, 0.05f },
            new float[] { 0f, 0f, 0f, 0.80f, 0.20f },
        };

        /// <summary>档次名称前缀，索引与 MonsterTier 一致。</summary>
        public static readonly string[] MonsterTierPrefixes = { "", "精英·", "远古·" };

        // ==================== 怪物词缀（要求 3）====================

        /// <summary>词缀显示名，索引与 MonsterAffix 一致（None 为空串）。</summary>
        public static readonly string[] AffixNames =
        {
            "", "狂暴", "坚甲", "迅捷", "吸血", "护盾", "剧毒",
        };

        /// <summary>
        /// 词缀对 [生命, 攻击, 防御, 速度] 的乘算系数，行索引与 MonsterAffix 一致。
        /// 吸血 / 护盾 / 剧毒 不改面板，走下面各自的特效常量。
        /// </summary>
        public static readonly float[][] AffixStatFactors =
        {
            new float[] { 1.00f, 1.00f, 1.00f, 1.00f }, // None
            new float[] { 1.00f, 1.30f, 0.80f, 1.00f }, // 狂暴：攻击 +30%，防御 -20%
            new float[] { 1.00f, 1.00f, 1.50f, 0.80f }, // 坚甲：防御 +50%，速度 -20%
            new float[] { 0.80f, 1.00f, 1.00f, 1.40f }, // 迅捷：速度 +40%，生命 -20%
            new float[] { 1.00f, 1.00f, 1.00f, 1.00f }, // 吸血
            new float[] { 1.00f, 1.00f, 1.00f, 1.00f }, // 护盾
            new float[] { 1.00f, 1.00f, 1.00f, 1.00f }, // 剧毒
        };

        // ==================== 掉落（要求 11、12）====================

        /// <summary>
        /// 品质抽取权重，顺序与 EquipmentQuality 一致：普通/优秀/稀有/史诗/传说。
        /// 本表为 62.5% / 25% / 9% / 3% / 0.5%，合计正好 1。
        /// </summary>
        public static readonly float[] QualityWeights = { 0.625f, 0.25f, 0.09f, 0.03f, 0.005f };

        /// <summary>
        /// 品质属性倍率的下限，顺序与 EquipmentQuality 一致。
        /// 每件装备在 [下限, 上限] 之间随机取值，因此**同品质的两件装备强弱也不同**。
        /// </summary>
        public static readonly float[] QualityMultiplierMin = { 1.00f, 1.30f, 1.80f, 2.60f, 4.00f };

        /// <summary>
        /// 品质属性倍率的上限。高品质段已拉回：史诗约 3 倍、传说约 4~5 倍，
        /// 让「打到一件高品质」重新有明显收益。
        /// </summary>
        public static readonly float[] QualityMultiplierMax = { 1.15f, 1.55f, 2.15f, 3.10f, 4.80f };

        /// <summary>品质显示名。</summary>
        public static readonly string[] QualityNames = { "普通", "优秀", "稀有", "史诗", "传说" };

        /// <summary>品质显示色（默认）：白 / 绿 / 蓝 / 紫 / 橙。</summary>
        private static readonly Color[] QualityColorsNormal =
        {
            new Color(1.00f, 1.00f, 1.00f), // 普通 - 白
            new Color(0.40f, 0.85f, 0.40f), // 优秀 - 绿
            new Color(0.35f, 0.60f, 1.00f), // 稀有 - 蓝
            new Color(0.75f, 0.45f, 1.00f), // 史诗 - 紫
            new Color(1.00f, 0.65f, 0.20f), // 传说 - 橙
        };

        /// <summary>
        /// 品质显示色（色弱友好，C11）：Okabe-Ito 风格色板，红绿色弱也能分开。
        /// 只换颜色、不换含义——品质名（普通/优秀/…）在所有卡片上照旧显示，颜色只是第二编码。
        /// </summary>
        private static readonly Color[] QualityColorsColorBlind =
        {
            new Color(0.80f, 0.82f, 0.84f), // 普通 - 浅灰
            new Color(0.00f, 0.62f, 0.45f), // 优秀 - 蓝绿 #009E73
            new Color(0.34f, 0.71f, 0.91f), // 稀有 - 天蓝 #56B4E9
            new Color(0.80f, 0.47f, 0.65f), // 史诗 - 紫红 #CC79A7
            new Color(0.90f, 0.62f, 0.00f), // 传说 - 橙 #E69F00
        };

        /// <summary>品质显示色，按设置里的「色弱友好」在两张色板间切换。</summary>
        public static Color[] QualityColors =>
            DisplaySettings.UseColorBlindPalette ? QualityColorsColorBlind : QualityColorsNormal;

        // ==================== 金币（卖装备所得）====================

        /// <summary>品质卖价倍率，顺序与 EquipmentQuality 一致；品质越高卖得越多。</summary>
        public static readonly float[] SellQualityMultipliers = { 1f, 2f, 4f, 8f, 16f };

        // ==================== 装备强化（步骤 2）====================

        /// <summary>
        /// 强化费用的品质倍率，顺序与 EquipmentQuality 一致：
        /// 普通 0.6 / 优秀 0.8 / 稀有 1.0 / 史诗 1.3 / 传说 1.6。
        /// 高品质装备的主属性基数更大，每级 +3% 换来的绝对值也更大，
        /// 所以费用跟着品质走，否则「强化传说」会成为性价比碾压一切的选择。
        /// 注意这里和 SellQualityMultipliers（1/2/4/8/16）刻意拉得很开：
        /// 卖价差 16 倍是「掉落品质之间的价值差」，强化费只差 2.7 倍是
        /// 「同一件装备打磨到顶的代价」，两者不是一回事——费用若也按 16 倍走，
        /// 传说装备的强化会贵到没人强化得起，等于把高品质装备的成长空间废掉。
        /// </summary>
        public static readonly float[] EnhanceQualityFactors = { 0.6f, 0.8f, 1.0f, 1.3f, 1.6f };

        // ==================== 装备副词条 ====================

        /// <summary>副词条显示名，顺序与 EquipmentAffix 一致。</summary>
        public static readonly string[] EquipAffixNames = { "暴击率", "暴击伤害", "攻击速度", "全属性", "护甲穿透" };

        /// <summary>副词条的基础随机下限（暴击率 1% / 暴击伤害 5% / 攻速 2% / 全属性 2% / 护甲穿透 2%）。</summary>
        public static readonly float[] EquipAffixMin = { 0.01f, 0.045f, 0.02f, 0.02f, 0.02f };

        /// <summary>副词条的基础随机上限（暴击率 8% / 暴击伤害 40% / 攻速 10% / 全属性 8% / 护甲穿透 10%）。</summary>
        public static readonly float[] EquipAffixMax = { 0.08f, 0.36f, 0.10f, 0.08f, 0.10f };

        /// <summary>
        /// 各品质的副词条条数下限，索引与 EquipmentQuality 一致：
        /// 普通 1 / 优秀 1 / 稀有 2 / 史诗 3 / 传说 4。
        /// </summary>
        public static readonly int[] EquipAffixCountMin = { 1, 1, 2, 3, 4 };

        /// <summary>
        /// 各品质的副词条条数上限：普通 1 / 优秀 2 / 稀有 2 / 史诗 3 / 传说 4。
        /// 只有优秀档是 1~2 条随机，其余品质上下限相同。
        /// 同一件装备内不会重复出现同一种词条（词条池共 5 种：暴击率 / 暴击伤害 / 攻速 / 全属性 / 护甲穿透）。
        /// </summary>
        public static readonly int[] EquipAffixCountMax = { 1, 2, 2, 3, 4 };

        // ==================== 加速档位（要求 16）====================

        /// <summary>可选的游戏速度倍率。</summary>
        public static readonly float[] SpeedOptions = { 1f, 2f, 4f };
    }
}

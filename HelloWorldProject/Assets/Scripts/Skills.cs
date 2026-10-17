using UnityEngine;

namespace AutoBattle
{
    /// <summary>技能类别：主动要手动/自动释放，被动只改属性，触发由战斗事件拉起。</summary>
    public enum SkillKind
    {
        /// <summary>主动技能：各自独立冷却，受 1 秒公共冷却约束，战斗中自动释放。</summary>
        Active,

        /// <summary>被动技能：只参与属性计算，不进入战斗循环。</summary>
        Passive,

        /// <summary>触发技能：在受击 / 暴击 / 低血等事件上检查。</summary>
        Trigger,
    }

    /// <summary>
    /// 技能编号。顺序必须与 <see cref="SkillCatalog.All"/> 一致。
    /// 主动 0~3、被动 4~10、触发 11~14。
    /// </summary>
    public enum SkillId
    {
        // ---- 主动 ----
        HeavyStrike,
        Whirlwind,
        Berserk,
        Execute,

        // ---- 被动 ----
        IronBody,
        BerserkHeart,
        LethalStrike,
        Vampire,
        Swiftness,
        GiantSlayer,
        Undying,

        // ---- 触发 ----
        Counter,
        CritChain,
        NearDeathBurst,
        ShieldGen,
    }

    /// <summary>
    /// 一条技能的静态定义。数值部分可在 <c>Assets/Resources/skills.json</c> 里改，
    /// 无需重编译（见 <see cref="SkillCatalog"/> 的加载部分）。
    ///
    /// 字段必须全部是 JsonUtility 认得的类型（float / int / string / 枚举 / Color），
    /// 因为外置表就是靠它反序列化的。
    /// </summary>
    [System.Serializable]
    public class SkillDef
    {
        /// <summary>技能编号。反序列化时会校验它与数组下标一致，错位的一律丢弃。</summary>
        public SkillId id;

        /// <summary>类别。</summary>
        public SkillKind kind;

        /// <summary>显示名。</summary>
        public string name;

        /// <summary>说明文本。</summary>
        public string description;

        /// <summary>主动技能冷却（秒，游戏内时间）；被动/触发为 0。</summary>
        public float cooldown;

        /// <summary>主数值：伤害倍率 / 属性加成比例 / 减伤比例。</summary>
        public float value;

        /// <summary>狂暴的暴击率加成（全表只有狂暴用它，其余技能恒为 0）。</summary>
        public float critBonus;

        /// <summary>
        /// 生命阈值（占最大生命的比例）。两个语义完全不同的技能共用同一种「低血线」概念：
        /// 斩杀 = 怪物生命低于此值才允许释放；不死之身 = 自身生命低于此值才吃减伤。
        /// </summary>
        public float hpThreshold;

        /// <summary>触发概率（0~1）：反击的受击反弹率、暴击连锁的追加打击率。</summary>
        public float triggerChance;

        /// <summary>多段攻击的次数（旋风斩 3 次，其余 1）。</summary>
        public int hits;

        /// <summary>增益持续时间（秒）；无增益为 0。</summary>
        public float duration;

        /// <summary>技能栏配色。</summary>
        public Color color;

        public bool IsActive => kind == SkillKind.Active;
        public bool IsPassive => kind == SkillKind.Passive;
        public bool IsTrigger => kind == SkillKind.Trigger;
    }

    /// <summary>
    /// 技能池。下面的 <see cref="All"/> 是**代码内默认值**，同时也是外置表的落点：
    /// 加载 <c>Resources/skills.json</c> 时是就地覆盖数组里各对象的字段，
    /// 不替换数组本身——这样任何地方缓存的 <see cref="SkillDef"/> 引用都不会失效。
    /// 顺序必须与 <see cref="SkillId"/> 一致（外置表也只能改数值，不能增删条目）。
    ///
    /// 这里每个 <c>SkillDef.color</c> 都是**逐技能独占的辨识色**，不是主题色：
    /// 它决定技能槽边框、图标底衬和伤害飘字的色调，两个技能共用一色就失去了
    /// 快速辨认的意义。所以它们**刻意不并入 <c>GameConfig.Colors.cs</c> 的语义色板**，
    /// 和 <c>GameConfig.QualityColors</c>（按品质索引的数据表）同理。
    /// </summary>
    public static partial class SkillCatalog
    {
        public static readonly SkillDef[] All =
        {
            // ==================== 主动 ====================
            new SkillDef
            {
                id = SkillId.HeavyStrike, kind = SkillKind.Active, name = "重击",
                description = "对怪物造成 250% 攻击伤害",
                cooldown = 8f, value = 2.50f, hits = 1, duration = 0f,
                color = new Color(1.00f, 0.62f, 0.35f),
            },
            new SkillDef
            {
                id = SkillId.Whirlwind, kind = SkillKind.Active, name = "旋风斩",
                description = "连续 3 次、每次 150% 攻击伤害",
                cooldown = 8f, value = 1.50f, hits = 3, duration = 0f,
                color = new Color(0.60f, 0.90f, 1.00f),
            },
            new SkillDef
            {
                id = SkillId.Berserk, kind = SkillKind.Active, name = "狂暴",
                description = "10 秒内攻击 +30%、暴击率 +20%",
                cooldown = 30f, value = 0.30f, critBonus = 0.20f, hits = 0, duration = 10f,
                color = new Color(1.00f, 0.42f, 0.38f),
            },
            new SkillDef
            {
                id = SkillId.Execute, kind = SkillKind.Active, name = "斩杀",
                description = "对生命低于 30% 的怪物造成 1500% 攻击伤害",
                cooldown = 20f, value = 15.00f, hpThreshold = 0.30f, hits = 1, duration = 0f,
                color = new Color(0.85f, 0.35f, 0.95f),
            },

            // ==================== 被动 ====================
            new SkillDef
            {
                id = SkillId.IronBody, kind = SkillKind.Passive, name = "钢铁之躯",
                description = "防御 +150%",
                cooldown = 0f, value = 1.50f, hits = 0, duration = 0f,
                color = new Color(0.65f, 0.75f, 0.90f),
            },
            new SkillDef
            {
                id = SkillId.BerserkHeart, kind = SkillKind.Passive, name = "狂暴之心",
                description = "暴击率 +8%",
                cooldown = 0f, value = 0.08f, hits = 0, duration = 0f,
                color = new Color(1.00f, 0.55f, 0.45f),
            },
            new SkillDef
            {
                id = SkillId.LethalStrike, kind = SkillKind.Passive, name = "致命打击",
                description = "暴击伤害 ×1.2",
                cooldown = 0f, value = 0.20f, hits = 0, duration = 0f,
                color = new Color(0.95f, 0.45f, 0.70f),
            },
            new SkillDef
            {
                id = SkillId.Vampire, kind = SkillKind.Passive, name = "吸血鬼",
                description = "吸血 +30%（造成伤害的 30% 转为生命）",
                cooldown = 0f, value = 0.30f, hits = 0, duration = 0f,
                color = new Color(0.75f, 0.30f, 0.45f),
            },
            new SkillDef
            {
                id = SkillId.Swiftness, kind = SkillKind.Passive, name = "迅捷",
                description = "攻击速度 +8%",
                cooldown = 0f, value = 0.08f, hits = 0, duration = 0f,
                color = new Color(0.55f, 0.95f, 0.65f),
            },
            new SkillDef
            {
                id = SkillId.GiantSlayer, kind = SkillKind.Passive, name = "巨人杀手",
                description = "对精英 / Boss 的伤害 +20%",
                cooldown = 0f, value = 0.20f, hits = 0, duration = 0f,
                color = new Color(1.00f, 0.80f, 0.35f),
            },
            new SkillDef
            {
                id = SkillId.Undying, kind = SkillKind.Passive, name = "不死之身",
                description = "生命低于 50% 时受到的伤害减少 55%",
                cooldown = 0f, value = 0.55f, hpThreshold = 0.50f, hits = 0, duration = 0f,
                color = new Color(0.85f, 0.85f, 0.55f),
            },

            // ==================== 触发 ====================
            new SkillDef
            {
                id = SkillId.Counter, kind = SkillKind.Trigger, name = "反击",
                description = "受击时 35% 概率反弹 70% 攻击伤害",
                cooldown = 0f, value = 0.70f, triggerChance = 0.35f, hits = 0, duration = 0f,
                color = new Color(0.90f, 0.70f, 0.40f),
            },
            new SkillDef
            {
                id = SkillId.CritChain, kind = SkillKind.Trigger, name = "暴击连锁",
                description = "暴击时 30% 概率追加 50% 攻击伤害",
                cooldown = 0f, value = 0.50f, triggerChance = 0.30f, hits = 0, duration = 0f,
                color = new Color(1.00f, 0.90f, 0.45f),
            },
            new SkillDef
            {
                id = SkillId.NearDeathBurst, kind = SkillKind.Trigger, name = "濒死爆发",
                description = "濒死（会致死的那一击）时攻击 +100%，3 秒内不会死亡，30 秒一次",
                cooldown = 30f, value = 1.00f, hits = 0, duration = 3f,
                color = new Color(1.00f, 0.35f, 0.30f),
            },
            new SkillDef
            {
                id = SkillId.ShieldGen, kind = SkillKind.Trigger, name = "护盾生成",
                description = "每 0.5 秒获得 10% 最大生命的护盾",
                cooldown = 0.5f, value = 0.10f, hits = 0, duration = 0f,
                color = new Color(0.55f, 0.80f, 1.00f),
            },
        };

        public static SkillDef Get(SkillId id) => All[(int)id];

        /// <summary>按类别收集全部技能编号。</summary>
        public static SkillId[] OfKind(SkillKind kind)
        {
            int count = 0;
            for (int i = 0; i < All.Length; i++) if (All[i].kind == kind) count++;

            var list = new SkillId[count];
            int k = 0;
            for (int i = 0; i < All.Length; i++) if (All[i].kind == kind) list[k++] = All[i].id;
            return list;
        }
    }
}

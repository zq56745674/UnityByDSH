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

    /// <summary>一条技能的静态定义（数值部分）。</summary>
    public class SkillDef
    {
        /// <summary>技能编号。</summary>
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

        /// <summary>次数值：Buff 的第二项加成（狂暴的暴击率）或触发概率（反击、暴击连锁）。</summary>
        public float extra;

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
    /// 技能池。数值集中在这里，方便平衡；顺序必须与 <see cref="SkillId"/> 一致。
    /// </summary>
    public static class SkillCatalog
    {
        public static readonly SkillDef[] All =
        {
            // ==================== 主动 ====================
            new SkillDef
            {
                id = SkillId.HeavyStrike, kind = SkillKind.Active, name = "重击",
                description = "对怪物造成 250% 攻击伤害",
                cooldown = 8f, value = 2.50f, extra = 0f, hits = 1, duration = 0f,
                color = new Color(1.00f, 0.62f, 0.35f),
            },
            new SkillDef
            {
                id = SkillId.Whirlwind, kind = SkillKind.Active, name = "旋风斩",
                description = "连续 3 次、每次 150% 攻击伤害",
                cooldown = 8f, value = 1.50f, extra = 0f, hits = 3, duration = 0f,
                color = new Color(0.60f, 0.90f, 1.00f),
            },
            new SkillDef
            {
                id = SkillId.Berserk, kind = SkillKind.Active, name = "狂暴",
                description = "10 秒内攻击 +30%、暴击率 +20%",
                cooldown = 30f, value = 0.30f, extra = 0.20f, hits = 0, duration = 10f,
                color = new Color(1.00f, 0.42f, 0.38f),
            },
            new SkillDef
            {
                id = SkillId.Execute, kind = SkillKind.Active, name = "斩杀",
                description = "对生命低于 30% 的怪物造成 1500% 攻击伤害",
                cooldown = 20f, value = 15.00f, extra = 0.30f, hits = 1, duration = 0f,
                color = new Color(0.85f, 0.35f, 0.95f),
            },

            // ==================== 被动 ====================
            new SkillDef
            {
                id = SkillId.IronBody, kind = SkillKind.Passive, name = "钢铁之躯",
                description = "防御 +150%",
                cooldown = 0f, value = 1.50f, extra = 0f, hits = 0, duration = 0f,
                color = new Color(0.65f, 0.75f, 0.90f),
            },
            new SkillDef
            {
                id = SkillId.BerserkHeart, kind = SkillKind.Passive, name = "狂暴之心",
                description = "暴击率 +8%",
                cooldown = 0f, value = 0.08f, extra = 0f, hits = 0, duration = 0f,
                color = new Color(1.00f, 0.55f, 0.45f),
            },
            new SkillDef
            {
                id = SkillId.LethalStrike, kind = SkillKind.Passive, name = "致命打击",
                description = "暴击伤害 ×1.2",
                cooldown = 0f, value = 0.20f, extra = 0f, hits = 0, duration = 0f,
                color = new Color(0.95f, 0.45f, 0.70f),
            },
            new SkillDef
            {
                id = SkillId.Vampire, kind = SkillKind.Passive, name = "吸血鬼",
                description = "吸血 +30%（造成伤害的 30% 转为生命）",
                cooldown = 0f, value = 0.30f, extra = 0f, hits = 0, duration = 0f,
                color = new Color(0.75f, 0.30f, 0.45f),
            },
            new SkillDef
            {
                id = SkillId.Swiftness, kind = SkillKind.Passive, name = "迅捷",
                description = "攻击速度 +8%",
                cooldown = 0f, value = 0.08f, extra = 0f, hits = 0, duration = 0f,
                color = new Color(0.55f, 0.95f, 0.65f),
            },
            new SkillDef
            {
                id = SkillId.GiantSlayer, kind = SkillKind.Passive, name = "巨人杀手",
                description = "对精英 / Boss 的伤害 +20%",
                cooldown = 0f, value = 0.20f, extra = 0f, hits = 0, duration = 0f,
                color = new Color(1.00f, 0.80f, 0.35f),
            },
            new SkillDef
            {
                id = SkillId.Undying, kind = SkillKind.Passive, name = "不死之身",
                description = "生命低于 50% 时受到的伤害减少 55%",
                cooldown = 0f, value = 0.55f, extra = 0.50f, hits = 0, duration = 0f,
                color = new Color(0.85f, 0.85f, 0.55f),
            },

            // ==================== 触发 ====================
            new SkillDef
            {
                id = SkillId.Counter, kind = SkillKind.Trigger, name = "反击",
                description = "受击时 35% 概率反弹 70% 攻击伤害",
                cooldown = 0f, value = 0.70f, extra = 0.35f, hits = 0, duration = 0f,
                color = new Color(0.90f, 0.70f, 0.40f),
            },
            new SkillDef
            {
                id = SkillId.CritChain, kind = SkillKind.Trigger, name = "暴击连锁",
                description = "暴击时 30% 概率追加 50% 攻击伤害",
                cooldown = 0f, value = 0.50f, extra = 0.30f, hits = 0, duration = 0f,
                color = new Color(1.00f, 0.90f, 0.45f),
            },
            new SkillDef
            {
                id = SkillId.NearDeathBurst, kind = SkillKind.Trigger, name = "濒死爆发",
                description = "濒死（会致死的那一击）时攻击 +100%，3 秒内不会死亡，30 秒一次",
                cooldown = 30f, value = 1.00f, extra = 0f, hits = 0, duration = 3f,
                color = new Color(1.00f, 0.35f, 0.30f),
            },
            new SkillDef
            {
                id = SkillId.ShieldGen, kind = SkillKind.Trigger, name = "护盾生成",
                description = "每 0.5 秒获得 10% 最大生命的护盾",
                cooldown = 0.5f, value = 0.10f, extra = 0f, hits = 0, duration = 0f,
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

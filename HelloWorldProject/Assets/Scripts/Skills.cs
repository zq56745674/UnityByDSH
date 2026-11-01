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

    /// <summary>技能所属流派。通用技能任何流派都能使用。</summary>
    public enum SkillSchool
    {
        General,
        Storm,
        Mountain,
    }

    /// <summary>
    /// 技能编号。顺序必须与 <see cref="SkillCatalog.All"/> 一致。
    /// 主动 0~4、被动 5~9、触发 10~14。
    /// </summary>
    public enum SkillId
    {
        // ---- 主动 ----
        ThunderRush,
        ThunderGod,
        MountainSlam,
        Bulwark,
        WarCry,

        // ---- 被动 ----
        StaticResonance,
        StormHaste,
        MountainBody,
        HeavyImpact,
        Vampire,

        // ---- 触发 ----
        Arc,
        ThunderPunish,
        Aftershock,
        RockArmor,
        LastStand,
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

        /// <summary>流派。通用技能不受流派限制。</summary>
        public SkillSchool school;

        /// <summary>显示名。</summary>
        public string name;

        /// <summary>说明文本。</summary>
        public string description;

        /// <summary>一行连携说明，技能池与 tooltip 用。</summary>
        public string synergy;

        /// <summary>主动技能冷却（秒，游戏内时间）；被动/触发为 0。</summary>
        public float cooldown;

        /// <summary>主数值：伤害倍率 / 属性加成比例 / 减伤比例。</summary>
        public float value;

        /// <summary>副数值：按层加成、护盾比例、转攻比例等，语义由技能自己解释。</summary>
        public float value2;

        /// <summary>第三数值：上限、破甲比例、压制比例等。</summary>
        public float value3;

        /// <summary>第四数值：备用（目前只有崩山击的压制比例用它）。</summary>
        public float value4;

        /// <summary>暴击率加成（静电共鸣用）。</summary>
        public float critBonus;

        /// <summary>生命阈值（占最大生命的比例）。保留给需要在低血线释放的技能。</summary>
        public float hpThreshold;

        /// <summary>触发概率（0~1）。</summary>
        public float triggerChance;

        /// <summary>计数 / 段数：雷殛的「每第 4 次」、风暴加速的最多层数。</summary>
        public int hits;

        /// <summary>增益持续时间（秒）；无增益为 0。</summary>
        public float duration;

        /// <summary>第二持续时间：崩山击破甲 6 秒、压制 2 秒、磐石护盾 6 秒 / 攻击 18 秒。</summary>
        public float duration2;

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
    /// </summary>
    public static partial class SkillCatalog
    {
        public static readonly Color StormColor = new Color(0.31f, 0.765f, 0.969f);
        public static readonly Color MountainColor = new Color(0.843f, 0.627f, 0.29f);
        public static readonly Color GeneralColor = new Color(0.72f, 0.70f, 0.86f);

        /// <summary>风暴流派的六个专属技能。</summary>
        public static readonly SkillId[] StormExclusive =
        {
            SkillId.ThunderRush, SkillId.ThunderGod, SkillId.StaticResonance,
            SkillId.StormHaste, SkillId.Arc, SkillId.ThunderPunish,
        };

        /// <summary>山崩流派的六个专属技能。</summary>
        public static readonly SkillId[] MountainExclusive =
        {
            SkillId.MountainSlam, SkillId.Bulwark, SkillId.MountainBody,
            SkillId.HeavyImpact, SkillId.Aftershock, SkillId.RockArmor,
        };

        /// <summary>三个通用技能。</summary>
        public static readonly SkillId[] GeneralSkills =
        {
            SkillId.WarCry, SkillId.Vampire, SkillId.LastStand,
        };

        /// <summary>风暴毕业 build 的六格顺序：主动 0/1、被动 0/1、触发 0/1。</summary>
        public static readonly SkillId[] StormBuild =
        {
            SkillId.ThunderRush, SkillId.ThunderGod, SkillId.StaticResonance,
            SkillId.StormHaste, SkillId.Arc, SkillId.ThunderPunish,
        };

        /// <summary>山崩毕业 build 的六格顺序：主动 0/1、被动 0/1、触发 0/1。</summary>
        public static readonly SkillId[] MountainBuild =
        {
            SkillId.MountainSlam, SkillId.Bulwark, SkillId.MountainBody,
            SkillId.HeavyImpact, SkillId.Aftershock, SkillId.RockArmor,
        };

        public static readonly SkillDef[] All =
        {
            // ==================== 主动 ====================
            new SkillDef
            {
                id = SkillId.ThunderRush, kind = SkillKind.Active, school = SkillSchool.Storm,
                name = "雷霆突袭",
                description = "320% 攻击伤害，获得 2 层雷印，攻速 +20% 持续 4 秒；连锁闪电每命中回复 6% 最大生命",
                synergy = "连携：快速叠雷印，为雷神降临与连锁闪电铺路。",
                cooldown = 8f, value = 3.20f, value2 = 2f, value3 = 0.20f, value4 = 0.06f, hits = 1, duration = 4f,
                color = StormColor,
            },
            new SkillDef
            {
                id = SkillId.ThunderGod, kind = SkillKind.Active, school = SkillSchool.Storm,
                name = "雷神降临",
                description = "500% 伤害，每层雷印 +100%；不消耗雷印，满 8 层再 ×1.5 并重置雷霆突袭；雷印护体：防御 +40%",
                synergy = "连携：雷印越满伤害越高，满层爆发后继续叠印。",
                cooldown = 30f, value = 5.00f, value2 = 1.00f, value3 = 1.5f, value4 = 0.40f, hits = 1, duration = 0f,
                color = StormColor,
            },
            new SkillDef
            {
                id = SkillId.MountainSlam, kind = SkillKind.Active, school = SkillSchool.Mountain,
                name = "崩山击",
                description = "400% 伤害，每层岩层 +150%（满 3 层 850%），破甲 30% 6 秒、压制 2 秒",
                synergy = "连携：岩层满时伤害最高，破甲让后续普攻与回声更痛。",
                cooldown = 10f, value = 4.00f, value2 = 1.50f, value3 = 0.30f, value4 = 0.20f,
                hits = 1, duration = 6f, duration2 = 2f,
                color = MountainColor,
            },
            new SkillDef
            {
                id = SkillId.Bulwark, kind = SkillKind.Active, school = SkillSchool.Mountain,
                name = "磐石壁垒",
                description = "获得 20% 最大生命护盾 6 秒；护盾存在时攻击 +25%，持续 18 秒",
                synergy = "连携：先开壁垒抢攻击加成，再靠岩甲维持护盾。",
                cooldown = 18f, value = 0.20f, value2 = 0.25f, hits = 0, duration = 6f, duration2 = 18f,
                color = MountainColor,
            },
            new SkillDef
            {
                id = SkillId.WarCry, kind = SkillKind.Active, school = SkillSchool.General,
                name = "战吼",
                description = "攻击 +20%、受到伤害 -10%，持续 8 秒",
                synergy = "连携：通用增伤窗口，适合在爆发前开启。",
                cooldown = 20f, value = 0.20f, value2 = 0.10f, hits = 0, duration = 8f,
                color = GeneralColor,
            },

            // ==================== 被动 ====================
            new SkillDef
            {
                id = SkillId.StaticResonance, kind = SkillKind.Passive, school = SkillSchool.Storm,
                name = "静电共鸣",
                description = "暴击率 +12%、暴伤 +25%；暴击率超过 95% 的部分每 1% 转 +2% 暴伤，最多 +30%",
                synergy = "连携：把溢出的暴击率变成暴伤，配合电弧的高频暴击。",
                cooldown = 0f, value = 0.25f, value2 = 0.30f, critBonus = 0.12f, hits = 0, duration = 0f,
                color = StormColor,
            },
            new SkillDef
            {
                id = SkillId.StormHaste, kind = SkillKind.Passive, school = SkillSchool.Storm,
                name = "风暴加速",
                description = "攻速 +12%；每次暴击再 +2% 攻速，最多 5 层，持续 4 秒",
                synergy = "连携：暴击越密攻速越高，雷印与闪电频率一起起飞。",
                cooldown = 0f, value = 0.12f, value2 = 0.02f, hits = 5, duration = 4f,
                color = StormColor,
            },
            new SkillDef
            {
                id = SkillId.MountainBody, kind = SkillKind.Passive, school = SkillSchool.Mountain,
                name = "山岳之躯",
                description = "防御 +30%；装备防御的 10% 转为攻击，最多 +25% 面板攻击",
                synergy = "连携：越肉越痛，配磐石壁垒与岩甲形成滚雪球。",
                cooldown = 0f, value = 0.30f, value2 = 0.10f, value3 = 0.25f, hits = 0, duration = 0f,
                color = MountainColor,
            },
            new SkillDef
            {
                id = SkillId.HeavyImpact, kind = SkillKind.Passive, school = SkillSchool.Mountain,
                name = "沉重打击",
                description = "暴伤 +40%，攻速 -10%，崩山与崩山击伤害 +20%",
                synergy = "连携：牺牲频率换单次质量，正对崩山击的爆发节奏。",
                cooldown = 0f, value = 0.40f, value2 = -0.10f, value3 = 0.20f, hits = 0, duration = 0f,
                color = MountainColor,
            },
            new SkillDef
            {
                id = SkillId.Vampire, kind = SkillKind.Passive, school = SkillSchool.General,
                name = "吸血鬼",
                description = "吸血 20%（造成伤害的 20% 转为生命）",
                synergy = "连携：高频伤害与多段闪电都能触发，续航核心。",
                cooldown = 0f, value = 0.20f, hits = 0, duration = 0f,
                color = new Color(0.75f, 0.30f, 0.45f),
            },

            // ==================== 触发 ====================
            new SkillDef
            {
                id = SkillId.Arc, kind = SkillKind.Trigger, school = SkillSchool.Storm,
                name = "电弧",
                description = "暴击时 25% 概率追加 60% 连锁伤害",
                synergy = "连携：每次暴击都可能补一段闪电，加速雷殛计数。",
                cooldown = 0f, value = 0.60f, triggerChance = 0.25f, hits = 0, duration = 0f,
                color = StormColor,
            },
            new SkillDef
            {
                id = SkillId.ThunderPunish, kind = SkillKind.Trigger, school = SkillSchool.Storm,
                name = "雷殛",
                description = "每第 4 次闪电命中追加 150% 伤害并 +1 雷印（雷殛自身不计数）",
                synergy = "连携：电弧与连锁闪电越频繁，雷殛与雷印叠得越快。",
                cooldown = 0f, value = 1.50f, value2 = 1f, triggerChance = 1f, hits = 4, duration = 0f,
                color = StormColor,
            },
            new SkillDef
            {
                id = SkillId.Aftershock, kind = SkillKind.Trigger, school = SkillSchool.Mountain,
                name = "余震",
                description = "崩山与崩山击命中时 40% 概率追加 60% 回声伤害（回声不再触发余震）",
                synergy = "连携：崩山击与沉重打击放大单次爆发后，回声再补一刀。",
                cooldown = 0f, value = 0.60f, triggerChance = 0.40f, hits = 0, duration = 0f,
                color = MountainColor,
            },
            new SkillDef
            {
                id = SkillId.RockArmor, kind = SkillKind.Trigger, school = SkillSchool.Mountain,
                name = "岩甲",
                description = "受击时 35% 概率获得 10% 最大生命护盾 5 秒（1.5 秒一次），总护盾不超过 30% 最大生命",
                synergy = "连携：护盾与磐石壁垒取较大值，撑住岩层与反打。",
                cooldown = 0f, value = 0.10f, value2 = 0.30f, value3 = 1.5f,
                triggerChance = 0.35f, hits = 0, duration = 5f,
                color = MountainColor,
            },
            new SkillDef
            {
                id = SkillId.LastStand, kind = SkillKind.Trigger, school = SkillSchool.General,
                name = "保命",
                description = "受到致死伤害时保留 1 点生命并无敌 2 秒，60 秒一次",
                synergy = "连携：给慢节奏的山崩流一次容错，也给风暴流抢输出窗口。",
                cooldown = 60f, value = 2.00f, triggerChance = 1f, hits = 0, duration = 2f,
                color = GeneralColor,
            },
        };

        public static SkillDef Get(SkillId id)
        {
            // 存档里可能读到越界的技能 id（旧版本 / 手改 JSON），不要让它直接越界崩掉。
            return All[Mathf.Clamp((int)id, 0, All.Length - 1)];
        }

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

        public static SkillSchool SchoolOf(int doctrine)
        {
            if (doctrine == 1) return SkillSchool.Storm;
            if (doctrine == 2) return SkillSchool.Mountain;
            return SkillSchool.General;
        }

        public static string SchoolName(SkillSchool school)
        {
            switch (school)
            {
                case SkillSchool.Storm: return "风暴";
                case SkillSchool.Mountain: return "山崩";
                default: return "通用";
            }
        }

        public static Color SchoolColor(SkillSchool school)
        {
            switch (school)
            {
                case SkillSchool.Storm: return StormColor;
                case SkillSchool.Mountain: return MountainColor;
                default: return GeneralColor;
            }
        }

        /// <summary>doctrine 口径的重载（0 未选 / 1 风暴 / 2 山崩），UI 与日志直接用。</summary>
        public static string SchoolName(int doctrine) => SchoolName(SchoolOf(doctrine));

        /// <summary>doctrine 口径的重载（0 未选 / 1 风暴 / 2 山崩）。</summary>
        public static Color SchoolColor(int doctrine) => SchoolColor(SchoolOf(doctrine));

        /// <summary>该流派能获得的六个专属技能。doctrine 非法时返回空数组。</summary>
        public static SkillId[] ExclusiveOf(int doctrine)
        {
            if (doctrine == 1) return StormExclusive;
            if (doctrine == 2) return MountainExclusive;
            return new SkillId[0];
        }

        /// <summary>该流派整套九个技能（六专属 + 三通用），旧档迁移用。</summary>
        public static SkillId[] KitOf(int doctrine)
        {
            SkillId[] exclusive = ExclusiveOf(doctrine);
            var kit = new SkillId[exclusive.Length + GeneralSkills.Length];
            for (int i = 0; i < exclusive.Length; i++) kit[i] = exclusive[i];
            for (int i = 0; i < GeneralSkills.Length; i++) kit[exclusive.Length + i] = GeneralSkills[i];
            return kit;
        }

        /// <summary>该流派的毕业六格。</summary>
        public static SkillId[] BuildOf(int doctrine)
        {
            if (doctrine == 1) return StormBuild;
            if (doctrine == 2) return MountainBuild;
            return new SkillId[0];
        }

        /// <summary>流派签名主动（风暴=雷霆突袭，山崩=崩山击）。技能点自选后不再免费授予，保留给探针与旧数据参考。</summary>
        public static SkillId StarterOf(int doctrine)
        {
            SkillId[] build = BuildOf(doctrine);
            return build.Length > 0 ? build[0] : SkillId.ThunderRush;
        }

        /// <summary>本流派能否使用该技能（通用技能永远可以）。doctrine 为 0 时不做限制。</summary>
        public static bool IsAvailableTo(int doctrine, SkillId id)
        {
            if (doctrine == 0) return true;
            SkillSchool school = Get(id).school;
            return school == SkillSchool.General || school == SchoolOf(doctrine);
        }

        public static int DoctrineCount => 2;

        public const int ExclusivePerDoctrine = 6;
        public const int GeneralCount = 3;
        public const int TotalPerDoctrine = ExclusivePerDoctrine + GeneralCount;
    }
}

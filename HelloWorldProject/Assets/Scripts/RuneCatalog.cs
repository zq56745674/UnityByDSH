using UnityEngine;

namespace AutoBattle
{
    /// <summary>符文的两个互斥形态。未解锁时按甲处理（但没有任何效果）。</summary>
    public enum RuneForm
    {
        A = 0,
        B = 1,
    }

    /// <summary>
    /// 可以按形态覆写的技能字段。
    /// 覆写只作用在「读取点」上，不改 SkillCatalog 的静态表——技能面板显示的仍是基础值，
    /// 这样符文永远是一层独立的机制，不会污染技能表 / 存档 / 木桩基准。
    /// </summary>
    public enum RuneField
    {
        Value,
        Value2,
        Value3,
        Value4,
        TriggerChance,
        Hits,
        Duration,
        Duration2,
        Cooldown,
        CritBonus,
    }

    /// <summary>一个技能的两种符文形态：名字 + 一句话机制说明（技能面板 / 符文面板共用）。</summary>
    public sealed class RuneDef
    {
        public SkillId skill;
        public string nameA;
        public string nameB;
        public string descA;
        public string descB;
    }

    /// <summary>
    /// 巅峰符文目录（docs/gameplay-expansion.md §6 方案 C）。
    /// 每 100 巅峰 1 点，P100~P1000 共 10 点；每个技能花 1 点永久解锁「形态选择权」，
    /// 解锁后两个形态在局外免费切换。点数由 paragonLevel 派生，解锁与形态进存档（v17）。
    /// </summary>
    public static class RuneCatalog
    {
        /// <summary>多少巅峰等级换 1 点符文点。</summary>
        public const int ParagonPerPoint = 100;

        /// <summary>总点数上限（P1000 = 10 点，9 个技能 + 1 点余量）。</summary>
        public const int MaxPoints = 10;

        /// <summary>按巅峰等级算可获得的总点数。</summary>
        public static int PointsFor(int paragonLevel) =>
            Mathf.Clamp(Mathf.Max(0, paragonLevel) / ParagonPerPoint, 0, MaxPoints);

        /// <summary>按 SkillId 顺序排列的符文定义（长度 = SkillCatalog.All.Length）。</summary>
        public static readonly RuneDef[] All =
        {
            // ---- 主动 ----
            new RuneDef
            {
                skill = SkillId.ThunderRush, nameA = "裂雷", nameB = "蓄雷",
                descA = "伤害 320%→448%，但不再获得雷印",
                descB = "伤害 320%→192%，雷印 +2→+4、攻速 +20%→+35%",
            },
            new RuneDef
            {
                skill = SkillId.ThunderGod, nameA = "审罚", nameB = "永雷",
                descA = "每层雷印加成 100%→160%，但满印不再重置雷霆突袭",
                descB = "满印倍率 ×1.5→×2.0，但自身冷却 30→40 秒",
            },
            new RuneDef
            {
                skill = SkillId.MountainSlam, nameA = "裂地", nameB = "余震",
                descA = "破甲 30%→50%，但冷却 10→12 秒",
                descB = "额外造成 25%×岩层 伤害，但破甲 30%→15%",
            },
            new RuneDef
            {
                skill = SkillId.Bulwark, nameA = "坚壁", nameB = "战壁",
                descA = "护盾 20%→30%，攻击加成 25%→12%",
                descB = "攻击加成 25%→35%，护盾 20%→12%",
            },
            new RuneDef
            {
                skill = SkillId.WarCry, nameA = "狂战", nameB = "守战",
                descA = "攻击 +30%，但不再减伤",
                descB = "减伤 10%→18%，攻击 +20%→+10%",
            },
            // ---- 被动 ----
            new RuneDef
            {
                skill = SkillId.StaticResonance, nameA = "超载", nameB = "稳态",
                descA = "暴击率 +12%→+16%，溢出转化上限 30%→100%",
                descB = "暴伤 +25%→+32%，但没有溢出转化",
            },
            new RuneDef
            {
                skill = SkillId.StormHaste, nameA = "疾风", nameB = "绵风",
                descA = "每层攻速 +2%→+3%，但最多 5→3 层",
                descB = "持续 4→8 秒，但每层 +2%→+1.5%",
            },
            new RuneDef
            {
                skill = SkillId.MountainBody, nameA = "铁山", nameB = "力山",
                descA = "防御 +30%→+45%，转攻率 10%→5%",
                descB = "转攻率 10%→16%、上限 25%→35%，但防御 +30%→+18%",
            },
            new RuneDef
            {
                skill = SkillId.HeavyImpact, nameA = "粉碎", nameB = "沉势",
                descA = "崩山伤害 +20%→+35%，攻速 -10%→-15%",
                descB = "攻速惩罚 -10%→-2%，暴伤保持 +40%",
            },
            new RuneDef
            {
                skill = SkillId.Vampire, nameA = "嗜血", nameB = "血偿",
                descA = "吸血 20%→30%，但受到伤害 +8%",
                descB = "吸血 20%→16%，生命低于 40% 时翻倍",
            },
            // ---- 触发 ----
            new RuneDef
            {
                skill = SkillId.Arc, nameA = "连锁", nameB = "贯注",
                descA = "触发率 25%→35%，伤害 60%→45%",
                descB = "触发率 25%→18%，伤害 60%→105%",
            },
            new RuneDef
            {
                skill = SkillId.ThunderPunish, nameA = "速惩", nameB = "重惩",
                descA = "每 4→2 次闪电触发，伤害 150%→90%",
                descB = "每 4→6 次闪电触发，伤害 150%→260%",
            },
            new RuneDef
            {
                skill = SkillId.Aftershock, nameA = "频震", nameB = "深震",
                descA = "触发率 40%→55%，伤害 60%→45%",
                descB = "触发率 40%→28%，伤害 60%→95%",
            },
            new RuneDef
            {
                skill = SkillId.RockArmor, nameA = "厚甲", nameB = "速甲",
                descA = "护盾 10%→14%、上限 30%→40%，但触发率 35%→25%",
                descB = "间隔 1.5→0.8 秒，但护盾 10%→7%",
            },
            new RuneDef
            {
                skill = SkillId.LastStand, nameA = "死守", nameB = "再起",
                descA = "免死 2→3 秒，但冷却 60→75 秒",
                descB = "冷却 60→45 秒，但免死 2→1.2 秒",
            },
        };

        public static bool Has(SkillId id)
        {
            int i = (int)id;
            return i >= 0 && i < All.Length;
        }

        public static RuneDef Get(SkillId id)
        {
            int i = (int)id;
            return i >= 0 && i < All.Length ? All[i] : null;
        }

        /// <summary>某形态的名字；未解锁 / 非法 id 返回空串。</summary>
        public static string FormName(SkillId id, RuneForm form)
        {
            RuneDef def = Get(id);
            if (def == null) return "";
            return form == RuneForm.B ? def.nameB : def.nameA;
        }

        public static string FormDesc(SkillId id, RuneForm form)
        {
            RuneDef def = Get(id);
            if (def == null) return "";
            return form == RuneForm.B ? def.descB : def.descA;
        }
    }

    /// <summary>
    /// 符文对战斗数值的实际覆写。所有读取点都走这里，保证「未解锁 = 原样返回基础值」。
    /// 放在静态类里而不是 PlayerData：BattleSystem 里的 def 字段是只读的静态表，覆写只能发生在读取处。
    /// </summary>
    public static class RuneMods
    {
        public static bool IsA(PlayerData p, SkillId id) =>
            p != null && p.HasRuneUnlocked(id) && p.RuneFormOf(id) == RuneForm.A;

        public static bool IsB(PlayerData p, SkillId id) =>
            p != null && p.HasRuneUnlocked(id) && p.RuneFormOf(id) == RuneForm.B;

        private static RuneForm Form(PlayerData p, SkillId id)
        {
            if (p == null || !p.HasRuneUnlocked(id)) return RuneForm.A;
            return p.RuneFormOf(id);
        }

        /// <summary>按形态覆写某个字段；未解锁时原样返回 baseValue。</summary>
        public static float Field(PlayerData p, SkillId id, RuneField field, float baseValue)
        {
            if (p == null || !p.HasRuneUnlocked(id)) return baseValue;
            RuneForm f = Form(p, id);
            bool a = f == RuneForm.A;

            switch (id)
            {
                case SkillId.ThunderRush:
                    if (field == RuneField.Value) return a ? 4.48f : 1.92f;
                    if (field == RuneField.Value2) return a ? 0f : 4f;
                    if (field == RuneField.Value3) return a ? 0.20f : 0.35f;
                    break;

                case SkillId.ThunderGod:
                    if (field == RuneField.Value2) return a ? 1.60f : baseValue;
                    if (field == RuneField.Value3) return a ? baseValue : 2.00f;
                    if (field == RuneField.Cooldown) return a ? baseValue : 40f;
                    break;

                case SkillId.MountainSlam:
                    if (field == RuneField.Value3) return a ? 0.50f : 0.15f;
                    if (field == RuneField.Cooldown) return a ? 12f : baseValue;
                    break;

                case SkillId.Bulwark:
                    if (field == RuneField.Value) return a ? 0.30f : 0.12f;
                    if (field == RuneField.Value2) return a ? 0.12f : 0.35f;
                    break;

                case SkillId.WarCry:
                    if (field == RuneField.Value) return a ? 0.30f : 0.10f;
                    if (field == RuneField.Value2) return a ? 0f : 0.18f;
                    break;

                case SkillId.StaticResonance:
                    if (field == RuneField.CritBonus) return a ? 0.16f : baseValue;
                    if (field == RuneField.Value) return a ? baseValue : 0.32f;
                    if (field == RuneField.Value2) return a ? 1.00f : 0f;
                    break;

                case SkillId.StormHaste:
                    if (field == RuneField.Value2) return a ? 0.03f : 0.015f;
                    if (field == RuneField.Duration) return a ? baseValue : 8f;
                    break;

                case SkillId.MountainBody:
                    if (field == RuneField.Value) return a ? 0.45f : 0.18f;
                    if (field == RuneField.Value2) return a ? 0.05f : 0.16f;
                    if (field == RuneField.Value3) return a ? baseValue : 0.35f;
                    break;

                case SkillId.HeavyImpact:
                    if (field == RuneField.Value) return a ? baseValue : 0.40f;
                    if (field == RuneField.Value2) return a ? -0.15f : -0.02f;
                    if (field == RuneField.Value3) return a ? 0.35f : baseValue;
                    break;

                case SkillId.Vampire:
                    if (field == RuneField.Value) return a ? 0.30f : 0.16f;
                    break;

                case SkillId.Arc:
                    if (field == RuneField.TriggerChance) return a ? 0.35f : 0.18f;
                    if (field == RuneField.Value) return a ? 0.45f : 1.05f;
                    break;

                case SkillId.ThunderPunish:
                    if (field == RuneField.Hits) return a ? 2f : 6f;
                    if (field == RuneField.Value) return a ? 0.90f : 2.60f;
                    break;

                case SkillId.Aftershock:
                    if (field == RuneField.TriggerChance) return a ? 0.55f : 0.28f;
                    if (field == RuneField.Value) return a ? 0.45f : 0.95f;
                    break;

                case SkillId.RockArmor:
                    if (field == RuneField.Value) return a ? 0.14f : 0.07f;
                    if (field == RuneField.Value2) return a ? 0.40f : baseValue;
                    if (field == RuneField.Value3) return a ? baseValue : 0.7f;
                    if (field == RuneField.TriggerChance) return a ? 0.25f : baseValue;
                    break;

                case SkillId.LastStand:
                    if (field == RuneField.Duration) return a ? 3f : 1.2f;
                    if (field == RuneField.Cooldown) return a ? 75f : 45f;
                    break;
            }

            return baseValue;
        }

        /// <summary>风暴加速最多叠几层：疾风形态 3 层，其余 5 层。</summary>
        public static int StormHasteMaxStacks(PlayerData p) => IsA(p, SkillId.StormHaste) ? 3 : 5;

        /// <summary>雷神形态「审罚」：满印不再重置雷霆突袭。</summary>
        public static bool SuppressFullMarkReset(PlayerData p) => IsA(p, SkillId.ThunderGod);

        /// <summary>山崩形态「余震」：每层岩层额外造成的伤害倍率（0 = 不触发）。</summary>
        public static float MountainSlamLayerBonus(PlayerData p) => IsB(p, SkillId.MountainSlam) ? 0.25f : 0f;

        /// <summary>吸血鬼形态「嗜血」：受到伤害乘区。</summary>
        public static float TakenDamageMultiplier(PlayerData p) => IsA(p, SkillId.Vampire) ? 1.08f : 1f;

        /// <summary>吸血鬼形态「血偿」：低血时吸血翻倍。</summary>
        public static float LifestealScale(PlayerData p)
        {
            if (!IsB(p, SkillId.Vampire)) return 1f;
            return p.currentHp <= Mathf.RoundToInt(p.MaxHp * 0.40f) ? 2f : 1f;
        }
    }
}

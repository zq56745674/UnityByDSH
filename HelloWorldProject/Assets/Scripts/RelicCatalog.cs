using System.Collections.Generic;
using UnityEngine;

namespace AutoBattle
{
    /// <summary>遗物分类：锋（进攻）/ 守（生存）/ 贪（收益）/ 险（双刃）。</summary>
    public enum RelicClass
    {
        Edge,
        Guard,
        Greed,
        Risk,
    }

    /// <summary>
    /// 一件遗物的静态定义与数值。v1 只有 tier1 的 12 件：tier 本身不改强度，
    /// 只决定「什么条件下会出现在三选一里」（tier2/tier3 依赖深层契约分，暂未开放）。
    ///
    /// 数值纪律（docs/gameplay-expansion.md §4.4）：
    /// - 单件等效战力 ≤ +4%，双刃 ≤ +9%；
    /// - 6 槽全满 ≤ +18%；超了就砍单件，不改槽位上限；
    /// - 遗物走**独立乘区**（最终伤害乘数），不并入技能加算池。
    /// </summary>
    public sealed class RelicDef
    {
        public int id;
        public string name;
        public string text;
        public RelicClass cls;
        public int tier = 1;

        // ---- 数值（未用到的字段保持中性值）----
        /// <summary>最终伤害乘数（独立乘区，落在 BattleSystem.HitMonster 的 dmg 上）。</summary>
        public float damageMult = 1f;
        /// <summary>暴击伤害倍率的加算项（0.06 = +6 个百分点）。</summary>
        public float critDamageAdd;
        /// <summary>暴击率的加算项。</summary>
        public float critChanceAdd;
        /// <summary>攻速加成（与装备/技能攻速同池加算）。</summary>
        public float hasteAdd;
        /// <summary>吸血（与吸血鬼被动同池加算）。</summary>
        public float lifestealAdd;
        /// <summary>受到伤害的乘数（< 1 减伤，> 1 增伤）。</summary>
        public float damageTakenMult = 1f;
        /// <summary>生命低于阈值时的额外减伤（0~1）。</summary>
        public float lowHpReduction;
        /// <summary>最大生命乘数。</summary>
        public float maxHpMult = 1f;
        /// <summary>金币乘数（只乘正向来源）。</summary>
        public float goldMult = 1f;
        /// <summary>掉落率加算。</summary>
        public float dropBonus;
        /// <summary>经验乘数。</summary>
        public float expMult = 1f;

        public string ClassName
        {
            get
            {
                switch (cls)
                {
                    case RelicClass.Edge: return "锋";
                    case RelicClass.Guard: return "守";
                    case RelicClass.Greed: return "贪";
                    default: return "险";
                }
            }
        }

        public Color ClassColor
        {
            get
            {
                switch (cls)
                {
                    case RelicClass.Edge: return new Color(0.60f, 0.20f, 0.12f);
                    case RelicClass.Guard: return new Color(0.16f, 0.34f, 0.54f);
                    case RelicClass.Greed: return new Color(0.48f, 0.33f, 0.05f);
                    default: return new Color(0.42f, 0.16f, 0.40f);
                }
            }
        }
    }

    /// <summary>
    /// 深渊遗物目录与抽取规则。全部纯数据 + 确定性随机，不碰 UI、不碰存档，
    /// 编辑器探针可以照原样调用（验收第 3 条要求「同种子两次序列完全一致」）。
    /// </summary>
    public static class RelicCatalog
    {
        /// <summary>槽位上限：v1 固定 6；契约分 800 / 1500 扩到 7 / 8 留给 B。</summary>
        public const int SlotCount = 6;

        /// <summary>一次给几个候选。</summary>
        public const int ChoiceCount = 3;

        /// <summary>每清多少层触发一次三选一（每两个 BOSS 层）。</summary>
        public const int FloorStep = 10;

        /// <summary>生命低于这个比例时，「残喘」类遗物生效。</summary>
        public const float LowHpThreshold = 0.3f;

        public const int Tier1Count = 12;

        private static readonly RelicDef[] Pool =
        {
            // ---- 锋：进攻 ----
            new RelicDef
            {
                id = 0, name = "裂石之刃", cls = RelicClass.Edge,
                text = "造成的伤害 +4%",
                damageMult = 1.04f,
            },
            new RelicDef
            {
                id = 1, name = "猎手之眼", cls = RelicClass.Edge,
                text = "暴击伤害 +6%",
                critDamageAdd = 0.06f,
            },
            new RelicDef
            {
                id = 2, name = "疾风护腕", cls = RelicClass.Edge,
                text = "攻击速度 +3%",
                hasteAdd = 0.03f,
            },

            // ---- 守：生存 ----
            new RelicDef
            {
                id = 3, name = "血之器", cls = RelicClass.Guard,
                text = "造成伤害的 1.5% 回复为生命",
                lifestealAdd = 0.015f,
            },
            new RelicDef
            {
                id = 4, name = "铁壁", cls = RelicClass.Guard,
                text = "受到的伤害 -3%",
                damageTakenMult = 0.97f,
            },
            new RelicDef
            {
                id = 5, name = "残喘", cls = RelicClass.Guard,
                text = "生命低于 30% 时，受到的伤害再 -12%",
                lowHpReduction = 0.12f,
            },

            // ---- 贪：收益 ----
            new RelicDef
            {
                id = 6, name = "金脉", cls = RelicClass.Greed,
                text = "获得的金币 +12%",
                goldMult = 1.12f,
            },
            new RelicDef
            {
                id = 7, name = "拾荒者之手", cls = RelicClass.Greed,
                text = "装备掉落率 +6%",
                dropBonus = 0.06f,
            },
            new RelicDef
            {
                id = 8, name = "远见", cls = RelicClass.Greed,
                text = "获得的经验 +8%",
                expMult = 1.08f,
            },

            // ---- 险：双刃 ----
            new RelicDef
            {
                id = 9, name = "血祭", cls = RelicClass.Risk,
                text = "造成的伤害 +9%，最大生命 -6%",
                damageMult = 1.09f, maxHpMult = 0.94f,
            },
            new RelicDef
            {
                id = 10, name = "孤注一掷", cls = RelicClass.Risk,
                text = "暴击率 +6%，暴击伤害 -12%",
                critChanceAdd = 0.06f, critDamageAdd = -0.12f,
            },
            new RelicDef
            {
                id = 11, name = "死斗", cls = RelicClass.Risk,
                text = "造成的伤害 +9%，受到的伤害 +6%",
                damageMult = 1.09f, damageTakenMult = 1.06f,
            },
        };

        public static RelicDef Get(int id)
        {
            return id >= 0 && id < Pool.Length ? Pool[id] : null;
        }

        public static RelicDef[] All => Pool;

        /// <summary>
        /// 按「种子 + 层号」抽 <see cref="ChoiceCount"/> 个不重复候选，并排除本局已持有的。
        /// 用 <see cref="System.Random"/> 而不是 UnityEngine.Random：后者是全局流，
        /// 战斗里的暴击/掉落也在消耗它，同一个种子换一条战斗路径就会抽出不同遗物。
        /// </summary>
        public static List<int> RollChoices(int seed, int floor, IList<int> owned)
        {
            var result = new List<int>(ChoiceCount);
            int[] order = new int[Pool.Length];
            for (int i = 0; i < order.Length; i++) order[i] = i;

            var rng = new System.Random(unchecked(seed * 1000003 + floor * 7919 + 17));
            // Fisher-Yates：确定性洗牌，只依赖种子与层号。
            for (int i = order.Length - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                int tmp = order[i];
                order[i] = order[j];
                order[j] = tmp;
            }

            for (int i = 0; i < order.Length && result.Count < ChoiceCount; i++)
            {
                if (owned != null && owned.Contains(order[i])) continue;
                result.Add(order[i]);
            }
            return result;
        }

        /// <summary>本局该有几格：v1 固定 6（后续按契约分扩到 7/8，B 落地时改这里）。</summary>
        public static int SlotsFor(int contractScore) => SlotCount;

        /// <summary>这一层清完要不要弹三选一。</summary>
        public static bool ShouldOffer(int clearedFloor)
        {
            return clearedFloor > 0
                && clearedFloor % FloorStep == 0
                && clearedFloor < EndlessCatalog.MaxFloor;
        }
    }

    /// <summary>
    /// 遗物加成的聚合结果。<see cref="PlayerData.relic"/> 为 null 时表示本局没有遗物
    /// （战役、或还没拿到第一件），所有属性读取都走「中性值」分支。
    /// </summary>
    public sealed class RelicMods
    {
        public float damageMult = 1f;
        public float critDamageAdd;
        public float critChanceAdd;
        public float hasteAdd;
        public float lifestealAdd;
        public float damageTakenMult = 1f;
        public float lowHpReduction;
        public float maxHpMult = 1f;
        public float goldMult = 1f;
        public float dropBonus;
        public float expMult = 1f;

        /// <summary>按持有列表重算全部加成。同类乘区连乘、同类加算项相加。</summary>
        public static RelicMods Build(IList<int> ids)
        {
            if (ids == null || ids.Count == 0) return null;

            var mods = new RelicMods();
            for (int i = 0; i < ids.Count; i++)
            {
                RelicDef def = RelicCatalog.Get(ids[i]);
                if (def == null) continue;
                mods.damageMult *= def.damageMult;
                mods.critDamageAdd += def.critDamageAdd;
                mods.critChanceAdd += def.critChanceAdd;
                mods.hasteAdd += def.hasteAdd;
                mods.lifestealAdd += def.lifestealAdd;
                mods.damageTakenMult *= def.damageTakenMult;
                mods.lowHpReduction += def.lowHpReduction;
                mods.maxHpMult *= def.maxHpMult;
                mods.goldMult *= def.goldMult;
                mods.dropBonus += def.dropBonus;
                mods.expMult *= def.expMult;
            }

            // 兜底：残喘类叠加最多减伤 40%，避免堆成物理免疫。
            mods.lowHpReduction = Mathf.Clamp(mods.lowHpReduction, 0f, 0.4f);
            return mods;
        }
    }
}

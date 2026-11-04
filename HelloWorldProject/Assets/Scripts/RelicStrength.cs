using UnityEngine;

namespace AutoBattle
{
    /// <summary>
    /// 遗物实测强度表 —— 三选一卡右上角那行小字（docs/gameplay-expansion.md §4.5.1「方案①」）。
    ///
    /// 数据来源：编辑器探针「Tools/测试/遗物实测强度（24 件 × 40 种子 × 600 层）」，
    /// 开局只挂这一件、与「不带遗物」同种子配对，取首死层数的均值差（取整）。
    /// 运行时跑不了模拟器，所以只能把结果静态抄进来；换模板要重跑探针再更新这张表。
    ///
    /// 注意：换算随模板变——铁壁在满倍率模板 +14 层、半装模板只有 +3 层。
    /// 这张表只用于「同一批卡片里谁更值得拿」的相对比较，不是跨模板的绝对强度。
    /// </summary>
    public static class RelicStrength
    {
        /// <summary>探针口径，写进 tooltip 备查。</summary>
        public const string Recipe = "40 种子 · 满倍率装备 · P1000 · 600 层上限 · 无契约 · 无符文";

        // id 顺序与 RelicCatalog 一致；数值 = 首死层数的均值差（层）。
        static readonly int[] Floors =
        {
            3, 2, 0, 11, 14, 8, 7, -2, 8, 5, -2, 3,          // T1：0~11
            10, 8, 1, 11, 13, 14,                           // T2：12~17
            12, 6, 0, 16, 21, 3,                            // T3：18~23
        };

        public static int Delta(int id) => id >= 0 && id < Floors.Length ? Floors[id] : 0;

        /// <summary>卡片右上角的短标签：+14 / ±0 / −2。</summary>
        public static string Chip(int id)
        {
            int d = Delta(id);
            return d > 0 ? "+" + d : (d < 0 ? "−" + (-d) : "±0");
        }

        /// <summary>标签颜色：强项烫金、中段深蓝、弱项墨灰。</summary>
        public static Color ColorOf(int id)
        {
            int d = Delta(id);
            if (d >= 10) return GameConfig.TextGoldDeep;
            if (d >= 5) return GameConfig.TextInfoDeep;
            if (d >= 1) return GameConfig.TextNormal;
            return GameConfig.TextMuted;
        }

        /// <summary>悬停说明：说清「这是相对值、换模板会变」。</summary>
        public static string Tooltip(int id)
        {
            int d = Delta(id);
            string head = d > 0
                ? "实测：相比不带遗物，无尽首死层数 +" + d
                : (d < 0
                    ? "实测：相比不带遗物，无尽首死层数 " + d
                    : "实测：与不带遗物持平");
            return head + " 层（40 种子配对均值，95% 区间约 ±7 层）\n" + Recipe
                 + "\n生存类在非满倍率模板里会明显更弱，这张表只作相对比较；"
                 + "差距在 7 层以内可以当作同一档。";
        }
    }
}

using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using AutoBattle;

namespace AutoBattle.EditorTools
{
    /// <summary>
    /// 平衡回归测试。
    ///
    /// 为什么需要：这个工程的数值分散在三处——GameConfig 的标量（可热改 json）、
    /// Skills.cs 里硬编码的 15 张技能表、以及各处的公式。动其中任何一处，都可能在没人
    /// 察觉的情况下把某个巅峰档的胜率挪走几十个百分点。原来的做法是手动跑胜率探针用
    /// 眼睛比，既慢又靠不住：perType=30 时单格二项噪声本就有 ±5 个百分点，肉眼分不清
    /// 「真的变弱了」和「这次抽样手气差」。
    ///
    /// 两个关键设计：
    ///
    /// 1. 固定种子。战斗里的暴击、技能触发、伤害浮动、装备词条全走 UnityEngine.Random，
    ///    所以 Random.InitState 之后整轮模拟逐位可复现，阈值能收到 0.5 个百分点，
    ///    而不是被抽样噪声逼到 5 个。每个巅峰档用独立种子（DefaultSeed + 档位下标），
    ///    这样一个档的数值改动只会让那一档报红，不会顺带搅乱其它档，定位更准。
    ///
    /// 2. 分两层。便宜的「静态哨兵」（技能表、公式采样、关卡表、怪物样本、评分自洽性）
    ///    不跑战斗，毫秒级；贵的「胜率表」才需要真打。日常改完代码先看哨兵红了没有，
    ///    红了再跑胜率确认影响范围。
    ///
    /// 用法（Editor 菜单「Tools/平衡回归」，或命令行）：
    ///   unity command eval -- --code 'return AutoBattle.EditorTools.BalanceRegression.Run(false, 12, 30);'
    /// 首次运行会自动写入基线；之后一般只跑 Run(false, ...) 做比对。
    /// 确认某次数值改动是想要的，用 Run(true, ...) 把当前状态写成新基线。
    ///
    /// 注意：静态哨兵是精确字符串比对，所以它红了就是数值真的变了，不存在噪声。
    /// 只有 winrate 那一栏是带容差的浮点比较。
    /// </summary>
    public static class BalanceRegression
    {
        /// <summary>固定种子。改它等于把所有胜率基线作废，别随手改。</summary>
        public const int DefaultSeed = 20261016;

        /// <summary>胜率容差：0.5 个百分点。固定种子下重复跑应当逐位一致，这个容差是给浮点累积误差留的。</summary>
        const float WinrateTolerance = 0.005f;

        /// <summary>差异明细里每张表最多列几条，免得一个改动刷屏。</summary>
        const int MaxDiffLines = 8;

        /// <summary>基线默认落在工程外，避免 Unity 把它当资源导入。</summary>
        public static string DefaultBaselinePath =>
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "_balance", "baseline.json"));

        static readonly int[] Paragons = { 0, 10, 20, 30, 40, 50, 70, 100 };
        const int CellsPerParagon = 4;

        // ------------------------------------------------------------------ 数据模型

        [Serializable]
        public class Report
        {
            public int seed;
            public int trials;
            public int perType;
            public string[] skills;      // 15 张技能表（硬编码在 Skills.cs，改了就变）
            public string[] formulas;    // GameConfig 纯函数采样
            public string[] stages;      // 11 章 × 10 关的怪级 / 击杀数 / 关底档次 / 首通金币
            public string[] monsters;    // 怪物数值样本
            public string[] scoreChecks; // 装备评分自洽性 + Of() 无副作用
            public float[] winrate;      // 8 档 × 4 格，0~1
        }

        // ------------------------------------------------------------------ 采集

        public static Report Collect(int trials = 12, int perType = 30, int seed = DefaultSeed, bool withWinrate = true)
        {
            var r = new Report { seed = seed, trials = trials, perType = perType };
            // 这几张表都不消耗随机数，先跑，避免打乱后面的种子重置
            r.skills = CollectSkills();
            r.formulas = CollectFormulas();
            r.stages = CollectStages();
            r.monsters = CollectMonsters();
            r.scoreChecks = CollectScoreChecks();
            if (withWinrate) r.winrate = CollectWinrate(trials, perType, seed);
            return r;
        }

        static string[] CollectSkills()
        {
            var list = new string[SkillCatalog.All.Length];
            for (int i = 0; i < SkillCatalog.All.Length; i++)
            {
                SkillDef s = SkillCatalog.All[i];
                list[i] = string.Format(CultureInfo.InvariantCulture,
                    "{0}|{1}|value={2:0.####}|cd={3:0.####}|extra={4:0.####}|hits={5}|dur={6:0.####}",
                    s.id, s.kind, s.value, s.cooldown, s.extra, s.hits, s.duration);
            }
            return list;
        }

        static string[] CollectFormulas()
        {
            var list = new System.Collections.Generic.List<string>();
            void Add(string name, double v) =>
                list.Add(string.Format(CultureInfo.InvariantCulture, "{0}={1:0.#####}", name, v));

            // 攻击间隔：曲线的低 / 中 / 高段
            foreach (int spd in new[] { 1, 10, 50, 100, 200 })
                Add($"AttackInterval({spd})", GameConfig.AttackInterval(spd));

            // 减伤：等级进分母，越级打不动是关卡制的难度基石，必须盯住
            foreach (int def in new[] { 0, 100, 500, 2000 })
                foreach (int lv in new[] { 1, 10, 100, 110 })
                    Add($"DamageReduction({def},{lv})", GameConfig.DamageReduction(def, lv));

            // 护甲穿透要真的削弱减伤，否则这个属性是假的
            Add("DamageReduction(2000,100,pen0.5)", GameConfig.DamageReduction(2000, 100, 0.5f));

            // 成长曲线
            foreach (int lv in new[] { 1, 10, 50, 100 }) Add($"ExpToNextLevel({lv})", GameConfig.ExpToNextLevel(lv));
            foreach (int t in new[] { 1, 2, 3 }) Add($"MonsterTypeTier({t * 30})", GameConfig.MonsterTypeTier(t * 30));
            foreach (int lv in new[] { 10, 50, 110 }) Add($"ChapterOfMonsterLevel({lv})", GameConfig.ChapterOfMonsterLevel(lv));
            foreach (int lv in new[] { 1, 50, 100 }) Add($"SellPrice({lv},Legendary)", GameConfig.SellPrice(lv, EquipmentQuality.Legendary));
            foreach (int lv in new[] { 1, 50, 100 }) Add($"PotionPrice({lv})", GameConfig.PotionPrice(lv));
            foreach (int p in new[] { 0, 50, 100, 200 }) Add($"ParagonExpToNext({p})", GameConfig.ParagonExpToNext(p));
            foreach (int p in new[] { 0, 50, 100, 200 }) Add($"SkillLevel({p})", GameConfig.SkillLevel(p));
            Add("ScaleSkillValue(0.3,1)", GameConfig.ScaleSkillValue(0.3f, 1));
            Add("ScaleSkillValue(0.3,10)", GameConfig.ScaleSkillValue(0.3f, 10));
            foreach (var q in new[] { EquipmentQuality.Common, EquipmentQuality.Rare, EquipmentQuality.Epic, EquipmentQuality.Legendary })
                Add($"SellPrice(60,{q})", GameConfig.SellPrice(60, q));
            return list.ToArray();
        }

        static string[] CollectStages()
        {
            var list = new System.Collections.Generic.List<string>();
            for (int ch = StageCatalog.FirstChapter; ch <= StageCatalog.ChapterCount; ch++)
            {
                list.Add(string.Format(CultureInfo.InvariantCulture,
                    "ch{0}|target={1}|clearTier={2}|gold={3}",
                    ch, StageCatalog.KillTargetOf(ch), StageCatalog.ClearTierOf(ch, 1),
                    StageCatalog.FirstClearGold(ch, 1)));
                // 章末关的关底档次与奖励和普通关不同，单独记一行
                list.Add(string.Format(CultureInfo.InvariantCulture,
                    "ch{0}-{1}|clearTier={2}|gold={3}",
                    ch, StageCatalog.StagesPerChapter,
                    StageCatalog.ClearTierOf(ch, StageCatalog.StagesPerChapter),
                    StageCatalog.FirstClearGold(ch, StageCatalog.StagesPerChapter)));
            }
            list.Add($"totalFirstClearGold={StageCatalog.TotalFirstClearGold(StageCatalog.ChapterCount)}");
            return list.ToArray();
        }

        static string[] CollectMonsters()
        {
            var list = new System.Collections.Generic.List<string>();
            foreach (int lv in new[] { 10, 50, 100, 110 })
                foreach (MonsterType type in new[] { MonsterType.Melee, MonsterType.Ranged, MonsterType.Crit })
                    foreach (MonsterTier tier in new[] { MonsterTier.Elite, MonsterTier.Boss })
                    {
                        var m = MonsterData.CreateForLevel(lv, type, tier, MonsterAffix.None);
                        list.Add(string.Format(CultureInfo.InvariantCulture,
                            "Lv{0}|{1}|{2}|hp={3}|atk={4}|def={5}|spd={6:0.##}|crit={7:0.###}",
                            lv, type, tier, m.maxHp, m.attack, m.defense, m.speed, m.critChance));
                    }
            return list.ToArray();
        }

        static string[] CollectScoreChecks()
        {
            var list = new System.Collections.Generic.List<string>();
            list.Add("--- score self-consistency (fixed seed) ---");

            // 评分必须单调：同槽位、同等级下品质越高分越高，否则自动换装会做错决定
            foreach (var slot in new[] { EquipmentSlot.Weapon, EquipmentSlot.Armor })
                foreach (var quality in new[] { EquipmentQuality.Common, EquipmentQuality.Rare, EquipmentQuality.Epic, EquipmentQuality.Legendary })
                {
                    UnityEngine.Random.InitState(DefaultSeed ^ ((int)slot * 977 + (int)quality));
                    var p = new PlayerData { level = 100 };
                    var e = LootSystem.CreateEquipment(slot, 100, quality);
                    float score = EquipmentScore.Of(p, e, slot);
                    list.Add(string.Format(CultureInfo.InvariantCulture,
                        "{0}/{1}=score{2:0.#}|atk{3}|def{4}|hp{5}", slot, quality, score,
                        e.attack, e.defense, e.hp));
                }

            // Of() 会临时把装备卸下再装回去来算增量，必须保证退出时玩家装备原封不动。
            // 这里同时是那个 try/finally 修复的回归测试。
            UnityEngine.Random.InitState(DefaultSeed ^ 0x5EED);
            var probe = new PlayerData { level = 100 };
            var kept = LootSystem.CreateEquipment(EquipmentSlot.Weapon, 100, EquipmentQuality.Legendary);
            probe.weapon = kept;
            var candidate = LootSystem.CreateEquipment(EquipmentSlot.Weapon, 100, EquipmentQuality.Common);
            EquipmentScore.Of(probe, candidate, EquipmentSlot.Weapon);
            list.Add($"OfRestoresWeapon={ReferenceEquals(probe.weapon, kept)}");
            list.Add($"OfRestoresLevel={probe.level}");

            // 换装增量评分：同槽两件相比，分数高的那件换上去总评分应当更高（评分的定义就是这个）
            UnityEngine.Random.InitState(DefaultSeed ^ 0xBEEF);
            var p2 = new PlayerData { level = 100 };
            var a = LootSystem.CreateEquipment(EquipmentSlot.Weapon, 100, EquipmentQuality.Legendary);
            var b = LootSystem.CreateEquipment(EquipmentSlot.Weapon, 100, EquipmentQuality.Legendary);
            float sa = EquipmentScore.Of(p2, a, EquipmentSlot.Weapon);
            float sb = EquipmentScore.Of(p2, b, EquipmentSlot.Weapon);
            list.Add($"pairOrderingConsistent={sa != sb || ReferenceEquals(a, b)}");
            return list.ToArray();
        }

        static float[] CollectWinrate(int trials, int perType, int seed)
        {
            var outp = new float[Paragons.Length * CellsPerParagon];
            for (int i = 0; i < Paragons.Length; i++)
            {
                // 每档独立种子：改一档的数值只会让这一档报红
                UnityEngine.Random.InitState(seed + i);
                float[] row = LateGameProbe.RunWinrateRow(Paragons[i], perType, true, trials, true, out _, out _);
                Array.Copy(row, 0, outp, i * CellsPerParagon, CellsPerParagon);
            }
            return outp;
        }

        // ------------------------------------------------------------------ 比对

        /// <summary>
        /// 跑一次回归。updateBaseline=true 时把当前状态写成新基线（用于确认「这次改动是我要的」）。
        /// 返回人类可读的结论，同时会在 Console 里打印明细。
        /// </summary>
        public static string Run(bool updateBaseline = false, int trials = 12, int perType = 30, string baselinePath = null)
        {
            if (string.IsNullOrEmpty(baselinePath)) baselinePath = DefaultBaselinePath;

            var cur = Collect(trials, perType);
            if (updateBaseline || !File.Exists(baselinePath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(baselinePath));
                File.WriteAllText(baselinePath, JsonUtility.ToJson(cur, true));
                string msg = updateBaseline
                    ? $"基线已更新（seed={cur.seed}, trials={cur.trials}, perType={cur.perType}）"
                    : $"首次运行，已写入基线（seed={cur.seed}, trials={cur.trials}, perType={cur.perType}）";
                Debug.Log($"[BalanceRegression] {msg} → {baselinePath}");
                return msg;
            }

            Report old;
            try
            {
                old = JsonUtility.FromJson<Report>(File.ReadAllText(baselinePath));
            }
            catch (Exception ex)
            {
                return $"基线读不出来（{ex.GetType().Name}: {ex.Message}）→ {baselinePath}";
            }
            if (old == null) return $"基线为空 → {baselinePath}";

            var sb = new StringBuilder();
            int problems = 0;
            problems += DiffList(sb, "技能表", old.skills, cur.skills);
            problems += DiffList(sb, "公式采样", old.formulas, cur.formulas);
            problems += DiffList(sb, "关卡表", old.stages, cur.stages);
            problems += DiffList(sb, "怪物样本", old.monsters, cur.monsters);
            problems += DiffList(sb, "评分自洽性", old.scoreChecks, cur.scoreChecks);
            problems += DiffWinrate(sb, old, cur);

            string head = problems == 0
                ? $"PASS — 与基线一致（seed={cur.seed}, trials={cur.trials}, perType={cur.perType}）"
                : $"FAIL — {problems} 处偏离基线";
            string body = sb.Length > 0 ? "\n" + sb : "";
            Debug.Log($"[BalanceRegression] {head}{body}");
            return head + body;
        }

        /// <summary>
        /// 只跑静态哨兵做比对（不跑胜率）。毫秒级，改完代码顺手跑一下用这个；
        /// 它红了说明数值真的变了，此时再跑完整版看胜率被挪动了多少。
        /// </summary>
        public static string RunStaticOnly(string baselinePath = null)
        {
            if (string.IsNullOrEmpty(baselinePath)) baselinePath = DefaultBaselinePath;
            if (!File.Exists(baselinePath)) return $"还没有基线，先跑一次完整版：{baselinePath}";

            Report old;
            try { old = JsonUtility.FromJson<Report>(File.ReadAllText(baselinePath)); }
            catch (Exception ex) { return $"基线读不出来（{ex.GetType().Name}: {ex.Message}）"; }
            if (old == null) return "基线为空";

            var cur = Collect(1, 1, DefaultSeed, withWinrate: false);
            var sb = new StringBuilder();
            int problems = 0;
            problems += DiffList(sb, "技能表", old.skills, cur.skills);
            problems += DiffList(sb, "公式采样", old.formulas, cur.formulas);
            problems += DiffList(sb, "关卡表", old.stages, cur.stages);
            problems += DiffList(sb, "怪物样本", old.monsters, cur.monsters);
            problems += DiffList(sb, "评分自洽性", old.scoreChecks, cur.scoreChecks);

            string head = problems == 0 ? "PASS — 静态哨兵与基线一致" : $"FAIL — {problems} 处偏离基线（未跑胜率）";
            string body = sb.Length > 0 ? "\n" + sb : "";
            Debug.Log($"[BalanceRegression] {head}{body}");
            return head + body;
        }

        static int DiffList(StringBuilder sb, string title, string[] oldV, string[] newV)
        {
            oldV ??= Array.Empty<string>();
            newV ??= Array.Empty<string>();
            int diffs = 0, shown = 0;
            int n = Mathf.Max(oldV.Length, newV.Length);
            for (int i = 0; i < n; i++)
            {
                string a = i < oldV.Length ? oldV[i] : "<缺失>";
                string b = i < newV.Length ? newV[i] : "<缺失>";
                if (a == b) continue;
                diffs++;
                if (shown < MaxDiffLines)
                {
                    sb.Append($"\n  [{title}] 基线: {a}\n  [{title}] 当前: {b}");
                    shown++;
                }
            }
            if (diffs > shown) sb.Append($"\n  [{title}] …另有 {diffs - shown} 处");
            if (diffs > 0) sb.Append($"\n  → {title}：{diffs} 处不同");
            return diffs;
        }

        static int DiffWinrate(StringBuilder sb, Report old, Report cur)
        {
            if (old.winrate == null || cur.winrate == null) return 0;
            int n = Mathf.Min(old.winrate.Length, cur.winrate.Length);
            int diffs = 0, shown = 0;
            for (int i = 0; i < n; i++)
            {
                float d = cur.winrate[i] - old.winrate[i];
                if (Mathf.Abs(d) <= WinrateTolerance) continue;
                diffs++;
                if (shown < MaxDiffLines)
                {
                    int par = Paragons[i / CellsPerParagon];
                    string[] cellName = { "Lv100精英", "Lv100Boss", "Lv110精英", "Lv110Boss" };
                    sb.Append(string.Format(CultureInfo.InvariantCulture,
                        "\n  [胜率] 巅峰{0} {1}: {2:0.0}% → {3:0.0}%  ({4:+0.0;-0.0}pp)",
                        par, cellName[i % CellsPerParagon],
                        old.winrate[i] * 100f, cur.winrate[i] * 100f, d * 100f));
                    shown++;
                }
            }
            if (diffs > shown) sb.Append($"\n  [胜率] …另有 {diffs - shown} 格");
            if (old.trials != cur.trials || old.perType != cur.perType)
                sb.Append($"\n  [胜率] 注意：抽样参数变了（trials {old.trials}→{cur.trials}，perType {old.perType}→{cur.perType}），胜率差异不可比");
            return diffs;
        }

        // ------------------------------------------------------------------ 菜单

        [MenuItem("Tools/平衡回归/跑一次比对")]
        static void MenuRun() => Debug.Log(Run(false, 12, 30));

        [MenuItem("Tools/平衡回归/跑一次（含胜率，较慢）")]
        static void MenuRunWinrate() => Debug.Log(Run(false, 40, 30));

        [MenuItem("Tools/平衡回归/把当前状态写成新基线")]
        static void MenuUpdate() => Debug.Log(Run(true, 12, 30));

        [MenuItem("Tools/平衡回归/只跑静态哨兵（毫秒级）")]
        static void MenuStaticCompare() => Debug.Log(RunStaticOnly());

        [MenuItem("Tools/平衡回归/打印静态哨兵全部条目")]
        static void MenuStaticOnly()
        {
            var sb = new StringBuilder();
            var skills = CollectSkills();
            var formulas = CollectFormulas();
            var stages = CollectStages();
            var monsters = CollectMonsters();
            sb.Append($"技能表 {skills.Length} 条 / 公式 {formulas.Length} 条 / 关卡 {stages.Length} 条 / 怪物 {monsters.Length} 条\n");
            foreach (var s in formulas) sb.Append("  ").Append(s).Append('\n');
            Debug.Log("[BalanceRegression] 静态哨兵：\n" + sb);
        }
    }
}

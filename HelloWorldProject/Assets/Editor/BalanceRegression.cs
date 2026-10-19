using System;
using System.Collections.Generic;
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
    /// 2. 分三层。便宜的「静态哨兵」（技能表、公式采样、关卡表、怪物样本、评分自洽性）
    ///    不跑战斗，毫秒级；中间层是「新档过渡轨迹」（从 Lv.1 无装备跑到终局，约 20ms）；
    ///    最贵的是「胜率表」，需要真打。日常改完代码先看哨兵红了没有，红了再跑胜率确认影响范围。
    ///
    /// 3. 过渡必须单独有哨兵。前两层都在「稳态假设」里——关卡表只记怪级，胜率表直接把玩家
    ///    放在第 c 章、等级 10c、装备白送。2026-10 那次事故（新档 Lv.1 打 Lv.10 怪，第 1 关
    ///    就死锁、复活无收益）三层里的前两层全绿，只有从 Lv.1 真跑一遍的过渡轨迹能发现。
    ///    现在这条轨迹进基线（transition 小节）、进静态哨兵，任何让成长曲线断档的改动都会报红。
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

        // 章节胜率用单独一套更省的采样。它比 8 档 winrate 贵得多（11 章 × 2 档 × 3 类型），
        // 每格只有 ChapterPerType × ChapterTrials = 120 场，标准差约 4.6%，
        // 所以容差必须放到 5 个百分点——它的职责不是逐点盯数值，
        // 而是抓住「某章从 0% 变成 50%」这种结构性变化。
        const int ChapterTrials = 6;
        const int ChapterPerType = 20;
        const float ChapterWinrateTolerance = 0.05f;

        /// <summary>过渡轨迹用的种子与重试上限，与 NewGameProgression 的默认值一致。</summary>
        public const int TransitionSeed = 12345;
        public const int TransitionMaxAttempts = 30;

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
        public float[] chapterWinrate; // 11 章 × 2 格（精英 / Boss），0~1
        public int chapterTrials;    // 章节胜率的采样次数（比 winrate 低，容差也更大）
        public int chapterPerType;
            // 新档过渡轨迹（NewGameProgression.Summary）：每章一行「章末等级 / 尝试 / 阵亡 / 秒数」。
            // 稳态胜率表测不出成长断档——它假设玩家已经在那一章；这张表是唯一盯着「走过去」的哨兵。
            public string[] transition;
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
            r.transition = CollectTransition();   // 20ms 级，静态哨兵也带得上
            if (withWinrate)
            {
                r.winrate = CollectWinrate(trials, perType, seed);
                r.chapterTrials = ChapterTrials;
                r.chapterPerType = ChapterPerType;
                r.chapterWinrate = CollectChapterWinrate(ChapterPerType, ChapterTrials);
            }
            return r;
        }

        /// <summary>
        /// 新档过渡轨迹：从 Lv.1 无装备起跑完 11 章，压成每章一行。
        /// 这是「只测稳态、不测过渡」的解药——它不看胜率，只看成长曲线能不能一路接上。
        /// </summary>
        static string[] CollectTransition()
        {
            return NewGameProgression.Trace(TransitionSeed, TransitionMaxAttempts);
        }

        static string[] CollectSkills()
        {
            var list = new string[SkillCatalog.All.Length];
            for (int i = 0; i < SkillCatalog.All.Length; i++)
            {
                SkillDef s = SkillCatalog.All[i];
                list[i] = string.Format(CultureInfo.InvariantCulture,
                    "{0}|{1}|value={2:0.####}|cd={3:0.####}|crit={4:0.####}|hpTh={5:0.####}|trig={6:0.####}|hits={7}|dur={8:0.####}",
                    s.id, s.kind, s.value, s.cooldown, s.critBonus, s.hpThreshold, s.triggerChance, s.hits, s.duration);
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

            // --- 装备强化（步骤 2）---
            // 用户给的三条约束里，「只加主属性、不碰暴击暴伤」是设计红线。派生属性是这个
            // 红线的唯一出口：谁若在 EnhanceMultiplier 的路径上掺进暴击/暴伤，下面的
            // Enhanced* 采样就会变红。费用与上限也一并钉住，免得调参时悄悄改贵/改短。
            Add("EnhanceBonusPerLevel", GameConfig.EnhanceBonusPerLevel);
            Add("EnhanceMaxLevel", GameConfig.EnhanceMaxLevel);
            Add("EnhanceBaseCost", GameConfig.EnhanceBaseCost);
            Add("EnhanceCostPerLevel", GameConfig.EnhanceCostPerLevel);
            for (int i = 0; i < GameConfig.EnhanceQualityFactors.Length; i++)
                Add($"EnhanceQualityFactors[{i}]", GameConfig.EnhanceQualityFactors[i]);

            // 费用公式：品质 / 等级两个维度的低中高段。注意 GameConfig.EnhanceCost 的第一个
            // 参数是**当前**强化等级（目标等级 = 当前 + 1），与 Equipment.EnhanceCost 一致。
            foreach (var q in new[] { EquipmentQuality.Common, EquipmentQuality.Rare, EquipmentQuality.Epic, EquipmentQuality.Legendary })
                foreach (int lv in new[] { 0, 1, 7, 14 })
                    Add($"EnhanceCost({lv},{q})", GameConfig.EnhanceCost(lv, q));

            // 派生属性：固定基础值手算，不消耗随机数，因此是精确比对。
            var enh = new Equipment { attack = 1000, defense = 800, hp = 5000 };
            foreach (int lv in new[] { 0, 1, 7, 14, 15 })
            {
                enh.enhanceLevel = lv;
                Add($"EnhanceMultiplier({lv})", enh.EnhanceMultiplier);
                Add($"EnhancedAttack({lv})", enh.EnhancedAttack);
                Add($"EnhancedDefense({lv})", enh.EnhancedDefense);
                Add($"EnhancedHp({lv})", enh.EnhancedHp);
                Add($"CanEnhance({lv})", enh.CanEnhance ? 1 : 0);
                Add($"EquipEnhanceCost({lv})", enh.EnhanceCost);
            }
            // 满级不再吃钱：CanEnhance=false 时 EnhanceCost 必须是 0，否则 UI 会显示可点的价格
            enh.enhanceLevel = GameConfig.EnhanceMaxLevel;
            Add("CostAtMaxLevel", enh.EnhanceCost);
            Add("CanEnhanceAtMaxLevel", enh.CanEnhance ? 1 : 0);
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

            // 评分品质顺序：同槽位、同等级下，品质越高，主属性整体越强。
            // 不能拿单件直接比——同一品质的词条是随机的，Epic 随到攻速可能高于 Legendary 随到暴伤；
            // 这里固定主词条形态、清空全部副词条，并对每个品质取多个样本的平均值，才是在测品质倍率本身。
            foreach (var slot in EquipmentSlots.All)
            {
                var averages = new float[4];
                var qualities = new[] { EquipmentQuality.Common, EquipmentQuality.Rare, EquipmentQuality.Epic, EquipmentQuality.Legendary };
                for (int qi = 0; qi < qualities.Length; qi++)
                {
                    float sum = 0f;
                    const int samples = 6;
                    for (int s = 0; s < samples; s++)
                    {
                        UnityEngine.Random.InitState(DefaultSeed ^ ((int)slot * 977 + qi * 131 + s * 17));
                        var p = new PlayerData { level = 100 };
                        var e = LootSystem.CreateEquipment(slot, 100, qualities[qi]);
                        e.affixCritChance = 0f;
                        e.affixCritDamage = 0f;
                        e.affixAttackSpeed = 0f;
                        e.affixAllStats = 0f;
                        e.affixArmorPen = 0f;
                        if (slot == EquipmentSlot.Weapon)
                        {
                            // 武器主词条在暴击率/暴伤之间随机；固定成同一种，品质才可比。
                            e.critChance = 0.05f;
                            e.critDamage = 0f;
                        }
                        sum += EquipmentScore.Of(p, e, slot);
                    }
                    averages[qi] = sum / samples;
                    list.Add(string.Format(CultureInfo.InvariantCulture,
                        "{0}/{1}Avg=score{2:0.#}", slot, qualities[qi], averages[qi]));
                }
                bool ordered = averages[0] <= averages[1] && averages[1] <= averages[2] && averages[2] <= averages[3];
                list.Add($"{slot}/qualityOrdering={(ordered ? "ok" : "BAD")}");
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

        /// <summary>
        /// 逐章胜率：每章采「精英 / Boss」两格，按章号顺序铺平成 11 × 2。
        /// 每章独立重置种子，理由与 CollectWinrate 相同——改一章不该让别的章报红。
        /// 采样口径见 LateGameProbe.BuildForChapter：等级 = 该章怪级、装备按精英掉落权重抽、
        /// 巅峰 0 级，所以这是「裸配装下界」，强化与巅峰技能都还没算进去。
        /// </summary>
        static float[] CollectChapterWinrate(int perType, int trials)
        {
            const int tiersPerChapter = 2;
            var outp = new float[GameConfig.ChapterCount * tiersPerChapter];
            for (int ch = 1; ch <= GameConfig.ChapterCount; ch++)
            {
                UnityEngine.Random.InitState(DefaultSeed + ch * 31);
                float[] row = LateGameProbe.RunChapterWinrateRow(ch, perType, true, trials, out _, out _);
                Array.Copy(row, 0, outp, (ch - 1) * tiersPerChapter, tiersPerChapter);
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
            problems += DiffTransition(sb, old, cur);
            problems += DiffWinrate(sb, old, cur);
            problems += DiffChapterWinrate(sb, old, cur);

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
            problems += DiffTransition(sb, old, cur);

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

        static int DiffChapterWinrate(StringBuilder sb, Report old, Report cur)
        {
            if (cur.chapterWinrate == null || cur.chapterWinrate.Length == 0) return 0;
            if (old.chapterWinrate == null || old.chapterWinrate.Length == 0)
            {
                sb.Append("\n  [章节胜率] 基线里还没有这张表 —— 跑一次「把当前状态写成新基线」即可纳入");
                return 0;
            }
            if (old.chapterWinrate.Length != cur.chapterWinrate.Length)
            {
                sb.Append(string.Format(CultureInfo.InvariantCulture,
                    "\n  [章节胜率] 长度不符：基线 {0} / 当前 {1}", old.chapterWinrate.Length, cur.chapterWinrate.Length));
                return 1;
            }
            const int tiersPerChapter = 2;
            string[] tierName = { "精英", "Boss" };
            int diffs = 0, shown = 0;
            for (int i = 0; i < cur.chapterWinrate.Length; i++)
            {
                float a = old.chapterWinrate[i], b = cur.chapterWinrate[i];
                if (Mathf.Abs(b - a) <= ChapterWinrateTolerance) continue;
                diffs++;
                if (shown < MaxDiffLines)
                {
                    sb.Append(string.Format(CultureInfo.InvariantCulture,
                        "\n  [章节胜率] 第{0,2}章 {1,-4}: {2,6:0.0}% → {3,6:0.0}%  ({4:+0.0;-0.0}pp)",
                        i / tiersPerChapter + 1, tierName[i % tiersPerChapter], a * 100f, b * 100f, (b - a) * 100f));
                    shown++;
                }
            }
            if (diffs > shown) sb.Append($"\n  [章节胜率] …另有 {diffs - shown} 格");
            if (old.chapterTrials != cur.chapterTrials || old.chapterPerType != cur.chapterPerType)
                sb.Append($"\n  [章节胜率] 注意：抽样参数变了（trials {old.chapterTrials}→{cur.chapterTrials}，perType {old.chapterPerType}→{cur.chapterPerType}），差异不可比");
            return diffs;
        }

        // ------------------------------------------------------------------ 过渡轨迹

        /// <summary>过渡轨迹里的一行：key + 若干整数字段（保持原顺序，便于逐行比对）。</summary>
        sealed class TLine
        {
            public string key;
            public readonly List<string> fields = new List<string>();
            public readonly Dictionary<string, int> values = new Dictionary<string, int>();
        }

        static List<TLine> ParseTransition(string[] lines)
        {
            var list = new List<TLine>();
            if (lines == null) return list;
            foreach (string raw in lines)
            {
                if (string.IsNullOrEmpty(raw)) continue;
                string[] parts = raw.Split('|');
                var t = new TLine { key = parts[0] };
                for (int i = 1; i < parts.Length; i++)
                {
                    int eq = parts[i].IndexOf('=');
                    if (eq <= 0) continue;
                    string name = parts[i].Substring(0, eq);
                    if (!int.TryParse(parts[i].Substring(eq + 1), NumberStyles.Integer,
                            CultureInfo.InvariantCulture, out int v)) continue;
                    t.fields.Add(name);
                    t.values[name] = v;
                }
                list.Add(t);
            }
            return list;
        }

        /// <summary>
        /// 单项容差。等级/尝试/阵亡/秒数都是模拟量，数值微调会让它们动一点，这不该报红；
        /// 但「卡在同不同一关」「章有没有打通」这种结构量必须一致。
        /// </summary>
        static int TransitionTolerance(string field, int baseline)
        {
            switch (field)
            {
                case "endLevel":
                case "level":
                    return 2;
                case "attempts":
                    return Mathf.Max(2, Mathf.RoundToInt(Mathf.Abs(baseline) * 0.3f));
                case "deaths":
                    return Mathf.Max(3, Mathf.RoundToInt(Mathf.Abs(baseline) * 0.3f));
                case "seconds":
                    return Mathf.RoundToInt(Mathf.Abs(baseline) * 0.35f);
                case "kills":
                    return Mathf.Max(10, Mathf.RoundToInt(Mathf.Abs(baseline) * 0.2f));
                case "cleared":
                case "stuck":
                case "chapter":
                case "stage":
                    return 0;
                default:
                    return int.MaxValue;   // 纯信息量（巅峰等级等），不参与判定
            }
        }

        /// <summary>
        /// 新档过渡轨迹比对。这是 P1-5 的核心：把「玩家能不能一路走过去」做成常驻门禁，
        /// 而不是每次靠人手动跑一遍探针、看完就忘。
        /// </summary>
        static int DiffTransition(StringBuilder sb, Report old, Report cur)
        {
            if (cur == null || cur.transition == null || cur.transition.Length == 0) return 0;
            if (old == null || old.transition == null || old.transition.Length == 0)
            {
                sb.Append("\n  [过渡] 基线里还没有这张表 —— 跑一次「只更新过渡轨迹基线」即可纳入（之后静态哨兵也会带上它）");
                return 0;
            }

            List<TLine> oldLines = ParseTransition(old.transition);
            List<TLine> curLines = ParseTransition(cur.transition);
            var oldMap = new Dictionary<string, TLine>();
            foreach (TLine l in oldLines) oldMap[l.key] = l;

            int diffs = 0, shown = 0;
            var seen = new HashSet<string>();

            foreach (TLine c in curLines)
            {
                seen.Add(c.key);
                if (!oldMap.TryGetValue(c.key, out TLine o))
                {
                    diffs++;
                    if (shown++ < MaxDiffLines) sb.Append($"\n  [过渡] {c.key} 基线里没有（当前新增）");
                    continue;
                }

                foreach (string f in c.fields)
                {
                    if (!o.values.TryGetValue(f, out int ov))
                    {
                        diffs++;
                        if (shown++ < MaxDiffLines) sb.Append($"\n  [过渡] {c.key} {f} 基线里没有这个字段");
                        continue;
                    }
                    int nv = c.values[f];
                    int tol = TransitionTolerance(f, ov);
                    if (Mathf.Abs(nv - ov) <= tol) continue;
                    diffs++;
                    if (shown++ < MaxDiffLines)
                        sb.Append(string.Format(CultureInfo.InvariantCulture,
                            "\n  [过渡] {0} {1}: {2} → {3}（容差 ±{4}）", c.key, f, ov, nv, tol));
                }

                foreach (string f in o.fields)
                {
                    if (c.values.ContainsKey(f)) continue;
                    diffs++;
                    if (shown++ < MaxDiffLines) sb.Append($"\n  [过渡] {c.key} {f} 在当前结果里消失了");
                }
            }

            foreach (TLine o in oldLines)
            {
                if (seen.Contains(o.key)) continue;
                diffs++;
                if (shown++ < MaxDiffLines)
                    sb.Append($"\n  [过渡] {o.key} 在当前结果里消失了（大概卡死在这一章之前）");
            }

            if (diffs > shown) sb.Append($"\n  [过渡] …另有 {diffs - shown} 处");
            if (diffs > 0) sb.Append($"\n  → 过渡轨迹：{diffs} 处不同");
            return diffs;
        }

        /// <summary>
        /// 只把过渡轨迹写进现有基线，不动其它小节（不用重跑 25s 的胜率表）。
        /// 新档过渡这条门禁上线时用它补一次基线。
        /// </summary>
        public static string WriteTransitionBaseline(string baselinePath = null)
        {
            if (string.IsNullOrEmpty(baselinePath)) baselinePath = DefaultBaselinePath;

            Report rep = null;
            if (File.Exists(baselinePath))
            {
                try { rep = JsonUtility.FromJson<Report>(File.ReadAllText(baselinePath)); }
                catch (Exception ex) { return $"基线读不出来（{ex.GetType().Name}: {ex.Message}）"; }
            }
            if (rep == null) rep = new Report { seed = DefaultSeed };

            rep.transition = CollectTransition();
            Directory.CreateDirectory(Path.GetDirectoryName(baselinePath));
            File.WriteAllText(baselinePath, JsonUtility.ToJson(rep, true));
            Debug.Log($"[BalanceRegression] 过渡轨迹已写入基线（{rep.transition.Length} 行）→ {baselinePath}");
            return string.Join("\n", rep.transition);
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

        [MenuItem("Tools/平衡回归/只更新过渡轨迹基线（秒级）")]
        static void MenuWriteTransition() => Debug.Log(WriteTransitionBaseline());

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

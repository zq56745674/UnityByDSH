using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AutoBattle.EditorTools
{
    /// <summary>
    /// 无尽节奏探针：回答两个问题——**满装玩家能爬到第几层才死**、**一局要打多久**。
    ///
    /// 口径（docs/endless-mode.md §12.1）：
    /// - 满装玩家：Lv.100、巅峰 200（15 张技能卡全拿，六个槽按 ProbePolicy 的实打评分装）、
    ///   传说 Lv.110 +15、副词条质量约 80%；
    /// - 自动换装、自动补药、每层关底前把余钱砸进强化；
    /// - 怪物/经验/掉落/金币全部走 EndlessCatalog 与真实战斗代码，不另写简化公式；
    /// - 每局从第 1 层开始，一直打到阵亡或 maxFloor；阵亡即结束（无尽不复活）。
    ///
    /// 输出：首死层数分布（min/P10/中位/P90/max）、单层耗时 P50/P90、每层 HP 损失与药水消耗、
    /// 50 层累计金币/经验。这些数字是基线，不是硬门槛；调参顺序见文档 §12.2。
    /// </summary>
    public static class EndlessPacing
    {
        [MenuItem("Tools/测试/无尽节奏探针（首死层数与耗时）")]
        static void MenuRun() => Debug.Log("[EndlessPacing]\n" + Run(20, 100));


        class RunResult
        {
            public int seed;
            public int firstDeathFloor;       // 没死 = maxFloor + 1
            public int clearedFloors;
            public float seconds;
            public int kills;
            public int potionsUsed;
            public long goldEarned;           // 层奖励 + 首通 + 卖店
            public int reforgeSpent;
            public int goldEnd;
            public long xp;
            public int level;
            public int paragon;
            public int relicCount;
            public int relicMaxTier;
            public string relicIds;
        }

        class FloorSample
        {
            public int floor;
            public float seconds;
            public int startHp;
            public int endHp;
            public int potions;
            public bool cleared;
        }

        public static string Run(int seedCount = 20, int maxFloor = 100, int baseSeed = 20261019,
            int paragonLevel = 200, int gearLevel = 110, float affixQuality = 0.8f, bool maxGear = false,
            int rebirthBattleRank = 0, int rebirthGrowthRank = 0, int rebirthFortuneRank = 0,
            int rebirthLegacyRank = 0, int rebirthAlchemyRank = 0, bool fixedSkills = false,
            int fixedDoctrine = 0, int relicPolicy = 0)
        {
            seedCount = Mathf.Max(1, seedCount);
            maxFloor = Mathf.Clamp(maxFloor, 1, EndlessCatalog.MaxFloor);
            paragonLevel = Mathf.Clamp(paragonLevel, 0, GameConfig.MaxParagonLevel);
            gearLevel = Mathf.Max(1, gearLevel);
            affixQuality = Mathf.Clamp01(affixQuality);

            // 模板必须可复现：装备词条与技能装槽都吃 Random，先用固定种子生成，
            // 否则同一份配置每跑一次探针，玩家面板都会变一个样，没法比较调参结果。
            Random.InitState(baseSeed);
            PlayerData template = BuildFullPlayer(paragonLevel, gearLevel, affixQuality, maxGear,
                rebirthBattleRank, rebirthGrowthRank, rebirthFortuneRank, rebirthLegacyRank, rebirthAlchemyRank,
                fixedSkills, fixedDoctrine);
            var results = new List<RunResult>(seedCount);
            var floorSamples = new Dictionary<int, List<FloorSample>>();

            for (int i = 0; i < seedCount; i++)
            {
                int seed = baseSeed + i * 7919;
                RunResult rr = Simulate(template, seed, maxFloor, floorSamples, affixQuality, relicPolicy);
                results.Add(rr);
            }

            var sb = new StringBuilder();
            sb.AppendLine($"[EndlessPacing] {seedCount} 个种子，最多 {maxFloor} 层；模板 Lv.{template.level} 巅峰 {template.paragonLevel} 技能 {template.OwnedSkillCount}/{SkillCatalog.All.Length}，装备 Lv.{gearLevel} 词条质量 {affixQuality:P0}{(maxGear ? "（满倍率+满主词条）" : "")}");
            sb.AppendLine($"模板：攻 {template.Attack} 防 {template.Defense} 血 {template.MaxHp} 速度 {template.Speed} 暴击 {template.CritChance:P1} 暴伤 {template.CritDamage:P0} 穿甲 {template.ArmorPenetration:P1} 强化 武器+{template.weapon.enhanceLevel}/衣服+{template.armor.enhanceLevel}/头盔+{template.helmet.enhanceLevel}/鞋+{template.boots.enhanceLevel}/饰品+{template.accessory.enhanceLevel} 词条质量 {template.weapon.AffixQuality:P0}/{template.armor.AffixQuality:P0}/{template.helmet.AffixQuality:P0}/{template.boots.AffixQuality:P0}/{template.accessory.AffixQuality:P0}");
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "技能槽：{0}/{1}/{2}/{3}/{4}/{5}  已拥有 {6}",
                template.GetSkillSlot(0), template.GetSkillSlot(1), template.GetSkillSlot(2),
                template.GetSkillSlot(3), template.GetSkillSlot(4), template.GetSkillSlot(5),
                template.OwnedSkillCount));
            if (rebirthBattleRank + rebirthGrowthRank + rebirthFortuneRank + rebirthLegacyRank + rebirthAlchemyRank > 0)
                sb.AppendLine($"转生加成：战斗 {rebirthBattleRank} / 历练 {rebirthGrowthRank} / 财运 {rebirthFortuneRank} / 传承 {rebirthLegacyRank} / 行囊 {rebirthAlchemyRank}（转生 {template.rebirthCount} 世）");
            sb.AppendLine();

            // ---- 首死分布 ----
            var deaths = new List<int>();
            int censored = 0;
            foreach (RunResult r in results)
            {
                if (r.firstDeathFloor > maxFloor) censored++;
                else deaths.Add(r.firstDeathFloor);

                sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                    "seed {0,-10}  首死 {1,-4}  清层 {2,3}  {3,7:0.0}min  击杀 {4,4}  药 {5,3}  金币 +{6,8}{7}",
                    r.seed, r.firstDeathFloor > maxFloor ? "无" : r.firstDeathFloor.ToString(),
                    r.clearedFloors, r.seconds / 60f, r.kills, r.potionsUsed, r.goldEarned,
                    relicPolicy > 0 ? $"  遗物 {r.relicCount}/{RelicCatalog.SlotsFor(template.contractScore)}" : ""));
            }

            if (relicPolicy > 0)
            {
                int relicSum = 0, relicFull = 0;
                foreach (RunResult r in results)
                {
                    relicSum += r.relicCount;
                    if (r.relicCount >= RelicCatalog.SlotsFor(template.contractScore)) relicFull++;
                }
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                    "遗物策略 {0}：平均 {1:0.00} 件 / {2} 件槽位，满槽 {3}/{4} 局（0 无 / 1 总拿第一张 / 2 按分最优+替换 / 3 按分最优不替换 / 4 开局满槽（理论上限：全池最高分、无抽签成本））",
                    relicPolicy, (float)relicSum / Mathf.Max(1, results.Count), RelicCatalog.SlotsFor(template.contractScore), relicFull, results.Count));
            }

            sb.AppendLine();
            if (deaths.Count > 0)
            {
                deaths.Sort();
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                    "首死层数：min {0}  P10 {1}  中位 {2}  P90 {3}  max {4}   （{5} 个种子阵亡，{6} 个打满）",
                    deaths[0], Percentile(deaths, 0.10f), Percentile(deaths, 0.50f),
                    Percentile(deaths, 0.90f), deaths[deaths.Count - 1], deaths.Count, censored));
            }
            else
            {
                sb.AppendLine($"首死层数：{censored} 个种子全部打满 {maxFloor} 层，没有阵亡——难度曲线偏低。");
            }

            // ---- 单层耗时与消耗 ----
            var p50 = new List<string>();
            var p90 = new List<string>();
            var reach = new List<string>();
            var rates = new List<string>();
            foreach (int f in SortedKeys(floorSamples))
            {
                List<FloorSample> list = floorSamples[f];
                var secs = new List<float>();
                int hpLoss = 0, potions = 0, clears = 0;
                int attempts = list.Count;
                foreach (FloorSample s in list)
                {
                    secs.Add(s.seconds);
                    if (s.cleared) { clears++; hpLoss += Mathf.Max(0, s.startHp - s.endHp); potions += s.potions; }
                }
                if (clears > 0) secs.Sort();
                if (f <= 10 || f % 5 == 0)
                {
                    reach.Add(f + ":" + clears);
                    rates.Add(string.Format(CultureInfo.InvariantCulture, "{0}:{1}/{2}({3:P0})",
                        f, clears, attempts, attempts > 0 ? (float)clears / attempts : 0f));
                    if (clears > 0)
                    {
                        p50.Add(f + ":" + PercentileF(secs, 0.50f).ToString("0.0", CultureInfo.InvariantCulture));
                        p90.Add(f + ":" + PercentileF(secs, 0.90f).ToString("0.0", CultureInfo.InvariantCulture));
                    }
                }
            }

            sb.AppendLine();
            sb.AppendLine("层清层数（清层局数；尝试数见下行）：" + string.Join("  ", reach));
            sb.AppendLine("层通关率（通关/尝试）：" + string.Join("  ", rates));
            sb.AppendLine("单层秒数 P50：" + string.Join("  ", p50));
            sb.AppendLine("单层秒数 P90：" + string.Join("  ", p90));

            long reforgeAll = 0;
            foreach (RunResult r in results) reforgeAll += r.reforgeSpent;
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "洗练：累计花费 {0:N0} 金币（目标质量 {1:P0}）", reforgeAll, affixQuality));

            // ---- 50 层经济 ----
            long gold50 = 0, xp50 = 0;
            int reached50 = 0;
            foreach (RunResult r in results)
            {
                // 这里统计的是「清完 50 层时」的累计值；没到 50 层的局不计入。
                if (r.clearedFloors >= 50) reached50++;
            }
            foreach (RunResult r in results)
            {
                if (r.clearedFloors >= 50) { gold50 += r.goldEarned; xp50 += r.xp; }
            }
            if (reached50 > 0)
            {
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                    "50 层经济：{0} 个局到达，平均累计金币 {1:N0}、经验 {2:N0}（金币上限参考：终局约 600 万）",
                    reached50, gold50 / reached50, xp50 / reached50));
            }
            else
            {
                sb.AppendLine("50 层经济：没有一局到达 50 层。");
            }

            // ---- 初始目标提示（非硬门槛） ----
            sb.AppendLine();
            if (deaths.Count > 0)
            {
                int median = Percentile(deaths, 0.50f);
                sb.AppendLine(median >= 35 && median <= 50
                    ? $"✔ 首死中位 {median} 层，落在文档 §12.1 的 35~50 目标区间。"
                    : $"△ 首死中位 {median} 层，不在 35~50 目标区间（调参顺序见文档 §12.2）。");
            }

            return sb.ToString();
        }

        /// <summary>
        /// 遗物验收对照（docs/gameplay-expansion.md §4.5）：同一批种子跑五种策略，输出首死层数分布与层数差。
        /// 0 无遗物 / 1 总拿第一张 / 2 按分最优 + 满槽替换 / 3 按分最优但永不替换 / 4 开局满 6 件（上限参照）。
        /// 三选一的抽签只依赖（种子, 层号），四组策略共享同一条战斗随机流，层数差只来自遗物。
        /// </summary>
        public static string CompareRelics(int seedCount = 20, int maxFloor = 400, int baseSeed = 20261019,
            int paragonLevel = 1000, int gearLevel = 150, float affixQuality = 0.85f, bool maxGear = true)
        {
            seedCount = Mathf.Max(1, seedCount);
            maxFloor = Mathf.Clamp(maxFloor, 1, EndlessCatalog.MaxFloor);
            affixQuality = Mathf.Clamp01(affixQuality);
            Random.InitState(baseSeed);
            PlayerData template = BuildFullPlayer(paragonLevel, gearLevel, affixQuality, maxGear);

            string[] names = { "无遗物", "总拿第一张", "按分最优+替换", "按分最优不替换", "开局满6件(上限)" };
            int[] policies = { 0, 1, 2, 3, 4 };
            var medians = new int[policies.Length];
            var p10s = new int[policies.Length];
            var p90s = new int[policies.Length];

            var sb = new StringBuilder();
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "[RelicCompare] {0} 个种子，最多 {1} 层；模板 Lv.{2} 巅峰 {3}，攻 {4} 防 {5} 血 {6} 暴击 {7:P1} 暴伤 {8:P0}",
                seedCount, maxFloor, template.level, template.paragonLevel, template.Attack, template.Defense,
                template.MaxHp, template.CritChance, template.CritDamage));

            // 策略 4 是「理论上限」：不抽签、每次挑全池（按本档契约分允许的最高档）评分最高的那件，
            // 用来给 4-0 的收益差做上界；真实局内是每 10 层三选一，收益必然低于这一档。
            sb.AppendLine($"遗物档位口径：模板契约分 {template.contractScore} → 槽位 {RelicCatalog.SlotsFor(template.contractScore)}、三选一最高 {ContractCatalog.RelicMaxTier(template.contractScore)} 档");
            sb.AppendLine();
            var perPolicy = new List<List<int>>(policies.Length);
            for (int k = 0; k < policies.Length; k++)
            {
                var floors = new List<int>(seedCount);
                for (int i = 0; i < seedCount; i++)
                {
                    int seed = baseSeed + i * 7919;
                    var samples = new Dictionary<int, List<FloorSample>>();
                    RunResult rr = Simulate(template, seed, maxFloor, samples, affixQuality, policies[k]);
                    floors.Add(rr.firstDeathFloor > maxFloor ? maxFloor + 1 : rr.firstDeathFloor);
                }
                perPolicy.Add(floors);
                floors.Sort();
                p10s[k] = Percentile(floors, 0.10f);
                medians[k] = Percentile(floors, 0.50f);
                p90s[k] = Percentile(floors, 0.90f);
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                    "策略 {0} {1,-14} 首死 P10 {2,4}  中位 {3,4}  P90 {4,4}",
                    policies[k], names[k], p10s[k], medians[k], p90s[k]));
            }

            float spread0 = p90s[0] - p10s[0];
            float spread4 = p90s[4] - p10s[4];
            sb.AppendLine();
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "遗物总收益（4 - 0）：中位 {0:+#;-#;0} 层，P10 {1:+#;-#;0}，P90 {2:+#;-#;0}   ← §4.5 目标 +30~+40",
                medians[4] - medians[0], p10s[4] - p10s[0], p90s[4] - p90s[0]));
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "自然获取（2 - 0）：中位 {0:+#;-#;0} 层", medians[2] - medians[0]));
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "选择质量（2 - 1）：中位 {0:+#;-#;0} 层   ← §4.5 目标 ≥ +15", medians[2] - medians[1]));
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "替换价值（2 - 3）：中位 {0:+#;-#;0} 层   ← §4.5 目标 ≥ +10", medians[2] - medians[3]));
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "离散度（P90-P10）：无遗物 {0:0}，满遗物 {1:0}，比值 {2:0.00}   ← §4.5 要求 ≤ 2.0",
                spread0, spread4, spread0 > 0f ? spread4 / spread0 : 0f));

            // 第 3 条验收：同种子两次抽签序列必须完全一致（只依赖 种子+层号）。
            int seqOk = 0, seqTotal = 0;
            for (int i = 0; i < 3; i++)
            {
                int seed = baseSeed + i * 7919;
                int relicSeed = unchecked(seed * 7919 + 104729);
                var a = new List<int>();
                var b = new List<int>();
                for (int floor = 10; floor <= 120; floor += 10)
                {
                    List<int> first = RelicCatalog.RollChoices(relicSeed, floor, a);
                    List<int> second = RelicCatalog.RollChoices(relicSeed, floor, b);
                    seqTotal++;
                    if (first.Count == second.Count && first.TrueForAll(x => second.Contains(x))) seqOk++;
                    // 两份列表保持同步，模拟「一路拿第一张」的持有集合。
                    if (first.Count > 0) { a.Add(first[0]); b.Add(second[0]); }
                }
            }
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "序列一致性：{0}/{1} 组抽签完全一致", seqOk, seqTotal));

            // 整数层数的中位很粗（大量并列），再给逐种子配对的均值与胜负局数，才能判断是否真有效果。
            sb.AppendLine();
            sb.AppendLine("逐种子配对（相对策略 0 无遗物；正 = 多活）：");
            for (int k = 1; k < policies.Length; k++)
            {
                int win = 0, lose = 0, tie = 0;
                long sum = 0;
                for (int i = 0; i < seedCount; i++)
                {
                    int d = perPolicy[k][i] - perPolicy[0][i];
                    sum += d;
                    if (d > 0) win++; else if (d < 0) lose++; else tie++;
                }
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                    "  {0,-14} 平均 {1:+0.0;-0.0;0.0} 层   胜 {2}/{3}  负 {4}  平 {5}",
                    names[k], (float)sum / Mathf.Max(1, seedCount), win, seedCount, lose, tie));
            }

            // 选择质量（2-1）与替换价值（2-3）也用逐种子配对，避免中位数并列掩盖差异。
            sb.AppendLine();
            sb.AppendLine("逐种子配对（策略 2 按分最优+替换 vs 其他）：");
            for (int other = 1; other <= 3; other += 2)
            {
                int win = 0, lose = 0, tie = 0;
                long sum = 0;
                for (int i = 0; i < seedCount; i++)
                {
                    int d = perPolicy[2][i] - perPolicy[other][i];
                    sum += d;
                    if (d > 0) win++; else if (d < 0) lose++; else tie++;
                }
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                    "  2 比 {0}（{1}）平均 {2:+0.0;-0.0;0.0} 层   胜 {3}/{4}  负 {5}  平 {6}",
                    other, names[other], (float)sum / Mathf.Max(1, seedCount), win, seedCount, lose, tie));
            }

            // 数值自检：把模板挂满 6 件最优遗物，直接打印面板差，确认乘区真的生效（不是靠战斗随机流碰巧）。
            PlayerData full = JsonUtility.FromJson<PlayerData>(JsonUtility.ToJson(template));
            var fullIds = new List<int>();
            for (int i = 0; i < RelicCatalog.SlotsFor(template.contractScore); i++)
            {
                int id = BestUnheldRelic(full, fullIds);
                if (id < 0) break;
                fullIds.Add(id);
            }
            full.relic = RelicMods.Build(fullIds);
            sb.AppendLine();
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "面板自检（模板 + 6 件最优遗物 {0}）：攻 {1} → {2}，血 {3} → {4}，暴击 {5:P1} → {6:P1}，攻速 {7:P1} → {8:P1}",
                JoinRelics(fullIds), template.Attack, full.Attack, template.MaxHp, full.MaxHp,
                template.CritChance, full.CritChance, template.AttackSpeedBonus, full.AttackSpeedBonus));
            return sb.ToString();
        }

        [MenuItem("Tools/测试/深渊遗物对照（五策略）")]
        static void MenuCompareRelics()
        {
            string text = CompareRelics(50, 900);
            Debug.Log("[RelicCompare]\n" + text);
            // eval/菜单都受主线程时限约束，结果同时落盘，避免日志被截断后拿不到数字。
            System.IO.File.WriteAllText("E:/pytest/unitydsh1/HelloWorldProject/_shots/relic-compare.txt", text);
        }


        const string RelicPickersPath = "E:/pytest/unitydsh1/HelloWorldProject/_shots/relic-pickers.txt";

        [MenuItem("Tools/测试/遗物选择质量（Oracle vs 公式 vs 第一张，50 种子）")]
        static void MenuCompareRelicPickers()
        {
            string text = CompareRelicPickers();
            Debug.Log("[RelicPickers]\n" + text);
            System.IO.File.WriteAllText(RelicPickersPath, text);
        }

        /// <summary>
        /// 「遗物区分度」的正面回答：同一批种子、同一抽签序列，只换挑选规则。
        /// 0 无遗物 / 1 总拿第一张 / 3 按公式评分最优不替换 / 5 按实测边际最优（Oracle，玩家知道真相的上限）。
        /// 1 与 3 的差就是「公式能不能选对」，5 与 1 的差是「选择质量的天花板」。
        /// </summary>
        public static string CompareRelicPickers(int seedCount = 50, int maxFloor = 900, int baseSeed = 20261019,
            int paragonLevel = 1000, int gearLevel = 150, float affixQuality = 0.85f, bool maxGear = true)
        {
            seedCount = Mathf.Max(2, seedCount);
            maxFloor = Mathf.Clamp(maxFloor, 20, EndlessCatalog.MaxFloor);

            Random.InitState(baseSeed);
            PlayerData template = BuildFullPlayer(paragonLevel, gearLevel, affixQuality, maxGear);

            string[] names = { "无遗物", "总拿第一张", "公式评分最优", "实测Oracle" };
            int[] policies = { 0, 1, 3, 5 };
            var per = new List<List<int>>(policies.Length);
            var sb = new StringBuilder();
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "[RelicPickers] {0} 个种子，最多 {1} 层；模板 Lv.{2} 巅峰 {3}；无契约、6 槽、tier1",
                seedCount, maxFloor, template.level, template.paragonLevel));
            sb.AppendLine("只换挑选规则，抽签序列完全相同（同种子同层号 → 同样的三选一）。");
            sb.AppendLine();

            for (int k = 0; k < policies.Length; k++)
            {
                var floors = new List<int>(seedCount);
                for (int i = 0; i < seedCount; i++)
                {
                    int seed = baseSeed + i * 7919;
                    RunResult rr = Simulate(template, seed, maxFloor, new Dictionary<int, List<FloorSample>>(),
                        affixQuality, policies[k]);
                    floors.Add(rr.firstDeathFloor > maxFloor ? maxFloor + 1 : rr.firstDeathFloor);
                }
                per.Add(floors);
                var sorted = new List<int>(floors);
                sorted.Sort();
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                    "策略 {0} {1,-12} P10 {2,4}  中位 {3,4}  P90 {4,4}",
                    policies[k], names[k], Percentile(sorted, 0.10f), Percentile(sorted, 0.50f), Percentile(sorted, 0.90f)));
            }

            sb.AppendLine();
            sb.AppendLine("逐种子配对（正 = 多活）：");
            for (int k = 1; k < policies.Length; k++)
            {
                AppendPaired(sb, per[k], per[0], names[k] + " vs 无遗物");
            }
            AppendPaired(sb, per[3], per[1], "Oracle vs 总拿第一张（选择质量天花板）");
            AppendPaired(sb, per[2], per[1], "公式评分 vs 总拿第一张（公式能选对吗）");
            AppendPaired(sb, per[3], per[2], "Oracle vs 公式评分（评分函数的差距）");

            sb.AppendLine();
            sb.AppendLine("读法：Oracle 与「总拿第一张」的差就是本池的选择深度；公式与 Oracle 的差是评分函数没看出来的部分。");
            return sb.ToString();
        }

        static void AppendPaired(StringBuilder sb, List<int> a, List<int> b, string label)
        {
            int win = 0, lose = 0, tie = 0;
            long sum = 0;
            for (int i = 0; i < a.Count; i++)
            {
                int d = a[i] - b[i];
                sum += d;
                if (d > 0) win++; else if (d < 0) lose++; else tie++;
            }
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "  {0,-34} 平均 {1:+0.0;-0.0;0.0} 层   胜 {2}/{3}  负 {4}  平 {5}",
                label, (float)sum / Mathf.Max(1, a.Count), win, a.Count, lose, tie));
        }

        const string RelicTiersPath = "E:/pytest/unitydsh1/HelloWorldProject/_shots/relic-tiers.txt";

        [MenuItem("Tools/测试/深渊遗物分档矩阵（5 档 × 3 策略，50 种子）")]
        static void MenuCompareRelicTiers()
        {
            string text = CompareRelicTiers(50, 900);
            Debug.Log("[RelicTiers]\n" + text);
            System.IO.File.WriteAllText(RelicTiersPath, text);
        }

        [MenuItem("Tools/测试/深渊遗物分档矩阵（冒烟：2 种子 × 60 层）")]
        static void MenuCompareRelicTiersSmoke()
        {
            const string smokePath = "E:/pytest/unitydsh1/HelloWorldProject/_shots/relic-tiers-smoke.txt";
            string text = CompareRelicTiersCore(smokePath, 2, 60, 20261019, 1000, 150, 0.85f, true);
            Debug.Log("[RelicTiersSmoke]\n" + text);
            System.IO.File.WriteAllText(smokePath, text);
        }

        /// <summary>
        /// 分档预算的层数验收（docs/gameplay-expansion.md §4.5 第 7 条）。
        /// 累计契约分 0 / 300 / 800 / 1500 / 3000 各一组，每组跑三种遗物策略：
        /// 0 无遗物（本档基线）、2 按分最优 + 满槽替换（自然获取）、4 开局满槽（理论上限，不抽签）。
        /// 槽位与三选一档位走正式口径（RelicCatalog.SlotsFor / ContractCatalog.RelicMaxTier），
        /// 抽签走带 maxTier 的 RollChoices，掉落走 RelicDropBonus。契约分本身不带战斗系数
        /// （战斗系数在 ContractMods 里，本方法传 null），所以层数差只归因于遗物。
        /// 每跑完一档就落盘一次：主线程耗时可能让 CLI / 日志截断，但已完成的数字不会丢。
        /// </summary>
        public static string CompareRelicTiers(int seedCount = 50, int maxFloor = 900, int baseSeed = 20261019,
            int paragonLevel = 1000, int gearLevel = 150, float affixQuality = 0.85f, bool maxGear = true)
        {
            return CompareRelicTiersCore(RelicTiersPath, seedCount, maxFloor, baseSeed,
                paragonLevel, gearLevel, affixQuality, maxGear);
        }

        static string CompareRelicTiersCore(string outPath, int seedCount, int maxFloor, int baseSeed,
            int paragonLevel, int gearLevel, float affixQuality, bool maxGear)
        {
            seedCount = Mathf.Max(2, seedCount);
            maxFloor = Mathf.Clamp(maxFloor, 20, EndlessCatalog.MaxFloor);
            affixQuality = Mathf.Clamp01(affixQuality);

            Random.InitState(baseSeed);
            PlayerData root = BuildFullPlayer(paragonLevel, gearLevel, affixQuality, maxGear);

            int[] scores = { 0, 300, 800, 1500, 3000 };
            int[] policies = { 0, 2, 4 };
            string[] policyNames = { "无遗物", "按分最优+替换", "开局满槽(上限)" };

            var sb = new StringBuilder();
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "[RelicTiers] {0} 个种子，最多 {1} 层；模板 Lv.{2} 巅峰 {3}，攻 {4} 防 {5} 血 {6} 暴击 {7:P1} 暴伤 {8:P0} 攻速加成 {9:P1}",
                seedCount, maxFloor, root.level, root.paragonLevel, root.Attack, root.Defense, root.MaxHp,
                root.CritChance, root.CritDamage, root.AttackSpeedBonus));
            sb.AppendLine("口径：契约分只决定「遗物槽位 / 三选一最高档」，不带战斗系数（ContractMods 传 null）；层数差只来自遗物。");
            sb.AppendLine("策略 0 无遗物（本档基线） / 2 按分最优 + 满槽替换（自然获取） / 4 开局满槽（理论上限，不抽签）。");
            sb.AppendLine();

            var med = new int[scores.Length, policies.Length];
            var p10 = new int[scores.Length, policies.Length];
            var p90 = new int[scores.Length, policies.Length];
            var naturalDiff = new float[scores.Length];
            var upperDiff = new float[scores.Length];
            float natSd = 0f, upSd = 0f, natSe = 0f, upSe = 0f, natMedDiff = 0f, upMedDiff = 0f;

            for (int si = 0; si < scores.Length; si++)
            {
                int score = scores[si];
                int slots = RelicCatalog.SlotsFor(score);
                int maxTier = ContractCatalog.RelicMaxTier(score);
                PlayerData t = JsonUtility.FromJson<PlayerData>(JsonUtility.ToJson(root));
                t.contractScore = score;

                var upperIds = new List<int>(slots);
                for (int i = 0; i < slots; i++)
                {
                    int id = BestUnheldRelic(t, upperIds, maxTier);
                    if (id < 0) break;
                    upperIds.Add(id);
                }

                sb.AppendLine($"== 契约分 {score}：{slots} 槽，三选一最高 {maxTier} 档 ==");
                sb.AppendLine($"   上限组合：{JoinRelics(upperIds)}");

                var raw = new List<int>[policies.Length];
                for (int pi = 0; pi < policies.Length; pi++)
                {
                    var seedFloors = new List<int>(seedCount);
                    int relicSum = 0, tierSum = 0, fullRuns = 0;
                    for (int i = 0; i < seedCount; i++)
                    {
                        int seed = baseSeed + i * 7919;
                        var samples = new Dictionary<int, List<FloorSample>>();
                        RunResult rr = Simulate(t, seed, maxFloor, samples, affixQuality, policies[pi]);
                        seedFloors.Add(rr.firstDeathFloor > maxFloor ? maxFloor + 1 : rr.firstDeathFloor);
                        relicSum += rr.relicCount;
                        tierSum += rr.relicMaxTier;
                        if (rr.relicCount >= slots) fullRuns++;
                    }
                    raw[pi] = seedFloors;
                    var sorted = new List<int>(seedFloors);
                    sorted.Sort();
                    p10[si, pi] = Percentile(sorted, 0.10f);
                    med[si, pi] = Percentile(sorted, 0.50f);
                    p90[si, pi] = Percentile(sorted, 0.90f);

                    string extra = policies[pi] == 0 ? "" : string.Format(CultureInfo.InvariantCulture,
                        "   平均 {0:0.00}/{1} 件  最高档均值 {2:0.00}  满槽 {3}/{4}",
                        (float)relicSum / seedCount, slots, (float)tierSum / seedCount, fullRuns, seedCount);
                    sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                        "   策略 {0} {1,-12} P10 {2,4}  中位 {3,4}  P90 {4,4}{5}",
                        policies[pi], policyNames[pi], p10[si, pi], med[si, pi], p90[si, pi], extra));

                    System.IO.File.WriteAllText(outPath, sb.ToString());
                }

                sb.AppendLine("   逐种子配对（相对本档无遗物，正 = 多活）：");
                long natSum = 0, upSum = 0;
                natSd = 0f; upSd = 0f; natMedDiff = 0f; upMedDiff = 0f;
                for (int pi = 1; pi < policies.Length; pi++)
                {
                    int win = 0, lose = 0, tie = 0;
                    long sum = 0;
                    var diffs = new List<int>(seedCount);
                    for (int i = 0; i < seedCount; i++)
                    {
                        int d = raw[pi][i] - raw[0][i];
                        diffs.Add(d);
                        sum += d;
                        if (d > 0) win++; else if (d < 0) lose++; else tie++;
                    }
                    float mean = (float)sum / seedCount;
                    double sq = 0;
                    for (int i = 0; i < diffs.Count; i++) sq += (diffs[i] - mean) * (diffs[i] - mean);
                    float sd = Mathf.Sqrt((float)(sq / Mathf.Max(1, seedCount - 1)));
                    float se = sd / Mathf.Sqrt(seedCount);
                    var dSorted = new List<int>(diffs);
                    dSorted.Sort();
                    int medDiff = Percentile(dSorted, 0.5f);
                    if (pi == 1) { natSum = sum; natSd = sd; natSe = se; natMedDiff = medDiff; }
                    else { upSum = sum; upSd = sd; upSe = se; upMedDiff = medDiff; }
                    sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                        "      {0,-12} 平均 {1:+0.0;-0.0;0.0} 层（中位 {2:+0;-0;0}，SD {3:0.0}，SE {4:0.0}，95% CI ±{5:0.0}）  胜 {6}/{7}  负 {8}  平 {9}",
                        policyNames[pi], mean, medDiff, sd, se, 1.96f * se, win, seedCount, lose, tie));
                }
                naturalDiff[si] = (float)natSum / seedCount;
                upperDiff[si] = (float)upSum / seedCount;
                sb.AppendLine();
                System.IO.File.WriteAllText(outPath, sb.ToString());
            }

            sb.AppendLine("汇总（首死层数中位；Δ 为逐种子配对均值 ± 1.96·SE）：");
            sb.AppendLine("| 契约分 | 槽位 | 最高档 | 无遗物 | 自然获取 | 上限 | Δ自然 | Δ上限 |");
            sb.AppendLine("|---|---|---|---|---|---|---|---|");
            for (int si = 0; si < scores.Length; si++)
            {
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                    "| {0} | {1} | T{2} | {3} | {4} | {5} | {6:+0.0;-0.0;0.0} ± {7:0.0} | {8:+0.0;-0.0;0.0} ± {9:0.0} |",
                    scores[si], RelicCatalog.SlotsFor(scores[si]), ContractCatalog.RelicMaxTier(scores[si]),
                    med[si, 0], med[si, 1], med[si, 2],
                    naturalDiff[si], 1.96f * natSe, upperDiff[si], 1.96f * upSe));
            }

            int idx3 = System.Array.IndexOf(scores, 300);
            int idx7 = System.Array.IndexOf(scores, 800);
            int idxT3 = System.Array.IndexOf(scores, 1500);
            int idx8 = System.Array.IndexOf(scores, 3000);
            sb.AppendLine();
            sb.AppendLine("注：契约分 1500 解锁 T3，但 RelicSlots 在 800 分就已给到 7 槽——「6 槽 T3」这一档在现行里程碑下不可达（§4.3 表格第 3 行应视为假想边界）。");
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "T3 @1500（7 槽）：中位 {0} 层 vs 无遗物 {1} 层；自然 {2:+0.0;-0.0;0.0} ± {3:0.0} 层、上限 {4:+0.0;-0.0;0.0} ± {5:0.0} 层（§4.3 原外推 +32）",
                med[idxT3, 2], med[idxT3, 0], naturalDiff[idxT3], 1.96f * natSe, upperDiff[idxT3], 1.96f * upSe));
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "T3 @3000（8 槽）：中位 {0} 层 vs 无遗物 {1} 层；自然 {2:+0.0;-0.0;0.0} ± {3:0.0} 层、上限 {4:+0.0;-0.0;0.0} ± {5:0.0} 层（§4.3 原外推 +40）",
                med[idx8, 2], med[idx8, 0], naturalDiff[idx8], 1.96f * natSe, upperDiff[idx8], 1.96f * upSe));
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "T2 @300（6 槽）：自然 {0:+0.0;-0.0;0.0} 层；T2 @800（7 槽）：自然 {1:+0.0;-0.0;0.0} 层",
                naturalDiff[idx3], naturalDiff[idx7]));
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "换算率：8 槽 T3 净战力 ×1.658 → {0:0.0} 层 / 1% 净战力（7 槽 T3 无单列净战力，故不给换算率）",
                upperDiff[idx8] / 65.8f));
            System.IO.File.WriteAllText(outPath, sb.ToString());
            return sb.ToString();
        }

        const string RelicMarginalPath = "E:/pytest/unitydsh1/HelloWorldProject/_shots/relic-marginal.txt";

        [MenuItem("Tools/测试/遗物解析净战力（不跑模拟）")]
        static void MenuRelicAnalytic()
        {
            Random.InitState(20261019);
            PlayerData root = BuildFullPlayer(1000, 150, 0.85f, true);
            var sb = new StringBuilder();
            sb.AppendLine("遗物解析净战力（P1000 / Lv150 / 满倍率，不跑模拟；无契约）");
            AppendRelicAnalytic(sb, root);
            string text = sb.ToString();
            Debug.Log("[RelicAnalytic]\n" + text);
            System.IO.File.WriteAllText("E:/pytest/unitydsh1/HelloWorldProject/_shots/relic-analytic.txt", text);
        }

        /// <summary>
        /// 把 12 件 tier1 遗物折成「解析净战力」：ΔDPS × √ΔEHP。
        /// 只覆盖能写进公式的部分——吸血、低血减伤、金币/经验/掉落都进不去，
        /// 这三类要么单独标注，要么看实测层数。
        /// </summary>
        static void AppendRelicAnalytic(StringBuilder sb, PlayerData root)
        {
            sb.AppendLine("### 解析净战力（EquipmentScore 口径：ΔDPS × √ΔEHP，含受伤倍率）");
            sb.AppendLine();
            sb.AppendLine("| id | 名称 | 类 | ΔDPS | ΔEHP | 净战力 × | RelicValue |");
            sb.AppendLine("|---|---|---|---|---|---|---|");
            float baseDps = EquipmentScore.Dps(root, EquipmentScore.Standard);
            float baseEhp = EquipmentScore.EffectiveHp(root, EquipmentScore.Standard);
            for (int id = 0; id < RelicCatalog.All.Length; id++)
            {
                RelicDef d = RelicCatalog.Get(id);
                if (d == null || d.tier != 1) continue;
                root.relic = RelicMods.Build(new List<int> { id });
                // 遗物的伤害是独立乘区，落在 BattleSystem.HitMonster 上，EquipmentScore.Dps 本身不含它，
                // 所以这里要自己乘 RelicDamageMultiplier，否则「造成的伤害 +4%」会被算成 0。
                float dps = EquipmentScore.Dps(root, EquipmentScore.Standard) * root.RelicDamageMultiplier;
                float ehp = EquipmentScore.EffectiveHp(root, EquipmentScore.Standard) / root.RelicDamageTakenMultiplier;
                float score = RelicValue(d, root);
                root.relic = null;
                float net = (dps / baseDps) * Mathf.Sqrt(ehp / baseEhp);
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                    "| {0} | {1} | {2} | {3:+0.0;-0.0;0.0}% | {4:+0.0;-0.0;0.0}% | ×{5:0.0000} | {6:0.000} |",
                    id, d.name, d.ClassName, (dps / baseDps - 1f) * 100f, (ehp / baseEhp - 1f) * 100f, net, score));
            }
        }


        [MenuItem("Tools/测试/遗物单件边际（12 件 × 50 种子）")]
        static void MenuRelicMarginal()
        {
            string text = RelicMarginal();
            Debug.Log("[RelicMarginal]\n" + text);
            System.IO.File.WriteAllText(RelicMarginalPath, text);
        }

        const string RelicEconPath = "E:/pytest/unitydsh1/HelloWorldProject/_shots/relic-marginal-econ.txt";

        /// <summary>
        /// 经济类（金脉 / 远见 / 拾荒者）在「满倍率装备」模板下必然为 0：金币与经验都换不来战力。
        /// 这条换个未满装的模板复跑，避免把「模板假设」写成「遗物很强/很弱」。
        /// 参考件挂铁壁（守），用来确认该模板下遗物整体仍在正常工作。
        /// </summary>
        [MenuItem("Tools/测试/遗物经济类复核（非满装模板：铁壁/金脉/拾荒/远见，50 种子）")]
        static void MenuRelicEconRecheck()
        {
            int[] ids = { 4, 6, 7, 8 };
            string text = RelicMarginal(50, 900, 20261019, 1000, 150, 0.85f, false, RelicEconPath, ids);
            Debug.Log("[RelicEcon]\n" + text);
            System.IO.File.WriteAllText(RelicEconPath, text);
        }

        /// <summary>
        /// 12 件 tier1 遗物的单件边际（「遗物区分度不足」的诊断）。
        /// 每件只在开局挂一次、之后不再三选一；与「完全无遗物」同种子配对，层数差只归因这一件。
        /// 同时打印 <see cref="RelicValue"/> 的评分，用来判断「区分度差」是数值挤在一起，
        /// 还是评分函数本身排不出序。
        /// </summary>
        public static string RelicMarginal(int seedCount = 50, int maxFloor = 900, int baseSeed = 20261019,
            int paragonLevel = 1000, int gearLevel = 150, float affixQuality = 0.85f, bool maxGear = true,
            string outPath = null, int[] onlyIds = null, int runeForm = -1)
        {
            if (string.IsNullOrEmpty(outPath)) outPath = RelicMarginalPath;
            seedCount = Mathf.Max(2, seedCount);
            maxFloor = Mathf.Clamp(maxFloor, 20, EndlessCatalog.MaxFloor);

            Random.InitState(baseSeed);
            PlayerData root = BuildFullPlayer(paragonLevel, gearLevel, affixQuality, maxGear);

            var sb = new StringBuilder();
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "遗物单件边际：{0} 种子 / {1} 层上限 / P{2} / Lv{3} / 满倍率={4} / 无契约",
                seedCount, maxFloor, paragonLevel, gearLevel, maxGear));
            sb.AppendLine("每件只在开局挂一次、之后不再获得遗物；与无遗物同种子配对，Δ 单位为层。");

            var baseFloors = new List<int>(seedCount);
            for (int i = 0; i < seedCount; i++)
            {
                int seed = baseSeed + i * 7919;
                RunResult r = Simulate(root, seed, maxFloor, new Dictionary<int, List<FloorSample>>(), affixQuality, 0,
                    null, -1, runeForm);
                baseFloors.Add(r.firstDeathFloor > maxFloor ? maxFloor + 1 : r.firstDeathFloor);
            }

            sb.AppendLine();
            AppendRelicAnalytic(sb, root);

            sb.AppendLine();
            sb.AppendLine("| id | 名称 | 类 | RelicValue 评分 | Δ中位 | Δ均值 ± 95%CI | 胜/负/平 |");
            sb.AppendLine("|---|---|---|---|---|---|---|");
            for (int id = 0; id < RelicCatalog.All.Length; id++)
            {
                RelicDef d = RelicCatalog.Get(id);
                if (d == null || d.tier != 1) continue;
                if (onlyIds != null && System.Array.IndexOf(onlyIds, id) < 0) continue;

                var diffs = new List<int>(seedCount);
                int win = 0, lose = 0, tie = 0;
                long sum = 0;
                for (int i = 0; i < seedCount; i++)
                {
                    int seed = baseSeed + i * 7919;
                    RunResult r = Simulate(root, seed, maxFloor, new Dictionary<int, List<FloorSample>>(),
                        affixQuality, 0, null, id, runeForm);
                    int f = r.firstDeathFloor > maxFloor ? maxFloor + 1 : r.firstDeathFloor;
                    int dif = f - baseFloors[i];
                    diffs.Add(dif);
                    sum += dif;
                    if (dif > 0) win++; else if (dif < 0) lose++; else tie++;
                }

                diffs.Sort();
                float mean = (float)sum / seedCount;
                double sq = 0;
                for (int i = 0; i < diffs.Count; i++) sq += (diffs[i] - mean) * (diffs[i] - mean);
                float sd = seedCount > 1 ? Mathf.Sqrt((float)(sq / (seedCount - 1))) : 0f;
                float se = sd / Mathf.Sqrt(seedCount);
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                    "| {0} | {1} | {2} | {3:0.000} | {4:+0;-0;0} | {5:+0.0;-0.0;0.0} ± {6:0.0} | {7}/{8}/{9} |",
                    id, d.name, d.ClassName, RelicValue(d, root), diffs[seedCount / 2], mean, 1.96f * se, win, lose, tie));
                System.IO.File.WriteAllText(outPath, sb.ToString());
            }

            sb.AppendLine();
            var baseSorted = new List<int>(baseFloors);
            baseSorted.Sort();
            sb.AppendLine("注：基线中位 " + baseSorted[seedCount / 2] + " 层（无遗物）；Δ 与 6 槽 T1 全满 +18.7 层对照，单件按「套装 / 6」本应在 +2~4 层，但 2026-11-04 实测显示承伤类单件可达 +8~+16（层数按 15 层量化，死亡只落在 BOSS 层），"
                + "所以逐件 Δ 不能按净战力线性外推，见 docs/gameplay-expansion.md §4.5.1。");
            System.IO.File.WriteAllText(outPath, sb.ToString());
            return sb.ToString();
        }

        const string RuneRelicPath = "E:/pytest/unitydsh1/HelloWorldProject/_shots/rune-relic-matrix.txt";

        [MenuItem("Tools/测试/符文形态下的遗物边际（风暴，甲/乙 × 6 件，20 种子）")]
        static void MenuRuneRelicMatrix()
        {
            string text = RuneRelicMatrix();
            Debug.Log("[RuneRelic]\n" + text);
            System.IO.File.WriteAllText(RuneRelicPath, text);
        }

        [MenuItem("Tools/测试/符文形态下的遗物边际（50 种子降噪版）")]
        static void MenuRuneRelicMatrix50()
        {
            const string path = "E:/pytest/unitydsh1/HelloWorldProject/_shots/rune-relic-matrix50.txt";
            string text = RuneRelicMatrix(50, 600, 20261019, 1000, 150, 0.85f, true, new[] { 0, 3, 4, 5, 9 });
            Debug.Log("[RuneRelic50]\n" + text);
            System.IO.File.WriteAllText(path, text);
        }

        [MenuItem("Tools/测试/符文形态下的遗物边际（冒烟：2 种子 × 60 层）")]
        static void MenuRuneRelicMatrixSmoke()
        {
            const string smokePath = "E:/pytest/unitydsh1/HelloWorldProject/_shots/rune-relic-matrix-smoke.txt";
            string text = RuneRelicMatrix(2, 60);
            Debug.Log("[RuneRelicSmoke]\n" + text);
            System.IO.File.WriteAllText(smokePath, text);
        }

        /// <summary>
        /// docs/gameplay-expansion.md §6 验收第 2 条：证明「同一套装备，换形态后最优遗物组合不同」。
        /// 同一模板、同一批种子，只切「全甲 / 全乙」，逐件比较遗物边际 Δ 层数的排序是否变化。
        /// 差异要大于噪声（20 种子时 CI 约 ±8 层）才算「最优组合不同」。
        /// </summary>
        public static string RuneRelicMatrix(int seedCount = 20, int maxFloor = 600, int baseSeed = 20261019,
            int paragonLevel = 1000, int gearLevel = 150, float affixQuality = 0.85f, bool maxGear = true,
            int[] ids = null)
        {
            if (ids == null) ids = new[] { 0, 1, 3, 4, 5, 9 };
            seedCount = Mathf.Max(2, seedCount);
            maxFloor = Mathf.Clamp(maxFloor, 20, EndlessCatalog.MaxFloor);

            Random.InitState(baseSeed);
            PlayerData root = BuildFullPlayer(paragonLevel, gearLevel, affixQuality, maxGear);
            var samples = new Dictionary<int, List<FloorSample>>();

            var sb = new StringBuilder();
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "符文形态 × 遗物边际：{0} 种子 / {1} 层上限 / P{2} / Lv{3} / 满倍率={4} / 无契约 / 流派={5}",
                seedCount, maxFloor, paragonLevel, gearLevel, maxGear, SkillCatalog.SchoolName(root.doctrine)));
            sb.AppendLine("同一模板同一批种子，只切形态（甲/乙）；Δ = 单件遗物相对本形态无遗物的层数差。");

            string[] formNames = { "甲", "乙" };
            var baseFloors = new int[2][];
            var deltas = new float[2][];
            var medians = new int[2][];
            for (int form = 0; form < 2; form++)
            {
                baseFloors[form] = new int[seedCount];
                for (int i = 0; i < seedCount; i++)
                {
                    int seed = baseSeed + i * 7919;
                    RunResult r = Simulate(root, seed, maxFloor, samples, affixQuality, 0, null, -1, form);
                    baseFloors[form][i] = r.firstDeathFloor > maxFloor ? maxFloor + 1 : r.firstDeathFloor;
                }
                deltas[form] = new float[ids.Length];
                medians[form] = new int[ids.Length];
                for (int k = 0; k < ids.Length; k++)
                {
                    var diffs = new List<int>(seedCount);
                    for (int i = 0; i < seedCount; i++)
                    {
                        int seed = baseSeed + i * 7919;
                        RunResult r = Simulate(root, seed, maxFloor, samples, affixQuality, 0, null, ids[k], form);
                        int fl = r.firstDeathFloor > maxFloor ? maxFloor + 1 : r.firstDeathFloor;
                        diffs.Add(fl - baseFloors[form][i]);
                    }
                    diffs.Sort();
                    long sum = 0;
                    foreach (int d in diffs) sum += d;
                    deltas[form][k] = (float)sum / seedCount;
                    medians[form][k] = diffs[seedCount / 2];
                }
            }

            sb.AppendLine();
            for (int form = 0; form < 2; form++)
            {
                var sorted = new List<int>(baseFloors[form]);
                sorted.Sort();
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                    "无遗物基线（全{0}）：中位 {1} 层", formNames[form], sorted[seedCount / 2]));
            }

            sb.AppendLine();
            sb.AppendLine("| id | 名称 | 类 | Δ均值 甲 | Δ均值 乙 | 甲−乙 |");
            sb.AppendLine("|---|---|---|---|---|---|");
            for (int k = 0; k < ids.Length; k++)
            {
                RelicDef d = RelicCatalog.Get(ids[k]);
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                    "| {0} | {1} | {2} | {3:+0.0;-0.0;0.0} | {4:+0.0;-0.0;0.0} | {5:+0.0;-0.0;0.0} |",
                    ids[k], d.name, d.ClassName, deltas[0][k], deltas[1][k], deltas[0][k] - deltas[1][k]));
            }

            // 排序比较：两形态下各自的最优件与末位件是否一致。
            int bestA = 0, bestB = 0;
            for (int k = 1; k < ids.Length; k++)
            {
                if (deltas[0][k] > deltas[0][bestA]) bestA = k;
                if (deltas[1][k] > deltas[1][bestB]) bestB = k;
            }
            sb.AppendLine();
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "最优件：甲 = {0}（{1:+0.0;-0.0;0.0}） / 乙 = {2}（{3:+0.0;-0.0;0.0}） → {4}",
                RelicCatalog.Get(ids[bestA]).name, deltas[0][bestA],
                RelicCatalog.Get(ids[bestB]).name, deltas[1][bestB],
                bestA == bestB ? "一致" : "不同"));
            System.IO.File.WriteAllText(RuneRelicPath, sb.ToString());
            return sb.ToString();
        }

        const string RebirthBattlePath = "E:/pytest/unitydsh1/HelloWorldProject/_shots/rebirth-battle-5.txt";

        [MenuItem("Tools/测试/满轮回战斗加成复测（R0 vs R5，50 种子）")]
        static void MenuRebirthBattleRemeasure()
        {
            string text = RebirthBattleRemeasure();
            Debug.Log("[RebirthBattle]\n" + text);
            System.IO.File.WriteAllText(RebirthBattlePath, text);
        }

        [MenuItem("Tools/测试/满轮回战斗加成复测（冒烟：2 种子 × 60 层）")]
        static void MenuRebirthBattleRemeasureSmoke()
        {
            const string smokePath = "E:/pytest/unitydsh1/HelloWorldProject/_shots/rebirth-battle-5-smoke.txt";
            string text = RebirthBattleRemeasure(2, 60, 20261019, 1000, 150, 0.85f, true, smokePath);
            Debug.Log("[RebirthBattleSmoke]\n" + text);
            System.IO.File.WriteAllText(smokePath, text);
        }

        /// <summary>
        /// 「满轮回 +5% 战斗加成」复测（README 已知问题 / docs/handoff.md §4 第 7 条）。
        /// 现行配置 RebirthBattlePerRank = 1%/级、MaxRank = 5 → 满级 = 攻/防/血各 +5%。
        /// 与遗物分档矩阵同口径（50 种子 / 900 层 / P1000 / Lv150 / 满倍率 / 无契约 / 无遗物），
        /// 只切 rebirthBattleRank 0 → MaxRank，其余完全相同；技能在套加成之前选定，两档是同一套构筑。
        /// </summary>
        public static string RebirthBattleRemeasure(int seedCount = 50, int maxFloor = 900, int baseSeed = 20261019,
            int paragonLevel = 1000, int gearLevel = 150, float affixQuality = 0.85f, bool maxGear = true,
            string outPath = null, float perRankOverride = -1f)
        {
            if (string.IsNullOrEmpty(outPath)) outPath = RebirthBattlePath;
            // 降档预估：临时把每级系数换掉，跑完还原（GameConfig 是静态字段，Editor 内改完要还原）。
            float savedPerRank = GameConfig.RebirthBattlePerRank;
            if (perRankOverride > 0f) GameConfig.RebirthBattlePerRank = perRankOverride;
            seedCount = Mathf.Max(2, seedCount);
            maxFloor = Mathf.Clamp(maxFloor, 20, EndlessCatalog.MaxFloor);
            affixQuality = Mathf.Clamp01(affixQuality);

            // 两次建模板都用同一个种子重置：技能在「套加成之前」选定，两档因此拿到同一套构筑。
            Random.InitState(baseSeed);
            PlayerData root0 = BuildFullPlayer(paragonLevel, gearLevel, affixQuality, maxGear);
            Random.InitState(baseSeed);
            PlayerData rootN = BuildFullPlayer(paragonLevel, gearLevel, affixQuality, maxGear,
                RebirthCatalog.MaxRank, 0, 0, 0, 0);

            var floors0 = new List<int>(seedCount);
            var floorsN = new List<int>(seedCount);
            long sum0 = 0, sumN = 0;
            for (int i = 0; i < seedCount; i++)
            {
                int seed = baseSeed + i * 7919;
                RunResult r0 = Simulate(root0, seed, maxFloor, new Dictionary<int, List<FloorSample>>(), affixQuality, 0);
                RunResult rn = Simulate(rootN, seed, maxFloor, new Dictionary<int, List<FloorSample>>(), affixQuality, 0);
                int f0 = r0.firstDeathFloor > maxFloor ? maxFloor + 1 : r0.firstDeathFloor;
                int fn = rn.firstDeathFloor > maxFloor ? maxFloor + 1 : rn.firstDeathFloor;
                floors0.Add(f0);
                floorsN.Add(fn);
                sum0 += f0;
                sumN += fn;
            }

            var s0 = new List<int>(floors0);
            s0.Sort();
            var sN = new List<int>(floorsN);
            sN.Sort();

            var diffs = new List<int>(seedCount);
            int win = 0, lose = 0, tie = 0;
            long sum = 0;
            for (int i = 0; i < seedCount; i++)
            {
                int d = floorsN[i] - floors0[i];
                diffs.Add(d);
                sum += d;
                if (d > 0) win++; else if (d < 0) lose++; else tie++;
            }

            float mean = (float)sum / seedCount;
            double sq = 0;
            for (int i = 0; i < diffs.Count; i++) sq += (diffs[i] - mean) * (diffs[i] - mean);
            float sd = Mathf.Sqrt((float)(sq / Mathf.Max(1, seedCount - 1)));
            float se = sd / Mathf.Sqrt(seedCount);
            var ds = new List<int>(diffs);
            ds.Sort();

            int censored0 = 0, censoredN = 0;
            for (int i = 0; i < seedCount; i++)
            {
                if (floors0[i] > maxFloor) censored0++;
                if (floorsN[i] > maxFloor) censoredN++;
            }

            float perRank = GameConfig.RebirthBattlePerRank;
            float totalMul = 1f + perRank * RebirthCatalog.MaxRank;
            float netPower = totalMul * Mathf.Sqrt(totalMul);
            float expected = (netPower - 1f) * 100f * 0.6f;
            float ci = 1.96f * se;

            var sb = new StringBuilder();
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "[RebirthBattle] {0} 个种子，最多 {1} 层；模板 Lv.{2} 巅峰 {3}，攻 {4} 防 {5} 血 {6} 暴击 {7:P1} 暴伤 {8:P0}",
                seedCount, maxFloor, root0.level, root0.paragonLevel, root0.Attack, root0.Defense, root0.MaxHp,
                root0.CritChance, root0.CritDamage));
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "口径：RebirthBattlePerRank={0:P2}/级，MaxRank={1} → 满轮回 = 攻/防/血各 +{2:P2}；重生倍率 R0 {3:0.000} → R5 {4:0.000}",
                perRank, RebirthCatalog.MaxRank, perRank * RebirthCatalog.MaxRank,
                root0.RebirthBattleMultiplier, rootN.RebirthBattleMultiplier));
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "模板技能：{0}/{1}/{2}/{3}/{4}/{5}  已拥有 {6}（两档同种子选定，构筑一致）",
                root0.GetSkillSlot(0), root0.GetSkillSlot(1), root0.GetSkillSlot(2),
                root0.GetSkillSlot(3), root0.GetSkillSlot(4), root0.GetSkillSlot(5), root0.OwnedSkillCount));
            sb.AppendLine("同口径：50 种子 / 900 层 / P1000 / Lv150 / 满倍率装备 / 无契约 / 无遗物。");
            sb.AppendLine();
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "R0 无加成：P10 {0}  中位 {1}  P90 {2}  均值 {3:0.0}",
                Percentile(s0, 0.10f), Percentile(s0, 0.50f), Percentile(s0, 0.90f), (float)sum0 / seedCount));
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "R5 满轮回：P10 {0}  中位 {1}  P90 {2}  均值 {3:0.0}",
                Percentile(sN, 0.10f), Percentile(sN, 0.50f), Percentile(sN, 0.90f), (float)sumN / seedCount));
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "逐种子配对：平均 {0:+0.0;-0.0;0.0} 层（中位 {1:+0;-0;0}，SD {2:0.0}，SE {3:0.0}，95% CI ±{4:0.0}）  胜 {5}/{6}  负 {7}  平 {8}",
                mean, Percentile(ds, 0.5f), sd, se, ci, win, seedCount, lose, tie));
            sb.AppendLine();
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "预期：+{0:P0} 攻防血 → 净战力 ×{1:0.000}；按分档矩阵换算率 +0.6 层 / 1% 净战力 ≈ {2:+0.0;-0.0;0.0} 层",
                perRank * RebirthCatalog.MaxRank, netPower, expected));
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "判定：实测 {0:+0.0;-0.0;0.0} ± {1:0.0} 层 —— {2}",
                mean, ci,
                Mathf.Abs(mean - expected) <= ci + 1f
                    ? "与换算率一致，加成没有超出预算"
                    : (mean > expected ? "高于换算率预期（加成偏强），建议按 README 降到 0.005/级或仅战役生效" : "低于换算率预期（加成偏弱）")));
            if (censored0 == seedCount && censoredN == seedCount)
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                    "注意：两档 {0} 个种子全部打满 {1} 层（censored），本档无法区分加成——请提高 maxFloor 后重跑。",
                    seedCount, maxFloor));
            GameConfig.RebirthBattlePerRank = savedPerRank;
            System.IO.File.WriteAllText(outPath, sb.ToString());
            return sb.ToString();
        }

        [MenuItem("Tools/测试/满轮回降档预估（0.005/级，50 种子）")]
        static void MenuRebirthBattleHalfRate()
        {
            const string halfPath = "E:/pytest/unitydsh1/HelloWorldProject/_shots/rebirth-battle-half.txt";
            string text = RebirthBattleRemeasure(50, 900, 20261019, 1000, 150, 0.85f, true, halfPath, 0.005f);
            Debug.Log("[RebirthHalf]\n" + text);
            System.IO.File.WriteAllText(halfPath, text);
        }

        const string RebirthDosePath = "E:/pytest/unitydsh1/HelloWorldProject/_shots/rebirth-battle-dose.txt";

        [MenuItem("Tools/测试/满轮回战斗加成剂量曲线（R0~R5，20 种子）")]
        static void MenuRebirthBattleDoseCurve()
        {
            string text = RebirthBattleDoseCurve();
            Debug.Log("[RebirthDose]\n" + text);
            System.IO.File.WriteAllText(RebirthDosePath, text);
        }

        /// <summary>
        /// 剂量曲线：rank 0 → 5（每级 +1% 攻/防/血）各跑同一批种子，看层数增量是线性还是有阈值。
        /// 用来判断「满轮回 +5% 相当于 +29 层」是加法效应还是踩到了某个断点，
        /// 以及「降到 0.005/级」大概能回收多少层。
        /// </summary>
        public static string RebirthBattleDoseCurve(int seedCount = 20, int maxFloor = 900, int baseSeed = 20261019,
            int paragonLevel = 1000, int gearLevel = 150, float affixQuality = 0.85f, bool maxGear = true)
        {
            seedCount = Mathf.Max(2, seedCount);
            maxFloor = Mathf.Clamp(maxFloor, 20, EndlessCatalog.MaxFloor);
            affixQuality = Mathf.Clamp01(affixQuality);
            int maxRank = RebirthCatalog.MaxRank;

            var roots = new PlayerData[maxRank + 1];
            for (int r = 0; r <= maxRank; r++)
            {
                Random.InitState(baseSeed);
                roots[r] = BuildFullPlayer(paragonLevel, gearLevel, affixQuality, maxGear, r, 0, 0, 0, 0);
            }

            var floors = new List<int>[maxRank + 1];
            for (int r = 0; r <= maxRank; r++) floors[r] = new List<int>(seedCount);
            for (int i = 0; i < seedCount; i++)
            {
                int seed = baseSeed + i * 7919;
                for (int r = 0; r <= maxRank; r++)
                {
                    RunResult rr = Simulate(roots[r], seed, maxFloor, new Dictionary<int, List<FloorSample>>(), affixQuality, 0);
                    floors[r].Add(rr.firstDeathFloor > maxFloor ? maxFloor + 1 : rr.firstDeathFloor);
                }
            }

            var sb = new StringBuilder();
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "[RebirthDose] {0} 个种子，最多 {1} 层；RebirthBattlePerRank={2:P1}/级，MaxRank={3}",
                seedCount, maxFloor, GameConfig.RebirthBattlePerRank, maxRank));
            sb.AppendLine("同口径：P1000 / Lv150 / 满倍率装备 / 无契约 / 无遗物；两档技能同种子选定。");
            sb.AppendLine();
            sb.AppendLine("| rank | 攻防血加成 | P10 | 中位 | P90 | Δ中位 | Δ均值 ± CI | 胜/负/平 |");
            sb.AppendLine("|---|---|---|---|---|---|---|---|");

            var medians = new int[maxRank + 1];
            for (int r = 0; r <= maxRank; r++)
            {
                var sorted = new List<int>(floors[r]);
                sorted.Sort();
                medians[r] = Percentile(sorted, 0.50f);

                if (r == 0)
                {
                    sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                        "| 0 | +0% | {0} | {1} | {2} | — | — | — |",
                        Percentile(sorted, 0.10f), medians[r], Percentile(sorted, 0.90f)));
                    continue;
                }

                long sum = 0;
                int win = 0, lose = 0, tie = 0;
                var diffs = new List<int>(seedCount);
                for (int i = 0; i < seedCount; i++)
                {
                    int d = floors[r][i] - floors[0][i];
                    diffs.Add(d);
                    sum += d;
                    if (d > 0) win++; else if (d < 0) lose++; else tie++;
                }
                float mean = (float)sum / seedCount;
                double sq = 0;
                for (int i = 0; i < diffs.Count; i++) sq += (diffs[i] - mean) * (diffs[i] - mean);
                float sd = Mathf.Sqrt((float)(sq / Mathf.Max(1, seedCount - 1)));
                float se = sd / Mathf.Sqrt(seedCount);

                sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                    "| {0} | +{1:0.#}% | {2} | {3} | {4} | {5:+0;-0;0} | {6:+0.0;-0.0;0.0} ± {7:0.0} | {8}/{9}/{10} |",
                    r, r * GameConfig.RebirthBattlePerRank * 100f,
                    Percentile(sorted, 0.10f), medians[r], Percentile(sorted, 0.90f),
                    medians[r] - medians[0], mean, 1.96f * se, win, lose, tie));
            }

            // 斜率：用「中位增量 / rank」粗估每级层数（线性假设下的最小二乘不好手工做，这里给相邻差）。
            sb.AppendLine();
            sb.Append("相邻 rank 的中位增量：");
            for (int r = 1; r <= maxRank; r++)
                sb.Append(string.Format(CultureInfo.InvariantCulture, "R{0}→R{1} {2:+0;-0;0}{3}",
                    r - 1, r, medians[r] - medians[r - 1], r < maxRank ? "、" : ""));
            sb.AppendLine();
            sb.AppendLine("读法：若增量近似恒定 = 加法效应（降 per-rank 系数即可线性回收）；若集中在某一级 = 踩到断点（先找断点原因再调系数）。");
            System.IO.File.WriteAllText(RebirthDosePath, sb.ToString());
            return sb.ToString();
        }

        [MenuItem("Tools/测试/深层契约对照（八条 × Ⅰ/Ⅲ）")]
        static void MenuCompareContracts()
        {
            string text = CompareContracts(20, 400);
            Debug.Log("[ContractCompare]\n" + text);
            System.IO.File.WriteAllText("E:/pytest/unitydsh1/HelloWorldProject/_shots/contract-compare.txt", text);
        }

        /// <summary>
        /// 深层契约验收（docs/gameplay-expansion.md §5.4）：
        /// 同一批种子跑「无契约」与「每条契约 Ⅰ / Ⅲ 档」，看首死层数降幅（Ⅰ 档 5%~15%、Ⅲ 档 25%~45%）；
        /// 再跑「3 条 Ⅲ 档全开」确认首死层 ≥5；最后统计「2 条 Ⅱ 档」单局契约分中位（目标 80~140）。
        /// 决绝没有任何战斗系数（只锁撤退），不参与降幅判定，单独列出。
        /// </summary>
        public static string CompareContracts(int seedCount = 20, int maxFloor = 400, int baseSeed = 20261019,
            int paragonLevel = 200, int gearLevel = 110, float affixQuality = 0.8f, bool maxGear = false,
            int scoreSeedCount = 50, int scoreMaxFloor = 300)
        {
            seedCount = Mathf.Max(1, seedCount);
            maxFloor = Mathf.Clamp(maxFloor, 1, EndlessCatalog.MaxFloor);
            scoreSeedCount = Mathf.Max(1, scoreSeedCount);
            scoreMaxFloor = Mathf.Clamp(scoreMaxFloor, 1, EndlessCatalog.MaxFloor);

            Random.InitState(baseSeed);
            PlayerData template = BuildFullPlayer(paragonLevel, gearLevel, affixQuality, maxGear);

            var sb = new StringBuilder();
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "[ContractCompare] {0} 个种子，最多 {1} 层；模板 Lv.{2} 巅峰 {3}，攻 {4} 防 {5} 血 {6} 暴击 {7:P1}",
                seedCount, maxFloor, template.level, template.paragonLevel, template.Attack, template.Defense,
                template.MaxHp, template.CritChance));

            List<int> baseline = RunContractFloors(template, new int[0], seedCount, maxFloor, baseSeed, affixQuality);
            int baseP10 = Percentile(baseline, 0.10f);
            int baseMedian = Percentile(baseline, 0.50f);
            int baseP90 = Percentile(baseline, 0.90f);
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "无契约基线：首死 P10 {0}  中位 {1}  P90 {2}", baseP10, baseMedian, baseP90));
            sb.AppendLine();
            sb.AppendLine("契约           Ⅰ档中位   降幅     Ⅲ档中位   降幅   判定（Ⅰ 5%~15% / Ⅲ 25%~45%）");

            int pass1 = 0, pass3 = 0, judged = 0;
            foreach (ContractDef def in ContractCatalog.All)
            {
                if (def.kind == ContractKind.NoRetreat)
                {
                    sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                        "{0,-12}   {1,-8}   —       {2,-8}   —     不参与（只锁撤退，无战斗系数）",
                        def.name, "—", "—"));
                    continue;
                }

                int[] t1 = new int[ContractCatalog.All.Length];
                int[] t3 = new int[ContractCatalog.All.Length];
                t1[(int)def.kind] = 1;
                t3[(int)def.kind] = 3;

                List<int> f1 = RunContractFloors(template, t1, seedCount, maxFloor, baseSeed, affixQuality);
                List<int> f3 = RunContractFloors(template, t3, seedCount, maxFloor, baseSeed, affixQuality);
                int m1 = Percentile(f1, 0.50f);
                int m3 = Percentile(f3, 0.50f);
                float d1 = baseMedian > 0 ? 1f - (float)m1 / baseMedian : 0f;
                float d3 = baseMedian > 0 ? 1f - (float)m3 / baseMedian : 0f;
                bool ok1 = d1 >= 0.05f && d1 <= 0.15f;
                bool ok3 = d3 >= 0.25f && d3 <= 0.45f;
                judged++;
                if (ok1) pass1++;
                if (ok3) pass3++;

                sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                    "{0,-12}  {1,6}   {2,6:P0}   {3,6}   {4,6:P0}   {5} / {6}",
                    def.name, m1, d1, m3, d3, ok1 ? "✔" : "✘", ok3 ? "✔" : "✘"));
            }

            sb.AppendLine();
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "档位验收：Ⅰ 档 {0}/{1} 命中 5%~15%，Ⅲ 档 {2}/{1} 命中 25%~45%", pass1, judged, pass3));

            // ---- 3 条 Ⅲ 档全开：只要能过第 5 层就算「不是首层秒死」----
            int[] trio = new int[ContractCatalog.All.Length];
            trio[(int)ContractKind.Frenzy] = 3;
            trio[(int)ContractKind.Hardened] = 3;
            trio[(int)ContractKind.Swarm] = 3;
            List<int> fTrio = RunContractFloors(template, trio, seedCount, maxFloor, baseSeed, affixQuality);
            int trioMin = fTrio.Count > 0 ? fTrio[0] : 0;
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "3 条 Ⅲ 档（狂躁+硬化+群袭）：首死 P10 {0}  中位 {1}  最差 {2}  {3}",
                Percentile(fTrio, 0.10f), Percentile(fTrio, 0.50f), trioMin,
                trioMin >= 5 ? "✔ 最差也活过 5 层" : "✘ 有局在第 5 层前阵亡"));

            // ---- 2 条 Ⅱ 档：单局契约分中位 ----
            int[] pair = new int[ContractCatalog.All.Length];
            pair[(int)ContractKind.Frenzy] = 2;
            pair[(int)ContractKind.Swarm] = 2;
            var scores = new List<int>(scoreSeedCount);
            int pairMaxFloor = Mathf.Max(1, scoreMaxFloor);
            List<int> fPair = RunContractFloors(template, pair, scoreSeedCount, pairMaxFloor, baseSeed, affixQuality, scores);
            scores.Sort();
            int scoreMedian = Percentile(scores, 0.50f);
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "2 条 Ⅱ 档（狂躁Ⅱ+群袭Ⅱ，{0} 种子，最多 {1} 层）：首死中位 {2}  单局契约分 P10 {3}  中位 {4}  P90 {5}  {6}",
                scoreSeedCount, pairMaxFloor, Percentile(fPair, 0.50f),
                Percentile(scores, 0.10f), scoreMedian, Percentile(scores, 0.90f),
                scoreMedian >= 80 && scoreMedian <= 140 ? "✔ 落在 80~140" : "△ 不在 80~140"));

            // ---- 面板自检：契约系数确实进了玩家面板 ----
            // 生存侧（背水/黑雾/枯竭）与敌人侧（群袭/硬化）分开验，各自挑能观察到的三条。
            var probe = JsonUtility.FromJson<PlayerData>(JsonUtility.ToJson(template));
            var panelTiers = new int[ContractCatalog.All.Length];
            panelTiers[(int)ContractKind.LastStand] = 3;
            panelTiers[(int)ContractKind.BlackFog] = 3;
            panelTiers[(int)ContractKind.Drain] = 3;
            probe.contract = ContractMods.Build(panelTiers);
            sb.AppendLine();
            float templateDr = GameConfig.DamageReduction(template.Defense, template.level);
            int probeDefense = Mathf.RoundToInt(probe.Defense * probe.ContractArmorEffectiveMult);
            float probeDr = GameConfig.DamageReduction(probeDefense, probe.level);
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "面板自检（背水Ⅲ+黑雾Ⅲ+枯竭Ⅲ）：血 {0} → {1}，护甲有效率 {2:P0}（减伤 {3:P1} → {4:P1}），治疗系数 {5:P0}",
                template.MaxHp, probe.MaxHp, probe.ContractArmorEffectiveMult,
                templateDr, probeDr, probe.ContractHealMultiplier));

            var enemyTiers = new int[ContractCatalog.All.Length];
            enemyTiers[(int)ContractKind.Swarm] = 3;
            enemyTiers[(int)ContractKind.Hardened] = 3;
            enemyTiers[(int)ContractKind.Swift] = 3;
            var enemyProbe = JsonUtility.FromJson<PlayerData>(JsonUtility.ToJson(template));
            enemyProbe.contract = ContractMods.Build(enemyTiers);
            var sampleMonster = MonsterData.CreateForEndlessFloor(120, MonsterType.Melee, MonsterTier.Normal, MonsterAffix.None);
            float baseInterval = GameConfig.AttackInterval(sampleMonster.speed);
            sampleMonster.ApplyContract(enemyProbe.contract);
            float contractInterval = GameConfig.AttackInterval(sampleMonster.speed, sampleMonster.attackHaste)
                                   * sampleMonster.attackIntervalMult;
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "面板自检（群袭Ⅲ+硬化Ⅲ+疾袭Ⅲ）：守关怪 +{0} 层，敌人受伤 ×{1:0.00}，120 层怪出手间隔 {2:0.000}s → {3:0.000}s",
                enemyProbe.contract.clearFloorBonus,
                enemyProbe.ContractMonsterDamageTakenMultiplier, baseInterval, contractInterval));
            return sb.ToString();
        }

        /// <summary>跑一批种子并返回首死层数（升序）；scores 非空时同时收集每局的契约分。</summary>
        static List<int> RunContractFloors(PlayerData template, int[] tiers, int seedCount, int maxFloor,
            int baseSeed, float affixQuality, List<int> scores = null)
        {
            ContractMods mods = ContractMods.Build(tiers);
            var floors = new List<int>(seedCount);
            for (int i = 0; i < seedCount; i++)
            {
                int seed = baseSeed + i * 7919;
                var samples = new Dictionary<int, List<FloorSample>>();
                RunResult rr = Simulate(template, seed, maxFloor, samples, affixQuality, 0, mods);
                floors.Add(rr.firstDeathFloor > maxFloor ? maxFloor + 1 : rr.firstDeathFloor);
                if (scores != null)
                    scores.Add(ContractCatalog.ScoreOf(rr.clearedFloors, tiers));
            }
            floors.Sort();
            return floors;
        }

        const string RelicStrengthPath = "E:/pytest/unitydsh1/HelloWorldProject/_shots/relic-strength.txt";
        const string RelicStrengthSmokePath = "E:/pytest/unitydsh1/HelloWorldProject/_shots/relic-strength-smoke.txt";

        [MenuItem("Tools/测试/遗物实测强度（24 件 × 40 种子 × 600 层）")]
        static void MenuRelicStrength()
        {
            string text = RelicStrengthScan(40, 600);
            Debug.Log("[RelicStrength]\n" + text);
            System.IO.File.WriteAllText(RelicStrengthPath, text);
        }

        [MenuItem("Tools/测试/遗物实测强度（冒烟：3 种子 × 200 层）")]
        static void MenuRelicStrengthSmoke()
        {
            string text = RelicStrengthScan(3, 200);
            Debug.Log("[RelicStrengthSmoke]\n" + text);
            System.IO.File.WriteAllText(RelicStrengthSmokePath, text);
        }

        /// <summary>
        /// 给「遗物卡片显示实测强度」（docs/gameplay-expansion.md §4.5.1 方案①）供数：
        /// 24 件遗物逐件挂开局、与无遗物同种子配对，输出 Δ 层与胜负平。
        /// 结果会被抄进 Assets/Scripts/RelicStrength.cs（运行时读不到模拟器，只能静态表）。
        /// </summary>
        public static string RelicStrengthScan(int seedCount = 40, int maxFloor = 600, int baseSeed = 20261019,
            int paragonLevel = 1000, int gearLevel = 150, float affixQuality = 0.85f, bool maxGear = true,
            string outPath = null)
        {
            if (string.IsNullOrEmpty(outPath)) outPath = RelicStrengthPath;
            seedCount = Mathf.Max(2, seedCount);
            maxFloor = Mathf.Clamp(maxFloor, 20, EndlessCatalog.MaxFloor);

            Random.InitState(baseSeed);
            PlayerData root = BuildFullPlayer(paragonLevel, gearLevel, affixQuality, maxGear);

            var sb = new StringBuilder();
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "遗物实测强度：{0} 种子 / {1} 层上限 / P{2} / Lv{3} / 满倍率={4} / 无契约 / 无符文",
                seedCount, maxFloor, paragonLevel, gearLevel, maxGear));
            sb.AppendLine("每件只在开局挂一次，与无遗物同种子配对；Δ 单位为层（首死层）。");

            List<int> baseFloors = FloorsOfTemplate(root, seedCount, maxFloor, baseSeed, affixQuality);
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "基线（无遗物）中位 {0} 层", MedianOf(baseFloors)));
            sb.AppendLine();
            sb.AppendLine("| id | 名称 | 档 | 类 | Δ中位 | Δ均值 ± 95%CI | 胜/负/平 |");
            sb.AppendLine("|---|---|---|---|---|---|---|");

            var table = new int[RelicCatalog.All.Length];
            for (int id = 0; id < RelicCatalog.All.Length; id++)
            {
                RelicDef d = RelicCatalog.Get(id);
                if (d == null) continue;

                var diffs = new List<int>(seedCount);
                int win = 0, lose = 0, tie = 0;
                long sum = 0;
                for (int i = 0; i < seedCount; i++)
                {
                    int seed = baseSeed + i * 7919;
                    RunResult r = Simulate(root, seed, maxFloor, new Dictionary<int, List<FloorSample>>(),
                        affixQuality, 0, null, id, -1);
                    int f = r.firstDeathFloor > maxFloor ? maxFloor + 1 : r.firstDeathFloor;
                    int dif = f - baseFloors[i];
                    diffs.Add(dif);
                    sum += dif;
                    if (dif > 0) win++; else if (dif < 0) lose++; else tie++;
                }

                var sorted = new List<int>(diffs);
                sorted.Sort();
                float mean = (float)sum / seedCount;
                double sq = 0;
                for (int i = 0; i < sorted.Count; i++) sq += (sorted[i] - mean) * (sorted[i] - mean);
                float sd = seedCount > 1 ? Mathf.Sqrt((float)(sq / (seedCount - 1))) : 0f;
                float se = sd / Mathf.Sqrt(seedCount);
                table[id] = Mathf.RoundToInt(mean);
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                    "| {0} | {1} | T{2} | {3} | {4:+0;-0;0} | {5:+0.0;-0.0;0.0} ± {6:0.0} | {7}/{8}/{9} |",
                    id, d.name, d.tier, d.ClassName, Percentile(sorted, 0.5f), mean, 1.96f * se, win, lose, tie));
                System.IO.File.WriteAllText(outPath, sb.ToString());
            }

            sb.AppendLine();
            var arr = new StringBuilder();
            arr.Append("Floors = { ");
            for (int i = 0; i < table.Length; i++)
            {
                arr.Append(table[i]);
                if (i < table.Length - 1) arr.Append(", ");
            }
            arr.Append(" };");
            sb.AppendLine(arr.ToString());
            System.IO.File.WriteAllText(outPath, sb.ToString());
            return sb.ToString();
        }

        const string RuneFormScanPath = "E:/pytest/unitydsh1/HelloWorldProject/_shots/rune-form-scan.txt";
        const string RuneFormScanSmokePath = "E:/pytest/unitydsh1/HelloWorldProject/_shots/rune-form-scan-smoke.txt";

        [MenuItem("Tools/测试/符文形态逐技能扫描（2 流派 × 全甲/全乙/单技能翻转，60 种子 × 400 层）")]
        static void MenuRuneFormScan()
        {
            string text = RuneFormScan(60, 400);
            Debug.Log("[RuneScan]\n" + text);
            System.IO.File.WriteAllText(RuneFormScanPath, text);
        }

        [MenuItem("Tools/测试/符文形态逐技能扫描（冒烟：4 种子 × 120 层）")]
        static void MenuRuneFormScanSmoke()
        {
            string text = RuneFormScan(4, 120);
            Debug.Log("[RuneScanSmoke]\n" + text);
            System.IO.File.WriteAllText(RuneFormScanSmokePath, text);
        }

        /// <summary>
        /// 「甲强乙弱」定位探针：同一模板同一批种子下比较
        /// ① 无符文 / 全甲 / 全乙；② 全甲里只把某一个技能翻到乙。
        /// 层数按 BOSS（每 5 层）量化，单技能翻转的 Δ 只作方向参考，聚合结论以「全乙 − 全甲」为准。
        /// </summary>
        public static string RuneFormScan(int seedCount = 60, int maxFloor = 400, int baseSeed = 20261019,
            int paragonLevel = 1000, int gearLevel = 150, float affixQuality = 0.85f, bool maxGear = true,
            string outPath = null)
        {
            if (string.IsNullOrEmpty(outPath)) outPath = RuneFormScanPath;
            seedCount = Mathf.Max(2, seedCount);
            maxFloor = Mathf.Clamp(maxFloor, 20, EndlessCatalog.MaxFloor);

            var sb = new StringBuilder();
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "符文形态逐技能扫描：{0} 种子 / {1} 层上限 / P{2} / Lv{3} / 满倍率={4} / 无契约 / 无遗物",
                seedCount, maxFloor, paragonLevel, gearLevel, maxGear));
            sb.AppendLine("层数按 BOSS（每 5 层）量化；所有配置配对同一批种子，Δ 只归因形态。");

            for (int doctrine = 1; doctrine <= SkillCatalog.DoctrineCount; doctrine++)
            {
                Random.InitState(baseSeed);
                PlayerData root = BuildFullPlayer(paragonLevel, gearLevel, affixQuality, maxGear,
                    0, 0, 0, 0, 0, false, doctrine);

                var equipped = new List<int>();
                for (int slot = 0; slot < PlayerData.SkillSlotCount; slot++)
                {
                    int raw = root.GetSkillSlot(slot);
                    if (raw >= 0 && RuneCatalog.Has((SkillId)raw)) equipped.Add(raw);
                }

                List<int> none = FloorsOfTemplate(root, seedCount, maxFloor, baseSeed, affixQuality);
                PlayerData allA = WithRuneForms(root, equipped, 0);
                PlayerData allB = WithRuneForms(root, equipped, 1);
                List<int> fA = FloorsOfTemplate(allA, seedCount, maxFloor, baseSeed, affixQuality);
                List<int> fB = FloorsOfTemplate(allB, seedCount, maxFloor, baseSeed, affixQuality);

                sb.AppendLine();
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                    "## {0}（流派 {1}）· 已装备可解锁技能 {2} 个",
                    SkillCatalog.SchoolName(doctrine), doctrine, equipped.Count));
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                    "- 无符文 中位 {0}｜全甲 中位 {1}（配对 {2:+0.0;-0.0;0.0}）｜全乙 中位 {3}（配对 {4:+0.0;-0.0;0.0}）",
                    MedianOf(none), MedianOf(fA), PairedMeanDiff(fA, none),
                    MedianOf(fB), PairedMeanDiff(fB, none)));
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                    "- 全乙 − 全甲：均值 {0:+0.0;-0.0;0.0} 层（负 = 乙更弱）", PairedMeanDiff(fB, fA)));
                sb.AppendLine();
                sb.AppendLine("| 技能 | 甲 | 乙 | 全甲中位 | 只翻这一个到乙 | Δ(乙−甲) |");
                sb.AppendLine("|---|---|---|---|---|---|");

                for (int i = 0; i < equipped.Count; i++)
                {
                    var forms = new int[equipped.Count];
                    for (int k = 0; k < equipped.Count; k++) forms[k] = k == i ? 1 : 0;
                    List<int> fF = FloorsOfTemplate(WithRuneForms(root, equipped, forms),
                        seedCount, maxFloor, baseSeed, affixQuality);
                    SkillId id = (SkillId)equipped[i];
                    sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                        "| {0} | {1} | {2} | {3} | {4} | {5:+0.0;-0.0;0.0} |",
                        SkillCatalog.Get(id).name, RuneCatalog.FormName(id, RuneForm.A),
                        RuneCatalog.FormName(id, RuneForm.B), MedianOf(fA), MedianOf(fF),
                        PairedMeanDiff(fF, fA)));
                    System.IO.File.WriteAllText(outPath, sb.ToString());
                }

                System.IO.File.WriteAllText(outPath, sb.ToString());
            }

            System.IO.File.WriteAllText(outPath, sb.ToString());
            return sb.ToString();
        }

        static PlayerData WithRuneForms(PlayerData root, List<int> ids, int form)
        {
            var forms = new int[ids.Count];
            for (int i = 0; i < forms.Length; i++) forms[i] = form;
            return WithRuneForms(root, ids, forms);
        }

        /// <summary>克隆模板并给 ids 里每个技能解锁；forms[i] = 0 甲 / 1 乙。</summary>
        static PlayerData WithRuneForms(PlayerData root, List<int> ids, int[] forms)
        {
            PlayerData p = JsonUtility.FromJson<PlayerData>(JsonUtility.ToJson(root));
            p.runeUnlockMask = 0;
            p.runeFormMask = 0;
            for (int i = 0; i < ids.Count; i++)
            {
                int raw = ids[i];
                p.runeUnlockMask |= 1 << raw;
                if (forms[i] != 0) p.runeFormMask |= 1 << raw;
            }
            return p;
        }

        static List<int> FloorsOfTemplate(PlayerData template, int seedCount, int maxFloor, int baseSeed,
            float affixQuality)
        {
            var floors = new List<int>(seedCount);
            for (int i = 0; i < seedCount; i++)
            {
                int seed = baseSeed + i * 7919;
                RunResult r = Simulate(template, seed, maxFloor, new Dictionary<int, List<FloorSample>>(),
                    affixQuality, 0, null, -1, -1);
                floors.Add(r.firstDeathFloor > maxFloor ? maxFloor + 1 : r.firstDeathFloor);
            }
            return floors;
        }

        static int MedianOf(List<int> floors)
        {
            var copy = new List<int>(floors);
            copy.Sort();
            return Percentile(copy, 0.5f);
        }

        static float PairedMeanDiff(List<int> a, List<int> b)
        {
            if (a == null || b == null || a.Count == 0 || a.Count != b.Count) return 0f;
            long sum = 0;
            for (int i = 0; i < a.Count; i++) sum += a[i] - b[i];
            return (float)sum / a.Count;
        }

        /// <summary>
        /// 给模板里已装备的技能统一解锁同一个形态（runeForm 0 = 全甲、1 = 全乙、-1 = 不装符文），
        /// 用来验证「同一套装备换形态后，最优遗物组合不同」（docs/gameplay-expansion.md §6 验收第 2 条）。
        /// </summary>
        static void ApplyRuneForm(PlayerData p, int runeForm)
        {
            if (p == null || runeForm < 0) return;
            p.runeUnlockMask = 0;
            p.runeFormMask = 0;
            for (int slot = 0; slot < PlayerData.SkillSlotCount; slot++)
            {
                int raw = p.GetSkillSlot(slot);
                if (raw < 0) continue;
                SkillId id = (SkillId)raw;
                if (!RuneCatalog.Has(id)) continue;
                if (!SkillCatalog.IsAvailableTo(p.doctrine, id)) continue;
                p.runeUnlockMask |= 1 << raw;
                if (runeForm == 1) p.runeFormMask |= 1 << raw;
            }
        }

        static RunResult Simulate(PlayerData template, int seed, int maxFloor,
            Dictionary<int, List<FloorSample>> samples, float reforgeQuality, int relicPolicy = 0,
            ContractMods contract = null, int forcedRelicId = -1, int runeForm = -1)
        {
            Random.InitState(seed);

            PlayerData p = JsonUtility.FromJson<PlayerData>(JsonUtility.ToJson(template));
            // 深层契约：背水改生命上限、黑雾改暴击率，都必须先挂上再 FillHp。
            p.contract = contract;
            ApplyRuneForm(p, runeForm);
            p.FillHp();
            var battle = new BattleSystem();
            var rr = new RunResult { seed = seed, firstDeathFloor = maxFloor + 1 };

            // ---- 深渊遗物（与局内同一套 RelicCatalog / RelicMods）----
            // relicPolicy：0 无 / 1 总拿第一张 / 2 按分最优 + 满槽替换 / 3 按分最优但不替换 / 4 开局满 6 件（上限参照）。
            // 抽签种子取自局内种子，不消耗 UnityEngine.Random，四组策略因此共享同一条战斗随机流，层数差只来自遗物。
            int relicSeed = unchecked(seed * 7919 + 104729);
            // 槽位与三选一档位都按「本档累计契约分」走正式口径（SlotsFor / RelicMaxTier），
            // 不再固定按契约分 0 给 6 槽，否则高契约分模板会被结构性低估。
            int contractScore = p.contractScore;
            int relicSlots = RelicCatalog.SlotsFor(contractScore);
            int relicMaxTier = ContractCatalog.RelicMaxTier(contractScore);
            var relics = new List<int>(relicSlots);
            // 单件边际诊断：开局只挂这一件，之后不再三选一（relicPolicy 传 0）。
            if (forcedRelicId >= 0)
            {
                relics.Add(forcedRelicId);
                p.relic = RelicMods.Build(relics);
                p.FillHp();
            }

            if (relicPolicy == 4)
            {
                for (int i = 0; i < relicSlots; i++)
                {
                    int best = BestUnheldRelic(p, relics, relicMaxTier);
                    if (best < 0) break;
                    relics.Add(best);
                }
                p.relic = RelicMods.Build(relics);
                p.FillHp();
            }

            int cap = Mathf.Max(1, GameConfig.EndlessDropLevelCap);
            long goldStart = p.gold;

            for (int floor = 1; floor <= maxFloor; floor++)
            {
                EndlessFloorDef def = EndlessCatalog.Get(floor);
                int startHp = p.currentHp;
                int startPotions = p.potionCount;
                float floorSeconds = 0f;
                bool cleared = true;
                int killTarget = def.killTarget;
                // 群袭：整层怪物按更深的层数生成（与 GameController.SpawnEndlessMonsterNow 同口径）。
                int clearFloorBonus = contract != null ? Mathf.Max(0, contract.clearFloorBonus) : 0;

                for (int k = 0; k < killTarget; k++)
                {
                    bool last = k == killTarget - 1;
                    MonsterTier tier = last ? def.clearTier : MonsterTier.Normal;

                    if (last)
                    {
                        // 与游戏内一致：关底怪前开商店（补药 + 砸强化 / 洗练）；BOSS 战前回满并清盾。
                        NewGameProgression.FillPotions(p);
                        ProbePolicy.SpendGoldOnEnhance(p);
                        rr.reforgeSpent += ProbePolicy.SpendGoldOnReforge(p, reforgeQuality);
                        if (tier == MonsterTier.Boss || GameConfig.HealBeforeFight)
                        {
                            p.FillHp();
                            p.shield = 0;
                        }
                    }

                    MonsterType type = tier == MonsterTier.Boss && def.isBossFloor
                        ? def.bossType
                        : MonsterData.RollType(def.monsterLevel);
                    MonsterAffix affix = tier == MonsterTier.Boss ? MonsterAffix.None : MonsterData.RollAffix();
                    int spawnFloor = def.floor + clearFloorBonus;
                    MonsterData m = MonsterData.CreateForEndlessFloor(spawnFloor, type, tier, affix);
                    if (contract != null) m.ApplyContract(contract);

                    bool win = NewGameProgression.Fight(p, m, battle, false, out float sec, out _);
                    floorSeconds += sec;
                    if (!win)
                    {
                        rr.firstDeathFloor = floor;
                        rr.seconds += floorSeconds;
                        rr.goldEnd = p.gold;
                        rr.level = p.level;
                        rr.paragon = p.paragonLevel;
                        rr.relicCount = relics.Count;
                        rr.relicMaxTier = MaxTierOfRelics(relics);
                        rr.relicIds = JoinRelics(relics);
                        AddSample(samples, floor, new FloorSample
                        {
                            floor = floor,
                            seconds = floorSeconds,
                            startHp = startHp,
                            endHp = 0,
                            potions = startPotions - p.potionCount,
                            cleared = false,
                        });
                        return rr;
                    }

                    rr.kills++;
                    GrantKill(p, m, cap, ref rr.xp);
                }

                rr.clearedFloors = floor;
                rr.seconds += floorSeconds;
                rr.potionsUsed += Mathf.Max(0, startPotions - p.potionCount);
                AddSample(samples, floor, new FloorSample
                {
                    floor = floor,
                    seconds = floorSeconds,
                    startHp = startHp,
                    endHp = p.currentHp,
                    potions = Mathf.Max(0, startPotions - p.potionCount),
                    cleared = cleared,
                });

                // 层奖励 + 新纪录首通（这一局每一层都是第一次清到，等价于一路刷新纪录）。
                long reward = (long)EndlessCatalog.FloorGoldOf(floor) + EndlessCatalog.FirstClearGoldOf(floor);
                p.AddGold((int)reward);
                rr.goldEarned += reward;

                // 每清 10 层弹一次三选一（与 GameController.ResolveEndlessFloorClear 同触发点）。
                if (relicPolicy > 0 && relicPolicy != 4 && RelicCatalog.ShouldOffer(floor))
                {
                    List<int> choices = RelicCatalog.RollChoices(relicSeed, floor, relics, relicMaxTier);
                    if (choices.Count > 0)
                    {
                        int pick = relicPolicy == 1 ? choices[0]
                                 : relicPolicy == 5 ? BestMeasuredChoice(choices)
                                 : BestChoiceOf(choices, p);
                        if (relics.Count < relicSlots)
                        {
                            relics.Add(pick);
                        }
                        else if (relicPolicy == 2 || relicPolicy == 5)
                        {
                            // 策略 5 和 2 的替换规则相同，只是「谁更值」的判据换成实测边际（OracleValue）。
                            if (relicPolicy == 5)
                            {
                                int worst5 = 0;
                                for (int i = 1; i < relics.Count; i++)
                                    if (OracleValue(relics[i]) < OracleValue(relics[worst5])) worst5 = i;
                                if (OracleValue(pick) > OracleValue(relics[worst5])) relics[worst5] = pick;
                                p.relic = RelicMods.Build(relics);
                                if (p.currentHp > p.MaxHp) p.currentHp = p.MaxHp;
                                continue;
                            }

                            // 满槽后必须替换：挑当前评分最低的一件，只有新的更高才换。
                            int worst = 0;
                            for (int i = 1; i < relics.Count; i++)
                                if (RelicValue(RelicCatalog.Get(relics[i]), p) < RelicValue(RelicCatalog.Get(relics[worst]), p)) worst = i;
                            if (RelicValue(RelicCatalog.Get(pick), p) > RelicValue(RelicCatalog.Get(relics[worst]), p)) relics[worst] = pick;
                        }
                        p.relic = RelicMods.Build(relics);
                        if (p.currentHp > p.MaxHp) p.currentHp = p.MaxHp;
                    }
                }
            }

            rr.goldEnd = p.gold;
            rr.goldEarned = p.gold - goldStart;
            rr.level = p.level;
            rr.paragon = p.paragonLevel;
            rr.relicCount = relics.Count;
            rr.relicMaxTier = MaxTierOfRelics(relics);
            rr.relicIds = JoinRelics(relics);
            return rr;
        }

        static void AddSample(Dictionary<int, List<FloorSample>> map, int floor, FloorSample sample)
        {
            if (!map.TryGetValue(floor, out List<FloorSample> list))
            {
                list = new List<FloorSample>();
                map[floor] = list;
            }
            list.Add(sample);
        }

        // ---- 深渊遗物：探针侧的策略评分（只用于挑遗物，不进游戏逻辑）----

        /// <summary>把一件遗物折成「等效战力」：进攻按期望 DPS 增量，生存/收益按经验权重打折。</summary>
        static float RelicValue(RelicDef d, PlayerData p)
        {
            if (d == null) return 0f;
            float crit = Mathf.Clamp01(p.CritChance);
            float critAfter = Mathf.Clamp(p.CritChance + d.critChanceAdd, 0f, GameConfig.MaxCritChance);
            float critDmg = Mathf.Max(1f, p.CritDamage);
            // 暴击率有上限：已经 95% 时「+6% 暴击率」等于 0，不能按未封顶的增量算。
            float critGain = critAfter - crit;
            float offense = (d.damageMult - 1f)
                          + critGain * (critDmg - 1f)
                          + d.critDamageAdd * critAfter
                          + d.hasteAdd;
            float survive = (1f - d.damageTakenMult) * 0.6f
                          + d.lowHpReduction * 0.35f
                          + (d.maxHpMult - 1f) * 0.6f
                          + d.lifestealAdd * 0.8f;
            float economy = (d.goldMult - 1f) * 0.25f
                          + (d.expMult - 1f) * 0.25f
                          + d.dropBonus * 0.4f;
            return offense + survive * 0.8f + economy * 0.5f;
        }

        /// <summary>
        /// 2026-11-04 实测的单件「首死层数边际」（50 种子 / 900 层 / P1000 / Lv150 / 满倍率 / 无契约，
        /// 见 _shots/relic-marginal.txt）。只给探针的 Oracle 策略当判据，不进游戏逻辑：
        /// 它代表「玩家完全知道每件真实价值」时的上限，用来看选择质量的天花板在哪。
        /// </summary>
        static readonly float[] MeasuredRelicFloors =
        {
            3.2f, 1.2f, 0.3f,      // 锋：裂石 / 猎手 / 疾风
            11.7f, 15.6f, 9.8f,    // 守：血之器 / 铁壁 / 残喘
            0f, 4.1f, 0f,          // 贪：金脉 / 拾荒者 / 远见
            4.3f, -1.9f, 1.1f,     // 险：血祭 / 孤注一掷 / 死斗
        };

        static float OracleValue(int id) =>
            id >= 0 && id < MeasuredRelicFloors.Length ? MeasuredRelicFloors[id] : 0f;

        static int BestMeasuredChoice(List<int> choices)
        {
            int best = choices[0];
            for (int i = 1; i < choices.Count; i++)
                if (OracleValue(choices[i]) > OracleValue(best)) best = choices[i];
            return best;
        }

        static int BestChoiceOf(List<int> choices, PlayerData p)
        {
            int best = choices[0];
            for (int i = 1; i < choices.Count; i++)
                if (RelicValue(RelicCatalog.Get(choices[i]), p) > RelicValue(RelicCatalog.Get(best), p)) best = choices[i];
            return best;
        }

        /// <summary>挑池里评分最高的一件；maxTier 用来限制「本档契约分实际允许出现的最高档」，默认 3 = 理论全池。</summary>
        static int BestUnheldRelic(PlayerData p, List<int> owned, int maxTier = 3)
        {
            int best = -1;
            float bestValue = float.MinValue;
            RelicDef[] pool = RelicCatalog.All;
            for (int i = 0; i < pool.Length; i++)
            {
                if (pool[i].tier > maxTier) continue;
                if (owned != null && owned.Contains(pool[i].id)) continue;
                float v = RelicValue(pool[i], p);
                if (v > bestValue || (v == bestValue && best < 0)) { bestValue = v; best = pool[i].id; }
            }
            return best;
        }

        static string JoinRelics(List<int> relics)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < relics.Count; i++)
            {
                RelicDef d = RelicCatalog.Get(relics[i]);
                if (i > 0) sb.Append('/');
                sb.Append(d != null ? d.name : relics[i].ToString());
            }
            return sb.ToString();
        }

        /// <summary>本局持有的最高遗物档（0 = 一件都没有），只用于探针统计。</summary>
        static int MaxTierOfRelics(List<int> relics)
        {
            int max = 0;
            if (relics == null) return max;
            for (int i = 0; i < relics.Count; i++)
            {
                RelicDef d = RelicCatalog.Get(relics[i]);
                if (d != null && d.tier > max) max = d.tier;
            }
            return max;
        }

        /// <summary>无尽击杀结算：经验按等级封顶，精英/BOSS 掉落也按封顶等级，普通怪仍用主角等级（与 GameController 一致）。</summary>
        static void GrantKill(PlayerData p, MonsterData m, int cap, ref long xpTotal)
        {
            int rewardLevel = m.tier == MonsterTier.Normal ? p.level : Mathf.Min(m.level, cap);
            int reward = GameConfig.MonsterExpReward(rewardLevel, m.type, m.tier);
            xpTotal += reward;

            int levels = p.AddExp(reward);
            if (levels > 0 && p.level < GameConfig.MaxLevel)
                p.currentHp = Mathf.Min(p.MaxHp, p.currentHp + GameConfig.PlayerMaxHpPerLevel * levels);

            int heal = Mathf.RoundToInt(p.MaxHp * GameConfig.MonsterTierHealOnKillPercent[(int)m.tier] * p.ContractHealMultiplier);
            p.currentHp = Mathf.Min(p.MaxHp, p.currentHp + heal);

            int dropCount = GameConfig.MonsterTierGuaranteedDrops[(int)m.tier];
            if (dropCount == 0 && LootSystem.RollDrop(p.UnlockedEquipmentSlotCount, p.RebirthDropBonus + p.RelicDropBonus)) dropCount = 1;

            float[] weights = GameConfig.MonsterTierDropQualityWeights[(int)m.tier];
            int dropLevel = m.tier == MonsterTier.Normal ? p.level : Mathf.Min(m.level, cap);
            for (int i = 0; i < dropCount; i++)
            {
                Equipment dropped = weights != null
                    ? LootSystem.CreateEquipmentAtLevel(dropLevel, weights, p)
                    : LootSystem.CreateRandomEquipment(dropLevel, 0f, p);

                if (TryAutoEquip(p, dropped)) continue;
                p.AddGold(GameConfig.SellPrice(dropped.level, dropped.quality));
            }
        }

        static bool TryAutoEquip(PlayerData p, Equipment dropped)
        {
            if (dropped == null) return false;

            Equipment current = p.GetEquipment(dropped.slot);
            if (current != null &&
                EquipmentScore.Of(p, dropped, dropped.slot) <= EquipmentScore.Of(p, current, current.slot))
                return false;

            int oldMaxHp = p.MaxHp;
            p.SetEquipment(dropped);

            int delta = p.MaxHp - oldMaxHp;
            if (delta > 0) p.currentHp = Mathf.Min(p.MaxHp, p.currentHp + delta);
            else p.currentHp = Mathf.Min(p.currentHp, p.MaxHp);
            return true;
        }

        /// <summary>模板：Lv.100 / 可配巅峰 / 传说装备 +15 / 可配词条质量。</summary>
        static PlayerData BuildFullPlayer(int paragonLevel, int gearLevel, float affixQuality, bool maxGear = false,
            int rebirthBattleRank = 0, int rebirthGrowthRank = 0, int rebirthFortuneRank = 0,
            int rebirthLegacyRank = 0, int rebirthAlchemyRank = 0, bool fixedSkills = false,
            int fixedDoctrine = 0)
        {
            var p = new PlayerData
            {
                level = 100,
                paragonLevel = paragonLevel,
                paragonExp = 0,
            };
            for (int i = 0; i < EquipmentSlots.Count; i++)
            {
                EquipmentSlot slot = EquipmentSlots.All[i];
                Equipment gear = maxGear
                    ? BuildMaxGear(slot, gearLevel)
                    : BuildGear(slot, gearLevel, affixQuality);
                p.SetEquipment(gear);
            }

            // 先固定本世流派（0 表示默认风暴），再让 ProbePolicy 逐次实打挑选并装槽。
            ProbePolicy.ProbeDoctrine = fixedDoctrine > 0 ? fixedDoctrine : 1;
            ProbePolicy.ResolvePendingSkills(p);
            if (fixedSkills) ApplyFixedSkills(p, fixedDoctrine > 0 ? fixedDoctrine : 1);

            // 转生加成必须在选完技能之后再套：技能推荐分按当前属性算，先套加成会让
            // max 档选出一套不同的技能槽（实测会丢钢铁之躯），和正常玩家的构筑不可比。
            if (rebirthBattleRank + rebirthGrowthRank + rebirthFortuneRank + rebirthLegacyRank + rebirthAlchemyRank > 0)
            {
                p.rebirthCount = 1;
                p.SetRebirthRank(RebirthTrack.Battle, rebirthBattleRank);
                p.SetRebirthRank(RebirthTrack.Growth, rebirthGrowthRank);
                p.SetRebirthRank(RebirthTrack.Fortune, rebirthFortuneRank);
                p.SetRebirthRank(RebirthTrack.Legacy, rebirthLegacyRank);
                p.SetRebirthRank(RebirthTrack.Alchemy, rebirthAlchemyRank);
            }

            p.FillHp();
            return p;
        }

        /// <summary>
        /// 调试用：把技能固定成「满倍率模板」在 2026-10-19 选出的那一套。
        /// 不固定时技能由 ProbePolicy 按当前属性实打挑选，改词条/暴击上限会连技能一起换掉，
        /// 测出来的层数差就不能归因到词条本身。只在 A/B 探针里传 true。
        /// </summary>
        static void ApplyFixedSkills(PlayerData p, int doctrine)
        {
            p.doctrine = Mathf.Clamp(doctrine, 1, SkillCatalog.DoctrineCount);
            p.ownedSkillMask = 0;
            foreach (SkillId id in SkillCatalog.KitOf(p.doctrine))
                p.ownedSkillMask |= 1 << (int)id;
            p.skillEverMask |= p.ownedSkillMask;
            SkillId[] build = SkillCatalog.BuildOf(p.doctrine);
            for (int i = 0; i < build.Length && i < PlayerData.SkillSlotsPerKind * 3; i++)
                p.SetSkillSlot(i, (int)build[i]);
        }

        static Equipment BuildGear(EquipmentSlot slot, int level, float affixQuality)
        {
            Equipment e = LootSystem.CreateEquipment(slot, level, EquipmentQuality.Legendary);
            e.enhanceLevel = Mathf.Max(0, GameConfig.EnhanceMaxLevel);

            // 把每条已有副词条定在区间 affixQuality 的位置，得到确定性的模板。
            float levelScale = 1f + e.level * GameConfig.EquipAffixLevelScale;
            e.affixCritChance = AffixAt(e.affixCritChance, EquipmentAffix.CritChance, levelScale, affixQuality);
            e.affixCritDamage = AffixAt(e.affixCritDamage, EquipmentAffix.CritDamage, levelScale, affixQuality);
            e.affixAttackSpeed = AffixAt(e.affixAttackSpeed, EquipmentAffix.AttackSpeed, levelScale, affixQuality);
            e.affixAllStats = AffixAt(e.affixAllStats, EquipmentAffix.AllStats, levelScale, affixQuality);
            e.affixArmorPen = AffixAt(e.affixArmorPen, EquipmentAffix.ArmorPen, levelScale, affixQuality);
            return e;
        }

        /// <summary>
        /// 真正“最高数值”模板：品质倍率取该品质上限、主属性按上限重算、强化满级，
        /// 副词条按传说 4 条可取的最优组合固定（武器拿暴击率，衣服拿护甲穿透）。
        /// 只给探针用，不对应任何正常掉落（正常掉落不会同时满倍率+满词条）。
        /// </summary>
        static Equipment BuildMaxGear(EquipmentSlot slot, int level)
        {
            level = Mathf.Max(1, level);
            Equipment e = LootSystem.CreateEquipment(slot, level, EquipmentQuality.Legendary);
            int q = (int)EquipmentQuality.Legendary;
            float multiplier = GameConfig.QualityMultiplierMax[q];
            e.level = level;
            e.qualityMultiplier = multiplier;
            e.enhanceLevel = Mathf.Max(0, GameConfig.EnhanceMaxLevel);

            int steps = level - 1;
            switch (slot)
            {
                case EquipmentSlot.Weapon:
                    e.attack = Mathf.Max(1, Mathf.RoundToInt(
                        (GameConfig.WeaponBaseAttack + GameConfig.WeaponAttackPerLevel * steps) * multiplier));
                    // 主词条取暴击率：在满词条模板下暴击率会顶到 95% 上限，收益高于暴伤主词条。
                    e.critChance = (GameConfig.WeaponBaseCritChance + GameConfig.WeaponCritChancePerLevel * steps) * multiplier;
                    e.critDamage = 0f;
                    break;

                case EquipmentSlot.Armor:
                    e.defense = Mathf.Max(1, Mathf.RoundToInt(
                        (GameConfig.ArmorBaseDefense + GameConfig.ArmorDefensePerLevel * steps) * multiplier));
                    e.hp = Mathf.Max(1, Mathf.RoundToInt(
                        (GameConfig.ArmorBaseHp + GameConfig.ArmorHpPerLevel * steps) * multiplier));
                    break;

                case EquipmentSlot.Helmet:
                    e.defense = Mathf.Max(1, Mathf.RoundToInt(
                        (GameConfig.ArmorBaseDefense + GameConfig.ArmorDefensePerLevel * steps)
                        * GameConfig.HelmetDefenseFactor * multiplier));
                    e.hp = Mathf.Max(1, Mathf.RoundToInt(
                        (GameConfig.ArmorBaseHp + GameConfig.ArmorHpPerLevel * steps)
                        * GameConfig.HelmetHpFactor * multiplier));
                    break;

                case EquipmentSlot.Boots:
                    // 速度不乘品质倍率，只按品质档位加固定值（与 LootSystem 同口径）。
                    e.speed = Mathf.Max(0, Mathf.RoundToInt(
                        GameConfig.BootsBaseSpeed + GameConfig.BootsSpeedPerLevel * steps
                        + GameConfig.BootsSpeedPerQuality * q));
                    e.haste = (GameConfig.BootsBaseHaste + GameConfig.BootsHastePerLevel * steps)
                            * (1f + GameConfig.BootsHastePerQuality * q);
                    break;

                case EquipmentSlot.Accessory:
                    e.critDamage = (GameConfig.AccessoryBaseCritDamage
                                  + GameConfig.AccessoryCritDamagePerLevel * steps) * multiplier;
                    e.armorPen = (GameConfig.AccessoryBaseArmorPen
                                + GameConfig.AccessoryArmorPenPerLevel * steps) * multiplier;
                    break;
            }

            // 先清空随机副词条，再按槽位给「满值且合法条数」的组合：
            // 大件 4 条、小件 2 条；小件数值还要再乘 affixScale（0.5）。
            e.affixCritChance = 0f;
            e.affixCritDamage = 0f;
            e.affixAttackSpeed = 0f;
            e.affixAllStats = 0f;
            e.affixArmorPen = 0f;

            float scale = 1f + level * GameConfig.EquipAffixLevelScale;
            float minorScale = Mathf.Max(0.01f, e.affixScale);
            switch (slot)
            {
                case EquipmentSlot.Weapon:
                    e.affixCritChance = GameConfig.EquipAffixMax[(int)EquipmentAffix.CritChance] * scale;
                    e.affixCritDamage = GameConfig.EquipAffixMax[(int)EquipmentAffix.CritDamage] * scale;
                    e.affixAttackSpeed = GameConfig.EquipAffixMax[(int)EquipmentAffix.AttackSpeed] * scale;
                    e.affixAllStats = GameConfig.EquipAffixMax[(int)EquipmentAffix.AllStats] * scale;
                    break;

                case EquipmentSlot.Armor:
                    e.affixCritDamage = GameConfig.EquipAffixMax[(int)EquipmentAffix.CritDamage] * scale;
                    e.affixAttackSpeed = GameConfig.EquipAffixMax[(int)EquipmentAffix.AttackSpeed] * scale;
                    e.affixAllStats = GameConfig.EquipAffixMax[(int)EquipmentAffix.AllStats] * scale;
                    e.affixArmorPen = GameConfig.EquipAffixMax[(int)EquipmentAffix.ArmorPen] * scale;
                    break;

                case EquipmentSlot.Helmet:
                    e.affixCritDamage = GameConfig.EquipAffixMax[(int)EquipmentAffix.CritDamage] * scale * minorScale;
                    e.affixAllStats = GameConfig.EquipAffixMax[(int)EquipmentAffix.AllStats] * scale * minorScale;
                    break;

                case EquipmentSlot.Boots:
                    e.affixAttackSpeed = GameConfig.EquipAffixMax[(int)EquipmentAffix.AttackSpeed] * scale * minorScale;
                    e.affixAllStats = GameConfig.EquipAffixMax[(int)EquipmentAffix.AllStats] * scale * minorScale;
                    break;

                case EquipmentSlot.Accessory:
                    e.affixCritDamage = GameConfig.EquipAffixMax[(int)EquipmentAffix.CritDamage] * scale * minorScale;
                    e.affixArmorPen = GameConfig.EquipAffixMax[(int)EquipmentAffix.ArmorPen] * scale * minorScale;
                    break;
            }
            return e;
        }

        static float AffixAt(float current, EquipmentAffix kind, float levelScale, float affixQuality)
        {
            if (current <= 0f) return 0f;
            int k = (int)kind;
            float min = GameConfig.EquipAffixMin[k];
            float max = GameConfig.EquipAffixMax[k];
            return (min + (max - min) * affixQuality) * levelScale;
        }

        static List<int> SortedKeys(Dictionary<int, List<FloorSample>> map)
        {
            var keys = new List<int>(map.Keys);
            keys.Sort();
            return keys;
        }

        static int Percentile(List<int> sorted, float p)
        {
            if (sorted.Count == 0) return 0;
            int idx = Mathf.Clamp(Mathf.RoundToInt((sorted.Count - 1) * p), 0, sorted.Count - 1);
            return sorted[idx];
        }

        static float PercentileF(List<float> sorted, float p)
        {
            if (sorted.Count == 0) return 0f;
            int idx = Mathf.Clamp(Mathf.RoundToInt((sorted.Count - 1) * p), 0, sorted.Count - 1);
            return sorted[idx];
        }
    }
}

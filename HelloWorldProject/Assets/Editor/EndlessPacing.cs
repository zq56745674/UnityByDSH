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
                    relicPolicy > 0 ? $"  遗物 {r.relicCount}/{RelicCatalog.SlotsFor(0)}" : ""));
            }

            if (relicPolicy > 0)
            {
                int relicSum = 0, relicFull = 0;
                foreach (RunResult r in results)
                {
                    relicSum += r.relicCount;
                    if (r.relicCount >= RelicCatalog.SlotsFor(0)) relicFull++;
                }
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                    "遗物策略 {0}：平均 {1:0.00} 件 / {2} 件槽位，满槽 {3}/{4} 局（0 无 / 1 总拿第一张 / 2 按分最优+替换 / 3 按分最优不替换 / 4 开局满 6 件）",
                    relicPolicy, (float)relicSum / Mathf.Max(1, results.Count), RelicCatalog.SlotsFor(0), relicFull, results.Count));
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
            for (int i = 0; i < RelicCatalog.SlotsFor(0); i++)
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

        static RunResult Simulate(PlayerData template, int seed, int maxFloor,
            Dictionary<int, List<FloorSample>> samples, float reforgeQuality, int relicPolicy = 0)
        {
            Random.InitState(seed);

            PlayerData p = JsonUtility.FromJson<PlayerData>(JsonUtility.ToJson(template));
            p.FillHp();
            var battle = new BattleSystem();
            var rr = new RunResult { seed = seed, firstDeathFloor = maxFloor + 1 };

            // ---- 深渊遗物（与局内同一套 RelicCatalog / RelicMods）----
            // relicPolicy：0 无 / 1 总拿第一张 / 2 按分最优 + 满槽替换 / 3 按分最优但不替换 / 4 开局满 6 件（上限参照）。
            // 抽签种子取自局内种子，不消耗 UnityEngine.Random，四组策略因此共享同一条战斗随机流，层数差只来自遗物。
            int relicSeed = unchecked(seed * 7919 + 104729);
            int relicSlots = RelicCatalog.SlotsFor(0);
            var relics = new List<int>(relicSlots);
            if (relicPolicy == 4)
            {
                for (int i = 0; i < relicSlots; i++)
                {
                    int best = BestUnheldRelic(p, relics);
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

                for (int k = 0; k < def.killTarget; k++)
                {
                    bool last = k == def.killTarget - 1;
                    MonsterTier tier = last ? def.clearTier : MonsterTier.Normal;

                    if (last)
                    {
                        // 与游戏内一致：开商店时补药、把余钱砸进强化 / 洗练；BOSS 战前回满并清盾。
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
                    MonsterData m = MonsterData.CreateForEndlessFloor(def.floor, type, tier, affix);

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
                    List<int> choices = RelicCatalog.RollChoices(relicSeed, floor, relics);
                    if (choices.Count > 0)
                    {
                        int pick = relicPolicy == 1 ? choices[0] : BestChoiceOf(choices, p);
                        if (relics.Count < relicSlots)
                        {
                            relics.Add(pick);
                        }
                        else if (relicPolicy == 2)
                        {
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

        static int BestChoiceOf(List<int> choices, PlayerData p)
        {
            int best = choices[0];
            for (int i = 1; i < choices.Count; i++)
                if (RelicValue(RelicCatalog.Get(choices[i]), p) > RelicValue(RelicCatalog.Get(best), p)) best = choices[i];
            return best;
        }

        static int BestUnheldRelic(PlayerData p, List<int> owned)
        {
            int best = -1;
            float bestValue = float.MinValue;
            RelicDef[] pool = RelicCatalog.All;
            for (int i = 0; i < pool.Length; i++)
            {
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

        /// <summary>无尽击杀结算：经验按等级封顶，精英/BOSS 掉落也按封顶等级，普通怪仍用主角等级（与 GameController 一致）。</summary>
        static void GrantKill(PlayerData p, MonsterData m, int cap, ref long xpTotal)
        {
            int rewardLevel = m.tier == MonsterTier.Normal ? p.level : Mathf.Min(m.level, cap);
            int reward = GameConfig.MonsterExpReward(rewardLevel, m.type, m.tier);
            xpTotal += reward;

            int levels = p.AddExp(reward);
            if (levels > 0 && p.level < GameConfig.MaxLevel)
                p.currentHp = Mathf.Min(p.MaxHp, p.currentHp + GameConfig.PlayerMaxHpPerLevel * levels);

            int heal = Mathf.RoundToInt(p.MaxHp * GameConfig.MonsterTierHealOnKillPercent[(int)m.tier]);
            p.currentHp = Mathf.Min(p.MaxHp, p.currentHp + heal);

            int dropCount = GameConfig.MonsterTierGuaranteedDrops[(int)m.tier];
            if (dropCount == 0 && LootSystem.RollDrop(p.UnlockedEquipmentSlotCount, p.RebirthDropBonus)) dropCount = 1;

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

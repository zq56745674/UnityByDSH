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
            int paragonLevel = 200, int gearLevel = 110, float affixQuality = 0.8f, bool maxGear = false)
        {
            seedCount = Mathf.Max(1, seedCount);
            maxFloor = Mathf.Clamp(maxFloor, 1, EndlessCatalog.MaxFloor);
            paragonLevel = Mathf.Clamp(paragonLevel, 0, GameConfig.MaxParagonLevel);
            gearLevel = Mathf.Max(1, gearLevel);
            affixQuality = Mathf.Clamp01(affixQuality);

            // 模板必须可复现：装备词条与技能装槽都吃 Random，先用固定种子生成，
            // 否则同一份配置每跑一次探针，玩家面板都会变一个样，没法比较调参结果。
            Random.InitState(baseSeed);
            PlayerData template = BuildFullPlayer(paragonLevel, gearLevel, affixQuality, maxGear);
            var results = new List<RunResult>(seedCount);
            var floorSamples = new Dictionary<int, List<FloorSample>>();

            for (int i = 0; i < seedCount; i++)
            {
                int seed = baseSeed + i * 7919;
                RunResult rr = Simulate(template, seed, maxFloor, floorSamples, affixQuality);
                results.Add(rr);
            }

            var sb = new StringBuilder();
            sb.AppendLine($"[EndlessPacing] {seedCount} 个种子，最多 {maxFloor} 层；模板 Lv.{template.level} 巅峰 {template.paragonLevel} 技能 {template.OwnedSkillCount}/{SkillCatalog.All.Length}，装备 Lv.{gearLevel} 词条质量 {affixQuality:P0}{(maxGear ? "（满倍率+满主词条）" : "")}");
            sb.AppendLine($"模板：攻 {template.Attack} 防 {template.Defense} 血 {template.MaxHp} 暴击 {template.CritChance:P1} 暴伤 {template.CritDamage:P0} 武器 +{template.weapon.enhanceLevel} 衣服 +{template.armor.enhanceLevel} 词条质量 {template.weapon.AffixQuality:P0}/{template.armor.AffixQuality:P0}");
            sb.AppendLine();

            // ---- 首死分布 ----
            var deaths = new List<int>();
            int censored = 0;
            foreach (RunResult r in results)
            {
                if (r.firstDeathFloor > maxFloor) censored++;
                else deaths.Add(r.firstDeathFloor);

                sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                    "seed {0,-10}  首死 {1,-4}  清层 {2,3}  {3,7:0.0}min  击杀 {4,4}  药 {5,3}  金币 +{6,8}",
                    r.seed, r.firstDeathFloor > maxFloor ? "无" : r.firstDeathFloor.ToString(),
                    r.clearedFloors, r.seconds / 60f, r.kills, r.potionsUsed, r.goldEarned));
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

        static RunResult Simulate(PlayerData template, int seed, int maxFloor,
            Dictionary<int, List<FloorSample>> samples, float reforgeQuality)
        {
            Random.InitState(seed);

            PlayerData p = JsonUtility.FromJson<PlayerData>(JsonUtility.ToJson(template));
            p.FillHp();
            var battle = new BattleSystem();
            var rr = new RunResult { seed = seed, firstDeathFloor = maxFloor + 1 };

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
                p.gold += (int)reward;
                rr.goldEarned += reward;
            }

            rr.goldEnd = p.gold;
            rr.goldEarned = p.gold - goldStart;
            rr.level = p.level;
            rr.paragon = p.paragonLevel;
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
            if (dropCount == 0 && LootSystem.RollDrop()) dropCount = 1;

            float[] weights = GameConfig.MonsterTierDropQualityWeights[(int)m.tier];
            int dropLevel = m.tier == MonsterTier.Normal ? p.level : Mathf.Min(m.level, cap);
            for (int i = 0; i < dropCount; i++)
            {
                Equipment dropped = weights != null
                    ? LootSystem.CreateEquipmentAtLevel(dropLevel, weights)
                    : LootSystem.CreateRandomEquipment(dropLevel);

                if (TryAutoEquip(p, dropped)) continue;
                p.gold += GameConfig.SellPrice(dropped.level, dropped.quality);
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
        static PlayerData BuildFullPlayer(int paragonLevel, int gearLevel, float affixQuality, bool maxGear = false)
        {
            var p = new PlayerData
            {
                level = 100,
                paragonLevel = paragonLevel,
                paragonExp = 0,
            };
            p.weapon = maxGear ? BuildMaxGear(EquipmentSlot.Weapon, gearLevel) : BuildGear(EquipmentSlot.Weapon, gearLevel, affixQuality);
            p.armor = maxGear ? BuildMaxGear(EquipmentSlot.Armor, gearLevel) : BuildGear(EquipmentSlot.Armor, gearLevel, affixQuality);

            // 巅峰 200 应得 15 张技能卡；让 ProbePolicy 逐次实打挑选并装槽。
            ProbePolicy.ResolvePendingSkills(p);
            p.FillHp();
            return p;
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
            if (slot == EquipmentSlot.Weapon)
            {
                e.attack = Mathf.Max(1, Mathf.RoundToInt(
                    (GameConfig.WeaponBaseAttack + GameConfig.WeaponAttackPerLevel * steps) * multiplier));
                // 主词条取暴击率：在满词条模板下暴击率会顶到 95% 上限，收益高于暴伤主词条。
                e.critChance = (GameConfig.WeaponBaseCritChance + GameConfig.WeaponCritChancePerLevel * steps) * multiplier;
                e.critDamage = 0f;
            }
            else
            {
                e.defense = Mathf.Max(1, Mathf.RoundToInt(
                    (GameConfig.ArmorBaseDefense + GameConfig.ArmorDefensePerLevel * steps) * multiplier));
                e.hp = Mathf.Max(1, Mathf.RoundToInt(
                    (GameConfig.ArmorBaseHp + GameConfig.ArmorHpPerLevel * steps) * multiplier));
            }

            // 先清空随机副词条，再按槽位给满值组合。
            e.affixCritChance = 0f;
            e.affixCritDamage = 0f;
            e.affixAttackSpeed = 0f;
            e.affixAllStats = 0f;
            e.affixArmorPen = 0f;
            float scale = 1f + level * GameConfig.EquipAffixLevelScale;
            e.affixCritDamage = GameConfig.EquipAffixMax[(int)EquipmentAffix.CritDamage] * scale;
            e.affixAttackSpeed = GameConfig.EquipAffixMax[(int)EquipmentAffix.AttackSpeed] * scale;
            e.affixAllStats = GameConfig.EquipAffixMax[(int)EquipmentAffix.AllStats] * scale;
            if (slot == EquipmentSlot.Weapon)
                e.affixCritChance = GameConfig.EquipAffixMax[(int)EquipmentAffix.CritChance] * scale;
            else
                e.affixArmorPen = GameConfig.EquipAffixMax[(int)EquipmentAffix.ArmorPen] * scale;
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

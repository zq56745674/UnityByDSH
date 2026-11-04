using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AutoBattle.EditorTools
{
    /// <summary>
    /// 终局平衡探针（编辑器专用，放在 Editor 目录，不会进包）。
    ///
    /// 为什么不直接用 <c>unity command eval</c> 跑：eval 的脚本编译器在脚本里有
    /// 任何真实编译错误（例如枚举成员名写错）时，只会吐一句毫无关系的
    /// 「Unreachable code detected」，把真正的 CS 错误盖掉。放进正常编译的
    /// 程序集才能看到真实报错。
    /// </summary>
    public static class LateGameProbe
    {
        /// <summary>单场对局的详细数据，用来核对面板与伤害是否正常。</summary>
        public static string Diagnose(int monsterLevel, int boss, int paragon)
        {
            var sb = new StringBuilder();
            var p = Build(paragon);
            sb.Append($"主角 Lv.{p.level} 巅峰{p.paragonLevel} HP={p.MaxHp} 攻={p.Attack} 防={p.Defense} 速={p.Speed:0.0} 暴击={p.CritChance:0.00}%/{p.CritDamage:0.0}% 穿={p.ArmorPenetration:0.00}% 攻速加成={p.AttackSpeedBonus:0.0}%\n");
            for (int i = 0; i < EquipmentSlots.Count; i++)
            {
                Equipment gear = p.GetEquipment(EquipmentSlots.All[i]);
                sb.Append($"{EquipmentSlots.Name(gear.slot)} {gear.StatLine} || {gear.AffixLine}\n");
            }

            MonsterTier tier = boss != 0 ? MonsterTier.Boss : MonsterTier.Elite;
            var m = MonsterData.CreateForLevel(monsterLevel, MonsterType.Ranged, tier, MonsterAffix.None);
            sb.Append($"{m.Title} HP={m.maxHp} 攻={m.attack} 防={m.defense} 速={m.speed:0.0} 间隔={GameConfig.AttackInterval(m.speed):0.00}s\n");

            var battle = new BattleSystem();
            battle.BeginBattle(p);
            var log = new BattleLog();
            string res = "未结束";
            int used = 0;
            for (int step = 0; step < 40000 && res == "未结束"; step++)
            {
                var r = battle.Tick(p, m, 0.05f, log);
                if (step < 30)
                {
                    for (int i = 0; i < log.Count; i++) sb.Append("    " + log[i].message + "\n");
                }
                log.Clear();
                used = step;
                if (r != BattleTickResult.Ongoing) res = r.ToString();
            }
            sb.Append($"结果={res} 步={used} 游戏内={used * 0.05f:0.0}s 主角HP={p.currentHp}/{p.MaxHp} 怪HP={m.currentHp}/{m.maxHp}");
            return sb.ToString();
        }

        /// <summary>
        /// 主角满级（Lv.100，传说全套）在不同巅峰等级下，对 Lv.100 / Lv.110 精英与 Boss 的胜率。
        /// 口径：按固定类型分别统计后加权（战士 45% / 弓箭手 30% / 盗贼 25%），每格每类型
        /// <paramref name="perType"/> 场，技能冷却跨战斗延续。
        ///
        /// 巅峰技能是随机抽取的，「抽到护盾生成」和「抽到巨人杀手」差距很大，所以单个巅峰等级
        /// 至少要跑 <paramref name="trials"/> 次独立随机，输出的是这些次抽样的平均值；
        /// 只跑一次会得到非单调、误导性的曲线。
        /// </summary>
        /// <param name="resetHp">
        /// true = 每场开局把主角生命补满。游戏内精英 / Boss 开战前会「恢复全部生命」，
        /// 所以这是接近实际的口径；false（沿用上一场残血）会放大连锁崩盘效应。
        /// </param>
        /// <param name="detail">为 true 时额外输出 Lv.110 Boss 的逐类型胜率。</param>
        public static string RunWinrate(int perType, bool resetHp, bool detail, int trials, bool smartPick = true)
        {
            var sb = new StringBuilder();
            int[] paras = { 0, 10, 20, 30, 40, 50, 70, 100 };
            sb.Append($"每格 {perType} 场/类型 × {trials} 次独立随机技能组合（取平均）；每场重置生命={resetHp}；加权 战45/弓30/盗25\n");
            sb.Append("巅峰  技能   Lv.100精英  Lv.100Boss  Lv.110精英  Lv.110Boss    (Lv.110Boss 逐类型)\n");
            foreach (int par in paras) sb.Append(RunWinrateCell(par, perType, resetHp, detail, trials, smartPick)).Append("\n");
            return sb.ToString();
        }

        /// <summary>
        /// 单个巅峰等级的胜率行。拆出来是为了能分批调用——trials 上到 30 时，
        /// 一次跑完 8 个等级的场次会让 Unity 主线程阻塞到 pipeline 心跳超时。
        /// </summary>
        public static string RunWinrateCell(int paragon, int perType, bool resetHp, bool detail, int trials, bool smartPick = true,
                                            int rebirthBattleRank = 0, int rebirthGrowthRank = 0, int rebirthFortuneRank = 0,
                                            int rebirthLegacyRank = 0, int rebirthAlchemyRank = 0)
        {
            float[] acc = RunWinrateRow(paragon, perType, resetHp, trials, smartPick,
                rebirthBattleRank, rebirthGrowthRank, rebirthFortuneRank, rebirthLegacyRank, rebirthAlchemyRank,
                out int skillSum, out int[] accWin);
            var sb = new StringBuilder();
            sb.Append($"{paragon,4}  {(float)skillSum / trials,4:0.0}   ");
            for (int c = 0; c < acc.Length; c++) sb.Append($"{acc[c] * 100f,8:0.0}%  ");
            if (detail)
            {
                int n = perType * trials;
                sb.Append($"   战 {(float)accWin[0] / n * 100f:0.0}% / 弓 {(float)accWin[1] / n * 100f:0.0}% / 盗 {(float)accWin[2] / n * 100f:0.0}%");
            }
            return sb.ToString();
        }

        /// <summary>
        /// 单个巅峰等级的四格胜率（0~1），顺序：Lv.100精英 / Lv.100Boss / Lv.110精英 / Lv.110Boss。
        /// 抽出来是因为平衡回归测试要的是数值而不是格式化文本；RunWinrateCell 也走这里，两条路不会算出不同结果。
        /// </summary>
        /// <param name="skillSum">trials 次抽样里主角已装备技能数的总和（除以 trials 得平均）。</param>
        /// <param name="accWin">Lv.110 Boss 的逐类型胜场数，下标 0/1/2 = 近战/远程/暴击。</param>
        public static float[] RunWinrateRow(int paragon, int perType, bool resetHp, int trials, bool smartPick,
                                            out int skillSum, out int[] accWin)
        {
            return RunWinrateRow(paragon, perType, resetHp, trials, smartPick,
                0, 0, 0, 0, 0, out skillSum, out accWin);
        }

        /// <summary>带转生加成的重载；五条 rank 默认由上面的重载传 0，保持旧基线不变。</summary>
        public static float[] RunWinrateRow(int paragon, int perType, bool resetHp, int trials, bool smartPick,
                                            int rebirthBattleRank, int rebirthGrowthRank, int rebirthFortuneRank,
                                            int rebirthLegacyRank, int rebirthAlchemyRank,
                                            out int skillSum, out int[] accWin)
        {
            var battle = new BattleSystem();
            float[] w = { 0.45f, 0.30f, 0.25f };
            int[] lvArr = { 100, 100, 110, 110 };
            MonsterTier[] tierArr = { MonsterTier.Elite, MonsterTier.Boss, MonsterTier.Elite, MonsterTier.Boss };

            var acc = new float[lvArr.Length];
            accWin = new int[3];
            skillSum = 0;
            for (int tr = 0; tr < trials; tr++)
            {
                var p = Build(paragon, smartPick, rebirthBattleRank, rebirthGrowthRank,
                    rebirthFortuneRank, rebirthLegacyRank, rebirthAlchemyRank);
                skillSum += p.OwnedSkillCount;
                for (int c = 0; c < lvArr.Length; c++)
                {
                    for (int t = 0; t < 3; t++)
                    {
                        int wins = 0;
                        var type = (MonsterType)t;
                        for (int i = 0; i < perType; i++)
                        {
                            var m = MonsterData.CreateForLevel(lvArr[c], type, tierArr[c], MonsterAffix.None);
                            if (Fight(p, m, battle, resetHp)) wins++;
                        }
                        acc[c] += w[t] * ((float)wins / perType);
                        if (c == lvArr.Length - 1) accWin[t] += wins;
                    }
                }
            }
            for (int c = 0; c < acc.Length; c++) acc[c] /= trials;
            return acc;
        }

        /// <summary>
        /// 按章节的胜率（功能缺口 2）：玩家等级 = 该章怪级（ChapterMonsterLevelStep × 章节），
        /// 装备按「精英保底掉落」的品质权重采样，巅峰 0 级（满级前没有巅峰）。
        ///
        /// 补这个口径是因为原 RunWinrate 固定 Lv.100 传说套打 Lv.100/110 怪，
        /// 只反映满级天花板，看不出中间章节是卡关还是过易。
        /// 玩家等级取怪级而不是更高，理由是关卡制下经验来自击杀同级怪，
        /// 推进到第 N 章时等级大致就落在这附近；这偏保守，但偏保守的胜率才有诊断价值。
        /// </summary>
        public static string RunChapterWinrate(int perType, bool resetHp, int trials)
        {
            var sb = new StringBuilder();
            sb.Append($"每章 {perType} 场/类型 × {trials} 次独立装备采样；每场重置生命={resetHp}；加权 战45/弓30/盗25\n");
            sb.Append("章节  怪级   装备品质   精英胜率    Boss胜率    (Boss 逐类型)\n");
            for (int ch = 1; ch <= GameConfig.ChapterCount; ch++)
                sb.Append(RunChapterWinrateCell(ch, perType, resetHp, trials)).Append("\n");
            return sb.ToString();
        }

        /// <summary>单个章节的胜率行（拆出来是为了能分批调用）。</summary>
        public static string RunChapterWinrateCell(int chapter, int perType, bool resetHp, int trials)
        {
            float[] acc = RunChapterWinrateRow(chapter, perType, resetHp, trials, out float avgQuality, out int[] accWin);
            int monsterLevel = GameConfig.ChapterMonsterLevelStep * chapter;
            var sb = new StringBuilder();
            sb.Append($"{chapter,4}  {monsterLevel,4}   {avgQuality,8:0.00}   ");
            for (int c = 0; c < acc.Length; c++) sb.Append($"{acc[c] * 100f,8:0.0}%  ");
            int n = perType * trials;
            sb.Append($"   战 {(float)accWin[0] / n * 100f:0.0}% / 弓 {(float)accWin[1] / n * 100f:0.0}% / 盗 {(float)accWin[2] / n * 100f:0.0}%");
            return sb.ToString();
        }

        /// <summary>
        /// 单个章节的两格胜率（0~1），顺序：精英 / Boss。
        /// avgQuality 是 trials 次采样里的平均品质序号（0=普通 ~ 4=传说），
        /// 用来观察「推到这一章时装备大概什么档次」——胜率异常时先看它是否偏离预期。
        /// </summary>
        public static float[] RunChapterWinrateRow(int chapter, int perType, bool resetHp, int trials,
                                                   out float avgQuality, out int[] accWin,
                                                   GearBand band = GearBand.Neutral)
        {
            var battle = new BattleSystem();
            float[] w = { 0.45f, 0.30f, 0.25f };
            MonsterTier[] tierArr = { MonsterTier.Elite, MonsterTier.Boss };

            var acc = new float[tierArr.Length];
            accWin = new int[3];
            avgQuality = 0f;
            int qualityCount = 0;
            for (int tr = 0; tr < trials; tr++)
            {
                var p = BuildForChapter(chapter, band);
                for (int i = 0; i < EquipmentSlots.Count; i++)
                {
                    Equipment gear = p.GetEquipment(EquipmentSlots.All[i]);
                    if (gear == null) continue;
                    avgQuality += (int)gear.quality;
                    qualityCount++;
                }
                for (int c = 0; c < tierArr.Length; c++)
                {
                    for (int t = 0; t < 3; t++)
                    {
                        int wins = 0;
                        var type = (MonsterType)t;
                        for (int i = 0; i < perType; i++)
                        {
                            var m = MonsterData.CreateForLevel(
                                GameConfig.ChapterMonsterLevelStep * chapter, type, tierArr[c], MonsterAffix.None);
                            if (Fight(p, m, battle, resetHp)) wins++;
                        }
                        acc[c] += w[t] * ((float)wins / perType);
                        if (c == tierArr.Length - 1) accWin[t] += wins;
                    }
                }
            }
            for (int c = 0; c < acc.Length; c++) acc[c] /= trials;
            avgQuality = qualityCount > 0 ? avgQuality / qualityCount : 0f;
            return acc;
        }

        /// <summary>
        /// 按章节构造主角：等级 = 该章怪级，装备（已解锁的槽位）按某一档掉落品质权重抽一件。
        /// 旧口径固定用「精英保底」，理由是普通怪 62.5% 出普通品质、用最保守的权重会得出
        /// 「所有中期章节都必败」，没有诊断价值；但那也只是猜测，代表不了玩家真实配装。
        /// 现在把三种假设都跑一遍（悲观测普通怪、中性测精英、乐观测 Boss），结论只认三档同向。
        /// 巅峰技能给 0：满级前本来就没有巅峰。
        /// </summary>
        public enum GearBand { Pessimistic, Neutral, Optimistic }

        static PlayerData BuildForChapter(int chapter) => BuildForChapter(chapter, GearBand.Neutral);

        static PlayerData BuildForChapter(int chapter, GearBand band)
        {
            int level = Mathf.Min(GameConfig.ChapterMonsterLevelStep * chapter, GameConfig.MaxLevel);
            MonsterTier dropTier = band == GearBand.Pessimistic ? MonsterTier.Normal
                : band == GearBand.Optimistic ? MonsterTier.Boss : MonsterTier.Elite;
            float[] weights = GameConfig.MonsterTierDropQualityWeights[(int)dropTier];
            var p = new PlayerData();
            p.level = level;
            p.paragonLevel = 0;
            p.paragonExp = 0;
            // 按当前章节能解锁的槽位给装备：头盔 3-10、鞋 6-10、饰品 11-10 之后才有。
            p.maxChapterUnlocked = chapter;
            for (int i = 0; i < EquipmentSlots.Count; i++)
            {
                EquipmentSlot slot = EquipmentSlots.All[i];
                if (!p.IsEquipmentSlotUnlocked(slot)) continue;
                p.SetEquipment(LootSystem.CreateEquipment(slot, level, LootSystem.RollQualityWithWeights(weights)));
            }
            p.FillHp();
            return p;
        }

        [MenuItem("Tools/平衡回归/章节胜率表（缺口 2）")]
        static void MenuChapterWinrate()
        {
            Debug.Log("[ChapterWinrate]\n" + RunChapterWinrate(30, true, 12));
        }


        /// <summary>
        /// 章节胜率「带」：同一章在悲观 / 中性 / 乐观三种配装假设下各跑一遍。
        /// 旧口径只给中性一条线，README 那句「中间章节只做了近似验证」指的就是这条线的代表性未知；
        /// 三档并列后，只要结论在三档下方向一致，就可以说结论不依赖配装假设。
        /// </summary>
        [MenuItem("Tools/平衡回归/章节胜率带（悲观/中性/乐观配装）")]
        static void MenuChapterWinrateBand()
        {
            const string path = "E:/pytest/unitydsh1/HelloWorldProject/_shots/chapter-winrate-band.txt";
            var sb = new StringBuilder();
            sb.AppendLine("[章节胜率带] 每章每档 12 次装备采样 × 30 场/类型；加权 战45/弓30/盗25；每场重置生命");
            sb.AppendLine("配装假设：悲观=普通怪品质权重 / 中性=精英 / 乐观=Boss");
            sb.AppendLine("章节  怪级    悲观 精英/Boss     中性 精英/Boss     乐观 精英/Boss");
            for (int ch = 1; ch <= GameConfig.ChapterCount; ch++)
            {
                var line = new StringBuilder($"{ch,4}  {GameConfig.ChapterMonsterLevelStep * ch,4}   ");
                foreach (GearBand band in new[] { GearBand.Pessimistic, GearBand.Neutral, GearBand.Optimistic })
                {
                    float[] acc = RunChapterWinrateRow(ch, 30, true, 12, out float q, out int[] wins, band);
                    line.Append($"{acc[0] * 100f,6:0.0}%/{acc[1] * 100f,6:0.0}%   ");
                }
                sb.AppendLine(line.ToString());
            }
            string text = sb.ToString();
            Debug.Log("[ChapterWinrateBand]\n" + text);
            System.IO.File.WriteAllText(path, text);
        }
        /// <summary>
        /// P200 的 11-10 胜率重测（README / docs/rebirth.md §14.4 的 80.8% / 90.5% 是 FinalBossStatFactor=1.6 口径）。
        /// 口径与旧数据一致：perType=30、trials=12、resetHp=true、smartPick=true，加权 战45/弓30/盗25；
        /// Lv.110 Boss 那一列即 11-10。两档都走 RunWinrateRow，技能在套 rank 之后选（旧数据同口径）。
        /// </summary>
        [MenuItem("Tools/平衡回归/P200 的 11-10 胜率（baseline vs 满轮回）")]
        static void MenuP200FinalBossWinrate()
        {
            const string path = "E:/pytest/unitydsh1/HelloWorldProject/_shots/p200-11-10.txt";
            var sb = new StringBuilder();
            sb.AppendLine($"[P200-11-10] FinalBossStatFactor={GameConfig.FinalBossStatFactor:0.00}；perType=30，trials=12，resetHp=true，smartPick=true；加权 战45/弓30/盗25；Lv.110 Boss = 11-10");
            AppendP200Row(sb, "全 0（baseline）", 0, 0, 0, 0, 0);
            AppendP200Row(sb, "全 5（满轮回）", 5, 5, 5, 5, 5);
            string text = sb.ToString();
            Debug.Log("[P200-11-10]\n" + text);
            System.IO.File.WriteAllText(path, text);
        }

        /// <summary>
        /// 11-10 归因：把「1.6→1.25 的终局倍率」与「技能 1.0 的 3 技能 → 技能 2.x 的 9 技能」两个变量分开。
        /// 旧数据（80.83% / 90.54%）是 1.6 + 3 技能口径，必须能复现才说明新的 99% 不是探针坏了。
        /// </summary>
        [MenuItem("Tools/平衡回归/P200 的 11-10 归因（1.25/1.6 × 3/9 技能）")]
        static void MenuP200Attribution()
        {
            const string path = "E:/pytest/unitydsh1/HelloWorldProject/_shots/p200-11-10-attrib.txt";
            var sb = new StringBuilder();
            sb.AppendLine("[P200-11-10 归因] perType=30，trials=12，resetHp=true，smartPick=true；加权 战45/弓30/盗25");
            float saved = GameConfig.FinalBossStatFactor;
            try
            {
                sb.AppendLine($"当前配置 FinalBossStatFactor={saved:0.00}");
                sb.AppendLine("倍率 × 技能数 → 11-10 胜率（全 0 转生 / 全 5 满轮回）");
                foreach (float factor in new[] { 1.25f, 1.6f })
                {
                    GameConfig.FinalBossStatFactor = factor;
                    foreach (int skills in new[] { 9, 3 })
                    {
                        sb.AppendLine(AttribRow(factor, skills, 0, 0, 0, 0, 0, "全 0"));
                        sb.AppendLine(AttribRow(factor, skills, 5, 5, 5, 5, 5, "全 5"));
                    }
                }
            }
            finally
            {
                GameConfig.FinalBossStatFactor = saved;
            }

            string text = sb.ToString();
            Debug.Log("[P200-11-10 归因]\n" + text);
            System.IO.File.WriteAllText(path, text);
        }

        /// <summary>
        /// 9 技能口径下的终局倍率扫描：给「要不要把 FinalBossStatFactor 从 1.25 调回去」一个可读的曲线。
        /// 目标区间按 §14.4 的 80~90%（P200 + 满级传说套 + BiS 技能），但那是技能 1.0 的 3 技能口径，
        /// 现在实际是 9 技能，所以曲线以 9 技能为准。
        /// </summary>
        [MenuItem("Tools/平衡回归/P200 的 11-10 倍率扫描（9 技能）")]
        static void MenuP200FactorSweep()
        {
            const string path = "E:/pytest/unitydsh1/HelloWorldProject/_shots/p200-11-10-sweep.txt";
            var sb = new StringBuilder();
            sb.AppendLine("[P200-11-10 倍率扫描] 9 技能，perType=30，trials=12，resetHp=true，smartPick=true；加权 战45/弓30/盗25");
            float saved = GameConfig.FinalBossStatFactor;
            try
            {
                foreach (float factor in new[] { 1.25f, 1.4f, 1.45f, 1.5f, 1.55f, 1.6f })
                {
                    GameConfig.FinalBossStatFactor = factor;
                    sb.AppendLine(AttribRow(factor, 9, 0, 0, 0, 0, 0, "全 0"));
                    sb.AppendLine(AttribRow(factor, 9, 5, 5, 5, 5, 5, "全 5"));
                }
            }
            finally
            {
                GameConfig.FinalBossStatFactor = saved;
            }

            string text = sb.ToString();
            Debug.Log("[P200-11-10 倍率扫描]\n" + text);
            System.IO.File.WriteAllText(path, text);
        }

        /// <summary>
        /// 降噪复跑：trials 12 → 30（1080 → 2700 场/行），只测四个关键倍率。
        /// 第一轮 1.6 × 9 技能两次跑出 85.3% 与 88.5%，说明单行噪声有 ±3 个点，结论不能靠单次。
        /// </summary>
        [MenuItem("Tools/平衡回归/P200 的 11-10 倍率扫描（降噪：30 trials）")]
        static void MenuP200FactorSweepLowNoise()
        {
            const string path = "E:/pytest/unitydsh1/HelloWorldProject/_shots/p200-11-10-sweep2.txt";
            var sb = new StringBuilder();
            sb.AppendLine("[P200-11-10 倍率扫描·降噪] 9 技能，perType=30，trials=30（2700 场/行），resetHp=true，smartPick=true；加权 战45/弓30/盗25");
            float saved = GameConfig.FinalBossStatFactor;
            try
            {
                foreach (float factor in new[] { 1.25f, 1.45f, 1.55f, 1.6f })
                {
                    GameConfig.FinalBossStatFactor = factor;
                    sb.AppendLine(AttribRow(factor, 9, 0, 0, 0, 0, 0, "全 0", 30));
                    sb.AppendLine(AttribRow(factor, 9, 5, 5, 5, 5, 5, "全 5", 30));
                }
            }
            finally
            {
                GameConfig.FinalBossStatFactor = saved;
            }

            string text = sb.ToString();
            Debug.Log("[P200-11-10 倍率扫描·降噪]\n" + text);
            System.IO.File.WriteAllText(path, text);
        }

        static string AttribRow(float factor, int skills, int rb, int rg, int rf, int rl, int ra, string label, int trials = 12)
        {
            const int per = 30;
            int tr = trials;
            float[] acc = RunWinrateRowCapped(200, per, true, tr, true, rb, rg, rf, rl, ra, skills, out int skillSum, out int[] accWin);
            int n = per * tr;
            return $"系数 {factor:0.00} × {skills} 技能 · {label}：11-10 {acc[3] * 100f:0.00}%（战 {accWin[0] * 100f / n:0.0}% / 弓 {accWin[1] * 100f / n:0.0}% / 盗 {accWin[2] * 100f / n:0.0}%），实际技能 {skillSum / (float)tr:0.0}";
        }

        /// <summary>带技能数上限的胜率行（口径同 RunWinrateRow 的五 rank 重载）。</summary>
        public static float[] RunWinrateRowCapped(int paragon, int perType, bool resetHp, int trials, bool smartPick,
                                                  int rebirthBattleRank, int rebirthGrowthRank, int rebirthFortuneRank,
                                                  int rebirthLegacyRank, int rebirthAlchemyRank, int maxSkills,
                                                  out int skillSum, out int[] accWin)
        {
            var battle = new BattleSystem();
            float[] w = { 0.45f, 0.30f, 0.25f };
            int[] lvArr = { 100, 100, 110, 110 };
            MonsterTier[] tierArr = { MonsterTier.Elite, MonsterTier.Boss, MonsterTier.Elite, MonsterTier.Boss };

            var acc = new float[lvArr.Length];
            accWin = new int[3];
            skillSum = 0;
            for (int tr = 0; tr < trials; tr++)
            {
                var p = Build(paragon, smartPick, rebirthBattleRank, rebirthGrowthRank,
                    rebirthFortuneRank, rebirthLegacyRank, rebirthAlchemyRank, maxSkills);
                skillSum += p.OwnedSkillCount;
                for (int c = 0; c < lvArr.Length; c++)
                {
                    for (int t = 0; t < 3; t++)
                    {
                        int wins = 0;
                        var type = (MonsterType)t;
                        for (int i = 0; i < perType; i++)
                        {
                            var m = MonsterData.CreateForLevel(lvArr[c], type, tierArr[c], MonsterAffix.None);
                            if (Fight(p, m, battle, resetHp)) wins++;
                        }
                        acc[c] += w[t] * ((float)wins / perType);
                        if (c == lvArr.Length - 1) accWin[t] += wins;
                    }
                }
            }
            for (int c = 0; c < acc.Length; c++) acc[c] /= trials;
            return acc;
        }

        static void AppendP200Row(StringBuilder sb, string label, int rb, int rg, int rf, int rl, int ra)
        {
            const int per = 30, tr = 12;
            float[] acc = RunWinrateRow(200, per, true, tr, true, rb, rg, rf, rl, ra, out int skillSum, out int[] accWin);
            int n = per * tr;
            sb.AppendLine($"{label}：11-10 胜率 {acc[3] * 100f:0.00}%（战 {accWin[0] * 100f / n:0.0}% / 弓 {accWin[1] * 100f / n:0.0}% / 盗 {accWin[2] * 100f / n:0.0}%），平均技能 {skillSum / (float)tr:0.0}");
        }

        /// <summary>构造一个满级主角：传说五件套 + 按技能点自选（探针按推荐分取前 3 实打）。</summary>
        static PlayerData Build(int paragon, bool smartPick = true,
            int rebirthBattleRank = 0, int rebirthGrowthRank = 0, int rebirthFortuneRank = 0,
            int rebirthLegacyRank = 0, int rebirthAlchemyRank = 0, int maxSkills = -1)
        {
            var p = new PlayerData();
            p.level = 100;
            p.paragonLevel = paragon;
            p.paragonExp = 0;
            for (int i = 0; i < EquipmentSlots.Count; i++)
                p.SetEquipment(LootSystem.CreateEquipment(EquipmentSlots.All[i], 100, EquipmentQuality.Legendary));

            if (paragon >= 10) p.BeginDoctrine(ProbePolicy.ProbeDoctrine);

            // 技能 2.1：没有随机候选。每次花 1 点，从本流派未掌握里按推荐分取前 3 实打。
            // maxSkills 用来复现「技能 1.0 时代只有 3 个技能」的旧口径（归因用，-1 = 不限）。
            int learned = 0;
            for (int guard = 0; guard < SkillCatalog.TotalPerDoctrine && p.SkillPoints > 0
                 && (maxSkills < 0 || learned < maxSkills); guard++)
            {
                List<SkillId> candidates = p.AvailableSkillIds();
                if (candidates.Count == 0) break;
                if (candidates.Count > 3)
                {
                    candidates.Sort((a, b) => SkillAdvice.Score(p, b).CompareTo(SkillAdvice.Score(p, a)));
                    candidates.RemoveRange(3, candidates.Count - 3);
                }

                // 与游戏内 TryAutoEquip 一致：只填空槽，槽满只进「已获得」。
                // 以前这里允许替换，结果巅峰 100+ 的模拟会拿 5 场评估出来的「更优技能」
                // 把好技能换掉，胜率随巅峰非单调（P100 27%、P150 10%）。
                bool hasEmpty = false;
                foreach (SkillId id in candidates)
                {
                    int baseSlot = (int)SkillCatalog.Get(id).kind * GameConfig.SkillSlotsPerKind;
                    for (int i = 0; i < GameConfig.SkillSlotsPerKind; i++)
                        if (p.GetSkillSlot(baseSlot + i) < 0) { hasEmpty = true; break; }
                    if (hasEmpty) break;
                }

                if (hasEmpty && smartPick)
                {
                    PickAndEquipBest(p, candidates);   // 真人会在技能面板挑最优（只填空槽）
                }
                else
                {
                    SkillId got = candidates[0];
                    if (!p.GrantSkill(got)) break;
                    SkillDef def = SkillCatalog.Get(got);
                    int baseSlot = (int)def.kind * GameConfig.SkillSlotsPerKind;
                    for (int i = 0; i < GameConfig.SkillSlotsPerKind; i++)
                    {
                        if (p.GetSkillSlot(baseSlot + i) < 0) { p.SetSkillSlot(baseSlot + i, (int)got); break; }
                    }
                }
                learned++;
            }
            // 转生加成在技能挑选之后套：先套会改变技能点评分，构筑与正常玩家不可比。
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
        /// 模拟真人在技能面板自选：对每个候选实际打一场 Lv.110 盗贼 Boss，用「胜 = 1，
        /// 败 = 1 − 怪物剩余血量占比」打分，取分最高者；同时决定装哪个槽
        /// （空槽优先，满槽就把两个槽都试一遍，取表现更好的那个）。
        /// 候选由调用方按 SkillAdvice 取前 3，不再随机。
        /// </summary>
        static SkillId PickAndEquipBest(PlayerData p, List<SkillId> candidates)
        {
            var battle = new BattleSystem();
            SkillId best = candidates[0];
            float bestScore = float.MinValue;
            int bestSlot = -1;

            // 候选里只要还有空槽可填，就只填空槽（决策便宜、不需要降噪，因为不用跟旧技能比）。
            // 全都填满了才进入「替换」模式：这时必须多跑几场，否则评分噪声会盖过技能真实差距。
            bool hasEmpty = false;
            foreach (SkillId id in candidates)
            {
                int baseSlot = (int)SkillCatalog.Get(id).kind * GameConfig.SkillSlotsPerKind;
                for (int i = 0; i < GameConfig.SkillSlotsPerKind; i++)
                {
                    if (p.GetSkillSlot(baseSlot + i) < 0) { hasEmpty = true; break; }
                }
                if (hasEmpty) break;
            }

            int repeats = hasEmpty ? 1 : 5;

            foreach (SkillId id in candidates)
            {
                int baseSlot = (int)SkillCatalog.Get(id).kind * GameConfig.SkillSlotsPerKind;

                for (int i = 0; i < GameConfig.SkillSlotsPerKind; i++)
                {
                    int slot = baseSlot + i;
                    int old = p.GetSkillSlot(slot);
                    if (old == (int)id) break;              // 已经装着这个技能，不必再试
                    if (hasEmpty && old >= 0) continue;     // 还有空槽时不考虑替换

                    p.SetSkillSlot(slot, (int)id);
                    float score = TrialScore(p, battle, repeats);
                    p.SetSkillSlot(slot, old);   // 先还原，落定放到最后

                    if (score > bestScore) { bestScore = score; best = id; bestSlot = slot; }

                    if (old < 0) break;          // 空槽只需试一次
                }
            }

            if (bestSlot >= 0) p.SetSkillSlot(bestSlot, (int)best);
            p.GrantSkill(best);   // 必须置掩码，否则 Build 的 while 会死循环
            return best;
        }

        /// <summary>
        /// 一场 Lv.110 盗贼 Boss 的表现分。关键是要有区分度：早期版本用「胜=1 / 败=看剩余血量」，
        /// 结果满装时场场都赢、所有候选都得 1.0，选择退化成「永远取候选里的第一个」。
        /// 现在赢也按击杀耗时细排：胜 → 1 + 100/(1+秒数)（越快越高，恒 &gt; 1），
        /// 败 → 0~0.5（按把怪打到多少血）。
        /// </summary>
        static float TrialScore(PlayerData p, BattleSystem battle, int repeats)
        {
            // 三种怪各打 repeats 场再平均。只打盗贼一场时，纯输出型技能会被高估，
            // 而实战里盗贼 Boss 恰恰是胜率短板，必须让生存型技能在这里拿得到分。
            // repeats 用来压噪声：一次战斗的胜负/耗时方差极大，单场评分做替换决策等于掷骰子。
            // 评分必须是「胜场数主导、速度只做细分」。
            // 早先的写法是 胜 → 1 + 100/(1+秒)，输 → 0~0.5：这样赢的场次里速度能贡献到 100 倍，
            // 于是纯输出型技能（打战士/弓箭手更快）把生存型技能（能赢盗贼但慢）压下去，
            // 结果满巅峰时盗贼 Boss 胜率被拉到 55%，比随机送技能还差。
            // 现在：赢一场 = 10 起步（+ 最多 1 分的速度奖励），输一场 = 0~1（按打掉的血量）。
            // 多赢一场的价值（10）远大于快几秒（≤1），胜率成为主序。
            float sum = 0f;
            for (int t = 0; t < 3; t++)
            {
                for (int r = 0; r < repeats; r++)
                {
                    battle.ResetSkillState();
                    p.FillHp();
                    var m = MonsterData.CreateForLevel(GameConfig.MaxTierLevel, (MonsterType)t, MonsterTier.Boss, MonsterAffix.None);
                    bool win = Fight(p, m, battle, true, out float seconds);
                    sum += win ? 10f + 1f / (1f + seconds) : 1f - (float)m.currentHp / m.maxHp;
                }
            }
            return sum;
        }

        /// <summary>生成一套固定的 Lv.100 传说五件套，供各类技能审计复用（避免每次随机导致技能间差异）。</summary>
        static Equipment[] LegendarySet()
        {
            var set = new Equipment[EquipmentSlots.Count];
            for (int i = 0; i < EquipmentSlots.Count; i++)
                set[i] = LootSystem.CreateEquipment(EquipmentSlots.All[i], 100, EquipmentQuality.Legendary);
            return set;
        }

        /// <summary>把同一套装备装到角色身上；withWeapon=false 时摘掉武器（生存口径用）。</summary>
        static void EquipSet(PlayerData p, Equipment[] set, bool withWeapon = true)
        {
            for (int i = 0; i < EquipmentSlots.Count; i++)
                p.SetEquipment(set[i]);
            if (!withWeapon) p.weapon = null;
        }

        /// <summary>木桩：主角每 tick 回满血，累计对怪物造成的伤害（被打死就换一只新的）。</summary>
        static float DummyDamage(PlayerData p, float seconds)
        {
            var b = new BattleSystem();
            var log = new BattleLog();
            var m = MonsterData.CreateForLevel(GameConfig.MaxTierLevel, MonsterType.Melee, MonsterTier.Boss, MonsterAffix.None);
            p.FillHp();
            b.BeginBattle(p);
            float total = 0f;
            int last = m.currentHp;
            for (float t = 0f; t < seconds; t += 0.05f)
            {
                p.currentHp = p.MaxHp;
                var res = b.Tick(p, m, 0.05f, log);
                log.Clear();
                if (m.currentHp < last) total += last - m.currentHp;
                last = m.currentHp;
                if (res == BattleTickResult.MonsterKilled)
                {
                    m = MonsterData.CreateForLevel(GameConfig.MaxTierLevel, MonsterType.Melee, MonsterTier.Boss, MonsterAffix.None);
                    last = m.currentHp;
                }
            }
            p.FillHp();
            return total;
        }

        /// <summary>把一场战斗跑到分出胜负，含自动喝药（口径与游戏内一致）。</summary>
        static bool Fight(PlayerData p, MonsterData m, BattleSystem battle, bool resetHp) =>
            Fight(p, m, battle, resetHp, out _);

        /// <summary>带「击杀耗时」的战斗：胜时 seconds = 击杀用的游戏秒数，败时 = 撑到的秒数。</summary>
        static bool Fight(PlayerData p, MonsterData m, BattleSystem battle, bool resetHp, out float seconds)
        {
            seconds = 0f;
            if (resetHp) p.FillHp();
            battle.BeginBattle(p);
            var log = new BattleLog();
            float cd = 0f;
            int left = GameConfig.PotionMaxCount;
            for (int s = 0; s < 150000; s++)
            {
                if (cd > 0f) cd -= 0.05f;
                if (left > 0 && cd <= 0f && p.currentHp > 0 && p.currentHp < p.MaxHp * GameConfig.PotionAutoUseHpPercent)
                {
                    left--;
                    p.currentHp = Mathf.Min(p.MaxHp, p.currentHp + Mathf.RoundToInt(p.MaxHp * GameConfig.PotionHealPercent));
                    cd = GameConfig.PotionCooldown;
                }
                var r = battle.Tick(p, m, 0.05f, log);
                log.Clear();
                if (r == BattleTickResult.MonsterKilled) { seconds = s * 0.05f; return true; }
                if (r == BattleTickResult.PlayerDied) { seconds = s * 0.05f; return false; }
            }
            seconds = 150000 * 0.05f;
            return false;
        }

        static string KindShort(SkillKind k) =>
            k == SkillKind.Active ? "主动" : k == SkillKind.Passive ? "被动" : "触发";

        /// <summary>
        /// 技能强度审计：装备只生成一次并复用，逐个技能单独装到对应类别的空槽，
        /// 测「木桩输出增幅」（主角不死，只看输出）与「对 Lv.110 Boss 的胜率」（看生存价值）。
        /// 每场都新建 BattleSystem，所以主动技能冷却每场从 0 开始，各技能口径一致。
        /// </summary>
        public static string SkillAudit(int paragon, float seconds, int repeats, int battles)
        {
            var set = LegendarySet();

            PlayerData Template()
            {
                var p = new PlayerData();
                p.level = 100;
                p.paragonLevel = paragon;
                p.paragonExp = 0;
                EquipSet(p, set);
                p.autoBuyPotion = true;
                p.potionCount = GameConfig.PotionMaxCount;
                p.FillHp();
                return p;
            }

            float BaseDamage()
            {
                float sum = 0f;
                for (int i = 0; i < repeats; i++) sum += DummyDamage(Template(), seconds);
                return sum / repeats;
            }

            int BaseWins()
            {
                int win = 0;
                for (int i = 0; i < battles; i++)
                {
                    var p = Template();
                    var m = MonsterData.CreateForLevel(GameConfig.MaxTierLevel, MonsterType.Melee, MonsterTier.Boss, MonsterAffix.None);
                    if (Fight(p, m, new BattleSystem(), true)) win++;
                }
                return win;
            }

            var sb = new StringBuilder();
            var p0 = Template();
            sb.Append($"基准：Lv.100 传说套 + 巅峰 {paragon}（技能数值为固定值）\n");
            sb.Append($"基准属性：攻击 {p0.Attack}  暴击率 {p0.CritChance * 100f:0.0}%  暴击伤害 {p0.CritDamage * 100f:0.0}%  " +
                      $"速度 {p0.Speed}  间隔 {BattleSystem.PlayerAttackInterval(p0):0.000}s  穿透 {p0.ArmorPenetration * 100f:0.0}%  " +
                      $"生命 {p0.MaxHp}  防御 {p0.Defense}\n");
            sb.Append($"木桩口径：对 Lv.110 Boss 打 {seconds:0} 秒 × {repeats} 次取平均；胜率口径：对 Lv.110 Boss {battles} 场、每场重置生命\n\n");

            float baseDmg = BaseDamage();
            int baseWin = BaseWins();
            float baseWr = baseWin * 100f / battles;
            sb.Append($"无技能基准：木桩伤害 {baseDmg:0}（DPS {baseDmg / seconds:0}），对 Boss 胜率 {baseWr:0.0}%\n\n");
            sb.Append("技能                类别   木桩DPS增幅   对Boss胜率   相对基准\n");

            foreach (var def in SkillCatalog.All)
            {
                float sum = 0f;
                for (int i = 0; i < repeats; i++)
                {
                    var p = Template();
                    p.SetSkillSlot((int)def.kind * GameConfig.SkillSlotsPerKind, (int)def.id);
                    sum += DummyDamage(p, seconds);
                }
                float dmg = sum / repeats;

                int win = 0;
                for (int i = 0; i < battles; i++)
                {
                    var p = Template();
                    p.SetSkillSlot((int)def.kind * GameConfig.SkillSlotsPerKind, (int)def.id);
                    var m = MonsterData.CreateForLevel(GameConfig.MaxTierLevel, MonsterType.Melee, MonsterTier.Boss, MonsterAffix.None);
                    if (Fight(p, m, new BattleSystem(), true)) win++;
                }
                float wr = win * 100f / battles;

                sb.Append(def.name.PadRight(7) + "(" + KindShort(def.kind) + ")  ");
                sb.Append($"{(dmg / baseDmg - 1f) * 100f,+10:0.0}%   ");
                sb.Append($"{wr,8:0.0}%   ");
                sb.Append($"{(wr - baseWr),+10:0.0}pt\n");
            }
            return sb.ToString();
        }

        /// <summary>主角能撑多久（秒）；跑不死怪物返回 -1，超时返回 2000。</summary>
        static float SurvivalSeconds(PlayerData p, int monsterLevel, float monsterHpScale = 1f)
        {
            var b = new BattleSystem();
            var log = new BattleLog();
            var m = MonsterData.CreateForLevel(monsterLevel, MonsterType.Melee, MonsterTier.Boss, MonsterAffix.None);
            if (monsterHpScale != 1f)
            {
                m.maxHp = Mathf.Max(1, Mathf.RoundToInt(m.maxHp * monsterHpScale));
                m.currentHp = m.maxHp;
            }
            p.FillHp();
            b.BeginBattle(p);
            float cd = 0f;
            int left = GameConfig.PotionMaxCount;
            for (int s = 0; s < 40000; s++)
            {
                if (cd > 0f) cd -= 0.05f;
                if (left > 0 && cd <= 0f && p.currentHp > 0 && p.currentHp < p.MaxHp * GameConfig.PotionAutoUseHpPercent)
                {
                    left--;
                    p.currentHp = Mathf.Min(p.MaxHp, p.currentHp + Mathf.RoundToInt(p.MaxHp * GameConfig.PotionHealPercent));
                    cd = GameConfig.PotionCooldown;
                }
                var r = b.Tick(p, m, 0.05f, log);
                log.Clear();
                if (r == BattleTickResult.PlayerDied) return (s + 1) * 0.05f;
                if (r == BattleTickResult.MonsterKilled) return -1f;
            }
            return 2000f;
        }

        /// <summary>
        /// 生存口径：摘掉武器把输出压到接近 0，只测各技能能让主角多撑多少秒。
        /// 输出向技能在这一列应当接近 0（它们不提供生存），生存向技能则主要看这一列。
        /// </summary>
        public static string SurvivalAudit(int paragon, int monsterLevel, int trials, bool withWeapon = false)
        {
            var set = LegendarySet();
            // 带武器时把怪物血量放大，保证怪物打不死，主角只能靠自己活下来。
            float hpScale = withWeapon ? 500f : 1f;

            PlayerData Template()
            {
                var p = new PlayerData();
                p.level = 100;
                p.paragonLevel = paragon;
                p.paragonExp = 0;
                EquipSet(p, set, withWeapon);   // 不带武器时输出可忽略
                p.autoBuyPotion = true;
                p.potionCount = GameConfig.PotionMaxCount;
                p.FillHp();
                return p;
            }

            float Measure(SkillId? id)
            {
                float sum = 0f;
                for (int i = 0; i < trials; i++)
                {
                    var p = Template();
                    if (id.HasValue) p.SetSkillSlot((int)SkillCatalog.Get(id.Value).kind * GameConfig.SkillSlotsPerKind, (int)id.Value);
                    float t = SurvivalSeconds(p, monsterLevel, hpScale);
                    sum += t < 0f ? 2000f : t;
                }
                return sum / trials;
            }

            var sb = new StringBuilder();
            var p0 = Template();
            float baseT = Measure(null);
            sb.Append($"生存口径：Lv.100 传说五件套、{(withWeapon ? "**带武器**（输出全开，怪物血量 ×500 保证打不死）" : "**无武器**（输出压到接近 0）")}+ 巅峰 {paragon}（技能固定值），对手 Lv.{monsterLevel} Boss，{trials} 次取平均\n");
            sb.Append($"基准属性：生命 {p0.MaxHp}  防御 {p0.Defense}  攻击 {p0.Attack}\n");
            sb.Append($"无技能基准存活：{baseT:0.0} 秒\n\n");
            sb.Append("技能                类别   存活(秒)   相对基准    折算有效生命\n");

            foreach (var def in SkillCatalog.All)
            {
                float t = Measure(def.id);
                float ratio = baseT > 0f ? t / baseT : 0f;
                sb.Append(def.name.PadRight(7) + "(" + KindShort(def.kind) + ")  ");
                sb.Append($"{t,9:0.0}   ");
                sb.Append($"{(ratio - 1f) * 100f,+9:0.0}%   ");
                sb.Append($"{p0.MaxHp * ratio,9:0} ({(ratio - 1f) * 100f,+6:0.0}%)\n");
            }
            return sb.ToString();
        }

        /// <summary>
        /// 槽位拥挤审计：主动技能共享 1 秒公共冷却，多装一个主动会互相抢时间；
        /// 触发技能没有公共冷却，理论上可以叠加。逐个比较「单装 vs 两两组合」的边际收益。
        /// </summary>
        public static string ComboAudit(int paragon, float seconds, int repeats)
        {
            var set = LegendarySet();

            PlayerData Template(params SkillId[] ids)
            {
                var p = new PlayerData();
                p.level = 100;
                p.paragonLevel = paragon;
                p.paragonExp = 0;
                EquipSet(p, set);
                p.autoBuyPotion = true;
                p.potionCount = GameConfig.PotionMaxCount;
                foreach (var id in ids)
                {
                    var def = SkillCatalog.Get(id);
                    int baseSlot = (int)def.kind * GameConfig.SkillSlotsPerKind;
                    for (int i = 0; i < GameConfig.SkillSlotsPerKind; i++)
                        if (p.GetSkillSlot(baseSlot + i) < 0) { p.SetSkillSlot(baseSlot + i, (int)id); break; }
                }
                p.FillHp();
                return p;
            }

            float Dmg(params SkillId[] ids)
            {
                float sum = 0f;
                for (int i = 0; i < repeats; i++) sum += DummyDamage(Template(ids), seconds);
                return sum / repeats;
            }

            var sb = new StringBuilder();
            sb.Append($"槽位拥挤审计：Lv.100 传说套 + 巅峰 {paragon}，木桩 {seconds:0} 秒 × {repeats} 次\n\n");
            float baseDmg = Dmg();

            var actives = SkillCatalog.OfKind(SkillKind.Active);
            sb.Append("单装主动技能：\n");
            var solo = new float[actives.Length];
            for (int i = 0; i < actives.Length; i++)
            {
                solo[i] = Dmg(actives[i]);
                sb.Append($"  {SkillCatalog.Get(actives[i]).name.PadRight(6)}  {(solo[i] / baseDmg - 1f) * 100f,+7:0.0}%\n");
            }

            sb.Append("\n两两组合（第 2 个技能带来的「边际」增幅，与它单装时对比）：\n");
            sb.Append("  组合                     总增幅     第2个边际    该技能单装    挤压损失\n");
            for (int i = 0; i < actives.Length; i++)
            {
                for (int j = 0; j < actives.Length; j++)
                {
                    if (i == j) continue;
                    float both = Dmg(actives[i], actives[j]);
                    float pairGain = (both / baseDmg - 1f) * 100f;
                    float second = (both / solo[i] - 1f) * 100f;   // 在已有 i 的基础上加 j
                    float alone = (solo[j] / baseDmg - 1f) * 100f;
                    string n1 = SkillCatalog.Get(actives[i]).name;
                    string n2 = SkillCatalog.Get(actives[j]).name;
                    sb.Append($"  {n1}+{n2}".PadRight(24));
                    sb.Append($"{pairGain,8:0.0}%   {second,9:0.0}%   {alone,9:0.0}%   {second - alone,+8:0.0}pt\n");
                }
            }

            var triggers = SkillCatalog.OfKind(SkillKind.Trigger);
            sb.Append("\n触发技能两两组合（无公共冷却，看是否真能叠加）：\n");
            for (int i = 0; i < triggers.Length; i++)
            {
                for (int j = i + 1; j < triggers.Length; j++)
                {
                    float both = Dmg(triggers[i], triggers[j]);
                    string n1 = SkillCatalog.Get(triggers[i]).name;
                    string n2 = SkillCatalog.Get(triggers[j]).name;
                    sb.Append($"  {n1}+{n2}".PadRight(24));
                    sb.Append($"{(both / baseDmg - 1f) * 100f,8:0.0}%\n");
                }
            }
            return sb.ToString();
        }

        /// <summary>
        /// 巅峰符文木桩矩阵（2026-11-04）：两个流派 × 无符文 / 全甲 / 全乙。
        /// 同一套 Lv.100 传说装、同一个毕业六格技能，只改符文形态，300 秒木桩平均 DPS。
        /// 验收口径：任一格相对同流派「无符文」基准超出 ±25% 即判失败（方案 C 的验收条件）。
        /// </summary>
        [MenuItem("Tools/测试/巅峰符文木桩矩阵（2 流派 × 3 配置，300 秒）")]
        public static void MenuRuneMatrix() => RunRuneMatrix(300f, 3, 1000);

        [MenuItem("Tools/测试/巅峰符文木桩矩阵（冒烟：30 秒）")]
        public static void MenuRuneMatrixSmoke() => RunRuneMatrix(30f, 1, 1000);

        const string RuneMatrixPath = "E:/pytest/unitydsh1/HelloWorldProject/_shots/rune-matrix.txt";

        static void RunRuneMatrix(float seconds, int repeats, int paragon)
        {
            var set = LegendarySet();

            PlayerData Template(int doctrine, int form)
            {
                var p = new PlayerData();
                p.level = 100;
                p.paragonLevel = paragon;
                p.paragonExp = 0;
                EquipSet(p, set);
                p.autoBuyPotion = true;
                p.potionCount = GameConfig.PotionMaxCount;
                p.BeginDoctrine(doctrine);
                p.GrantDoctrineKit(doctrine);
                p.EquipDoctrineBuild(doctrine);

                if (form >= 0)
                {
                    for (int slot = 0; slot < PlayerData.SkillSlotCount; slot++)
                    {
                        int raw = p.GetSkillSlot(slot);
                        if (raw < 0) continue;
                        var id = (SkillId)raw;
                        if (!RuneCatalog.Has(id)) continue;
                        p.UnlockRune(id);
                        p.SetRuneForm(id, form == 0 ? RuneForm.A : RuneForm.B);
                    }
                }

                p.FillHp();
                return p;
            }

            var sb = new StringBuilder();
            sb.Append($"巅峰符文木桩矩阵（{System.DateTime.Now:yyyy-MM-dd HH:mm}）\n");
            sb.Append($"口径：Lv.100 传说五件套 + 巅峰 {paragon}（符文 {RuneCatalog.PointsFor(paragon)} 点）；" +
                      $"每流派装 SkillCatalog.BuildOf 的毕业六格；木桩 {seconds:0} 秒 × {repeats} 次取平均；" +
                      $"验收：相对同流派无符文基准 ±25% 以内\n");
            sb.Append($"按档位说明：甲 = RuneForm.A，乙 = RuneForm.B；只解锁已装备的六个技能\n\n");

            var configs = new[] { -1, 0, 1 };
            var configName = new[] { "无符文", "全甲", "全乙" };
            bool allPass = true;

            for (int doctrine = 1; doctrine <= 2; doctrine++)
            {
                float baseline = 0f;
                var rows = new float[configs.Length];
                for (int ci = 0; ci < configs.Length; ci++)
                {
                    float sum = 0f;
                    for (int i = 0; i < repeats; i++) sum += DummyDamage(Template(doctrine, configs[ci]), seconds);
                    rows[ci] = sum / repeats / seconds;
                    if (ci == 0) baseline = rows[ci];
                }

                sb.Append($"【{SkillCatalog.SchoolName(doctrine)}】无符文基准 DPS {baseline:0}\n");
                for (int ci = 0; ci < configs.Length; ci++)
                {
                    float ratio = baseline > 0f ? rows[ci] / baseline : 1f;
                    bool pass = System.Math.Abs(ratio - 1f) <= 0.25f;
                    if (!pass) allPass = false;
                    sb.Append($"  {configName[ci],-5} DPS {rows[ci],8:0.0}   {(ratio - 1f) * 100f,+7:0.0}%   {(pass ? "PASS" : "**超出 ±25%**")}\n");
                }
                sb.Append("\n");
            }

            sb.Append(allPass ? "结论：全部格子在 ±25% 以内，方案 C 的 DPS 验收通过。\n"
                              : "结论：存在超出 ±25% 的格子，需要回看形态数值。\n");

            System.IO.File.WriteAllText(RuneMatrixPath, sb.ToString());
            Debug.Log("[RUNE] " + sb.ToString().Replace("\n", " | "));
            Debug.Log($"[RUNE] 已写入 {RuneMatrixPath}");
        }
    }
}

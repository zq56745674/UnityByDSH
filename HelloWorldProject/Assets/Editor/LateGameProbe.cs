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
            sb.Append($"武器 {p.weapon.StatLine} || {p.weapon.AffixLine}\n");
            sb.Append($"衣服 {p.armor.StatLine} || {p.armor.AffixLine}\n");

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
        public static string RunWinrateCell(int paragon, int perType, bool resetHp, bool detail, int trials, bool smartPick = true)
        {
            float[] acc = RunWinrateRow(paragon, perType, resetHp, trials, smartPick, out int skillSum, out int[] accWin);
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
            var battle = new BattleSystem();
            float[] w = { 0.45f, 0.30f, 0.25f };
            int[] lvArr = { 100, 100, 110, 110 };
            MonsterTier[] tierArr = { MonsterTier.Elite, MonsterTier.Boss, MonsterTier.Elite, MonsterTier.Boss };

            var acc = new float[lvArr.Length];
            accWin = new int[3];
            skillSum = 0;
            for (int tr = 0; tr < trials; tr++)
            {
                var p = Build(paragon, smartPick);
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
                                                   out float avgQuality, out int[] accWin)
        {
            var battle = new BattleSystem();
            float[] w = { 0.45f, 0.30f, 0.25f };
            MonsterTier[] tierArr = { MonsterTier.Elite, MonsterTier.Boss };

            var acc = new float[tierArr.Length];
            accWin = new int[3];
            avgQuality = 0f;
            for (int tr = 0; tr < trials; tr++)
            {
                var p = BuildForChapter(chapter);
                avgQuality += (int)p.weapon.quality;
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
            avgQuality /= trials;
            return acc;
        }

        /// <summary>
        /// 按章节构造主角：等级 = 该章怪级，武器与衣服各按「精英保底掉落」的品质权重抽一件。
        /// 用精英权重而不是普通怪 QualityWeights，是因为普通怪 62.5% 出普通品质，
        /// 玩家的可用装备其实来自精英与 Boss；用最保守的权重会得出「所有中期章节都必败」，
        /// 那种结论没有诊断价值。巅峰技能给 0：满级前本来就没有巅峰。
        /// </summary>
        static PlayerData BuildForChapter(int chapter)
        {
            int level = Mathf.Min(GameConfig.ChapterMonsterLevelStep * chapter, GameConfig.MaxLevel);
            float[] weights = GameConfig.MonsterTierDropQualityWeights[(int)MonsterTier.Elite];
            var p = new PlayerData();
            p.level = level;
            p.paragonLevel = 0;
            p.paragonExp = 0;
            p.weapon = LootSystem.CreateEquipment(EquipmentSlot.Weapon, level, LootSystem.RollQualityWithWeights(weights));
            p.armor = LootSystem.CreateEquipment(EquipmentSlot.Armor, level, LootSystem.RollQualityWithWeights(weights));
            p.FillHp();
            return p;
        }

        [MenuItem("Tools/平衡回归/章节胜率表（缺口 2）")]
        static void MenuChapterWinrate()
        {
            Debug.Log("[ChapterWinrate]\n" + RunChapterWinrate(30, true, 12));
        }

        /// <summary>构造一个满级主角：传说武器 + 传说衣服 + 按「巅峰每 10 级随机一个技能」自动装备。</summary>
        static PlayerData Build(int paragon, bool smartPick = true)
        {
            var p = new PlayerData();
            p.level = 100;
            p.paragonLevel = paragon;
            p.paragonExp = 0;
            p.weapon = LootSystem.CreateEquipment(EquipmentSlot.Weapon, 100, EquipmentQuality.Legendary);
            p.armor = LootSystem.CreateEquipment(EquipmentSlot.Armor, 100, EquipmentQuality.Legendary);

            int want = Mathf.Min(paragon / GameConfig.ParagonPassiveEvery, SkillCatalog.All.Length);
            while (p.OwnedSkillCount < want)
            {
                var candidates = p.RollSkillCandidates(GameConfig.SkillChoiceCount);
                if (candidates.Count == 0) break;

                // 与游戏内 TryAutoEquip 一致：只填空槽，槽满了就只进「已获得」。
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
                    PickAndEquipBest(p, candidates);   // 真人会在三选一里挑最优（只填空槽）
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
            }
            p.FillHp();
            return p;
        }

        /// <summary>
        /// 模拟真人玩三选一：对每个候选实际打一场 Lv.110 盗贼 Boss，用「胜 = 1，
        /// 败 = 1 − 怪物剩余血量占比」打分，取分最高者；同时决定装哪个槽
        /// （空槽优先，满槽就把两个槽都试一遍，取表现更好的那个）。
        /// 旧口径是随机直接送技能，那套胜率相当于「技能乱给」的下界。
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

        /// <summary>
        /// 装备评分合理性审计：逐词条算「每 1 点评分换来多少期望 DPS」。
        /// 用理论期望 DPS（不含随机浮动、不含技能触发），这样权重是否与实战收益一致可以直接看出来。
        /// </summary>
        public static string ScoreAudit(int paragon)
        {
            var m = MonsterData.CreateForLevel(GameConfig.MaxTierLevel, MonsterType.Melee, MonsterTier.Boss, MonsterAffix.None);
            var p0 = Build(paragon);
            float baseDps = ExpectedDps(p0, m);

            var sb = new StringBuilder();
            sb.Append($"基准：Lv.100 传说套 + 巅峰 {paragon}；对手 Lv.110 Boss（防御 {m.defense}，等级 {m.level}）\n");
            sb.Append($"基准属性：攻击 {p0.Attack}  暴击率 {p0.CritChance * 100f:0.0}%  暴击伤害 {p0.CritDamage * 100f:0.0}%  " +
                      $"速度 {p0.Speed}  间隔 {BattleSystem.PlayerAttackInterval(p0):0.000}s  穿透 {p0.ArmorPenetration * 100f:0.0}%\n");
            sb.Append($"基准期望 DPS：{baseDps:0}（攻击×(1-减伤)×(1+暴击率×(暴击倍率-1))÷攻击间隔）\n\n");
            sb.Append("词条                 +1单位     评分增量   期望DPS增幅   每分收益(每点DPS%)\n");

            Probe(sb, m, paragon, "主属性 攻击", "+10", p => p.weapon.attack += 10);
            Probe(sb, m, paragon, "主属性 暴击率", "+1%", p => p.weapon.critChance += 0.01f);
            Probe(sb, m, paragon, "主属性 暴击伤害", "+10%", p => p.weapon.critDamage += 0.10f);
            Probe(sb, m, paragon, "副词条 暴击率", "+1%", p => p.weapon.affixCritChance += 0.01f);
            Probe(sb, m, paragon, "副词条 暴击伤害", "+10%", p => p.weapon.affixCritDamage += 0.10f);
            Probe(sb, m, paragon, "副词条 攻击速度", "+1%", p => p.weapon.affixAttackSpeed += 0.01f);
            Probe(sb, m, paragon, "副词条 全属性", "+1%", p => p.weapon.affixAllStats += 0.01f);
            Probe(sb, m, paragon, "副词条 护甲穿透", "+1%", p => p.weapon.affixArmorPen += 0.01f);
            Probe(sb, m, paragon, "主属性 防御", "+10", p => p.armor.defense += 10);
            Probe(sb, m, paragon, "主属性 生命", "+100", p => p.armor.hp += 100);
            return sb.ToString();
        }

        /// <summary>给基准角色加一个词条增量，比较评分增量和期望 DPS 增幅。</summary>
        static void Probe(StringBuilder sb, MonsterData m, int paragon, string label, string unit,
            System.Action<PlayerData> mutate)
        {
            var p = Build(paragon);
            float s0 = ScoreOf(p);
            float d0 = ExpectedDps(p, m);
            mutate(p);
            float ds = ScoreOf(p) - s0;
            float dd = (ExpectedDps(p, m) - d0) / d0 * 100f;
            sb.Append($"{label,-19} {unit,6}  {ds,9:0.0}   {dd,9:0.000}%   {dd / ds,12:0.000}%\n");
        }

        /// <summary>当前角色两件装备的评分合计（新口径：对标准怪的期望 DPS + 有效生命 ÷ EquipmentScore.SurvivabilityDivisor）。</summary>
        static float ScoreOf(PlayerData p) =>
            EquipmentScore.Of(p, p.weapon, EquipmentSlot.Weapon) + EquipmentScore.Of(p, p.armor, EquipmentSlot.Armor);

        /// <summary>
        /// 理论期望 DPS。实现只有一份——`EquipmentScore.Dps`，
        /// 这里保留薄封装只是为了不惊动本文件里已有的调用点。
        /// </summary>
        static float ExpectedDps(PlayerData p, MonsterData m) => EquipmentScore.Dps(p, m);

        /// <summary>
        /// 评分排序 vs 实测 DPS 排序的一致性检验：随机生成一批传说武器，
        /// 分别按装备评分和理论期望 DPS 排序，看两者是否指向同一把武器。
        /// 单点「每分收益」有差异不代表评分有问题（各词条数值范围不同），排序一致性才是真判据。
        /// </summary>
        public static string ScoreRankAudit(int count, int paragon)
        {
            var m = MonsterData.CreateForLevel(GameConfig.MaxTierLevel, MonsterType.Melee, MonsterTier.Boss, MonsterAffix.None);
            int n = Mathf.Max(5, count);
            var scores = new float[n];
            var dps = new float[n];
            var texts = new string[n];
            for (int i = 0; i < n; i++)
            {
                var p = Build(paragon);
                var w = LootSystem.CreateEquipment(EquipmentSlot.Weapon, 100, EquipmentQuality.Legendary);
                p.weapon = w;
                scores[i] = EquipmentScore.Of(p, w, EquipmentSlot.Weapon) + EquipmentScore.Of(p, p.armor, EquipmentSlot.Armor);
                dps[i] = ExpectedDps(p, m);
                texts[i] = w.StatLine + (string.IsNullOrEmpty(w.AffixLine) ? "" : " | " + w.AffixLine);
            }

            int[] byScore = RankDesc(scores);
            int[] byDps = RankDesc(dps);
            var rankScore = new int[n];
            var rankDps = new int[n];
            for (int r = 0; r < n; r++) { rankScore[byScore[r]] = r + 1; rankDps[byDps[r]] = r + 1; }
            double d2 = 0;
            for (int i = 0; i < n; i++) { double d = rankScore[i] - rankDps[i]; d2 += d * d; }
            double rho = 1 - 6 * d2 / ((double)n * (n * n - 1));

            var sb = new StringBuilder();
            sb.Append($"随机 {n} 把 Lv.100 传说武器（同一件传说衣服）；Spearman ρ = {rho:0.000}（1=排序完全一致，0=无关）\n\n");
            sb.Append("— 按装备评分排序前 3 —\n");
            for (int r = 0; r < 3; r++)
            {
                int i = byScore[r];
                sb.Append($"  #{r + 1} 评分 {scores[i],5:0}（DPS 第 {rankDps[i],2}/{n}，{dps[i]:0}）  {texts[i]}\n");
            }
            sb.Append("\n— 按期望 DPS 排序前 3 —\n");
            for (int r = 0; r < 3; r++)
            {
                int i = byDps[r];
                sb.Append($"  #{r + 1} DPS {dps[i],5:0}（评分第 {rankScore[i],2}/{n}，{scores[i]:0}）  {texts[i]}\n");
            }
            sb.Append($"\n评分第 1 与 DPS 第 1 是否同一把：{(byScore[0] == byDps[0] ? "是" : "否")}\n");
            return sb.ToString();
        }

        /// <summary>返回按值降序排列的下标数组（不依赖 Linq）。</summary>
        static int[] RankDesc(float[] v)
        {
            int n = v.Length;
            var idx = new int[n];
            for (int i = 0; i < n; i++) idx[i] = i;
            for (int i = 0; i < n - 1; i++)
            {
                for (int j = i + 1; j < n; j++)
                {
                    if (v[idx[j]] > v[idx[i]]) { int t = idx[i]; idx[i] = idx[j]; idx[j] = t; }
                }
            }
            return idx;
        }

        /// <summary>
        /// 评分 vs 实战排序：同一套技能、同一件衣服，只换武器，实际打 Lv.110 弓箭手 Boss 的耗时，
        /// 换算成「越快越强」的指标再和装备评分比排序一致性。比理论 DPS 更接近玩家体感。
        /// </summary>
        public static string ScoreBattleAudit(int count, int paragon, int repeats)
        {
            var tpl = Build(paragon);
            int n = Mathf.Max(5, count);
            int slots = GameConfig.SkillSlotsPerKind * 3;
            var scores = new float[n];
            var speed = new float[n];
            var texts = new string[n];
            for (int i = 0; i < n; i++)
            {
                var p = Build(paragon);
                for (int s = 0; s < slots; s++) p.SetSkillSlot(s, tpl.GetSkillSlot(s));
                var w = LootSystem.CreateEquipment(EquipmentSlot.Weapon, 100, EquipmentQuality.Legendary);
                p.weapon = w;
                scores[i] = EquipmentScore.Of(p, w, EquipmentSlot.Weapon) + EquipmentScore.Of(p, p.armor, EquipmentSlot.Armor);
                texts[i] = w.StatLine;

                float sum = 0f;
                int ok = 0;
                for (int r = 0; r < Mathf.Max(1, repeats); r++)
                {
                    var b = new BattleSystem();
                    var m = MonsterData.CreateForLevel(GameConfig.MaxTierLevel, MonsterType.Ranged, MonsterTier.Boss, MonsterAffix.None);
                    p.FillHp();
                    b.BeginBattle(p);
                    var log = new BattleLog();
                    float t = 0f;
                    for (int s = 0; s < 150000; s++)
                    {
                        t += 0.05f;
                        var res = b.Tick(p, m, 0.05f, log);
                        log.Clear();
                        if (res == BattleTickResult.MonsterKilled) { sum += t; ok++; break; }
                        if (res == BattleTickResult.PlayerDied) break;
                    }
                }
                speed[i] = ok == 0 ? -1f : -sum / ok;   // 用时越短 → 值越大
            }

            int[] byScore = RankDesc(scores);
            int[] bySpeed = RankDesc(speed);
            var rankScore = new int[n];
            var rankSpeed = new int[n];
            for (int r = 0; r < n; r++) { rankScore[byScore[r]] = r + 1; rankSpeed[bySpeed[r]] = r + 1; }
            double d2 = 0;
            for (int i = 0; i < n; i++) { double d = rankScore[i] - rankSpeed[i]; d2 += d * d; }
            double rho = 1 - 6 * d2 / ((double)n * (n * n - 1));

            var sb = new StringBuilder();
            sb.Append($"随机 {n} 把 Lv.100 传说武器（同一套技能 + 同一件衣服，对手 Lv.110 弓箭手 Boss，每把打 {repeats} 场取平均）\n");
            sb.Append($"Spearman ρ(评分, 实战速度) = {rho:0.000}\n\n");
            sb.Append("— 按评分排序前 3 —\n");
            for (int r = 0; r < 3; r++)
            {
                int i = byScore[r];
                sb.Append($"  #{r + 1} 评分 {scores[i],5:0}（实战第 {rankSpeed[i],2}/{n}，用时 {TimeText(speed[i])}）  {texts[i]}\n");
            }
            sb.Append("\n— 按实战速度排序前 3 —\n");
            for (int r = 0; r < 3; r++)
            {
                int i = bySpeed[r];
                sb.Append($"  #{r + 1} 用时 {TimeText(speed[i])}（评分第 {rankScore[i],2}/{n}，{scores[i]:0}）  {texts[i]}\n");
            }
            return sb.ToString();
        }

        /// <summary>
        /// 评分 vs 木桩实战排序：只换武器、同一套技能，主角全程不死，测固定时长内对 Lv.110 Boss 打出的总伤害。
        /// 战斗通常两三个回合就结束，「击杀用时」没有区分度，木桩总伤害才能把输出差距拉开。
        /// </summary>
        public static string ScoreDummyAudit(int count, int paragon, float seconds, int repeats)
        {
            var tpl = Build(paragon);
            int n = Mathf.Max(5, count);
            int slots = GameConfig.SkillSlotsPerKind * 3;
            var scores = new float[n];
            var dmg = new float[n];
            var texts = new string[n];
            for (int i = 0; i < n; i++)
            {
                var p = Build(paragon);
                for (int s = 0; s < slots; s++) p.SetSkillSlot(s, tpl.GetSkillSlot(s));
                var w = LootSystem.CreateEquipment(EquipmentSlot.Weapon, 100, EquipmentQuality.Legendary);
                p.weapon = w;
                scores[i] = EquipmentScore.Of(p, w, EquipmentSlot.Weapon) + EquipmentScore.Of(p, p.armor, EquipmentSlot.Armor);
                texts[i] = w.StatLine + (string.IsNullOrEmpty(w.AffixLine) ? "" : " | " + w.AffixLine);

                float acc = 0f;
                for (int r = 0; r < Mathf.Max(1, repeats); r++) acc += DummyDamage(p, seconds);
                dmg[i] = acc / Mathf.Max(1, repeats);
            }

            int[] byScore = RankDesc(scores);
            int[] byDmg = RankDesc(dmg);
            var rankScore = new int[n];
            var rankDmg = new int[n];
            for (int r = 0; r < n; r++) { rankScore[byScore[r]] = r + 1; rankDmg[byDmg[r]] = r + 1; }
            double d2 = 0;
            for (int i = 0; i < n; i++) { double d = rankScore[i] - rankDmg[i]; d2 += d * d; }
            double rho = 1 - 6 * d2 / ((double)n * (n * n - 1));

            var sb = new StringBuilder();
            sb.Append($"随机 {n} 把 Lv.100 传说武器（同一套技能 + 同一件衣服，木桩 Lv.110 Boss，{seconds:0} 秒 × {repeats} 次取平均）\n");
            sb.Append($"Spearman ρ(评分, 木桩总伤害) = {rho:0.000}\n\n");
            sb.Append("— 按评分排序前 3 —\n");
            for (int r = 0; r < 3; r++)
            {
                int i = byScore[r];
                sb.Append($"  #{r + 1} 评分 {scores[i],5:0}（伤害第 {rankDmg[i],2}/{n}，{dmg[i]:0}）  {texts[i]}\n");
            }
            sb.Append("\n— 按木桩总伤害排序前 3 —\n");
            for (int r = 0; r < 3; r++)
            {
                int i = byDmg[r];
                sb.Append($"  #{r + 1} 伤害 {dmg[i],6:0}（评分第 {rankScore[i],2}/{n}，{scores[i]:0}）  {texts[i]}\n");
            }
            return sb.ToString();
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

        /// <summary>
        /// 权重标定：随机生成样本（记录各评分项 + 木桩实测伤害），网格搜索「暴击率 / 暴击伤害」权重，
        /// 找出与实战排序最一致的组合。其余权重保持现状，方便直接替换。
        /// </summary>
        public static string ScoreWeightSearch(int count, int paragon, float seconds)
        {
            var tpl = Build(paragon);
            int n = Mathf.Max(10, count);
            int slots = GameConfig.SkillSlotsPerKind * 3;
            var refMonster = MonsterData.CreateForLevel(GameConfig.MaxTierLevel, MonsterType.Melee, MonsterTier.Boss, MonsterAffix.None);
            var dmg = new float[n];
            var atk = new float[n];
            var cch = new float[n];
            var cdm = new float[n];
            var asp = new float[n];
            var all = new float[n];
            var pen = new float[n];
            var theory = new float[n];
            for (int i = 0; i < n; i++)
            {
                var p = Build(paragon);
                for (int s = 0; s < slots; s++) p.SetSkillSlot(s, tpl.GetSkillSlot(s));
                var w = LootSystem.CreateEquipment(EquipmentSlot.Weapon, 100, EquipmentQuality.Legendary);
                p.weapon = w;
                atk[i] = w.attack;
                cch[i] = w.critChance + w.affixCritChance;
                cdm[i] = w.critDamage + w.affixCritDamage;
                asp[i] = w.affixAttackSpeed;
                all[i] = w.affixAllStats;
                pen[i] = w.affixArmorPen;
                dmg[i] = DummyDamage(p, seconds);
                theory[i] = ExpectedDps(p, refMonster);
            }
            int[] byDmg = RankDesc(dmg);
            var rank = new int[n];
            for (int r = 0; r < n; r++) rank[byDmg[r]] = r + 1;

            var sb = new StringBuilder();
            sb.Append($"样本 {n} 把 Lv.100 传说武器（同一件衣服 / 同一套技能，木桩 {seconds:0}s），以实测伤害排名为基准\n\n");
            var cur = new float[n];
            for (int i = 0; i < n; i++)
            {
                cur[i] = atk[i] * 2f + cch[i] * GameConfig.ScoreWeightCritChance + cdm[i] * GameConfig.ScoreWeightCritDamage
                       + asp[i] * 600f + all[i] * 300f + pen[i] * 800f;
            }
            sb.Append($"现状权重（暴击率 {GameConfig.ScoreWeightCritChance:0} / 暴伤 {GameConfig.ScoreWeightCritDamage:0}）：ρ = {RhoRank(n, rank, cur):0.000}\n");
            sb.Append($"理论期望 DPS（含角色全部属性）当评分：ρ = {RhoRank(n, rank, theory):0.000}  ← 上限参考\n\n");

            float bestC = 0f, bestD = 0f;
            double best = -2;
            var hits = new List<string>();
            for (int wc = 100; wc <= 1200; wc += 100)
            {
                for (int wd = 20; wd <= 400; wd += 20)
                {
                    var s = new float[n];
                    for (int i = 0; i < n; i++)
                    {
                        s[i] = atk[i] * 2f + cch[i] * wc + cdm[i] * wd + asp[i] * 600f + all[i] * 300f + pen[i] * 800f;
                    }
                    double rho = RhoRank(n, rank, s);
                    if (rho > best) { best = rho; bestC = wc; bestD = wd; }
                    if (rho >= 0.8) hits.Add($"    ρ={rho:0.000}  暴击率 {wc} / 暴伤 {wd}");
                }
            }
            sb.Append($"最优（其余权重不动）：暴击率 {bestC:0} / 暴伤 {bestD:0} → ρ = {best:0.000}\n");
            if (hits.Count > 0)
            {
                sb.Append($"ρ ≥ 0.8 的组合共 {hits.Count} 个，前 8 个：\n");
                for (int i = 0; i < Mathf.Min(8, hits.Count); i++) sb.Append(hits[i] + "\n");
            }
            return sb.ToString();
        }

        /// <summary>按 values 降序排名与给定参考排名做 Spearman 相关。</summary>
        static double RhoRank(int n, int[] rank, float[] values)
        {
            int[] byV = RankDesc(values);
            double d2 = 0;
            for (int r = 0; r < n; r++)
            {
                double d = (r + 1) - rank[byV[r]];
                d2 += d * d;
            }
            return 1 - 6 * d2 / ((double)n * (n * n - 1));
        }

        static string TimeText(float speed) => speed < 0f ? "战败" : (-speed).ToString("0.0") + "s";

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
            var w = LootSystem.CreateEquipment(EquipmentSlot.Weapon, 100, EquipmentQuality.Legendary);
            var a = LootSystem.CreateEquipment(EquipmentSlot.Armor, 100, EquipmentQuality.Legendary);

            PlayerData Template()
            {
                var p = new PlayerData();
                p.level = 100;
                p.paragonLevel = paragon;
                p.paragonExp = 0;
                p.weapon = w;
                p.armor = a;
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
            var a = LootSystem.CreateEquipment(EquipmentSlot.Armor, 100, EquipmentQuality.Legendary);
            var w = LootSystem.CreateEquipment(EquipmentSlot.Weapon, 100, EquipmentQuality.Legendary);
            // 带武器时把怪物血量放大，保证怪物打不死，主角只能靠自己活下来。
            float hpScale = withWeapon ? 500f : 1f;

            PlayerData Template()
            {
                var p = new PlayerData();
                p.level = 100;
                p.paragonLevel = paragon;
                p.paragonExp = 0;
                p.weapon = withWeapon ? w : null;   // 不带武器时输出可忽略
                p.armor = a;
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
            sb.Append($"生存口径：Lv.100 传说衣服、{(withWeapon ? "**带武器**（输出全开，怪物血量 ×500 保证打不死）" : "**无武器**（输出压到接近 0）")}+ 巅峰 {paragon}（技能固定值），对手 Lv.{monsterLevel} Boss，{trials} 次取平均\n");
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
            var w = LootSystem.CreateEquipment(EquipmentSlot.Weapon, 100, EquipmentQuality.Legendary);
            var a = LootSystem.CreateEquipment(EquipmentSlot.Armor, 100, EquipmentQuality.Legendary);

            PlayerData Template(params SkillId[] ids)
            {
                var p = new PlayerData();
                p.level = 100;
                p.paragonLevel = paragon;
                p.paragonExp = 0;
                p.weapon = w;
                p.armor = a;
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
    }
}

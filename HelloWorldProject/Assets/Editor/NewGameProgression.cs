using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AutoBattle.EditorTools
{
    /// <summary>
    /// 新档进度探针：从 new PlayerData()（Lv.1、无装备、无金币）起，按关卡顺序一路打到底，
    /// 用真实战斗 / 经验 / 掉落 / 药剂规则跑，回答一个 LateGameProbe 回答不了的问题：
    /// **一个刚开档的玩家，能不能靠正常推进走到终局？**
    ///
    /// 为什么必须单独写一个：
    /// LateGameProbe 的各个口径都是「稳态假设」——BuildForChapter 直接把等级设成该章怪级、
    /// 装备按精英保底权重白送、巅峰技能按最优挑。它测的是「如果玩家已经站在那个位置」，而不是
    /// 「玩家能不能走到那个位置」。新档从 Lv.1 打第 1 章 Lv.10 怪这种结构性死锁，那套口径永远测不出来。
    ///
    /// 口径取舍：
    /// - 普通怪按连战算（血量继承、喝药消耗真实库存）；关底 Boss 开战前回满血，
    ///   精英默认不回（与 GameConfig.HealBeforeFight / SpawnMonsterNow 一致）。
    /// - 药剂只在「开商店」时补充（与 AutoFillPotions 一致），且只买得起才买。
    /// - 掉落、卖出、自动换装、首通金币全部走真实代码路径，不另写简化公式。
    /// - 阵亡不给经验（2026-10-18 改）：本关击杀数归零、复活满血、回去刷本关小怪，
    ///   经验只能从活着的怪身上拿。终局 BOSS 的类型按尝试次数轮换。
    ///
    /// 现在它不只是个手动探针：Summary() 的逐章轨迹进了 _balance/baseline.json 的 transition 小节，
    /// BalanceRegression 的静态哨兵每次都会比对，CheckInvariants() 的硬约束（不卡死、每章末等级、
    /// 终局等级、阵亡上限）与基线无关，任何一次运行都必须满足。
    /// </summary>
    public static class NewGameProgression
    {
        /// <summary>单关的模拟结果。</summary>
        public class StageLog
        {
            public int chapter;
            public int stage;
            public int monsterLevel;
            public int attempts;
            public int deaths;
            public float seconds;
            public int levelStart;
            public int levelEnd;
            public bool cleared;
        }

        public class SimResult
        {
            public readonly List<StageLog> stages = new List<StageLog>();
            public int level;
            public int paragonLevel;
            public int gold;
            public int deaths;
            public int kills;
            public int reforgeSpent;
            public float seconds;
            public bool stuck;
            public string stuckReason = "";
            public int stuckChapter;
            public int stuckStage;

            /// <summary>走到了终局（11-10）并进入「刷小怪 → 打 BOSS」的循环。
            /// 这一步不是卡死：每轮小怪都给经验，巅峰会一直涨，只是要磨很久。</summary>
            public bool reachedEndgame;
        }

        [MenuItem("Tools/测试/新档进度探针（从头打到底）")]
        static void MenuRun() => Debug.Log("[NewGameProgression]\n" + Run(12345, 30));

        /// <summary>
        /// 跑一遍完整新档。返回逐章报告。
        /// </summary>
        /// <param name="seed">随机种子；同种子结果可复现（掉落、怪物类型、词缀、伤害浮动）。</param>
        /// <param name="maxAttemptsPerStage">单关最多重试几次；超过就判定为卡死并停止整轮。</param>
        /// <param name="verbose">true 时逐关输出一行。</param>
        /// <param name="reforgeQuality">关底前把词条洗到多少质量；0 = 不洗练，用于对照。</param>
        public static string Run(int seed = 12345, int maxAttemptsPerStage = 30, bool verbose = false,
            float reforgeQuality = ProbePolicy.DefaultReforgeQuality)
        {
            SimResult r = Simulate(seed, maxAttemptsPerStage, verbose, reforgeQuality, out string detail);
            var sb = new StringBuilder();
            sb.AppendLine($"[NewGameProgression] seed={seed}  单关上限 {maxAttemptsPerStage} 次尝试");
            sb.AppendLine("口径：真实 BattleSystem + 真实经验/掉落/药剂；普通怪连战、关底回满血、精英不回；阵亡不给经验；终局 BOSS 类型轮换");
            sb.AppendLine();
            sb.Append(detail);
            sb.AppendLine();
            sb.AppendLine($"总击杀 {r.kills}   总阵亡 {r.deaths}   模拟时长 {Minutes(r.seconds)}   结束时 Lv.{r.level} 巅峰 {r.paragonLevel} 金币 {r.gold}   洗练花费 {r.reforgeSpent:N0}");
            if (r.stuck)
            {
                sb.AppendLine($"✖ 卡死：{r.stuckChapter}-{r.stuckStage}（{r.stuckReason}）");
                sb.AppendLine("   说明：该关在允许的重试次数内无法通关，新档在此永久停滞——继续挂机只会重复「刷普通怪→被关底打死→进度归零」。");
            }
            else if (r.reachedEndgame)
            {
                sb.AppendLine($"◎ 到达终局 11-10：{maxAttemptsPerStage} 次尝试内没打下来（巅峰 {r.paragonLevel}）。");
                sb.AppendLine("   这不是卡死：每轮 5 只小怪照常给经验，巅峰会一直涨；终局节奏见 EndgamePacing 探针。");
            }
            else
            {
                sb.AppendLine("✔ 全程通关（1-1 → 11-10）");
            }
            return sb.ToString();
        }

        /// <summary>
        /// 只跑模拟、输出判定行，供平衡回归调用。
        /// 判据不是「某一关打得过」，而是整条过渡曲线成立：不卡死、成长不断档、终局能摸到满级。
        /// </summary>
        public static string RunAssert(int seed = 12345, int maxAttemptsPerStage = 30)
        {
            SimResult r = Simulate(seed, maxAttemptsPerStage, false, ProbePolicy.DefaultReforgeQuality, out _);
            List<string> problems = CheckInvariants(r);
            var sb = new StringBuilder();
            foreach (string p in problems) sb.AppendLine("FAIL  " + p);
            if (problems.Count == 0)
            {
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                    r.reachedEndgame
                        ? "PASS  新档推进到终局 11-10（Lv.{0} 巅峰 {1}，{2}，阵亡 {3} 次）——终局节奏由 EndgamePacing 单独把关"
                        : "PASS  新档 1-1 → 11-10 全程通关（Lv.{0} 巅峰 {1}，{2}，阵亡 {3} 次）",
                    r.level, r.paragonLevel, Minutes(r.seconds), r.deaths));
            }
            return sb.ToString();
        }

        /// <summary>
        /// 只取过渡轨迹（每章一行 + 收尾一行），供 BalanceRegression 与命令行查看。
        /// </summary>
        public static string[] Trace(int seed = 12345, int maxAttemptsPerStage = 30)
        {
            SimResult r = Simulate(seed, maxAttemptsPerStage, false, ProbePolicy.DefaultReforgeQuality, out _);
            return Summary(r);
        }

        /// <summary>
        /// 过渡硬约束 —— 与基线无关，任何一次模拟都必须满足。
        /// 这些就是 LateGameProbe 那套「稳态口径」结构上测不出来的东西：
        /// 稳态假设玩家已经站在第 c 章、等级 = 10c、装备白送；而真实玩家是**一路走过去的**。
        /// </summary>
        public static List<string> CheckInvariants(SimResult r)
        {
            var problems = new List<string>();

            if (r.stuck)
                problems.Add($"卡在 {r.stuckChapter}-{r.stuckStage}（{r.stuckReason}）");

            if (!r.stuck && r.level < GameConfig.MaxLevel - 5)
                problems.Add(string.Format(CultureInfo.InvariantCulture,
                    "终局只有 Lv.{0}（要求 ≥ Lv.{1}）：满级、巅峰、15 张技能卡全都摸不到",
                    r.level, GameConfig.MaxLevel - 5));

            // 每章末至少要够到「下一章开局怪级」的底线。低于它意味着这一章是靠残血硬磨过去的，
            // 下一章必然崩——这正是「只测稳态」看不到的断档。
            for (int c = 1; c <= GameConfig.ChapterCount; c++)
            {
                StageLog last = null;
                foreach (StageLog st in r.stages)
                {
                    if (st.chapter != c) continue;
                    if (!st.cleared) { last = null; break; }
                    last = st;
                }
                if (last == null) break;   // 卡死已单独报过，后面的章没数据
                int need = Mathf.Min(GameConfig.ChapterMonsterLevelStep * (c - 1) + 1, GameConfig.MaxLevel);
                if (last.levelEnd < need)
                    problems.Add(string.Format(CultureInfo.InvariantCulture,
                        "第 {0} 章末只有 Lv.{1}，低于下一章怪级下限 Lv.{2}（成长断档）", c, last.levelEnd, need));
            }

            if (r.deaths > 120)
                problems.Add(string.Format(CultureInfo.InvariantCulture,
                    "全程阵亡 {0} 次（上限 120）：关卡在靠死亡次数堆运气", r.deaths));

            if (!r.stuck && r.seconds > 45f * 60f)
                problems.Add(string.Format(CultureInfo.InvariantCulture,
                    "全程模拟时长 {0:0.0}min（上限 45min）：推进过于拖沓", r.seconds / 60f));

            return problems;
        }

        /// <summary>
        /// 把一次模拟压成可入库的过渡轨迹：每章一行 + 收尾一行（卡死时多一行卡点）。
        /// 全整数，不受区域设置影响，可直接进 baseline.json 做漂移比对。
        /// </summary>
        public static string[] Summary(SimResult r)
        {
            var lines = new List<string>();
            for (int c = 1; c <= GameConfig.ChapterCount; c++)
            {
                int attempts = 0, deaths = 0, seconds = 0, endLevel = -1;
                foreach (StageLog st in r.stages)
                {
                    if (st.chapter != c) continue;
                    attempts += st.attempts;
                    deaths += st.deaths;
                    seconds += Mathf.RoundToInt(st.seconds);
                    if (!st.cleared) break;
                    endLevel = st.levelEnd;
                }
                if (endLevel < 0) break;
                lines.Add(string.Format(CultureInfo.InvariantCulture,
                    "ch{0}|cleared=1|endLevel={1}|attempts={2}|deaths={3}|seconds={4}",
                    c, endLevel, attempts, deaths, seconds));
            }

            lines.Add(string.Format(CultureInfo.InvariantCulture,
                "end|stuck={0}|endgame={1}|level={2}|paragon={3}|kills={4}|deaths={5}",
                r.stuck ? 1 : 0, r.reachedEndgame ? 1 : 0, r.level, r.paragonLevel, r.kills, r.deaths));

            if (r.reachedEndgame)
                lines.Add(string.Format(CultureInfo.InvariantCulture,
                    "endgame|chapter={0}|stage={1}|paragon={2}|attempts={3}",
                    GameConfig.ChapterCount, GameConfig.StagesPerChapter, r.paragonLevel,
                    r.stages.Count > 0 ? r.stages[r.stages.Count - 1].attempts : 0));

            if (r.stuck)
                lines.Add(string.Format(CultureInfo.InvariantCulture,
                    "stuck|chapter={0}|stage={1}", r.stuckChapter, r.stuckStage));

            return lines.ToArray();
        }

        static SimResult Simulate(int seed, int maxAttemptsPerStage, bool verbose, float reforgeQuality, out string detail)
        {
            Random.InitState(seed);

            var p = new PlayerData();
            p.FillHp();

            var battle = new BattleSystem();
            var r = new SimResult();
            var sb = new StringBuilder();
            sb.AppendLine($"起始：Lv.{p.level}  攻 {p.Attack}  防 {p.Defense}  血 {p.MaxHp}  金币 {p.gold}  药剂 {p.potionCount}");

            int maxAttempts = Mathf.Max(1, maxAttemptsPerStage);

            for (int c = 1; c <= GameConfig.ChapterCount && !r.stuck; c++)
            {
                int chapterStartLevel = p.level;
                int chapterDeaths = 0;
                int chapterAttempts = 0;
                float chapterSeconds = 0f;
                int worstStage = 0;
                int worstAttempts = 0;

                for (int s = 1; s <= GameConfig.StagesPerChapter && !r.stuck; s++)
                {
                    StageDef def = StageCatalog.Get(c, s);
                    p.EnterStage(c, s);

                    var st = new StageLog
                    {
                        chapter = c,
                        stage = s,
                        monsterLevel = def.monsterLevel,
                        levelStart = p.level,
                    };

                    int attempts = 0;
                    bool cleared = false;
                    MonsterTier diedOn = def.clearTier;

                    while (!cleared && attempts < maxAttempts)
                    {
                        attempts++;

                        // 进关先花钱：真人打不过时第一反应就是把金币砸进强化。
                        // 改动前探针的金币只用来买药，攒到最后变成一串没意义的数字。
                        ProbePolicy.SpendGoldOnEnhance(p);

                        bool died = false;

                        // ---- 前 K-1 只：普通怪，血量继承（不重置） ----
                        for (int k = 0; k < def.killTarget - 1; k++)
                        {
                            MonsterData m = Spawn(def.monsterLevel, MonsterTier.Normal);
                            bool win = Fight(p, m, battle, false, out float sec, out int dealt);
                            st.seconds += sec;
                            if (!win)
                            {
                                died = true; diedOn = MonsterTier.Normal;
                                // 阵亡不给经验（与 GameController.HandlePlayerDied 一致）：
                                // 要经验只能回去刷小怪，送死不是成长路径。
                                break;
                            }

                            r.kills++;
                            GrantKill(p, m);
                        }

                        if (died)
                        {
                            r.deaths++; st.deaths++; chapterDeaths++;
                            p.FillHp();          // 复活
                            continue;
                        }

                        // ---- 关底：精英 / Boss ----
                        FillPotions(p);          // 开商店时自动补药
                        // 与真人一致：有余钱就把拖后腿的词条洗到 80%，再打关底。
                        r.reforgeSpent += ProbePolicy.SpendGoldOnReforge(p, reforgeQuality);
                        // 与 SpawnMonsterNow 一致：默认不回血（HealBeforeFight=false），整关是一条消耗链
                        if (def.clearTier == MonsterTier.Boss || GameConfig.HealBeforeFight) p.FillHp();

                        MonsterData boss = Spawn(def.monsterLevel, def.clearTier, BossTypeFor(c, s, attempts));
                        bool won = Fight(p, boss, battle, true, out float sec2, out int dealtBoss);
                        st.seconds += sec2;

                        if (!won)
                        {
                            r.deaths++; st.deaths++; chapterDeaths++;
                            diedOn = def.clearTier;
                            continue;
                        }

                        r.kills++;
                        GrantKill(p, boss);
                        cleared = true;
                    }

                    st.attempts = attempts;
                    st.cleared = cleared;
                    st.levelEnd = p.level;
                    r.stages.Add(st);
                    chapterAttempts += attempts;
                    chapterSeconds += st.seconds;
                    if (attempts > worstAttempts) { worstAttempts = attempts; worstStage = s; }

                    if (verbose)
                    {
                        sb.AppendLine($"  {c}-{s}  M={def.monsterLevel,3}  {TierName(def.clearTier),-4} 尝试 {attempts,2}  阵亡 {st.deaths,2}  Lv.{st.levelStart}→{st.levelEnd}  {Minutes(st.seconds)}  Q={(int)(p.weapon?.quality ?? 0)}/{(int)(p.armor?.quality ?? 0)}  HP={p.MaxHp} 攻={p.Attack}");
                    }

                    if (!cleared)
                    {
                        bool finalStage = c >= GameConfig.ChapterCount && s >= GameConfig.StagesPerChapter;
                        if (finalStage)
                        {
                            // 终局不是死路：每轮 5 只小怪都给经验，巅峰会一直涨，
                            // 只是 maxAttempts 次预算内还没打完（真机可以一直刷）。
                            r.reachedEndgame = true;
                        }
                        else
                        {
                            r.stuck = true;
                            r.stuckChapter = c;
                            r.stuckStage = s;
                            r.stuckReason = $"Lv.{p.level} 打不过 Lv.{def.monsterLevel} 的{TierName(diedOn)}，{maxAttempts} 次重试全部阵亡";
                        }
                        break;
                    }

                    // 首通金币（真实规则：只发一次）
                    if (p.MarkStageFirstCleared(c, s)) p.gold += StageCatalog.FirstClearGold(c, s);
                }

                if (!r.stuck)
                {
                    sb.AppendLine($"第 {c,2} 章  起始 Lv.{chapterStartLevel,2} → 结束 Lv.{p.level,2}  " +
                                  $"尝试 {chapterAttempts,3}  阵亡 {chapterDeaths,2}  {Minutes(chapterSeconds)}  最难关 {c}-{worstStage}");
                }
                else
                {
                    sb.AppendLine($"第 {c,2} 章  起始 Lv.{chapterStartLevel,2} → 卡死 Lv.{p.level,2}  " +
                                  $"尝试 {chapterAttempts,3}  阵亡 {chapterDeaths,2}  {Minutes(chapterSeconds)}");
                }
            }

            r.level = p.level;
            r.paragonLevel = p.paragonLevel;
            r.gold = p.gold;
            r.seconds = 0f;
            foreach (StageLog st in r.stages) r.seconds += st.seconds;

            detail = sb.ToString();
            return r;
        }

        /// <summary>
        /// 打一场。口径与游戏内一致：战前（关底）回满、低血自动喝药、0.05s 步进。
        /// 返回是否击杀；seconds 为这场耗掉的游戏秒数（阵亡时=撑到的秒数）；
        /// dealt 为对这只怪最终打掉的血量（阵亡结算经验要用）。
        /// </summary>
        internal static bool Fight(PlayerData p, MonsterData m, BattleSystem battle, bool resetHp, out float seconds, out int dealt)
        {
            seconds = 0f;
            dealt = 0;
            if (resetHp) p.FillHp();
            battle.BeginBattle(p);

            var log = new BattleLog();
            float cd = 0f;

            for (int s = 0; s < 150000; s++)
            {
                if (cd > 0f) cd -= 0.05f;
                if (cd <= 0f && p.potionCount > 0 && p.currentHp > 0 &&
                    p.currentHp < p.MaxHp * GameConfig.PotionAutoUseHpPercent)
                {
                    p.potionCount--;
                    p.currentHp = Mathf.Min(p.MaxHp,
                        p.currentHp + Mathf.RoundToInt(p.MaxHp * GameConfig.PotionHealPercent));
                    cd = GameConfig.PotionCooldown;
                }

                BattleTickResult res = battle.Tick(p, m, 0.05f, log);
                log.Clear();

                if (res == BattleTickResult.MonsterKilled) { seconds = s * 0.05f; dealt = m.maxHp; return true; }
                if (res == BattleTickResult.PlayerDied)
                {
                    seconds = s * 0.05f;
                    dealt = Mathf.Clamp(m.maxHp - m.currentHp, 0, m.maxHp);
                    return false;
                }
            }

            seconds = 150000 * 0.05f;
            dealt = Mathf.Clamp(m.maxHp - m.currentHp, 0, m.maxHp);
            return false;
        }

        /// <summary>阵亡结算：按打掉的血量占比折算经验（与 GameController.HandlePlayerDied 同规则）。</summary>
        static void GrantPartialExp(PlayerData p, MonsterData m, int dealt)
        {
            if (m == null || m.maxHp <= 0) return;
            dealt = Mathf.Clamp(dealt, 0, m.maxHp);
            if (dealt <= 0) return;

            int full = GameConfig.MonsterExpReward(m.level, m.type, m.tier);
            int partial = Mathf.RoundToInt(full * (float)dealt / m.maxHp);
            if (partial <= 0) return;

            int levels = p.AddExp(partial);
            if (levels > 0 && p.level < GameConfig.MaxLevel)
                p.currentHp = Mathf.Min(p.MaxHp, p.currentHp + GameConfig.PlayerMaxHpPerLevel * levels);
        }

        /// <summary>击杀结算：经验、按档次回血、掉落（与 GrantKillReward / ResolveDrops 同规则）。</summary>
        internal static void GrantKill(PlayerData p, MonsterData m)
        {
            int reward = GameConfig.MonsterExpReward(m.level, m.type, m.tier);
            int levels = p.AddExp(reward);
            if (levels > 0 && p.level < GameConfig.MaxLevel)
            {
                p.currentHp = Mathf.Min(p.MaxHp, p.currentHp + GameConfig.PlayerMaxHpPerLevel * levels);
            }

            int heal = Mathf.RoundToInt(p.MaxHp * GameConfig.MonsterTierHealOnKillPercent[(int)m.tier]);
            p.currentHp = Mathf.Min(p.MaxHp, p.currentHp + heal);

            ResolveDrops(p, m);

            // 巅峰每 10 级欠玩家一次「三选一」：交给决策层挑最优卡并装槽。
            // 改动前探针没有这一步，等于技能永远不会出现。
            ProbePolicy.ResolvePendingSkills(p);
        }

        /// <summary>掉落判定，规则逐行对齐 GameController.ResolveDrops（:1104-1144）。</summary>
        static void ResolveDrops(PlayerData p, MonsterData m)
        {
            int dropCount = GameConfig.MonsterTierGuaranteedDrops[(int)m.tier];
            if (dropCount == 0 && LootSystem.RollDrop()) dropCount = 1;

            float[] weights = GameConfig.MonsterTierDropQualityWeights[(int)m.tier];
            int dropLevel = m.tier == MonsterTier.Normal ? p.level : m.level;

            for (int i = 0; i < dropCount; i++)
            {
                Equipment dropped = weights != null
                    ? LootSystem.CreateEquipmentAtLevel(dropLevel, weights)
                    : LootSystem.CreateRandomEquipment(dropLevel);

                if (TryAutoEquip(p, dropped)) continue;
                p.gold += GameConfig.SellPrice(dropped.level, dropped.quality);
            }
        }

        /// <summary>自动换装：比较装备评分，更强则替换（与 TryAutoEquip 同规则）。</summary>
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

        /// <summary>自动补药：与 AutoFillPotions（:728-751）同规则，只在开商店时调用。</summary>
        internal static void FillPotions(PlayerData p)
        {
            int guard = 0;
            while (p.potionCount < GameConfig.PotionMaxCount &&
                   p.gold >= GameConfig.PotionPrice(p.level) && guard++ < 999)
            {
                p.gold -= GameConfig.PotionPrice(p.level);
                p.potionCount++;
            }
        }

        /// <summary>造一只怪：类型按关卡怪级抽、非 Boss 随机词缀（与 SpawnMonsterNow 一致）。</summary>
        internal static MonsterData Spawn(int monsterLevel, MonsterTier tier, MonsterType? forcedType = null)
        {
            MonsterType type = forcedType ?? MonsterData.RollType(monsterLevel);
            MonsterAffix affix = tier == MonsterTier.Boss ? MonsterAffix.None : MonsterData.RollAffix();
            return MonsterData.CreateForLevel(monsterLevel, type, tier, affix);
        }

        /// <summary>终局 BOSS 的类型按挑战次数轮换（近战→远程→暴击怪），与 SpawnMonsterNow 一致。</summary>
        static MonsterType? BossTypeFor(int chapter, int stage, int attempt)
        {
            if (chapter < GameConfig.ChapterCount || stage < GameConfig.StagesPerChapter) return null;
            return (MonsterType)((attempt - 1) % 3);
        }

        static string TierName(MonsterTier t) =>
            t == MonsterTier.Boss ? "Boss" : t == MonsterTier.Elite ? "精英" : "普通";

        static string Minutes(float seconds) => (seconds / 60f).ToString("0.0") + "min";
    }
}

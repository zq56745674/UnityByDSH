using System.Collections.Generic;
using UnityEngine;

namespace AutoBattle.EditorTools
{
    /// <summary>
    /// 探针的「玩家决策层」：三选一挑哪张、拿到技能装哪格、金币什么时候拿去强化 / 洗练。
    ///
    /// 为什么单独抽一层：改动前的探针只会拿「候选里的第一张」（等于随机抽卡），
    /// 而且从没有一步走过 GameController.EnhanceEquipment —— 于是模拟出来的是一个
    /// 「技能乱给、金币只买药」的玩家，和真人差得很远，终局节奏与逐章胜率都被低估。
    ///
    /// 口径：
    /// - 技能一律用「实际打一场 Lv.110 BOSS」来评，而不是手写一张强弱表。手写表会随
    ///   平衡漂移而失真（技能数值一改就得重算），实打则自动跟着 BattleSystem 走。
    /// - 评分标准与 LateGameProbe.PickAndEquipBest 一致：赢一场 10 分（+最多 1 分的速度奖励），
    ///   输一场按打掉的血量给 0~1 分。多赢一场的价值远大于快几秒，所以胜率是主序。
    /// - 强化按「每金币换来的评分增量」挑槽位，评分口径与 EquipmentScore 相同
    ///   （期望 DPS + 有效生命 ÷ 8），因此强化的决策和自动换装的决策在同一把尺子上。
    /// - 洗练按「词条质量」做贪心：达到目标质量（默认 80%）就停；每次只洗更差的一件，
    ///   把已经达到目标质量的那几条锁住，避免把好词条洗掉。这个策略与 docs/equipment-reforge.md
    ///   的「80% 是可接受线、90% 是追梦线」一致，也是普通人真会做的操作。
    /// </summary>
    public static class ProbePolicy
    {
        // ==================== 评分口径 ====================

        /// <summary>当前配装的战力：期望 DPS + 有效生命 ÷ SurvivabilityDivisor（与评分面板同式）。</summary>
        public static float LoadoutScore(PlayerData p) =>
            EquipmentScore.Dps(p, EquipmentScore.Standard)
            + EquipmentScore.EffectiveHp(p, EquipmentScore.Standard) / EquipmentScore.SurvivabilityDivisor;

        // ==================== 技能：三选一 ====================

        /// <summary>
        /// 模拟真人玩三选一：对每个候选实打一轮三种 BOSS，取总分最高者。
        /// slot 返回「装到哪格」（-1 表示一个都不装，技能只进背包）。
        ///
        /// 有空槽时只填空槽：这时不需要跟已有技能比，一场就够，决策也便宜。
        /// 同类槽位满了才进入「替换」模式：必须多跑几场压噪声，否则单场胜负会盖过技能差距。
        /// </summary>
        public static SkillId ChooseSkill(PlayerData p, IReadOnlyList<SkillId> candidates, out int slot)
        {
            slot = -1;
            if (candidates == null || candidates.Count == 0) return (SkillId)(-1);

            SkillId best = candidates[0];
            float bestScore = float.MinValue;

            var battle = new BattleSystem();
            int savedHp = p.currentHp;

            bool hasEmpty = false;
            foreach (SkillId id in candidates)
            {
                int baseSlot = (int)SkillCatalog.Get(id).kind * GameConfig.SkillSlotsPerKind;
                for (int i = 0; i < GameConfig.SkillSlotsPerKind; i++)
                    if (p.GetSkillSlot(baseSlot + i) < 0) { hasEmpty = true; break; }
                if (hasEmpty) break;
            }

            int repeats = hasEmpty ? 1 : 3;

            try
            {
                foreach (SkillId id in candidates)
                {
                    int baseSlot = (int)SkillCatalog.Get(id).kind * GameConfig.SkillSlotsPerKind;

                    for (int i = 0; i < GameConfig.SkillSlotsPerKind; i++)
                    {
                        int s = baseSlot + i;
                        int old = p.GetSkillSlot(s);
                        if (old == (int)id) continue;          // 已经装着，不必再试
                        if (hasEmpty && old >= 0) continue;    // 有空槽时不替换

                        p.SetSkillSlot(s, (int)id);
                        float score = TrialScore(p, battle, repeats);
                        p.SetSkillSlot(s, old);                // 先还原，落定放到调用方决定

                        if (score > bestScore) { bestScore = score; best = id; slot = s; }

                        if (old < 0) break;                    // 空槽只需试一次
                    }
                }
            }
            finally
            {
                p.currentHp = savedHp;
            }

            return best;
        }

        /// <summary>
        /// 把一次「三选一」走完：挑一张、授予、装进槽位。返回实际拿到的技能（没有候选时返回 -1）。
        /// 调用方负责决定「什么时候该给一次三选一」（巅峰每 10 级一次）。
        /// </summary>
        public static SkillId ResolveSkillChoice(PlayerData p)
        {
            List<SkillId> candidates = p.RollSkillCandidates(GameConfig.SkillChoiceCount);
            if (candidates.Count == 0) return (SkillId)(-1);

            SkillId picked = ChooseSkill(p, candidates, out int slot);
            if (!p.GrantSkill(picked)) return (SkillId)(-1);

            if (slot >= 0) p.SetSkillSlot(slot, (int)picked);
            else AutoPlace(p, picked);

            return picked;
        }

        /// <summary>没有测试结果时（例如只剩一张候选）按「同类空槽优先」装，与游戏内 TryAutoEquip 一致。</summary>
        static void AutoPlace(PlayerData p, SkillId id)
        {
            SkillDef def = SkillCatalog.Get(id);
            int baseSlot = (int)def.kind * GameConfig.SkillSlotsPerKind;
            for (int i = 0; i < GameConfig.SkillSlotsPerKind; i++)
            {
                if (p.GetSkillSlot(baseSlot + i) < 0) { p.SetSkillSlot(baseSlot + i, (int)id); return; }
            }
        }

        /// <summary>
        /// 补上「欠玩家的三选一」。规则与 GameController.ResolveLevelUp 一致：
        /// 每 ParagonPassiveEvery 级巅峰给一次，最多给到技能总数。
        /// 探针里玩家会立刻选（真机是暂停面板，属游戏外时间），所以这里直接结算。
        /// 返回本次结算了几次。
        /// </summary>
        public static int ResolvePendingSkills(PlayerData p)
        {
            int want = Mathf.Min(p.paragonLevel / GameConfig.ParagonPassiveEvery, SkillCatalog.All.Length);
            int owed = want - p.OwnedSkillCount - p.pendingSkillPicks;
            int done = 0;
            for (int i = 0; i < owed; i++)
            {
                if (ResolveSkillChoice(p) == (SkillId)(-1)) break;
                done++;
            }
            return done;
        }

        /// <summary>
        /// 一场战斗的表现分：三种 BOSS 各打 repeats 场再平均。
        /// 只打一种时，纯输出型技能会被高估，而实战里盗贼 BOSS 恰恰是胜率短板，
        /// 生存型技能必须能在这一项拿到分。
        /// </summary>
        static float TrialScore(PlayerData p, BattleSystem battle, int repeats)
        {
            float sum = 0f;
            for (int t = 0; t < 3; t++)
            {
                for (int r = 0; r < repeats; r++)
                {
                    battle.ResetSkillState();
                    p.FillHp();
                    MonsterData m = MonsterData.CreateForLevel(
                        GameConfig.MaxTierLevel, (MonsterType)t, MonsterTier.Boss, MonsterAffix.None);

                    bool win = FightToEnd(p, m, battle, out float seconds);
                    sum += win ? 10f + 1f / (1f + seconds)
                               : 1f - (float)m.currentHp / Mathf.Max(1, m.maxHp);
                }
            }
            return sum;
        }

        /// <summary>
        /// 试打用的战斗循环：药剂用「无限量」的局部计数器，绝不碰 p.potionCount / p.gold。
        /// 试打是为了做决策，不能顺手把玩家的药喝掉、把金币花掉 —— 那会污染后续模拟。
        /// </summary>
        static bool FightToEnd(PlayerData p, MonsterData m, BattleSystem battle, out float seconds)
        {
            seconds = 0f;
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

                BattleTickResult r = battle.Tick(p, m, 0.05f, log);
                log.Clear();
                if (r == BattleTickResult.MonsterKilled) { seconds = s * 0.05f; return true; }
                if (r == BattleTickResult.PlayerDied) { seconds = s * 0.05f; return false; }
            }

            seconds = 150000 * 0.05f;
            return false;
        }

        // ==================== 金币：强化 ====================

        /// <summary>
        /// 给强化留出的金币：够把药补满就行。真人不会为了强化把药钱花光，
        /// 探针也不该用「不喝药」模拟出一个更弱的玩家。
        /// </summary>
        public static int PotionReserve(PlayerData p) => GameConfig.PotionPrice(p.level) * GameConfig.PotionMaxCount;

        /// <summary>
        /// 把金币花在强化上，直到「再加一级就买不起（留够药钱）」为止。
        /// 每级挑「每金币带来的评分增量」最高的槽位，与自动换装共用同一套评分口径。
        /// 返回共花掉多少金币。
        /// </summary>
        public static int SpendGoldOnEnhance(PlayerData p, int extraReserve = 0)
        {
            int spent = 0;
            int reserve = PotionReserve(p) + extraReserve;

            for (int guard = 0; guard < 64; guard++)
            {
                Equipment bestEquip = null;
                EquipmentSlot bestSlot = EquipmentSlot.Weapon;
                float bestValue = 0f;
                int bestCost = 0;

                foreach (EquipmentSlot slot in new[] { EquipmentSlot.Weapon, EquipmentSlot.Armor })
                {
                    Equipment e = p.GetEquipment(slot);
                    if (e == null || !e.CanEnhance) continue;

                    int cost = e.EnhanceCost;
                    if (p.gold - cost < reserve) continue;

                    float gain = EnhanceGain(p, e);
                    float value = gain / Mathf.Max(1, cost);
                    if (value > bestValue) { bestValue = value; bestEquip = e; bestSlot = slot; bestCost = cost; }
                }

                if (bestEquip == null) break;

                // 与 GameController.EnhanceEquipment 同一步骤：先记旧生命上限，再扣钱、升级、补血。
                int oldMaxHp = p.MaxHp;
                p.gold -= bestCost;
                bestEquip.enhanceLevel++;
                p.currentHp += Mathf.Max(0, p.MaxHp - oldMaxHp);
                spent += bestCost;
            }

            return spent;
        }

        // ==================== 金币：洗练 ====================

        /// <summary>
        /// 探针的洗练停止线：词条质量 80%。docs/equipment-reforge.md 把它定义为
        /// 「可接受线」——再往上每 5% 的价格接近翻倍，长线探针不该假设玩家追到 90%。
        /// </summary>
        public const float DefaultReforgeQuality = 0.80f;

        /// <summary>
        /// 把金币花在洗练上，直到两件装备的词条质量都达到 targetQuality，或金币只剩药钱。
        /// 每次挑「质量更低的那件」洗；洗之前锁住该件里已经达到目标质量的词条，
        /// 所以只会重抽拖后腿的那几条。返回共花掉多少金币。
        ///
        /// 为什么不做穷举：洗练是随机过程，穷举要把每条词条的未来价值都算进去，
        /// 成本远高于收益。这里模拟的是「普通人能做到的贪心」——先锁好的、再洗差的，
        /// 达到 80% 就停，正好对应文档里的性价比拐点。
        /// </summary>
        public static int SpendGoldOnReforge(PlayerData p, float targetQuality = DefaultReforgeQuality,
            int extraReserve = 0, int maxSteps = 128)
        {
            targetQuality = Mathf.Clamp01(targetQuality);
            int spent = 0;
            int reserve = PotionReserve(p) + extraReserve;

            for (int step = 0; step < maxSteps; step++)
            {
                Equipment bestEquip = null;
                int bestMask = 0;
                int bestCost = 0;
                float bestQuality = float.MaxValue;

                foreach (EquipmentSlot slot in new[] { EquipmentSlot.Weapon, EquipmentSlot.Armor })
                {
                    Equipment e = p.GetEquipment(slot);
                    if (e == null || !e.CanReforge) continue;

                    float quality = e.AffixQuality;
                    if (quality >= targetQuality) continue;

                    int mask = LockMaskAt(e, targetQuality);
                    int locked = e.LockedAffixCount(mask);
                    if (locked >= e.AffixCount) continue;

                    int cost = e.ReforgeCostWithLocks(locked);
                    if (p.gold - cost < reserve) continue;

                    if (quality < bestQuality || (Mathf.Approximately(quality, bestQuality) && cost < bestCost))
                    {
                        bestEquip = e;
                        bestMask = mask;
                        bestCost = cost;
                        bestQuality = quality;
                    }
                }

                if (bestEquip == null) break;

                // 与 GameController.ReforgeEquipment 同一步骤：先记旧生命上限，再扣钱、洗练、补血。
                int oldMaxHp = p.MaxHp;
                p.gold -= bestCost;
                LootSystem.ReforgeAffixes(bestEquip, bestMask);
                bestEquip.reforgeCount++;

                int newMaxHp = p.MaxHp;
                p.currentHp = Mathf.Min(newMaxHp, p.currentHp + Mathf.Max(0, newMaxHp - oldMaxHp));
                spent += bestCost;
            }

            return spent;
        }

        /// <summary>把「当前已达到目标质量」的词条锁住；没有词条或全都没达标时返回 0。</summary>
        static int LockMaskAt(Equipment e, float targetQuality)
        {
            int mask = 0;
            float levelScale = 1f + e.level * GameConfig.EquipAffixLevelScale;
            for (int i = 0; i <= (int)EquipmentAffix.ArmorPen; i++)
            {
                EquipmentAffix kind = (EquipmentAffix)i;
                float q = AffixPercent(e, kind, levelScale);
                if (q >= targetQuality) mask |= Equipment.AffixBit(kind);
            }
            return mask;
        }

        /// <summary>单条词条的百分位；没有这条词条时返回 0。算法与 Equipment.AffixQuality 一致。</summary>
        static float AffixPercent(Equipment e, EquipmentAffix kind, float levelScale)
        {
            float value = e.AffixValue(kind);
            if (value <= 0f) return 0f;

            int k = (int)kind;
            float min = GameConfig.EquipAffixMin[k];
            float max = GameConfig.EquipAffixMax[k];
            if (max <= min) return 1f;
            return Mathf.Clamp01((value / levelScale - min) / (max - min));
        }

        /// <summary>强化这一件一级能涨多少评分（临时加一级再退回，不改状态）。</summary>
        static float EnhanceGain(PlayerData p, Equipment e)
        {
            if (e == null || !e.CanEnhance) return 0f;

            float before = LoadoutScore(p);
            e.enhanceLevel++;
            float after = LoadoutScore(p);
            e.enhanceLevel--;
            return after - before;
        }
    }
}

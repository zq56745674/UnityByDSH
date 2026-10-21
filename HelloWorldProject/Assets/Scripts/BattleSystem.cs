using System.Collections.Generic;
using UnityEngine;

namespace AutoBattle
{
    /// <summary>一次战斗推进的结果。</summary>
    public enum BattleTickResult
    {
        /// <summary>战斗继续。</summary>
        Ongoing,

        /// <summary>怪物被击杀，可以结算奖励并准备重生。</summary>
        MonsterKilled,

        /// <summary>主角生命归零。</summary>
        PlayerDied,
    }

    /// <summary>
    /// 实时自动战斗（要求 5、6）。主角与怪物各自按「速度换算出的攻击间隔」独立计时，
    /// 谁的计时先到谁先出手，互不等待。
    /// 技能运行状态（冷却 / Buff / 护盾计时）保存在本类里，**跨战斗保留**，
    /// 只有调用 <see cref="ResetSkillState"/> 才清空（新游戏 / 读档 / 重置）。
    /// </summary>
    public class BattleSystem
    {
        /// <summary>主角距离下次攻击的剩余时间。</summary>
        private float _playerAttackTimer;

        /// <summary>本场战斗累计出手次数，用于统计展示。</summary>
        public int PlayerHits { get; private set; }
        public int MonsterHits { get; private set; }

        /// <summary>本场战斗累计暴击次数。</summary>
        public int PlayerCrits { get; private set; }
        public int MonsterCrits { get; private set; }

        // ==================== 技能运行状态 ====================

        /// <summary>
        /// 单次 Tick 最多结算多少次出手。极端 dt（编辑器卡顿、断点恢复、探针直接传大值）
        /// 会让 while 追帧循环跑上很久；超过这个上限就丢弃剩余追帧时间。
        /// 正常帧（60fps，最慢 0.2 秒一次）远达不到这个值。
        /// </summary>
        private const int MaxCatchUpAttacks = 256;

        /// <summary>两个主动技能槽各自的剩余冷却（游戏内秒）。</summary>
        private readonly float[] _activeCooldown = new float[PlayerData.SkillSlotsPerKind];

        /// <summary>主动技能共享的公共冷却剩余。</summary>
        private float _globalCooldown;

        /// <summary>狂暴 Buff 剩余时间。</summary>
        private float _berserkTimer;

        /// <summary>濒死爆发 Buff 剩余时间与它的触发冷却。</summary>
        private float _nearDeathTimer;
        private float _nearDeathCooldown;

        /// <summary>护盾生成的计时。</summary>
        private float _shieldTimer;

        /// <summary>清空全部技能运行状态（新游戏 / 读档 / 重置时调用）。</summary>
        public void ResetSkillState()
        {
            for (int i = 0; i < _activeCooldown.Length; i++) _activeCooldown[i] = 0f;
            _globalCooldown = 0f;
            _berserkTimer = 0f;
            _nearDeathTimer = 0f;
            _nearDeathCooldown = 0f;
            _shieldTimer = 0f;
        }

        /// <summary>供 UI 查询：某个主动槽的剩余冷却与冷却总时长。</summary>
        public float GetActiveCooldown(int slot)
        {
            if (slot < 0 || slot >= _activeCooldown.Length) return 0f;
            return Mathf.Max(0f, _activeCooldown[slot]);
        }

        /// <summary>公共冷却剩余（UI 用）。</summary>
        public float GlobalCooldown => Mathf.Max(0f, _globalCooldown);

        /// <summary>狂暴剩余时间（UI Buff 栏用）。</summary>
        public float BerserkTimeLeft => Mathf.Max(0f, _berserkTimer);

        /// <summary>狂暴的总时长，用来画剩余比例。</summary>
        public float BerserkDuration => SkillCatalog.Get(SkillId.Berserk).duration;

        /// <summary>濒死爆发的免死窗口剩余（UI Buff 栏用）。</summary>
        public float NearDeathTimeLeft => Mathf.Max(0f, _nearDeathTimer);

        /// <summary>濒死爆发的免死总时长。</summary>
        public float NearDeathDuration => SkillCatalog.Get(SkillId.NearDeathBurst).duration;

        /// <summary>护盾生成的下次触发倒计时（UI Buff 栏用）。</summary>
        public float ShieldReadyIn => Mathf.Max(0f, _shieldTimer);

        /// <summary>护盾生成的总冷却。</summary>
        public float ShieldInterval => SkillCatalog.Get(SkillId.ShieldGen).cooldown;

        /// <summary>狂暴 Buff 是否生效中。</summary>
        public bool BerserkActive => _berserkTimer > 0f;

        /// <summary>濒死爆发是否生效中。</summary>
        public bool NearDeathActive => _nearDeathTimer > 0f;

        /// <summary>开始一场新战斗：主角立即进入准备状态。技能冷却跨战斗保留，所以这里不重置。</summary>
        public void BeginBattle(PlayerData player)
        {
            PlayerHits = 0;
            MonsterHits = 0;
            PlayerCrits = 0;
            MonsterCrits = 0;
            _playerAttackTimer = PlayerAttackInterval(player);
        }

        /// <summary>
        /// 主角的实际出手间隔：速度与攻速加成一起进入软上限曲线（见 GameConfig.AttacksPerSecond）。
        /// 攻速乘在线性部分内，超过软上限后只渐近逼近硬上限，不会像旧公式那样直接撞 0.2s。
        /// </summary>
        public static float PlayerAttackInterval(PlayerData player)
        {
            if (player == null) return Mathf.Max(0.01f, GameConfig.MinAttackInterval);
            return GameConfig.AttackInterval(player.Speed, player.AttackSpeedBonus);
        }

        /// <summary>某单位当前的有效攻速（次/秒），UI 与评分共用同一口径。</summary>
        public static float AttacksPerSecond(float speed, float haste = 0f) =>
            GameConfig.AttacksPerSecond(speed, haste);

        /// <summary>
        /// 百分比减伤伤害公式：
        /// 有效防御 = 防御 × (1 - 护甲穿透)，减伤率 = 有效防御 / (有效防御 + 100 + 20 × 防御方等级)；
        /// 基础伤害 = 攻击 × (1 - 减伤率)；
        /// 伤害 = Round(基础伤害 × 随机 0.9~1.1)；
        /// 若暴击，再 Round(伤害 × 暴击伤害倍率)；最后不低于 GameConfig.MinDamage。
        /// </summary>
        public static int RollDamage(int attack, int defense, int defenderLevel,
            float critChance, float critDamage, out bool isCrit, float armorPen = 0f)
        {
            float reduction = GameConfig.DamageReduction(defense, defenderLevel, armorPen);
            float baseDamage = attack * (1f - reduction);

            int damage = Mathf.RoundToInt(
                baseDamage * Random.Range(GameConfig.DamageRandomMin, GameConfig.DamageRandomMax));

            isCrit = Random.value < critChance;
            if (isCrit) damage = Mathf.RoundToInt(damage * critDamage);

            return Mathf.Max(GameConfig.MinDamage, damage);
        }

        /// <summary>把一次攻击按减伤公式折算成实际伤害（技能伤害用，不再随机浮动）。</summary>
        private static int ApplyDefense(int rawAttack, int defense, int defenderLevel, float armorPen = 0f)
        {
            float reduction = GameConfig.DamageReduction(defense, defenderLevel, armorPen);
            return Mathf.Max(GameConfig.MinDamage, Mathf.RoundToInt(rawAttack * (1f - reduction)));
        }

        /// <summary>护盾优先吸收，返回（吸收量, 实际掉血量）。</summary>
        private static void SplitShield(ref int shield, int damage, out int absorbed, out int hpDamage)
        {
            absorbed = Mathf.Min(shield, damage);
            shield -= absorbed;
            hpDamage = damage - absorbed;
        }

        /// <summary>当前有效攻击力（叠加狂暴 / 濒死爆发的百分比加成）。</summary>
        private int EffectiveAttack(PlayerData player)
        {
            float mult = 1f;
            if (_berserkTimer > 0f)
                mult += GameConfig.ScaleSkillValue(SkillCatalog.Get(SkillId.Berserk).value, player.SkillLevel);
            if (_nearDeathTimer > 0f)
                mult += GameConfig.ScaleSkillValue(SkillCatalog.Get(SkillId.NearDeathBurst).value, player.SkillLevel);
            return Mathf.RoundToInt(player.Attack * mult);
        }

        /// <summary>当前有效暴击率（叠加狂暴）。</summary>
        private float EffectiveCritChance(PlayerData player)
        {
            float chance = player.CritChance;
            if (_berserkTimer > 0f)
                chance += GameConfig.ScaleSkillValue(SkillCatalog.Get(SkillId.Berserk).critBonus, player.SkillLevel);
            return Mathf.Min(GameConfig.MaxCritChance, chance);
        }

        /// <summary>巨人杀手：对精英 / Boss 的伤害乘区。</summary>
        private static float GiantSlayerFactor(PlayerData player, MonsterData monster) =>
            monster.tier == MonsterTier.Normal ? 1f : 1f + player.GiantSlayerBonus;

        /// <summary>吸血鬼：把造成伤害的一部分转回主角生命。</summary>
        private static int ApplyLifesteal(PlayerData player, int damageDealt)
        {
            if (damageDealt <= 0 || player.Lifesteal <= 0f) return 0;
            if (player.currentHp <= 0) return 0;

            int before = player.currentHp;
            player.currentHp = Mathf.Min(player.MaxHp,
                player.currentHp + Mathf.RoundToInt(damageDealt * player.Lifesteal));
            return player.currentHp - before;
        }

        /// <summary>
        /// 推进 dt 秒的战斗。dt 应当已经乘过游戏速度倍率。
        /// 顺序：技能计时 → 主动技能释放 → 主角出手 → 怪物出手。
        /// </summary>
        public BattleTickResult Tick(PlayerData player, MonsterData monster, float dt, BattleLog log)
        {
            if (player == null || monster == null) return BattleTickResult.Ongoing;

            TickSkillTimers(player, dt, log);
            TryCastActiveSkills(player, monster, log);

            // ---- 主角出手 ----
            _playerAttackTimer -= dt;
            int catchUp = 0;
            while (_playerAttackTimer <= 0f && !monster.IsDead && catchUp++ < MaxCatchUpAttacks)
            {
                float giant = GiantSlayerFactor(player, monster);
                int attack = EffectiveAttack(player);

                bool crit;
                int dmg = RollDamage(Mathf.RoundToInt(attack * giant), monster.defense, monster.level,
                    EffectiveCritChance(player), player.CritDamage, out crit, player.ArmorPenetration);
                PlayerHits++;
                if (crit) PlayerCrits++;

                // 护盾先扛，剩余部分才进生命。
                int absorbed;
                int hpDamage;
                SplitShield(ref monster.shield, dmg, out absorbed, out hpDamage);
                if (hpDamage > 0) monster.currentHp = Mathf.Max(0, monster.currentHp - hpDamage);

                string shieldNote = absorbed > 0 ? $"  (护盾吸收 {absorbed})" : "";
                string shieldLeft = monster.shield > 0 ? $"  护盾 {monster.shield}" : "";

                log.AddCombat(
                    $"主角 → 怪物  {(crit ? "暴击 " : "")}{dmg} 伤害{shieldNote}   (怪物 {monster.currentHp}/{monster.maxHp}{shieldLeft})",
                    crit ? GameConfig.LogGold : GameConfig.LogInfo);
                GameAudio.PlayHit(crit);

                // 吸血鬼：按实际造成的伤害（含护盾吸收部分）回血。
                int healed = ApplyLifesteal(player, dmg);
                if (healed > 0)
                    log.AddCombat($"✦ 吸血 +{healed}   (主角 {player.currentHp}/{player.MaxHp})",
                        SkillCatalog.Get(SkillId.Vampire).color);

                // 暴击连锁：暴击时按概率追加一次伤害。
                if (crit && !monster.IsDead && player.HasSkill(SkillId.CritChain))
                {
                    SkillDef chain = SkillCatalog.Get(SkillId.CritChain);
                    if (Random.value < GameConfig.ScaleSkillValue(chain.triggerChance, player.SkillLevel))
                        DealSkillDamage(player, monster, giant, chain, attack, log);
                }

                _playerAttackTimer += PlayerAttackInterval(player);
            }

            if (monster.IsDead) return BattleTickResult.MonsterKilled;

            // 追帧上限触发时 timer 仍可能是负数，直接归位，避免下一帧继续补算同一段丢失时间。
            if (_playerAttackTimer <= 0f) _playerAttackTimer = PlayerAttackInterval(player);

            // ---- 怪物出手 ----
            monster.attackTimer -= dt;
            catchUp = 0;
            while (monster.attackTimer <= 0f && !monster.IsDead && player.currentHp > 0
                   && catchUp++ < MaxCatchUpAttacks)
            {
                float giant = GiantSlayerFactor(player, monster);

                bool crit;
                int dmg = RollDamage(monster.attack, player.Defense, player.level,
                    monster.critChance, monster.critDamage, out crit);

                // 不死之身：生命低于阈值时额外减伤。
                bool undying = player.HasSkill(SkillId.Undying)
                               && player.currentHp < player.MaxHp * player.UndyingThreshold;
                if (undying)
                {
                    dmg = Mathf.Max(GameConfig.MinDamage,
                        Mathf.RoundToInt(dmg * (1f - player.UndyingReduction)));
                }

                // 主角护盾先扛。
                int absorbed;
                int hpDamage;
                SplitShield(ref player.shield, dmg, out absorbed, out hpDamage);
                if (hpDamage > 0) player.currentHp = Mathf.Max(0, player.currentHp - hpDamage);
                MonsterHits++;
                if (crit) MonsterCrits++;

                string notes = "";
                if (absorbed > 0) notes += $"  护盾吸收 {absorbed}";
                if (undying) notes += "  不死之身";
                if (player.shield > 0) notes += $"  护盾 {player.shield}";

                // 剧毒：每次攻击附加目标最大生命 2% 的伤害。
                if (monster.affix == MonsterAffix.Poison && player.currentHp > 0)
                {
                    int poison = Mathf.Max(1, Mathf.RoundToInt(player.MaxHp * GameConfig.PoisonAffixPercent));
                    player.currentHp = Mathf.Max(0, player.currentHp - poison);
                    notes += $"  剧毒 {poison}";
                }

                // 吸血：把本次伤害的 30% 转回自身生命。
                if (monster.affix == MonsterAffix.Lifesteal && !monster.IsDead)
                {
                    int before = monster.currentHp;
                    monster.currentHp = Mathf.Min(monster.maxHp,
                        monster.currentHp + Mathf.RoundToInt(dmg * GameConfig.LifestealAffixPercent));
                    int mHealed = monster.currentHp - before;
                    if (mHealed > 0) notes += $"  吸血 +{mHealed}";
                }

                log.AddCombat(
                    $"怪物 → 主角  {(crit ? "暴击 " : "")}{dmg} 伤害{notes}   (主角 {player.currentHp}/{player.MaxHp})",
                    crit ? GameConfig.LogDanger : GameConfig.LogWarn);
                GameAudio.PlayHit(crit);

                // 反击：受击时按概率反弹伤害。
                if (player.HasSkill(SkillId.Counter) && !monster.IsDead && player.currentHp > 0)
                {
                    SkillDef counter = SkillCatalog.Get(SkillId.Counter);
                    if (Random.value < GameConfig.ScaleSkillValue(counter.triggerChance, player.SkillLevel))
                        DealSkillDamage(player, monster, giant, counter, EffectiveAttack(player), log);
                }

                monster.attackTimer += GameConfig.AttackInterval(monster.speed);
            }

            // 追帧上限触发时同样把负计时归位，避免跨帧补算。
            if (monster.attackTimer <= 0f && !monster.IsDead && player.currentHp > 0)
                monster.attackTimer = GameConfig.AttackInterval(monster.speed);

            // 反击可能在本轮怪物出手时直接打死怪物。这里必须立刻结算，
            // 否则 Tick 会先返回 Ongoing，击杀要晚一帧才被 GameController 处理。
            if (monster.IsDead) return BattleTickResult.MonsterKilled;

            // 濒死爆发（触发）：这一击本来会致死时留 1 点血，并进入「5 秒内不会死亡」。
            // 冷却没就绪时，只要还在那 5 秒窗口里同样不会死。
            if (player.currentHp <= 0 && player.HasSkill(SkillId.NearDeathBurst))
            {
                SkillDef nd = SkillCatalog.Get(SkillId.NearDeathBurst);
                if (_nearDeathCooldown <= 0f)
                {
                    _nearDeathTimer = nd.duration;
                    _nearDeathCooldown = nd.cooldown;
                    player.currentHp = 1;

                    int pct = Mathf.RoundToInt(GameConfig.ScaleSkillValue(nd.value, player.SkillLevel) * 100f);
                    log.AddCombat($"✦ 濒死爆发！攻击 +{pct}%，{nd.duration:0} 秒内不会死亡", nd.color);
                }
                else if (_nearDeathTimer > 0f)
                {
                    player.currentHp = 1;
                }
            }

            if (player.currentHp <= 0) return BattleTickResult.PlayerDied;

            return BattleTickResult.Ongoing;
        }

        /// <summary>推进技能冷却与周期性效果（护盾生成、濒死爆发）。</summary>
        private void TickSkillTimers(PlayerData player, float dt, BattleLog log)
        {
            if (_globalCooldown > 0f) _globalCooldown -= dt;
            for (int i = 0; i < _activeCooldown.Length; i++)
                if (_activeCooldown[i] > 0f) _activeCooldown[i] -= dt;

            if (_berserkTimer > 0f) _berserkTimer -= dt;
            if (_nearDeathTimer > 0f) _nearDeathTimer -= dt;
            if (_nearDeathCooldown > 0f) _nearDeathCooldown -= dt;

            // ---- 护盾生成：按 cd 反复刷新护盾（取较大值，不叠加） ----
            // 计时器永远走：以前只在「当前装备了护盾技能」时递减，卸下技能后 timer 冻结，
            // 再装回来会保留一个旧倒计时（甚至立刻触发）。
            if (_shieldTimer > 0f) _shieldTimer = Mathf.Max(0f, _shieldTimer - dt);
            if (player.HasSkill(SkillId.ShieldGen))
            {
                SkillDef def = SkillCatalog.Get(SkillId.ShieldGen);

                if (_shieldTimer <= 0f)
                {
                    int amount = Mathf.RoundToInt(
                        player.MaxHp * GameConfig.ScaleSkillValue(def.value, player.SkillLevel));
                    _shieldTimer = def.cooldown;

                    // cd 很短（0.5 秒）时不重复刷日志，只在护盾真的变厚时记一条。
                    if (amount > player.shield)
                    {
                        player.shield = amount;
                        log.AddCombat($"✦ 护盾生成  +{amount}  (护盾 {player.shield})", def.color);
                    }
                }
            }

            // 濒死爆发改为「受到致死一击时触发」（含 5 秒免死），触发点在主角结算掉血之后，这里不再按血线判断。
        }

        /// <summary>主动技能自动释放：公共冷却结束、该技能冷却结束、满足条件就放一个。</summary>
        private void TryCastActiveSkills(PlayerData player, MonsterData monster, BattleLog log)
        {
            if (_globalCooldown > 0f || monster.IsDead) return;

            for (int slot = 0; slot < _activeCooldown.Length; slot++)
            {
                int id = player.GetSkillSlot(slot); // 槽位 0~1 是主动
                if (id < 0 || id >= SkillCatalog.All.Length) continue;

                SkillDef def = SkillCatalog.All[id];
                if (!def.IsActive) continue;
                if (_activeCooldown[slot] > 0f) continue;

                // 斩杀只在怪物残血时放，否则留着。血线来自技能表（skills.json 可改）。
                if (def.id == SkillId.Execute && monster.currentHp > monster.maxHp * def.hpThreshold)
                    continue;

                CastActive(player, monster, def, slot, log);
                return; // 一次只放一个，其余等公共冷却
            }
        }

        /// <summary>释放一个主动技能。</summary>
        private void CastActive(PlayerData player, MonsterData monster, SkillDef def, int slot, BattleLog log)
        {
            _activeCooldown[slot] = def.cooldown;
            _globalCooldown = GameConfig.SkillGlobalCooldown;

            if (def.id == SkillId.Berserk)
            {
                _berserkTimer = def.duration;

                int atkPct = Mathf.RoundToInt(GameConfig.ScaleSkillValue(def.value, player.SkillLevel) * 100f);
                int critPct = Mathf.RoundToInt(GameConfig.ScaleSkillValue(def.critBonus, player.SkillLevel) * 100f);
                log.AddCombat($"✦ 狂暴！{def.duration:0} 秒内攻击 +{atkPct}%、暴击率 +{critPct}%", def.color);
                return;
            }

            float giant = GiantSlayerFactor(player, monster);
            int hits = Mathf.Max(1, def.hits);

            for (int h = 0; h < hits && !monster.IsDead; h++)
                DealSkillDamage(player, monster, giant, def, EffectiveAttack(player), log);
        }

        /// <summary>
        /// 结算一次技能伤害：伤害 = 有效攻击 × 技能倍率（随等级放大）× 巨人杀手，再走减伤。
        /// 技能伤害不吃暴击（保证倍率可预期）。触发技能的日志文案单独给。
        /// </summary>
        private void DealSkillDamage(PlayerData player, MonsterData monster, float giant,
            SkillDef def, int attack, BattleLog log)
        {
            float mult = GameConfig.ScaleSkillValue(def.value, player.SkillLevel);
            int dmg = ApplyDefense(Mathf.RoundToInt(attack * mult * giant), monster.defense, monster.level,
                player.ArmorPenetration);

            int absorbed;
            int hpDamage;
            SplitShield(ref monster.shield, dmg, out absorbed, out hpDamage);
            if (hpDamage > 0) monster.currentHp = Mathf.Max(0, monster.currentHp - hpDamage);

            string shieldNote = absorbed > 0 ? $"  (护盾吸收 {absorbed})" : "";
            string label = def.name;
            log.AddCombat(
                $"✦ {label}！{dmg} 伤害{shieldNote}   (怪物 {monster.currentHp}/{monster.maxHp})",
                def.color);

            int healed = ApplyLifesteal(player, dmg);
            if (healed > 0)
                log.AddCombat($"✦ 吸血 +{healed}   (主角 {player.currentHp}/{player.MaxHp})",
                    SkillCatalog.Get(SkillId.Vampire).color);
        }
    }
}

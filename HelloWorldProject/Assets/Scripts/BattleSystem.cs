using UnityEngine;

namespace AutoBattle
{
    /// <summary>
    /// 战斗表现事件（P4 舞台用）。战斗只负责「发生了什么」，怎么发光、怎么抖由 UI 决定——
    /// 这样战斗逻辑不需要引用任何 UI 类型，也能被探针脚本无界面地跑。
    /// </summary>
    public enum BattleFx
    {
        /// <summary>主角普通命中。</summary>
        PlayerHit,
        /// <summary>主角暴击。</summary>
        PlayerCrit,
        /// <summary>重击类技能。</summary>
        SkillHeavy,
        /// <summary>旋风斩（旧特效，保留给连锁闪电的跳射）。</summary>
        SkillWhirlwind,
        /// <summary>斩杀 / 崩山类大伤害。</summary>
        SkillExecute,
        /// <summary>反击 / 余震。</summary>
        SkillCounter,
        /// <summary>其他主动技能。</summary>
        SkillOther,
        /// <summary>怪物普通命中。</summary>
        MonsterHit,
        /// <summary>怪物暴击。</summary>
        MonsterCrit,
        /// <summary>吸血回复。</summary>
        Drain,
        /// <summary>护盾生成。</summary>
        Shield,
        /// <summary>剧毒词缀。</summary>
        Poison,
        /// <summary>战吼开启。</summary>
        Berserk,
        /// <summary>电弧 / 连锁闪电。</summary>
        SkillCritChain,
        /// <summary>保命触发。</summary>
        SkillNearDeath,
        /// <summary>减伤类被动（保留）。</summary>
        SkillUndying,
        /// <summary>雷殛 / 大伤害收官。</summary>
        SkillGiantSlayer,
        /// <summary>纯属性被动：山岳之躯。</summary>
        PassiveIronBody,
        /// <summary>纯属性被动：静电共鸣。</summary>
        PassiveBerserkHeart,
        /// <summary>纯属性被动：沉重打击。</summary>
        PassiveLethalStrike,
        /// <summary>纯属性被动：风暴加速。</summary>
        PassiveSwiftness,
    }

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
    ///
    /// 技能 2.0：风暴流的雷印、山崩流的岩层、感电 / 破甲 / 压制 / 护盾计时都保存在本类，
    /// 跨怪物保留冷却，但核心资源（雷印 / 岩层）每只怪重新开始。
    /// </summary>
    public class BattleSystem
    {
        public event System.Action<BattleFx, int> Visual;

        private void RaiseVisual(BattleFx fx, int value = 0)
        {
            System.Action<BattleFx, int> handler = Visual;
            if (handler != null) handler(fx, value);
        }

        /// <summary>主角距离下次攻击的剩余时间。</summary>
        private float _playerAttackTimer;

        public int PlayerHits { get; private set; }
        public int MonsterHits { get; private set; }
        public int PlayerCrits { get; private set; }
        public int MonsterCrits { get; private set; }

        // ==================== 技能运行状态 ====================

        private const int MaxCatchUpAttacks = 256;

        private readonly float[] _activeCooldown = new float[PlayerData.SkillSlotsPerKind];
        private float _globalCooldown;

        // 通用主动
        private float _warCryTimer;
        private float _lastStandTimer;
        private float _lastStandCooldown;
        private float _bulwarkAttackTimer;
        private float _bulwarkShieldTimer;
        private int _bulwarkShieldAmount;

        // 风暴
        private int _stormMarks;
        private float _stormCritMarkIcd;
        private float _rushHasteTimer;
        private int _stormHasteStacks;
        private float _stormHasteTimer;
        private float _shockTimer;
        private int _shockStacks;
        private int _lightningHits;

        // 山崩
        private int _mountainLayers;
        private float _mountainHitIcd;
        private int _mountainAttackCounter;
        private float _armorBreakTimer;
        private float _armorBreakFactor = 1f;
        private float _suppressTimer;
        private float _rockShieldTimer;
        private int _rockShieldAmount;
        private float _rockArmorIcd;

        public int StormMarks => _stormMarks;
        public const int StormMarkMax = 8;
        public int MountainLayers => _mountainLayers;
        public const int MountainLayerMax = 3;
        public int ShockStacks => _shockStacks;
        public float ShockTimeLeft => Mathf.Max(0f, _shockTimer);
        public float ArmorBreakTimeLeft => Mathf.Max(0f, _armorBreakTimer);
        public float SuppressTimeLeft => Mathf.Max(0f, _suppressTimer);
        public bool StormReady => _stormMarks >= StormMarkMax;
        public bool MountainReady => _mountainLayers >= MountainLayerMax;

        public void ResetSkillState()
        {
            _carrySeconds = 0f;
            for (int i = 0; i < _activeCooldown.Length; i++) _activeCooldown[i] = 0f;
            _globalCooldown = 0f;

            _warCryTimer = 0f;
            _lastStandTimer = 0f;
            _lastStandCooldown = 0f;
            _bulwarkAttackTimer = 0f;
            _bulwarkShieldTimer = 0f;
            _bulwarkShieldAmount = 0;

            ResetBattleResources();
        }

        /// <summary>每只怪开打时清空核心资源与 Debuff；主动冷却跨怪保留。</summary>
        private void ResetBattleResources()
        {
            _stormMarks = 0;
            _stormCritMarkIcd = 0f;
            _rushHasteTimer = 0f;
            _stormHasteStacks = 0;
            _stormHasteTimer = 0f;
            _shockTimer = 0f;
            _shockStacks = 0;
            _lightningHits = 0;

            _mountainLayers = 0;
            _mountainHitIcd = 0f;
            _mountainAttackCounter = 0;
            _armorBreakTimer = 0f;
            _armorBreakFactor = 1f;
            _suppressTimer = 0f;
            _rockShieldTimer = 0f;
            _rockShieldAmount = 0;
            _rockArmorIcd = 0f;
        }

        /// <summary>
        /// 技能换槽时把冷却一起搬走：冷却属于技能，不属于槽位。
        /// 装卸/换槽不再调用 ResetSkillState（那会把主动、公共与保命冷却全部清零，
        /// 玩家在暂停的技能面板里反复装卸就能刷新 60 秒一次的保命）。
        /// </summary>
        public void CarryActiveCooldown(int fromSlot, int toSlot)
        {
            if (fromSlot < 0 || toSlot < 0 || fromSlot == toSlot) return;
            if (fromSlot >= _activeCooldown.Length || toSlot >= _activeCooldown.Length) return;
            _activeCooldown[toSlot] = Mathf.Max(_activeCooldown[toSlot], _activeCooldown[fromSlot]);
        }

        public float GetActiveCooldown(int slot)
        {
            if (slot < 0 || slot >= _activeCooldown.Length) return 0f;
            return Mathf.Max(0f, _activeCooldown[slot]);
        }

        public float GlobalCooldown => Mathf.Max(0f, _globalCooldown);

        /// <summary>战吼剩余时间（Buff 栏继续叫「狂暴」的位置，文案已在 UI 改）。</summary>
        public float BerserkTimeLeft => Mathf.Max(0f, _warCryTimer);

        public float BerserkDuration => SkillCatalog.Get(SkillId.WarCry).duration;

        /// <summary>保命的免死窗口剩余。</summary>
        public float NearDeathTimeLeft => Mathf.Max(0f, _lastStandTimer);

        public float NearDeathDuration => SkillCatalog.Get(SkillId.LastStand).duration;

        /// <summary>岩甲的下次可触发倒计时。</summary>
        public float ShieldReadyIn => Mathf.Max(0f, _rockArmorIcd);

        public float ShieldInterval => SkillCatalog.Get(SkillId.RockArmor).value3;

        public bool BerserkActive => _warCryTimer > 0f;

        public bool NearDeathActive => _lastStandTimer > 0f;

        /// <summary>开始一场新战斗：清空本场资源，冷却保留。</summary>
        public void BeginBattle(PlayerData player)
        {
            PlayerHits = 0;
            MonsterHits = 0;
            PlayerCrits = 0;
            MonsterCrits = 0;
            ResetBattleResources();
            _playerAttackTimer = PlayerAttackInterval(player);
        }

        /// <summary>静态口径的出手间隔（UI / 评分用；不含雷印与临时攻速）。</summary>
        public static float PlayerAttackInterval(PlayerData player)
        {
            if (player == null) return Mathf.Max(0.01f, GameConfig.MinAttackInterval);
            return GameConfig.AttackInterval(player.Speed, player.AttackSpeedBonus);
        }

        public static float AttacksPerSecond(float speed, float haste = 0f) =>
            GameConfig.AttacksPerSecond(speed, haste);

        /// <summary>
        /// 当前实际出手间隔：装备攻速 + 雷印 + 风暴加速层数 + 雷霆突袭临时攻速。
        /// 风暴加速每层数值与雷霆突袭攻速都走符文覆写——技能表里的静态值只当默认值。
        /// </summary>
        private float CurrentAttackInterval(PlayerData player)
        {
            float haste = player.AttackSpeedBonus
                        + 0.015f * _stormMarks
                        + player.PassiveValue2(SkillId.StormHaste) * _stormHasteStacks
                        + (_rushHasteTimer > 0f ? RushHaste(player) : 0f);
            return GameConfig.AttackInterval(player.Speed, haste);
        }

        private static float RushHaste(PlayerData player) =>
            RuneMods.Field(player, SkillId.ThunderRush, RuneField.Value3,
                SkillCatalog.Get(SkillId.ThunderRush).value3);

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

        private static int ApplyDefense(int rawAttack, int defense, int defenderLevel, float armorPen = 0f)
        {
            float reduction = GameConfig.DamageReduction(defense, defenderLevel, armorPen);
            return Mathf.Max(GameConfig.MinDamage, Mathf.RoundToInt(rawAttack * (1f - reduction)));
        }

        private static void SplitShield(ref int shield, int damage, out int absorbed, out int hpDamage)
        {
            absorbed = Mathf.Min(shield, damage);
            shield -= absorbed;
            hpDamage = damage - absorbed;
        }

        private int EffectiveAttack(PlayerData player)
        {
            float mult = 1f;
            if (_warCryTimer > 0f)
                mult += RuneMods.Field(player, SkillId.WarCry, RuneField.Value,
                    SkillCatalog.Get(SkillId.WarCry).value);
            if (_bulwarkAttackTimer > 0f && player.shield > 0f)
                mult += RuneMods.Field(player, SkillId.Bulwark, RuneField.Value2,
                    SkillCatalog.Get(SkillId.Bulwark).value2);
            return Mathf.RoundToInt(player.Attack * mult);
        }

        private int EffectiveMonsterDefense(MonsterData monster)
        {
            float factor = _armorBreakTimer > 0f ? _armorBreakFactor : 1f;
            return Mathf.Max(0, Mathf.RoundToInt(monster.defense * factor));
        }

        private float EffectiveMonsterInterval(MonsterData monster)
        {
            float speed = monster.speed * (_suppressTimer > 0f ? 0.8f : 1f);
            // 疾袭契约的攻速加成跟速度一样进「出手间隔」这一条路，不单开一套计时；
            // 但乘在间隔上而不是塞进攻速加成，否则深层已撞上限的怪会完全免疫这条契约。
            return GameConfig.AttackInterval(speed, monster.attackHaste)
                 * Mathf.Max(0.05f, monster.attackIntervalMult);
        }

        private static int ApplyLifesteal(PlayerData player, int damageDealt)
        {
            if (damageDealt <= 0 || player.Lifesteal <= 0f) return 0;
            if (player.currentHp <= 0) return 0;

            // 枯竭契约：治疗量走同一个乘区（0 = 完全不回血）。
            // 符文「血偿」：生命低于 40% 时吸血翻倍（乘在这里，不动 Lifesteal 本身）。
            int heal = Mathf.RoundToInt(damageDealt * player.Lifesteal
                                        * player.ContractHealMultiplier
                                        * RuneMods.LifestealScale(player));
            if (heal <= 0) return 0;

            int before = player.currentHp;
            player.currentHp = Mathf.Min(player.MaxHp, player.currentHp + heal);
            return player.currentHp - before;
        }

        /// <summary>
        /// 固定子步长上限。一帧不论多长、倍速多少，只用 ≤1/120 秒的等长子步推进：
        /// 以前直接按 Time.deltaTime * SpeedMultiplier 推进，x1 / x2 / x4 与掉帧会得到不同的胜负
        /// （审计 L1：同一个种子、同一份状态，步长不同结果就不同）。
        /// </summary>
        public const float MaxTickStep = 1f / 120f;

        /// <summary>单帧最多补算的子步数。超过就丢时间——宁可慢放，也不要长卡顿后一帧算几百步。</summary>
        private const int MaxSubStepsPerFrame = 32;

        /// <summary>不足一个子步的余量，留到下一帧继续攒。</summary>
        private float _carrySeconds;

        public BattleTickResult Tick(PlayerData player, MonsterData monster, float dt, BattleLog log)
        {
            if (player == null || monster == null) return BattleTickResult.Ongoing;

            // 只推进整数个固定子步，余量留给下一帧：事件序列因此只取决于「已经过的时间」，
            // 与帧率、与倍速（倍速只是让同样的时间更快流过）都无关。
            if (dt > 0f) _carrySeconds += dt;
            int steps = (int)(_carrySeconds / MaxTickStep);
            if (steps <= 0) return BattleTickResult.Ongoing;

            if (steps > MaxSubStepsPerFrame)
            {
                _carrySeconds = 0f;   // 长卡顿 / 后台挂起之后不补算，直接丢掉积压
                steps = MaxSubStepsPerFrame;
            }
            else
            {
                _carrySeconds -= steps * MaxTickStep;
            }

            BattleTickResult result = BattleTickResult.Ongoing;
            for (int i = 0; i < steps; i++)
            {
                result = TickStep(player, monster, MaxTickStep, log);
                if (result != BattleTickResult.Ongoing) break;
            }
            return result;
        }

        /// <summary>一个固定子步内的结算：技能计时 → 主动施放 → 主角出手 → 怪物出手 → 保命 / 死亡判定。</summary>
        private BattleTickResult TickStep(PlayerData player, MonsterData monster, float dt, BattleLog log)
        {
            TickSkillTimers(player, dt, log);
            TryCastActiveSkills(player, monster, log);

            // ---- 主角出手 ----
            _playerAttackTimer -= dt;
            int catchUp = 0;
            while (_playerAttackTimer <= 0f && !monster.IsDead && catchUp++ < MaxCatchUpAttacks)
            {
                if (player.doctrine == 1 && _stormMarks >= StormMarkMax)
                {
                    CastChainLightning(player, monster, EffectiveAttack(player), log);
                }
                else if (player.doctrine == 2 && _mountainLayers >= MountainLayerMax)
                {
                    AutoMountainSlam(player, monster, EffectiveAttack(player), log);
                }
                else
                {
                    NormalAttack(player, monster, log);
                }

                _playerAttackTimer += CurrentAttackInterval(player);
            }

            if (monster.IsDead) return BattleTickResult.MonsterKilled;
            if (_playerAttackTimer <= 0f) _playerAttackTimer = CurrentAttackInterval(player);

            // ---- 怪物出手 ----
            monster.attackTimer -= dt;
            catchUp = 0;
            while (monster.attackTimer <= 0f && !monster.IsDead && player.currentHp > 0
                   && catchUp++ < MaxCatchUpAttacks)
            {
                MonsterAttack(player, monster, log);
                monster.attackTimer += EffectiveMonsterInterval(monster);
            }

            if (monster.attackTimer <= 0f && !monster.IsDead && player.currentHp > 0)
                monster.attackTimer = EffectiveMonsterInterval(monster);

            if (monster.IsDead) return BattleTickResult.MonsterKilled;

            // 保命：致死一击时留 1 血并进入 2 秒无敌。
            if (player.currentHp <= 0 && player.HasSkill(SkillId.LastStand))
            {
                SkillDef def = SkillCatalog.Get(SkillId.LastStand);
                if (_lastStandCooldown <= 0f)
                {
                    float window = RuneMods.Field(player, SkillId.LastStand, RuneField.Duration, def.duration);
                    _lastStandTimer = window;
                    _lastStandCooldown = RuneMods.Field(player, SkillId.LastStand, RuneField.Cooldown, def.cooldown);
                    player.currentHp = 1;
                    log.AddCombat($"✦ 保命！保留 1 点生命，{window:0.#} 秒内不会死亡", def.color);
                    RaiseVisual(BattleFx.SkillNearDeath, 1);
                }
                else if (_lastStandTimer > 0f)
                {
                    player.currentHp = 1;
                }
            }

            if (player.currentHp <= 0) return BattleTickResult.PlayerDied;
            return BattleTickResult.Ongoing;
        }

        // ==================== 主角出手 ====================

        private void NormalAttack(PlayerData player, MonsterData monster, BattleLog log)
        {
            int attack = EffectiveAttack(player);
            bool crit;
            int dmg = HitMonster(player, monster, attack, true, out crit, "主角 → 怪物", GameConfig.LogInfo,
                BattleFx.PlayerHit, false, log);
            PlayerHits++;
            if (crit) PlayerCrits++;

            if (crit) OnPlayerCrit(player, monster, attack, log);

            if (player.doctrine == 1)
            {
                AddStormMark(player, 1);
                if (crit && _stormCritMarkIcd <= 0f)
                {
                    AddStormMark(player, 1);
                    _stormCritMarkIcd = 1f;
                }
            }
            else if (player.doctrine == 2)
            {
                _mountainAttackCounter++;
                if (_mountainAttackCounter % 2 == 0) AddMountainLayer(player, 1);
            }

        }

        private int HitMonster(PlayerData player, MonsterData monster, int rawAttack, bool canCrit,
            out bool crit, string prefix, Color color, BattleFx fx, bool lightning, BattleLog log)
        {
            int defense = EffectiveMonsterDefense(monster);
            int dmg;
            if (canCrit)
            {
                dmg = RollDamage(Mathf.RoundToInt(rawAttack), defense, monster.level,
                    player.CritChance, player.CritDamage, out crit, player.ArmorPenetration);
            }
            else
            {
                crit = false;
                dmg = ApplyDefense(Mathf.RoundToInt(rawAttack), defense, monster.level, player.ArmorPenetration);
            }

            if (_shockStacks > 0)
                dmg = Mathf.Max(1, Mathf.RoundToInt(dmg * (1f + 0.06f * _shockStacks)));

            // 深渊遗物：独立伤害乘区，落在最终伤害上（不并入技能加算池）。
            float relicDamage = player.RelicDamageMultiplier;
            if (relicDamage != 1f)
                dmg = Mathf.Max(1, Mathf.RoundToInt(dmg * relicDamage));

            // 深层契约 · 硬化：敌人吃伤害的乘区（同样不并入技能加算池）。
            float monsterTaken = player.ContractMonsterDamageTakenMultiplier;
            if (monsterTaken != 1f)
                dmg = Mathf.Max(1, Mathf.RoundToInt(dmg * monsterTaken));

            int absorbed;
            int hpDamage;
            SplitShield(ref monster.shield, dmg, out absorbed, out hpDamage);
            if (hpDamage > 0) monster.currentHp = Mathf.Max(0, monster.currentHp - hpDamage);

            string shieldNote = absorbed > 0 ? $"  (护盾吸收 {absorbed})" : "";
            string shieldLeft = monster.shield > 0 ? $"  护盾 {monster.shield}" : "";
            log.AddCombat($"{prefix}  {(crit ? "暴击 " : "")}{dmg} 伤害{shieldNote}   (怪物 {monster.currentHp}/{monster.maxHp}{shieldLeft})",
                color);
            GameAudio.PlayHit(crit);
            RaiseVisual(crit && fx == BattleFx.PlayerHit ? BattleFx.PlayerCrit : fx, dmg);

            int healed = ApplyLifesteal(player, dmg);
            if (healed > 0)
            {
                RaiseVisual(BattleFx.Drain, healed);
                log.AddCombat($"✦ 吸血 +{healed}   (主角 {player.currentHp}/{player.MaxHp})",
                    SkillCatalog.Get(SkillId.Vampire).color);
            }

            if (lightning && !monster.IsDead) OnLightningHit(player, monster, log);
            return dmg;
        }

        private void OnLightningHit(PlayerData player, MonsterData monster, BattleLog log)
        {
            AddShock(player);
            if (!player.HasSkill(SkillId.ThunderPunish) || monster.IsDead) return;

            _lightningHits++;
            SkillDef punish = SkillCatalog.Get(SkillId.ThunderPunish);
            int need = Mathf.Max(2, Mathf.RoundToInt(
                RuneMods.Field(player, SkillId.ThunderPunish, RuneField.Hits, punish.hits)));
            if (_lightningHits < need) return;

            _lightningHits = 0;
            SkillDef def = punish;
            float punishMult = RuneMods.Field(player, SkillId.ThunderPunish, RuneField.Value, def.value);
            int dmg = HitMonster(player, monster, Mathf.RoundToInt(EffectiveAttack(player) * punishMult),
                false, out _, $"✦ {def.name}！", def.color, BattleFx.SkillGiantSlayer, false, log);
            if (dmg > 0) AddStormMark(player, 1);
        }

        private void OnPlayerCrit(PlayerData player, MonsterData monster, int attack, BattleLog log)
        {
            if (player.doctrine == 1 && player.HasSkill(SkillId.StormHaste))
            {
                _stormHasteStacks = Mathf.Min(RuneMods.StormHasteMaxStacks(player), _stormHasteStacks + 1);
                _stormHasteTimer = RuneMods.Field(player, SkillId.StormHaste, RuneField.Duration,
                    SkillCatalog.Get(SkillId.StormHaste).duration);
            }

            if (player.doctrine == 1 && player.HasSkill(SkillId.Arc) && !monster.IsDead)
            {
                SkillDef arc = SkillCatalog.Get(SkillId.Arc);
                float chance = RuneMods.Field(player, SkillId.Arc, RuneField.TriggerChance, arc.triggerChance);
                if (Random.value < chance)
                {
                    float mult = RuneMods.Field(player, SkillId.Arc, RuneField.Value, arc.value);
                    HitMonster(player, monster, Mathf.RoundToInt(attack * mult), false, out _,
                        $"✦ {arc.name}！", arc.color, BattleFx.SkillCritChain, true, log);
                }
            }
        }

        private void CastChainLightning(PlayerData player, MonsterData monster, int attack, BattleLog log)
        {
            SkillDef core = SkillCatalog.Get(SkillId.ThunderRush); // 只用配色
            _stormMarks = 0;

            int dmg = HitMonster(player, monster, Mathf.RoundToInt(attack * 1.80f), true, out bool crit,
                "✦ 连锁闪电！", SkillCatalog.StormColor, BattleFx.SkillCritChain, true, log);
            if (crit) PlayerCrits++;
            HealFromLightning(player, log);
            if (monster.IsDead) return;

            for (int i = 0; i < 2 && !monster.IsDead; i++)
            {
                HitMonster(player, monster, Mathf.RoundToInt(attack * 0.90f), false, out _,
                    "  ↳ 连锁闪电 · 跳射", core.color, BattleFx.SkillWhirlwind, true, log);
                HealFromLightning(player, log);
            }
        }

        private void HealFromLightning(PlayerData player, BattleLog log)
        {
            if (player.currentHp <= 0 || player.currentHp >= player.MaxHp) return;
            int heal = Mathf.RoundToInt(player.MaxHp * SkillCatalog.Get(SkillId.ThunderRush).value4
                                        * player.ContractHealMultiplier);
            if (heal <= 0) return;
            int before = player.currentHp;
            player.currentHp = Mathf.Min(player.MaxHp, player.currentHp + heal);
            int healed = player.currentHp - before;
            if (healed > 0)
            {
                RaiseVisual(BattleFx.Drain, healed);
                log.AddCombat($"  ↳ 闪电回流 +{healed}", SkillCatalog.StormColor);
            }
        }

        private void AutoMountainSlam(PlayerData player, MonsterData monster, int attack, BattleLog log)
        {
            _mountainLayers = 0;
            ApplyArmorBreak(0.75f, 5f);
            _suppressTimer = Mathf.Max(_suppressTimer, 1.5f);

            float mult = 3.30f * (1f + player.MountainSkillDamageBonus);
            HitMonster(player, monster, Mathf.RoundToInt(attack * mult), true, out bool crit,
                "✦ 崩山！", SkillCatalog.MountainColor, BattleFx.SkillExecute, false, log);
            if (crit)
            {
                PlayerCrits++;
                OnPlayerCrit(player, monster, attack, log);
            }
            if (!monster.IsDead) OnMountainHit(player, monster, attack, log);
        }

        private void CastThunderRush(PlayerData player, MonsterData monster, SkillDef def, BattleLog log)
        {
            float mult = RuneMods.Field(player, SkillId.ThunderRush, RuneField.Value, def.value);
            int marks = Mathf.RoundToInt(RuneMods.Field(player, SkillId.ThunderRush, RuneField.Value2, def.value2));
            float haste = RuneMods.Field(player, SkillId.ThunderRush, RuneField.Value3, def.value3);
            string hasteNote = haste > 0f ? $"，攻速 +{haste * 100f:0}% {def.duration:0} 秒" : "";

            HitMonster(player, monster, Mathf.RoundToInt(EffectiveAttack(player) * mult), false, out _,
                $"✦ {def.name}！", def.color, BattleFx.SkillHeavy, true, log);
            AddStormMark(player, marks);
            if (haste > 0f) _rushHasteTimer = def.duration;
            log.AddCombat($"  ↳ 雷印 {_stormMarks}/{StormMarkMax}{hasteNote}", def.color);
        }

        private void CastThunderGod(PlayerData player, MonsterData monster, SkillDef def, BattleLog log)
        {
            int marks = _stormMarks;
            float perMark = RuneMods.Field(player, SkillId.ThunderGod, RuneField.Value2, def.value2);
            float fullMult = RuneMods.Field(player, SkillId.ThunderGod, RuneField.Value3, def.value3);
            float mult = def.value + perMark * marks;
            bool full = marks >= StormMarkMax;
            if (full) mult *= fullMult;

            HitMonster(player, monster, Mathf.RoundToInt(EffectiveAttack(player) * mult), false, out _,
                $"✦ {def.name}！", def.color, BattleFx.SkillHeavy, true, log);
            if (full)
            {
                // 符文「审罚」：满印爆发更狠，但不再重置雷霆突袭——两条收益只能选一条。
                if (!RuneMods.SuppressFullMarkReset(player))
                {
                    _activeCooldown[0] = 0f;
                    log.AddCombat("  ↳ 满雷印爆发，雷霆突袭冷却已重置", def.color);
                }
                else
                {
                    log.AddCombat("  ↳ 满雷印爆发（审罚：不重置雷霆突袭）", def.color);
                }
            }
            log.AddCombat($"  ↳ 本次按 {marks} 层雷印结算（不消耗）", def.color);
        }

        private void CastMountainSlam(PlayerData player, MonsterData monster, SkillDef def, BattleLog log)
        {
            int layers = _mountainLayers;
            float mult = def.value + def.value2 * layers;
            mult *= (1f + player.MountainSkillDamageBonus);

            HitMonster(player, monster, Mathf.RoundToInt(EffectiveAttack(player) * mult), false, out _,
                $"✦ {def.name}！", def.color, BattleFx.SkillExecute, false, log);
            ApplyArmorBreak(1f - RuneMods.Field(player, SkillId.MountainSlam, RuneField.Value3, def.value3),
                def.duration);
            _suppressTimer = Mathf.Max(_suppressTimer, def.duration2);

            // 符文「余震」：按当前岩层数补一段，代价是破甲从 30% 砍到 15%。
            float layerBonus = RuneMods.MountainSlamLayerBonus(player);
            if (layerBonus > 0f && layers > 0 && !monster.IsDead)
            {
                HitMonster(player, monster, Mathf.RoundToInt(EffectiveAttack(player) * layerBonus * layers),
                    false, out _, $"  ↳ 余震 ×{layers} 层", def.color, BattleFx.SkillCounter, false, log);
            }

            if (!monster.IsDead) OnMountainHit(player, monster, EffectiveAttack(player), log);
        }

        private void OnMountainHit(PlayerData player, MonsterData monster, int attack, BattleLog log)
        {
            if (!player.HasSkill(SkillId.Aftershock) || monster.IsDead) return;
            SkillDef def = SkillCatalog.Get(SkillId.Aftershock);
            float chance = RuneMods.Field(player, SkillId.Aftershock, RuneField.TriggerChance, def.triggerChance);
            if (Random.value >= chance) return;

            float mult = RuneMods.Field(player, SkillId.Aftershock, RuneField.Value, def.value)
                       * (1f + player.MountainSkillDamageBonus);
            HitMonster(player, monster, Mathf.RoundToInt(attack * mult), false, out _,
                $"✦ {def.name}！", def.color, BattleFx.SkillCounter, false, log);
        }

        private void CastBulwark(PlayerData player, MonsterData monster, SkillDef def, BattleLog log)
        {
            float shieldPct = RuneMods.Field(player, SkillId.Bulwark, RuneField.Value, def.value);
            float attackPct = RuneMods.Field(player, SkillId.Bulwark, RuneField.Value2, def.value2);
            int amount = Mathf.RoundToInt(player.MaxHp * shieldPct);
            _bulwarkShieldAmount = amount;
            _bulwarkShieldTimer = def.duration;
            player.shield = Mathf.Max(player.shield, amount);
            _bulwarkAttackTimer = def.duration2;

            log.AddCombat($"✦ {def.name}！护盾 {amount}，{def.duration:0} 秒；攻击 +{attackPct * 100f:0}% {def.duration2:0} 秒",
                def.color);
            RaiseVisual(BattleFx.Shield, amount);
        }

        private void CastWarCry(PlayerData player, MonsterData monster, SkillDef def, BattleLog log)
        {
            _warCryTimer = def.duration;
            float atk = RuneMods.Field(player, SkillId.WarCry, RuneField.Value, def.value);
            float guard = RuneMods.Field(player, SkillId.WarCry, RuneField.Value2, def.value2);
            log.AddCombat($"✦ {def.name}！攻击 +{atk * 100f:0}%、受伤 -{guard * 100f:0}% {def.duration:0} 秒",
                def.color);
            RaiseVisual(BattleFx.Berserk);
        }

        // ==================== 怪物出手 ====================

        private void MonsterAttack(PlayerData player, MonsterData monster, BattleLog log)
        {
            if (_lastStandTimer > 0f)
            {
                // 免死窗口内完全免疫，但仍要推进攻击间隔。
                log.AddCombat("怪物 → 主角  免死窗口，伤害无效", GameConfig.LogWarn);
                return;
            }

            bool crit;
            // 黑雾：护甲有效率被压，受击时按缩水后的防御算减伤（对肉装打击更大）。
            float armorMult = player.ContractArmorEffectiveMult;
            int incomingDefense = armorMult >= 1f
                ? player.Defense
                : Mathf.Max(0, Mathf.RoundToInt(player.Defense * Mathf.Max(0f, armorMult)));
            int dmg = RollDamage(monster.attack, incomingDefense, player.level,
                monster.critChance, monster.critDamage, out crit);

            if (_warCryTimer > 0f)
                dmg = Mathf.Max(GameConfig.MinDamage,
                    Mathf.RoundToInt(dmg * (1f - RuneMods.Field(player, SkillId.WarCry, RuneField.Value2,
                        SkillCatalog.Get(SkillId.WarCry).value2))));

            // 流派核心减伤：风暴按雷印层数、山崩按岩层数，资源越满越硬。
            float doctrineGuard = player.doctrine == 1 ? 0.03f * _stormMarks
                : player.doctrine == 2 ? 0.06f * _mountainLayers : 0f;
            if (doctrineGuard > 0f)
                dmg = Mathf.Max(GameConfig.MinDamage, Mathf.RoundToInt(dmg * (1f - doctrineGuard)));

            // 符文「嗜血」：用受到伤害 +8% 换吸血 30%（乘法区，放在遗物受伤乘区之前）。
            float runeTaken = RuneMods.TakenDamageMultiplier(player);
            if (runeTaken != 1f)
                dmg = Mathf.Max(GameConfig.MinDamage, Mathf.RoundToInt(dmg * runeTaken));

            // 深渊遗物：受伤乘区（铁壁 / 死斗）与残喘（低血额外减伤），排在流派减伤之后、护盾之前。
            float relicTaken = player.RelicDamageTakenMultiplier;
            if (relicTaken != 1f)
                dmg = Mathf.Max(GameConfig.MinDamage, Mathf.RoundToInt(dmg * relicTaken));
            float relicLowHp = player.RelicLowHpReduction;
            if (relicLowHp > 0f)
                dmg = Mathf.Max(GameConfig.MinDamage, Mathf.RoundToInt(dmg * (1f - relicLowHp)));

            int absorbed;
            int hpDamage;
            SplitShield(ref player.shield, dmg, out absorbed, out hpDamage);
            if (hpDamage > 0) player.currentHp = Mathf.Max(0, player.currentHp - hpDamage);
            MonsterHits++;
            if (crit) MonsterCrits++;

            string notes = "";
            if (absorbed > 0) notes += $"  护盾吸收 {absorbed}";
            if (player.shield > 0) notes += $"  护盾 {player.shield}";

            if (monster.affix == MonsterAffix.Poison && player.currentHp > 0)
            {
                int poison = Mathf.Max(1, Mathf.RoundToInt(player.MaxHp * GameConfig.PoisonAffixPercent));
                player.currentHp = Mathf.Max(0, player.currentHp - poison);
                notes += $"  剧毒 {poison}";
                RaiseVisual(BattleFx.Poison, poison);
            }

            if (monster.affix == MonsterAffix.Lifesteal && !monster.IsDead)
            {
                int before = monster.currentHp;
                monster.currentHp = Mathf.Min(monster.maxHp,
                    monster.currentHp + Mathf.RoundToInt(dmg * GameConfig.LifestealAffixPercent));
                int mHealed = monster.currentHp - before;
                if (mHealed > 0) notes += $"  吸血 +{mHealed}";
            }

            log.AddCombat($"怪物 → 主角  {(crit ? "暴击 " : "")}{dmg} 伤害{notes}   (主角 {player.currentHp}/{player.MaxHp})",
                crit ? GameConfig.LogDanger : GameConfig.LogWarn);
            GameAudio.PlayHit(crit);
            RaiseVisual(crit ? BattleFx.MonsterCrit : BattleFx.MonsterHit, dmg);

            OnPlayerHit(player);
        }

        private void OnPlayerHit(PlayerData player)
        {
            if (player.doctrine == 2 && _mountainHitIcd <= 0f)
            {
                AddMountainLayer(player, 1);
                _mountainHitIcd = 1.5f;
            }

            if (player.HasSkill(SkillId.RockArmor) && _rockArmorIcd <= 0f)
            {
                SkillDef def = SkillCatalog.Get(SkillId.RockArmor);
                float chance = RuneMods.Field(player, SkillId.RockArmor, RuneField.TriggerChance, def.triggerChance);
                if (Random.value < chance)
                {
                    int amount = Mathf.RoundToInt(player.MaxHp *
                        RuneMods.Field(player, SkillId.RockArmor, RuneField.Value, def.value));
                    int cap = Mathf.RoundToInt(player.MaxHp *
                        RuneMods.Field(player, SkillId.RockArmor, RuneField.Value2, def.value2));
                    _rockShieldAmount = Mathf.Min(amount, cap);
                    _rockShieldTimer = def.duration;
                    player.shield = Mathf.Max(player.shield, _rockShieldAmount);
                    _rockArmorIcd = RuneMods.Field(player, SkillId.RockArmor, RuneField.Value3, def.value3);
                    RaiseVisual(BattleFx.Shield, _rockShieldAmount);
                }
            }
        }

        // ==================== 计时与资源 ====================

        private void TickSkillTimers(PlayerData player, float dt, BattleLog log)
        {
            if (_globalCooldown > 0f) _globalCooldown -= dt;
            for (int i = 0; i < _activeCooldown.Length; i++)
                if (_activeCooldown[i] > 0f) _activeCooldown[i] -= dt;

            if (_warCryTimer > 0f) _warCryTimer = Mathf.Max(0f, _warCryTimer - dt);
            if (_lastStandTimer > 0f) _lastStandTimer = Mathf.Max(0f, _lastStandTimer - dt);
            if (_lastStandCooldown > 0f) _lastStandCooldown = Mathf.Max(0f, _lastStandCooldown - dt);
            if (_bulwarkAttackTimer > 0f) _bulwarkAttackTimer = Mathf.Max(0f, _bulwarkAttackTimer - dt);
            if (_bulwarkShieldTimer > 0f) _bulwarkShieldTimer = Mathf.Max(0f, _bulwarkShieldTimer - dt);
            if (_rushHasteTimer > 0f) _rushHasteTimer = Mathf.Max(0f, _rushHasteTimer - dt);
            if (_stormCritMarkIcd > 0f) _stormCritMarkIcd = Mathf.Max(0f, _stormCritMarkIcd - dt);
            if (_stormHasteTimer > 0f)
            {
                _stormHasteTimer = Mathf.Max(0f, _stormHasteTimer - dt);
                if (_stormHasteTimer <= 0f) _stormHasteStacks = 0;
            }
            if (_shockTimer > 0f)
            {
                _shockTimer = Mathf.Max(0f, _shockTimer - dt);
                if (_shockTimer <= 0f) _shockStacks = 0;
            }
            if (_mountainHitIcd > 0f) _mountainHitIcd = Mathf.Max(0f, _mountainHitIcd - dt);
            if (_armorBreakTimer > 0f)
            {
                _armorBreakTimer = Mathf.Max(0f, _armorBreakTimer - dt);
                if (_armorBreakTimer <= 0f) _armorBreakFactor = 1f;
            }
            if (_suppressTimer > 0f) _suppressTimer = Mathf.Max(0f, _suppressTimer - dt);
            if (_rockArmorIcd > 0f) _rockArmorIcd = Mathf.Max(0f, _rockArmorIcd - dt);
            if (_rockShieldTimer > 0f) _rockShieldTimer = Mathf.Max(0f, _rockShieldTimer - dt);

            int cap = 0;
            if (_bulwarkShieldTimer > 0f) cap = Mathf.Max(cap, _bulwarkShieldAmount);
            if (_rockShieldTimer > 0f) cap = Mathf.Max(cap, _rockShieldAmount);
            if (cap < player.shield) player.shield = cap;
            if (_bulwarkShieldTimer <= 0f) _bulwarkShieldAmount = 0;
            if (_rockShieldTimer <= 0f) _rockShieldAmount = 0;
        }

        private void AddStormMark(PlayerData player, int amount)
        {
            if (player.doctrine != 1 || amount <= 0) return;
            _stormMarks = Mathf.Min(StormMarkMax, _stormMarks + amount);
        }

        private void AddMountainLayer(PlayerData player, int amount)
        {
            if (player.doctrine != 2 || amount <= 0) return;
            _mountainLayers = Mathf.Min(MountainLayerMax, _mountainLayers + amount);
        }

        private void AddShock(PlayerData player)
        {
            if (player.doctrine != 1) return;
            _shockStacks = Mathf.Min(3, _shockStacks + 1);
            _shockTimer = 6f;
        }

        private void ApplyArmorBreak(float factor, float duration)
        {
            _armorBreakFactor = Mathf.Clamp(factor, 0.1f, 1f);
            _armorBreakTimer = Mathf.Max(_armorBreakTimer, duration);
        }

        // ==================== 主动技能 ====================

        private void TryCastActiveSkills(PlayerData player, MonsterData monster, BattleLog log)
        {
            if (_globalCooldown > 0f || monster.IsDead) return;

            for (int slot = 0; slot < _activeCooldown.Length; slot++)
            {
                int id = player.GetSkillSlot(slot);
                if (id < 0 || id >= SkillCatalog.All.Length) continue;

                SkillDef def = SkillCatalog.All[id];
                if (!def.IsActive) continue;
                if (_activeCooldown[slot] > 0f) continue;

                // 让自动释放看起来像人：雷神降临等雷印过半、战吼不覆盖、磐石不叠空盾。
                if (def.id == SkillId.ThunderGod && _stormMarks < 4) continue;
                if (def.id == SkillId.WarCry && _warCryTimer > 0f) continue;
                if (def.id == SkillId.Bulwark && player.shield >= Mathf.RoundToInt(player.MaxHp * 0.10f)) continue;

                CastActive(player, monster, def, slot, log);
                return;
            }
        }

        private void CastActive(PlayerData player, MonsterData monster, SkillDef def, int slot, BattleLog log)
        {
            _activeCooldown[slot] = RuneMods.Field(player, def.id, RuneField.Cooldown, def.cooldown);
            _globalCooldown = GameConfig.SkillGlobalCooldown;

            switch (def.id)
            {
                case SkillId.ThunderRush: CastThunderRush(player, monster, def, log); break;
                case SkillId.ThunderGod: CastThunderGod(player, monster, def, log); break;
                case SkillId.MountainSlam: CastMountainSlam(player, monster, def, log); break;
                case SkillId.Bulwark: CastBulwark(player, monster, def, log); break;
                case SkillId.WarCry: CastWarCry(player, monster, def, log); break;
                default: break;
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace AutoBattle
{
    /// <summary>
    /// 主角数据（要求 2）。基础属性由等级推导，最终属性 = 基础 + 装备加成。
    /// </summary>
    [Serializable]
    public class PlayerData
    {
        /// <summary>每类技能的槽位数：主动 / 被动 / 触发各 2 个。</summary>
        public const int SkillSlotsPerKind = 2;

        /// <summary>技能槽总数（三类 × 每类槽位）。存档/UI/战斗循环都以这个常量为准。</summary>
        public const int SkillSlotCount = SkillSlotsPerKind * 3;

        public int level = 1;
        public int exp = 0;

        /// <summary>金币总数：卖掉弱于当前装备的掉落累积，单价见 GameConfig.SellPrice。</summary>
        public int gold;

        /// <summary>持有的生命药剂数量（上限见 GameConfig.PotionMaxCount）。</summary>
        public int potionCount;

        /// <summary>是否开启「自动购买」：持有数低于阈值且金币足够时自动补 1 瓶（交战时不补）。</summary>
        public bool autoBuyPotion = true;

        /// <summary>巅峰等级：满级（Lv.100）后继续累积经验提升，每级 +1 攻击 / +1 防御。</summary>
        public int paragonLevel;

        /// <summary>巅峰经验（只在满级后累积）。</summary>
        public int paragonExp;

        /// <summary>
        /// 已获得的技能位掩码（第 N 位对应 SkillId N）。
        /// 100 级前恒为 0（完全没有技能）；满级后每升 10 级巅峰获得一次「三选一」。
        /// </summary>
        public int ownedSkillMask;

        /// <summary>
        /// 还没兑现的技能三选一次数。巅峰升级时按里程碑差值补上，玩家每选一个减一。
        /// 存进存档：中途退出不会丢应得的技能。
        /// </summary>
        public int pendingSkillPicks;

        // ==================== 关卡 / 章节进度（步骤 4a） ====================

        /// <summary>当前章节号，1 起。关卡怪物等级 = ChapterMonsterLevelStep × chapterIndex。</summary>
        public int chapterIndex = 1;

        /// <summary>当前章节内的关卡号，1 起。</summary>
        public int stageIndex = 1;

        /// <summary>本关已击杀的怪物数（主角阵亡重试时归零）。</summary>
        public int stageKillCount;

        /// <summary>历史解锁到的最高章节，选关界面用它判断锁定。</summary>
        public int maxChapterUnlocked = 1;

        // ==================== 首通记录（步骤 4c） ====================

        /// <summary>
        /// 已经拿过首通奖励的关卡，元素是 chapter * 100 + stage 的编码。
        /// 用 List&lt;int&gt; 而不是 HashSet&lt;int&gt;：JsonUtility 不序列化 HashSet。
        /// </summary>
        public List<int> clearedStages = new List<int>();

        /// <summary>
        /// 本存档在无尽模式里清过的最高层（0 = 还没打过）。只记「已清完」的层，
        /// 死在第 N 层不算第 N 层。可被无尽模式写，也可以被手改档写坏，读档时夹 0..9999。
        /// </summary>
        public int endlessBestFloor;

        /// <summary>把关卡编码成 clearedStages 里的整数。章节 &lt; 100，所以两位关卡号放得下。</summary>
        public static int StageKey(int chapter, int stage) => chapter * 100 + stage;

        /// <summary>
        /// 编码方式的解码侧。和 StageKey 放在一起，改编码时只需看这两行
        /// （原先 Stages.cs 里直接写着 / 100 和 % 100，改一处就会静默错位）。
        /// </summary>
        public static int ChapterOfKey(int stageKey) => stageKey / 100;

        /// <summary>见 ChapterOfKey。</summary>
        public static int StageOfKey(int stageKey) => stageKey % 100;

        /// <summary>这一关是否已经拿过首通奖励。</summary>
        public bool IsStageFirstCleared(int chapter, int stage)
        {
            return clearedStages != null && clearedStages.Contains(StageKey(chapter, stage));
        }

        /// <summary>标记首通。返回 true 表示这是头一回（调用方据此发奖励）。</summary>
        public bool MarkStageFirstCleared(int chapter, int stage)
        {
            if (clearedStages == null) clearedStages = new List<int>();
            int key = StageKey(chapter, stage);
            if (clearedStages.Contains(key)) return false;
            clearedStages.Add(key);
            return true;
        }

        /// <summary>已首通的关卡总数。</summary>
        public int ClearedStageCount => clearedStages != null ? clearedStages.Count : 0;

        /// <summary>
        /// 已经领到的首通金币总额（步骤 4d）。逐关按 StageCatalog 的公式累加，
        /// 所以改了首通奖励配置之后这个数字会跟着变——它是「按规则重算」，
        /// 不是另记一笔流水。
        /// </summary>
        public int TotalFirstClearGoldClaimed()
        {
            if (clearedStages == null) return 0;
            int sum = 0;
            for (int i = 0; i < clearedStages.Count; i++)
            {
                sum += StageCatalog.FirstClearGoldOfKey(clearedStages[i]);
            }
            return sum;
        }

        /// <summary>该章已经首通的关卡数。</summary>
        public int ClearedCountInChapter(int chapter)
        {
            if (clearedStages == null) return 0;
            int n = 0;
            for (int s = 1; s <= StageCatalog.StagesPerChapter; s++)
            {
                if (clearedStages.Contains(StageKey(chapter, s))) n++;
            }
            return n;
        }

        /// <summary>本关是不是头一回打（还没拿过首通奖励）。</summary>
        public bool IsCurrentStageFirstClear => !IsStageFirstCleared(chapterIndex, stageIndex);

        /// <summary>当前关卡的定义（章节 / 关卡号会被夹到合法范围）。</summary>
        public StageDef CurrentStage => StageCatalog.Get(chapterIndex, stageIndex);

        /// <summary>本关还差几只怪通关。</summary>
        public int StageKillsLeft => Mathf.Max(0, CurrentStage.killTarget - stageKillCount);

        /// <summary>本关是否已经打满击杀数。</summary>
        public bool IsStageCleared => stageKillCount >= CurrentStage.killTarget;

        /// <summary>
        /// 直接把进度放到某关（选关 / 读档 / 关卡结束推进都走这里）。
        /// 会推进 maxChapterUnlocked，但不会回退它。
        /// </summary>
        public void EnterStage(int chapter, int stage)
        {
            chapterIndex = Mathf.Clamp(chapter, 1, GameConfig.ChapterCount);
            stageIndex = Mathf.Clamp(stage, 1, GameConfig.StagesPerChapter);
            stageKillCount = 0;
            if (chapterIndex > maxChapterUnlocked) maxChapterUnlocked = chapterIndex;
        }

        /// <summary>
        /// 通关推进：本章还有下一关就去下一关，否则进入下一章第一关。
        /// 返回 false 表示已经是最后一关（终局），进度不动。
        /// </summary>
        public bool AdvanceStage()
        {
            int c = chapterIndex;
            int s = stageIndex;
            if (!StageCatalog.Advance(ref c, ref s)) return false;
            EnterStage(c, s);
            return true;
        }

        /// <summary>本关关底怪的档次（普通关精英、章末关 Boss）。</summary>
        public MonsterTier StageClearTier => CurrentStage.clearTier;

        // ---- 六个技能槽（-1 表示空）：0~1 主动、2~3 被动、4~5 触发 ----

        public int skillActive0 = -1;
        public int skillActive1 = -1;
        public int skillPassive0 = -1;
        public int skillPassive1 = -1;
        public int skillTrigger0 = -1;
        public int skillTrigger1 = -1;

        /// <summary>当前生命。载入后由 GameController 夹到 MaxHp 范围内。</summary>
        public int currentHp;

        /// <summary>当前护盾（「护盾生成」技能给的）。运行时状态，不存档；受击时优先于生命被消耗。</summary>
        [System.NonSerialized] public int shield;

        public Equipment weapon;
        public Equipment armor;
        public Equipment helmet;
        public Equipment boots;
        public Equipment accessory;

        /// <summary>
        /// 哪些槽位的「解锁赠礼」已经发过（位 = EquipmentSlot）。
        /// 防止读档/重开反复送同一件新手装备；旧存档没有这个字段时为 0。
        /// </summary>
        public int slotStarterMask;

        // ---- 基础属性（仅由等级决定）----

        public int BaseMaxHp => GameConfig.PlayerBaseMaxHp + GameConfig.PlayerMaxHpPerLevel * (level - 1);
        public int BaseAttack => GameConfig.PlayerBaseAttack + GameConfig.PlayerAttackPerLevel * (level - 1);
        public int BaseDefense => GameConfig.PlayerBaseDefense + GameConfig.PlayerDefensePerLevel * (level - 1);
        public int BaseSpeed => GameConfig.PlayerBaseSpeed + GameConfig.PlayerSpeedPerLevel * (level - 1);

        // ---- 最终属性（基础 + 装备）----

        /// <summary>对五个槽位做一次汇总；空槽按 0 处理。</summary>
        private float SumEquipment(Func<Equipment, float> pick)
        {
            float sum = 0f;
            for (int i = 0; i < EquipmentSlots.Count; i++)
            {
                Equipment e = GetEquipment(EquipmentSlots.All[i]);
                if (e != null) sum += pick(e);
            }
            return sum;
        }

        /// <summary>攻击速度加成 = 五槽副词条 + 鞋主属性（强化后）+ 迅捷被动。</summary>
        public float EquipAttackSpeedBonus => SumEquipment(e => e.affixAttackSpeed)
                                            + (boots != null ? boots.EnhancedHaste : 0f);

        /// <summary>副词条汇总：全属性加成，按比例同时放大攻击、防御、生命。</summary>
        public float AllStatsBonus => SumEquipment(e => e.affixAllStats);

        /// <summary>副词条汇总：暴击率。</summary>
        public float AffixCritChance => SumEquipment(e => e.affixCritChance);

        /// <summary>副词条汇总：暴击伤害。</summary>
        public float AffixCritDamage => SumEquipment(e => e.affixCritDamage);

        /// <summary>
        /// 护甲穿透（0.1 表示无视目标 10% 防御）= 副词条 + 饰品主属性（强化后）。
        /// 上限 80% —— 与暴击率 95% 上限同理，避免「完全无视防御」把防御属性变成废纸。
        /// </summary>
        public float ArmorPenetration => Mathf.Min(0.8f,
            SumEquipment(e => e.affixArmorPen) + (accessory != null ? accessory.EnhancedArmorPen : 0f));

        // ==================== 巅峰与技能汇总 ====================

        /// <summary>当前技能等级（由巅峰等级推导：每 10 级 +1）。</summary>
        public int SkillLevel => GameConfig.SkillLevel(paragonLevel);

        /// <summary>巅峰提供的攻击加成（纯加算，不参与百分比乘区）。</summary>
        public int ParagonAttackBonus => paragonLevel * GameConfig.ParagonAttackPerLevel;

        /// <summary>巅峰提供的防御加成。</summary>
        public int ParagonDefenseBonus => paragonLevel * GameConfig.ParagonDefensePerLevel;

        /// <summary>巅峰提供的生命加成（每级 +50，纯加算）。</summary>
        public int ParagonHpBonus => paragonLevel * GameConfig.ParagonHpPerLevel;

        /// <summary>该技能是否已装在任意槽位。</summary>
        public bool HasSkill(SkillId id)
        {
            int v = (int)id;
            return skillActive0 == v || skillActive1 == v || skillPassive0 == v
                || skillPassive1 == v || skillTrigger0 == v || skillTrigger1 == v;
        }

        /// <summary>取某个被动按技能等级放大后的数值；没装则返回 0。</summary>
        public float PassiveValue(SkillId id)
        {
            if (!HasSkill(id)) return 0f;
            return GameConfig.ScaleSkillValue(SkillCatalog.Get(id).value, SkillLevel);
        }

        // ---- 被动技能带来的属性 ----

        /// <summary>钢铁之躯：防御 +50%。</summary>
        public float SkillDefenseBonus => PassiveValue(SkillId.IronBody);

        /// <summary>狂暴之心：暴击率 +8%。</summary>
        public float SkillCritChanceBonus => PassiveValue(SkillId.BerserkHeart);

        /// <summary>致命打击：暴击伤害乘区（基础 +20%，随技能等级放大）。</summary>
        public float SkillCritDamageBonus => PassiveValue(SkillId.LethalStrike);

        /// <summary>吸血鬼：吸血 +5%。</summary>
        public float Lifesteal => PassiveValue(SkillId.Vampire);

        /// <summary>迅捷：攻击速度 +8%。</summary>
        public float SkillAttackSpeedBonus => PassiveValue(SkillId.Swiftness);

        /// <summary>巨人杀手：对精英 / Boss 的伤害加成。</summary>
        public float GiantSlayerBonus => PassiveValue(SkillId.GiantSlayer);

        /// <summary>不死之身：低血时的减伤比例。</summary>
        public float UndyingReduction => PassiveValue(SkillId.Undying);

        /// <summary>不死之身的触发血线（未装备返回 0）。</summary>
        public float UndyingThreshold => HasSkill(SkillId.Undying) ? SkillCatalog.Get(SkillId.Undying).hpThreshold : 0f;

        /// <summary>攻击速度总加成 = 装备副词条 + 迅捷被动。</summary>
        public float AttackSpeedBonus => EquipAttackSpeedBonus + SkillAttackSpeedBonus;

        // ---- 最终属性 ----

        // 全属性作用在「基础 + 装备」之和上；防御再吃一次钢铁之躯；巅峰加成是纯加算。
        // 装备进公式用的是 Enhanced*（强化后主属性），强化等级因此会自动流进
        // 角色属性 → 战斗 → EquipmentScore 评分，自动换装不需要任何额外改动。
        public int MaxHp => Mathf.RoundToInt((BaseMaxHp
                              + (armor != null ? armor.EnhancedHp : 0)
                              + (helmet != null ? helmet.EnhancedHp : 0)) * (1f + AllStatsBonus))
                          + ParagonHpBonus;

        public int Attack => Mathf.RoundToInt((BaseAttack + (weapon != null ? weapon.EnhancedAttack : 0)) * (1f + AllStatsBonus))
                           + ParagonAttackBonus;

        public int Defense => Mathf.RoundToInt(
                                Mathf.RoundToInt((BaseDefense
                                    + (armor != null ? armor.EnhancedDefense : 0)
                                    + (helmet != null ? helmet.EnhancedDefense : 0)) * (1f + AllStatsBonus))
                                * (1f + SkillDefenseBonus))
                            + ParagonDefenseBonus;

        /// <summary>速度本身不受全属性影响；鞋的主属性直接加算，攻速词条走独立的「攻击间隔乘区」。</summary>
        public int Speed => BaseSpeed + (boots != null ? boots.EnhancedSpeed : 0);

        /// <summary>暴击率 = 初始 5% + 武器主属性 + 副词条 + 狂暴之心，并夹在 MaxCritChance 以内。</summary>
        public float CritChance => Mathf.Min(
            GameConfig.MaxCritChance,
            GameConfig.PlayerBaseCritChance + (weapon != null ? weapon.critChance : 0f)
            + AffixCritChance + SkillCritChanceBonus);

        /// <summary>暴击伤害倍率 =（初始 150% + 武器主属性 + 饰品主属性 + 副词条）× 致命打击乘区。</summary>
        public float CritDamage => (GameConfig.PlayerBaseCritDamage
                                 + (weapon != null ? weapon.critDamage : 0f)
                                 + (accessory != null ? accessory.EnhancedCritDamage : 0f)
                                 + AffixCritDamage)
                                 * (1f + SkillCritDamageBonus);

        /// <summary>当前等级升到下一级所需经验；满级返回 0（要求 3）。</summary>
        public int ExpToNextLevel => level >= GameConfig.MaxLevel ? 0 : GameConfig.ExpToNextLevel(level);

        public bool IsMaxLevel => level >= GameConfig.MaxLevel;

        /// <summary>进度条填充比例 0~1：满级前是普通经验，满级后转为巅峰经验进度。</summary>
        public float ExpProgress
        {
            get
            {
                if (IsMaxLevel)
                {
                    if (IsMaxParagon) return 1f;
                    int needParagon = ParagonExpToNext;
                    return needParagon <= 0 ? 1f : Mathf.Clamp01((float)paragonExp / needParagon);
                }

                int need = ExpToNextLevel;
                return need <= 0 ? 0f : Mathf.Clamp01((float)exp / need);
            }
        }

        public float HpProgress => MaxHp <= 0 ? 0f : Mathf.Clamp01((float)currentHp / MaxHp);

        /// <summary>把当前生命补齐到上限（新开档或升级后调用）。</summary>
        public void FillHp() => currentHp = MaxHp;

        public Equipment GetEquipment(EquipmentSlot s)
        {
            switch (s)
            {
                case EquipmentSlot.Weapon: return weapon;
                case EquipmentSlot.Armor: return armor;
                case EquipmentSlot.Helmet: return helmet;
                case EquipmentSlot.Boots: return boots;
                case EquipmentSlot.Accessory: return accessory;
                default: return null;
            }
        }

        public void SetEquipment(Equipment e)
        {
            if (e == null) return;
            switch (e.slot)
            {
                case EquipmentSlot.Weapon: weapon = e; break;
                case EquipmentSlot.Armor: armor = e; break;
                case EquipmentSlot.Helmet: helmet = e; break;
                case EquipmentSlot.Boots: boots = e; break;
                case EquipmentSlot.Accessory: accessory = e; break;
            }
        }

        /// <summary>
        /// 槽位是否已解锁：武器/衣服天生可用；头盔 3-10、鞋 6-10、饰品 11-10。
        /// 用「首通记录 OR 章节进度」兜底旧档——早期存档可能没有完整 clearedStages。
        /// </summary>
        public bool IsEquipmentSlotUnlocked(EquipmentSlot slot)
        {
            switch (slot)
            {
                case EquipmentSlot.Weapon:
                case EquipmentSlot.Armor:
                    return true;
                case EquipmentSlot.Helmet:
                    return maxChapterUnlocked > GameConfig.HelmetUnlockChapter
                        || IsStageFirstCleared(GameConfig.HelmetUnlockChapter, GameConfig.StagesPerChapter);
                case EquipmentSlot.Boots:
                    return maxChapterUnlocked > GameConfig.BootsUnlockChapter
                        || IsStageFirstCleared(GameConfig.BootsUnlockChapter, GameConfig.StagesPerChapter);
                case EquipmentSlot.Accessory:
                    // 11-10 是终局，通关后不会推进到第 12 章，只能靠首通记录判断。
                    return IsStageFirstCleared(GameConfig.AccessoryUnlockChapter, GameConfig.StagesPerChapter);
                default:
                    return false;
            }
        }

        /// <summary>当前已解锁的装备槽数量（掉落率与智能掉落按它算）。</summary>
        public int UnlockedEquipmentSlotCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < EquipmentSlots.Count; i++)
                    if (IsEquipmentSlotUnlocked(EquipmentSlots.All[i])) n++;
                return n;
            }
        }

        /// <summary>当前巅峰等级升到下一级所需经验；满巅峰返回 0。</summary>
        public int ParagonExpToNext => IsMaxParagon ? 0 : GameConfig.ParagonExpToNext(paragonLevel);

        public bool IsMaxParagon => paragonLevel >= GameConfig.MaxParagonLevel;

        /// <summary>
        /// 增加经验并处理升级。满级（Lv.100）之前走普通等级；满级之后经验转为巅峰经验。
        /// 返回本次实际升了几级（含巅峰），供日志使用。
        /// </summary>
        public int AddExp(int amount)
        {
            if (amount <= 0) return 0;

            if (IsMaxLevel)
            {
                // 已满级：全部转成巅峰经验。
                return AddParagonExp(amount);
            }

            exp += amount;
            int gained = 0;

            // 用 while 支持一次击杀连升多级；need<=0 时立即停手，
            // 防止热改/探针把经验公式改成 0 或负数后在这里空转。
            while (!IsMaxLevel)
            {
                int need = ExpToNextLevel;
                if (need <= 0 || exp < need) break;
                exp -= need;
                level++;
                gained++;
            }

            if (IsMaxLevel) exp = 0; // 满级后清空经验条
            return gained;
        }

        /// <summary>加巅峰经验并处理升级；返回本次升了几级巅峰。</summary>
        public int AddParagonExp(int amount)
        {
            if (IsMaxParagon || amount <= 0) return 0;

            paragonExp += amount;
            int gained = 0;

            while (!IsMaxParagon)
            {
                int need = ParagonExpToNext;
                if (need <= 0 || paragonExp < need) break;
                paragonExp -= need;
                paragonLevel++;
                gained++;
            }

            if (IsMaxParagon) paragonExp = 0;
            return gained;
        }

        /// <summary>该技能是否已获得（100 级前一个都没有）。</summary>
        public bool IsSkillOwned(SkillId id) => (ownedSkillMask & (1 << (int)id)) != 0;

        /// <summary>已获得的技能数量。</summary>
        public int OwnedSkillCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < SkillCatalog.All.Length; i++)
                    if ((ownedSkillMask & (1 << i)) != 0) count++;
                return count;
            }
        }

        /// <summary>
        /// 随机获得一个尚未拥有的技能（巅峰每 10 级触发一次）。
        /// 返回获得的技能编号；十五个技能全部拥有时返回 (SkillId)(-1)。
        /// </summary>
        public SkillId GrantRandomSkill()
        {
            var pool = new List<SkillId>();
            for (int i = 0; i < SkillCatalog.All.Length; i++)
                if ((ownedSkillMask & (1 << i)) == 0) pool.Add((SkillId)i);

            if (pool.Count == 0) return (SkillId)(-1);

            SkillId pick = pool[UnityEngine.Random.Range(0, pool.Count)];
            ownedSkillMask |= 1 << (int)pick;
            return pick;
        }

        /// <summary>
        /// 从「尚未拥有」的技能里随机抽 count 个作为候选（互不重复）。
        /// 剩余不足 count 个时返回全部剩余，全拥有时返回空表。
        /// </summary>
        public List<SkillId> RollSkillCandidates(int count)
        {
            var pool = new List<SkillId>();
            for (int i = 0; i < SkillCatalog.All.Length; i++)
                if ((ownedSkillMask & (1 << i)) == 0) pool.Add((SkillId)i);

            var result = new List<SkillId>();
            for (int i = 0; i < count && pool.Count > 0; i++)
            {
                int k = UnityEngine.Random.Range(0, pool.Count);
                result.Add(pool[k]);
                pool.RemoveAt(k);   // 抽走就移出池子，保证候选不重复
            }
            return result;
        }

        /// <summary>授予指定技能（三选一的落点）。已拥有或编号非法时返回 false。</summary>
        public bool GrantSkill(SkillId id)
        {
            int index = (int)id;
            if (index < 0 || index >= SkillCatalog.All.Length) return false;
            if ((ownedSkillMask & (1 << index)) != 0) return false;

            ownedSkillMask |= 1 << index;
            return true;
        }

        /// <summary>按精英 / Boss 档次判断是否为「巨人杀手」的目标。</summary>
        public static bool IsEliteOrBoss(MonsterTier tier) => tier != MonsterTier.Normal;

        // ---- 技能槽读写（0~1 主动、2~3 被动、4~5 触发；-1 表示空）----

        public int GetSkillSlot(int index)
        {
            switch (index)
            {
                case 0: return skillActive0;
                case 1: return skillActive1;
                case 2: return skillPassive0;
                case 3: return skillPassive1;
                case 4: return skillTrigger0;
                case 5: return skillTrigger1;
                default: return -1;
            }
        }

        public void SetSkillSlot(int index, int skillId)
        {
            switch (index)
            {
                case 0: skillActive0 = skillId; break;
                case 1: skillActive1 = skillId; break;
                case 2: skillPassive0 = skillId; break;
                case 3: skillPassive1 = skillId; break;
                case 4: skillTrigger0 = skillId; break;
                case 5: skillTrigger1 = skillId; break;
            }
        }

        /// <summary>某个技能当前装在哪个槽位；没装返回 -1。</summary>
        public int FindSkillSlot(int skillId)
        {
            for (int i = 0; i < SkillSlotCount; i++) if (GetSkillSlot(i) == skillId) return i;
            return -1;
        }
    }
}

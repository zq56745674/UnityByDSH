using UnityEngine;

namespace AutoBattle
{
    /// <summary>
    /// 外部配置（Assets/Resources/game_config.json）的数据载体。
    /// 字段名必须与 GameConfig 的静态字段一一对应，由 GameConfig.Apply() 反射覆盖；
    /// 这里的默认值就是原先的 const 值，JSON 缺失某项时保持默认。
    /// </summary>
    [System.Serializable]
    public class GameConfigData
    {
        public int MaxLevel = 100;
        public float DamageReductionBase = 100f;
        public float DamageReductionPerLevel = 20f;
        public float DamageRandomMin = 0.9f;
        public float DamageRandomMax = 1.1f;
        public int MinDamage = 1;
        public float MonsterRespawnDelay = 0.5f;
        public float PlayerReviveDelay = 1.5f;
        public float AttackIntervalBase = 2.0f;
        public float AttackSpeedDivisor = 20f;
        public float AttackSpeedSoftCap = 3.5f;
        public float AttackSpeedHardCap = 5.0f;
        public float MinAttackInterval = 0.2f;
        public int PlayerBaseMaxHp = 100;
        public int PlayerBaseAttack = 10;
        public int PlayerBaseDefense = 5;
        public int PlayerBaseSpeed = 10;
        public int PlayerMaxHpPerLevel = 35;
        public int PlayerAttackPerLevel = 3;
        public int PlayerDefensePerLevel = 2;
        public int PlayerSpeedPerLevel = 1;
        public float PlayerBaseCritChance = 0.05f;
        public float PlayerBaseCritDamage = 1.5f;
        public float MaxCritChance = 0.95f;
        public int MonsterBaseMaxHp = 55;
        public int MonsterMaxHpPerLevel = 20;
        public int MonsterBaseAttack = 9;
        public float MonsterAttackPerLevel = 3.0f;
        public int MonsterBaseDefense = 4;
        public float MonsterDefensePerLevel = 2.4f;
        public int MonsterBaseSpeed = 10;
        public float MonsterSpeedPerLevel = 0.3f;
        public int MaxTierLevel = 110;
        public float MaxTierHpFactor = 3.0f;
        public float MaxTierAttackFactor = 3.0f;
        public float MaxTierDefenseFactor = 3.0f;
        public float FinalBossStatFactor = 1.6f;
        public float MaxTierSpeedFactor = 2.0f;
        public float AffixChance = 0.30f;
        public float LifestealAffixPercent = 0.30f;
        public float ShieldAffixPercent = 0.30f;
        public float PoisonAffixPercent = 0.02f;
        public float DropChance = 0.30f;
        public float DropChancePerExtraSlot = 0.05f;
        public float SmartDropChance = 0.30f;
        public int SellBaseGold = 5;
        public float SellGoldPerLevel = 3f;
        public float EnhanceBonusPerLevel = 0.03f;
        public int EnhanceMaxLevel = 15;
        public int EnhanceBaseCost = 150;
        public int EnhanceCostPerLevel = 300;
        public int ReforgeBaseCost = 200;
        public int ReforgeCostPerLevel = 14;
        public float ReforgeCostGrowth = 0.9f;

        /// <summary>锁词条的单价倍率（见 GameConfig.ReforgeLockFactor）。</summary>
        public float ReforgeLockFactor = 3f;
        public int WeaponBaseAttack = 5;
        public float WeaponAttackPerLevel = 2.0f;
        public float WeaponBaseCritChance = 0.01f;
        public float WeaponCritChancePerLevel = 0.001f;
        public float WeaponBaseCritDamage = 0.10f;
        public float WeaponCritDamagePerLevel = 0.00195f;
        public int ArmorBaseDefense = 3;
        public float ArmorDefensePerLevel = 1.2f;
        public int ArmorBaseHp = 20;
        public float ArmorHpPerLevel = 8f;
        public float HelmetHpFactor = 0.25f;
        public float HelmetDefenseFactor = 0.12f;
        public int BootsBaseSpeed = 3;
        public float BootsSpeedPerLevel = 0.02f;
        public float BootsSpeedPerQuality = 0.75f;
        public float BootsBaseHaste = 0.012f;
        public float BootsHastePerLevel = 0.00005f;
        public float BootsHastePerQuality = 0.15f;
        public float AccessoryBaseCritDamage = 0.012f;
        public float AccessoryCritDamagePerLevel = 0.0002f;
        public float AccessoryBaseArmorPen = 0.005f;
        public float AccessoryArmorPenPerLevel = 0.00005f;
        public float MinorAffixScale = 0.5f;
        public int HelmetUnlockChapter = 3;
        public int BootsUnlockChapter = 6;
        public int AccessoryUnlockChapter = 11;
        public float EquipAffixLevelScale = 0.005f;
        public int ScoreStandardLevel = 100;
        public bool HealBeforeFight = false;
        public int PotionMaxCount = 5;
        public float PotionHealPercent = 0.30f;
        public int PotionPriceBase = 20;
        public int PotionPricePerLevel = 5;
        public float PotionAutoUseHpPercent = 0.40f;
        public float PotionCooldown = 3f;
        public float ShopDuration = 3f;
        public int MaxParagonLevel = 1000;
        public int ParagonAttackPerLevel = 1;
        public int ParagonDefensePerLevel = 1;
        public int ParagonHpPerLevel = 50;
        public int ParagonExpBase = 2000;
        public int ParagonExpPerLevel = 200;
        public float ParagonExpQuadratic = 20f;
        public int ParagonPassiveEvery = 10;
        public int SkillSlotsPerKind = 2;
        public int SkillChoiceCount = 3;
        public float SkillGlobalCooldown = 1f;
        public float SkillLevelScale = 0.0f;
        public float AutoSaveInterval = 5f;
        public int MaxLogEntries = 60;

        // ---- 关卡 / 章节（步骤 4a）----
        public int ChapterCount = 11;
        public float ChapterPressureBase = 1.0f;
        public float ChapterPressurePerChapter = 0.055f;
        public float ChapterBossPressurePerChapter = 0.04f;
        public int StagesPerChapter = 10;
        public int ChapterMonsterLevelStep = 10;
        public int StageKillTargetBase = 3;
        public int StageKillTargetMaxBonus = 3;
        public int FirstBossChapter = 2;
        public float StageClearDelay = 3f;

        // ---- 首通奖励（步骤 4c）----
        public int FirstClearGoldBase = 120;
        public int FirstClearGoldPerChapter = 80;
        public int ChapterClearGoldBonus = 600;
        public float BannerDuration = 3.2f;

        // ---- 无尽模式 ----
        public int EndlessBaseMonsterLevel = 110;
        public int EndlessMonsterLevelStep = 6;
        public int EndlessKillTargetBase = 3;
        public int EndlessKillTargetEvery = 5;
        public int EndlessKillTargetMax = 6;
        public int EndlessBossEvery = 5;
        public float EndlessBaseFactor = 3.4f;
        public int EndlessFloorGoldBase = 300;
        public int EndlessFloorGoldPerFloor = 40;
        public int EndlessFirstClearGoldBase = 1000;
        public int EndlessFirstClearGoldPerFloor = 150;
        public float EndlessFloorClearDelay = 2.0f;
        public int EndlessCheckpointOffset = 4;
        public int EndlessDropLevelCap = 150;

        // ---- 转生 ----
        public int RebirthUnlockParagon = 100;
        public float RebirthBattlePerRank = 0.01f;
        public float RebirthExpPerRank = 0.60f;
        public float RebirthGoldPerRank = 0.20f;
        public float RebirthDropPerRank = 0.02f;
        public int RebirthLegacyLevelPerRank = 3;
        public int RebirthAlchemyPotionPerRank = 1;
        public int RebirthAlchemyGoldPerRank = 500;
        public int RebirthBattleCostBase = 3;
        public int RebirthGrowthCostBase = 2;
        public int RebirthFortuneCostBase = 2;
        public int RebirthLegacyCostBase = 4;
        public int RebirthAlchemyCostBase = 1;
        public int RebirthMarkParagonStep = 100;
        public int RebirthMarkFloorStep = 50;
        public int RebirthMarkGoldBase = 100000;
    }
}

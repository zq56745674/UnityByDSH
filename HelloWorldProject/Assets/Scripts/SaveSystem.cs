using System;
using System.Collections.Generic;
using UnityEngine;

namespace AutoBattle
{
    /// <summary>
    /// 存档系统（要求 17）。用 PlayerPrefs 存一份 JSON：
    /// 编辑器里退出/重新进入 Play 模式、或 WebGL 构建下刷新浏览器，都能恢复进度。
    /// </summary>
    public static class SaveSystem
    {
        /// <summary>
        /// 当前存档格式版本。
        /// v2：装备加入 qualityMultiplier 与暴击字段；
        /// v3：加入生命药剂持有数与自动购买开关；
        /// v4：加入巅峰等级与六个技能槽；
        /// v5：加入关卡 / 章节进度；
        /// v6：加入首通记录（clearedStages）。
        /// 旧档缺 version 字段时读到 0，据此判断需要迁移。
        /// </summary>
        private const int CurrentSaveVersion = 6;

        /// <summary>装备字段迁移的版本线：低于此值的装备需要反推品质倍率。</summary>
        private const int EquipmentFieldVersion = 2;

        /// <summary>解析失败时，原始 JSON 挪到这个 Key 下留底，避免被下一次自动存档覆盖。</summary>
        public const string BackupSuffix = "_backup";

        /// <summary>
        /// 存档槽数量。
        /// 槽 0 沿用原来的单档 Key（<see cref="GameConfig.SaveKey"/>），槽 1、2 用 `_slot1` / `_slot2`。
        /// 这样加多槽之前的老存档天然就是槽 0，不需要迁移代码。
        /// </summary>
        public const int SlotCount = 3;

        private const string SlotKeyPrefix = "_slot";

        /// <summary>记住「上次用的是哪个槽」的 Key（它本身不是存档）。</summary>
        private const string CurrentSlotKey = GameConfig.SaveKey + "_current";

        /// <summary>当前槽（0 起）。只在本次运行内切换，另外写进 PlayerPrefs 以便下次启动还原。</summary>
        public static int CurrentSlot { get; private set; }

        /// <summary>槽位对应的 PlayerPrefs Key。</summary>
        public static string KeyOf(int slot)
        {
            slot = Mathf.Clamp(slot, 0, SlotCount - 1);
            return slot == 0 ? GameConfig.SaveKey : GameConfig.SaveKey + SlotKeyPrefix + slot;
        }

        /// <summary>该槽有没有存档（不解析内容）。</summary>
        public static bool HasSaveIn(int slot) => PlayerPrefs.HasKey(KeyOf(slot));

        /// <summary>某槽的预览信息。供选档界面显示，不修改任何状态。</summary>
        public struct SlotInfo
        {
            public bool exists;
            public bool broken;
            public int level;
            public int paragonLevel;
            public int chapter;
            public int stage;
            public int gold;
            public int clearedCount;
            public int version;
        }

        /// <summary>
        /// 读取某槽的摘要用于界面展示。
        /// 刻意不做 Load 里的那套迁移/备份：预览不该产生副作用，坏档只标记 broken。
        /// </summary>
        public static SlotInfo Describe(int slot)
        {
            var info = new SlotInfo();
            if (!HasSaveIn(slot)) return info;

            string json = PlayerPrefs.GetString(KeyOf(slot), string.Empty);
            if (string.IsNullOrEmpty(json))
            {
                // Key 在、内容空：算存在但读不出来。
                info.exists = true;
                info.broken = true;
                return info;
            }

            SaveData data;
            try
            {
                data = JsonUtility.FromJson<SaveData>(json);
            }
            catch (Exception)
            {
                info.exists = true;
                info.broken = true;
                return info;
            }

            if (data == null)
            {
                info.exists = true;
                info.broken = true;
                return info;
            }

            info.exists = true;
            info.version = data.version;
            info.level = data.level;
            info.paragonLevel = data.paragonLevel;
            info.gold = data.gold;
            info.clearedCount = data.clearedStages != null ? data.clearedStages.Count : 0;

            // 与 Load 一致：旧档没有关卡字段时按 1-1 显示。
            bool hasStage = data.version >= 5;
            info.chapter = hasStage ? Mathf.Clamp(data.chapterIndex, 1, GameConfig.ChapterCount) : 1;
            info.stage = hasStage ? Mathf.Clamp(data.stageIndex, 1, GameConfig.StagesPerChapter) : 1;
            return info;
        }

        /// <summary>
        /// 把当前槽切到 slot，并记住这个选择（下次启动直接进这个槽）。
        /// 只改「指向哪个槽」，不动 PlayerData——加载由调用方决定。
        /// </summary>
        public static void SelectSlot(int slot)
        {
            CurrentSlot = Mathf.Clamp(slot, 0, SlotCount - 1);
            PlayerPrefs.SetInt(CurrentSlotKey, CurrentSlot);
            PlayerPrefs.Save();
        }

        /// <summary>启动时还原上次用的槽。</summary>
        public static void RestoreCurrentSlot()
        {
            CurrentSlot = Mathf.Clamp(PlayerPrefs.GetInt(CurrentSlotKey, 0), 0, SlotCount - 1);
        }

        /// <summary>删除某槽的存档（连同它的备份）。</summary>
        public static void ClearSlot(int slot)
        {
            PlayerPrefs.DeleteKey(KeyOf(slot));
            PlayerPrefs.DeleteKey(KeyOf(slot) + BackupSuffix);
            PlayerPrefs.Save();
        }

        /// <summary>实际落盘的字段集合（只存必要状态，属性由等级推导，不冗余存储）。</summary>
        [Serializable]
        public class SaveData
        {
            /// <summary>存档格式版本：0 = 装备还没有暴击与浮动倍率的旧档。</summary>
            public int version;

            public int level;
            public int exp;
            public int gold;

            /// <summary>v3 起：生命药剂持有数。</summary>
            public int potionCount;

            /// <summary>v3 起：自动购买开关。</summary>
            public bool autoBuyPotion;

            /// <summary>v4 起：巅峰等级 / 巅峰经验 / 已获得技能的位掩码。</summary>
            public int paragonLevel;
            public int paragonExp;
            public int ownedSkillMask;

        // v5：关卡 / 章节进度
        public int chapterIndex;
        public int stageIndex;
        public int stageKillCount;
        public int maxChapterUnlocked;

        /// <summary>v6 起：已首通的关卡编码（chapter * 100 + stage）。</summary>
        public List<int> clearedStages;
            public int pendingSkillPicks;

            /// <summary>v4 起：六个技能槽（-1 表示空）。</summary>
            public int skillActive0 = -1;
            public int skillActive1 = -1;
            public int skillPassive0 = -1;
            public int skillPassive1 = -1;
            public int skillTrigger0 = -1;
            public int skillTrigger1 = -1;

            public int currentHp;
            public Equipment weapon;
            public Equipment armor;
        }

        public static bool HasSave => HasSaveIn(CurrentSlot);

        /// <summary>写入存档。</summary>
        public static void Save(PlayerData player)
        {
            if (player == null) return;

            var data = new SaveData
            {
                version = CurrentSaveVersion,
                level = player.level,
                exp = player.exp,
                gold = player.gold,
                potionCount = player.potionCount,
                autoBuyPotion = player.autoBuyPotion,
                paragonLevel = player.paragonLevel,
                paragonExp = player.paragonExp,
                ownedSkillMask = player.ownedSkillMask,
                chapterIndex = player.chapterIndex,
                stageIndex = player.stageIndex,
                stageKillCount = player.stageKillCount,
                maxChapterUnlocked = player.maxChapterUnlocked,
                clearedStages = player.clearedStages != null
                    ? new List<int>(player.clearedStages)
                    : new List<int>(),
                pendingSkillPicks = player.pendingSkillPicks,
                skillActive0 = player.skillActive0,
                skillActive1 = player.skillActive1,
                skillPassive0 = player.skillPassive0,
                skillPassive1 = player.skillPassive1,
                skillTrigger0 = player.skillTrigger0,
                skillTrigger1 = player.skillTrigger1,
                currentHp = player.currentHp,
                weapon = player.weapon,
                armor = player.armor,
            };

            try
            {
                PlayerPrefs.SetString(KeyOf(CurrentSlot), JsonUtility.ToJson(data));
                PlayerPrefs.Save();
            }
            catch (Exception e)
            {
                // 存档失败（PlayerPrefs 超限、序列化异常）不能把异常抛给 OnApplicationQuit，
                // 那会打断退出流程。记一条警告即可，游戏本身可以继续跑。
                Debug.LogWarning($"[AutoBattle] 存档写入失败：{e.Message}");
            }
        }

        /// <summary>
        /// 读取存档并写入 player。有存档返回 true 并填充，无存档返回 false。
        /// 读取后会做一次合法性收敛（等级夹取、生命夹取）。
        /// </summary>
        public static bool Load(PlayerData player)
        {
            if (player == null || !HasSaveIn(CurrentSlot)) return false;

            string json = PlayerPrefs.GetString(KeyOf(CurrentSlot), string.Empty);
            if (string.IsNullOrEmpty(json)) return false;

            SaveData data;
            try
            {
                data = JsonUtility.FromJson<SaveData>(json);
            }
            catch (Exception e)
            {
                // 坏档不能就地丢弃：调用方拿到 false 会开新档，5 秒后的自动存档就把原档覆盖了。
                // 先把原始 JSON 原样挪到备份 Key，至少还能人工捞回来。
                string backupKey = KeyOf(CurrentSlot) + BackupSuffix;
                PlayerPrefs.SetString(backupKey, json);
                PlayerPrefs.Save();
                Debug.LogWarning($"[AutoBattle] 存档解析失败，原档已备份到 {backupKey}：{e.Message}");
                return false;
            }

            if (data == null) return false;

            // 比当前版本更新的档（降级运行）不能按旧语义读：未知字段会被 JsonUtility 静默忽略，
            // 已知字段可能已被新版本改写过。与坏档同样处理——备份原档再拒绝读取。
            if (data.version > CurrentSaveVersion)
            {
                string newerKey = KeyOf(CurrentSlot) + BackupSuffix;
                PlayerPrefs.SetString(newerKey, json);
                PlayerPrefs.Save();
                Debug.LogWarning($"[AutoBattle] 存档版本 {data.version} 高于当前支持的 {CurrentSaveVersion}，"
                    + $"已备份到 {newerKey} 并忽略该档。");
                return false;
            }

            player.level = Mathf.Clamp(data.level, 1, GameConfig.MaxLevel);
            player.exp = Mathf.Max(0, data.exp);
            // 旧版本存档没有 gold 字段，JsonUtility 会给 0，这里一并夹住负数。
            player.gold = Mathf.Max(0, data.gold);

            // v3 才有的药剂字段：持有数直接读（旧档缺失即 0）；
            // 自动购买开关的默认值是 true，所以只有 v3 及以后的存档才覆盖它。
            player.potionCount = Mathf.Clamp(data.potionCount, 0, GameConfig.PotionMaxCount);
            if (data.version >= 3) player.autoBuyPotion = data.autoBuyPotion;

            // v4 才有的巅峰与技能字段。技能槽的字段初值是 -1，旧档缺失时自然为空。
            player.paragonLevel = Mathf.Clamp(data.paragonLevel, 0, GameConfig.MaxParagonLevel);
            player.paragonExp = Mathf.Max(0, data.paragonExp);
            // 技能位掩码只保留真实存在的技能位；越界 bit 会让「已获得技能数」虚高。
            int allSkillMask = (1 << SkillCatalog.All.Length) - 1;
            player.ownedSkillMask = data.ownedSkillMask & allSkillMask;
            player.pendingSkillPicks = Mathf.Max(0, data.pendingSkillPicks);

            // v5 才有的关卡进度。旧档缺这些字段时 JsonUtility 会给 0，
            // 所以不能只看值，必须按版本号决定是「迁移成 1-1」还是「读档」。
            bool hasStage = data.version >= 5;
            player.chapterIndex = hasStage
                ? Mathf.Clamp(data.chapterIndex, 1, GameConfig.ChapterCount) : 1;
            player.stageIndex = hasStage
                ? Mathf.Clamp(data.stageIndex, 1, GameConfig.StagesPerChapter) : 1;
            player.stageKillCount = hasStage ? Mathf.Max(0, data.stageKillCount) : 0;
            // 手改档可能把击杀数写得比目标大很多：进游戏后第一次结算就会多算首通/卡关。
            player.stageKillCount = Mathf.Clamp(player.stageKillCount, 0, player.CurrentStage.killTarget);
            player.maxChapterUnlocked = hasStage
                ? Mathf.Clamp(data.maxChapterUnlocked, 1, GameConfig.ChapterCount) : 1;
            // 一致性兜底：字段缺失（0）或手改档都可能得到「当前章 > 已解锁章」。
            // 不修的话选关面板会显示错乱，下面的回填也会把当前章当成未解锁而漏补。
            player.maxChapterUnlocked = Mathf.Max(player.maxChapterUnlocked, player.chapterIndex);

            // v6 才有的首通记录。旧档（v5 及以前）没有这份名单，但进度本身证明了
            // 「当前关之前的都打通过」，直接按进度补一份，免得老玩家重刷时白拿一遍首通奖励。
            // 当前关不算——它可能只是走到这里，还没真打完。
            player.clearedStages = new List<int>();
            if (data.version >= 6 && data.clearedStages != null)
            {
                // 只收合法且不重复的关卡：重复项会让首通统计虚高，越界项会污染选关/进度判断。
                var seenStages = new HashSet<int>();
                int maxCleared = Mathf.Max(1, GameConfig.ChapterCount * GameConfig.StagesPerChapter);
                for (int i = 0; i < data.clearedStages.Count && player.clearedStages.Count < maxCleared; i++)
                {
                    int key = data.clearedStages[i];
                    int c = PlayerData.ChapterOfKey(key);
                    int s = PlayerData.StageOfKey(key);
                    if (key <= 0 || c < 1 || c > GameConfig.ChapterCount
                        || s < 1 || s > GameConfig.StagesPerChapter) continue;
                    if (seenStages.Add(key)) player.clearedStages.Add(key);
                }
            }
            else
            {
                // 判断依据必须是 maxChapterUnlocked（已解锁章 = 通关章末推进来的），
                // 不能用 chapterIndex：玩家可以回头刷旧章，那时 chapterIndex 会小于已解锁章，
                // 把「后面那些早已整章通过的章」误判成「只打到当前关」，导致重刷时重复发首通奖励。
                for (int c = 1; c <= player.maxChapterUnlocked; c++)
                {
                    int last;
                    if (c < player.maxChapterUnlocked) last = GameConfig.StagesPerChapter;
                    else if (c == player.chapterIndex) last = player.stageIndex - 1;
                    else last = 0;
                    for (int s = 1; s <= last; s++) player.clearedStages.Add(PlayerData.StageKey(c, s));
                }
            }
            player.skillActive0 = data.skillActive0;
            player.skillActive1 = data.skillActive1;
            player.skillPassive0 = data.skillPassive0;
            player.skillPassive1 = data.skillPassive1;
            player.skillTrigger0 = data.skillTrigger0;
            player.skillTrigger1 = data.skillTrigger1;
            SanitizeSkillState(player);

            player.weapon = data.weapon;
            player.armor = data.armor;

            // 旧档（version < 2）的装备是在暴击与浮动倍率之前生成的。
            // 注意 JsonUtility 对 JSON 里缺失的字段会保留 C# 字段初值（qualityMultiplier 初值是 1），
            // 所以「值为 0/1」判断不出缺失，必须用存档版本号来决定是否迁移。
            bool legacy = data.version < EquipmentFieldVersion;
            FixupEquipment(player.weapon, EquipmentSlot.Weapon, legacy);
            FixupEquipment(player.armor, EquipmentSlot.Armor, legacy);

            // 生命必须夹在当前等级的真实上限内（装备变动后上限会变）。
            player.currentHp = Mathf.Clamp(data.currentHp, 1, Mathf.Max(1, player.MaxHp));

            return true;
        }

        /// <summary>
        /// 技能状态兜底：清掉越界 / 类别不符 / 未拥有 / 重复装配的技能槽，
        /// 并把待选次数夹到「还没获得的技能数」以内。
        /// </summary>
        private static void SanitizeSkillState(PlayerData player)
        {
            int allMask = (1 << SkillCatalog.All.Length) - 1;
            player.ownedSkillMask &= allMask;

            int perKind = Mathf.Max(1, GameConfig.SkillSlotsPerKind);
            var used = new bool[SkillCatalog.All.Length];
            for (int i = 0; i < 6; i++)
            {
                int id = player.GetSkillSlot(i);
                if (id < 0 || id >= SkillCatalog.All.Length)
                {
                    player.SetSkillSlot(i, -1);
                    continue;
                }

                SkillKind expected = (SkillKind)Mathf.Clamp(i / perKind, 0, 2);
                bool owned = (player.ownedSkillMask & (1 << id)) != 0;
                if (SkillCatalog.All[id].kind != expected || !owned || used[id])
                {
                    player.SetSkillSlot(i, -1);
                    continue;
                }
                used[id] = true;
            }

            int ownedCount = 0;
            for (int i = 0; i < SkillCatalog.All.Length; i++)
                if ((player.ownedSkillMask & (1 << i)) != 0) ownedCount++;
            player.pendingSkillPicks = Mathf.Clamp(
                player.pendingSkillPicks, 0, Mathf.Max(0, SkillCatalog.All.Length - ownedCount));
        }

        /// <summary>
        /// 旧存档兼容与坏档兜底：补齐旧字段，并把装备的品质 / 等级 / 强化 / 词条夹到合法范围。
        /// slot 由调用方按武器/衣服给出——手改档把两件装备的 slot 写反时，评分与穿脱都会错位。
        /// </summary>
        private static void FixupEquipment(Equipment e, EquipmentSlot expectedSlot, bool legacy)
        {
            if (e == null) return;

            e.slot = expectedSlot;
            if ((int)e.quality < 0 || (int)e.quality >= GameConfig.QualityNames.Length)
                e.quality = EquipmentQuality.Common;
            e.level = Mathf.Clamp(e.level, 1, 9999);

            if (legacy) e.qualityMultiplier = EstimateMultiplier(e);
            if (float.IsNaN(e.qualityMultiplier) || float.IsInfinity(e.qualityMultiplier) || e.qualityMultiplier <= 0f)
                e.qualityMultiplier = 1f;
            e.qualityMultiplier = Mathf.Clamp(e.qualityMultiplier, 0.01f, 1000f);

            e.enhanceLevel = Mathf.Clamp(e.enhanceLevel, 0, Mathf.Max(0, GameConfig.EnhanceMaxLevel));
            e.reforgeCount = Mathf.Max(0, e.reforgeCount);
            e.attack = Mathf.Max(0, e.attack);
            e.defense = Mathf.Max(0, e.defense);
            e.hp = Mathf.Max(0, e.hp);
            e.critChance = SanitizeStat(e.critChance);
            e.critDamage = SanitizeStat(e.critDamage);
            e.affixCritChance = SanitizeStat(e.affixCritChance);
            e.affixCritDamage = SanitizeStat(e.affixCritDamage);
            e.affixAttackSpeed = SanitizeStat(e.affixAttackSpeed);
            e.affixAllStats = SanitizeStat(e.affixAllStats);
            e.affixArmorPen = SanitizeStat(e.affixArmorPen);
        }

        private static float SanitizeStat(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) return 0f;
            return Mathf.Max(0f, value);
        }

        /// <summary>
        /// 反推旧装备的品质倍率：生成时倍率已固化进属性，所以用「实际属性 ÷ 同等级基础属性」还原。
        /// 武器看攻击；衣服取防御与生命两次推算中较大的一个。推不出来（属性为 0）时按 1 处理。
        /// </summary>
        private static float EstimateMultiplier(Equipment e)
        {
            int steps = Mathf.Max(0, e.level - 1);
            float guess = 0f;

            if (e.slot == EquipmentSlot.Weapon)
            {
                float baseAttack = GameConfig.WeaponBaseAttack + GameConfig.WeaponAttackPerLevel * steps;
                if (baseAttack > 0f) guess = e.attack / baseAttack;
            }
            else
            {
                float baseDefense = GameConfig.ArmorBaseDefense + GameConfig.ArmorDefensePerLevel * steps;
                float baseHp = GameConfig.ArmorBaseHp + GameConfig.ArmorHpPerLevel * steps;
                if (baseDefense > 0f) guess = e.defense / baseDefense;
                if (baseHp > 0f) guess = Mathf.Max(guess, e.hp / baseHp);
            }

            return guess > 0f ? guess : 1f;
        }

        /// <summary>清空当前槽的存档（重置按钮用，要求 16）。</summary>
        public static void Clear()
        {
            ClearSlot(CurrentSlot);
        }
    }
}

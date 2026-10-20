using System;
using System.Collections.Generic;
using UnityEngine;

namespace AutoBattle
{
    /// <summary>
    /// 成就求值器。求值是事件驱动的：所有会改变指标的入口在动作完成后调用
    /// <see cref="Evaluate"/>，不逐帧轮询；同一批动作里多次调用不会重复发奖。
    ///
    /// Live 求值会发固定金币并触发 toast；LoadReconcile 只把「按存档已能证明达成」的
    /// 成就补进 unlocked，不发钱、不弹提示、不写日志。
    /// </summary>
    public static class AchievementSystem
    {
        /// <summary>Live 解锁：参数是该成就与本次实际到账金币（余额满时可能为 0）。</summary>
        public static event Action<AchievementDef, int> Unlocked;

        /// <summary>切档 / 重置 / 转生 / 读档：界面应清空尚未播完的 toast 队列。</summary>
        public static event Action ContextReset;

        /// <summary>unlocked 列表的版本号；UI 用它判断成就面板是否需要重绘。</summary>
        public static int Version { get; private set; }

        /// <summary>有 Live 解锁尚未确认落盘；自动存档失败时保留，下次再试。</summary>
        public static bool HasUnsavedUnlocks { get; private set; }

        /// <summary>
        /// 按目录顺序求值。返回本次新解锁的条数。
        /// 只在真实动作成功后调用；读档迁移调用 <see cref="LoadReconcile"/>。
        /// </summary>
        public static int Evaluate(PlayerData player, bool live)
        {
            if (player == null) return 0;
            if (!AchievementCatalog.Loaded && !AchievementCatalog.Reload()) return 0;

            EnsureContainers(player);
            CaptureEquipmentPeaks(player.stats, player);

            int gained = 0;
            IReadOnlyList<AchievementDef> all = AchievementCatalog.All;
            for (int i = 0; i < all.Count; i++)
            {
                AchievementDef def = all[i];
                if (def == null || player.achievements.IsUnlocked(def.id)) continue;
                if (Metric(player, def.metric) < def.target) continue;

                player.achievements.unlocked.Add(def.id);
                Version++;
                HasUnsavedUnlocks = true;
                gained++;

                int actualGold = 0;
                if (live && def.gold > 0) actualGold = player.AddGoldFixed(def.gold);
                if (live) Unlocked?.Invoke(def, actualGold);
            }

            return gained;
        }

        /// <summary>读档核对：只补已能证明的成就，不发钱、不弹提示。</summary>
        public static int LoadReconcile(PlayerData player)
        {
            return Evaluate(player, false);
        }

        /// <summary>
        /// 迁移专用：只把某条成就记进 unlocked，不发钱、不弹提示。
        /// 用于旧档里由结构本身可证明、但求值指标已随重置丢失的条目（如转生过的档）。
        /// </summary>
        public static bool GrantSilent(PlayerData player, string id)
        {
            if (player == null || string.IsNullOrEmpty(id)) return false;
            if (!AchievementCatalog.Loaded && !AchievementCatalog.Reload()) return false;
            EnsureContainers(player);
            if (!AchievementCatalog.Contains(id) || player.achievements.IsUnlocked(id)) return false;

            player.achievements.unlocked.Add(id);
            Version++;
            HasUnsavedUnlocks = true;
            return true;
        }

        /// <summary>存档成功后调用，清掉「待落盘」标记。</summary>
        public static void NotifySaveResult(bool saved)
        {
            if (saved) HasUnsavedUnlocks = false;
        }

        /// <summary>切档 / 重置 / 转生时清空 toast 队列。</summary>
        public static void NotifyContextReset()
        {
            ContextReset?.Invoke();
        }

        /// <summary>为旧档或手改档补齐容器，并清掉结构上不可能的值。</summary>
        public static void EnsureContainers(PlayerData player)
        {
            if (player == null) return;
            if (player.stats == null) player.stats = new PlayerStats();
            if (player.achievements == null) player.achievements = new AchievementState();
            if (player.achievements.unlocked == null)
                player.achievements.unlocked = new List<string>();
            if (player.achievements.schemaVersion <= 0) player.achievements.schemaVersion = 1;

            PlayerStats s = player.stats;
            s.kills = Math.Max(0L, s.kills);
            s.drops = Math.Max(0L, s.drops);
            s.equipped = Math.Max(0L, s.equipped);
            s.sold = Math.Max(0L, s.sold);
            s.goldEarned = Math.Max(0L, s.goldEarned);
            s.deaths = Math.Max(0L, s.deaths);
            s.eliteKills = Math.Max(0L, s.eliteKills);
            s.bossKills = Math.Max(0L, s.bossKills);
            s.enhances = Math.Max(0L, s.enhances);
            s.reforges = Math.Max(0L, s.reforges);
            s.legendaryDrops = Math.Max(0L, s.legendaryDrops);
            s.threeLockReforges = Math.Max(0L, s.threeLockReforges);
            s.bestEnhanceLevel = Mathf.Clamp(s.bestEnhanceLevel, 0, GameConfig.EnhanceMaxLevel);
            s.bestAffixQuality = Mathf.Clamp01(s.bestAffixQuality);
            s.noDeathChapterMask &= 0x700;
        }

        /// <summary>把当前五槽里达到过的峰值记进历史，防止换装后 best_* 倒退。</summary>
        private static void CaptureEquipmentPeaks(PlayerStats stats, PlayerData player)
        {
            if (stats == null || player == null) return;
            for (int i = 0; i < EquipmentSlots.Count; i++)
            {
                Equipment e = player.GetEquipment(EquipmentSlots.All[i]);
                if (e == null) continue;
                stats.bestEnhanceLevel = Mathf.Max(stats.bestEnhanceLevel, e.enhanceLevel);
                stats.bestAffixQuality = Mathf.Max(stats.bestAffixQuality, e.AffixQuality);
            }
        }

        /// <summary>指标当前值。未定义的指标返回 0（目录校验只保证非空，不保证拼写，方便以后扩展）。</summary>
        public static long Metric(PlayerData p, string metric)
        {
            if (p == null || string.IsNullOrEmpty(metric)) return 0;
            EnsureContainers(p);
            PlayerStats s = p.stats;

            switch (metric)
            {
                case "kills": return s.kills;
                case "drops": return s.drops;
                case "equipped": return s.equipped;
                case "sold": return s.sold;
                case "gold_earned": return s.goldEarned;
                case "deaths": return s.deaths;
                case "elite_kills": return s.eliteKills;
                case "boss_kills": return s.bossKills;
                case "enhances": return s.enhances;
                case "reforges": return s.reforges;
                case "legendary_drops": return s.legendaryDrops;
                case "three_lock_reforges": return s.threeLockReforges;
                case "level": return p.level;
                case "paragon": return p.paragonLevel;
                case "cleared_stages": return CountValidCleared(p);
                case "first_stage_cleared":
                    return HasCleared(p, 1, 1) ? 1 : 0;
                case "chapter_clear": return HighestClearedChapter(p);
                case "epic_slots": return CountSlotsAtLeast(p, EquipmentQuality.Epic);
                case "legendary_slots": return CountSlotsAtLeast(p, EquipmentQuality.Legendary);
                case "legendary_seen":
                    return Math.Max(s.legendaryDrops, CountSlotsAtLeast(p, EquipmentQuality.Legendary));
                case "best_enhance": return s.bestEnhanceLevel;
                case "min_enhance": return MinEnhanceLevel(p);
                case "best_reforge_quality": return QualityPercent(s.bestAffixQuality);
                case "min_reforge_quality": return MinReforgeQuality(p);
                case "equipped_skills": return EquippedSkillCount(p);
                case "owned_skills": return OwnedSkillCount(p);
                case "owned_active": return OwnedSkillCountByKind(p, SkillKind.Active);
                case "owned_each_kind_min": return MinOwnedKindCount(p);
                case "no_death_chapter": return (s.noDeathChapterMask & 0x700) != 0 ? 1 : 0;
                case "endless_best": return p.endlessBestFloor;
                default: return 0;
            }
        }

        private static int CountValidCleared(PlayerData p)
        {
            if (p.clearedStages == null) return 0;
            var seen = new HashSet<int>();
            int valid = 0;
            int max = GameConfig.ChapterCount * GameConfig.StagesPerChapter;
            for (int i = 0; i < p.clearedStages.Count && valid < max; i++)
            {
                int key = p.clearedStages[i];
                int c = PlayerData.ChapterOfKey(key);
                int st = PlayerData.StageOfKey(key);
                if (key <= 0 || c < 1 || c > GameConfig.ChapterCount
                    || st < 1 || st > GameConfig.StagesPerChapter) continue;
                if (seen.Add(key)) valid++;
            }
            return valid;
        }

        private static bool HasCleared(PlayerData p, int chapter, int stage)
        {
            if (p.clearedStages == null) return false;
            int key = PlayerData.StageKey(chapter, stage);
            for (int i = 0; i < p.clearedStages.Count; i++)
                if (p.clearedStages[i] == key) return true;
            return false;
        }

        /// <summary>已完整首通的最高章；不要求连续，按「该章 10 关都在名单里」判断。</summary>
        private static int HighestClearedChapter(PlayerData p)
        {
            for (int c = GameConfig.ChapterCount; c >= 1; c--)
            {
                bool all = true;
                for (int st = 1; st <= GameConfig.StagesPerChapter; st++)
                {
                    if (HasCleared(p, c, st)) continue;
                    all = false;
                    break;
                }
                if (all) return c;
            }
            return 0;
        }

        private static int CountSlotsAtLeast(PlayerData p, EquipmentQuality minimum)
        {
            int n = 0;
            for (int i = 0; i < EquipmentSlots.Count; i++)
            {
                Equipment e = p.GetEquipment(EquipmentSlots.All[i]);
                if (e != null && e.quality >= minimum) n++;
            }
            return n;
        }

        private static int MinEnhanceLevel(PlayerData p)
        {
            int min = int.MaxValue;
            for (int i = 0; i < EquipmentSlots.Count; i++)
            {
                Equipment e = p.GetEquipment(EquipmentSlots.All[i]);
                if (e == null) return 0;
                min = Mathf.Min(min, e.enhanceLevel);
            }
            return min == int.MaxValue ? 0 : min;
        }

        private static int MinReforgeQuality(PlayerData p)
        {
            float min = 1f;
            for (int i = 0; i < EquipmentSlots.Count; i++)
            {
                Equipment e = p.GetEquipment(EquipmentSlots.All[i]);
                if (e == null) return 0;
                min = Mathf.Min(min, e.AffixQuality);
            }
            return QualityPercent(min);
        }

        /// <summary>质量换成分数并向下取整；79.99% 不能算成 80%。</summary>
        private static int QualityPercent(float quality)
        {
            return Mathf.Clamp(Mathf.FloorToInt(quality * 100f + 0.0001f), 0, 100);
        }

        private static int EquippedSkillCount(PlayerData p)
        {
            int n = 0;
            var used = new HashSet<int>();
            for (int i = 0; i < PlayerData.SkillSlotCount; i++)
            {
                int id = p.GetSkillSlot(i);
                if (id < 0 || id >= SkillCatalog.All.Length) continue;
                if ((p.ownedSkillMask & (1 << id)) == 0) continue;
                if (used.Add(id)) n++;
            }
            return n;
        }

        private static int OwnedSkillCount(PlayerData p)
        {
            int n = 0;
            for (int i = 0; i < SkillCatalog.All.Length; i++)
                if ((p.ownedSkillMask & (1 << i)) != 0) n++;
            return n;
        }

        private static int OwnedSkillCountByKind(PlayerData p, SkillKind kind)
        {
            int n = 0;
            int mask = p.ownedSkillMask;
            for (int i = 0; i < SkillCatalog.All.Length; i++)
                if ((mask & (1 << i)) != 0 && SkillCatalog.All[i].kind == kind) n++;
            return n;
        }

        private static int MinOwnedKindCount(PlayerData p)
        {
            int active = OwnedSkillCountByKind(p, SkillKind.Active);
            int passive = OwnedSkillCountByKind(p, SkillKind.Passive);
            int trigger = OwnedSkillCountByKind(p, SkillKind.Trigger);
            return Mathf.Min(active, Mathf.Min(passive, trigger));
        }
    }
}

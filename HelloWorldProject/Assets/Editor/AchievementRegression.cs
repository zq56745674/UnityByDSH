using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AutoBattle.EditorTools
{
    /// <summary>
    /// 成就系统 §11 回归：42 条指标边界、质量百分位、金币边界、统计记账、
    /// 无阵亡挑战、存档迁移/异常/清洗/幂等。只读验证，不改玩法数值。
    /// </summary>
    public static class AchievementRegression
    {
        private static readonly StringBuilder Log = new StringBuilder();
        private static int _pass;
        private static int _fail;

        [MenuItem("Tools/测试/成就 §11 回归（核心）")]
        private static void MenuCore() { Debug.Log(RunCore()); }

        [MenuItem("Tools/测试/成就 §11 回归（探针）")]
        private static void MenuProbes() { Debug.Log(RunProbes()); }

        public static string RunCore()
        {
            Reset();
            RunSection("目录", CatalogTests);
            RunSection("42 条指标边界", BoundaryTests);
            RunSection("质量边界", QualityTests);
            RunSection("金币与幂等", GoldTests);
            RunSection("统计记账", StatTests);
            RunSection("无阵亡挑战", NoDeathTests);
            RunSection("转生规则", RebirthTests);
            RunSection("存档", SaveTests);
            return Summary("成就 §11 核心");
        }

        private static string Summary(string title)
        {
            string head = title + " " + (_fail == 0 ? "PASS" : "FAIL")
                + " | 通过 " + _pass + " / 失败 " + _fail;
            return Log.Length == 0 ? head : head + "\n" + Log;
        }

        private static void Reset()
        {
            Log.Length = 0;
            _pass = 0;
            _fail = 0;
        }

        private static void RunSection(string name, Action action)
        {
            Log.AppendLine("== " + name + " ==");
            try { action(); }
            catch (Exception e) { Fail(name + " 异常", e.ToString()); }
        }

        private static void Check(bool ok, string name, string detail = "")
        {
            if (ok) { _pass++; return; }
            Fail(name, detail);
        }

        private static void Fail(string name, string detail)
        {
            _fail++;
            Log.Append("[FAIL] ").Append(name);
            if (!string.IsNullOrEmpty(detail)) Log.Append(" | ").Append(detail);
            Log.AppendLine();
        }

        private static PlayerData Fresh()
        {
            PlayerData p = new PlayerData();
            p.stats = new PlayerStats();
            p.achievements = new AchievementState();
            p.clearedStages = new List<int>();
            p.level = 1;
            p.exp = 0;
            p.gold = 0;
            p.paragonLevel = 0;
            p.paragonExp = 0;
            p.ownedSkillMask = 0;
            p.pendingSkillPicks = 0;
            p.chapterIndex = 1;
            p.stageIndex = 1;
            p.stageKillCount = 0;
            p.maxChapterUnlocked = 1;
            p.endlessBestFloor = 0;
            p.rebirthCount = 0;
            p.rebirthMarks = 0;
            p.rebirthBestParagon = 0;
            p.rebirthBattleRank = 0;
            p.rebirthGrowthRank = 0;
            p.rebirthFortuneRank = 0;
            p.rebirthLegacyRank = 0;
            p.rebirthAlchemyRank = 0;
            p.potionCount = 0;
            p.autoBuyPotion = true;
            p.weapon = null;
            p.armor = null;
            p.helmet = null;
            p.boots = null;
            p.accessory = null;
            p.slotStarterMask = 0;
            for (int i = 0; i < PlayerData.SkillSlotCount; i++) p.SetSkillSlot(i, -1);
            p.currentHp = p.MaxHp;
            return p;
        }

        private static void CatalogTests()
        {
            Check(AchievementCatalog.Loaded, "目录已加载");
            Check(AchievementCatalog.Count == AchievementCatalog.ExpectedCount,
                "条目数 42", "实际 " + AchievementCatalog.Count);
            Check(AchievementCatalog.TotalPoints == AchievementCatalog.ExpectedPoints,
                "总点数 805", "实际 " + AchievementCatalog.TotalPoints);
            Check(AchievementCatalog.TotalGold == AchievementCatalog.ExpectedGold,
                "总金币 43400", "实际 " + AchievementCatalog.TotalGold);
            Check(AchievementCatalog.TitleCount == AchievementCatalog.ExpectedTitles,
                "称号数 7", "实际 " + AchievementCatalog.TitleCount);

            HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> titles = new HashSet<string>(StringComparer.Ordinal);
            int points = 0;
            int gold = 0;
            foreach (AchievementDef def in AchievementCatalog.All)
            {
                Check(!string.IsNullOrEmpty(def.id), "条目 id 非空", def.name);
                Check(ids.Add(def.id), "条目 id 唯一", def.id);
                Check(!string.IsNullOrEmpty(def.name), "条目名称非空", def.id);
                Check(!string.IsNullOrEmpty(def.desc), "条目描述非空", def.id);
                Check(!string.IsNullOrEmpty(def.metric), "条目 metric 非空", def.id);
                Check(def.target > 0, "条目 target > 0", def.id);
                Check(def.points > 0, "条目 points > 0", def.id);
                Check(def.gold >= 0, "条目 gold >= 0", def.id);
                Check(Array.IndexOf(AchievementCatalog.CategoryKeys, def.category) >= 1,
                    "条目分类合法", def.id + " = " + def.category);
                if (!string.IsNullOrEmpty(def.title)) titles.Add(def.title);
                points += def.points;
                gold += def.gold;
            }
            Check(points == AchievementCatalog.ExpectedPoints, "逐条点数合计 805", "实际 " + points);
            Check(gold == AchievementCatalog.ExpectedGold, "逐条金币合计 43400", "实际 " + gold);
            Check(titles.Count == AchievementCatalog.ExpectedTitles, "逐条称号数 7", "实际 " + titles.Count);
            Check(AchievementCatalog.Get("first_kill") != null, "按 id 取条目");
            Check(AchievementCatalog.Contains("no_death_chapter"), "无阵亡条目存在");
        }

        private static void BoundaryTests()
        {
            foreach (AchievementDef def in AchievementCatalog.All)
            {
                PlayerData below = Fresh();
                SetMetric(below, def.metric, def.target - 1);
                AchievementSystem.Evaluate(below, false);
                bool belowUnlocked = below.achievements.IsUnlocked(def.id);

                PlayerData at = Fresh();
                SetMetric(at, def.metric, def.target);
                AchievementSystem.Evaluate(at, false);
                bool atUnlocked = at.achievements.IsUnlocked(def.id);

                Check(!belowUnlocked && atUnlocked, "边界 " + def.id + " (" + def.metric + ")",
                    "target-1=" + (def.target - 1) + " 解锁=" + belowUnlocked
                    + "；target=" + def.target + " 解锁=" + atUnlocked);
            }
        }

        private static void SetMetric(PlayerData p, string metric, int value)
        {
            switch (metric)
            {
                case "kills": p.stats.kills = value; break;
                case "drops": p.stats.drops = value; break;
                case "equipped": p.stats.equipped = value; break;
                case "sold": p.stats.sold = value; break;
                case "gold_earned": p.stats.goldEarned = value; break;
                case "deaths": p.stats.deaths = value; break;
                case "elite_kills": p.stats.eliteKills = value; break;
                case "boss_kills": p.stats.bossKills = value; break;
                case "enhances": p.stats.enhances = value; break;
                case "reforges": p.stats.reforges = value; break;
                case "legendary_drops": p.stats.legendaryDrops = value; break;
                case "three_lock_reforges": p.stats.threeLockReforges = value; break;
                case "level": p.level = Mathf.Clamp(value, 1, GameConfig.MaxLevel); break;
                case "paragon": p.paragonLevel = Mathf.Max(0, value); break;
                case "cleared_stages": SetClearedStages(p, value); break;
                case "first_stage_cleared":
                    p.clearedStages.Clear();
                    if (value >= 1) p.clearedStages.Add(PlayerData.StageKey(1, 1));
                    break;
                case "chapter_clear": SetClearedChapters(p, value); break;
                case "epic_slots": SetSlotsQuality(p, EquipmentQuality.Epic, value); break;
                case "legendary_slots": SetSlotsQuality(p, EquipmentQuality.Legendary, value); break;
                case "legendary_seen": p.stats.legendaryDrops = value; break;
                case "best_enhance": ClearGear(p); p.stats.bestEnhanceLevel = value; break;
                case "min_enhance": SetAllGear(p, value, 0f); break;
                case "best_reforge_quality": ClearGear(p); p.stats.bestAffixQuality = value / 100f; break;
                case "min_reforge_quality": SetAllAffixQuality(p, value / 100f); break;
                case "equipped_skills": SetEquippedSkills(p, value); break;
                case "owned_skills": SetOwnedSkills(p, value); break;
                case "owned_active": SetOwnedActive(p, value); break;
                case "owned_each_kind_min": SetOwnedEachKind(p, value); break;
                case "no_death_chapter": p.stats.noDeathChapterMask = value > 0 ? 0x700 : 0; break;
                case "endless_best": p.endlessBestFloor = Mathf.Max(0, value); break;
                default: throw new ArgumentException("未覆盖的 metric: " + metric);
            }
        }

        private static void SetClearedStages(PlayerData p, int count)
        {
            p.clearedStages.Clear();
            int n = 0;
            for (int c = 1; c <= GameConfig.ChapterCount && n < count; c++)
            {
                int stages = GameConfig.StagesPerChapter;
                for (int s = 1; s <= stages && n < count; s++)
                {
                    p.clearedStages.Add(PlayerData.StageKey(c, s));
                    n++;
                }
            }
        }

        private static void SetClearedChapters(PlayerData p, int chapters)
        {
            p.clearedStages.Clear();
            for (int c = 1; c <= chapters && c <= GameConfig.ChapterCount; c++)
            {
                for (int s = 1; s <= GameConfig.StagesPerChapter; s++)
                    p.clearedStages.Add(PlayerData.StageKey(c, s));
            }
        }

        private static void ClearGear(PlayerData p)
        {
            p.weapon = null;
            p.armor = null;
            p.helmet = null;
            p.boots = null;
            p.accessory = null;
        }

        private static Equipment MakeGear(EquipmentSlot slot, EquipmentQuality quality, int level, int enhance)
        {
            return new Equipment
            {
                slot = slot,
                quality = quality,
                level = level,
                qualityMultiplier = 1f,
                enhanceLevel = enhance,
                affixScale = EquipmentSlots.IsMinor(slot) ? GameConfig.MinorAffixScale : 1f,
                attack = slot == EquipmentSlot.Weapon ? 10 : 0,
                defense = slot == EquipmentSlot.Armor ? 10 : 0,
                hp = slot == EquipmentSlot.Helmet ? 10 : 0,
                speed = slot == EquipmentSlot.Boots ? 1 : 0,
                armorPen = slot == EquipmentSlot.Accessory ? 0.01f : 0f
            };
        }

        private static void SetSlotsQuality(PlayerData p, EquipmentQuality quality, int count)
        {
            ClearGear(p);
            for (int i = 0; i < EquipmentSlots.Count; i++)
            {
                EquipmentQuality q = i < count ? quality : EquipmentQuality.Common;
                p.SetEquipment(MakeGear(EquipmentSlots.All[i], q, 1, 0));
            }
        }

        private static void SetAllGear(PlayerData p, int enhance, float affixQuality)
        {
            ClearGear(p);
            for (int i = 0; i < EquipmentSlots.Count; i++)
            {
                Equipment e = MakeGear(EquipmentSlots.All[i], EquipmentQuality.Epic, 0, enhance);
                if (affixQuality >= 0f) e.affixCritChance = AffixValue(0, affixQuality);
                p.SetEquipment(e);
            }
        }

        private static void SetAllAffixQuality(PlayerData p, float quality)
        {
            SetAllGear(p, 0, quality);
        }

        private static float AffixValue(int index, float quality)
        {
            float min = GameConfig.EquipAffixMin[index];
            float max = GameConfig.EquipAffixMax[index];
            return min + Mathf.Clamp01(quality) * (max - min) + (quality >= 0.8f ? 0.000001f : 0f);
        }

        private static void SetEquippedSkills(PlayerData p, int count)
        {
            p.ownedSkillMask = 0;
            for (int i = 0; i < PlayerData.SkillSlotCount; i++) p.SetSkillSlot(i, -1);
            List<SkillId> chosen = new List<SkillId>();
            SkillKind[] kinds = { SkillKind.Active, SkillKind.Passive, SkillKind.Trigger };
            foreach (SkillKind kind in kinds)
            {
                int taken = 0;
                foreach (SkillDef def in SkillCatalog.All)
                {
                    if (def.kind != kind) continue;
                    chosen.Add(def.id);
                    taken++;
                    if (taken >= 2) break;
                }
            }
            for (int i = 0; i < count && i < chosen.Count && i < PlayerData.SkillSlotCount; i++)
            {
                p.GrantSkill(chosen[i]);
                p.SetSkillSlot(i, (int)chosen[i]);
            }
        }

        private static void SetOwnedSkills(PlayerData p, int count)
        {
            p.ownedSkillMask = 0;
            for (int i = 0; i < PlayerData.SkillSlotCount; i++) p.SetSkillSlot(i, -1);
            int n = 0;
            foreach (SkillDef def in SkillCatalog.All)
            {
                if (n >= count) break;
                p.GrantSkill(def.id);
                n++;
            }
        }

        private static void SetOwnedActive(PlayerData p, int count)
        {
            p.ownedSkillMask = 0;
            for (int i = 0; i < PlayerData.SkillSlotCount; i++) p.SetSkillSlot(i, -1);
            int n = 0;
            foreach (SkillDef def in SkillCatalog.All)
            {
                if (def.kind != SkillKind.Active || n >= count) continue;
                p.GrantSkill(def.id);
                n++;
            }
        }

        private static void SetOwnedEachKind(PlayerData p, int count)
        {
            p.ownedSkillMask = 0;
            for (int i = 0; i < PlayerData.SkillSlotCount; i++) p.SetSkillSlot(i, -1);
            SkillKind[] kinds = { SkillKind.Active, SkillKind.Passive, SkillKind.Trigger };
            foreach (SkillKind kind in kinds)
            {
                int n = 0;
                foreach (SkillDef def in SkillCatalog.All)
                {
                    if (def.kind != kind || n >= count) continue;
                    p.GrantSkill(def.id);
                    n++;
                }
            }
        }

        private static int MetricInt(PlayerData p, string metric)
        {
            return (int)AchievementSystem.Metric(p, metric);
        }

        private static void QualityTests()
        {
            float[] qs = { 0.7949f, 0.795f, 0.7999f, 0.8f };
            int[] expected = { 79, 79, 79, 80 };
            for (int i = 0; i < qs.Length; i++)
            {
                PlayerData p = Fresh();
                SetAllAffixQuality(p, qs[i]);
                int actual = MetricInt(p, "min_reforge_quality");
                Check(actual == expected[i], "质量边界 " + qs[i].ToString("0.0000"),
                    "min_reforge_quality=" + actual + " 期望 " + expected[i]);
                AchievementSystem.Evaluate(p, false);
                Check(p.achievements.IsUnlocked("all_reforge_80") == (expected[i] >= 80),
                    "质量边界解锁 all_reforge_80 @" + qs[i].ToString("0.0000"));
            }

            PlayerData empty = Fresh();
            SetAllAffixQuality(empty, 0.8f);
            empty.helmet = null;
            Check(MetricInt(empty, "min_reforge_quality") == 0, "空槽时 min_reforge_quality=0");
            AchievementSystem.Evaluate(empty, false);
            Check(!empty.achievements.IsUnlocked("all_reforge_80"), "空槽不解锁 all_reforge_80");

            PlayerData enhance = Fresh();
            SetAllGear(enhance, 15, -1f);
            enhance.boots = null;
            Check(MetricInt(enhance, "min_enhance") == 0, "空槽时 min_enhance=0");
            AchievementSystem.Evaluate(enhance, false);
            Check(!enhance.achievements.IsUnlocked("all_enhance_15"), "空槽不解锁 all_enhance_15");

            UnityEngine.Random.InitState(20261020);
            for (int i = 0; i < 40; i++)
            {
                Equipment minor = LootSystem.CreateEquipment(EquipmentSlot.Helmet, 100, EquipmentQuality.Rare);
                Check(minor.AffixCount <= 2, "小件副词条 <= 2", "实际 " + minor.AffixCount);
                Check(Mathf.Abs(minor.affixScale - GameConfig.MinorAffixScale) < 0.0001f,
                    "小件 affixScale=0.5", "实际 " + minor.affixScale);
            }
        }

        private static void GoldTests()
        {
            PlayerData p = Fresh();
            p.gold = 1000;
            p.stats.kills = 1;
            int unlocked = AchievementSystem.Evaluate(p, true);
            Check(unlocked >= 1, "live 求值返回解锁数", "实际 " + unlocked);
            Check(p.gold == 1200, "first_kill 固定 +200 金币", "实际 " + p.gold);
            Check(p.achievements.IsUnlocked("first_kill"), "first_kill 已解锁");
            Check(AchievementSystem.HasUnsavedUnlocks, "解锁后 HasUnsavedUnlocks=true");

            int again = AchievementSystem.Evaluate(p, true);
            Check(again == 0 && p.gold == 1200, "重复求值不重复发金币",
                "again=" + again + " gold=" + p.gold);
            AchievementSystem.NotifySaveResult(true);
            Check(!AchievementSystem.HasUnsavedUnlocks, "保存后 HasUnsavedUnlocks=false");

            PlayerData silent = Fresh();
            silent.gold = 1000;
            silent.stats.kills = 1;
            AchievementSystem.Evaluate(silent, false);
            Check(silent.gold == 1000, "silent 求值不发金币", "实际 " + silent.gold);
            Check(silent.achievements.IsUnlocked("first_kill"), "silent 仍标记解锁");

            PlayerData fortune = Fresh();
            fortune.rebirthFortuneRank = 5;
            fortune.rebirthCount = 1;
            fortune.gold = 1000;
            fortune.stats.kills = 1;
            AchievementSystem.Evaluate(fortune, true);
            Check(fortune.gold == 1200, "成就金币不吃财运倍率", "实际 " + fortune.gold);

            PlayerData overflow = Fresh();
            overflow.gold = int.MaxValue - 50;
            overflow.stats.kills = 1;
            AchievementSystem.Evaluate(overflow, true);
            Check(overflow.gold == int.MaxValue, "金币不溢出", "实际 " + overflow.gold);
            Check(overflow.stats.goldEarned >= 50, "goldEarned 记录实际到账", "实际 " + overflow.stats.goldEarned);
        }

        private static GameController NewController(PlayerData player)
        {
            GameController gc = (GameController)FormatterServices.GetUninitializedObject(typeof(GameController));
            PropertyInfo prop = typeof(GameController).GetProperty("Player",
                BindingFlags.Public | BindingFlags.Instance);
            if (prop != null)
            {
                try { prop.SetValue(gc, player); return gc; } catch (Exception) { }
            }
            FieldInfo field = typeof(GameController).GetField("<Player>k__BackingField",
                BindingFlags.NonPublic | BindingFlags.Instance);
            if (field == null) throw new MissingFieldException("GameController.Player backing field");
            field.SetValue(gc, player);
            return gc;
        }

        private static object Invoke(object target, string name, params object[] args)
        {
            MethodInfo[] methods = target.GetType().GetMethods(
                BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
            foreach (MethodInfo m in methods)
            {
                if (m.Name != name) continue;
                ParameterInfo[] ps = m.GetParameters();
                if (ps.Length != args.Length) continue;
                bool ok = true;
                for (int i = 0; i < ps.Length; i++)
                {
                    if (args[i] == null) continue;
                    if (!ps[i].ParameterType.IsInstanceOfType(args[i])) { ok = false; break; }
                }
                if (ok) return m.Invoke(target, args);
            }
            throw new MissingMethodException(target.GetType().Name + "." + name + " / " + args.Length);
        }

        private static bool FieldBool(object target, string name)
        {
            FieldInfo f = target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
            if (f == null) throw new MissingFieldException(name);
            return (bool)f.GetValue(target);
        }

        private static void StatTests()
        {
            PlayerData p = Fresh();
            GameController gc = NewController(p);
            Invoke(gc, "RecordKillStats", MonsterTier.Normal);
            Invoke(gc, "RecordKillStats", MonsterTier.Elite);
            Invoke(gc, "RecordKillStats", MonsterTier.Boss);
            Check(p.stats.kills == 3 && p.stats.eliteKills == 1 && p.stats.bossKills == 1,
                "击杀统计", "kills=" + p.stats.kills + " elite=" + p.stats.eliteKills
                + " boss=" + p.stats.bossKills);

            Invoke(gc, "RecordDropStats", LootSystem.CreateEquipment(EquipmentSlot.Weapon, 100, EquipmentQuality.Legendary));
            Check(p.stats.drops == 1 && p.stats.legendaryDrops == 1, "掉落统计",
                "drops=" + p.stats.drops + " legendary=" + p.stats.legendaryDrops);

            Invoke(gc, "RecordSaleStats");
            Invoke(gc, "RecordEquipStats");
            Check(p.stats.sold == 1 && p.stats.equipped == 1, "卖出/装备统计");

            Equipment gear = MakeGear(EquipmentSlot.Weapon, EquipmentQuality.Legendary, 100, 15);
            Invoke(gc, "RecordEnhanceStats", gear);
            Check(p.stats.enhances == 1 && p.stats.bestEnhanceLevel == 15, "强化统计");

            Invoke(gc, "RecordReforgeStats", gear, 3);
            Check(p.stats.reforges == 1 && p.stats.threeLockReforges == 1, "洗练统计");
            Check(p.stats.bestAffixQuality == 0f, "无词条时 bestAffixQuality=0",
                "实际 " + p.stats.bestAffixQuality);

            Invoke(gc, "RecordDeathStats");
            Check(p.stats.deaths == 1, "死亡统计");

            p.stats.kills = long.MaxValue;
            Invoke(gc, "RecordKillStats", MonsterTier.Normal);
            Check(p.stats.kills == long.MaxValue, "统计不溢出", "实际 " + p.stats.kills);
        }

        private static void NoDeathTests()
        {
            PlayerData p = Fresh();
            GameController gc = NewController(p);

            p.chapterIndex = 9;
            p.stageIndex = 1;
            Invoke(gc, "TryOpenNoDeathAttempt", 9, 1);
            Check(FieldBool(gc, "_noDeathActive"), "9-1 开启无阵亡挑战");

            Invoke(gc, "ClearNoDeathAttempt");
            p.chapterIndex = 8;
            p.stageIndex = 1;
            Invoke(gc, "TryOpenNoDeathAttempt", 8, 1);
            Check(!FieldBool(gc, "_noDeathActive"), "第 8 章不开启挑战");

            p.chapterIndex = 9;
            p.stageIndex = 1;
            Invoke(gc, "TryOpenNoDeathAttempt", 9, 1);
            p.stageIndex = 3;
            Invoke(gc, "AdvanceNoDeathAttempt");
            Check(!FieldBool(gc, "_noDeathActive"), "跳关作废挑战");

            for (int chapter = 9; chapter <= 11; chapter++)
            {
                p.chapterIndex = chapter;
                p.stageIndex = 1;
                Invoke(gc, "TryOpenNoDeathAttempt", chapter, 1);
                for (int stage = 1; stage <= 10; stage++)
                {
                    p.stageIndex = stage;
                    Invoke(gc, "AdvanceNoDeathAttempt");
                }
                int bit = 1 << (chapter - 1);
                Check((p.stats.noDeathChapterMask & bit) != 0,
                    "第 " + chapter + " 章十连无阵亡记录", "mask=" + p.stats.noDeathChapterMask);
                Check(!FieldBool(gc, "_noDeathActive"), "第 " + chapter + " 章完成后清空挑战");
            }

            AchievementSystem.Evaluate(p, false);
            Check(p.achievements.IsUnlocked("no_death_chapter"), "no_death_chapter 解锁");

            p.chapterIndex = 9;
            p.stageIndex = 1;
            Invoke(gc, "TryOpenNoDeathAttempt", 9, 1);
            Invoke(gc, "ClearNoDeathAttempt");
            Invoke(gc, "RecordDeathStats");
            Check(!FieldBool(gc, "_noDeathActive") && p.stats.deaths == 1, "死亡清空挑战并计数");
        }

        private class PrefsSnapshot
        {
            public string[] Keys;
            public bool[] Has;
            public string[] Values;
            public int CurrentSlot;
            public bool HasCurrentSlot;
            public int EndlessBest;
            public bool HasEndlessBest;
        }

        private static string CurrentSlotKey { get { return GameConfig.SaveKey + "_current"; } }
        private static string EndlessBestKey { get { return GameConfig.SaveKey + ".Endless.Best"; } }

        private static PrefsSnapshot CapturePrefs()
        {
            List<string> keys = new List<string>();
            for (int i = 0; i < SaveSystem.SlotCount; i++)
            {
                keys.Add(SaveSystem.KeyOf(i));
                keys.Add(SaveSystem.KeyOf(i) + SaveSystem.BackupSuffix);
            }
            PrefsSnapshot snap = new PrefsSnapshot
            {
                Keys = keys.ToArray(),
                Has = new bool[keys.Count],
                Values = new string[keys.Count],
                CurrentSlot = PlayerPrefs.GetInt(CurrentSlotKey, 0),
                HasCurrentSlot = PlayerPrefs.HasKey(CurrentSlotKey),
                EndlessBest = PlayerPrefs.GetInt(EndlessBestKey, 0),
                HasEndlessBest = PlayerPrefs.HasKey(EndlessBestKey)
            };
            for (int i = 0; i < keys.Count; i++)
            {
                snap.Has[i] = PlayerPrefs.HasKey(keys[i]);
                snap.Values[i] = PlayerPrefs.GetString(keys[i], string.Empty);
            }
            return snap;
        }

        private static void RestorePrefs(PrefsSnapshot snap)
        {
            for (int i = 0; i < snap.Keys.Length; i++)
            {
                if (snap.Has[i]) PlayerPrefs.SetString(snap.Keys[i], snap.Values[i]);
                else PlayerPrefs.DeleteKey(snap.Keys[i]);
            }
            if (snap.HasCurrentSlot) PlayerPrefs.SetInt(CurrentSlotKey, snap.CurrentSlot);
            else PlayerPrefs.DeleteKey(CurrentSlotKey);
            if (snap.HasEndlessBest) PlayerPrefs.SetInt(EndlessBestKey, snap.EndlessBest);
            else PlayerPrefs.DeleteKey(EndlessBestKey);
            PlayerPrefs.Save();
            SaveSystem.SelectSlot(snap.CurrentSlot);
        }

        private static void ClearSlot0()
        {
            SaveSystem.SelectSlot(0);
            PlayerPrefs.DeleteKey(SaveSystem.KeyOf(0));
            PlayerPrefs.DeleteKey(SaveSystem.KeyOf(0) + SaveSystem.BackupSuffix);
            PlayerPrefs.Save();
        }

        private static void WriteRaw(string json)
        {
            SaveSystem.SelectSlot(0);
            PlayerPrefs.SetString(SaveSystem.KeyOf(0), json);
            PlayerPrefs.Save();
        }

        private static string ReadRaw()
        {
            return PlayerPrefs.GetString(SaveSystem.KeyOf(0), string.Empty);
        }

        private static string BackupRaw()
        {
            return PlayerPrefs.GetString(SaveSystem.KeyOf(0) + SaveSystem.BackupSuffix, string.Empty);
        }

        private static bool LoadFromRaw(string json, out PlayerData player)
        {
            WriteRaw(json);
            player = Fresh();
            return SaveSystem.Load(player);
        }

        private static SaveSystem.SaveData BaseData(int version)
        {
            SaveSystem.SaveData d = new SaveSystem.SaveData();
            d.version = version;
            d.level = 1;
            d.gold = 0;
            d.potionCount = 0;
            d.autoBuyPotion = true;
            d.paragonLevel = 0;
            d.paragonExp = 0;
            d.ownedSkillMask = 0;
            d.chapterIndex = 1;
            d.stageIndex = 1;
            d.stageKillCount = 0;
            d.maxChapterUnlocked = 1;
            d.clearedStages = new List<int>();
            d.pendingSkillPicks = 0;
            d.skillActive0 = -1;
            d.skillActive1 = -1;
            d.skillPassive0 = -1;
            d.skillPassive1 = -1;
            d.skillTrigger0 = -1;
            d.skillTrigger1 = -1;
            d.currentHp = 100;
            d.stats = new PlayerStats();
            d.achievements = new AchievementState();
            return d;
        }

        private static List<int> FullStages()
        {
            List<int> list = new List<int>();
            for (int c = 1; c <= GameConfig.ChapterCount; c++)
                for (int s = 1; s <= GameConfig.StagesPerChapter; s++)
                    list.Add(PlayerData.StageKey(c, s));
            return list;
        }

        private static void RebirthTests()
        {
            Check(RebirthCatalog.TrackCount == 5 && RebirthCatalog.MaxRank == 5,
                "五条转生线、每条 5 级");
            Check(RebirthCatalog.MaxMarks == 30, "单次转生印记上限 30",
                "实际 " + RebirthCatalog.MaxMarks);

            int totalCost = 0;
            foreach (RebirthTrack track in RebirthCatalog.All) totalCost += RebirthCatalog.TotalCost(track);
            Check(totalCost == 110, "五条线升满总费用 110", "实际 " + totalCost);
            Check(RebirthCatalog.CostOf(RebirthTrack.Battle, 0) == 3
                && RebirthCatalog.CostOf(RebirthTrack.Alchemy, 0) == 1,
                "升级费用 = 基数 + 当前等级");

            Check(RebirthCatalog.ParagonMarks(99) == 0
                && RebirthCatalog.ParagonMarks(100) == 1
                && RebirthCatalog.ParagonMarks(999) == 9
                && RebirthCatalog.ParagonMarks(100000) == 10,
                "巅峰印记每 100 级 1 枚、封顶 10");
            Check(RebirthCatalog.FloorMarks(49) == 0
                && RebirthCatalog.FloorMarks(50) == 1
                && RebirthCatalog.FloorMarks(250) == 5
                && RebirthCatalog.FloorMarks(100000) == 5,
                "无尽印记每 50 层 1 枚、封顶 5");
            Check(RebirthCatalog.GoldMarks(99999) == 0
                && RebirthCatalog.GoldMarks(100000) == 0
                && RebirthCatalog.GoldMarks(200000) == 1
                && RebirthCatalog.GoldMarks(400000) == 2
                && RebirthCatalog.GoldMarks(int.MaxValue) == RebirthCatalog.MaxGoldMarks,
                "金币印记按 10 万翻倍、封顶 14");
            Check(RebirthCatalog.MarksFor(0, 0, 0) == 1,
                "零进度也有 1 枚基础印记");
            Check(RebirthCatalog.MarksFor(1000, 250, int.MaxValue) == 30,
                "满进度封顶 30 枚印记", "实际 " + RebirthCatalog.MarksFor(1000, 250, int.MaxValue));

            PlayerData p = Fresh();
            p.rebirthBattleRank = 5;
            p.rebirthGrowthRank = 5;
            p.rebirthFortuneRank = 5;
            p.rebirthLegacyRank = 5;
            p.rebirthAlchemyRank = 5;
            Check(Mathf.Abs(p.RebirthBattleMultiplier - 1.05f) < 0.0001f,
                "战斗本能 +5%", "实际 " + p.RebirthBattleMultiplier);
            Check(Mathf.Abs(p.RebirthExpMultiplier - 2f) < 0.0001f,
                "历练经验 +100%", "实际 " + p.RebirthExpMultiplier);
            Check(Mathf.Abs(p.RebirthGoldMultiplier - 2f) < 0.0001f,
                "财运金币 +100%", "实际 " + p.RebirthGoldMultiplier);
            Check(Mathf.Abs(p.RebirthDropBonus - 0.10f) < 0.0001f,
                "财运掉落 +10 个百分点", "实际 " + p.RebirthDropBonus);
            Check(p.RebirthStartLevel == 11, "传承 5 从 Lv.11 开始", "实际 " + p.RebirthStartLevel);
            Check(p.RebirthStartGold == 2500, "行囊 5 起始 2500 金币", "实际 " + p.RebirthStartGold);
            Check(p.RebirthStartPotions == 5, "行囊 5 起始 5 瓶药剂", "实际 " + p.RebirthStartPotions);

            PlayerData plain = Fresh();
            plain.level = 100;
            plain.paragonLevel = 100;
            int baseHp = plain.MaxHp;
            int baseAtk = plain.Attack;
            p.level = 100;
            p.paragonLevel = 100;
            Check(p.MaxHp > baseHp && p.Attack > baseAtk,
                "战斗本能作用于生命/攻击",
                "HP " + baseHp + "→" + p.MaxHp + " 攻 " + baseAtk + "→" + p.Attack);

        }

        private static void SaveTests()
        {
            PrefsSnapshot snap = CapturePrefs();
            try
            {
                SaveRoundTrip();
                SaveFailureTests();
                LoadV9Migration();
                LoadV8Migration();
                LoadV5Migration();
                LoadV0Migration();
                LoadRejectsNewer();
                LoadRejectsNewerSchema();
                LoadRejectsBadJson();
                LoadSanitizes();
                LoadIsIdempotent();
            }
            finally
            {
                RestorePrefs(snap);
            }
        }

        private static void SaveRoundTrip()
        {
            ClearSlot0();
            PlayerData p = Fresh();
            p.level = 42;
            p.gold = 12345;
            p.paragonLevel = 7;
            p.paragonExp = 3;
            p.ownedSkillMask = (1 << 0) | (1 << 4);
            p.pendingSkillPicks = 1;
            p.chapterIndex = 2;
            p.stageIndex = 3;
            p.stageKillCount = 1;
            p.maxChapterUnlocked = 2;
            p.clearedStages.Add(PlayerData.StageKey(1, 1));
            p.clearedStages.Add(PlayerData.StageKey(1, 2));
            p.potionCount = 3;
            p.autoBuyPotion = true;
            p.endlessBestFloor = 77;
            p.rebirthCount = 1;
            p.rebirthMarks = 2;
            p.rebirthBestParagon = 7;
            p.rebirthBattleRank = 1;
            p.rebirthFortuneRank = 4;
            p.weapon = MakeGear(EquipmentSlot.Weapon, EquipmentQuality.Legendary, 42, 5);
            p.weapon.attack = 321;
            p.armor = MakeGear(EquipmentSlot.Armor, EquipmentQuality.Epic, 40, 2);
            p.helmet = MakeGear(EquipmentSlot.Helmet, EquipmentQuality.Rare, 35, 1);
            p.accessory = MakeGear(EquipmentSlot.Accessory, EquipmentQuality.Uncommon, 30, 0);
            p.stats.kills = 1;
            AchievementSystem.Evaluate(p, true);
            int expectedGold = p.gold;
            Check(expectedGold > 12345, "结算成就金币后再存档", "实际 " + expectedGold);
            Check(AchievementSystem.HasUnsavedUnlocks, "存档前脏标记为 true");
            p.stats.kills = 17;
            p.stats.drops = 5;
            p.stats.bestEnhanceLevel = 6;
            p.stats.bestAffixQuality = 0.7f;
            p.currentHp = p.MaxHp;

            bool saved = SaveSystem.Save(p);
            Check(saved, "Save 返回 true");
            Check(!AchievementSystem.HasUnsavedUnlocks, "保存后脏标记清除");
            PlayerData q = Fresh();
            bool loaded = SaveSystem.Load(q);
            Check(loaded, "Load 返回 true");
            Check(q.level == 42 && q.gold == expectedGold, "基础字段往返", "level=" + q.level + " gold=" + q.gold + " 期望 " + expectedGold);
            Check(q.paragonLevel == 7 && q.paragonExp == 3, "巅峰字段往返");
            Check(q.ownedSkillMask == ((1 << 0) | (1 << 4)) && q.pendingSkillPicks == 1, "技能字段往返");
            Check(q.chapterIndex == 2 && q.stageIndex == 3 && q.stageKillCount == 1, "关卡字段往返");
            Check(q.clearedStages.Count == 2 && q.clearedStages.Contains(PlayerData.StageKey(1, 1)), "通关列表往返");
            Check(q.potionCount == 3, "药剂往返");
            Check(q.endlessBestFloor == 77, "无尽纪录往返");
            Check(q.rebirthCount == 1 && q.rebirthMarks == 2 && q.rebirthBestParagon == 7, "转生字段往返");
            Check(q.rebirthBattleRank == 1 && q.rebirthFortuneRank == 4, "转生等级往返");
            Check(q.weapon != null && q.weapon.attack == 321 && q.weapon.enhanceLevel == 5, "武器往返");
            Check(q.armor != null && q.armor.quality == EquipmentQuality.Epic, "衣服往返");
            Check(q.helmet != null && q.helmet.quality == EquipmentQuality.Rare, "头盔往返");
            Check(q.boots == null, "空鞋往返为 null");
            Check(q.accessory != null && q.accessory.quality == EquipmentQuality.Uncommon, "饰品往返");
            Check(q.stats.kills == 17 && q.stats.drops == 5, "统计往返");
            Check(q.achievements.IsUnlocked("first_kill"), "成就往返");
        }

        private static void SaveFailureTests()
        {
            ClearSlot0();
            SaveSystem.SelectSlot(0);
            PlayerData p = Fresh();
            p.level = 33;
            Check(SaveSystem.Save(p), "基线存档写入成功");
            string baseline = ReadRaw();
            Check(!string.IsNullOrEmpty(baseline), "基线存档非空");

            AchievementSystem.GrantSilent(p, "first_kill");
            Check(AchievementSystem.HasUnsavedUnlocks, "静默补发后有待保存标记");

            SaveSystem.FailNextSaveForTest = true;
            bool failed = SaveSystem.Save(p);
            Check(!failed, "注入失败时 Save 返回 false");
            Check(AchievementSystem.HasUnsavedUnlocks, "失败后待保存标记保留");
            Check(ReadRaw() == baseline, "失败不覆盖旧档");

            bool retried = SaveSystem.Save(p);
            Check(retried, "重试成功");
            Check(!AchievementSystem.HasUnsavedUnlocks, "成功后清除待保存标记");
            Check(ReadRaw() != baseline, "成功后写入新内容");

            PlayerData q;
            bool loaded = LoadFromRaw(ReadRaw(), out q);
            Check(loaded && q.achievements.IsUnlocked("first_kill"), "重试后成就已落盘");
            Check(!AchievementSystem.HasUnsavedUnlocks, "重试成功后无待保存标记");
        }

        private static void LoadV9Migration()
        {
            ClearSlot0();
            SaveSystem.SaveData d = BaseData(9);
            d.level = 100;
            d.gold = 12345;
            d.paragonLevel = 100;
            d.chapterIndex = 11;
            d.stageIndex = 10;
            d.maxChapterUnlocked = 11;
            d.clearedStages = FullStages();
            d.rebirthCount = 2;
            d.rebirthMarks = 10;
            d.rebirthBestParagon = 500;
            d.rebirthBattleRank = 2;
            d.rebirthFortuneRank = 3;
            d.stats = null;
            d.achievements = null;
            string json = JsonUtility.ToJson(d);
            PlayerData p;
            bool ok = LoadFromRaw(json, out p);
            Check(ok, "v9 可加载");
            Check(p.level == 100 && p.gold == 12345, "v9 基础字段");
            Check(p.rebirthCount == 2 && p.rebirthMarks == 10 && p.rebirthBestParagon == 500, "v9 转生字段");
            Check(p.rebirthBattleRank == 2 && p.rebirthFortuneRank == 3, "v9 转生等级");
            Check(p.stats != null && p.stats.kills == 0, "v9 迁移出空 stats");
            Check(p.achievements.IsUnlocked("level_100"), "v9 静默补 level_100");
            Check(p.achievements.IsUnlocked("clear_ch11"), "v9 静默补 clear_ch11");
            Check(p.achievements.IsUnlocked("paragon_100"), "v9 静默补 paragon_100");
            Check(p.achievements.IsUnlocked("all_stages"), "v9 全通关补 all_stages");
        }

        private static void LoadV8Migration()
        {
            ClearSlot0();
            SaveSystem.SaveData d = BaseData(8);
            d.level = 10;
            d.gold = 100;
            d.chapterIndex = 7;
            d.stageIndex = 3;
            d.maxChapterUnlocked = 7;
            d.clearedStages = null;
            d.weapon = MakeGear(EquipmentSlot.Weapon, EquipmentQuality.Rare, 10, 5);
            d.armor = MakeGear(EquipmentSlot.Armor, EquipmentQuality.Rare, 10, 0);
            d.armor.reforgeCount = 2;
            d.stats = null;
            d.achievements = null;
            PlayerData p;
            bool ok = LoadFromRaw(JsonUtility.ToJson(d), out p);
            Check(ok, "v8 可加载");
            Check(p.clearedStages.Count == 62, "v8 按最大章节回填通关", "实际 " + p.clearedStages.Count);
            Check(p.stats.enhances == 5, "v8 强化次数迁移", "实际 " + p.stats.enhances);
            Check(p.stats.reforges == 2, "v8 洗练次数迁移", "实际 " + p.stats.reforges);
            Check(p.stats.bestEnhanceLevel == 5, "v8 最高强化迁移", "实际 " + p.stats.bestEnhanceLevel);
            Check(p.rebirthCount == 0 && p.rebirthMarks == 0, "v8 无转生");
        }

        private static void LoadV5Migration()
        {
            ClearSlot0();
            SaveSystem.SaveData d = BaseData(5);
            d.maxChapterUnlocked = 3;
            d.chapterIndex = 3;
            d.stageIndex = 5;
            d.clearedStages = null;
            d.stats = null;
            d.achievements = null;
            PlayerData p;
            bool ok = LoadFromRaw(JsonUtility.ToJson(d), out p);
            Check(ok, "v5 可加载");
            Check(p.clearedStages.Count == 24, "v5 回填 3 章 24 关", "实际 " + p.clearedStages.Count);
        }

        private static void LoadV0Migration()
        {
            ClearSlot0();
            SaveSystem.SaveData d = BaseData(0);
            d.level = 5;
            d.chapterIndex = 0;
            d.stageIndex = 0;
            d.maxChapterUnlocked = 0;
            d.clearedStages = null;
            d.stats = null;
            d.achievements = null;
            PlayerData p;
            bool ok = LoadFromRaw(JsonUtility.ToJson(d), out p);
            Check(ok, "v0 可加载");
            Check(p.level == 5 && p.chapterIndex == 1 && p.stageIndex == 1, "v0 关卡归一到 1-1");
            Check(p.clearedStages.Count == 0, "v0 无通关记录");
        }

        private static int NextSaveVersion()
        {
            // 从 SaveSystem 自己的常量推导，避免存档版本再次升级后这条用例默默失效（2026-10-22 曾因此 FAIL）。
            var field = typeof(SaveSystem).GetField("CurrentSaveVersion",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            int current = field != null ? (int)field.GetRawConstantValue() : 11;
            return current + 1;
        }

        private static void LoadRejectsNewer()
        {
            ClearSlot0();
            int newer = NextSaveVersion();
            SaveSystem.SaveData d = BaseData(newer);
            d.level = 50;
            string json = JsonUtility.ToJson(d);
            PlayerData p;
            bool ok = LoadFromRaw(json, out p);
            Check(!ok, $"v{newer} 拒绝加载");
            Check(BackupRaw() == json, $"v{newer} 原文备份");
        }

        private static void LoadRejectsNewerSchema()
        {
            ClearSlot0();
            SaveSystem.SaveData d = BaseData(10);
            d.achievements = new AchievementState { schemaVersion = 2 };
            string json = JsonUtility.ToJson(d);
            PlayerData p;
            bool ok = LoadFromRaw(json, out p);
            Check(!ok, "schema=2 拒绝加载");
            Check(BackupRaw() == json, "schema=2 原文备份");
        }

        private static void LoadRejectsBadJson()
        {
            ClearSlot0();
            string json = "{broken";
            PlayerData p;
            bool ok = LoadFromRaw(json, out p);
            Check(!ok, "坏 JSON 拒绝加载");
            Check(BackupRaw() == json, "坏 JSON 原文备份");
        }

        private static void LoadSanitizes()
        {
            ClearSlot0();
            SaveSystem.SaveData d = BaseData(10);
            d.level = 50;
            d.gold = -5;
            d.potionCount = 999;
            d.ownedSkillMask = -1;
            d.pendingSkillPicks = 999;
            d.skillActive0 = 99;
            d.skillActive1 = 98;
            d.stats = new PlayerStats
            {
                kills = -1,
                drops = -2,
                bestEnhanceLevel = -3,
                bestAffixQuality = 2f,
                noDeathChapterMask = 0xFFFF
            };
            d.achievements = new AchievementState
            {
                unlocked = new List<string> { "", "first_kill", "first_kill", "unknown_id" }
            };
            PlayerData p;
            bool ok = LoadFromRaw(JsonUtility.ToJson(d), out p);
            Check(ok, "异常字段档仍可加载");
            Check(p.gold == 0 && p.potionCount <= 5, "金币/药剂夹取", "gold=" + p.gold + " potion=" + p.potionCount);
            Check(p.ownedSkillMask == 0x7FFF, "技能掩码夹到 15 位", "实际 " + p.ownedSkillMask);
            Check(p.pendingSkillPicks == 0, "待选次数上限夹取", "实际 " + p.pendingSkillPicks);
            Check(p.stats.kills == 0 && p.stats.drops == 0, "负统计夹到 0");
            Check(p.stats.bestEnhanceLevel == 0 && p.stats.bestAffixQuality == 1f, "质量/强化夹取");
            Check(p.stats.noDeathChapterMask == 0x700, "无阵亡掩码只保留 9~11 章", "实际 " + p.stats.noDeathChapterMask);
            Check(p.skillActive0 == -1 && p.skillActive1 == -1, "非法技能槽被清空");
            int firstKillCount = 0;
            int emptyCount = 0;
            foreach (string id in p.achievements.unlocked)
            {
                if (id == "first_kill") firstKillCount++;
                if (string.IsNullOrEmpty(id)) emptyCount++;
            }
            Check(p.achievements.unlocked.Count >= 2, "成就去空去重后至少保留两条",
                "实际 " + p.achievements.unlocked.Count);
            Check(firstKillCount == 1, "first_kill 去重后仅 1 条", "实际 " + firstKillCount);
            Check(emptyCount == 0, "空成就 id 已清理", "实际 " + emptyCount);
            Check(p.achievements.IsUnlocked("first_kill") && p.achievements.IsUnlocked("unknown_id"),
                "首杀与未知成就保留");
            Check(p.achievements.IsUnlocked("reforge_80") && p.achievements.IsUnlocked("no_death_chapter"),
                "清洗后按已证明条件补发成就");
            Check(p.achievements.IsUnlocked("skills_15") && p.achievements.IsUnlocked("all_active")
                && p.achievements.IsUnlocked("skill_triple_4"), "全技能掩码补发技能成就");
        }

        private static void LoadIsIdempotent()
        {
            ClearSlot0();
            SaveSystem.SaveData d = BaseData(10);
            d.level = 100;
            d.achievements = new AchievementState();
            d.stats = new PlayerStats();
            string json = JsonUtility.ToJson(d);
            PlayerData first;
            bool ok1 = LoadFromRaw(json, out first);
            int count1 = first.achievements.unlocked.Count;
            int gold1 = first.gold;
            PlayerData second;
            bool ok2 = LoadFromRaw(json, out second);
            Check(ok1 && ok2, "同一档重复加载成功");
            Check(first.achievements.unlocked.Count == second.achievements.unlocked.Count,
                "重复加载不重复成就", count1 + " vs " + second.achievements.unlocked.Count);
            Check(first.gold == second.gold, "重复加载不重复发金币", gold1 + " vs " + second.gold);
        }

        public static string RunProbes()
        {
            Reset();
            RunSection("新档成长", () =>
            {
                Log.AppendLine(NewGameProgression.Run(12345, 30, false, 0.8f, 0, 0, 0, 0, 0, AchievementCatalog.ExpectedGold));
            });
            RunSection("静态平衡基线", () =>
            {
                Log.AppendLine(BalanceRegression.RunStaticOnly());
            });
            return Summary("成就 §11 探针");
        }
    }
}

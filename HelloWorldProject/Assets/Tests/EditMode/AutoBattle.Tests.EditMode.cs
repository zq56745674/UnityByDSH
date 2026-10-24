using NUnit.Framework;
using UnityEngine;

namespace AutoBattle.Tests
{
    /// <summary>
    /// EditMode 回归（roadmap A5）：把「快速回归」里靠字符串判定的几条核心口径，
    /// 改成真正的 NUnit 断言——失败时能看到具体哪个数不对，而不是一整段 PASS/FAIL 文本。
    /// 只做纯计算与内存对象，不写 PlayerPrefs、不进 Play 模式。
    /// </summary>
    public class ConfigAndFormulaTests
    {
        [Test]
        public void ChapterPressure_MatchesFiveSlotRetune()
        {
            Assert.AreEqual(1.0f, GameConfig.ChapterPressure(1, false), 1e-4f);
            Assert.AreEqual(1.22f, GameConfig.ChapterPressure(5, false), 1e-4f);
            Assert.AreEqual(1.495f, GameConfig.ChapterPressure(10, false), 1e-4f);
            Assert.AreEqual(1.495f * 1.36f, GameConfig.ChapterPressure(10, true), 1e-3f);
            Assert.AreEqual(1.0f, GameConfig.ChapterPressure(11, false), 1e-4f);
            Assert.AreEqual(1.0f, GameConfig.ChapterPressure(11, true), 1e-4f);
        }

        [Test]
        public void ChapterPressure_IsMonotonicThroughChapterTen()
        {
            float previous = 0f;
            for (int chapter = 1; chapter <= 10; chapter++)
            {
                float normal = GameConfig.ChapterPressure(chapter, false);
                float boss = GameConfig.ChapterPressure(chapter, true);
                Assert.GreaterOrEqual(normal, previous, "第 " + chapter + " 章压迫比上一章低");
                Assert.GreaterOrEqual(boss, normal - 1e-5f, "第 " + chapter + " 章 BOSS 压迫低于普通怪");
                previous = normal;
            }
        }

        [Test]
        public void EnhanceCost_UsesQualityFactorAndLevelGrowth()
        {
            Assert.AreEqual(90, GameConfig.EnhanceCost(0, EquipmentQuality.Common));
            Assert.AreEqual(6960, GameConfig.EnhanceCost(14, EquipmentQuality.Legendary));
            Assert.Greater(
                GameConfig.EnhanceCost(1, EquipmentQuality.Rare),
                GameConfig.EnhanceCost(0, EquipmentQuality.Rare));
        }

        [Test]
        public void EndlessStartFloor_RetreatsByConfiguredOffset()
        {
            int offset = Mathf.Max(0, GameConfig.EndlessCheckpointOffset);
            Assert.AreEqual(Mathf.Max(1, 14 - offset), EndlessCatalog.StartFloorFor(14));
            Assert.AreEqual(1, EndlessCatalog.StartFloorFor(offset));
            Assert.AreEqual(1, EndlessCatalog.StartFloorFor(0));
            Assert.AreEqual(1, EndlessCatalog.StartFloorFor(1));
        }

        [Test]
        public void EquipmentScore_IsIncrementalAndSlotBound()
        {
            var player = new PlayerData { level = 100 };
            player.FillHp();

            var weapon = new Equipment
            {
                slot = EquipmentSlot.Weapon,
                quality = EquipmentQuality.Legendary,
                level = 100,
                qualityMultiplier = 1.6f,
                attack = 300,
                critChance = 0.1f,
                critDamage = 0.2f,
            };
            var armor = new Equipment
            {
                slot = EquipmentSlot.Armor,
                quality = EquipmentQuality.Legendary,
                level = 100,
                qualityMultiplier = 1.6f,
                defense = 200,
                hp = 2000,
            };

            Assert.Greater(EquipmentScore.Of(player, weapon, EquipmentSlot.Weapon), 0f);
            Assert.Greater(EquipmentScore.Of(player, armor, EquipmentSlot.Armor), 0f);
            Assert.AreEqual(0f, EquipmentScore.Of(player, weapon, EquipmentSlot.Armor), 1e-4f,
                "槽位不匹配应返回 0，而不是拿武器去和衣服槽比较");
        }

        [Test]
        public void SaveData_RoundTripsThroughJsonUtility()
        {
            var data = new SaveSystem.SaveData
            {
                version = SaveSystem.SaveVersion,
                level = 100,
                exp = 42,
                gold = 1234567,
                paragonLevel = 183,
                maxChapterUnlocked = 11,
                endlessBestFloor = 14,
                weapon = new Equipment
                {
                    slot = EquipmentSlot.Weapon,
                    quality = EquipmentQuality.Legendary,
                    level = 110,
                    enhanceLevel = 15,
                    attack = 929,
                    critChance = 0.549f,
                },
            };

            string json = JsonUtility.ToJson(data);
            Assert.IsFalse(string.IsNullOrEmpty(json));

            var back = JsonUtility.FromJson<SaveSystem.SaveData>(json);
            Assert.AreEqual(data.version, back.version);
            Assert.AreEqual(data.gold, back.gold);
            Assert.AreEqual(data.paragonLevel, back.paragonLevel);
            Assert.AreEqual(data.endlessBestFloor, back.endlessBestFloor);
            Assert.IsNotNull(back.weapon);
            Assert.AreEqual(EquipmentSlot.Weapon, back.weapon.slot);
            Assert.AreEqual(15, back.weapon.enhanceLevel);
            Assert.AreEqual(929, back.weapon.attack);
        }

        [Test]
        public void AchievementCatalog_LoadsExpectedContent()
        {
            Assert.IsTrue(AchievementCatalog.Reload(), "achievements.json 未能从 Resources 载入");
            Assert.AreEqual(AchievementCatalog.ExpectedCount, AchievementCatalog.Count);
            Assert.AreEqual(AchievementCatalog.ExpectedPoints, AchievementCatalog.TotalPoints);
            Assert.AreEqual(AchievementCatalog.ExpectedTitles, AchievementCatalog.TitleCount);
        }

        [Test]
        public void BalanceSentinel_StillMatchesBaseline()
        {
            string report = AutoBattle.EditorTools.BalanceRegression.RunStaticOnly();
            Assert.IsNotNull(report);
            StringAssert.Contains("PASS", report);
            StringAssert.DoesNotContain("FAIL", report);
        }
    }
}

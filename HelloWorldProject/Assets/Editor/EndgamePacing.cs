using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AutoBattle.EditorTools
{
    /// <summary>
    /// 终局节奏探针：从「Lv.100 + 传说武器/衣服 + 巅峰 0」开始，在 11-10 反复
    /// 「打 5 只小怪 → 打轮换 BOSS → 阵亡 → 再打小怪」，统计到通关与到各个巅峰档的游戏内时间。
    ///
    /// 存在的理由：NewGameProgression 只跑到 30 次尝试就停（它的职责是「能不能走到终局」），
    /// 而终局本身要磨多久、巅峰曲线够不够长，需要一个愿意一直刷下去的探针。
    /// 另外它直接回答了设计目标：「巅峰 0 → 200 大约多久」；现在也支持指定目标（例如升到 1000 的时间）。
    ///
    /// 口径与游戏内一致：小怪连战不回血、BOSS 战前回满、阵亡不给经验、终局 BOSS 类型按次轮换、
    /// 药水在「开商店」时补、掉落/换装/回血都走真实代码（复用 NewGameProgression 的 helper）。
/// 金币要花：探针不花钱时，长线数据全是假的（这个探针以前就是这样：金币堆到六位数，装备却还是 +0）。
    /// 现在每轮 BOSS 战前用 ProbePolicy.SpendGoldOnEnhance 把余钱砸进强化，和真人一致。
    /// </summary>
    public static class EndgamePacing
    {
        [MenuItem("Tools/测试/终局节奏探针（巅峰 0 → 目标）")]
        static void MenuRun() => Debug.Log("[EndgamePacing]\n" + Run());

        /// <param name="targetParagon">要跑到的巅峰目标；默认 200，配置上限现在是 1000。</param>
        /// <param name="maxStages">最多挑战次数；跑 1000 级需要明显调大。</param>
        public static string Run(int seed = 20261018, int maxStages = 20000, bool verbose = false,
            int targetParagon = 200, float reforgeQuality = ProbePolicy.DefaultReforgeQuality)
        {
            Random.InitState(seed);
            targetParagon = Mathf.Clamp(targetParagon, 1, GameConfig.MaxParagonLevel);

            PlayerData p = BuildFresh100();
            var battle = new BattleSystem();
            var milestones = new List<string>();
            var marks = new List<int>();
            foreach (int mark in new[] { 1, 30, 50, 100, 150, 200, 300, 400, 500, 600, 700, 800, 900, 1000 })
            {
                if (mark <= targetParagon) marks.Add(mark);
            }
            if (marks.Count == 0 || marks[marks.Count - 1] != targetParagon) marks.Add(targetParagon);
            int nextMark = 0;

            int attempts = 0;
            int deaths = 0;
            int kills = 0;
            float seconds = 0f;

            int clearAttempts = -1;
            int clearParagon = -1;
            float clearSeconds = -1f;
            float totalXp = 0f;
            int enhanceSpent = 0;
            int reforgeSpent = 0;
            int bossFights = 0;

            // Fight 只返回战斗时间；实际刷图还要等刷怪（每只 0.5s）和 BOSS 前的商店（3s）。
            float WallSeconds() => seconds + kills * GameConfig.MonsterRespawnDelay
                                            + bossFights * GameConfig.ShopDuration;

            void CheckMarks()
            {
                while (nextMark < marks.Count && p.paragonLevel >= marks[nextMark])
                {
                    string tag = marks[nextMark] >= targetParagon ? "目标" : ("P" + marks[nextMark]);
                    milestones.Add($"{tag}: {attempts} 次挑战 / 战斗 {seconds / 60f:0.0}min / 含刷怪+商店 {WallSeconds() / 60f:0.0}min / {kills} 击杀");
                    nextMark++;
                }
            }

            while (attempts < maxStages)
            {
                attempts++;

                // ---- 5 只普通怪（血量继承） ----
                bool died = false;
                for (int k = 0; k < 5 && !died; k++)
                {
                    MonsterData m = NewGameProgression.Spawn(110, MonsterTier.Normal);
                    bool win = NewGameProgression.Fight(p, m, battle, false, out float sec, out _);
                    seconds += sec;
                    if (!win) { died = true; break; }
                    kills++;
                    totalXp += GameConfig.MonsterExpReward(m.level, m.type, m.tier);
                    NewGameProgression.GrantKill(p, m);
                }

                if (died)
                {
                    deaths++;
                    p.FillHp();
                    seconds += GameConfig.PlayerReviveDelay;
                    CheckMarks();
                    continue;
                }

                // ---- 终局 BOSS：战前回满，类型按挑战次数轮换 ----
                NewGameProgression.FillPotions(p);
                // 药买齐了再把余钱砸进强化 / 洗练：这正是真人的顺序（先保命，再堆属性）。
                enhanceSpent += ProbePolicy.SpendGoldOnEnhance(p);
                reforgeSpent += ProbePolicy.SpendGoldOnReforge(p, reforgeQuality);
                p.FillHp();
                MonsterType bossType = (MonsterType)((attempts - 1) % 3);
                bossFights++;
                MonsterData boss = NewGameProgression.Spawn(110, MonsterTier.Boss, bossType);
                bool winBoss = NewGameProgression.Fight(p, boss, battle, false, out float secBoss, out _);
                seconds += secBoss;

                if (!winBoss)
                {
                    deaths++;
                    p.FillHp();
                    seconds += GameConfig.PlayerReviveDelay;
                    CheckMarks();
                    continue;
                }

                kills++;
                totalXp += GameConfig.MonsterExpReward(boss.level, boss.type, boss.tier);
                NewGameProgression.GrantKill(p, boss);

                if (clearAttempts < 0)
                {
                    clearAttempts = attempts;
                    clearParagon = p.paragonLevel;
                    clearSeconds = seconds;
                    milestones.Add(string.Format(CultureInfo.InvariantCulture,
                        "首胜面板：HP {0} 攻 {1} 防 {2} 技能 {3}   BOSS HP {4} 攻 {5}",
                        p.MaxHp, p.Attack, p.Defense, p.OwnedSkillCount, boss.maxHp, boss.attack));
                }

                CheckMarks();

                if (p.paragonLevel >= targetParagon) break;
            }

            var sb = new StringBuilder();
            sb.AppendLine($"[EndgamePacing] seed={seed}  从 Lv.100 巅峰 0 起，在 11-10 循环（5 小怪 + 轮换 BOSS）");
            sb.AppendLine($"目标：巅峰 {targetParagon}（配置上限 {GameConfig.MaxParagonLevel}）");
            sb.AppendLine($"起始：HP {BuildFresh100().MaxHp}  攻 {BuildFresh100().Attack}  传说武器/衣服  0 技能");
            sb.AppendLine();
            if (clearAttempts > 0)
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                    "首次通关 11-10：第 {0} 次挑战，巅峰 {1}，战斗 {2:0.0}min", clearAttempts, clearParagon, clearSeconds / 60f));
            else
                sb.AppendLine($"到 {maxStages} 次挑战仍未通关（巅峰 {p.paragonLevel}）");

            foreach (string m in milestones) sb.AppendLine("  " + m);

            sb.AppendLine();
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "累计：{0} 次挑战 / {1} 击杀 / {2} 阵亡 / 战斗 {3:0.0}min / 含刷怪+商店 {4:0.0}min / 经验 {5:0} / 战斗 XP 每分 {6:0}",
                attempts, kills, deaths, seconds / 60f, WallSeconds() / 60f, totalXp,
                seconds > 0 ? totalXp / (seconds / 60f) : 0f));
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "收尾：Lv.{0} 巅峰 {1}（目标 {2}；配置上限 {3}）  技能 {4}/{5}",
                p.level, p.paragonLevel, targetParagon, GameConfig.MaxParagonLevel, p.OwnedSkillCount, SkillCatalog.All.Length));
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "强化：累计花费 {0:N0} 金币   武器 +{1}  衣服 +{2}",
                enhanceSpent, p.weapon.enhanceLevel, p.armor.enhanceLevel));
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "洗练：累计花费 {0:N0} 金币（目标质量 {1:P0}）   武器 {2:P0}/洗 {3} 次   衣服 {4:P0}/洗 {5} 次   兜里还剩 {6:N0}",
                reforgeSpent, reforgeQuality,
                p.weapon.AffixQuality, p.weapon.reforgeCount,
                p.armor.AffixQuality, p.armor.reforgeCount, p.gold));
            sb.AppendLine("口径：战斗时间不含等待；含刷怪+商店 = 战斗 + 击杀数 × MonsterRespawnDelay(0.5s) + BOSS 次数 × ShopDuration(3s)，不含选关 UI、加载与手动操作。");
            sb.AppendLine("洗练口径：达到目标质量（默认 80%，docs/equipment-reforge.md 的性价比拐点）即停；锁住已达标的词条再洗差的，不追 90%。");

            return sb.ToString();
        }

        /// <summary>Lv.100 + 传说武器/衣服，0 巅峰 0 技能。</summary>
        static PlayerData BuildFresh100()
        {
            var p = new PlayerData();
            p.level = 100;
            p.paragonLevel = 0;
            p.paragonExp = 0;
            p.weapon = LootSystem.CreateEquipment(EquipmentSlot.Weapon, 100, EquipmentQuality.Legendary);
            p.armor = LootSystem.CreateEquipment(EquipmentSlot.Armor, 100, EquipmentQuality.Legendary);
            p.FillHp();
            return p;
        }

    }
}

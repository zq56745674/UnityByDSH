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
    /// 另外它直接回答了设计目标：「巅峰 0 → 200 大约多久」。
    ///
    /// 口径与游戏内一致：小怪连战不回血、BOSS 战前回满、阵亡不给经验、终局 BOSS 类型按次轮换、
    /// 药水在「开商店」时补、掉落/换装/回血都走真实代码（复用 NewGameProgression 的 helper）。
/// 金币要花：探针不花钱时，长线数据全是假的（这个探针以前就是这样：金币堆到六位数，装备却还是 +0）。
    /// 现在每轮 BOSS 战前用 ProbePolicy.SpendGoldOnEnhance 把余钱砸进强化，和真人一致。
    /// </summary>
    public static class EndgamePacing
    {
        [MenuItem("Tools/测试/终局节奏探针（巅峰 0 → 200 要多久）")]
        static void MenuRun() => Debug.Log("[EndgamePacing]\n" + Run());

        public static string Run(int seed = 20261018, int maxStages = 20000, bool verbose = false)
        {
            Random.InitState(seed);

            PlayerData p = BuildFresh100();
            var battle = new BattleSystem();
            var milestones = new List<string>();
            int[] marks = { 1, 30, 50, 100, 150, 200 };
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

            void CheckMarks()
            {
                while (nextMark < marks.Length && p.paragonLevel >= marks[nextMark])
                {
                    string tag = marks[nextMark] >= GameConfig.MaxParagonLevel ? "满级" : ("P" + marks[nextMark]);
                    milestones.Add($"{tag}: {attempts} 次挑战 / {seconds / 60f:0.0}min / {kills} 击杀");
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
                // 药买齐了再把余钱砸进强化：这正是真人的顺序（先保命，再堆属性）。
                enhanceSpent += ProbePolicy.SpendGoldOnEnhance(p);
                p.FillHp();
                MonsterType bossType = (MonsterType)((attempts - 1) % 3);
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

                if (p.paragonLevel >= GameConfig.MaxParagonLevel) break;
            }

            var sb = new StringBuilder();
            sb.AppendLine($"[EndgamePacing] seed={seed}  从 Lv.100 巅峰 0 起，在 11-10 循环（5 小怪 + 轮换 BOSS）");
            sb.AppendLine($"起始：HP {BuildFresh100().MaxHp}  攻 {BuildFresh100().Attack}  传说武器/衣服  0 技能");
            sb.AppendLine();
            if (clearAttempts > 0)
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                    "首次通关 11-10：第 {0} 次挑战，巅峰 {1}，游戏内 {2:0.0}min", clearAttempts, clearParagon, clearSeconds / 60f));
            else
                sb.AppendLine($"到 {maxStages} 次挑战仍未通关（巅峰 {p.paragonLevel}）");

            foreach (string m in milestones) sb.AppendLine("  " + m);

            sb.AppendLine();
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "累计：{0} 次挑战 / {1} 击杀 / {2} 阵亡 / {3:0.0}min / 经验 {4:0} / XP per 分钟 {5:0}",
                attempts, kills, deaths, seconds / 60f, totalXp, seconds > 0 ? totalXp / (seconds / 60f) : 0f));
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "收尾：Lv.{0} 巅峰 {1}（上限 {2}）  技能 {3}/{4}",
                p.level, p.paragonLevel, GameConfig.MaxParagonLevel, p.OwnedSkillCount, SkillCatalog.All.Length));
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "强化：累计花费 {0} 金币   武器 +{1}  衣服 +{2}   兜里还剩 {3}",
                enhanceSpent, p.weapon.enhanceLevel, p.armor.enhanceLevel, p.gold));

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

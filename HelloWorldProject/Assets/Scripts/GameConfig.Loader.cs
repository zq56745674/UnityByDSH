using UnityEngine;

namespace AutoBattle
{
    /// <summary>
    /// GameConfig 的外部配置加载部分：把 <c>Assets/Resources/game_config.json</c>
    /// 里的标量字段反射写回 <see cref="GameConfig"/> 的同名静态字段。
    ///
    /// 注意这一套只覆盖**标量**。表数据（数组）在 <c>GameConfig.Tables.cs</c>，
    /// 那边没有镜像字段、ExportJson 也不会导出，改 json 对它们无效。
    /// </summary>
    public static partial class GameConfig
    {
        // ==================== 外部配置加载 ====================

        /// <summary>外部配置的资源名（对应 Assets/Resources/game_config.json，不带扩展名）。</summary>
        public const string ConfigResourcePath = "game_config";

        /// <summary>本次进程是否成功读到外部配置文件。</summary>
        public static bool ConfigLoaded { get; private set; }

        /// <summary>
        /// 上一次 Apply 命中的字段数。加载时如果比 <see cref="GameConfigData"/> 的字段数少，
        /// <see cref="Apply"/> 会打警告——这正是「新增配置项忘了加镜像字段」的唯一反馈。
        /// </summary>
        public static int ConfigFieldCount { get; private set; }

        /// <summary>
        /// 静态构造：让编辑器脚本（LateGameProbe 的探针、菜单项）在非播放模式下也能读到外部配置。
        /// Resources 系统还没就绪时静默失败，保持下面代码里的默认值。
        /// </summary>
        static GameConfig()
        {
            try { Reload(); }
            catch (System.Exception e) { Debug.LogWarning("[GameConfig] 初始加载失败，使用代码默认值：" + e.Message); }
        }

        /// <summary>进入播放模式时再加载一次（编辑器里改完 JSON 重进 Play 就能生效）。</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AutoLoad()
        {
            // 外部 json 可能被手改坏：不能让 AutoLoad 的异常把整个播放模式启动流程打断。
            try { Reload(); }
            catch (System.Exception e)
            {
                ConfigLoaded = false;
                Debug.LogWarning("[GameConfig] 自动加载失败，使用代码默认值：" + e.Message);
            }
        }

        /// <summary>重新读取 game_config.json。返回是否读到文件。</summary>
        public static bool Reload()
        {
            var asset = Resources.Load<TextAsset>(ConfigResourcePath);
            if (asset == null)
            {
                ConfigLoaded = false;
                return false;
            }
            try
            {
                ApplyJson(asset.text);
                ReportMissingKeys(asset.text);
                ConfigLoaded = true;
                return true;
            }
            catch (System.Exception e)
            {
                ConfigLoaded = false;
                Debug.LogWarning("[GameConfig] game_config.json 解析失败，保留当前值：" + e.Message);
                return false;
            }
        }

        /// <summary>
        /// 外部 JSON 缺字段时 <c>JsonUtility</c> 会静默保留 <see cref="GameConfigData"/> 的默认值，
        /// 于是同一个数值要维护两份。这里把缺的键点名打出来：新增配置项忘了写进 json 时能立刻看见
        /// （2026-10 的四个强化字段就是这么漏掉的，一直靠代码默认值兜底）。
        /// </summary>
        private static void ReportMissingKeys(string json)
        {
            const System.Reflection.BindingFlags inst =
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance;

            var missing = new System.Collections.Generic.List<string>();
            foreach (var f in typeof(GameConfigData).GetFields(inst))
                if (json.IndexOf("\"" + f.Name + "\"", System.StringComparison.Ordinal) < 0)
                    missing.Add(f.Name);

            if (missing.Count == 0) return;
            Debug.LogWarning($"[GameConfig] game_config.json 缺 {missing.Count} 个配置项，"
                + "这些项会用 GameConfigData 的默认值：" + string.Join("、", missing));
        }

        /// <summary>用一段 JSON 覆盖可调数值，返回被覆盖的字段数。</summary>
        public static int ApplyJson(string json)
        {
            var data = new GameConfigData();
            JsonUtility.FromJsonOverwrite(json, data);
            return Apply(data);
        }

        /// <summary>
        /// 把数据载体里的字段反射写回同名静态字段。
        /// 名字对不上、类型不符的项会被跳过——所以改字段名必须同时改两处。
        /// 跳过的项会打警告：否则「新增配置项忘了加镜像字段」是零反馈的，
        /// 编译过、运行过、该字段永远保持代码默认值，没人会发现。
        /// </summary>
        public static int Apply(GameConfigData data)
        {
            const System.Reflection.BindingFlags inst =
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance;
            const System.Reflection.BindingFlags stat =
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static;

            var dst = typeof(GameConfig);
            var fields = typeof(GameConfigData).GetFields(inst);
            int n = 0;
            foreach (var f in fields)
            {
                var target = dst.GetField(f.Name, stat);
                if (target == null)
                {
                    Debug.LogWarning($"[GameConfig] 配置项 {f.Name} 在 GameConfig 里没有同名字段，已跳过。");
                    continue;
                }
                if (target.FieldType != f.FieldType)
                {
                    Debug.LogWarning($"[GameConfig] 配置项 {f.Name} 类型不符"
                        + $"（GameConfigData 是 {f.FieldType.Name}，GameConfig 是 {target.FieldType.Name}），已跳过。");
                    continue;
                }
                target.SetValue(null, f.GetValue(data));
                n++;
            }
            ConfigFieldCount = n;
            if (n != fields.Length)
            {
                Debug.LogWarning($"[GameConfig] 只覆盖了 {n}/{fields.Length} 个配置项——"
                    + "GameConfigData 与 GameConfig 的字段名或类型对不上，没对上的项将永远用代码默认值。");
            }
            SanitizeValues();
            return n;
        }

        // ==================== 配置校验 ====================
        // json 是手改的，0 / 负数 / 越界值都能直接写进来。下面这些点会把「配置写错」
        // 放大成除零、死循环或数组越界，加载后统一夹到安全区间，并留下一条警告。

        private static int ClampInt(int value, int min, int max, string name)
        {
            int clamped = Mathf.Clamp(value, min, max);
            if (clamped != value)
                Debug.LogWarning($"[GameConfig] {name}={value} 超出安全区间 [{min}, {max}]，已夹到 {clamped}。");
            return clamped;
        }

        private static float ClampFloat(float value, float min, float max, string name)
        {
            if (float.IsNaN(value))
            {
                Debug.LogWarning($"[GameConfig] {name}=NaN，已改为 {min}。");
                return min;
            }
            float clamped = Mathf.Clamp(value, min, max);
            if (clamped != value)
                Debug.LogWarning($"[GameConfig] {name}={value} 超出安全区间 [{min}, {max}]，已夹到 {clamped}。");
            return clamped;
        }

        private static void SanitizeValues()
        {
            MaxLevel = ClampInt(MaxLevel, 1, 9999, nameof(MaxLevel));
            // 巅峰上限是玩法口径（2026-10-19 由 200 提到 1000）：配置可以调低，但不能越过 1000。
            MaxParagonLevel = ClampInt(MaxParagonLevel, 0, 1000, nameof(MaxParagonLevel));
            EnhanceMaxLevel = ClampInt(EnhanceMaxLevel, 0, 999, nameof(EnhanceMaxLevel));
            MaxLogEntries = ClampInt(MaxLogEntries, 1, 100000, nameof(MaxLogEntries));

            // 除零 / 死循环 / 索引越界：0 是这些字段最容易被误填的值。
            ParagonPassiveEvery = ClampInt(ParagonPassiveEvery, 1, 9999, nameof(ParagonPassiveEvery));
            ChapterMonsterLevelStep = ClampInt(ChapterMonsterLevelStep, 1, 9999, nameof(ChapterMonsterLevelStep));
            StageKillTargetBase = ClampInt(StageKillTargetBase, 1, 9999, nameof(StageKillTargetBase));
            StageKillTargetMaxBonus = ClampInt(StageKillTargetMaxBonus, 0, 9999, nameof(StageKillTargetMaxBonus));
            StagesPerChapter = ClampInt(StagesPerChapter, 1, 9999, nameof(StagesPerChapter));

            // ChapterMonsterNames / ChapterStatBias 是固定 11 行的表：章节数超出就会索引越界。
            int maxChapters = Mathf.Max(1, Mathf.Min(ChapterMonsterNames.Length, ChapterStatBias.Length));
            ChapterCount = ClampInt(ChapterCount, 1, maxChapters, nameof(ChapterCount));
            FirstBossChapter = ClampInt(FirstBossChapter, 1, ChapterCount, nameof(FirstBossChapter));

            // UI 固定画 6 个槽（三类 × 2），存档也只有 6 个槽字段：
            // 这个值不是自由的每类数量，必须保持 2，否则槽位索引与技能类别会错位。
            SkillSlotsPerKind = ClampInt(SkillSlotsPerKind, PlayerData.SkillSlotsPerKind, PlayerData.SkillSlotsPerKind, nameof(SkillSlotsPerKind));
            SkillChoiceCount = ClampInt(SkillChoiceCount, 1, 15, nameof(SkillChoiceCount));

            AttackIntervalBase = ClampFloat(AttackIntervalBase, 0.01f, 10000f, nameof(AttackIntervalBase));
            MinAttackInterval = ClampFloat(MinAttackInterval, 0.01f, 10000f, nameof(MinAttackInterval));
            AttackSpeedDivisor = ClampFloat(AttackSpeedDivisor, 0.01f, 10000f, nameof(AttackSpeedDivisor));

            // 攻速软上限必须低于硬上限，否则渐近公式的分母为 0 / 曲线反向。
            AttackSpeedSoftCap = ClampFloat(AttackSpeedSoftCap, 0.1f, 100f, nameof(AttackSpeedSoftCap));
            AttackSpeedHardCap = ClampFloat(AttackSpeedHardCap, 0.11f, 100f, nameof(AttackSpeedHardCap));
            if (AttackSpeedHardCap <= AttackSpeedSoftCap)
                AttackSpeedHardCap = ClampFloat(AttackSpeedSoftCap + 0.5f, 0.11f, 100f, nameof(AttackSpeedHardCap));
            StageClearDelay = ClampFloat(StageClearDelay, 0f, 10000f, nameof(StageClearDelay));
            MonsterRespawnDelay = ClampFloat(MonsterRespawnDelay, 0f, 10000f, nameof(MonsterRespawnDelay));
            PlayerReviveDelay = ClampFloat(PlayerReviveDelay, 0.01f, 10000f, nameof(PlayerReviveDelay));
            BannerDuration = ClampFloat(BannerDuration, 0f, 10000f, nameof(BannerDuration));
            AutoSaveInterval = ClampFloat(AutoSaveInterval, 0.1f, 10000f, nameof(AutoSaveInterval));

            // 无尽模式：每个字段都直接进循环/索引/金币公式，0 或负数必须挡住。
            EndlessBaseMonsterLevel = ClampInt(EndlessBaseMonsterLevel, 1, 9999, nameof(EndlessBaseMonsterLevel));
            EndlessMonsterLevelStep = ClampInt(EndlessMonsterLevelStep, 1, 9999, nameof(EndlessMonsterLevelStep));
            EndlessKillTargetBase = ClampInt(EndlessKillTargetBase, 1, 9999, nameof(EndlessKillTargetBase));
            EndlessKillTargetEvery = ClampInt(EndlessKillTargetEvery, 1, 9999, nameof(EndlessKillTargetEvery));
            EndlessKillTargetMax = ClampInt(EndlessKillTargetMax, 1, 9999, nameof(EndlessKillTargetMax));
            EndlessBossEvery = ClampInt(EndlessBossEvery, 1, 9999, nameof(EndlessBossEvery));
            EndlessBaseFactor = ClampFloat(EndlessBaseFactor, 0.01f, 100f, nameof(EndlessBaseFactor));
            EndlessFloorGoldBase = ClampInt(EndlessFloorGoldBase, 0, 1000000, nameof(EndlessFloorGoldBase));
            EndlessFloorGoldPerFloor = ClampInt(EndlessFloorGoldPerFloor, 0, 1000000, nameof(EndlessFloorGoldPerFloor));
            EndlessFirstClearGoldBase = ClampInt(EndlessFirstClearGoldBase, 0, 1000000, nameof(EndlessFirstClearGoldBase));
            EndlessFirstClearGoldPerFloor = ClampInt(EndlessFirstClearGoldPerFloor, 0, 1000000, nameof(EndlessFirstClearGoldPerFloor));
            EndlessFloorClearDelay = ClampFloat(EndlessFloorClearDelay, 0f, 10000f, nameof(EndlessFloorClearDelay));
            EndlessCheckpointOffset = ClampInt(EndlessCheckpointOffset, 0, 9999, nameof(EndlessCheckpointOffset));
            EndlessDropLevelCap = ClampInt(EndlessDropLevelCap, 1, 9999, nameof(EndlessDropLevelCap));

            // 掉落与五槽：概率必须落在 [0,1]；小件倍率与属性系数不能为负。
            DropChance = ClampFloat(DropChance, 0f, 1f, nameof(DropChance));
            DropChancePerExtraSlot = ClampFloat(DropChancePerExtraSlot, 0f, 1f, nameof(DropChancePerExtraSlot));
            SmartDropChance = ClampFloat(SmartDropChance, 0f, 1f, nameof(SmartDropChance));
            MinorAffixScale = ClampFloat(MinorAffixScale, 0f, 2f, nameof(MinorAffixScale));
            HelmetHpFactor = ClampFloat(HelmetHpFactor, 0f, 10f, nameof(HelmetHpFactor));
            HelmetDefenseFactor = ClampFloat(HelmetDefenseFactor, 0f, 10f, nameof(HelmetDefenseFactor));
            BootsBaseSpeed = ClampInt(BootsBaseSpeed, 0, 9999, nameof(BootsBaseSpeed));
            BootsSpeedPerLevel = ClampFloat(BootsSpeedPerLevel, 0f, 999f, nameof(BootsSpeedPerLevel));
            BootsSpeedPerQuality = ClampFloat(BootsSpeedPerQuality, 0f, 999f, nameof(BootsSpeedPerQuality));
            BootsBaseHaste = ClampFloat(BootsBaseHaste, 0f, 10f, nameof(BootsBaseHaste));
            BootsHastePerLevel = ClampFloat(BootsHastePerLevel, 0f, 1f, nameof(BootsHastePerLevel));
            BootsHastePerQuality = ClampFloat(BootsHastePerQuality, 0f, 10f, nameof(BootsHastePerQuality));
            AccessoryBaseCritDamage = ClampFloat(AccessoryBaseCritDamage, 0f, 10f, nameof(AccessoryBaseCritDamage));
            AccessoryCritDamagePerLevel = ClampFloat(AccessoryCritDamagePerLevel, 0f, 1f, nameof(AccessoryCritDamagePerLevel));
            AccessoryBaseArmorPen = ClampFloat(AccessoryBaseArmorPen, 0f, 10f, nameof(AccessoryBaseArmorPen));
            AccessoryArmorPenPerLevel = ClampFloat(AccessoryArmorPenPerLevel, 0f, 1f, nameof(AccessoryArmorPenPerLevel));
            HelmetUnlockChapter = ClampInt(HelmetUnlockChapter, 1, ChapterCount, nameof(HelmetUnlockChapter));
            BootsUnlockChapter = ClampInt(BootsUnlockChapter, HelmetUnlockChapter, ChapterCount, nameof(BootsUnlockChapter));
            AccessoryUnlockChapter = ClampInt(AccessoryUnlockChapter, BootsUnlockChapter, ChapterCount, nameof(AccessoryUnlockChapter));
        }

        /// <summary>把当前生效的数值序列化成 JSON（用于生成初始配置文件，或导出改过的数值）。</summary>
        public static string ExportJson()
        {
            var data = new GameConfigData();
            var src = typeof(GameConfig);
            foreach (var f in typeof(GameConfigData).GetFields(
                         System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
            {
                var from = src.GetField(f.Name,
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                if (from == null || from.FieldType != f.FieldType) continue;
                f.SetValue(data, from.GetValue(null));
            }
            return JsonUtility.ToJson(data, true);
        }
    }
}

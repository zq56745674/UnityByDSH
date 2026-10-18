using UnityEngine;

namespace AutoBattle
{
    /// <summary>
    /// 外置技能表的载体。<c>JsonUtility</c> 不能反序列化顶层数组，
    /// 必须像这样包一层带名字段的对象。
    /// </summary>
    [System.Serializable]
    public class SkillTableData
    {
        /// <summary>技能条目，顺序必须与 <see cref="SkillId"/> 枚举一致。</summary>
        public SkillDef[] skills;
    }

    /// <summary>
    /// <see cref="SkillCatalog"/> 的外部配置部分：把 <c>Assets/Resources/skills.json</c>
    /// 读进来覆盖 15 条技能的数值，调平衡不用重编译。
    ///
    /// 与 <see cref="GameConfig"/> 的标量表有三点关键差别：
    /// <list type="number">
    /// <item>条目**不能增删**——技能槽位分配依赖 <see cref="SkillId"/> 与 <see cref="SkillKind"/>，
    /// 增删会让旧存档的技能槽指向别的技能。所以条目数不符时整表拒绝。</item>
    /// <item>覆盖是**就地写字段**，不替换 <see cref="SkillCatalog.All"/> 里的对象，
    /// 这样任何地方缓存的 <see cref="SkillDef"/> 引用都不会失效。</item>
    /// <item><c>id</c> 与 <c>kind</c> 只校验、不覆盖；<c>name</c>/<c>description</c>/<c>color</c>
    /// 留空时保留代码默认值。真正允许外改的是数值字段。</item>
    /// </list>
    /// </summary>
    public static partial class SkillCatalog
    {
        // ==================== 外部配置加载 ====================

        /// <summary>外部技能表的资源名（对应 Assets/Resources/skills.json，不带扩展名）。</summary>
        public const string SkillsResourcePath = "skills";

        /// <summary>本次进程是否成功读到外部技能表。</summary>
        public static bool ConfigLoaded { get; private set; }

        /// <summary>上一次 <see cref="ApplyJson"/> 实际覆盖的条目数。</summary>
        public static int ConfigSkillCount { get; private set; }

        /// <summary>
        /// 静态构造：让编辑器脚本（探针、菜单项）在非播放模式下也能读到外部技能表。
        /// Resources 系统还没就绪时静默失败，保持 <see cref="All"/> 里的代码默认值。
        /// </summary>
        static SkillCatalog()
        {
            try { Reload(); }
            catch (System.Exception e) { Debug.LogWarning("[SkillCatalog] 初始加载失败，使用代码默认值：" + e.Message); }
        }

        /// <summary>进入播放模式时再加载一次（编辑器里改完 JSON 重进 Play 就能生效）。</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AutoLoad()
        {
            try { Reload(); }
            catch (System.Exception e)
            {
                ConfigLoaded = false;
                Debug.LogWarning("[SkillCatalog] 自动加载失败，使用代码默认值：" + e.Message);
            }
        }

        /// <summary>重新读取 skills.json。返回是否读到文件。</summary>
        public static bool Reload()
        {
            var asset = Resources.Load<TextAsset>(SkillsResourcePath);
            if (asset == null)
            {
                ConfigLoaded = false;
                return false;
            }
            try
            {
                ApplyJson(asset.text);
                ConfigLoaded = true;
                return true;
            }
            catch (System.Exception e)
            {
                ConfigLoaded = false;
                Debug.LogWarning("[SkillCatalog] skills.json 解析失败，保留当前值：" + e.Message);
                return false;
            }
        }

        /// <summary>
        /// 用一段 JSON 覆盖技能数值，返回被覆盖的条目数。
        /// 任何结构性问题（条目数不符、id 错位）都会让**整表**被拒并打警告——
        /// 半张表生效比不生效更难查。
        /// </summary>
        public static int ApplyJson(string json)
        {
            SkillTableData table;
            try { table = JsonUtility.FromJson<SkillTableData>(json); }
            catch (System.Exception e)
            {
                Debug.LogWarning("[SkillCatalog] skills.json 解析失败，沿用代码默认值：" + e.Message);
                return 0;
            }
            if (table == null || table.skills == null)
            {
                Debug.LogWarning("[SkillCatalog] skills.json 解析失败或缺少 skills 数组，沿用代码默认值。");
                return 0;
            }

            if (table.skills.Length != All.Length)
            {
                Debug.LogWarning($"[SkillCatalog] skills.json 有 {table.skills.Length} 条，"
                    + $"代码定义了 {All.Length} 条——技能条目由 SkillId 枚举固定，不能增删。整表已忽略。");
                return 0;
            }

            int n = 0;
            for (int i = 0; i < All.Length; i++)
            {
                var src = table.skills[i];
                if (src == null)
                {
                    Debug.LogWarning($"[SkillCatalog] skills.json 第 {i} 条为空，沿用默认值。");
                    continue;
                }

                // id 是数组下标的镜像，错位说明 json 被重排过——继续用会让技能身份全乱。
                if ((int)src.id != i)
                {
                    Debug.LogWarning($"[SkillCatalog] skills.json 第 {i} 条的 id 是 {src.id}，"
                        + $"与枚举顺序不符（应为 {(SkillId)i}）——整表已忽略。");
                    return 0;
                }

                var dst = All[i];

                // kind 由代码决定：技能槽位按 (int)kind * SkillSlotsPerKind 分配，
                // 改了 kind 会让旧存档的技能落到别的槽位。这里只校验。
                if (src.kind != dst.kind)
                {
                    Debug.LogWarning($"[SkillCatalog] skills.json 第 {i} 条（{dst.id}）的 kind 是 {src.kind}，"
                        + $"代码定义是 {dst.kind}——kind 不可外改，沿用代码值。");
                    src.kind = dst.kind;
                }

                // 技能数值也会手改 json：负冷却 / 负次数会让计时器和循环出错，统一夹住。
                dst.value = SafeFloat(src.value, 0f, 1000f, dst, nameof(dst.value));
                dst.cooldown = SafeFloat(src.cooldown, 0f, 3600f, dst, nameof(dst.cooldown));
                dst.critBonus = SafeFloat(src.critBonus, 0f, 10f, dst, nameof(dst.critBonus));
                dst.hpThreshold = SafeFloat(src.hpThreshold, 0f, 1f, dst, nameof(dst.hpThreshold));
                dst.triggerChance = SafeFloat(src.triggerChance, 0f, 1f, dst, nameof(dst.triggerChance));
                dst.hits = Mathf.Clamp(src.hits, 0, 20);
                dst.duration = SafeFloat(src.duration, 0f, 3600f, dst, nameof(dst.duration));

                if (!string.IsNullOrWhiteSpace(src.name)) dst.name = src.name;
                if (!string.IsNullOrWhiteSpace(src.description)) dst.description = src.description;

                // Color 没有「缺省」表示，JsonUtility 缺失时给 (0,0,0,0)：
                // 全零视为没写，保留代码里的配色。
                if (src.color.r > 0f || src.color.g > 0f || src.color.b > 0f || src.color.a > 0f)
                    dst.color = src.color;

                n++;
            }

            ConfigSkillCount = n;
            if (n != All.Length)
                Debug.LogWarning($"[SkillCatalog] 只覆盖了 {n}/{All.Length} 条技能，其余沿用代码默认值。");

            return n;
        }

        private static float SafeFloat(float value, float min, float max, SkillDef skill, string field)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                Debug.LogWarning($"[SkillCatalog] {skill.id}.{field}={value} 非法，已改为 {min}。");
                return min;
            }
            float clamped = Mathf.Clamp(value, min, max);
            if (clamped != value)
                Debug.LogWarning($"[SkillCatalog] {skill.id}.{field}={value} 超出安全区间 [{min}, {max}]，已夹到 {clamped}。");
            return clamped;
        }

        /// <summary>把当前生效的技能表序列化成 JSON（生成初始配置文件，或导出改过的数值）。</summary>
        public static string ExportJson()
        {
            var data = new SkillTableData
            {
                skills = new SkillDef[All.Length],
            };
            for (int i = 0; i < All.Length; i++)
            {
                var s = All[i];
                data.skills[i] = new SkillDef
                {
                    id = s.id,
                    kind = s.kind,
                    name = s.name,
                    description = s.description,
                    cooldown = s.cooldown,
                    value = s.value,
                    critBonus = s.critBonus,
                    hpThreshold = s.hpThreshold,
                    triggerChance = s.triggerChance,
                    hits = s.hits,
                    duration = s.duration,
                    color = s.color,
                };
            }
            return JsonUtility.ToJson(data, true);
        }
    }
}

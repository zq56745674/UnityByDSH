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
        private static void AutoLoad() => Reload();

        /// <summary>重新读取 game_config.json。返回是否读到文件。</summary>
        public static bool Reload()
        {
            var asset = Resources.Load<TextAsset>(ConfigResourcePath);
            if (asset == null)
            {
                ConfigLoaded = false;
                return false;
            }
            ApplyJson(asset.text);
            ConfigLoaded = true;
            return true;
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
            return n;
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

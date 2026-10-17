using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AutoBattle.EditorTools
{
    /// <summary>
    /// skills.json 外置技能表的自检。菜单 <c>Tools/技能表验证/跑一遍</c>。
    ///
    /// 为什么用菜单而不是 <c>unity command eval</c>：eval 的 Roslyn 脚本模式对多行
    /// C# 会误报 CS0162 Unreachable code detected 并整体失败，改走编辑器程序集最稳。
    /// </summary>
    public static class SkillTableVerify
    {
        const string JsonAssetPath = "Assets/Resources/skills.json";

        [MenuItem("Tools/技能表验证/跑一遍")]
        public static void RunAll()
        {
            var sb = new StringBuilder();
            sb.AppendLine("[SkillTableVerify] ===== 技能表外置自检 =====");

            // ---- 1. 加载状态 ----
            sb.AppendLine($"加载状态：ConfigLoaded={SkillCatalog.ConfigLoaded}"
                + $"  ConfigSkillCount={SkillCatalog.ConfigSkillCount}"
                + $"  代码条目数={SkillCatalog.All.Length}");

            // ---- 2. 逐条数值（用于与 skills.json 对照）----
            sb.AppendLine("---- 当前生效技能表 ----");
            for (int i = 0; i < SkillCatalog.All.Length; i++)
            {
                var s = SkillCatalog.All[i];
                sb.AppendLine($"  [{i:00}] {s.id,-14} {s.kind,-7} {s.name,-6}"
                    + $" val={s.value:0.####} cd={s.cooldown:0.####}"
                    + $" crit={s.critBonus:0.####} hpTh={s.hpThreshold:0.####}"
                    + $" trig={s.triggerChance:0.####} hits={s.hits} dur={s.duration:0.####}");
            }

            // ---- 3. 字段拆分是否干净 ----
            bool hasExtra = false;
            foreach (var f in typeof(SkillDef).GetFields()) if (f.Name == "extra") hasExtra = true;
            sb.AppendLine($"SkillDef 仍存在 extra 字段：{hasExtra}（应为 False）");

            // ---- 4. 覆盖生效 ----
            float before = SkillCatalog.Get(SkillId.HeavyStrike).value;
            string patched = SkillCatalog.ExportJson().Replace("\"value\": 2.5,", "\"value\": 9.99,");
            int applied = SkillCatalog.ApplyJson(patched);
            float after = SkillCatalog.Get(SkillId.HeavyStrike).value;
            sb.AppendLine($"覆盖测试：{before} -> applied={applied} -> {after}（应为 9.99）");

            // ---- 5. 非法 JSON / 缺 skills 数组 -> 整表拒绝 ----
            int badShape = SkillCatalog.ApplyJson("{\"nope\":1}");
            sb.AppendLine($"非法结构测试：ApplyJson(\"{{\\\"nope\\\":1}}\") 返回 {badShape}（应为 0）");

            // ---- 6. 条目数不符 -> 整表拒绝 ----
            int badCount = SkillCatalog.ApplyJson("{\"skills\":[]}");
            sb.AppendLine($"条目数不符测试：空数组返回 {badCount}（应为 0）");

            // ---- 7. id 错位 -> 整表拒绝 ----
            int badId = SkillCatalog.ApplyJson(SkillCatalog.ExportJson().Replace("\"id\": 0,", "\"id\": 5,"));
            sb.AppendLine($"id 错位测试：返回 {badId}（应为 0）");

            // ---- 8. Reload 从磁盘恢复（验证前面的破坏都只影响内存）----
            bool reloaded = SkillCatalog.Reload();
            sb.AppendLine($"Reload：{reloaded}  value(破坏前 2.5 -> 现在 {SkillCatalog.Get(SkillId.HeavyStrike).value})");

            // ---- 9. 真·热改：改磁盘 json -> 重新导入 -> Reload -> 值应变化 ----
            RunHotReloadCheck(sb);

            Debug.Log(sb.ToString());
        }

        /// <summary>改磁盘上的 skills.json，重新导入并 Reload，确认改动能生效。</summary>
        static void RunHotReloadCheck(StringBuilder sb)
        {
            if (!File.Exists(JsonAssetPath))
            {
                sb.AppendLine($"热改测试：找不到 {JsonAssetPath}，跳过。");
                return;
            }

            string original = File.ReadAllText(JsonAssetPath);
            try
            {
                // 探针必须落在「全表唯一」的数值上：0.55 只属于不死之身，
                // 而 0.08 同时属于迅捷和狂暴之心，Replace 会一次改两条。
                string tweaked = original.Replace("\"value\": 0.55,", "\"value\": 0.99,");
                if (tweaked == original)
                {
                    sb.AppendLine("热改测试：没找到可替换的 \"value\": 0.55,，跳过。");
                    return;
                }

                File.WriteAllText(JsonAssetPath, tweaked);
                AssetDatabase.ImportAsset(JsonAssetPath, ImportAssetOptions.ForceUpdate);
                SkillCatalog.Reload();

                float undying = SkillCatalog.Get(SkillId.Undying).value;
                float counter = SkillCatalog.Get(SkillId.Counter).value;
                sb.AppendLine($"热改测试：不死之身 value 0.55 -> {undying:0.####}（应为 0.99）"
                    + $"；反击 {counter:0.####}（应仍为 0.7，确认只改了目标条目）");
            }
            finally
            {
                File.WriteAllText(JsonAssetPath, original);
                AssetDatabase.ImportAsset(JsonAssetPath, ImportAssetOptions.ForceUpdate);
                SkillCatalog.Reload();
                sb.AppendLine($"热改测试：已还原，不死之身 value={SkillCatalog.Get(SkillId.Undying).value:0.####}（应为 0.55）");
            }
        }

        [MenuItem("Tools/技能表验证/导出当前技能表到 json")]
        public static void ExportToJson()
        {
            File.WriteAllText(JsonAssetPath, SkillCatalog.ExportJson());
            AssetDatabase.ImportAsset(JsonAssetPath, ImportAssetOptions.ForceUpdate);
            Debug.Log($"[SkillTableVerify] 已把当前生效技能表写入 {JsonAssetPath}");
        }
    }
}

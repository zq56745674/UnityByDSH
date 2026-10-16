using UnityEditor;
using UnityEngine;

namespace AutoBattle.EditorTools
{
    /// <summary>
    /// 改完 Assets/Resources/game_config.json 后自动重载，省掉「重进 Play 模式」这一步。
    /// 只影响编辑器：打包后没有 AssetPostprocessor，走 GameConfig.AutoLoad()。
    /// </summary>
    public class GameConfigReloader : AssetPostprocessor
    {
        private const string ConfigFile = "game_config.json";

        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            if (!Touched(importedAssets) && !Touched(movedAssets)) return;

            bool ok = GameConfig.Reload();
            Debug.Log(ok
                ? $"[GameConfig] 已重载 {ConfigFile}，覆盖 {GameConfig.ConfigFieldCount} 个字段"
                    + $"（ConfigLoaded={GameConfig.ConfigLoaded}）。"
                : $"[GameConfig] 未找到 {ConfigFile}，全部使用代码默认值（ConfigLoaded={GameConfig.ConfigLoaded}）。");
        }

        private static bool Touched(string[] paths)
        {
            foreach (string p in paths)
            {
                if (p.Replace('\\', '/').EndsWith("/" + ConfigFile)) return true;
            }
            return false;
        }

        [MenuItem("AutoBattle/Reload Game Config")]
        private static void ReloadFromMenu()
        {
            bool ok = GameConfig.Reload();
            Debug.Log(ok
                ? $"[GameConfig] 手动重载完成，覆盖 {GameConfig.ConfigFieldCount} 个字段。"
                : "[GameConfig] 没有找到 game_config.json。");
        }

        [MenuItem("AutoBattle/Export Current Config To JSON")]
        private static void ExportToJson()
        {
            string path = "Assets/Resources/" + ConfigFile;
            System.IO.File.WriteAllText(path, GameConfig.ExportJson(), new System.Text.UTF8Encoding(false));
            AssetDatabase.ImportAsset(path);
            Debug.Log($"[GameConfig] 已把当前生效数值导出到 {path}。");
        }
    }
}

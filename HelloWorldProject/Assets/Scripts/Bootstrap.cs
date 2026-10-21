using UnityEngine;

namespace AutoBattle
{
    /// <summary>
    /// 入口（要求 20）：场景里只需要一个挂着本脚本的空物体。
    /// 它负责组装控制器与界面 —— <see cref="GameController"/> 跑逻辑，
    /// <see cref="GameUI"/> 负责绘制，两者通过控制器暴露的状态单向连接。
    /// </summary>
    public class Bootstrap : MonoBehaviour
    {
        private void Awake()
        {
            // AddComponent 会立即触发新组件的 Awake：音频先就绪，控制器与 UI 才有得用。
            gameObject.AddComponent<GameAudio>();
            // 背景音乐独立于短音效，但要早于 UI 建立，音乐入口才能立刻工作。
            gameObject.AddComponent<GameMusic>();
            var controller = gameObject.AddComponent<GameController>();
            var ui = gameObject.AddComponent<GameUI>();
            ui.Initialize(controller);

            Debug.Log("[AutoBattle] 初始化完成。CONFIG 位于 GameConfig.cs，可调数值与公式。");
        }
    }
}

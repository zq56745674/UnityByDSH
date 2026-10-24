using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.Events;

namespace AutoBattle
{
    public partial class GameUI : MonoBehaviour
    {
        // 居中横幅：首通奖励与章节解锁提示
        // ==================== 居中横幅（步骤 4c）====================

        /// <summary>
        /// 屏幕中央的提示横幅，首通奖励与章节解锁共用。
        /// 挂在最后、带 CanvasGroup，所以能盖在选关 / 技能面板上面淡出。
        /// </summary>
        private void BuildBanner(Transform root)
        {
            _bannerPanel = CreatePanel(root, "Banner", 340, 186, 600, 120,
                GameConfig.ScreenDim);

            // 金边：横幅要盖在日志上，光靠底色压不住下面的字。改用圆角描边 + 顶部金线。
            AddBorder(_bannerPanel.GetComponent<Image>(), RadiusPanel, GameConfig.AccentBanner);
            CreateImage(_bannerPanel, "BannerTopLine", 18, 0, 564, 2, GameConfig.AccentBanner);

            _bannerGroup = _bannerPanel.gameObject.AddComponent<CanvasGroup>();
            _bannerGroup.blocksRaycasts = false;
            _bannerGroup.interactable = false;

            _bannerTitle = CreateText(_bannerPanel, "BannerTitle", "", 16, 20, 568, 46, 28,
                GameConfig.AccentBannerTitle, TextAnchor.MiddleCenter, FontStyle.Bold);

            _bannerSubtitle = CreateText(_bannerPanel, "BannerSubtitle", "", 16, 68, 568, 34, 17,
                GameConfig.TextInfo, TextAnchor.MiddleCenter);

            _bannerPanel.gameObject.SetActive(false);
        }

        /// <summary>刷新横幅：内容、显隐、最后 0.6 秒淡出。</summary>
        private void RefreshBanner()
        {
            if (_bannerPanel == null || _game == null) return;

            bool show = !string.IsNullOrEmpty(_game.BannerTitle) && _game.BannerTimeLeft > 0f;
            _bannerPanel.gameObject.SetActive(show);
            if (!show) return;

            _bannerTitle.text = _game.BannerTitle;
            _bannerSubtitle.text = _game.BannerSubtitle;

            const float fadeWindow = 0.6f;
            _bannerGroup.alpha = Mathf.Clamp01(_game.BannerTimeLeft / fadeWindow);
        }

    }
}

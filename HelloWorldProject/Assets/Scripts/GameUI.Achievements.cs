using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace AutoBattle
{
    /// <summary>
    /// 成就界面（docs/achievements.md 第 3 节）：左栏入口、成就面板、解锁 toast。
    /// 面板只读控制器状态；解锁/切档事件由 AchievementSystem 主动推过来。
    /// </summary>
    public partial class GameUI
    {
        private const int AchRowsPerPage = 16;
        private const int AchColumns = 2;
        private const float AchToastHold = 2.5f;
        private const float AchToastFade = 0.3f;

        // ---- 左栏入口 ----
        private Button _achievementButton;
        private Text _achievementButtonLabel;

        // ---- 面板 ----
        private RectTransform _achievementPanel;
        private Text _achSummaryText;
        private Text _achPageText;
        private Text _achEmptyText;
        private Text _achFilterLabel;
        private Button _achFilterButton;
        private Button[] _achTabButtons;
        private Text[] _achTabLabels;
        private Button _achPrevButton, _achNextButton;
        private Text _achPrevLabel, _achNextLabel;
        private Image[] _achRowBg;
        private Image[] _achRowStrip;
        private Text[] _achRowName;
        private Text[] _achRowDesc;
        private Text[] _achRowProgress;
        private Text[] _achRowPoints;

        private int _achTabIndex;
        private bool _achOnlyUnfinished;
        private int _achPageIndex;
        private bool _achDirty = true;
        private bool _achWasOpen;
        private int _achVersion = -1;
        private float _achRefreshTimer;
        private readonly List<AchievementDef> _achVisible = new List<AchievementDef>(AchRowsPerPage);

        // ---- 解锁 toast ----
        private RectTransform _achToastPanel;
        private CanvasGroup _achToastGroup;
        private Text _achToastTitle;
        private Text _achToastSubtitle;
        private float _achToastTimer;
        private bool _achToastActive;
        private readonly Queue<AchievementToastItem> _achToastQueue = new Queue<AchievementToastItem>();

        private struct AchievementToastItem
        {
            public AchievementDef def;
            public int gold;
        }

        private void BuildAchievements(Transform root)
        {
            _achievementPanel = CreatePanel(root, "AchievementPanel", 236, 80, 808, 560,
                GameConfig.OverlayBg, true);

            CreateSectionTitle(_achievementPanel, "成就", 20, 12, 260, FontTitle);

            _achSummaryText = CreateText(_achievementPanel, "AchSummary", "", 100, 14, 570, 24,
                FontSmall + 1, GameConfig.TextInfo, TextAnchor.MiddleLeft);

            Button close = CreateButton(_achievementPanel, "AchClose", "关闭", 698, 12, 90, 30,
                GameConfig.CardBtn, out _);
            close.onClick.AddListener(OnAchievementCloseClicked);

            // 7 个页签：全部 + 六个类别。
            _achTabButtons = new Button[AchievementCatalog.CategoryKeys.Length];
            _achTabLabels = new Text[AchievementCatalog.CategoryKeys.Length];
            for (int i = 0; i < _achTabButtons.Length; i++)
            {
                int index = i;
                _achTabButtons[i] = CreateButton(_achievementPanel, "AchTab" + i,
                    AchievementCatalog.CategoryLabel(AchievementCatalog.CategoryKeys[i]),
                    20 + i * 104, 78, 96, 28, GameConfig.BtnHeader, out _achTabLabels[i]);
                _achTabLabels[i].fontSize = FontSmall;
                _achTabButtons[i].onClick.AddListener(() => OnAchievementTabClicked(index));
            }

            // 2 列 × 8 行，行内从左上往右排。
            _achRowBg = new Image[AchRowsPerPage];
            _achRowStrip = new Image[AchRowsPerPage];
            _achRowName = new Text[AchRowsPerPage];
            _achRowDesc = new Text[AchRowsPerPage];
            _achRowProgress = new Text[AchRowsPerPage];
            _achRowPoints = new Text[AchRowsPerPage];

            for (int i = 0; i < AchRowsPerPage; i++)
            {
                int col = i % AchColumns;
                int row = i / AchColumns;
                float x = 20 + col * 384;
                float y = 112 + row * 49;

                _achRowBg[i] = CreateCard(_achievementPanel, "AchRow" + i, x, y, 376, 44,
                    GameConfig.CardBg, RadiusCard, GameConfig.BorderSoft);
                _achRowStrip[i] = CreateImage(_achRowBg[i].transform, "Strip", 8, 7, 4, 30,
                    GameConfig.TextMuted);
                _achRowName[i] = CreateText(_achRowBg[i].transform, "Name", "", 18, 3, 250, 18,
                    FontSmall + 1, GameConfig.TextNormal, TextAnchor.MiddleLeft, FontStyle.Bold);
                _achRowPoints[i] = CreateText(_achRowBg[i].transform, "Points", "", 292, 3, 72, 18,
                    FontTiny, GameConfig.TextGold, TextAnchor.MiddleRight);
                _achRowDesc[i] = CreateText(_achRowBg[i].transform, "Desc", "", 18, 22, 206, 17,
                    FontTiny, GameConfig.TextMuted, TextAnchor.MiddleLeft);
                _achRowProgress[i] = CreateText(_achRowBg[i].transform, "Progress", "", 226, 22, 138, 17,
                    FontTiny, GameConfig.TextInfo, TextAnchor.MiddleRight);
                _achRowBg[i].gameObject.SetActive(false);
            }

            _achEmptyText = CreateText(_achievementPanel, "AchEmpty", "", 20, 300, 768, 40,
                FontBody, GameConfig.TextMuted, TextAnchor.MiddleCenter);
            _achEmptyText.gameObject.SetActive(false);

            _achFilterButton = CreateButton(_achievementPanel, "AchFilter", "只看未完成",
                20, 508, 120, 28, GameConfig.BtnHeader, out _achFilterLabel);
            _achFilterLabel.fontSize = FontTiny + 1;
            _achFilterButton.onClick.AddListener(OnAchievementFilterClicked);

            _achPrevButton = CreateButton(_achievementPanel, "AchPrev", "上一页", 496, 508, 80, 28,
                GameConfig.BtnHeader, out _achPrevLabel);
            _achPrevLabel.fontSize = FontTiny + 1;
            _achPrevButton.onClick.AddListener(OnAchievementPrevClicked);

            _achPageText = CreateText(_achievementPanel, "AchPage", "1 / 1", 584, 508, 80, 28,
                FontSmall, GameConfig.TextMuted, TextAnchor.MiddleCenter);

            _achNextButton = CreateButton(_achievementPanel, "AchNext", "下一页", 672, 508, 80, 28,
                GameConfig.BtnHeader, out _achNextLabel);
            _achNextLabel.fontSize = FontTiny + 1;
            _achNextButton.onClick.AddListener(OnAchievementNextClicked);

            CreateText(_achievementPanel, "AchFootNote", "成就点只用于展示，不提供战斗属性。",
                20, 536, 600, 18, 10, GameConfig.TextDisabled, TextAnchor.MiddleLeft);

            _achievementPanel.gameObject.SetActive(false);

            AchievementSystem.Unlocked += OnAchievementUnlocked;
            AchievementSystem.ContextReset += OnAchievementContextReset;
        }

        private void BuildAchievementToast(Transform root)
        {
            _achToastPanel = CreatePanel(root, "AchievementToast", 600, 430, 500, 56,
                GameConfig.OverlayBg, false);
            AddBorder(_achToastPanel.GetComponent<Image>(), RadiusPanel, GameConfig.AccentBanner);

            _achToastGroup = _achToastPanel.gameObject.AddComponent<CanvasGroup>();
            _achToastGroup.alpha = 0f;
            _achToastGroup.blocksRaycasts = false;
            _achToastGroup.interactable = false;

            CreateImage(_achToastPanel, "AchToastAccent", 0, 10, 4, 36, GameConfig.AccentBanner);
            _achToastTitle = CreateText(_achToastPanel, "AchToastTitle", "", 16, 4, 470, 26,
                FontBody, GameConfig.TextGold, TextAnchor.MiddleLeft, FontStyle.Bold);
            _achToastSubtitle = CreateText(_achToastPanel, "AchToastSub", "", 16, 29, 470, 22,
                FontTiny + 1, GameConfig.TextInfo, TextAnchor.MiddleLeft);

            _achToastPanel.gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            AchievementSystem.Unlocked -= OnAchievementUnlocked;
            AchievementSystem.ContextReset -= OnAchievementContextReset;
        }

        // ==================== 事件 ====================

        private void OnAchievementUnlocked(AchievementDef def, int gold)
        {
            if (def == null) return;
            _achToastQueue.Enqueue(new AchievementToastItem { def = def, gold = gold });
        }

        private void OnAchievementContextReset()
        {
            _achToastQueue.Clear();
            HideAchievementToast();

            // 切档/重开/转生后绑的是另一个 PlayerData，清掉行缓存与页码，避免残留上一档的勾选。
            _achPageIndex = 0;
            _achDirty = true;
            _achWasOpen = false;
            _achVersion = -1;
        }

        // ==================== 点击 ====================

        private void OnAchievementButtonClicked()
        {
            _game.ToggleAchievementPanel();
        }

        private void OnAchievementCloseClicked()
        {
            _game.CloseAchievementPanel();
        }

        private void OnAchievementTabClicked(int index)
        {
            _achTabIndex = Mathf.Clamp(index, 0, AchievementCatalog.CategoryKeys.Length - 1);
            _achPageIndex = 0;
            _achDirty = true;
        }

        private void OnAchievementFilterClicked()
        {
            _achOnlyUnfinished = !_achOnlyUnfinished;
            _achPageIndex = 0;
            _achDirty = true;
        }

        private void OnAchievementPrevClicked()
        {
            if (_achPageIndex <= 0) return;
            _achPageIndex--;
            _achDirty = true;
        }

        private void OnAchievementNextClicked()
        {
            int pages = Mathf.Max(1, (_achVisible.Count + AchRowsPerPage - 1) / AchRowsPerPage);
            if (_achPageIndex >= pages - 1) return;
            _achPageIndex++;
            _achDirty = true;
        }

        // ==================== 每帧刷新 ====================

        private void RefreshAchievementPanel()
        {
            if (_achievementPanel == null || _game == null || _game.Player == null) return;

            PlayerData p = _game.Player;

            if (_achievementButtonLabel != null)
            {
                int unlocked = CountUnlocked(p);
                string entry = unlocked > 0 ? $"成就 {unlocked}" : "成就";
                if (!string.Equals(_achievementButtonLabel.text, entry))
                    _achievementButtonLabel.text = entry;
            }

            RefreshAchievementToast();

            bool open = _game.AchievementPanelOpen;
            _achievementPanel.gameObject.SetActive(open);

            if (!open)
            {
                _achWasOpen = false;
                return;
            }

            if (!_achWasOpen)
            {
                _achWasOpen = true;
                _achDirty = true;
                _achRefreshTimer = 0f;
            }

            if (AchievementSystem.Version != _achVersion)
            {
                _achVersion = AchievementSystem.Version;
                _achDirty = true;
            }

            if (_achDirty)
            {
                RebuildAchievementList(p);
                _achDirty = false;
            }
            else
            {
                _achRefreshTimer -= Time.unscaledDeltaTime;
                if (_achRefreshTimer <= 0f)
                {
                    _achRefreshTimer = 0.5f;
                    UpdateAchievementRowProgress(p);
                }
            }
        }

        private int CountUnlocked(PlayerData p)
        {
            if (p == null || p.achievements == null || p.achievements.unlocked == null) return 0;
            int n = 0;
            IReadOnlyList<AchievementDef> all = AchievementCatalog.All;
            for (int i = 0; i < all.Count; i++)
                if (p.achievements.IsUnlocked(all[i].id)) n++;
            return n;
        }

        private void RebuildAchievementList(PlayerData p)
        {
            _achVisible.Clear();
            string category = AchievementCatalog.CategoryKeys[_achTabIndex];

            int unlockedCount = 0, earnedPoints = 0;
            IReadOnlyList<AchievementDef> all = AchievementCatalog.All;
            for (int i = 0; i < all.Count; i++)
            {
                AchievementDef def = all[i];
                if (def == null) continue;
                bool done = p.achievements.IsUnlocked(def.id);
                if (done)
                {
                    unlockedCount++;
                    earnedPoints += def.points;
                }

                if (category != "all" && def.category != category) continue;
                if (_achOnlyUnfinished && done) continue;
                _achVisible.Add(def);
            }

            int pages = Mathf.Max(1, (_achVisible.Count + AchRowsPerPage - 1) / AchRowsPerPage);
            _achPageIndex = Mathf.Clamp(_achPageIndex, 0, pages - 1);

            _achSummaryText.text =
                $"已完成 <b>{unlockedCount}/{AchievementCatalog.Count}</b> · 成就点 <b>{earnedPoints}</b>";

            for (int i = 0; i < _achTabButtons.Length; i++)
            {
                bool active = i == _achTabIndex;
                _achTabButtons[i].image.color = active ? GameConfig.BtnPrimary : GameConfig.BtnHeader;
                _achTabLabels[i].color = active ? Color.white : GameConfig.TextMuted;
            }

            _achFilterButton.image.color = _achOnlyUnfinished
                ? GameConfig.BtnPrimary
                : GameConfig.BtnHeader;
            _achFilterLabel.text = _achOnlyUnfinished ? "只看未完成 ✓" : "只看未完成";
            _achFilterLabel.color = _achOnlyUnfinished ? Color.white : GameConfig.TextMuted;

            _achPageText.text = $"{_achPageIndex + 1} / {pages}";
            _achPrevButton.interactable = _achPageIndex > 0;
            _achNextButton.interactable = _achPageIndex < pages - 1;
            _achPrevLabel.color = _achPrevButton.interactable ? Color.white : GameConfig.BtnDisabledText;
            _achNextLabel.color = _achNextButton.interactable ? Color.white : GameConfig.BtnDisabledText;

            for (int i = 0; i < AchRowsPerPage; i++)
            {
                int index = _achPageIndex * AchRowsPerPage + i;
                if (index >= _achVisible.Count)
                {
                    _achRowBg[i].gameObject.SetActive(false);
                    continue;
                }

                _achRowBg[i].gameObject.SetActive(true);
                FillAchievementRow(i, _achVisible[index], p);
            }

            bool empty = _achVisible.Count == 0;
            _achEmptyText.gameObject.SetActive(empty);
            if (empty)
            {
                _achEmptyText.text = AchievementCatalog.Loaded
                    ? (_achOnlyUnfinished ? "当前分类已全部完成。" : "当前分类没有成就。")
                    : "成就表未载入：找不到 Resources/achievements.json。";
            }
        }

        private void FillAchievementRow(int row, AchievementDef def, PlayerData p)
        {
            bool done = p.achievements.IsUnlocked(def.id);
            Color tier = AchievementCatalog.TierColor(def.tier);

            _achRowBg[row].color = done ? GameConfig.CardBgDim : GameConfig.CardBg;
            _achRowStrip[row].color = done
                ? new Color(tier.r, tier.g, tier.b, 0.45f)
                : tier;

            // 称号放名称行，避免挤占描述；名称行只有名字+短称号，250 宽足够。
            _achRowName[row].text = string.IsNullOrEmpty(def.title)
                ? def.name
                : $"{def.name}  <size=10><color=#FFD966>「{def.title}」</color></size>";
            _achRowName[row].color = done ? GameConfig.TextDisabled : GameConfig.TextNormal;

            _achRowDesc[row].text = TrimAchievementDesc(def.desc, 17);
            _achRowDesc[row].color = done ? GameConfig.TextDisabled : GameConfig.TextMuted;

            _achRowPoints[row].text = $"+{def.points}";
            _achRowPoints[row].color = done ? GameConfig.TextMuted : GameConfig.TextGold;
            _achRowPoints[row].alignment = TextAnchor.MiddleRight;

            SetAchievementProgressText(row, def, p);
        }

        private void SetAchievementProgressText(int row, AchievementDef def, PlayerData p)
        {
            bool done = p.achievements.IsUnlocked(def.id);
            if (done)
            {
                _achRowProgress[row].text = "✓ 已完成";
                _achRowProgress[row].color = GameConfig.LogSuccess;
                return;
            }

            long value = AchievementSystem.Metric(p, def.metric);
            if (value > def.target) value = def.target;
            _achRowProgress[row].text = $"{value} / {def.target}";
            _achRowProgress[row].color = GameConfig.TextInfo;
        }

        private void UpdateAchievementRowProgress(PlayerData p)
        {
            int count = Mathf.Min(AchRowsPerPage, _achVisible.Count - _achPageIndex * AchRowsPerPage);
            for (int i = 0; i < count; i++)
                SetAchievementProgressText(i, _achVisible[_achPageIndex * AchRowsPerPage + i], p);
        }

        /// <summary>描述只排一行；有称号时先截描述，保证金色称号不被挤掉。</summary>
        private static string TrimAchievementDesc(string text, int max)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            if (text.Length <= max) return text;
            return text.Substring(0, max) + "…";
        }

        // ==================== toast ====================

        private void RefreshAchievementToast()
        {
            if (_achToastPanel == null) return;

            if (!_achToastActive)
            {
                if (_achToastQueue.Count > 0) ShowNextAchievementToast();
                else return;
            }

            // 模态浮层或通关横幅可见时隐藏 toast 并暂停计时；恢复后接着播完。
            bool paused = (_modalDim != null && _modalDim.gameObject.activeSelf)
                || (_game != null && _game.BannerTimeLeft > 0f);

            if (paused)
            {
                if (_achToastPanel.gameObject.activeSelf) _achToastPanel.gameObject.SetActive(false);
                return;
            }

            // 暂停期间面板被隐藏，恢复后先显示出来再继续倒计时。
            if (!_achToastPanel.gameObject.activeSelf) _achToastPanel.gameObject.SetActive(true);

            _achToastTimer -= Time.unscaledDeltaTime;
            if (_achToastTimer <= 0f)
            {
                HideAchievementToast();
                if (_achToastQueue.Count > 0) ShowNextAchievementToast();
                return;
            }

            _achToastGroup.alpha = Mathf.Clamp01(_achToastTimer / AchToastFade);
        }

        private void ShowNextAchievementToast()
        {
            if (_achToastQueue.Count == 0) return;

            AchievementToastItem item = _achToastQueue.Dequeue();
            AchievementDef def = item.def;
            if (def == null) return;

            _achToastPanel.gameObject.SetActive(true);
            _achToastTitle.text = $"成就解锁 · {def.name}    +{def.points} 成就点";

            string sub = string.Empty;
            if (item.gold > 0) sub = $"+{item.gold} 金币";
            if (!string.IsNullOrEmpty(def.title))
                sub += (sub.Length > 0 ? " · " : "") + $"称号「{def.title}」";
            if (sub.Length == 0) sub = "已记入成就面板";
            _achToastSubtitle.text = sub;

            _achToastGroup.alpha = 1f;
            _achToastTimer = AchToastHold + AchToastFade;
            _achToastActive = true;
        }

        private void HideAchievementToast()
        {
            _achToastActive = false;
            _achToastTimer = 0f;
            if (_achToastGroup != null) _achToastGroup.alpha = 0f;
            if (_achToastPanel != null) _achToastPanel.gameObject.SetActive(false);
        }
    }
}

using UnityEngine;
using UnityEngine.UI;

namespace AutoBattle
{
    /// <summary>
    /// 轮回（转生）界面：存档面板里的入口、轮回面板、五条永久升级与二次确认。
    /// 规则与写盘都在 GameController.Rebirth.cs / RebirthCatalog.cs，这里只负责显示。
    /// </summary>
    public partial class GameUI
    {
        // ---- 存档面板里的入口 ----
        private Button _rebirthEntryButton;
        private Text _rebirthEntryLabel;
        private Text _rebirthEntryStatus;

        // ---- 轮回面板 ----
        private RectTransform _rebirthPanel;
        private Text _rebirthTitle;
        private Text _rebirthIntro;
        private Text _rebirthLeftText;
        private Text _rebirthRightText;
        private readonly Text[] _rebirthNameTexts = new Text[RebirthCatalog.TrackCount];
        private readonly Text[] _rebirthEffectTexts = new Text[RebirthCatalog.TrackCount];
        private readonly Text[] _rebirthRankTexts = new Text[RebirthCatalog.TrackCount];
        private readonly Button[] _rebirthBuyButtons = new Button[RebirthCatalog.TrackCount];
        private readonly Text[] _rebirthBuyLabels = new Text[RebirthCatalog.TrackCount];
        private Text _rebirthGainText;
        private Text _rebirthKeepText;
        private Text _rebirthResetText;
        private Text _rebirthReasonText;
        private Button _rebirthConfirmButton;
        private Text _rebirthConfirmLabel;

        /// <summary>二次确认的截止时间（Time.unscaledTime）；0 表示未上膛。</summary>
        private float _rebirthConfirmUntil;
        private bool _rebirthWasOpen;

        /// <summary>
        /// 轮回面板。尺寸按 docs/rebirth.md：680×540，五行永久升级，每行一个购买按钮。
        /// 入口在存档面板，因为它不可逆、低频，不适合放在左栏常驻按钮里。
        /// </summary>
        private void BuildRebirthPanel(Transform root)
        {
            _rebirthPanel = CreatePanel(root, "RebirthPanel", 300, 90, 680, 540,
                GameConfig.OverlayBg, true);

            _rebirthTitle = CreateText(_rebirthPanel, "RebirthTitle", "轮回", 20, 12, 400, 30,
                FontTitle, GameConfig.TextTitle, TextAnchor.MiddleLeft, FontStyle.Bold);

            Button close = CreateButton(_rebirthPanel, "RebirthClose", "关闭", 566, 12, 90, 30,
                GameConfig.CardBtn, out _);
            close.onClick.AddListener(OnRebirthCloseClicked);

            _rebirthIntro = CreateText(_rebirthPanel, "RebirthIntro",
                "重置本世，按巅峰 / 无尽 / 金币折算轮回印记，再用印记购买永久升级。",
                20, 46, 640, 20, FontSmall, GameConfig.TextMuted, TextAnchor.MiddleLeft);

            // 左：本世成绩；右：印记折算。两张卡高度 78，留出两行。
            CreateCard(_rebirthPanel, "RebirthLeftCard", 20, 72, 310, 78, GameConfig.CardBg);
            _rebirthLeftText = CreateText(_rebirthPanel, "RebirthLeftText", "", 32, 80, 286, 62,
                FontSmall, GameConfig.TextInfo, TextAnchor.UpperLeft);
            _rebirthLeftText.lineSpacing = 1.15f;

            CreateCard(_rebirthPanel, "RebirthRightCard", 340, 72, 300, 78, GameConfig.CardBg);
            _rebirthRightText = CreateText(_rebirthPanel, "RebirthRightText", "", 352, 80, 276, 62,
                FontSmall, GameConfig.TextInfo, TextAnchor.UpperLeft);
            _rebirthRightText.lineSpacing = 1.15f;

            // 五行永久升级：整行卡片 + 名称/效果/等级 + 右侧购买按钮。
            for (int i = 0; i < RebirthCatalog.TrackCount; i++)
            {
                RebirthTrack track = (RebirthTrack)i;
                float y = 160f + i * 54f;

                CreateCard(_rebirthPanel, "RebirthRow" + i, 20, y, 640, 48, GameConfig.CardBg);

                _rebirthNameTexts[i] = CreateText(_rebirthPanel, "RebirthName" + i,
                    RebirthCatalog.NameOf(track), 32, y + 5, 250, 18,
                    FontBody + 1, GameConfig.TextTitle, TextAnchor.MiddleLeft, FontStyle.Bold);

                _rebirthEffectTexts[i] = CreateText(_rebirthPanel, "RebirthEffect" + i, "",
                    32, y + 24, 460, 18, FontTiny + 1, GameConfig.TextMuted, TextAnchor.MiddleLeft);

                _rebirthRankTexts[i] = CreateText(_rebirthPanel, "RebirthRank" + i, "",
                    360, y + 14, 100, 20, FontSmall, GameConfig.TextInfo, TextAnchor.MiddleCenter);

                int index = i;
                _rebirthBuyButtons[i] = CreateButton(_rebirthPanel, "RebirthBuy" + i, "",
                    512, y + 9, 112, 30, GameConfig.BtnGold, out _rebirthBuyLabels[i]);
                _rebirthBuyLabels[i].fontSize = 13;
                _rebirthBuyButtons[i].onClick.AddListener(() => OnRebirthBuyClicked(index));
            }

            _rebirthGainText = CreateText(_rebirthPanel, "RebirthGain", "", 20, 438, 640, 20,
                FontSmall, GameConfig.TextInfo, TextAnchor.MiddleLeft);
            _rebirthKeepText = CreateText(_rebirthPanel, "RebirthKeep", "", 20, 460, 640, 18,
                FontTiny + 1, GameConfig.TextMuted, TextAnchor.MiddleLeft);
            _rebirthResetText = CreateText(_rebirthPanel, "RebirthReset", "", 20, 480, 640, 18,
                FontTiny + 1, GameConfig.TextMuted, TextAnchor.MiddleLeft);

            _rebirthConfirmButton = CreateButton(_rebirthPanel, "RebirthConfirm", "轮回", 20, 502, 190, 34,
                GameConfig.BtnGold, out _rebirthConfirmLabel);
            _rebirthConfirmLabel.fontSize = 14;
            _rebirthConfirmButton.onClick.AddListener(OnRebirthConfirmClicked);

            _rebirthReasonText = CreateText(_rebirthPanel, "RebirthReason", "", 220, 502, 440, 34,
                FontTiny + 1, GameConfig.TextMuted, TextAnchor.MiddleLeft);

            _rebirthPanel.gameObject.SetActive(false);
        }

        /// <summary>每帧刷新轮回面板。行数固定且文本短，不做脏标记。</summary>
        private void RefreshRebirthPanel()
        {
            if (_rebirthPanel == null || _game == null || _game.Player == null) return;

            bool open = _game.RebirthPanelOpen;
            _rebirthPanel.gameObject.SetActive(open);
            if (!open)
            {
                _rebirthWasOpen = false;
                _rebirthConfirmUntil = 0f;
                return;
            }

            float now = Time.unscaledTime;
            if (!_rebirthWasOpen)
            {
                _rebirthWasOpen = true;
                _rebirthConfirmUntil = 0f;
            }

            PlayerData p = _game.Player;
            int nextLife = p.rebirthCount + 1;
            int marks = _game.PendingRebirthMarks;
            int paragonMarks = _game.PendingRebirthParagonMarks;
            int floorMarks = _game.PendingRebirthFloorMarks;
            int goldMarks = _game.PendingRebirthGoldMarks;

            _rebirthTitle.text = $"轮回 —— 第 {nextLife} 世";
            string bestFloorText = p.endlessBestFloor > 0 ? $"第 {p.endlessBestFloor} 层" : "—";
            _rebirthLeftText.text =
                $"本世 Lv.{p.level} · 巅峰 {p.paragonLevel}\n" +
                $"无尽最高 {bestFloorText} · 金币 {p.gold:N0}";
            _rebirthRightText.text =
                $"预计 +{marks} 印记\n" +
                $"<color=#8C99B0>巅峰 {paragonMarks} + 无尽 {floorMarks} + 金币 {goldMarks} + 基础 1</color>";

            for (int i = 0; i < RebirthCatalog.TrackCount; i++)
            {
                RebirthTrack track = (RebirthTrack)i;
                int rank = p.GetRebirthRank(track);
                bool max = rank >= RebirthCatalog.MaxRank;
                int cost = RebirthCatalog.CostOf(track, rank);

                _rebirthRankTexts[i].text = $"Lv.{rank} / {RebirthCatalog.MaxRank}";
                _rebirthEffectTexts[i].text = max
                    ? $"满级：{RebirthCatalog.EffectText(track, RebirthCatalog.MaxRank)}"
                    : $"当前 {RebirthCatalog.EffectText(track, rank)}   →   {RebirthCatalog.EffectText(track, rank + 1)}";

                _rebirthBuyLabels[i].text = max ? "已满级" : $"{cost} 印记";
                bool affordable = !max && p.rebirthMarks >= cost;
                _rebirthBuyButtons[i].interactable = affordable;
                _rebirthBuyLabels[i].color = max
                    ? GameConfig.TextDisabled
                    : affordable ? Color.white : GameConfig.TextMuted;
            }

            _rebirthGainText.text =
                $"本次可得 <b>{marks}</b> 印记" +
                $"<color=#8C99B0>（巅峰 {paragonMarks} + 无尽 {floorMarks} + 金币 {goldMarks} + 基础 {RebirthCatalog.BaseMarks}）</color>";
            _rebirthKeepText.text =
                $"保留：印记余额 {p.rebirthMarks} → {p.rebirthMarks + marks} · 无尽最高 {bestFloorText} · 成就与全局纪录";
            _rebirthResetText.text =
                $"重置：Lv.{p.level} → Lv.{p.RebirthStartLevel}，金币 → {p.RebirthStartGold:N0}，药剂 → {p.RebirthStartPotions}；关卡 / 装备 / 技能 / 巅峰清零";

            bool can = _game.CanRebirth(out string reason);
            if (!can) _rebirthConfirmUntil = 0f;

            if (can && _rebirthConfirmUntil > now)
            {
                _rebirthConfirmLabel.text = $"确认转生（{Mathf.CeilToInt(_rebirthConfirmUntil - now)}）";
            }
            else
            {
                _rebirthConfirmLabel.text = can ? $"轮回（+{marks} 印记）" : "暂不能转生";
            }

            _rebirthConfirmButton.interactable = can;
            _rebirthReasonText.text = can ? "转生后本世从头开始，操作不可撤销。" : reason;
            _rebirthReasonText.color = can ? GameConfig.TextMuted : GameConfig.LogWarn;
        }

        /// <summary>存档面板里的入口状态：未解锁 / 有印记 / 可转或原因。</summary>
        private void RefreshRebirthEntry()
        {
            if (_rebirthEntryButton == null || _game == null || _game.Player == null) return;

            bool unlocked = _game.RebirthUnlocked;
            _rebirthEntryButton.interactable = unlocked;
            _rebirthEntryLabel.color = unlocked ? Color.white : GameConfig.TextDisabled;

            PlayerData p = _game.Player;
            int nextLife = p.rebirthCount + 1;

            if (!unlocked)
            {
                _rebirthEntryStatus.text = "通关 11-10 后解锁";
                _rebirthEntryStatus.color = GameConfig.TextMuted;
                return;
            }

            if (p.rebirthMarks > 0)
            {
                _rebirthEntryStatus.text = $"第 {nextLife} 世 · 印记 {p.rebirthMarks}";
                _rebirthEntryStatus.color = GameConfig.LogGold;
                return;
            }

            bool can = _game.CanRebirth(out string reason);
            _rebirthEntryStatus.text = can
                ? $"第 {nextLife} 世 · 预计 {_game.PendingRebirthMarks} 印记"
                : $"第 {nextLife} 世 · {reason}";
            _rebirthEntryStatus.color = can ? GameConfig.TextInfo : GameConfig.TextMuted;
        }

        private void OnRebirthEntryClicked()
        {
            if (_game == null) return;
            _savePanelDirty = true;
            _game.ToggleRebirthPanel();
        }

        private void OnRebirthCloseClicked()
        {
            _rebirthConfirmUntil = 0f;
            if (_game != null) _game.CloseRebirthPanel();
        }

        private void OnRebirthBuyClicked(int index)
        {
            if (_game == null) return;
            _game.BuyRebirthRank((RebirthTrack)index);
        }

        private void OnRebirthConfirmClicked()
        {
            if (_game == null || !_game.CanRebirth(out _)) return;

            float now = Time.unscaledTime;
            if (_rebirthConfirmUntil <= now)
            {
                // 第一次只上膛：3 秒内再点一次才真的重置，避免误触。
                _rebirthConfirmUntil = now + ConfirmWindow;
                return;
            }

            _rebirthConfirmUntil = 0f;
            _game.PerformRebirth();
        }
    }
}

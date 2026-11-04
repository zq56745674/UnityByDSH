using UnityEngine;
using UnityEngine.UI;

namespace AutoBattle
{
    /// <summary>
    /// 屠魔录面板（方案 D）：11 族 × 4 型 = 44 条，按击杀数分「未目击 / 已目击 / 已通晓」三档。
    /// 只做收藏与展示——不加战斗数值、不新增遗物/契约/成就条目，
    /// 免得稀释三选一池子、也免得动 AchievementCatalog 那张严格校验的内容表。
    /// 入口挂在成就面板头部（左栏图标行已经满了，塞不进第 6 个）。
    /// </summary>
    public partial class GameUI
    {
        private const int BestiaryRowHeight = 40;
        private const int BestiaryRowStep = 47;
        private const float BestiaryRowsTop = 76f;

        private RectTransform _bestiaryPanel;
        private Text _bestiarySummary;
        private Text _bestiaryFooter;

        private readonly Image[] _bestiaryCardBgs = new Image[BestiaryCatalog.EntryCount];
        private readonly Image[] _bestiaryCardIcons = new Image[BestiaryCatalog.EntryCount];
        private readonly Text[] _bestiaryCardNames = new Text[BestiaryCatalog.EntryCount];
        private readonly Text[] _bestiaryCardProgress = new Text[BestiaryCatalog.EntryCount];
        private readonly Image[] _bestiaryCardSeals = new Image[BestiaryCatalog.EntryCount];
        private readonly Image[] _bestiaryCardRings = new Image[BestiaryCatalog.EntryCount];
        private readonly Sprite[] _bestiaryArt = new Sprite[BestiaryCatalog.EntryCount];
        private readonly Text[] _bestiaryFamilyLabels = new Text[BestiaryCatalog.FamilyCount];

        private Button _bestiaryEntryButton;
        private Text _bestiaryEntryLabel;

        private bool _bestiaryWasOpen;
        private bool _bestiaryDirty = true;
        private int _bestiarySeenVersion = -1;

        private void BuildBestiaryPanel(Transform root)
        {
            _bestiaryPanel = CreatePanel(root, "BestiaryPanel", 236, 80, 808, 620,
                GameConfig.OverlayBg, true);

            CreateSectionTitle(_bestiaryPanel, "屠魔录", 20, 12, 260, FontTitle, "glyph_swords");

            _bestiarySummary = CreateText(_bestiaryPanel, "BestiarySummary", "", 132, 14, 400, 24,
                FontSmall, GameConfig.TextMuted, TextAnchor.UpperLeft);

            Button close = CreateButton(_bestiaryPanel, "BestiaryClose", "关闭", 698, 12, 90, 30,
                GameConfig.CardBg, out Text closeLabel);
            close.onClick.AddListener(OnBestiaryCloseClicked);
            StyleBar(close, closeLabel, "frame_pill", "glyph_close", 14f, "关闭屠魔录");

            CreateText(_bestiaryPanel, "BestiaryHint",
                "击杀累计到门槛即通晓；只做收藏记录，不改战斗数值。",
                20, 46, 768, 18, FontTiny, GameConfig.TextMuted, TextAnchor.UpperLeft);

            for (int family = 0; family < BestiaryCatalog.FamilyCount; family++)
            {
                float rowY = BestiaryRowsTop + family * BestiaryRowStep;

                _bestiaryFamilyLabels[family] = CreateText(_bestiaryPanel, "BestiaryFamily" + family, "",
                    20, rowY + 11, 84, 18, FontSmall, GameConfig.TextMuted, TextAnchor.UpperLeft);

                for (int kind = 0; kind < BestiaryCatalog.KindCount; kind++)
                {
                    int index = BestiaryCatalog.IndexOf(family, kind);
                    if (index < 0) continue;

                    float x = 108f + kind * 172f;
                    // raycast=true：卡片的悬停提示要靠 UITooltipArea 收事件，不挡射线就永远不亮。
                    Image card = CreateCard(_bestiaryPanel, "BestiaryCard" + index, x, rowY, 164, BestiaryRowHeight,
                        GameConfig.CardBg, RadiusCard, GameConfig.BorderSoft, true);
                    _bestiaryCardBgs[index] = card;

                    _bestiaryArt[index] = SpriteOf(BestiaryCatalog.Get(index).spritePath);

                    // 通晓卡加一圈烫金外环：只靠底色加深在 44 张里太不明显。
                    Image ring = CreateImage(card.transform, "BestiaryRing" + index, 0, 0, 164, BestiaryRowHeight,
                        GameConfig.LogGold);
                    ring.sprite = RingSprite(RadiusCard);
                    ring.type = Image.Type.Sliced;
                    ring.pixelsPerUnitMultiplier = 1f;
                    ring.raycastTarget = false;
                    Stretch(ring.rectTransform);
                    ring.gameObject.SetActive(false);
                    _bestiaryCardRings[index] = ring;

                    Image icon = CreateImage(card.transform, "BestiaryArt" + index, 5, 7, 26, 26, Color.white);
                    icon.sprite = _bestiaryArt[index];
                    icon.preserveAspect = true;
                    icon.raycastTarget = false;
                    if (icon.sprite == null) icon.enabled = false;
                    _bestiaryCardIcons[index] = icon;

                    _bestiaryCardNames[index] = CreateText(card.transform, "BestiaryName" + index, "",
                        36, 1, 122, 17, FontTiny, GameConfig.TextNormal, TextAnchor.UpperLeft);

                    _bestiaryCardProgress[index] = CreateText(card.transform, "BestiaryProgress" + index, "",
                        36, 19, 122, 16, FontTiny, GameConfig.TextMuted, TextAnchor.UpperLeft);

                    _bestiaryCardSeals[index] = CreateIcon(card.transform, "glyph_confirm", 146, 4, 12);
                    _bestiaryCardSeals[index].gameObject.SetActive(false);
                }
            }

            _bestiaryFooter = CreateText(_bestiaryPanel, "BestiaryFooter",
                "族印 = 一族四条全部通晓，纯展示：不追加战斗数值，也不进技能/遗物池。",
                20, 590, 768, 18, FontTiny, GameConfig.TextMuted, TextAnchor.UpperLeft);

            // 入口按钮：建在成就面板头部（本方法在 BuildAchievements 之后调用）。
            if (_achievementPanel != null)
            {
                _bestiaryEntryButton = CreateButton(_achievementPanel, "BestiaryEntry", "屠魔录", 552, 12, 140, 30,
                    GameConfig.CardBg, out _bestiaryEntryLabel);
                _bestiaryEntryButton.onClick.AddListener(OnBestiaryEntryClicked);
                StyleBar(_bestiaryEntryButton, _bestiaryEntryLabel, "frame_pill", "glyph_swords", 12f,
                    "打开屠魔录（44 条）", 12);
            }

            _bestiaryPanel.gameObject.SetActive(false);
        }

        private void OnBestiaryEntryClicked()
        {
            if (_game == null) return;
            _game.ToggleBestiaryPanel();
            GameAudio.PlayUiOpen();
        }

        private void OnBestiaryCloseClicked()
        {
            if (_game == null) return;
            _game.CloseBestiaryPanel();
            GameAudio.PlayUiClose();
        }

        private void RefreshBestiaryPanel()
        {
            if (_bestiaryPanel == null || _game == null || _game.Player == null) return;

            PlayerData player = _game.Player;

            if (_bestiaryEntryLabel != null)
            {
                string entry = "屠魔录 " + BestiaryCatalog.MasteredCount(player) + "/" + BestiaryCatalog.EntryCount;
                if (!string.Equals(_bestiaryEntryLabel.text, entry))
                    _bestiaryEntryLabel.text = entry;
            }

            bool open = _game.BestiaryPanelOpen;
            _bestiaryPanel.gameObject.SetActive(open);

            if (!open)
            {
                _bestiaryWasOpen = false;
                return;
            }

            if (!_bestiaryWasOpen)
            {
                _bestiaryWasOpen = true;
                _bestiaryDirty = true;
            }

            if (_bestiarySeenVersion != _game.BestiaryVersion)
            {
                _bestiarySeenVersion = _game.BestiaryVersion;
                _bestiaryDirty = true;
            }

            if (!_bestiaryDirty) return;
            _bestiaryDirty = false;

            RebuildBestiaryRows(player);
        }

        private void RebuildBestiaryRows(PlayerData player)
        {
            _bestiarySummary.text = "已通晓 " + BestiaryCatalog.MasteredCount(player) + "/" + BestiaryCatalog.EntryCount
                + " · 目击 " + BestiaryCatalog.WitnessedCount(player) + "/" + BestiaryCatalog.EntryCount
                + " · 族印 " + BestiaryCatalog.CompleteFamilyCount(player) + "/" + BestiaryCatalog.FamilyCount;

            for (int family = 0; family < BestiaryCatalog.FamilyCount; family++)
            {
                Text label = _bestiaryFamilyLabels[family];
                if (label == null) continue;

                bool complete = BestiaryCatalog.IsFamilyComplete(player, family);
                string name = BestiaryCatalog.FamilyKeys[family];
                BestiaryEntry first = BestiaryCatalog.Get(BestiaryCatalog.IndexOf(family, 0));
                if (first != null) name = first.familyName;

                label.text = complete ? "✦ " + name : name;
                label.color = complete ? GameConfig.TextGold : GameConfig.TextMuted;
            }

            for (int index = 0; index < BestiaryCatalog.EntryCount; index++)
            {
                BestiaryEntry entry = BestiaryCatalog.Get(index);
                if (entry == null) continue;

                bool witnessed = BestiaryCatalog.IsWitnessed(player, index);
                bool mastered = BestiaryCatalog.IsMastered(player, index);
                int kills = BestiaryCatalog.KillsOf(player, index);

                Text nameText = _bestiaryCardNames[index];
                if (nameText != null)
                {
                    string text = witnessed ? entry.entryName : "？？？";
                    if (!string.Equals(nameText.text, text)) nameText.text = text;
                    nameText.color = mastered ? GameConfig.TextGold
                        : witnessed ? GameConfig.TextNormal : GameConfig.TextDisabled;
                }

                Text progressText = _bestiaryCardProgress[index];
                if (progressText != null)
                {
                    string text = BestiaryCatalog.ProgressText(player, index);
                    if (!string.Equals(progressText.text, text)) progressText.text = text;
                    progressText.color = mastered ? GameConfig.TextGold : GameConfig.TextMuted;
                }

                Image art = _bestiaryCardIcons[index];
                if (art != null && art.sprite != null)
                    art.color = witnessed ? Color.white : new Color(0.18f, 0.16f, 0.15f, 0.85f);

                if (_bestiaryCardBgs[index] != null)
                    _bestiaryCardBgs[index].color = mastered ? GameConfig.CardBgActive : GameConfig.CardBg;

                if (_bestiaryCardSeals[index] != null)
                    _bestiaryCardSeals[index].gameObject.SetActive(mastered);

                if (_bestiaryCardRings[index] != null)
                    _bestiaryCardRings[index].gameObject.SetActive(mastered);

                if (_bestiaryCardBgs[index] != null)
                {
                    string tip = witnessed
                        ? entry.entryName + "\n" + entry.kindName + " · 第 " + entry.chapter + " 章\n击杀 "
                          + kills + " / " + entry.target + (mastered ? "（已通晓）" : "")
                        : "第 " + entry.chapter + " 章 · " + entry.familyName + " 族的" + entry.kindName + "\n尚未目击";
                    SetTooltip(_bestiaryCardBgs[index].rectTransform, tip);
                }
            }
        }
    }
}

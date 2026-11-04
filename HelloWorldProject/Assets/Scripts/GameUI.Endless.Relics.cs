using UnityEngine;
using UnityEngine.UI;

namespace AutoBattle
{
    /// <summary>
    /// 深渊遗物三选一面板（docs/gameplay-expansion.md §4）。
    ///
    /// 这一层是**强制停牌**：无尽清完第 10/20/30… 层时弹出，整局冻结
    /// （<see cref="GameController.RelicChoiceOpen"/> 在 GameController.Update 的冻结名单里），
    /// 玩家必须选一张或按「跳过本次」才能继续。
    /// </summary>
    public partial class GameUI
    {
        private RectTransform _relicPanel;
        private Text _relicSubtitle;
        private Text _relicHint;
        private Button[] _relicChoiceButtons;
        private Text[] _relicChoiceNames;
        private Text[] _relicChoiceClasses;
        private Text[] _relicChoiceTexts;
        private Text[] _relicChoiceStrengths;
        private Image[] _relicChoiceTags;

        private Button[] _relicOwnedButtons;
        private Text[] _relicOwnedLabels;
        private Text _relicOwnedTitle;
        private Text _relicFooterHint;
        private Button _relicSkipButton;
        private Text _relicSkipLabel;

        /// <summary>当前三张候选的遗物 id（刷新时写入，点牌时按槽位取）。</summary>
        private readonly int[] _relicChoiceIds = { -1, -1, -1 };

        /// <summary>当前显示的六个槽位对应的遗物 id（-1 = 空槽）。</summary>
        private readonly int[] _relicOwnedIds = { -1, -1, -1, -1, -1, -1 };

        private void BuildRelicPanel(Transform root)
        {
            // 860×470 居中：比无尽面板(680)宽，因为三张牌并排要 264 宽。
            _relicPanel = CreatePanel(root, "RelicChoicePanel", 210, 120, 860, 470,
                GameConfig.OverlayBg, true);

            CreateIcon(_relicPanel, "ui_endless", 24, 14, 24);
            CreateText(_relicPanel, "RelicTitle", "深渊馈赠", 58, 12, 420, 30,
                FontTitle, GameConfig.TextTitle, TextAnchor.MiddleLeft, FontStyle.Bold);
            _relicSubtitle = CreateText(_relicPanel, "RelicSubtitle", "", 58, 40, 760, 22,
                FontSmall + 1, GameConfig.TextMuted, TextAnchor.MiddleLeft);

            _relicHint = CreateText(_relicPanel, "RelicHint",
                "点击一张牌立即获得。遗物只在本局无尽生效，撤退/阵亡后清空。", 24, 74, 800, 22,
                FontTiny + 2, GameConfig.TextInfo, TextAnchor.MiddleLeft);

            // ---- 三张候选卡 ----
            string[] clsFallback = { "锋", "守", "贪", "险" };
            _relicChoiceButtons = new Button[RelicCatalog.ChoiceCount];
            _relicChoiceNames = new Text[RelicCatalog.ChoiceCount];
            _relicChoiceClasses = new Text[RelicCatalog.ChoiceCount];
            _relicChoiceTexts = new Text[RelicCatalog.ChoiceCount];
            _relicChoiceStrengths = new Text[RelicCatalog.ChoiceCount];
            _relicChoiceTags = new Image[RelicCatalog.ChoiceCount];

            for (int i = 0; i < _relicChoiceButtons.Length; i++)
            {
                int index = i;
                // 候选牌是本次三选一的主角，按「实体卡牌」的方向给硬投影 + 烫金细边 + 纸面高光，
                // 别跟功能按钮混成一块平面色块。投影必须先建，靠兄弟顺序垫在卡下面。
                Image cardShadow = CreateHardShadow(_relicPanel, "RelicChoiceShadow" + i,
                    24 + i * 274, 110, 264, 150, RadiusCard);
                Button card = CreateButton(_relicPanel, "RelicChoice" + i, "", 24 + i * 274, 110, 264, 150,
                    GameConfig.CardBg, out _);
                AddBorder(card.image, RadiusCard, GameConfig.Border);
                AddPaperSheen(card.image);
                LinkShadow(card.gameObject, cardShadow);
                card.onClick.AddListener(() => OnRelicChoiceClicked(index));
                _relicChoiceButtons[i] = card;

                Image tag = CreateImage(card.image.rectTransform, "RelicChoiceTag" + i, 12, 12, 60, 22,
                    GameConfig.SlotBg);
                Round(tag, RadiusButton);
                AddBorder(tag, RadiusButton, GameConfig.Border);
                _relicChoiceTags[i] = tag;
                _relicChoiceClasses[i] = CreateText(tag.rectTransform, "TagText", clsFallback[0],
                    0, 0, 60, 22, FontTiny + 2, GameConfig.BtnTextGold, TextAnchor.MiddleCenter, FontStyle.Bold);
                Stretch(_relicChoiceClasses[i].rectTransform);

                _relicChoiceNames[i] = CreateText(card.image.rectTransform, "RelicChoiceName" + i, "", 12, 40,
                    240, 28, FontBody + 2, GameConfig.TextTitle, TextAnchor.MiddleLeft, FontStyle.Bold);

                _relicChoiceTexts[i] = CreateText(card.image.rectTransform, "RelicChoiceText" + i, "", 12, 72,
                    240, 66, FontSmall + 1, GameConfig.TextNormal, TextAnchor.UpperLeft);
                _relicChoiceTexts[i].lineSpacing = 1.15f;

                // 右上角实测强度（方案①）：让玩家在「守类很强、经济类很弱」这件事上
                // 不需要靠猜——数值来自 RelicStrength 的静态表，悬停有口径说明。
                _relicChoiceStrengths[i] = CreateText(card.image.rectTransform, "RelicChoiceStrength" + i,
                    "", 124, 12, 128, 22, FontSmall + 1, GameConfig.TextMuted,
                    TextAnchor.MiddleRight, FontStyle.Bold);
            }

            CreateImage(_relicPanel, "RelicDivider", 24, 272, 812, 2, GameConfig.Divider);

            _relicOwnedTitle = CreateText(_relicPanel, "RelicOwnedTitle", "本局遗物 0/6", 24, 282, 500, 22,
                FontSmall + 2, GameConfig.TextInfo, TextAnchor.MiddleLeft, FontStyle.Bold);

            _relicFooterHint = CreateText(_relicPanel, "RelicFooterHint", "", 540, 282, 296, 22,
                FontTiny + 1, GameConfig.TextMuted, TextAnchor.MiddleRight);

            // ---- 六个槽位（3 列 × 2 行）：满槽时变成「点一件舍弃」 ----
            _relicOwnedButtons = new Button[RelicCatalog.SlotCount];
            _relicOwnedLabels = new Text[RelicCatalog.SlotCount];
            for (int i = 0; i < _relicOwnedButtons.Length; i++)
            {
                int index = i;
                Button chip = CreateButton(_relicPanel, "RelicSlot" + i, "", 24 + (i % 3) * 274,
                    308 + (i / 3) * 48, 264, 42, GameConfig.CardBgDim, out _relicOwnedLabels[i]);
                chip.onClick.AddListener(() => OnRelicSlotClicked(index));
                _relicOwnedButtons[i] = chip;
            }

            _relicSkipButton = CreateButton(_relicPanel, "RelicSkip", "跳过本次", 24, 410, 200, 40,
                GameConfig.CardBtn, out _relicSkipLabel);
            StyleBar(_relicSkipButton, _relicSkipLabel, "frame_pill", "glyph_back", 17f,
                "放弃这一次三选一，直接继续挑战");
            _relicSkipButton.onClick.AddListener(OnRelicSkipClicked);

            CreateText(_relicPanel, "RelicFooterNote",
                "单件等效战力 ≤ +4%，六槽全满 ≤ +18%；右上角为实测层数（对照不带遗物）。", 244, 418, 592, 24,
                FontTiny + 1, GameConfig.TextMuted, TextAnchor.MiddleLeft);

            _relicPanel.gameObject.SetActive(false);
        }

        /// <summary>
        /// 每帧刷新：面板开合、三张候选、六个槽位与页脚。信息量很小，直接按状态重写。
        /// </summary>
        private void RefreshRelicPanel()
        {
            if (_relicPanel == null || _game == null) return;

            EndlessRunState run = _game.EndlessRun;
            bool open = _game.RelicChoiceOpen && run != null;
            if (_relicPanel.gameObject.activeSelf != open) _relicPanel.gameObject.SetActive(open);
            if (!open) return;

            int pending = _game.RelicTakePending;
            bool replacing = pending >= 0;

            _relicSubtitle.text = replacing
                ? $"第 {run.relicPendingFloor} 层 · 槽位已满：先点一件旧遗物舍弃，再换入「{(RelicCatalog.Get(pending) != null ? RelicCatalog.Get(pending).name : "")}」"
                : $"第 {run.relicPendingFloor} 层 · 三选一（本局 {run.relics.Count}/{run.RelicSlots} 槽）";

            _relicHint.text = replacing
                ? "下面六件里选一件舍弃；舍弃不可撤销。"
                : "点击一张牌立即获得。遗物只在本局无尽生效，撤退/阵亡后清空。";

            // ---- 三张候选 ----
            for (int i = 0; i < _relicChoiceButtons.Length; i++)
            {
                bool has = i < run.pendingRelics.Count;
                _relicChoiceButtons[i].gameObject.SetActive(has);
                _relicChoiceIds[i] = has ? run.pendingRelics[i] : -1;
                if (!has) continue;

                RelicDef def = RelicCatalog.Get(_relicChoiceIds[i]);
                if (def == null) continue;

                // 替换模式下三张牌都点不动：先选出要舍弃的那件。
                _relicChoiceButtons[i].interactable = !replacing;
                _relicChoiceClasses[i].text = def.ClassName;
                _relicChoiceTags[i].color = def.ClassColor;
                _relicChoiceNames[i].text = def.name;
                _relicChoiceTexts[i].text = def.text;
                _relicChoiceStrengths[i].text = "实测 " + RelicStrength.Chip(_relicChoiceIds[i]);
                _relicChoiceStrengths[i].color = RelicStrength.ColorOf(_relicChoiceIds[i]);
                SetTooltip(_relicChoiceButtons[i], RelicStrength.Tooltip(_relicChoiceIds[i]));
            }

            // ---- 六个槽位 ----
            int owned = run.relics.Count;
            _relicOwnedTitle.text = $"本局遗物 {owned}/{run.RelicSlots}";

            for (int i = 0; i < _relicOwnedButtons.Length; i++)
            {
                int id = i < owned ? run.relics[i] : -1;
                _relicOwnedIds[i] = id;

                bool clickable = replacing && id >= 0;
                _relicOwnedButtons[i].interactable = clickable;
                _relicOwnedButtons[i].image.color = clickable
                    ? GameConfig.BtnDanger
                    : (id >= 0 ? GameConfig.CardBg : GameConfig.CardBgDim);

                if (id < 0)
                {
                    _relicOwnedLabels[i].text = "<color=#7A6A55>空槽</color>";
                    _relicOwnedLabels[i].fontSize = FontSmall + 1;
                    continue;
                }

                RelicDef def = RelicCatalog.Get(id);
                string name = def != null ? def.name : "?";
                string cls = def != null ? def.ClassName : "";
                _relicOwnedLabels[i].text = clickable
                    ? $"舍弃「{name}」"
                    : $"<color=#8A5A12>{cls}</color>  {name}";
                _relicOwnedLabels[i].fontSize = clickable ? FontSmall + 1 : FontSmall + 2;
            }

            _relicFooterHint.text = replacing ? "点一件旧遗物完成替换" : "满 6 槽后需先舍弃一件";

            _relicSkipLabel.text = replacing ? "取消替换" : "跳过本次";
        }

        // ==================== 点击 ====================

        private void OnRelicChoiceClicked(int index)
        {
            if (_game == null || index < 0 || index >= _relicChoiceIds.Length) return;
            int id = _relicChoiceIds[index];
            if (id < 0) return;
            _game.TakeRelic(id);
        }

        private void OnRelicSlotClicked(int index)
        {
            if (_game == null || index < 0 || index >= _relicOwnedIds.Length) return;
            // 只有在「满槽替换」模式下槽位才可点；其余时候是纯展示。
            if (_game.RelicTakePending < 0) return;
            int id = _relicOwnedIds[index];
            if (id < 0) return;
            _game.ReplaceRelicWith(id);
        }

        private void OnRelicSkipClicked()
        {
            if (_game == null) return;
            // 替换模式下的「取消」只退掉选牌，不结束这次三选一。
            if (_game.RelicTakePending >= 0)
            {
                _game.CancelRelicTake();
                return;
            }
            _game.SkipRelicChoice();
        }
    }
}

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.Events;

namespace AutoBattle
{
    public partial class GameUI : MonoBehaviour
    {
        // ==================== 技能面板 ====================

        /// <summary>
        /// 技能三选一面板：巅峰升级发技能时弹出，从候选里挑一个。
        /// 居中显示，不选完不消失（战斗在 State.SkillChoice 下暂停）。
        /// </summary>
        private void BuildSkillChoicePanel(Transform root)
        {
            int count = GameConfig.SkillChoiceCount;

            _skillChoicePanel = CreatePanel(root, "SkillChoicePanel", 250, 238, 780, 244,
                GameConfig.OverlayBg, true);

            _skillChoiceTitle = CreateText(_skillChoicePanel, "SkillChoiceTitle", "", 24, 12, 732, 30, 20,
                GameConfig.AccentBannerTitle, TextAnchor.MiddleCenter, FontStyle.Bold);

            CreateText(_skillChoicePanel, "SkillChoiceHint", "从下面选一个技能（选完自动装备并继续战斗）",
                24, 44, 732, 22, 14, GameConfig.TextMuted, TextAnchor.MiddleCenter);

            _skillChoiceButtons = new Button[count];
            _skillChoiceLabels = new Text[count];
            _choiceAccents = new Image[count];
            _skillChoiceIcons = new Image[count];

            const float cardW = 236f;
            const float cardH = 150f;
            float gap = count > 1 ? (732f - cardW * count) / (count - 1) : 0f;

            for (int i = 0; i < count; i++)
            {
                int captured = i;
                float x = 24f + i * (cardW + gap);

                _skillChoiceButtons[i] = CreateButton(_skillChoicePanel, $"SkillChoice{i}", "", x, 72f, cardW, cardH,
                    GameConfig.RowBg, out _skillChoiceLabels[i]);

                // 左上角技能图标：54px 圆形徽章，文字从图标右侧开始。
                _skillChoiceIcons[i] = CreateIconSlot(_skillChoiceButtons[i].transform, $"ChoiceIcon{i}", 14f, 10f, 54f);

                // 三行文案只占卡片上方一半，UpperLeft 会留一大片空底；垂直居中读起来才对得上「卡片」。
                _skillChoiceLabels[i].fontSize = 15;
                _skillChoiceLabels[i].alignment = TextAnchor.MiddleLeft;
                _skillChoiceLabels[i].rectTransform.offsetMin = new Vector2(80f, 10f);
                _skillChoiceLabels[i].rectTransform.offsetMax = new Vector2(-14f, -10f);

                _choiceAccents[i] = CreateImage(_skillChoiceButtons[i].transform, $"ChoiceAccent{i}",
                    0f, 10f, 3f, cardH - 20f, GameConfig.Border);

                _skillChoiceButtons[i].onClick.AddListener(() => OnSkillChoiceClicked(captured));
            }

            _skillChoicePanel.gameObject.SetActive(false);
        }

        private void OnSkillChoiceClicked(int index) => _game.ChooseSkill(index);

        /// <summary>刷新技能三选一面板：候选项、标题里的待选次数。</summary>
        private void RefreshSkillChoice()
        {
            if (_skillChoicePanel == null) return;

            bool open = _game.IsChoosingSkill;
            _skillChoicePanel.gameObject.SetActive(open);
            if (!open) return;

            IReadOnlyList<SkillId> choices = _game.SkillChoices;
            int pending = _game.PendingSkillPicks;

            // 推荐分只在候选人之间排序，不影响三选一本身；关掉后完全退回原来的卡片。
            bool advice = GameSettings.ShowSkillAdvice;
            int best = advice ? SkillAdvice.BestIndex(_game.Player, choices) : -1;

            _skillChoiceTitle.text = pending > 1
                ? $"✧ 巅峰奖励 — 选择技能（还有 {pending} 次待选）"
                : "✧ 巅峰奖励 — 选择技能";

            for (int i = 0; i < _skillChoiceButtons.Length; i++)
            {
                bool valid = i < choices.Count;
                _skillChoiceButtons[i].gameObject.SetActive(valid);
                if (!valid) continue;

                SkillDef def = SkillCatalog.Get(choices[i]);
                SetIcon(_skillChoiceIcons[i], SkillIconKey((int)def.id));
                int score = advice ? SkillAdvice.Score(_game.Player, choices[i]) : 0;
                string kindLine = advice
                    ? $"{KindShort((int)def.kind)} · <color=#{ColorUtility.ToHtmlStringRGB(SkillAdvice.TierColor(score))}>推荐 {score} {SkillAdvice.TierLabel(score)}</color>"
                    : KindShort((int)def.kind);
                string star = advice && i == best ? "  <color=#FFD966>★</color>" : "";

                _skillChoiceLabels[i].text =
                    $"<size=17><b>{def.name}</b></size>{star}\n<size=12><color=#8C99B0>{kindLine}</color></size>\n\n"
                    + $"<size=13><color=#C6CEE0>{def.description}</color></size>";
                _skillChoiceLabels[i].color = def.color;
                // 和技能栏同一套语言：底色只染 6%，左侧 3px 条才是类别色。
                // 推荐分最高的一张把强调条换成金色，扫一眼就知道「这一张最贴当前面板」。
                _skillChoiceButtons[i].image.color = Color.Lerp(GameConfig.RowBg, def.color, 0.06f);
                _choiceAccents[i].color = advice && i == best ? GameConfig.LogGold : def.color;
            }

            // 技能池快抽干时候选会少于三个，卡片都留在原来 1/3 的位置上会挤在左边；
            // 按实际可见数量重新居中，一张候选时也停在正中间。
            int shown = 0;
            for (int i = 0; i < _skillChoiceButtons.Length; i++)
                if (_skillChoiceButtons[i].gameObject.activeSelf) shown++;

            if (shown > 0 && shown < _skillChoiceButtons.Length)
            {
                const float cardW = 236f;
                const float stripW = 732f;
                float gap = shown > 1 ? Mathf.Min(24f, (stripW - cardW * shown) / (shown - 1)) : 0f;
                float totalW = cardW * shown + gap * (shown - 1);
                float startX = 24f + (stripW - totalW) * 0.5f;
                int slot = 0;
                for (int i = 0; i < _skillChoiceButtons.Length; i++)
                {
                    if (!_skillChoiceButtons[i].gameObject.activeSelf) continue;
                    var rt = (RectTransform)_skillChoiceButtons[i].transform;
                    rt.anchoredPosition = new Vector2(startX + slot * (cardW + gap), -72f);
                    slot++;
                }
            }
        }
        /// <summary>技能面板：点击技能栏槽位后弹出，列出整个技能池供装配。</summary>
        private void BuildSkillPanel(Transform root)
        {
            _skillPanel = CreatePanel(root, "SkillPanel", 236, 96, 808, 544, GameConfig.InsetBg, true);

            // 标题行只留 768 宽给文字，「关闭」缩到右上角同一行：15 张卡是 3 列 5 行，
            // 关闭按钮若留在底部（y=458）会把第 5 行挤到 66 高，推荐分行后描述必被截断。
            _skillPanelTitle = CreateText(_skillPanel, "SkillPanelTitle", "", 20, 12, 670, 30, 19,
                GameConfig.TextTitle, TextAnchor.MiddleLeft, FontStyle.Bold);

            CreateText(_skillPanel, "SkillPanelHint",
                "点击技能装到当前槽位（类别不符会自动放进同类空槽）；点击已装备的技能可卸下；推荐分越高越合适",
                20, 44, 768, 24, 13, GameConfig.TextMuted, TextAnchor.MiddleLeft);

            const int columns = 3;
            int total = SkillCatalog.All.Length;
            _skillPoolButtons = new Button[total];
            _skillPoolLabels = new Text[total];
            _skillPoolIcons = new Image[total];

            for (int i = 0; i < total; i++)
            {
                int captured = i;
                int col = i % columns;
                int row = i / columns;
                float x = 20f + col * 258f;
                // 3 列 5 行，关闭按钮移到标题行后底部不再被占，卡高从 66 增到 88、
                // 面板高 544：名称 + 推荐分 + 三行描述都放得下（此前最长的一条被 Truncate 截断）。
                float y = 76f + row * 90f;

                _skillPoolButtons[i] = CreateButton(_skillPanel, $"SkillPool{i}", "", x, y, 248, 88,
                    GameConfig.RowBg, out _skillPoolLabels[i]);

                _skillPoolIcons[i] = CreateIconSlot(_skillPoolButtons[i].transform, $"PoolIcon{i}", 8f, 8f, 40f);

                _skillPoolLabels[i].fontSize = 13;
                _skillPoolLabels[i].verticalOverflow = VerticalWrapMode.Truncate;
                _skillPoolLabels[i].alignment = TextAnchor.MiddleLeft;
                _skillPoolLabels[i].rectTransform.offsetMin = new Vector2(56f, 6f);
                _skillPoolLabels[i].rectTransform.offsetMax = new Vector2(-8f, -6f);

                _skillPoolButtons[i].onClick.AddListener(() => OnSkillPoolClicked(captured));
            }

            Button close = CreateButton(_skillPanel, "SkillPanelClose", "关闭", 698, 12, 90, 30,
                GameConfig.CardDanger, out Text skillCloseLabel);
            StyleBar(close, skillCloseLabel, "frame_pill", "glyph_close", 14f, "关闭技能面板");
            close.onClick.AddListener(OnSkillPanelCloseClicked);

            _skillPanel.gameObject.SetActive(false);
        }

        private void OnBuyClicked() => _game.BuyPotion();

        private void OnAutoBuyClicked() => _game.ToggleAutoBuyPotion();

        /// <summary>刷新商店区：金币、价格、持有数、按钮可用态与提示文本。</summary>
        private void RefreshShop(PlayerData p, bool textTick)
        {
            int price = _game.PotionPrice;
            bool open = _game.IsShopOpen;
            bool canBuy = open && p.gold >= price && p.potionCount < GameConfig.PotionMaxCount;

            // 金币每杀一只就变，所以这一区不能整段跳过；但真正要重画的只有
            // 金币数 / 持有数 / 按钮态，且只在它们变化时才重画。
            var sig = new Sig();
            sig.Add(price);
            sig.Add(p.gold);
            sig.Add(p.potionCount);
            sig.Add(open);
            sig.Add(canBuy);
            sig.Add(_game.AutoBuyPotion);
            if (sig.Value != _sigShopState)
            {
                _sigShopState = sig.Value;
                StaticRefreshCount++;

                _shopGoldText.text = $"金币 {p.gold:N0}";
                _shopPotionText.text = $"生命药剂    价格 {price:N0}    持有 {p.potionCount}/{GameConfig.PotionMaxCount}";

                // 底框是生图资源，不染成绿色；只做亮/暗区分。
                _buyButton.image.color = canBuy ? Color.white : new Color(0.55f, 0.58f, 0.65f, 1f);
                if (_buyIcon != null) _buyIcon.color = canBuy ? Color.white : new Color(1f, 1f, 1f, 0.45f);
                // 灰按钮上写「购买」会让人以为是没做好：把按不动的原因直接写在按钮上。
                _buyButtonLabel.text = canBuy
                    ? "购买"
                    : (!open ? "未开放"
                        : (p.potionCount >= GameConfig.PotionMaxCount ? "已满" : "金币不足"));
                _buyButtonLabel.color = canBuy ? Color.white : GameConfig.BtnDisabledText;

                _autoBuyLabel.text = _game.AutoBuyPotion ? "自动购买 开" : "自动购买 关";
                _autoBuyLabel.color = _game.AutoBuyPotion ? GameConfig.LogHeal : GameConfig.TextMuted;
                if (_autoBuyButton != null)
                    _autoBuyButton.image.color = _game.AutoBuyPotion
                        ? Color.white
                        : new Color(0.70f, 0.73f, 0.80f, 1f);
                if (_autoBuyIcon != null)
                    _autoBuyIcon.color = _game.AutoBuyPotion ? Color.white : new Color(1f, 1f, 1f, 0.55f);
            }

            if (!textTick) return;

            // 开放时显示倒计时（每 0.1 秒变一次）；关闭时显示提示（没有提示就说明当前不开放）。
            Color tipColor;
            string tip;
            if (open)
            {
                tipColor = GameConfig.LogGold;
                tip = $"商店开放中（剩 {_game.ShopTimeLeft:0.0} 秒）—— 精英/Boss 即将出现";
            }
            else
            {
                tipColor = GameConfig.LogWarn;
                tip = string.IsNullOrEmpty(_game.ShopMessage)
                    ? "商店只在精英/Boss 出现前开放"
                    : _game.ShopMessage;
            }

            if (_shopTipText.text == tip) return;
            _shopTipText.text = tip;
            _shopTipText.color = tipColor;
        }

    }
}

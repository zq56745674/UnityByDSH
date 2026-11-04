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

        /// <summary>技能面板：点击技能栏槽位后弹出，列出本流派 9 个技能供学习 / 装配。</summary>
        private void BuildSkillPanel(Transform root)
        {
            _skillPanel = CreatePanel(root, "SkillPanel", 236, 96, 808, 544, GameConfig.InsetBg, true);

            _skillPanelTitle = CreateText(_skillPanel, "SkillPanelTitle", "", 20, 12, 670, 30, 19,
                GameConfig.TextTitle, TextAnchor.MiddleLeft, FontStyle.Bold);

            CreateText(_skillPanel, "SkillPanelHint",
                "点击未掌握的技能花 1 技能点学习（空槽自动装备）；已掌握的点击装备 / 卸下",
                20, 44, 768, 22, 12, GameConfig.TextMuted, TextAnchor.MiddleLeft);

            // 流派条：把「你选的是哪一系」写在最显眼的位置，避免玩家在 9 张卡里分不清。
            _skillPanelSchoolBar = CreateImage(_skillPanel, "SkillPanelSchoolBar", 20, 70, 768, 28,
                GameConfig.RowBg);
            Round(_skillPanelSchoolBar, RadiusBar);
            _skillPanelSchoolText = CreateText(_skillPanel, "SkillPanelSchoolText", "", 30, 72, 748, 24, 14,
                GameConfig.TextTitle, TextAnchor.MiddleLeft, FontStyle.Bold);

            const int columns = 3;
            int total = SkillCatalog.TotalPerDoctrine;
            _skillPoolButtons = new Button[total];
            _skillPoolLabels = new Text[total];
            _skillPoolIcons = new Image[total];
            _skillPoolAccents = new Image[total];
            _skillPoolIds = new int[total];

            for (int i = 0; i < total; i++)
            {
                int captured = i;
                int col = i % columns;
                int row = i / columns;
                float x = 20f + col * 258f;
                // 3 列 3 行：第 2 行前空出一行写「通用技能」，所以第 3 行额外下移 24px。
                float y = 106f + row * 112f + (row >= 2 ? 24f : 0f);

                // 卡池里的每一张也是「压在面板上的纸片」：硬投影 → 纸底 → 烫金细边 → 上缘高光。
                // 之前只有一块 RowBg 平面色，锁定状态下整片糊成一条灰带，和背景分不开。
                Image poolShadow = CreateHardShadow(_skillPanel, $"SkillPoolShadow{i}", x, y, 248, 104, RadiusCard);
                _skillPoolButtons[i] = CreateButton(_skillPanel, $"SkillPool{i}", "", x, y, 248, 104,
                    GameConfig.RowBg, out _skillPoolLabels[i]);
                Image poolCard = _skillPoolButtons[i].GetComponent<Image>();
                if (poolCard != null)
                {
                    Round(poolCard, RadiusCard);
                    AddBorder(poolCard, RadiusCard, GameConfig.BorderSoft);
                    AddPaperSheen(poolCard);
                    Transform sheen = poolCard.transform.Find("Sheen");
                    if (sheen != null) sheen.SetSiblingIndex(0);
                }
                LinkShadow(_skillPoolButtons[i].gameObject, poolShadow);

                _skillPoolIcons[i] = CreateIconSlot(_skillPoolButtons[i].transform, $"PoolIcon{i}", 8f, 8f, 44f);

                _skillPoolLabels[i].fontSize = 12;
                _skillPoolLabels[i].verticalOverflow = VerticalWrapMode.Truncate;
                _skillPoolLabels[i].alignment = TextAnchor.MiddleLeft;
                _skillPoolLabels[i].rectTransform.offsetMin = new Vector2(58f, 4f);
                _skillPoolLabels[i].rectTransform.offsetMax = new Vector2(-8f, -4f);

                _skillPoolAccents[i] = CreateImage(_skillPoolButtons[i].transform, $"PoolAccent{i}",
                    0f, 8f, 3f, 88f, GameConfig.Border);

                _skillPoolButtons[i].onClick.AddListener(() => OnSkillPoolClicked(captured));
            }

            CreateText(_skillPanel, "SkillPanelGeneralLabel", "通用技能 · 全流派可用",
                20, 328, 768, 18, 12, GameConfig.TextMuted, TextAnchor.MiddleLeft, FontStyle.Bold);

            _skillPanelOtherText = CreateText(_skillPanel, "SkillPanelOtherText", "",
                20, 470, 768, 22, 12, GameConfig.TextMuted, TextAnchor.MiddleLeft);

            Button rune = CreateButton(_skillPanel, "SkillPanelRune", "符文", 596, 12, 90, 30,
                GameConfig.RowBg, out Text skillRuneLabel);
            StyleBar(rune, skillRuneLabel, "frame_pill", "glyph_star", 14f, "打开巅峰符文面板");
            rune.onClick.AddListener(OnSkillPanelRuneClicked);

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
                // 开=绿底：要用近白字（LogHeal 是深绿，压在 BtnPrimary 上只有 2.4 的对比度）；
            // 关=深皮革底，米白字同样比灰字清楚。
            _autoBuyLabel.color = _game.AutoBuyPotion ? new Color(0.93f, 0.98f, 0.90f) : new Color(0.90f, 0.86f, 0.78f);
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

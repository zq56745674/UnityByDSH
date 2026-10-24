using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.Events;

namespace AutoBattle
{
    public partial class GameUI : MonoBehaviour
    {
        // 装备强化面板
        // ==================== 装备强化面板（步骤 2） ====================

        /// <summary>
        /// 强化面板：顶部五个槽位标签，下方一张卡显示当前选中槽位的强化预览与费用。
        /// 强化是低频操作，做成浮层而不是常驻侧栏；面板打开时整局冻结（见 GameController.Update）。
        /// </summary>
        private void BuildEnhancePanel(Transform root)
        {
            _enhancePanel = CreatePanel(root, "EnhancePanel", 300, 160, 680, 380, GameConfig.OverlayBg, true);

            _enhancePanelTitle = CreateText(_enhancePanel, "EnhancePanelTitle", "", 20, 12, 640, 30, 19,
                GameConfig.TextTitle, TextAnchor.MiddleLeft, FontStyle.Bold);

            CreateText(_enhancePanel, "EnhancePanelHint",
                "强化只提升主属性，不影响暴击、暴击伤害与副词条。点顶部标签切换槽位。",
                20, 42, 640, 22, 13, GameConfig.TextInfo, TextAnchor.MiddleLeft);

            _enhanceSlotButtons = new Button[EquipmentSlots.Count];
            _enhanceSlotLabels = new Text[EquipmentSlots.Count];
            for (int i = 0; i < EquipmentSlots.Count; i++)
            {
                int captured = i;
                _enhanceSlotButtons[i] = CreateButton(_enhancePanel, "EnhanceTab" + i,
                    EquipmentSlots.Name(EquipmentSlots.All[i]), 20 + i * 116, 72, 110, 30,
                    GameConfig.BtnHeader, out _enhanceSlotLabels[i]);
                StyleTab(_enhanceSlotButtons[i], _enhanceSlotLabels[i], "frame_tab", 12);
                _enhanceSlotButtons[i].onClick.AddListener(() => OnEnhanceTabClicked(captured));
            }

            CreatePanel(_enhancePanel, "EnhanceCardBox", 20, 112, 640, 196, GameConfig.CardBg);
            _enhanceCardLabel = CreateText(_enhancePanel, "EnhanceCard", "", 34, 118, 600, 184, 13,
                GameConfig.TextNormal, TextAnchor.UpperLeft);
            _enhanceCardLabel.lineSpacing = 1.15f;

            _enhanceActionButton = CreateButton(_enhancePanel, "EnhanceAction", "强化", 20, 320, 150, 36,
                GameConfig.BtnGold, out _enhanceActionLabel);
            _enhanceActionIcon = StyleBar(_enhanceActionButton, _enhanceActionLabel, "frame_bar_round", "glyph_anvil", 16f, "强化选中装备", 12);
            _enhanceActionButton.onClick.AddListener(() => OnEnhanceSlotClicked(EquipmentSlots.All[_enhanceSlotIndex]));

            // 批量强化：+5 与「一直升到满级」。金币在循环里逐级扣，买不起就停在当前级。
            _enhanceBulk5Button = CreateButton(_enhancePanel, "EnhanceBulk5", "+5", 178, 320, 104, 36,
                GameConfig.BtnNeutral, out _enhanceBulk5Label);
            StyleTab(_enhanceBulk5Button, _enhanceBulk5Label, "frame_bar_round", 13);
            _enhanceBulk5Button.onClick.AddListener(() => OnEnhanceBulkClicked(5));

            _enhanceBulkMaxButton = CreateButton(_enhancePanel, "EnhanceBulkMax", "满级", 290, 320, 104, 36,
                GameConfig.BtnNeutral, out _enhanceBulkMaxLabel);
            StyleTab(_enhanceBulkMaxButton, _enhanceBulkMaxLabel, "frame_bar_round", 13);
            _enhanceBulkMaxButton.onClick.AddListener(() => OnEnhanceBulkClicked(GameConfig.EnhanceMaxLevel));

            Button close = CreateButton(_enhancePanel, "EnhancePanelClose", "关闭", 586, 320, 74, 36,
                GameConfig.CardBtn, out Text enhanceCloseLabel);
            StyleBar(close, enhanceCloseLabel, "frame_pill", "glyph_close", 14f, "关闭强化");
            close.onClick.AddListener(() =>
            {
                _game.CloseEnhancePanel();
                GameAudio.PlayUiClose();
            });

            _enhancePanel.gameObject.SetActive(false);
        }

        /// <summary>刷新强化面板：标签高亮、卡片正文与按钮费用。</summary>
        private void RefreshEnhancePanel()
        {
            if (_enhancePanel == null || _game == null) return;

            bool open = _game.EnhancePanelOpen;
            _enhancePanel.gameObject.SetActive(open);
            if (!open)
            {
                _enhancePanelWasOpen = false;
                return;
            }

            bool justOpened = !_enhancePanelWasOpen;
            _enhancePanelWasOpen = true;
            if (!justOpened && !_enhancePanelDirty) return;
            _enhancePanelDirty = false;

            PlayerData p = _game.Player;
            _enhancePanelTitle.text = $"强化装备 —— 金币 {p.gold:N0}";
            _enhanceSlotIndex = Mathf.Clamp(_enhanceSlotIndex, 0, EquipmentSlots.Count - 1);

            for (int i = 0; i < EquipmentSlots.Count; i++)
            {
                EquipmentSlot s = EquipmentSlots.All[i];
                bool selected = i == _enhanceSlotIndex;
                bool unlocked = p.IsEquipmentSlotUnlocked(s);
                _enhanceSlotButtons[i].image.color = selected
                    ? Color.white
                    : new Color(0.60f, 0.64f, 0.72f, 1f);
                _enhanceSlotLabels[i].color = selected ? Color.white
                    : unlocked ? GameConfig.TextMuted : GameConfig.BtnDisabledText;
                _enhanceSlotLabels[i].text = EquipmentSlots.Name(s) + (unlocked ? string.Empty : " · 未解锁");
            }

            EquipmentSlot slot = EquipmentSlots.All[_enhanceSlotIndex];
            Equipment e = p.GetEquipment(slot);
            _enhanceCardLabel.text = DescribeEnhanceCard(e, slot, p);

            bool canPay = e != null && e.CanEnhance && p.gold >= e.EnhanceCost;
            _enhanceActionLabel.text = e == null ? "无装备"
                : !e.CanEnhance ? "已满级"
                : $"强化 {e.EnhanceCost:N0}";

            _enhanceActionButton.image.color = canPay ? Color.white : new Color(0.62f, 0.66f, 0.74f, 1f);
            if (_enhanceActionIcon != null)
                _enhanceActionIcon.color = canPay ? Color.white : new Color(1f, 1f, 1f, 0.45f);
            _enhanceActionLabel.color = canPay ? Color.white : GameConfig.BtnDisabledText;

            // 批量按钮：+5 在剩余不足 5 级时改成 +N，避免按了却只升 2 级还不说清楚。
            int remaining = e != null && e.CanEnhance ? GameConfig.EnhanceMaxLevel - e.enhanceLevel : 0;
            int bulk5 = Mathf.Min(5, remaining);
            int bulk5Cost = remaining > 0 ? EnhanceBulkCost(e, bulk5) : 0;
            int maxCost = remaining > 0 ? EnhanceBulkCost(e, remaining) : 0;

            _enhanceBulk5Label.text = remaining > 0 ? $"+{bulk5}" : "+5";
            _enhanceBulkMaxLabel.text = "满级";

            bool canBulk = remaining > 0 && canPay;
            _enhanceBulk5Button.interactable = remaining > 0;
            _enhanceBulkMaxButton.interactable = remaining > 0;
            _enhanceBulk5Button.image.color = canBulk ? Color.white : new Color(0.62f, 0.66f, 0.74f, 1f);
            _enhanceBulkMaxButton.image.color = canBulk ? Color.white : new Color(0.62f, 0.66f, 0.74f, 1f);
            _enhanceBulk5Label.color = canBulk ? Color.white : GameConfig.BtnDisabledText;
            _enhanceBulkMaxLabel.color = canBulk ? Color.white : GameConfig.BtnDisabledText;

            SetTooltip(_enhanceBulk5Button, remaining > 0
                ? $"连升最多 {bulk5} 级，共需 {bulk5Cost:N0} 金币；金币不足会自动停在买得起的那一级"
                : "已经强化到上限");
            SetTooltip(_enhanceBulkMaxButton, remaining > 0
                ? $"一直强化到 +{GameConfig.EnhanceMaxLevel}，共需 {maxCost:N0} 金币；金币不足会自动停在买得起的那一级"
                : "已经强化到上限");
        }

        /// <summary>从当前等级连升 levels 级的总价；和控制器逐级扣费用的是同一张表，不会算错。</summary>
        private static int EnhanceBulkCost(Equipment e, int levels)
        {
            int total = 0;
            for (int i = 0; i < levels; i++)
            {
                int level = e.enhanceLevel + i;
                if (level >= GameConfig.EnhanceMaxLevel) break;
                total += GameConfig.EnhanceCost(level, e.quality);
            }
            return total;
        }

        /// <summary>一张强化卡片里的正文：当前、下一级、费用拆解与上限，填满整张卡。</summary>
        private static string DescribeEnhanceCard(Equipment e, EquipmentSlot slot, PlayerData p)
        {
            string slotName = EquipmentSlots.Name(slot);
            if (e == null)
                return $"{slotName}\n\n<color=#8C99B0>（空槽位 — 掉落会自动穿上，穿上后才能强化）</color>";

            int best = p.stats != null ? p.stats.bestEnhanceLevel : 0;
            float bonusPct = GameConfig.EnhanceBonusPerLevel * 100f;

            if (!e.CanEnhance)
            {
                return $"{e.Title}\n" +
                       $"当前   {e.EnhanceGainText}\n" +
                       $"已强化到上限 +{GameConfig.EnhanceMaxLevel}（每级 +{bonusPct:0.#}% 乘算，满级 ×{e.EnhanceMultiplier:0.00}）\n" +
                       $"成功率 100%，不会失败也不会掉级\n" +
                       $"本档最强强化 +{best}";
            }

            int cost = e.EnhanceCost;
            int next = e.enhanceLevel + 1;
            float factor = GameConfig.EnhanceQualityFactors[(int)GameConfig.ClampQuality(e.quality)];
            string goldTag = p.gold >= cost ? "" : "  <color=#FF8A80>（金币不足）</color>";
            return $"{e.Title}\n" +
                   $"当前   {e.EnhanceGainText}\n" +
                   $"下一级 {e.EnhanceGainTextAt(next)}\n" +
                   $"费用   {cost:N0} 金币 = ({GameConfig.EnhanceBaseCost} + {e.enhanceLevel}×{GameConfig.EnhanceCostPerLevel}) × 品质 {factor:0.0}{goldTag}\n" +
                   $"上限 +{GameConfig.EnhanceMaxLevel}  成功率 100%  本档最强强化 +{best}";
        }

        private void OnEnhanceEntryClicked()
        {
            _enhancePanelDirty = true;
            bool open = _game.EnhancePanelOpen;
            _game.ToggleEnhancePanel();
            if (open) GameAudio.PlayUiClose();
            else GameAudio.PlayUiOpen();
        }

        private void OnEnhanceTabClicked(int index)
        {
            _enhanceSlotIndex = Mathf.Clamp(index, 0, EquipmentSlots.Count - 1);
            _enhancePanelDirty = true;
        }

        private void OnEnhanceSlotClicked(EquipmentSlot slot)
        {
            EnhanceResult result = _game.EnhanceEquipment(slot);
            _enhancePanelDirty = true;

            if (result == EnhanceResult.NotEnoughGold)
                _game.AddLog("金币不足，无法强化。", GameConfig.LogWarn);
            else if (result == EnhanceResult.NoEquipment)
                _game.AddLog("该槽位还没有装备。", GameConfig.LogWarn);
        }

        /// <summary>批量强化：+5 或一直升到满级；金币不足时控制器会停在当前级，这里只补一句差额提示。</summary>
        private void OnEnhanceBulkClicked(int levels)
        {
            EquipmentSlot slot = EquipmentSlots.All[_enhanceSlotIndex];
            Equipment before = _game.Player.GetEquipment(slot);
            int startLevel = before != null ? before.enhanceLevel : 0;

            int gained = _game.EnhanceEquipmentBulk(slot, levels, out EnhanceResult stop);
            _enhancePanelDirty = true;

            if (gained <= 0)
            {
                if (stop == EnhanceResult.NoEquipment)
                    _game.AddLog("该槽位还没有装备。", GameConfig.LogWarn);
                else if (stop == EnhanceResult.MaxLevel)
                    _game.AddLog("已经强化到上限，不用再强化了。", GameConfig.LogWarn);
                else if (stop == EnhanceResult.NotEnoughGold && before != null)
                    _game.AddLog($"金币不足：强化到 +{startLevel + 1} 需要 {before.EnhanceCost:N0} 金币，现有 {_game.Player.gold:N0}。",
                        GameConfig.LogWarn);
                return;
            }

            if (stop == EnhanceResult.NotEnoughGold)
            {
                Equipment after = _game.Player.GetEquipment(slot);
                int shortage = after != null ? Mathf.Max(0, after.EnhanceCost - _game.Player.gold) : 0;
                _game.AddLog($"金币不足，本次只强化到 +{(after != null ? after.enhanceLevel : startLevel + gained)}；再升一级还差 {shortage:N0} 金币。",
                    GameConfig.LogWarn);
            }
        }

        /// <summary>
        /// 洗练面板：顶部五个槽位标签，下方一张卡显示当前选中槽位的词条、质量与费用。
        /// 每条副词条都是一行按钮，点一下锁定/解锁；锁住的词条不参与重抽。
        /// 面板打开时整局冻结（见 GameController.Update）。
        /// </summary>
        private void BuildReforgePanel(Transform root)
        {
            _reforgePanel = CreatePanel(root, "ReforgePanel", 300, 142, 680, 436, GameConfig.OverlayBg, true);

            _reforgePanelTitle = CreateText(_reforgePanel, "ReforgePanelTitle", "", 20, 12, 640, 30, 19,
                GameConfig.TextTitle, TextAnchor.MiddleLeft, FontStyle.Bold);

            CreateText(_reforgePanel, "ReforgePanelHint",
                "点词条行锁定/解锁：锁住的不重抽，每多锁一条费用 ×3；至少要留一条不锁。",
                20, 44, 640, 36, 13, GameConfig.TextInfo, TextAnchor.MiddleLeft);

            int affixKinds = GameConfig.EquipAffixNames.Length;

            _reforgeSlotButtons = new Button[EquipmentSlots.Count];
            _reforgeSlotLabels = new Text[EquipmentSlots.Count];
            _reforgeAffixButtons = new Button[affixKinds];
            _reforgeAffixLabels = new Text[affixKinds];
            _reforgeAffixStrips = new Image[affixKinds];

            for (int i = 0; i < EquipmentSlots.Count; i++)
            {
                int captured = i;
                _reforgeSlotButtons[i] = CreateButton(_reforgePanel, "ReforgeTab" + i,
                    EquipmentSlots.Name(EquipmentSlots.All[i]), 20 + i * 116, 72, 110, 30,
                    GameConfig.BtnHeader, out _reforgeSlotLabels[i]);
                StyleTab(_reforgeSlotButtons[i], _reforgeSlotLabels[i], "frame_tab", 12);
                _reforgeSlotButtons[i].onClick.AddListener(() => OnReforgeTabClicked(captured));
            }

            CreatePanel(_reforgePanel, "ReforgeCardBox", 20, 112, 640, 262, GameConfig.CardBg);
            _reforgeCardLabel = CreateText(_reforgePanel, "ReforgeCard", "", 34, 118, 600, 44, 14,
                GameConfig.TextNormal, TextAnchor.UpperLeft, FontStyle.Bold);

            for (int j = 0; j < affixKinds; j++)
            {
                EquipmentAffix kind = (EquipmentAffix)j;
                float rowY = 168f + j * 26f;

                Button row = CreateButton(_reforgePanel, "ReforgeAffix" + j, "", 34, rowY, 372, 24,
                    GameConfig.RowBg, out Text rowLabel);
                rowLabel.fontSize = 13;
                rowLabel.fontStyle = FontStyle.Normal;
                rowLabel.alignment = TextAnchor.MiddleLeft;
                rowLabel.color = GameConfig.TextNormal;
                rowLabel.rectTransform.offsetMin = new Vector2(10f, 0f);
                rowLabel.rectTransform.offsetMax = new Vector2(-8f, 0f);
                row.onClick.AddListener(() => OnReforgeAffixClicked(kind));

                _reforgeAffixStrips[j] = CreateImage(row.transform, "Strip", 0, 0, 3, 24, GameConfig.Border);
                _reforgeAffixButtons[j] = row;
                _reforgeAffixLabels[j] = rowLabel;
            }

            // 右侧信息栏：把卡片右半边原来空着的区域用来解释容量、质量分档与锁定倍率。
            // 左列词条行因此收窄到 372（一行最长约「◆ 已锁  暴击伤害 +38.2%」，不会折行）。
            _reforgeInfoText = CreateText(_reforgePanel, "ReforgeInfo", "", 418, 168, 236, 204, 11,
                GameConfig.TextMuted, TextAnchor.UpperLeft);
            _reforgeInfoText.lineSpacing = 1.15f;

            _reforgeQualityLabel = CreateText(_reforgePanel, "ReforgeQuality", "",
                34, 302, 372, 20, 12, GameConfig.TextInfo, TextAnchor.MiddleLeft);
            AttachTooltip(_reforgeQualityLabel.rectTransform,
                "词条质量：每条副词条在其数值区间里的百分位平均，0~100%。\n" +
                "≥85% 极品，≥70% 优秀，≥50% 合格，<50% 偏低。\n" +
                "质量只用于比较好坏，不参与评分与战斗计算。");

            _reforgeCostLabel = CreateText(_reforgePanel, "ReforgeCost", "",
                34, 326, 372, 20, 12, GameConfig.TextMuted, TextAnchor.MiddleLeft);

            _reforgeActionButton = CreateButton(_reforgePanel, "ReforgeAction", "洗练", 34, 350, 200, 30,
                GameConfig.BtnGold, out _reforgeActionLabel);
            _reforgeActionIcon = StyleBar(_reforgeActionButton, _reforgeActionLabel, "frame_bar_round", "glyph_flame", 17f, "洗练选中装备", 13);
            _reforgeActionButton.onClick.AddListener(() => OnReforgeSlotClicked(EquipmentSlots.All[_reforgeSlotIndex]));

            Button close = CreateButton(_reforgePanel, "ReforgePanelClose", "关闭", 586, 392, 74, 34,
                GameConfig.CardBtn, out Text reforgeCloseLabel);
            StyleBar(close, reforgeCloseLabel, "frame_pill", "glyph_close", 14f, "关闭洗练");
            close.onClick.AddListener(() =>
            {
                _game.CloseReforgePanel();
                GameAudio.PlayUiClose();
            });

            _reforgePanel.gameObject.SetActive(false);
        }

    }
}

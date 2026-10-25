using UnityEngine;
using UnityEngine.UI;

namespace AutoBattle
{
    /// <summary>
    /// 装备对比面板（2026-10-18 新增）。
    /// 掉落评分高于当前装备、但当前装备被锁定（或设置里关了自动换装）时，掉落不再直接卖店，
    /// 而是放进 <see cref="GameController.PendingDrop"/> 等玩家比较后手动换装 / 卖出。
    /// 面板本身只读数据 + 三个动作（换装、卖出、锁定当前），所有状态都在控制器里。
    /// </summary>
    public partial class GameUI
    {
        private RectTransform _comparePanel;
        private readonly Button[] _compareSlotButtons = new Button[EquipmentSlots.Count];
        private readonly Text[] _compareSlotLabels = new Text[EquipmentSlots.Count];
        private Text _compareCurrentText;
        private Text _comparePendingText;
        private Text _compareDeltaText;
        private Text _compareHintText;
        private Button _compareEquipButton;
        private Button _compareSellButton;
        private Button _compareLockButton;
        private Text _compareLockLabel;
        private Text _compareEquipLabel;
        private Text _compareSellLabel;
        private Image _compareEquipIcon;
        private Image _compareSellIcon;
        private Image _compareLockIcon;
        private int _compareSlot;
        private bool _comparePanelWasOpen;
        private bool _compareDirty;
        private bool _compareBadgeOn;

        private void BuildComparePanel(Transform root)
        {
            _comparePanel = CreatePanel(root, "ComparePanel", 300, 140, 680, 440, GameConfig.OverlayBg, true);

            CreateSectionTitle(_comparePanel, "装备对比", 20, 12, 220, FontTitle, "ui_compare");
            CreateText(_comparePanel, "CompareHint", "锁定当前装备（或关闭自动换装）后，更高分的掉落会留在这里等你决定。",
                20, 40, 600, 24, FontSmall, GameConfig.TextMuted, TextAnchor.MiddleLeft);

            Button close = CreateButton(_comparePanel, "CompareClose", "关闭", 570, 14, 90, 30,
                GameConfig.BtnNeutral, out Text closeLabel);
            StyleBar(close, closeLabel, "frame_pill", "glyph_close", 14f, "关闭装备对比");
            close.onClick.AddListener(OnCompareCloseClicked);

            // 五个槽位横排：680 宽里 5×110 + 4×6 = 574，正好留出右侧空白。
            for (int i = 0; i < EquipmentSlots.Count; i++)
            {
                int captured = i;
                _compareSlotButtons[i] = CreateButton(_comparePanel, "CompareSlot" + i,
                    EquipmentSlots.Name(EquipmentSlots.All[i]), 20 + i * 116, 72, 110, 30,
                    GameConfig.BtnHeader, out _compareSlotLabels[i]);
                StyleTab(_compareSlotButtons[i], _compareSlotLabels[i], "frame_tab", 12);
                _compareSlotButtons[i].onClick.AddListener(() => OnCompareSlotClicked(captured));
            }

            CreateText(_comparePanel, "CompareCurrentTitle", "当前装备", 20, 112, 300, 22, FontBody,
                GameConfig.TextTitle, TextAnchor.MiddleLeft, FontStyle.Bold);
            _compareCurrentText = CreateText(_comparePanel, "CompareCurrent", "", 20, 136, 300, 176, FontSmall,
                GameConfig.TextNormal, TextAnchor.UpperLeft);
            _compareCurrentText.lineSpacing = 1.2f;

            CreateText(_comparePanel, "ComparePendingTitle", "待选掉落", 360, 112, 300, 22, FontBody,
                GameConfig.TextGold, TextAnchor.MiddleLeft, FontStyle.Bold);
            _comparePendingText = CreateText(_comparePanel, "ComparePending", "", 360, 136, 300, 176, FontSmall,
                GameConfig.TextNormal, TextAnchor.UpperLeft);
            _comparePendingText.lineSpacing = 1.2f;

            // 逐项差现在分两行：主属性（攻防血）+ 词条（暴击率/暴伤/攻速/全属/穿甲）。
            // 单行放 8 项在 640 宽里必折行，而折行位置还会随数值长度乱跳。
            _compareDeltaText = CreateText(_comparePanel, "CompareDelta", "", 20, 314, 640, 44, FontSmall,
                GameConfig.TextInfo, TextAnchor.UpperLeft);
            _compareDeltaText.lineSpacing = 1.1f;
            _compareHintText = CreateText(_comparePanel, "CompareFoot", "", 20, 360, 640, 32, FontTiny,
                GameConfig.TextMuted, TextAnchor.UpperLeft);

            _compareEquipButton = CreateButton(_comparePanel, "CompareEquip", "换装", 20, 396, 100, 34,
                GameConfig.BtnPrimary, out _compareEquipLabel);
            _compareEquipIcon = StyleBar(_compareEquipButton, _compareEquipLabel, "frame_ribbon", "glyph_equip", 18f, "换上待选装备");
            _compareEquipButton.onClick.AddListener(OnCompareEquipClicked);

            _compareSellButton = CreateButton(_comparePanel, "CompareSell", "卖出", 128, 396, 100, 34,
                GameConfig.BtnGold, out _compareSellLabel);
            _compareSellIcon = StyleBar(_compareSellButton, _compareSellLabel, "frame_pill", "glyph_sell", 18f, "卖出待选装备");
            _compareSellButton.onClick.AddListener(OnCompareSellClicked);

            _compareLockButton = CreateButton(_comparePanel, "CompareLock", "锁定当前", 236, 396, 120, 34,
                GameConfig.BtnNeutral, out _compareLockLabel);
            _compareLockIcon = StyleBar(_compareLockButton, _compareLockLabel, "frame_pill", "glyph_confirm", 18f, "锁定 / 解除锁定当前装备");
            _compareLockButton.onClick.AddListener(OnCompareLockClicked);

            _comparePanel.gameObject.SetActive(false);
        }

        private void RefreshComparePanel()
        {
            if (_comparePanel == null || _game == null) return;

            bool badge = _game.HasAnyPendingDrop;
            if (badge != _compareBadgeOn)
            {
                _compareBadgeOn = badge;
                // 入口已经是大图标按钮，不再改文字/底衬：有待选时把整枚图标染金。
                if (_compareButtonIcon != null)
                    _compareButtonIcon.color = badge ? GameConfig.LogGold : Color.white;
            }

            bool open = _game.CompareOpen;
            _comparePanel.gameObject.SetActive(open);
            if (!open)
            {
                _comparePanelWasOpen = false;
                return;
            }

            bool justOpened = !_comparePanelWasOpen;
            _comparePanelWasOpen = true;
            if (!justOpened && !_compareDirty) return;
            _compareDirty = false;

            PlayerData p = _game.Player;
            EquipmentSlot slot = (EquipmentSlot)Mathf.Clamp(_compareSlot, 0, EquipmentSlots.Count - 1);
            Equipment current = p.GetEquipment(slot);
            Equipment pending = _game.PendingDrop(slot);

            for (int i = 0; i < EquipmentSlots.Count; i++)
            {
                bool selected = i == _compareSlot;
                EquipmentSlot s = EquipmentSlots.All[i];
                bool unlocked = p.IsEquipmentSlotUnlocked(s);
                // 生图底框不染品牌色，只区分选中/未选中亮度。
                _compareSlotButtons[i].image.color = selected
                    ? Color.white
                    : new Color(0.62f, 0.66f, 0.74f, 1f);
                _compareSlotLabels[i].color = unlocked ? (selected ? Color.white : GameConfig.TextMuted) : GameConfig.BtnDisabledText;
                bool has = _game.HasPendingDrop(s);
                // 转生 v2 会保留一件装备，保留件可能落在尚未解锁的槽位里；
                // 这时标签不能写「未解锁」，否则和「装备仍生效」自相矛盾。
                string lockText = unlocked ? string.Empty
                    : p.GetEquipment(s) != null ? " · 保留件" : " · 未解锁";
                _compareSlotLabels[i].text = EquipmentSlots.Name(s) + lockText + (has ? " ●" : "");
            }

            if (current == null)
            {
                _compareCurrentText.text = "<color=#8C99B0>（空）</color>";
                _compareCurrentText.color = GameConfig.TextMuted;
            }
            else
            {
                _compareCurrentText.color = GameConfig.TextNormal;
                _compareCurrentText.text =
                    $"<size=14><b>{current.QualityName} {current.SlotName} Lv.{current.level}{current.EnhanceText}</b></size>\n"
                    + $"<color=#8C99B0>评分</color> <b>{EquipmentScore.Of(p, current, slot):0}</b>"
                    + (current.locked ? "   <color=#FFC864>🔒 已锁定</color>" : "   <color=#8C99B0>🔓 可替换</color>")
                    + "\n" + current.StatLine
                    + (current.AffixCount > 0 ? "\n" + current.AffixLine : "")
                    + (current.reforgeCount > 0 ? "\n<color=#8C99B0>" + current.ReforgeCountText + "</color>" : "");
            }

            if (pending == null)
            {
                _comparePendingText.color = GameConfig.TextMuted;
                _comparePendingText.text = "（暂无待选）\n\n"
                    + "<color=#8C99B0>待选不是背包：每个槽位只留最高分的一件，换装或卖出后立刻腾空；"
                    + "切换存档 / 重开 / 读档也会清空（待选不进存档）。</color>";
                _compareDeltaText.text = "";
                _compareHintText.text = "开启「自动换装」时，更高分掉落会直接穿上，不会进待选。";
                _compareEquipButton.interactable = false;
                _compareSellButton.interactable = false;
                _compareEquipLabel.color = GameConfig.BtnDisabledText;
                _compareSellLabel.color = GameConfig.BtnDisabledText;
            }
            else
            {
                float delta = EquipmentScore.Of(p, pending, slot)
                    - (current != null ? EquipmentScore.Of(p, current, slot) : 0f);

                _comparePendingText.color = GameConfig.TextNormal;
                string deltaText = delta >= 0f
                    ? $"<color=#6FE08A>（Δ +{delta:0.#}）</color>"
                    : $"<color=#E08787>（Δ {delta:0.#}）</color>";

                _comparePendingText.text =
                    $"<size=14><b>{pending.QualityName} {pending.SlotName} Lv.{pending.level}{pending.EnhanceText}</b></size>\n"
                    + $"<color=#8C99B0>评分</color> <b>{EquipmentScore.Of(p, pending, slot):0}</b> {deltaText}\n"
                    + pending.StatLine
                    + (pending.AffixCount > 0 ? "\n" + pending.AffixLine : "");

                // 逐项差放在两栏下方整行显示：塞进 300 宽的待选栏会折行。
                _compareDeltaText.text = DiffLine(current, pending);

                _compareHintText.text = delta >= 0f
                    ? "换装后当前装备直接替换（旧的那件不会退回金币）；卖出按等级与品质折价。"
                    : "<color=#E08787>注意：这件比当前装备差，换装会掉评分。</color>卖出按等级与品质折价。";
                _compareEquipButton.interactable = true;
                _compareSellButton.interactable = true;
                _compareEquipLabel.color = Color.white;
                _compareSellLabel.color = Color.white;
            }

            if (current == null)
            {
                _compareLockButton.interactable = false;
                _compareLockLabel.color = GameConfig.BtnDisabledText;
                _compareLockLabel.text = "锁定当前";
                if (_compareLockIcon != null) _compareLockIcon.color = new Color(1f, 1f, 1f, 0.40f);
            }
            else
            {
                _compareLockButton.interactable = true;
                _compareLockLabel.color = Color.white;
                _compareLockLabel.text = current.locked ? "解除锁定" : "锁定当前";
                SetIcon(_compareLockIcon, current.locked ? "glyph_unlock" : "glyph_confirm");
            }
        }

        private static string DiffLine(Equipment current, Equipment candidate)
        {
            if (candidate == null) return "";
            if (current == null)
                return "<color=#8C99B0>当前槽位为空，换上就是全增量。</color>";

            // 主属性行只放强化后的大数值；词条行把主词条与副词条合并成有效值。
            // 只比 critChance 会漏掉副词条里的暴击率（同一件装备两条都可能出现），
            // 而攻速 / 全属性 / 穿甲只存在于副词条，以前整行都看不到。
            string main = "<color=#8C99B0>主属性：</color>"
                + Signed("攻", candidate.EnhancedAttack - current.EnhancedAttack, "0")
                + Signed("防", candidate.EnhancedDefense - current.EnhancedDefense, "0")
                + Signed("血", candidate.EnhancedHp - current.EnhancedHp, "0")
                + Signed("速", candidate.EnhancedSpeed - current.EnhancedSpeed, "0.#")
                + Signed("攻速", (candidate.EnhancedHaste - current.EnhancedHaste) * 100f, "0.#", "%")
                + Signed("穿甲", (candidate.EnhancedArmorPen - current.EnhancedArmorPen) * 100f, "0.#", "%");

            float critChance = (candidate.critChance + candidate.affixCritChance)
                - (current.critChance + current.affixCritChance);
            // 饰品的暴伤主词条吃强化，武器的暴伤主词条不吃——按槽位取对应的读法，
            // 否则换上满强化饰品时词条行会少报一截。
            float candidateCritDamage = (candidate.slot == EquipmentSlot.Accessory
                ? candidate.EnhancedCritDamage : candidate.critDamage) + candidate.affixCritDamage;
            float currentCritDamage = (current.slot == EquipmentSlot.Accessory
                ? current.EnhancedCritDamage : current.critDamage) + current.affixCritDamage;
            float critDamage = candidateCritDamage - currentCritDamage;
            float speed = candidate.affixAttackSpeed - current.affixAttackSpeed;
            float allStats = candidate.affixAllStats - current.affixAllStats;
            float armorPen = candidate.affixArmorPen - current.affixArmorPen;

            string affix = "<color=#8C99B0>词条：</color>"
                + Signed("暴击率", critChance * 100f, "0.#", "%")
                + Signed("暴伤", critDamage * 100f, "0.#", "%")
                + Signed("攻速", speed * 100f, "0.#", "%")
                + Signed("全属", allStats * 100f, "0.#", "%")
                + Signed("穿甲", armorPen * 100f, "0.#", "%");

            return main + "\n" + affix;
        }

        private static string Signed(string label, float value, string format, string suffix = "")
        {
            if (Mathf.Abs(value) < 0.05f) return $"  <color=#8C99B0>{label} ±0{suffix}</color>";
            string text = value > 0f ? $"+{value.ToString(format)}{suffix}" : $"{value.ToString(format)}{suffix}";
            string color = value > 0f ? "#6FE08A" : "#E08787";
            return $"  <color={color}>{label} {text}</color>";
        }

        private void OnCompareEntryClicked()
        {
            _compareSlot = 0;
            for (int i = 0; i < EquipmentSlots.Count; i++)
            {
                if (_game.HasPendingDrop(EquipmentSlots.All[i])) { _compareSlot = i; break; }
            }
            _compareDirty = true;
            bool open = _game.CompareOpen;
            _game.ToggleComparePanel();
            if (open) GameAudio.PlayUiClose();
            else GameAudio.PlayUiOpen();
        }

        /// <summary>点左栏装备卡：直接打开对比面板并切到该槽位。</summary>
        private void OnEquipCardClicked(int index)
        {
            _compareSlot = Mathf.Clamp(index, 0, EquipmentSlots.Count - 1);
            _compareDirty = true;
            if (!_game.CompareOpen)
            {
                _game.ToggleComparePanel();
                GameAudio.PlayUiOpen();
            }
        }

        private void OnCompareCloseClicked()
        {
            _compareDirty = true;
            _game.CloseComparePanel();
            GameAudio.PlayUiClose();
        }

        private void OnCompareSlotClicked(int side)
        {
            _compareSlot = Mathf.Clamp(side, 0, EquipmentSlots.Count - 1);
            _compareDirty = true;
        }

        private void OnCompareEquipClicked()
        {
            _game.EquipPending((EquipmentSlot)_compareSlot);
            _compareDirty = true;
        }

        private void OnCompareSellClicked()
        {
            _game.SellPending((EquipmentSlot)_compareSlot);
            _compareDirty = true;
        }

        private void OnCompareLockClicked()
        {
            _game.ToggleEquipLock((EquipmentSlot)_compareSlot);
            _compareDirty = true;
        }
    }
}
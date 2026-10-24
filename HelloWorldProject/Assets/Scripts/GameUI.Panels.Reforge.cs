using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.Events;

namespace AutoBattle
{
    public partial class GameUI : MonoBehaviour
    {
        // 装备洗练面板（含词条锁定）
        /// <summary>
        /// 刷新洗练面板。与强化面板同构：关着直接跳过、只在不脏且非刚打开时省掉重算。
        /// 洗练质量与费用都是纯计算，真正贵的只有字符串，所以判脏就够了，不需要分帧。
        /// </summary>
        private void RefreshReforgePanel()
        {
            if (_reforgePanel == null || _game == null) return;

            bool open = _game.ReforgePanelOpen;
            _reforgePanel.gameObject.SetActive(open);
            if (!open)
            {
                _reforgePanelWasOpen = false;
                return;
            }

            bool justOpened = !_reforgePanelWasOpen;
            _reforgePanelWasOpen = true;
            if (!justOpened && !_reforgePanelDirty) return;
            _reforgePanelDirty = false;

            PlayerData p = _game.Player;
            _reforgePanelTitle.text = $"洗练装备 —— 金币 {p.gold:N0}";
            _reforgeSlotIndex = Mathf.Clamp(_reforgeSlotIndex, 0, EquipmentSlots.Count - 1);

            for (int i = 0; i < EquipmentSlots.Count; i++)
            {
                EquipmentSlot s = EquipmentSlots.All[i];
                bool selected = i == _reforgeSlotIndex;
                bool unlocked = p.IsEquipmentSlotUnlocked(s);
                _reforgeSlotButtons[i].image.color = selected
                    ? Color.white
                    : new Color(0.60f, 0.64f, 0.72f, 1f);
                _reforgeSlotLabels[i].color = selected ? Color.white
                    : unlocked ? GameConfig.TextMuted : GameConfig.BtnDisabledText;
                _reforgeSlotLabels[i].text = EquipmentSlots.Name(s) + (unlocked ? string.Empty : " · 未解锁");
            }

            int side = _reforgeSlotIndex;
            EquipmentSlot slot = EquipmentSlots.All[side];
            Equipment e = p.GetEquipment(slot);

            // 换了一件装备（或卸载）就把锁定清掉：掩码指的是上一件的词条种类，
            // 新装备可能根本没有那一条，留着会让费用按不存在的词条算钱。
            if (!ReferenceEquals(e, _reforgeLockOwners[side]))
            {
                _reforgeLockOwners[side] = e;
                _reforgeLockMasks[side] = 0;
            }

            int mask = e != null ? _reforgeLockMasks[side] & e.PresentAffixMask : 0;
            _reforgeLockMasks[side] = mask;
            int locked = e != null ? e.LockedAffixCount(mask) : 0;
            int free = e != null ? e.AffixCount - locked : 0;
            int cost = e != null && free > 0 ? e.ReforgeCostWithLocks(locked) : 0;

            if (e != null)
            {
                _reforgeCardLabel.text = $"{e.Title}\n<size=13><color=#C6CEE0>{e.StatLine}</color></size>";
            }
            else
            {
                _reforgeCardLabel.text = $"{EquipmentSlots.Name(slot)}\n<size=13><color=#8C99B0>（空）</color></size>";
            }

            // 小件只有 2 条词条，按枚举序号固定排会留下空行；这里把显示出来的行紧凑重排。
            int visibleRow = 0;
            for (int j = 0; j < GameConfig.EquipAffixNames.Length; j++)
            {
                EquipmentAffix kind = (EquipmentAffix)j;
                int bit = Equipment.AffixBit(kind);
                bool present = e != null && (e.PresentAffixMask & bit) != 0;
                _reforgeAffixButtons[j].gameObject.SetActive(present);
                if (!present) continue;

                _reforgeAffixButtons[j].GetComponent<RectTransform>().anchoredPosition =
                    new Vector2(34f, -(168f + visibleRow * 26f));
                visibleRow++;

                bool isLocked = (mask & bit) != 0;
                _reforgeAffixLabels[j].text =
                    (isLocked ? "<color=#FFC857>◆ 已锁</color>  " : "<color=#8C99B0>◇ 未锁</color>  ") +
                    Equipment.AffixText(kind, e.AffixValue(kind));

                var rowBg = _reforgeAffixButtons[j].targetGraphic as Image;
                if (rowBg != null)
                {
                    rowBg.color = isLocked
                        ? Color.Lerp(GameConfig.RowBg, GameConfig.AccentBanner, 0.18f)
                        : GameConfig.RowBg;
                }
                _reforgeAffixStrips[j].color = isLocked ? GameConfig.AccentBanner : GameConfig.Border;
            }

            // 空槽位没有质量/费用/按钮可言，藏起来免得显示「费用 0 金币」。
            _reforgeQualityLabel.gameObject.SetActive(e != null);
            _reforgeCostLabel.gameObject.SetActive(e != null);
            _reforgeActionButton.gameObject.SetActive(e != null);

            if (e == null)
            {
                _reforgeInfoText.text =
                    $"<color=#C6CEE0>{EquipmentSlots.Name(slot)}</color>\n\n" +
                    "空槽位，没有可洗练的词条。\n" +
                    "装备掉落并自动穿上后，这里会显示词条容量、质量分档与锁定费用。";
                return;
            }

            int quality = Mathf.RoundToInt(e.AffixQuality * 100f);
            int capacity = GameConfig.EquipAffixCountMax[(int)GameConfig.ClampQuality(e.quality)];
            string tier = AffixQualityTier(quality);

            // 右侧信息栏：容量、质量分档、锁定倍率。宽 236、11 号字，行数控制在 ~12 行内不裁切。
            _reforgeInfoText.text =
                $"<color=#C6CEE0>词条容量</color>  {e.AffixCount} / {capacity} 条\n" +
                $"洗练只重抽种类与数值，条数不变\n\n" +
                $"<color=#C6CEE0>当前质量 {quality}% · {tier}</color>\n" +
                "≥85 极品 · ≥70 优秀\n" +
                "≥50 合格 · <50 偏低\n" +
                "质量 = 各词条数值百分位的平均\n\n" +
                $"<color=#C6CEE0>锁定 ×{GameConfig.ReforgeLockFactor:0.#}/条</color>  当前锁 {locked} 条 → ×{Equipment.ReforgeLockMultiplier(locked):0.#}";

            _reforgeQualityLabel.text =
                $"词条质量 {quality}%（{e.AffixCount} 条）    {e.ReforgeCountText}";
            SetTooltip(_reforgeQualityLabel.rectTransform,
                $"词条质量 {quality}% · {tier}\n" +
                $"每条词条取它在数值区间里的百分位，再平均；{e.AffixCount} 条词条参与计算。\n" +
                $"分档：≥85 极品 / ≥70 优秀 / ≥50 合格 / <50 偏低。\n" +
                "质量只用于和上一版比较，不参与装备评分与战斗计算。");

            // 全锁时没有可洗的，避免出现「费用 0 金币（锁 4 条 ×27）」这种自相矛盾的行。
            string costText;
            if (free <= 0)
            {
                costText = "费用 ——（词条已全部锁定）";
            }
            else
            {
                costText = $"费用 {cost:N0} 金币";
                if (locked > 0) costText += $"（锁 {locked} 条 ×{Equipment.ReforgeLockMultiplier(locked):0.#}）";
                if (p.gold < cost) costText += "（不足）";
            }
            _reforgeCostLabel.text = costText;
            _reforgeCostLabel.color = free > 0 && p.gold < cost ? GameConfig.LogWarn : GameConfig.TextMuted;

            bool canPay = e.CanReforge && free > 0 && p.gold >= cost;
            _reforgeActionLabel.text = !e.CanReforge ? "无可洗词条"
                : free <= 0 ? "已全锁"
                : $"洗练 {cost:N0}";

            _reforgeActionButton.image.color = canPay ? Color.white : new Color(0.62f, 0.66f, 0.74f, 1f);
            if (_reforgeActionIcon != null)
                _reforgeActionIcon.color = canPay ? Color.white : new Color(1f, 1f, 1f, 0.45f);
            _reforgeActionLabel.color = canPay ? Color.white : GameConfig.BtnDisabledText;
        }


        /// <summary>质量分档：和右侧信息栏、tooltip 共用一套阈值，改一处就全改。</summary>
        private static string AffixQualityTier(int quality)
        {
            if (quality >= 85) return "极品";
            if (quality >= 70) return "优秀";
            if (quality >= 50) return "合格";
            return "偏低";
        }

        private void OnReforgeEntryClicked()
        {
            _reforgePanelDirty = true;
            _game.ToggleReforgePanel();
        }

        private void OnReforgeTabClicked(int index)
        {
            _reforgeSlotIndex = Mathf.Clamp(index, 0, EquipmentSlots.Count - 1);
            _reforgePanelDirty = true;
        }

        /// <summary>切换一条词条的锁定。最后一条不许锁——全锁之后没有可重抽的词条。</summary>
        private void OnReforgeAffixClicked(EquipmentAffix kind)
        {
            int side = Mathf.Clamp(_reforgeSlotIndex, 0, EquipmentSlots.Count - 1);
            Equipment e = _game.Player.GetEquipment(EquipmentSlots.All[side]);
            if (e == null) return;

            int mask = _reforgeLockMasks[side] & e.PresentAffixMask;
            int bit = Equipment.AffixBit(kind);
            bool wasLocked = (mask & bit) != 0;

            if (!wasLocked && e.LockedAffixCount(mask) >= e.AffixCount - 1)
            {
                _game.AddLog("至少要留一条不锁，否则没有可洗的词条。", GameConfig.LogWarn);
                return;
            }

            mask ^= bit;
            _reforgeLockMasks[side] = mask;
            _reforgePanelDirty = true;

            int locked = e.LockedAffixCount(mask);
            float mult = Equipment.ReforgeLockMultiplier(locked);
            _game.AddLog(
                $"{(wasLocked ? "解锁" : "锁定")} {Equipment.AffixText(kind, e.AffixValue(kind))} —— 锁 {locked} 条，洗练费用 ×{mult:0.#}",
                GameConfig.LogInfo);
        }

        private void OnReforgeSlotClicked(EquipmentSlot slot)
        {
            int side = Mathf.Clamp((int)slot, 0, EquipmentSlots.Count - 1);
            ReforgeResult result = _game.ReforgeEquipment(slot, _reforgeLockMasks[side]);
            _reforgePanelDirty = true;

            if (result == ReforgeResult.NotEnoughGold)
                _game.AddLog("金币不足，无法洗练。", GameConfig.LogWarn);
            else if (result == ReforgeResult.NoEquipment)
                _game.AddLog("该槽位还没有装备。", GameConfig.LogWarn);
            else if (result == ReforgeResult.NoAffix)
                _game.AddLog("这件装备没有副词条可以洗练。", GameConfig.LogWarn);
            else if (result == ReforgeResult.AllLocked)
                _game.AddLog("全部词条都被锁住了，至少解锁一条再洗练。", GameConfig.LogWarn);
        }

    }
}

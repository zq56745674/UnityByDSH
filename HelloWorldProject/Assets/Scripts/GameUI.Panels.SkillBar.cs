using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.Events;

namespace AutoBattle
{
    public partial class GameUI : MonoBehaviour
    {
        // 技能栏与技能池面板
        private void OnSkillSlotClicked(int slot)
        {
            _selectedSkillSlot = slot;
            bool wasOpen = _game.SkillPanelOpen;
            // 开技能面板要关掉选关面板，这条互斥规则归控制器一份。
            _game.OpenSkillPanel();
            if (!wasOpen) GameAudio.PlayUiOpen();
            RefreshSkillPanel();
            RefreshSkillChoice();
        }

        private void OnSkillPanelCloseClicked()
        {
            _game.CloseSkillPanel();
            GameAudio.PlayUiClose();
            RefreshSkillPanel();
            RefreshSkillChoice();
        }

        /// <summary>点击技能池里的技能：装到当前槽位（类别不符则放进同类空槽）。</summary>
        private void OnSkillPoolClicked(int skillIndex)
        {
            PlayerData p = _game.Player;
            SkillDef def = SkillCatalog.All[skillIndex];

            // 已经装在选中槽位上 → 再点一次就是卸下。
            if (p.GetSkillSlot(_selectedSkillSlot) == skillIndex)
            {
                _game.UnequipSkill(_selectedSkillSlot);
                RefreshSkillPanel();
            RefreshSkillChoice();
                return;
            }

            int slot = _selectedSkillSlot;
            int slotKind = slot / PlayerData.SkillSlotsPerKind;

            if ((int)def.kind != slotKind)
            {
                // 类别不符：优先放进同类空槽，全满则替换该类的第一个槽。
                int baseSlot = (int)def.kind * PlayerData.SkillSlotsPerKind;
                slot = baseSlot;
                for (int i = 0; i < PlayerData.SkillSlotsPerKind; i++)
                {
                    if (p.GetSkillSlot(baseSlot + i) < 0) { slot = baseSlot + i; break; }
                }
            }

            _game.EquipSkill(slot, def.id);
            _selectedSkillSlot = slot;
            RefreshSkillPanel();
            RefreshSkillChoice();
        }

        /// <summary>刷新技能栏：名称、冷却秒数、冷却进度条。</summary>
        private void RefreshSkillBar(PlayerData p, bool textTick)
        {
            // 槽位里「装的是什么」只随装备/换技能变：名字、字号、配色、强调条都跟着它走。
            // 冷却条逐帧跟随但按 0.05px 去抖（ratio 变化 0.05% 以下不动物理尺寸），
            // 冷却秒数按 10Hz 文本节拍写——以前这整段每帧无条件跑，六格 × 十几个属性赋值为零信息。
            var sig = new Sig();
            for (int i = 0; i < _skillSlotButtons.Length; i++) sig.Add(p.GetSkillSlot(i));
            bool rebuilt = sig.Value != _sigSkillSlots;
            if (rebuilt)
            {
                _sigSkillSlots = sig.Value;
                StaticRefreshCount++;
            }

            if (_skillCdDraw == null || _skillCdDraw.Length != _skillSlotButtons.Length)
                _skillCdDraw = new float[_skillSlotButtons.Length];

            for (int i = 0; i < _skillSlotButtons.Length; i++)
            {
                int id = p.GetSkillSlot(i);
                bool empty = id < 0 || id >= SkillCatalog.All.Length;
                SkillDef def = empty ? null : SkillCatalog.All[id];

                if (rebuilt)
                {
                    if (empty)
                    {
                        _skillSlotLabels[i].text = "＋";
                        _skillSlotLabels[i].fontSize = 16;
                        _skillSlotLabels[i].color = GameConfig.TextDisabled;
                        _skillSlotStates[i].text = "";
                        _skillSlotButtons[i].image.color = GameConfig.CardBgDim;
                        _skillSlotAccents[i].color = new Color(1f, 1f, 1f, 0.04f);
                        if (_skillSlotIcons[i] != null) _skillSlotIcons[i].enabled = false;
                        _skillCdDraw[i] = 0f;
                        _skillSlotFills[i].rectTransform.sizeDelta = new Vector2(0f, 3f);
                        continue;
                    }

                    _skillSlotLabels[i].text = def.name;
                    _skillSlotLabels[i].fontSize = 13;
                    _skillSlotLabels[i].color = def.color;
                    // 底色只给一点点技能色，主体还是深色卡；颜色靠强调条与名字承担。
                    // 注意工程是 Linear 色彩空间，脚本给的线性值上屏会被转成 sRGB 提亮，
                    // 12% 渲染出来接近 40% 的饱和度，所以这里压到 5%。
                    _skillSlotButtons[i].image.color = Color.Lerp(GameConfig.CardBg, def.color, 0.05f);
                    _skillSlotAccents[i].color = def.color;
                    _skillSlotFills[i].color = def.color;
                    SetIcon(_skillSlotIcons[i], SkillIconKey(id));

                    if (!def.IsActive)
                    {
                        // 被动没有冷却：写「就绪」是错的（它一直是生效的），
                        // 满格的冷却条也只是把底部糊成一条亮线。名字 + 强调条已经够了。
                        _skillSlotStates[i].text = "";
                        _skillCdDraw[i] = 0f;
                        _skillSlotFills[i].rectTransform.sizeDelta = new Vector2(0f, 3f);
                        continue;
                    }
                }

                if (empty || !def.IsActive) continue;

                float cd = _game.SkillCooldown(i);
                float total = def.cooldown;
                float width = 116f * (total > 0f ? Mathf.Clamp01(1f - cd / total) : 1f);

                if (Mathf.Abs(_skillCdDraw[i] - width) > 0.05f)
                {
                    _skillCdDraw[i] = width;
                    _skillSlotFills[i].rectTransform.sizeDelta = new Vector2(width, 3f);
                }

                if (!textTick) continue;

                string cdText = cd > 0f ? $"{cd:0.0}s" : "就绪";
                if (_skillSlotStates[i].text == cdText) continue;
                _skillSlotStates[i].text = cdText;
                _skillSlotStates[i].color = cd > 0f ? GameConfig.FillSkillCooldown : GameConfig.TextDisabled;
            }
        }

        /// <summary>刷新技能面板的可见性与技能池按钮状态。</summary>
        private void RefreshSkillPanel()
        {
            bool open = _game.SkillPanelOpen;
            _skillPanel.gameObject.SetActive(open);
            if (!open) return;

            PlayerData p = _game.Player;
            int sel = _selectedSkillSlot;

            _skillPanelTitle.text =
                $"技能面板 — 槽位 {sel + 1}（{KindShort(sel / PlayerData.SkillSlotsPerKind)}）" +
                $"    巅峰 {p.paragonLevel}    已获得技能 {p.OwnedSkillCount}/{SkillCatalog.All.Length}";

            for (int i = 0; i < SkillCatalog.All.Length; i++)
            {
                SkillDef def = SkillCatalog.All[i];
                bool equipped = p.FindSkillSlot((int)def.id) >= 0;
                bool unlocked = p.IsSkillOwned(def.id);
                bool inSlot = p.GetSkillSlot(sel) == i;

                string mark = inSlot ? "▶ " : (equipped ? "● " : "");
                string lockNote = unlocked ? "" : "（未获得）";
                string adviseLine = "";
                if (GameSettings.ShowSkillAdvice)
                {
                    int score = SkillAdvice.Score(p, def.id);
                    adviseLine = $"<color=#{ColorUtility.ToHtmlStringRGB(SkillAdvice.TierColor(score))}>推荐 {score} {SkillAdvice.TierLabel(score)}</color><color=#8C99B0> │ </color>";
                }
                _skillPoolLabels[i].text = $"{mark}{def.name}{lockNote}\n{adviseLine}<size=12><color=#C6CEE0>{def.description}</color></size>";

                if (!unlocked) _skillPoolLabels[i].color = GameConfig.TextDisabled;
                else if (inSlot) _skillPoolLabels[i].color = GameConfig.LogGold;
                else if (equipped) _skillPoolLabels[i].color = GameConfig.LogSuccess;
                else _skillPoolLabels[i].color = Color.white;

                SetIcon(_skillPoolIcons[i], SkillIconKey((int)def.id));
                if (_skillPoolIcons[i] != null)
                    _skillPoolIcons[i].color = unlocked ? Color.white : new Color(1f, 1f, 1f, 0.35f);

                _skillPoolButtons[i].image.color = unlocked
                    ? GameConfig.RowBg
                    : GameConfig.CardBgDim;
            }
        }

    }
}

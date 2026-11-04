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
        }

        private void OnSkillPanelCloseClicked()
        {
            _game.CloseSkillPanel();
            GameAudio.PlayUiClose();
            RefreshSkillPanel();
        }

        /// <summary>技能面板右上角的「符文」：直接切到巅峰符文面板（两者互斥，控制器里统一关）。</summary>
        private void OnSkillPanelRuneClicked()
        {
            _game.OpenRunePanel();
            GameAudio.PlayUiOpen();
            RefreshSkillPanel();
        }

        /// <summary>
        /// 点击技能池卡片：未掌握 → 花 1 技能点学习并自动装空槽；
        /// 已掌握 → 装到当前槽位（类别不符则放进同类空槽），再点一次卸下。
        /// poolIndex 是本流派 9 张卡的下标，不是全局技能 id。
        /// </summary>
        private void OnSkillPoolClicked(int poolIndex)
        {
            PlayerData p = _game.Player;
            if (p == null || _skillPoolIds == null || poolIndex < 0 || poolIndex >= _skillPoolIds.Length)
                return;

            int skillId = _skillPoolIds[poolIndex];
            if (skillId < 0 || skillId >= SkillCatalog.All.Length) return;
            SkillDef def = SkillCatalog.All[skillId];

            if (!p.IsSkillOwned(def.id))
            {
                if (_game.LearnSkill(def.id))
                {
                    int learnedSlot = p.FindSkillSlot(skillId);
                    if (learnedSlot >= 0) _selectedSkillSlot = learnedSlot;
                }
                RefreshSkillPanel();
                RefreshSkillBar(p, true);
                return;
            }

            // 已经装在选中槽位上 → 再点一次就是卸下。
            if (p.GetSkillSlot(_selectedSkillSlot) == skillId)
            {
                _game.UnequipSkill(_selectedSkillSlot);
                RefreshSkillPanel();
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
                        _skillSlotLabels[i].color = GameConfig.SlotEmptyText;
                        _skillSlotStates[i].text = "";
                        _skillSlotButtons[i].image.color = GameConfig.SlotBg;
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
                    _skillSlotButtons[i].image.color = Color.Lerp(GameConfig.SlotBg, def.color, 0.12f);
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
                _skillSlotStates[i].color = cd > 0f ? GameConfig.SlotCooldownText : GameConfig.SlotReadyText;
            }

            RefreshResourceBar(p);
        }

        /// <summary>刷新技能面板：只列本流派 6 专属 + 3 通用，标注技能点与另一流派。</summary>
        private void RefreshSkillPanel()
        {
            bool open = _game.SkillPanelOpen;
            _skillPanel.gameObject.SetActive(open);
            if (!open) return;

            PlayerData p = _game.Player;
            int sel = _selectedSkillSlot;
            Color schoolColor = p.HasDoctrine ? SkillCatalog.SchoolColor(p.doctrine) : GameConfig.TextMuted;
            string schoolHex = GameConfig.InkHex(schoolColor);

            _skillPanelTitle.text = $"技能面板 — 槽位 {sel + 1}（{KindShort(sel / PlayerData.SkillSlotsPerKind)}）";

            if (!p.HasDoctrine)
            {
                _skillPanelSchoolBar.color = GameConfig.RowBg;
                _skillPanelSchoolText.text = "尚未选择流派";
                _skillPanelOtherText.text = "先在流派面板选择本世流派，之后才能学习技能。";
                for (int i = 0; i < _skillPoolButtons.Length; i++)
                {
                    _skillPoolButtons[i].interactable = false;
                    _skillPoolButtons[i].image.color = GameConfig.CardBgDim;
                    _skillPoolLabels[i].text = "";
                    _skillPoolIds[i] = -1;
                }
                return;
            }

            string schoolName = SkillCatalog.SchoolName(p.doctrine);
            int other = p.doctrine == 1 ? 2 : 1;
            int points = p.SkillPoints;

            _skillPanelSchoolBar.color = Color.Lerp(GameConfig.RowBg, schoolColor, 0.18f);
            _skillPanelSchoolText.text =
                $"<color=#{schoolHex}>{schoolName}</color> · 本流派专属 6 / 通用 3 · " +
                $"已掌握 {p.OwnedSkillCount}/{SkillCatalog.TotalPerDoctrine} · " +
                $"<color=#8A5A12>技能点 {points}</color>";
            _skillPanelOtherText.text = $"另一流派：{SkillCatalog.SchoolName(other)}（本世锁定，不可选）";

            SkillId[] kit = SkillCatalog.KitOf(p.doctrine);
            for (int i = 0; i < _skillPoolButtons.Length && i < kit.Length; i++)
            {
                SkillDef def = SkillCatalog.Get(kit[i]);
                int id = (int)def.id;
                _skillPoolIds[i] = id;

                bool unlocked = p.IsSkillOwned(def.id);
                bool equipped = p.FindSkillSlot(id) >= 0;
                bool inSlot = p.GetSkillSlot(sel) == id;
                bool learnable = !unlocked && points > 0;
                bool usable = unlocked || learnable;

                string mark = inSlot ? "▶ " : (equipped ? "● " : "");
                string state = unlocked
                    ? (equipped ? "已装备" : "已掌握")
                    : (learnable ? "可学习 · 1 技能点" : "技能点不足");
                string advise = "";
                if (GameSettings.ShowSkillAdvice)
                {
                    int score = SkillAdvice.Score(p, def.id);
                    advise = $" · <color=#{GameConfig.InkHex(SkillAdvice.TierColor(score))}>推荐 {score} {SkillAdvice.TierLabel(score)}</color>";
                }

                string nameColor = usable ? GameConfig.InkHex(def.color) : "8A8272";
                // 通用技能不能挂当前流派名：卡头写技能自己的流派（风暴/山崩/通用），配色也跟着它。
                string cardSchool = SkillCatalog.SchoolName(def.school);
                string cardSchoolHex = GameConfig.InkHex(SkillCatalog.SchoolColor(def.school));
                _skillPoolLabels[i].text =
                    $"<size=9><color=#{cardSchoolHex}>{cardSchool} · {KindShort((int)def.kind)} · {state}</color>{advise}</size>\n"
                    + $"<size=15><b><color=#{nameColor}>{mark}{def.name}</color></b></size>\n"
                    + $"<size=10><color=#5A5142>{def.description}</color></size>";
                _skillPoolLabels[i].color = GameConfig.TextNormal;

                SetIcon(_skillPoolIcons[i], SkillIconKey(id));
                if (_skillPoolIcons[i] != null)
                    _skillPoolIcons[i].color = usable ? Color.white : new Color(1f, 1f, 1f, 0.28f);

                _skillPoolButtons[i].interactable = usable;
                _skillPoolButtons[i].image.color = unlocked
                    ? Color.Lerp(GameConfig.RowBg, schoolColor, 0.05f)
                    : (learnable ? Color.Lerp(GameConfig.RowBg, schoolColor, 0.12f) : GameConfig.CardBgDim);
                _skillPoolAccents[i].color = inSlot ? GameConfig.LogGold : (usable ? schoolColor : GameConfig.Border);

                string tip = $"{def.name}\n{KindShort((int)def.kind)} · {state}";
                if (GameSettings.ShowSkillAdvice)
                    tip += $"\n推荐 {SkillAdvice.Score(p, def.id)} {SkillAdvice.TierLabel(SkillAdvice.Score(p, def.id))}";
                tip += $"\n{def.description}";
                if (!string.IsNullOrEmpty(def.synergy)) tip += $"\n{def.synergy}";
                SetTooltip(_skillPoolButtons[i], tip);
            }
        }

    }
}

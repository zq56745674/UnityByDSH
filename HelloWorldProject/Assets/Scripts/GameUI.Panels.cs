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

            const float cardW = 236f;
            const float cardH = 150f;
            float gap = count > 1 ? (732f - cardW * count) / (count - 1) : 0f;

            for (int i = 0; i < count; i++)
            {
                int captured = i;
                float x = 24f + i * (cardW + gap);

                _skillChoiceButtons[i] = CreateButton(_skillChoicePanel, $"SkillChoice{i}", "", x, 72f, cardW, cardH,
                    GameConfig.RowBg, out _skillChoiceLabels[i]);

                // 三行文案只占卡片上方一半，UpperLeft 会留一大片空底；垂直居中读起来才对得上「卡片」。
                _skillChoiceLabels[i].fontSize = 15;
                _skillChoiceLabels[i].alignment = TextAnchor.MiddleLeft;
                _skillChoiceLabels[i].rectTransform.offsetMin = new Vector2(18f, 10f);
                _skillChoiceLabels[i].rectTransform.offsetMax = new Vector2(-16f, -10f);

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

                _skillPoolLabels[i].fontSize = 13;
                _skillPoolLabels[i].verticalOverflow = VerticalWrapMode.Truncate;
                _skillPoolLabels[i].alignment = TextAnchor.MiddleLeft;
                _skillPoolLabels[i].rectTransform.offsetMin = new Vector2(10f, 6f);
                _skillPoolLabels[i].rectTransform.offsetMax = new Vector2(-8f, -6f);

                _skillPoolButtons[i].onClick.AddListener(() => OnSkillPoolClicked(captured));
            }

            Button close = CreateButton(_skillPanel, "SkillPanelClose", "关闭", 698, 12, 90, 30,
                GameConfig.CardDanger, out _);
            close.onClick.AddListener(OnSkillPanelCloseClicked);

            _skillPanel.gameObject.SetActive(false);
        }

        // ==================== 居中横幅（步骤 4c）====================

        /// <summary>
        /// 屏幕中央的提示横幅，首通奖励与章节解锁共用。
        /// 挂在最后、带 CanvasGroup，所以能盖在选关 / 技能面板上面淡出。
        /// </summary>
        // ==================== 存档槽面板 ====================

        /// <summary>
        /// 存档槽面板：三个槽横排，点卡片切槽，卡片右下角删除。
        /// 切槽会整体重置战斗状态，所以也走浮层（打开时冻结整局），不做成侧栏的常驻控件。
        /// </summary>
        private void BuildSavePanel(Transform root)
        {
            int slots = SaveSystem.SlotCount;
            // 350 高：给底部「轮回」入口留一行。入口刻意放在存档面板里，而不是左栏，
            // 因为转生是低频且不可逆的操作，不该和「开始/速度」挤在一起。
            _savePanel = CreatePanel(root, "SavePanel", 300, 168, 680, 360, GameConfig.OverlayBg, true);

            _savePanelTitle = CreateText(_savePanel, "SavePanelTitle", "", 20, 12, 640, 30, 19,
                GameConfig.TextTitle, TextAnchor.MiddleLeft, FontStyle.Bold);

            CreateText(_savePanel, "SavePanelHint",
                "点卡片切换存档，各槽进度互不影响；「删除」清空该槽。切档会结束当前这局。",
                20, 42, 640, 22, 13, GameConfig.TextInfo, TextAnchor.MiddleLeft);

            _saveSlotButtons = new Button[slots];
            _saveSlotLabels = new Text[slots];
            _saveSlotDeleteLabels = new Text[slots];

            const float cardW = 200f;
            const float cardH = 158f;
            const float gap = 14f;
            const float y0 = 72f;

            for (int i = 0; i < slots; i++)
            {
                int slot = i;
                float x = 20f + i * (cardW + gap);

                Button card = CreateButton(_savePanel, "SaveSlot" + slot, "", x, y0, cardW, cardH,
                    GameConfig.CardBg, out Text cardLabel);

                // CreateButton 把 label 拉满并居中；这里改成左上对齐 + 内缩。
                cardLabel.alignment = TextAnchor.UpperLeft;
                // 13 号：摘要现在有 7 行（含第 N 世/印记），14 号会在卡片底部被裁掉。
                cardLabel.fontSize = 13;
                cardLabel.fontStyle = FontStyle.Normal;
                cardLabel.rectTransform.offsetMin = new Vector2(14f, 32f);
                cardLabel.rectTransform.offsetMax = new Vector2(-12f, -12f);

                card.onClick.AddListener(() => OnSaveSlotClicked(slot));

                Button del = CreateButton(_savePanel, "SaveSlotDel" + slot, "删除", x + 112f, y0 + cardH - 34f, 76f, 26f,
                    GameConfig.BtnDanger, out _saveSlotDeleteLabels[slot]);
                _saveSlotDeleteLabels[slot].fontSize = 13;
                del.onClick.AddListener(() => OnSaveSlotDeleteClicked(slot));

                _saveSlotButtons[slot] = card;
                _saveSlotLabels[slot] = cardLabel;
            }

            _rebirthEntryButton = CreateButton(_savePanel, "RebirthEntry", "♾ 轮回", 20, 250, 120, 34,
                GameConfig.BtnGold, out _rebirthEntryLabel);
            _rebirthEntryLabel.fontSize = 15;
            _rebirthEntryButton.onClick.AddListener(OnRebirthEntryClicked);

            _rebirthEntryStatus = CreateText(_savePanel, "RebirthEntryStatus", "", 148, 250, 420, 34,
                12, GameConfig.TextInfo, TextAnchor.MiddleLeft);

            Button close = CreateButton(_savePanel, "SavePanelClose", "关闭", 586, 306, 74, 34,
                GameConfig.CardBtn, out _);
            close.onClick.AddListener(() => _game.CloseSavePanel());

            _savePanel.gameObject.SetActive(false);
        }

        /// <summary>
        /// 刷新存档槽面板。摘要要解析 JSON，不能每帧做——只在刚打开或刚操作过时重算。
        /// </summary>
        private void RefreshSavePanel()
        {
            if (_savePanel == null || _game == null) return;

            bool open = _game.SavePanelOpen;
            _savePanel.gameObject.SetActive(open);
            if (!open)
            {
                _savePanelWasOpen = false;
                return;
            }

            bool justOpened = !_savePanelWasOpen;
            _savePanelWasOpen = true;
            if (!justOpened && !_savePanelDirty) return;
            _savePanelDirty = false;

            RefreshRebirthEntry();

            int current = _game.SaveSlot;
            _savePanelTitle.text = $"存档槽 —— 当前 存档 {current + 1}";

            int totalStages = StageCatalog.ChapterCount * StageCatalog.StagesPerChapter;
            for (int i = 0; i < _saveSlotButtons.Length; i++)
            {
                SaveSystem.SlotInfo info = SaveSystem.Describe(i);
                bool isCurrent = i == current;

                _saveSlotLabels[i].text = (isCurrent ? $"▶ 存档 {i + 1}（当前）" : $"存档 {i + 1}")
                    + "\n" + DescribeSlotText(info, totalStages);

                var bg = _saveSlotButtons[i].targetGraphic as Image;
                if (bg != null)
                {
                    bg.color = isCurrent
                        ? GameConfig.CardBgActive
                        : GameConfig.CardBg;
                }

                _saveSlotDeleteLabels[i].color = info.exists ? Color.white : GameConfig.TextDisabled;
            }
        }

        /// <summary>槽摘要排成卡片里的几行。</summary>
        private static string DescribeSlotText(SaveSystem.SlotInfo info, int totalStages)
        {
            if (!info.exists) return "\n（空档）\n\n点击开始新游戏";
            if (info.broken) return "\n⚠ 存档损坏\n\n点击重开新档";

            string endless = !info.endlessUnlocked
                ? "未解锁"
                : info.endlessBestFloor > 0 ? $"{info.endlessBestFloor} 层" : "—";

            string rebirth = info.version >= 9
                ? $"第 {info.rebirthCount + 1} 世 · 印记 {info.rebirthMarks}"
                : "第 1 世";

            return $"\nLv.{info.level}   巅峰 {info.paragonLevel}\n" +
                   $"第 {info.chapter} 章 {info.chapter}-{info.stage}\n" +
                   $"金币 {info.gold:N0}\n" +
                   $"首通 {info.clearedCount}/{totalStages} 关\n" +
                   $"无尽 最高 {endless}\n" +
                   rebirth;
        }

        private void OnSaveButtonClicked()
        {
            _savePanelDirty = true;
            _game.ToggleSavePanel();
        }

        private void OnSaveSlotClicked(int slot)
        {
            if (slot == _game.SaveSlot) return;
            _game.SwitchSlot(slot);
            _savePanelDirty = true;
        }

        /// <summary>
        /// 删档同样要点两次：第一次只把该槽的「删除」改成「确认？」，3 秒内再点同一槽才真删；
        /// 点别的槽或超时都会把文字复原（CancelDeleteConfirm 由 TickConfirms 调用）。
        /// </summary>
        private void OnSaveSlotDeleteClicked(int slot)
        {
            if (_deleteConfirmSlot == slot && Time.unscaledTime < _deleteConfirmUntil)
            {
                _deleteConfirmSlot = -1;
                _deleteConfirmUntil = 0f;
                if (_saveSlotDeleteLabels != null && slot < _saveSlotDeleteLabels.Length)
                    _saveSlotDeleteLabels[slot].text = "删除";
                _game.DeleteSlot(slot);
                _savePanelDirty = true;
                return;
            }

            CancelDeleteConfirm();
            _deleteConfirmSlot = slot;
            _deleteConfirmUntil = Time.unscaledTime + ConfirmWindow;
            if (_saveSlotDeleteLabels != null && slot < _saveSlotDeleteLabels.Length)
                _saveSlotDeleteLabels[slot].text = "确认？";
            _game.AddLog($"⚠ 删除会清空存档 {slot + 1}：3 秒内再点一次「确认？」才会生效", GameConfig.LogWarn);
        }

        /// <summary>取消删档确认：把「确认？」的按钮文字还原成「删除」。</summary>
        private void CancelDeleteConfirm()
        {
            if (_deleteConfirmSlot >= 0 && _saveSlotDeleteLabels != null
                && _deleteConfirmSlot < _saveSlotDeleteLabels.Length)
            {
                Text label = _saveSlotDeleteLabels[_deleteConfirmSlot];
                if (label != null) label.text = "删除";
            }

            _deleteConfirmSlot = -1;
            _deleteConfirmUntil = 0f;
        }

        // ==================== 装备强化面板（步骤 2） ====================

        /// <summary>
        /// 强化面板：顶部五个槽位标签，下方一张卡显示当前选中槽位的强化预览与费用。
        /// 强化是低频操作，做成浮层而不是常驻侧栏；面板打开时整局冻结（见 GameController.Update）。
        /// </summary>
        private void BuildEnhancePanel(Transform root)
        {
            _enhancePanel = CreatePanel(root, "EnhancePanel", 300, 186, 680, 324, GameConfig.OverlayBg, true);

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
                _enhanceSlotLabels[i].fontSize = 14;
                _enhanceSlotButtons[i].onClick.AddListener(() => OnEnhanceTabClicked(captured));
            }

            CreatePanel(_enhancePanel, "EnhanceCardBox", 20, 112, 640, 142, GameConfig.CardBg);
            _enhanceCardLabel = CreateText(_enhancePanel, "EnhanceCard", "", 34, 118, 500, 128, 13,
                GameConfig.TextNormal, TextAnchor.UpperLeft);
            _enhanceCardLabel.lineSpacing = 1.2f;

            _enhanceActionButton = CreateButton(_enhancePanel, "EnhanceAction", "强化", 520, 216, 126, 30,
                GameConfig.BtnGold, out _enhanceActionLabel);
            _enhanceActionLabel.fontSize = 15;
            _enhanceActionButton.onClick.AddListener(() => OnEnhanceSlotClicked(EquipmentSlots.All[_enhanceSlotIndex]));

            Button close = CreateButton(_enhancePanel, "EnhancePanelClose", "关闭", 586, 282, 74, 34,
                GameConfig.CardBtn, out _);
            close.onClick.AddListener(() => _game.CloseEnhancePanel());

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
                _enhanceSlotButtons[i].image.color = selected ? GameConfig.BtnPrimary : GameConfig.BtnHeader;
                _enhanceSlotLabels[i].color = selected ? Color.white
                    : unlocked ? GameConfig.TextMuted : GameConfig.BtnDisabledText;
                _enhanceSlotLabels[i].text = EquipmentSlots.Name(s) + (unlocked ? string.Empty : " · 未解锁");
            }

            EquipmentSlot slot = EquipmentSlots.All[_enhanceSlotIndex];
            Equipment e = p.GetEquipment(slot);
            _enhanceCardLabel.text = DescribeEnhanceCard(e, slot, p.gold);

            bool canPay = e != null && e.CanEnhance && p.gold >= e.EnhanceCost;
            _enhanceActionLabel.text = e == null ? "无装备"
                : !e.CanEnhance ? "已满级"
                : $"强化 {e.EnhanceCost:N0}";

            var bg = _enhanceActionButton.targetGraphic as Image;
            if (bg != null) bg.color = canPay ? GameConfig.BtnGold : GameConfig.BtnDisabled;
            _enhanceActionLabel.color = canPay ? Color.white : GameConfig.BtnDisabledText;
        }

        /// <summary>一张强化卡片里的正文。</summary>
        private static string DescribeEnhanceCard(Equipment e, EquipmentSlot slot, int gold)
        {
            string slotName = EquipmentSlots.Name(slot);
            if (e == null) return $"{slotName}\n\n（空）\n掉落装备会自动穿上，之后即可强化";
            if (!e.CanEnhance) return $"{e.Title}\n\n{e.EnhanceGainText}\n已强化到上限 +{GameConfig.EnhanceMaxLevel}";

            int cost = e.EnhanceCost;
            string costLine = gold >= cost ? $"费用 {cost:N0} 金币" : $"费用 {cost:N0} 金币（不足）";
            return $"{e.Title}\n\n{e.EnhanceGainText}\n{costLine}\n+{e.enhanceLevel} → +{e.enhanceLevel + 1}";
        }

        private void OnEnhanceEntryClicked()
        {
            _enhancePanelDirty = true;
            _game.ToggleEnhancePanel();
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
                "点击词条行可锁定；锁住的不重抽，每多锁一条本次费用 ×3。至少要留一条不锁。点顶部标签切换槽位。",
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
                _reforgeSlotLabels[i].fontSize = 14;
                _reforgeSlotButtons[i].onClick.AddListener(() => OnReforgeTabClicked(captured));
            }

            CreatePanel(_reforgePanel, "ReforgeCardBox", 20, 112, 640, 262, GameConfig.CardBg);
            _reforgeCardLabel = CreateText(_reforgePanel, "ReforgeCard", "", 34, 118, 600, 44, 14,
                GameConfig.TextNormal, TextAnchor.UpperLeft, FontStyle.Bold);

            for (int j = 0; j < affixKinds; j++)
            {
                EquipmentAffix kind = (EquipmentAffix)j;
                float rowY = 168f + j * 26f;

                Button row = CreateButton(_reforgePanel, "ReforgeAffix" + j, "", 34, rowY, 600, 24,
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

            _reforgeQualityLabel = CreateText(_reforgePanel, "ReforgeQuality", "",
                34, 298, 600, 20, 13, GameConfig.TextInfo, TextAnchor.MiddleLeft);

            _reforgeCostLabel = CreateText(_reforgePanel, "ReforgeCost", "",
                34, 322, 600, 20, 13, GameConfig.TextMuted, TextAnchor.MiddleLeft);

            _reforgeActionButton = CreateButton(_reforgePanel, "ReforgeAction", "洗练", 34, 344, 200, 30,
                GameConfig.BtnGold, out _reforgeActionLabel);
            _reforgeActionLabel.fontSize = 15;
            _reforgeActionButton.onClick.AddListener(() => OnReforgeSlotClicked(EquipmentSlots.All[_reforgeSlotIndex]));

            Button close = CreateButton(_reforgePanel, "ReforgePanelClose", "关闭", 586, 392, 74, 34,
                GameConfig.CardBtn, out _);
            close.onClick.AddListener(() => _game.CloseReforgePanel());

            _reforgePanel.gameObject.SetActive(false);
        }

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
                _reforgeSlotButtons[i].image.color = selected ? GameConfig.BtnPrimary : GameConfig.BtnHeader;
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
            if (e == null) return;

            int quality = Mathf.RoundToInt(e.AffixQuality * 100f);
            _reforgeQualityLabel.text =
                $"词条质量 {quality}%（{e.AffixCount} 条）    {e.ReforgeCountText}";

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

            var bg = _reforgeActionButton.targetGraphic as Image;
            if (bg != null) bg.color = canPay ? GameConfig.BtnGold : GameConfig.BtnDisabled;
            _reforgeActionLabel.color = canPay ? Color.white : GameConfig.BtnDisabledText;
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

        private void BuildBanner(Transform root)
        {
            _bannerPanel = CreatePanel(root, "Banner", 340, 186, 600, 120,
                GameConfig.ScreenDim);

            // 金边：横幅要盖在日志上，光靠底色压不住下面的字。改用圆角描边 + 顶部金线。
            AddBorder(_bannerPanel.GetComponent<Image>(), RadiusPanel, GameConfig.AccentBanner);
            CreateImage(_bannerPanel, "BannerTopLine", 18, 0, 564, 2, GameConfig.AccentBanner);

            _bannerGroup = _bannerPanel.gameObject.AddComponent<CanvasGroup>();
            _bannerGroup.blocksRaycasts = false;
            _bannerGroup.interactable = false;

            _bannerTitle = CreateText(_bannerPanel, "BannerTitle", "", 16, 20, 568, 46, 28,
                GameConfig.AccentBannerTitle, TextAnchor.MiddleCenter, FontStyle.Bold);

            _bannerSubtitle = CreateText(_bannerPanel, "BannerSubtitle", "", 16, 68, 568, 34, 17,
                GameConfig.TextInfo, TextAnchor.MiddleCenter);

            _bannerPanel.gameObject.SetActive(false);
        }

        /// <summary>刷新横幅：内容、显隐、最后 0.6 秒淡出。</summary>
        private void RefreshBanner()
        {
            if (_bannerPanel == null || _game == null) return;

            bool show = !string.IsNullOrEmpty(_game.BannerTitle) && _game.BannerTimeLeft > 0f;
            _bannerPanel.gameObject.SetActive(show);
            if (!show) return;

            _bannerTitle.text = _game.BannerTitle;
            _bannerSubtitle.text = _game.BannerSubtitle;

            const float fadeWindow = 0.6f;
            _bannerGroup.alpha = Mathf.Clamp01(_game.BannerTimeLeft / fadeWindow);
        }



        // ==================== 选关面板（4b）====================

        /// <summary>
        /// 选关面板：左侧章节列表、右侧该章 10 个关卡，下方是选中章的详情。
        /// 章节级解锁——解锁某章即该章全可打；已通关的关卡随时能回头刷。
        /// </summary>
        private void BuildStagePanel(Transform root)
        {
            int chapters = StageCatalog.ChapterCount;
            int stages = StageCatalog.StagesPerChapter;

            _stagePanel = CreatePanel(root, "StagePanel", 236, 96, 808, 508, GameConfig.OverlayBg, true);

            _stagePanelTitle = CreateText(_stagePanel, "StagePanelTitle", "", 20, 12, 768, 30, 19,
                GameConfig.TextTitle, TextAnchor.MiddleLeft, FontStyle.Bold);

            // 这一行原来是静态操作提示，改成全局进度总览——操作提示在下方详情里已经有一份了。
            _stageSummaryText = CreateText(_stagePanel, "StagePanelSummary", "", 20, 44, 768, 24, 13,
                GameConfig.TextInfo, TextAnchor.MiddleLeft);

            // 左栏：11 个章节按钮，每个 31 高、间距 3。
            _stageChapterButtons = new Button[chapters];
            _stageChapterLabels = new Text[chapters];

            for (int i = 0; i < chapters; i++)
            {
                int captured = i;
                float y = 76f + i * 34f;

                _stageChapterButtons[i] = CreateButton(_stagePanel, $"StageChapter{i}", "", 20f, y, 148f, 31f,
                    GameConfig.RowBgDim, out _stageChapterLabels[i]);

                _stageChapterLabels[i].fontSize = 13;
                _stageChapterLabels[i].alignment = TextAnchor.MiddleLeft;
                _stageChapterLabels[i].rectTransform.offsetMin = new Vector2(10f, 2f);
                _stageChapterLabels[i].rectTransform.offsetMax = new Vector2(-8f, -2f);

                _stageChapterButtons[i].onClick.AddListener(() => OnStageChapterClicked(captured));
            }

            // 右栏：该章 10 个关卡，5 列 2 行，每格 110×76。
            _stageSlotButtons = new Button[stages];
            _stageSlotLabels = new Text[stages];

            for (int i = 0; i < stages; i++)
            {
                int captured = i;
                int col = i % 5;
                int row = i / 5;
                float x = 188f + col * 122f;
                float y = 76f + row * 88f;

                _stageSlotButtons[i] = CreateButton(_stagePanel, $"StageSlot{i}", "", x, y, 110f, 76f,
                    GameConfig.RowBg, out _stageSlotLabels[i]);

                _stageSlotLabels[i].fontSize = 14;
                _stageSlotLabels[i].alignment = TextAnchor.MiddleCenter;

                _stageSlotButtons[i].onClick.AddListener(() => OnStageSlotClicked(captured));
            }

            _stageInfoText = CreateText(_stagePanel, "StageInfo", "", 188, 252, 600, 180, 14,
                GameConfig.TextMuted, TextAnchor.UpperLeft);

            Button close = CreateButton(_stagePanel, "StagePanelClose", "关闭", 698, 458, 90, 36,
                GameConfig.CardDanger, out _);
            close.onClick.AddListener(OnStagePanelCloseClicked);

            _stagePanel.gameObject.SetActive(false);
        }

        private void OnStageButtonClicked() => _game.ToggleStagePanel();

        private void OnStagePanelCloseClicked()
        {
            if (_game != null) _game.CloseStagePanel();
        }

        private void OnStageChapterClicked(int index)
        {
            _stageViewChapter = index + 1;
        }

        private void OnStageSlotClicked(int index)
        {
            if (_game == null) return;
            _game.SelectStage(_stageViewChapter, index + 1);
        }

        /// <summary>刷新选关面板：章节解锁态、关卡按钮、当前章详情。</summary>
        private void RefreshStagePanel()
        {
            if (_stagePanel == null || _game == null || _game.Player == null) return;

            StageDef current = _game.Player.CurrentStage;
            int currentKey = current != null ? current.chapter * 100 + current.stage : -1;

            // 无尽进行中，左栏第一个按钮改报层号；用 900000+ 段避开战役的 chapter*100+stage。
            bool endless = _game.EndlessActive && _game.EndlessRun != null;
            if (endless) currentKey = 900000 + _game.EndlessRun.floor;

            // 入口按钮上的速览：面板不开也要对，但只有「关卡/层号」真的换了才重写。
            if (_stageButtonLabel != null && _stageButtonStageKey != currentKey)
            {
                _stageButtonStageKey = currentKey;
                if (endless)
                {
                    int floor = _game.EndlessRun.floor;
                    _stageButtonLabel.text =
                        $"第 {floor} 层 · 怪 Lv.{EndlessCatalog.MonsterLevelOf(floor)}";
                }
                else
                {
                    _stageButtonLabel.text = current != null
                        ? $"关卡 {current.Code} · 怪 Lv.{current.monsterLevel}"
                        : "关卡 —";
                }
            }

            bool open = _game.StagePanelOpen;
            _stagePanel.gameObject.SetActive(open);

            if (!open)
            {
                _stagePanelWasOpen = false;
                return;
            }

            // 只在「刚打开」这一帧把浏览章节拨到玩家当前所在的章；
            // 否则玩家一点别的章节，就会被每帧刷新弹回去。
            if (!_stagePanelWasOpen && current != null)
            {
                _stageViewChapter = Mathf.Clamp(current.chapter, 1, StageCatalog.ChapterCount);
            }
            _stagePanelWasOpen = true;

            // 面板开着时整局冻结（GameController.Update 直接 return），数据不会自己变；
            // 只有玩家点章节或关卡才会变。早先这里每帧全量重建——11 次首通金币、
            // 11 次章节进度、10 次 StageCatalog.Get（每次都 new StageDef）+ 十余段字符串拼接，
            // 全是白烧。用签名挡住重复重建。
            int sig = _stageViewChapter * 1000003 + _game.MaxChapterUnlocked * 1009 + currentKey
                      + (_game.Player.ClearedStageCount & 0xFFFF) * 101;
            if (sig == _stagePanelSig) return;
            _stagePanelSig = sig;

            int maxCh = _game.MaxChapterUnlocked;

            _stagePanelTitle.text =
                $"关卡选择 —— 当前 {current.Title}（怪 Lv.{current.monsterLevel}）    已解锁至 第 {maxCh} 章";

            // 全局进度总览（步骤 4d）：关卡数、章节数、首通金币三档进度放在一行。
            int totalStages = StageCatalog.ChapterCount * StageCatalog.StagesPerChapter;
            int clearedStages = _game.Player.ClearedStageCount;
            int claimedGold = _game.Player.TotalFirstClearGoldClaimed();
            int allGold = StageCatalog.TotalFirstClearGold(StageCatalog.ChapterCount);

            if (_stageSummaryText != null)
            {
                _stageSummaryText.text =
                    $"<color=#AAB4C6>总进度：</color>首通 <color=#9FD8A0>{clearedStages}</color>/{totalStages} 关 · " +
                    $"解锁 <color=#FFE08A>{maxCh}</color>/{StageCatalog.ChapterCount} 章 · " +
                    $"首通金币 <color=#FFD98A>{claimedGold:N0}</color> / {allGold:N0}";
            }

            for (int i = 0; i < _stageChapterButtons.Length; i++)
            {
                int chapter = i + 1;
                bool unlocked = chapter <= maxCh;
                bool viewing = _stageViewChapter == chapter;

                // 锁形 emoji 在 Unity 默认字体里没有字形，会渲染成空白方块，所以用「×」代替。
                // 已解锁的章带上该章首通进度：关卡能回头重刷，光看「已过」分不清哪章还没打满。
                int clearedInThis = unlocked ? _game.Player.ClearedCountInChapter(chapter) : 0;
                _stageChapterLabels[i].text = unlocked
                    ? $"第 {chapter} 章<size=11><color=#9FD8A0> {clearedInThis}/{StageCatalog.StagesPerChapter}</color></size>"
                    : $"× 第 {chapter} 章";
                _stageChapterLabels[i].color = unlocked
                    ? (viewing ? GameConfig.AccentBannerTitle : GameConfig.TextInfo)
                    : GameConfig.TextDisabled;
                _stageChapterLabels[i].fontStyle = current.chapter == chapter ? FontStyle.Bold : FontStyle.Normal;

                // Button.image 就是 targetGraphic，建按钮时已经设好；这里不必每帧再 GetComponent。
                Image bg = _stageChapterButtons[i].image;
                if (bg != null)
                {
                    bg.color = viewing
                        ? GameConfig.RowBgSelected
                        : GameConfig.RowBgDim;
                }
            }

            for (int i = 0; i < _stageSlotButtons.Length; i++)
            {
                int stageNo = i + 1;
                StageDef def = StageCatalog.Get(_stageViewChapter, stageNo);

                bool unlocked = _game.IsStageUnlocked(_stageViewChapter, stageNo);
                bool isCurrent = current.chapter == def.chapter && current.stage == def.stage;
                // 「已过」以当前进度线划线：当前章之前的章全算过，当前章内按关卡号比。
                // 回头看低章重刷时，之后的章其实也通关了，但那里不标——玩家正站在
                // 低章，把高章标成「已过」只会让人看花。
                bool passed = def.chapter < current.chapter
                    || (def.chapter == current.chapter && def.stage < current.stage);

                string title = def.IsChapterFinal
                    ? $"{def.Code}<size=11> 章末</size>"
                    : def.Code;

                _stageSlotLabels[i].text = unlocked
                    ? $"{title}\n<size=11><color=#AAB0BD>Lv.{def.monsterLevel}</color></size>\n" +
                      (isCurrent ? "<color=#FFE08A>▶ 当前</color>" : (passed ? "<color=#9FD8A0>✔ 已过</color>" : " "))
                    : $"{def.Code}\n<size=11><color=#6E7280>Lv.{def.monsterLevel} 锁定</color></size>\n ";

                _stageSlotLabels[i].color = unlocked
                    ? (isCurrent ? Color.white : (passed ? GameConfig.TextPassed : GameConfig.TextNormal))
                    : GameConfig.TextDisabled;
            }

            // 详情：选中章的规则一览。
            bool viewUnlocked = _stageViewChapter <= maxCh;
            StageDef sample = StageCatalog.Get(_stageViewChapter, 1);
            MonsterTier finalTier = StageCatalog.ClearTierOf(_stageViewChapter, StageCatalog.StagesPerChapter);
            string tierName = finalTier == MonsterTier.Boss ? "BOSS" : "精英怪";

            int clearedInChapter = _game.Player.ClearedCountInChapter(_stageViewChapter);

            int firstClearGold = StageCatalog.FirstClearGold(_stageViewChapter, 1);

            _stageInfoText.text =
                $"<size=17><b>第 {_stageViewChapter} 章</b></size>\n\n" +
                $"怪物等级：<color=#FFD98A>Lv.{sample.monsterLevel}</color>\n" +
                $"每关击杀：{sample.killTarget} 只（关底放 {tierName}）\n" +
                $"关卡数：{StageCatalog.StagesPerChapter} 关\n" +
                $"已首通：<color=#9FD8A0>{clearedInChapter}/{StageCatalog.StagesPerChapter}</color>\n" +
                $"首通奖励：<color=#FFD98A>{firstClearGold}</color> 金币" +
                (_stageViewChapter > 1 ? $"（关末再 +{GameConfig.ChapterClearGoldBonus}）" : "\n") +
                "\n" +
                (viewUnlocked
                    ? "<color=#9FD8A0>该章已解锁：点上方任意关卡开打；已通关的可以反复刷。</color>"
                    : $"<color=#E0A080>尚未解锁：先把第 {maxCh} 章打完。</color>");
        }

        private void OnSkillSlotClicked(int slot)
        {
            _selectedSkillSlot = slot;
            // 开技能面板要关掉选关面板，这条互斥规则归控制器一份。
            _game.OpenSkillPanel();
            RefreshSkillPanel();
            RefreshSkillChoice();
        }

        private void OnSkillPanelCloseClicked()
        {
            _game.CloseSkillPanel();
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
                float width = 106f * (total > 0f ? Mathf.Clamp01(1f - cd / total) : 1f);

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

                _skillPoolButtons[i].image.color = unlocked
                    ? GameConfig.RowBg
                    : GameConfig.CardBgDim;
            }
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

                _shopGoldText.text = $"金币 {p.gold}";
                _shopPotionText.text = $"生命药剂    价格 {price}    持有 {p.potionCount}/{GameConfig.PotionMaxCount}";

                _buyButton.image.color = canBuy
                    ? GameConfig.BtnPrimary
                    : GameConfig.CardBtn;
                // 灰按钮上写「购买」会让人以为是没做好：把按不动的原因直接写在按钮上。
                _buyButtonLabel.text = canBuy
                    ? "购买"
                    : (!open ? "未开放"
                        : (p.potionCount >= GameConfig.PotionMaxCount ? "已满" : "金币不足"));

                _autoBuyLabel.text = _game.AutoBuyPotion ? "[✓] 自动购买" : "[  ] 自动购买";
                _autoBuyLabel.color = _game.AutoBuyPotion
                    ? GameConfig.LogHeal
                    : GameConfig.TextMuted;
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

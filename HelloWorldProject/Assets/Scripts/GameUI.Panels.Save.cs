using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.Events;

namespace AutoBattle
{
    public partial class GameUI : MonoBehaviour
    {
        // 存档槽面板与重置/删档的二次确认
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

            CreateIcon(_savePanel, "ui_save", 20, 16, 20);
            _savePanelTitle = CreateText(_savePanel, "SavePanelTitle", "", 48, 12, 612, 30, 19,
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
                cardLabel.rectTransform.offsetMax = new Vector2(-12f, -10f);

                card.onClick.AddListener(() => OnSaveSlotClicked(slot));

                Button rename = CreateButton(_savePanel, "SaveSlotRename" + slot, "改名", x + 4f, y0 + cardH - 30f, 60f, 26f,
                    GameConfig.CardBtn, out Text renameLabel);
                StyleBar(rename, renameLabel, "frame_pill", "glyph_book", 10f, "重命名存档 " + (slot + 1), 10);
                rename.onClick.AddListener(() => OnSaveRenameClicked(slot));

                Button copy = CreateButton(_savePanel, "SaveSlotCopy" + slot, "复制", x + 68f, y0 + cardH - 30f, 60f, 26f,
                    GameConfig.CardBtn, out Text copyLabel);
                StyleBar(copy, copyLabel, "frame_pill", "glyph_equip", 10f, "把存档 " + (slot + 1) + " 复制到另一个槽", 10);
                copy.onClick.AddListener(() => OnSaveCopyClicked(slot));

                Button del = CreateButton(_savePanel, "SaveSlotDel" + slot, "删除", x + 132f, y0 + cardH - 30f, 64f, 26f,
                    GameConfig.BtnDanger, out _saveSlotDeleteLabels[slot]);
                StyleBar(del, _saveSlotDeleteLabels[slot], "frame_pill", "glyph_delete", 12f,
                    "删除存档 " + (slot + 1), 10);
                del.onClick.AddListener(() => OnSaveSlotDeleteClicked(slot));

                _saveSlotButtons[slot] = card;
                _saveSlotLabels[slot] = cardLabel;
            }

            _rebirthEntryButton = CreateButton(_savePanel, "RebirthEntry", "轮回", 20, 250, 120, 34,
                GameConfig.BtnGold, out _rebirthEntryLabel);
            StyleBar(_rebirthEntryButton, _rebirthEntryLabel, "frame_pill", "glyph_rebirth", 16f, "进入轮回", 13);
            _rebirthEntryButton.onClick.AddListener(OnRebirthEntryClicked);

            _rebirthEntryStatus = CreateText(_savePanel, "RebirthEntryStatus", "", 148, 250, 420, 34,
                12, GameConfig.TextInfo, TextAnchor.MiddleLeft);

            // 导出 / 导入（roadmap A6 / C10）：导出写「我的文档\AutoBattle\slotN.json」并复制 JSON 到剪贴板；
            // 导入优先读剪贴板，读不到再读同名导出文件。两个都只动当前槽，不碰其他槽。
            Button export = CreateButton(_savePanel, "SaveExport", "导出", 20, 306, 100, 34,
                GameConfig.CardBtn, out Text exportLabel);
            StyleBar(export, exportLabel, "frame_pill", "glyph_save", 14f, "导出当前存档", 13);
            export.onClick.AddListener(() =>
            {
                _game.ExportCurrentSlot();
                _savePanelDirty = true;
            });

            Button import = CreateButton(_savePanel, "SaveImport", "导入", 132, 306, 100, 34,
                GameConfig.CardBtn, out Text importLabel);
            StyleBar(import, importLabel, "frame_pill", "glyph_book", 14f, "从剪贴板或导出文件导入", 13);
            import.onClick.AddListener(() =>
            {
                _game.ImportIntoCurrentSlot();
                _savePanelDirty = true;
            });

            CreateText(_savePanel, "SaveExportHint", "导出到文档目录并复制 JSON；导入读剪贴板",
                244, 306, 332, 34, 12, GameConfig.TextMuted, TextAnchor.MiddleLeft);

            Button close = CreateButton(_savePanel, "SavePanelClose", "关闭", 586, 306, 74, 34,
                GameConfig.CardBtn, out Text saveCloseLabel);
            StyleBar(close, saveCloseLabel, "frame_pill", "glyph_close", 14f, "关闭存档槽");
            close.onClick.AddListener(() =>
            {
                _game.CloseSavePanel();
                GameAudio.PlayUiClose();
            });

            BuildSaveNamePanel();
            BuildSaveCopyPanel();

            _savePanel.gameObject.SetActive(false);
        }

        /// <summary>重命名弹层：一个输入框 + 确定/取消/用默认名。</summary>
        private void BuildSaveNamePanel()
        {
            _saveNamePanel = CreatePanel(_savePanel, "SaveNamePanel", 80, 92, 520, 176,
                GameConfig.OverlayBg, true);

            _saveNameTitle = CreateText(_saveNamePanel, "SaveNameTitle", "重命名存档", 24, 14, 472, 26,
                16, GameConfig.TextTitle, TextAnchor.MiddleLeft, FontStyle.Bold);

            Image fieldBg = CreateImage(_saveNamePanel, "SaveNameField", 24, 50, 472, 36,
                GameConfig.InsetBg);

            Text value = CreateText(fieldBg.transform, "Text", "", 10, 2, 452, 32, 15,
                Color.white, TextAnchor.MiddleLeft);
            value.supportRichText = false;
            value.raycastTarget = false;
            Text placeholder = CreateText(fieldBg.transform, "Placeholder", "输入名字，最多 12 个字", 10, 2, 452, 32,
                15, GameConfig.TextDisabled, TextAnchor.MiddleLeft);
            placeholder.raycastTarget = false;

            _saveNameInput = fieldBg.gameObject.AddComponent<InputField>();
            _saveNameInput.targetGraphic = fieldBg;
            _saveNameInput.textComponent = value;
            _saveNameInput.placeholder = placeholder;
            _saveNameInput.lineType = InputField.LineType.SingleLine;
            _saveNameInput.characterLimit = SaveSystem.SlotNameMaxLength;
            _saveNameInput.onEndEdit.AddListener(text =>
            {
                if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
                    ConfirmSaveRename();
            });

            _saveNameHint = CreateText(_saveNamePanel, "SaveNameHint", "", 24, 94, 472, 18,
                11, GameConfig.TextMuted, TextAnchor.MiddleLeft);

            Button useDefault = CreateButton(_saveNamePanel, "SaveNameDefault", "用默认名", 24, 126, 120, 34,
                GameConfig.CardBtn, out Text defaultLabel);
            StyleBar(useDefault, defaultLabel, "frame_pill", "glyph_reset", 13f, "改回「存档 N」", 12);
            useDefault.onClick.AddListener(() =>
            {
                _saveNameInput.text = _saveNameSlot >= 0 ? SaveSystem.DefaultSlotName(_saveNameSlot) : string.Empty;
                _saveNameInput.ActivateInputField();
            });

            Button confirm = CreateButton(_saveNamePanel, "SaveNameConfirm", "确定", 292, 126, 100, 34,
                GameConfig.BtnGold, out Text confirmLabel);
            StyleBar(confirm, confirmLabel, "frame_pill", "glyph_confirm", 14f, "保存名字", 13);
            confirm.onClick.AddListener(ConfirmSaveRename);

            Button cancel = CreateButton(_saveNamePanel, "SaveNameCancel", "取消", 402, 126, 94, 34,
                GameConfig.CardBtn, out Text cancelLabel);
            StyleBar(cancel, cancelLabel, "frame_pill", "glyph_close", 13f, "取消重命名", 12);
            cancel.onClick.AddListener(CloseSaveNamePanel);

            _saveNamePanel.gameObject.SetActive(false);
        }

        /// <summary>复制弹层：目标只可能是另外两个槽，有档的目标要二次确认防覆盖。</summary>
        private void BuildSaveCopyPanel()
        {
            _saveCopyPanel = CreatePanel(_savePanel, "SaveCopyPanel", 80, 80, 520, 208,
                GameConfig.OverlayBg, true);

            _saveCopyTitle = CreateText(_saveCopyPanel, "SaveCopyTitle", "复制存档", 24, 14, 472, 26,
                16, GameConfig.TextTitle, TextAnchor.MiddleLeft, FontStyle.Bold);

            _saveCopyButtons = new Button[SaveSystem.SlotCount];
            _saveCopyLabels = new Text[SaveSystem.SlotCount];
            for (int i = 0; i < SaveSystem.SlotCount; i++)
            {
                int target = i;
                float x = i == 0 ? 24f : 268f;
                _saveCopyButtons[i] = CreateButton(_saveCopyPanel, "SaveCopyTarget" + i, "", x, 52, 228, 48,
                    GameConfig.CardBtn, out _saveCopyLabels[i]);
                StyleBar(_saveCopyButtons[i], _saveCopyLabels[i], "frame_tab", "glyph_save", 13f,
                    "复制到这个槽", 12);
                _saveCopyButtons[i].onClick.AddListener(() => OnSaveCopyTargetClicked(target));
            }

            _saveCopyHint = CreateText(_saveCopyPanel, "SaveCopyHint", "", 24, 110, 472, 34,
                11, GameConfig.TextMuted, TextAnchor.UpperLeft);

            Button cancel = CreateButton(_saveCopyPanel, "SaveCopyCancel", "取消", 402, 158, 94, 34,
                GameConfig.CardBtn, out Text cancelLabel);
            StyleBar(cancel, cancelLabel, "frame_pill", "glyph_close", 13f, "取消复制", 12);
            cancel.onClick.AddListener(CloseSaveCopyPanel);

            _saveCopyPanel.gameObject.SetActive(false);
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
                CloseSaveNamePanel();
                CloseSaveCopyPanel();
                return;
            }

            bool justOpened = !_savePanelWasOpen;
            _savePanelWasOpen = true;
            RefreshSaveModals();
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

                string slotName = SaveSystem.DisplayNameOf(i);
                _saveSlotLabels[i].text = (isCurrent ? "▶ " + slotName + "（当前）" : slotName)
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
            bool open = _game.SavePanelOpen;
            _game.ToggleSavePanel();
            if (open) GameAudio.PlayUiClose();
            else GameAudio.PlayUiOpen();
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

        // ---- 改名 / 复制（C10 剩余部分）----

        private void OnSaveRenameClicked(int slot)
        {
            if (!SaveSystem.Describe(slot).exists)
            {
                _game.AddLog($"⚠ 存档 {slot + 1} 是空档，没有名字可改。", GameConfig.LogWarn);
                return;
            }

            _saveNameSlot = slot;
            _saveNameTitle.text = "重命名 存档 " + (slot + 1);
            _saveNameHint.text = "最多 12 个字；留空或改回「存档 " + (slot + 1) + "」即恢复默认名。";
            _saveNameInput.text = SaveSystem.DisplayNameOf(slot);
            _saveNamePanel.SetAsLastSibling();
            _saveNamePanel.gameObject.SetActive(true);
            _saveNameInput.ActivateInputField();
            _saveNameInput.Select();
            GameAudio.PlayUiOpen();
        }

        private void ConfirmSaveRename()
        {
            if (_saveNameSlot < 0) return;
            if (!SaveSystem.RenameSlot(_saveNameSlot, _saveNameInput.text, out string error))
            {
                _game.AddLog("<color=#FF8080>✖ " + error + "</color>", GameConfig.LogWarn);
                _saveNameInput.ActivateInputField();
                return;
            }

            _game.AddLog($"✎ 存档 {_saveNameSlot + 1} 现在叫「{SaveSystem.DisplayNameOf(_saveNameSlot)}」。",
                GameConfig.LogSuccess);
            _savePanelDirty = true;
            CloseSaveNamePanel();
            GameAudio.PlayUiClose();
        }

        private void CloseSaveNamePanel()
        {
            if (_saveNamePanel != null) _saveNamePanel.gameObject.SetActive(false);
            _saveNameSlot = -1;
        }

        private void OnSaveCopyClicked(int source)
        {
            if (!SaveSystem.Describe(source).exists)
            {
                _game.AddLog($"⚠ 存档 {source + 1} 是空档，没有内容可复制。", GameConfig.LogWarn);
                return;
            }

            _saveCopySource = source;
            _saveCopyConfirmSlot = -1;
            _saveCopyConfirmUntil = 0f;
            _saveCopyTitle.text = "复制 存档 " + (source + 1) + " 到…";
            _saveCopyHint.text = "复制会连进度和名字一起覆盖目标槽；当前槽不会被改动，除非目标就是当前槽（那样会立刻重读）。";
            _saveCopyPanel.SetAsLastSibling();
            _saveCopyPanel.gameObject.SetActive(true);
            RefreshSaveCopyTargets();
            GameAudio.PlayUiOpen();
        }

        private void OnSaveCopyTargetClicked(int target)
        {
            if (_saveCopySource < 0 || target == _saveCopySource) return;

            bool exists = SaveSystem.Describe(target).exists;
            bool armed = _saveCopyConfirmSlot == target && Time.unscaledTime < _saveCopyConfirmUntil;
            if (exists && !armed)
            {
                _saveCopyConfirmSlot = target;
                _saveCopyConfirmUntil = Time.unscaledTime + ConfirmWindow;
                RefreshSaveCopyTargets();
                _game.AddLog($"⚠ 存档 {target + 1} 已有进度：3 秒内再点一次才会覆盖。", GameConfig.LogWarn);
                return;
            }

            if (!SaveSystem.CopySlot(_saveCopySource, target, out string error))
            {
                _game.AddLog("<color=#FF8080>✖ " + error + "</color>", GameConfig.LogWarn);
                CloseSaveCopyPanel();
                return;
            }

            bool targetIsCurrent = target == _game.SaveSlot;
            if (targetIsCurrent) _game.ReloadCurrentSlotFromDisk();
            _game.AddLog($"⇄ 已把 存档 {_saveCopySource + 1} 复制到 存档 {target + 1} " +
                         (targetIsCurrent ? "（当前槽已重新读取）" : string.Empty) + "。", GameConfig.LogSuccess);
            _savePanelDirty = true;
            CloseSaveCopyPanel();
            GameAudio.PlayUiClose();
        }

        private void CloseSaveCopyPanel()
        {
            if (_saveCopyPanel != null) _saveCopyPanel.gameObject.SetActive(false);
            _saveCopySource = -1;
            _saveCopyConfirmSlot = -1;
            _saveCopyConfirmUntil = 0f;
        }

        /// <summary>弹层开着时每帧刷新（覆盖确认要倒数），关着就什么都不做。</summary>
        private void RefreshSaveModals()
        {
            if (_saveNamePanel != null && _saveNamePanel.gameObject.activeSelf && _saveNameSlot < 0)
                _saveNamePanel.gameObject.SetActive(false);

            if (_saveCopyPanel != null && _saveCopyPanel.gameObject.activeSelf)
            {
                if (_saveCopySource < 0) _saveCopyPanel.gameObject.SetActive(false);
                else RefreshSaveCopyTargets();
            }
        }

        private void RefreshSaveCopyTargets()
        {
            if (_saveCopyButtons == null || _saveCopySource < 0) return;

            int visible = 0;
            for (int i = 0; i < _saveCopyButtons.Length; i++)
            {
                if (i == _saveCopySource)
                {
                    _saveCopyButtons[i].gameObject.SetActive(false);
                    continue;
                }

                _saveCopyButtons[i].gameObject.SetActive(true);
                RectTransform targetRect = (RectTransform)_saveCopyButtons[i].transform;
                targetRect.anchoredPosition = new Vector2(visible == 0 ? 24f : 268f, targetRect.anchoredPosition.y);
                visible++;
                string name = SaveSystem.DisplayNameOf(i);
                bool exists = SaveSystem.Describe(i).exists;
                bool armed = _saveCopyConfirmSlot == i && Time.unscaledTime < _saveCopyConfirmUntil;

                if (armed)
                {
                    float left = Mathf.Max(0f, _saveCopyConfirmUntil - Time.unscaledTime);
                    _saveCopyLabels[i].text = $"确认覆盖 {name}\n{left:0.0}s 内再点一次";
                }
                else
                {
                    _saveCopyLabels[i].text = (exists ? "覆盖 " : "复制到 ") + name +
                                               (exists ? "\n（已有存档）" : "\n（空槽）");
                }
            }
        }

    }
}

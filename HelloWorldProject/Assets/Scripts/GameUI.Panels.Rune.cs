using UnityEngine;
using UnityEngine.UI;

namespace AutoBattle
{
    public partial class GameUI : MonoBehaviour
    {
        // ==================== 巅峰符文面板（2026-11-04） ====================
        // 每 100 巅峰 1 点，花 1 点解锁某个技能的形态选择权；解锁后两个形态免费来回切。
        // 面板只读 PlayerData 的符文掩码，所有写入都走 GameController 的 UnlockRune / SwitchRuneForm。

        /// <summary>符文面板：技能面板右上角「符文」进入。</summary>
        private void BuildRunePanel(Transform root)
        {
            _runePanel = CreatePanel(root, "RunePanel", 236, 96, 808, 544, GameConfig.InsetBg, true);

            _runePanelTitle = CreateText(_runePanel, "RunePanelTitle", "", 20, 12, 560, 30, 19,
                GameConfig.TextTitle, TextAnchor.MiddleLeft, FontStyle.Bold);

            CreateText(_runePanel, "RunePanelHint",
                "每 100 巅峰 1 点；花 1 点解锁一个技能的形态（甲 / 乙），解锁后点卡片免费互换",
                20, 44, 768, 22, 12, GameConfig.TextMuted, TextAnchor.MiddleLeft);

            _runePointsBar = CreateImage(_runePanel, "RunePointsBar", 20, 70, 768, 28, GameConfig.RowBg);
            Round(_runePointsBar, RadiusBar);
            _runePointsText = CreateText(_runePanel, "RunePointsText", "", 30, 72, 748, 24, 14,
                GameConfig.TextTitle, TextAnchor.MiddleLeft, FontStyle.Bold);

            const int columns = 3;
            int total = SkillCatalog.TotalPerDoctrine;
            _runeCardButtons = new Button[total];
            _runeCardLabels = new Text[total];
            _runeCardIcons = new Image[total];
            _runeCardAccents = new Image[total];
            _runeIds = new SkillId[total];

            for (int i = 0; i < total; i++)
            {
                int captured = i;
                int col = i % columns;
                int row = i / columns;
                float x = 20f + col * 258f;
                float y = 106f + row * 112f;

                Image shadow = CreateHardShadow(_runePanel, $"RuneCardShadow{i}", x, y, 248, 104, RadiusCard);
                _runeCardButtons[i] = CreateButton(_runePanel, $"RuneCard{i}", "", x, y, 248, 104,
                    GameConfig.RowBg, out _runeCardLabels[i]);
                Image card = _runeCardButtons[i].GetComponent<Image>();
                if (card != null)
                {
                    Round(card, RadiusCard);
                    AddBorder(card, RadiusCard, GameConfig.BorderSoft);
                    AddPaperSheen(card);
                    Transform sheen = card.transform.Find("Sheen");
                    if (sheen != null) sheen.SetSiblingIndex(0);
                }
                LinkShadow(_runeCardButtons[i].gameObject, shadow);

                _runeCardIcons[i] = CreateIconSlot(_runeCardButtons[i].transform, $"RuneIcon{i}", 8f, 8f, 40f);

                _runeCardLabels[i].fontSize = 11;
                _runeCardLabels[i].verticalOverflow = VerticalWrapMode.Truncate;
                _runeCardLabels[i].alignment = TextAnchor.MiddleLeft;
                _runeCardLabels[i].rectTransform.offsetMin = new Vector2(54f, 4f);
                _runeCardLabels[i].rectTransform.offsetMax = new Vector2(-8f, -4f);

                _runeCardAccents[i] = CreateImage(_runeCardButtons[i].transform, $"RuneAccent{i}",
                    0f, 8f, 3f, 88f, GameConfig.Border);

                _runeCardButtons[i].onClick.AddListener(() => OnRuneCardClicked(captured));
            }

            _runeFooterText = CreateText(_runePanel, "RuneFooterText", "",
                20, 452, 470, 40, 11, GameConfig.TextMuted, TextAnchor.UpperLeft);

            Button reset = CreateButton(_runePanel, "RuneReset", "清空符文", 496, 456, 110, 30,
                GameConfig.CardDanger, out Text runeResetLabel);
            StyleBar(reset, runeResetLabel, "frame_pill", "glyph_close", 13f, "清空全部符文解锁，点数退回");
            reset.onClick.AddListener(OnRuneResetClicked);

            Button close = CreateButton(_runePanel, "RunePanelClose", "关闭", 698, 12, 90, 30,
                GameConfig.CardDanger, out Text runeCloseLabel);
            StyleBar(close, runeCloseLabel, "frame_pill", "glyph_close", 14f, "关闭符文面板");
            close.onClick.AddListener(OnRuneCloseClicked);

            _runePanel.gameObject.SetActive(false);
        }

        private void OnRuneCloseClicked()
        {
            GameAudio.PlayUiClose();
            _game.CloseRunePanel();
        }

        private void OnRuneResetClicked()
        {
            _game.ResetRunes();
            InvalidateRunePanel();
        }

        private void OnRuneCardClicked(int index)
        {
            if (_runeIds == null || index < 0 || index >= _runeIds.Length) return;
            PlayerData p = _game.Player;
            if (p == null) return;

            SkillId id = _runeIds[index];
            if (p.HasRuneUnlocked(id)) _game.SwitchRuneForm(id);
            else _game.UnlockRune(id);
            InvalidateRunePanel();
        }

        /// <summary>点完立刻重画，不用等下一帧的判脏。</summary>
        private void InvalidateRunePanel()
        {
            _runeSig = 0;
            RefreshRunePanel();
        }

        private void RefreshRunePanel()
        {
            bool open = _game.RunePanelOpen;
            _runePanel.gameObject.SetActive(open);
            if (!open) return;

            PlayerData p = _game.Player;
            if (p == null) return;

            // 面板内容全由这几项决定，签名不变就跳过重排。
            ulong sig = 1469598103934665603UL;
            sig = (sig ^ (ulong)p.runeUnlockMask) * 1099511628211UL;
            sig = (sig ^ (ulong)p.runeFormMask) * 1099511628211UL;
            sig = (sig ^ (ulong)p.paragonLevel) * 1099511628211UL;
            sig = (sig ^ (ulong)p.doctrine) * 1099511628211UL;
            if (sig == _runeSig && _runeSig != 0) return;
            _runeSig = sig;

            Color schoolColor = p.HasDoctrine ? SkillCatalog.SchoolColor(p.doctrine) : GameConfig.TextMuted;
            string schoolHex = GameConfig.InkHex(schoolColor);
            int totalPoints = p.RunePointsTotal;
            int points = p.RunePoints;

            _runePanelTitle.text = $"巅峰符文 — 可用 {points}/{totalPoints} 点";

            if (!p.HasDoctrine)
            {
                _runePointsBar.color = GameConfig.RowBg;
                _runePointsText.text = "尚未选择流派";
                _runeFooterText.text = "先在流派面板选择本世流派，之后才能使用符文。";
                for (int i = 0; i < _runeCardButtons.Length; i++)
                {
                    _runeCardButtons[i].interactable = false;
                    _runeCardButtons[i].image.color = Color.Lerp(GameConfig.CardBg, GameConfig.CardBgDim, 0.45f);
                    _runeCardLabels[i].text = "";
                    _runeIds[i] = SkillId.ThunderRush;
                }
                return;
            }

            string schoolName = SkillCatalog.SchoolName(p.doctrine);
            _runePointsBar.color = Color.Lerp(GameConfig.RowBg, schoolColor, 0.18f);
            _runePointsText.text =
                $"<color=#{schoolHex}>{schoolName}</color> · 可用 <color=#8A5A12>{points}</color> 点 · " +
                $"已解锁 {p.RuneUnlockCount} 项 · 巅峰点数 {totalPoints} · " +
                (totalPoints >= RuneCatalog.MaxPoints
                    ? "已达上限"
                    : $"下一档 P{(totalPoints + 1) * RuneCatalog.ParagonPerPoint}");

            SkillId[] kit = SkillCatalog.KitOf(p.doctrine);
            for (int i = 0; i < _runeCardButtons.Length && i < kit.Length; i++)
            {
                SkillId id = kit[i];
                _runeIds[i] = id;

                SkillDef def = SkillCatalog.Get(id);
                bool has = RuneCatalog.Has(id);
                bool unlocked = p.HasRuneUnlocked(id);
                bool affordable = has && points > 0;
                bool owned = p.IsSkillOwned(id);

                if (!has)
                {
                    _runeCardButtons[i].interactable = false;
                    _runeCardButtons[i].image.color = Color.Lerp(GameConfig.CardBg, GameConfig.CardBgDim, 0.45f);
                    _runeCardLabels[i].text =
                        $"<size=9><color=#6F6252>该技能暂无符文</color></size>\n" +
                        $"<size=14><b>{def.name}</b></size>";
                    SetIcon(_runeCardIcons[i], SkillIconKey((int)id));
                    if (_runeCardIcons[i] != null) _runeCardIcons[i].color = new Color(1f, 1f, 1f, 0.28f);
                    _runeCardAccents[i].color = GameConfig.Border;
                    _runeCardButtons[i].interactable = false;
                    continue;
                }

                RuneForm form = p.RuneFormOf(id);
                string formName = RuneCatalog.FormName(id, form);
                string formDesc = RuneCatalog.FormDesc(id, form);
                string otherName = RuneCatalog.FormName(id, form == RuneForm.A ? RuneForm.B : RuneForm.A);

                string state = unlocked
                    ? $"已解锁 · 当前「{formName}」"
                    : (affordable ? "可解锁 · 1 点" : "点数不足");
                // 羊皮纸底上不能再用「深底时代」的浅灰：锁定态只是「没那么亮」，不是「快看不见」。
                string stateHex = unlocked ? schoolHex : (affordable ? "8A5A12" : "6F6252");
                // 解锁只看符文点数，但没学的技能解锁了也用不上，标出来免得玩家白花点。
                string learnNote = owned ? "" : " · 未学习";
                string nameColor = unlocked ? GameConfig.InkHex(def.color) : "4A4238";

                _runeCardLabels[i].text =
                    $"<size=9><color=#{stateHex}>{SkillCatalog.SchoolName(def.school)} · {state}{learnNote}</color></size>\n" +
                    $"<size=14><b><color=#{nameColor}>{def.name}</color></b></size>\n" +
                    $"<size=10><color=#4A4238>{formName}：{formDesc}</color></size>";
                // 卡内三行都由富文本 <color> 定色，基色留白即可（实测锁定态正文 #4A4238 落在
                // 71/64/54，已经是设计色，不需要再靠基色补对比）。
                _runeCardLabels[i].color = Color.white;

                SetIcon(_runeCardIcons[i], SkillIconKey((int)id));
                if (_runeCardIcons[i] != null)
                    _runeCardIcons[i].color = unlocked || affordable
                        ? Color.white
                        : new Color(1f, 1f, 1f, 0.28f);

                _runeCardButtons[i].interactable = unlocked || affordable;
                // 锁定态用「压暗的纸」而不是 CardBgDim：后者在羊皮纸面板上会糊成一片浅灰，
                // 深色正文压在它上面几乎读不出来（截图核对过）。
                _runeCardButtons[i].image.color = unlocked
                    ? Color.Lerp(GameConfig.RowBg, schoolColor, 0.06f)
                    : (affordable
                        ? Color.Lerp(GameConfig.RowBg, schoolColor, 0.12f)
                        : Color.Lerp(GameConfig.CardBg, GameConfig.CardBgDim, 0.45f));
                _runeCardAccents[i].color = unlocked ? GameConfig.LogGold : GameConfig.Border;

                string tip = $"{def.name}\n" +
                             $"甲 · {RuneCatalog.FormName(id, RuneForm.A)}：{RuneCatalog.FormDesc(id, RuneForm.A)}\n" +
                             $"乙 · {RuneCatalog.FormName(id, RuneForm.B)}：{RuneCatalog.FormDesc(id, RuneForm.B)}\n" +
                             (unlocked
                                 ? $"当前「{formName}」，点击切到「{otherName}」（免费）"
                                 : $"{state}：点击花 1 点解锁，之后可免费切换");
                if (!owned) tip += "\n注意：该技能尚未学习，解锁后要先在技能面板学会才能生效。";
                SetTooltip(_runeCardButtons[i], tip);
            }

            _runeFooterText.text =
                $"符文点由巅峰等级派生：每 {RuneCatalog.ParagonPerPoint} 巅峰 1 点，P{RuneCatalog.MaxPoints * RuneCatalog.ParagonPerPoint} 满 {RuneCatalog.MaxPoints} 点；转生清巅峰但保留解锁。\n" +
                $"解锁数 {p.RuneUnlockCount} · 剩余 {points} 点 · 两个形态互斥，同一时刻只有一个生效。";
        }
    }
}

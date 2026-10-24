using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.Events;

namespace AutoBattle
{
    public partial class GameUI : MonoBehaviour
    {
        // 选关面板：章节/关卡浏览与进度总览
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
                GameConfig.CardDanger, out Text stageCloseLabel);
            StyleBar(close, stageCloseLabel, "frame_pill", "glyph_close", 14f, "关闭选关");
            close.onClick.AddListener(OnStagePanelCloseClicked);

            _stagePanel.gameObject.SetActive(false);
        }

        private void OnStageButtonClicked()
        {
            bool open = _game.StagePanelOpen;
            _game.ToggleStagePanel();
            if (open) GameAudio.PlayUiClose();
            else GameAudio.PlayUiOpen();
        }

        private void OnStagePanelCloseClicked()
        {
            if (_game != null) _game.CloseStagePanel();
            GameAudio.PlayUiClose();
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
                    _stageButtonLabel.text = $"第 {floor} 层";
                }
                else
                {
                    _stageButtonLabel.text = current != null ? $"关卡 {current.Code}" : "关卡 —";
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

    }
}

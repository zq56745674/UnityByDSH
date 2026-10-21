using UnityEngine;
using UnityEngine.UI;

namespace AutoBattle
{
    /// <summary>
    /// 无尽模式的全部界面（docs/endless-mode.md 第 8 节）：左栏入口、无尽面板、本局结果面板。
    /// 战斗逻辑在 GameController.Endless.cs，这里只负责显示与把点击转给控制器。
    /// </summary>
    public partial class GameUI
    {
        // ---- 左栏入口 ----
        private Button _endlessButton;
        private Text _endlessButtonLabel;
        private Image _endlessButtonIcon;

        // ---- 无尽面板 ----
        private RectTransform _endlessPanel;
        private Text _endlessTitle;
        private Text _endlessBestText;
        private Text _endlessGlobalText;
        private Text _endlessStartText;
        private Text _endlessRulesText;
        private Text _endlessHintText;
        private Button _endlessStartButton;
        private Text _endlessStartLabel;
        private Button _endlessFreshButton;
        private Text _endlessFreshLabel;
        private Button _endlessRetreatButton;
        private Text _endlessRetreatLabel;
        private Button _endlessReturnButton;

        /// <summary>撤退二次确认的截止时间（Time.unscaledTime）。</summary>
        private float _endlessRetreatConfirmUntil;

        // ---- 本局结果面板 ----
        private RectTransform _endlessResultPanel;
        private Text _endlessResultTitle;
        private Text _endlessResultBody;
        private Text _endlessRetryLabel;
        private Text _endlessResultReturnLabel;

        /// <summary>
        /// 建无尽面板与结果面板。调用点在 BuildComparePanel 之后、BuildGuide 之前：
        /// 结果面板要压在所有常驻浮层之上，又不能盖住横幅（横幅永远最后建）。
        /// </summary>
        private void BuildEndlessPanel(Transform root)
        {
            _endlessPanel = CreatePanel(root, "EndlessPanel", 300, 142, 680, 436,
                GameConfig.OverlayBg, true);

            CreateIcon(_endlessPanel, "ui_endless", 20, 14, 24);
            _endlessTitle = CreateText(_endlessPanel, "EndlessTitle", "无尽模式", 50, 12, 450, 30,
                FontTitle, GameConfig.TextTitle, TextAnchor.MiddleLeft, FontStyle.Bold);

            Button close = CreateButton(_endlessPanel, "EndlessClose", "关闭", 570, 12, 90, 30,
                GameConfig.CardBtn, out Text closeLabel);
            StyleBar(close, closeLabel, "frame_pill", "glyph_close", 14f, "关闭无尽模式");
            close.onClick.AddListener(OnEndlessCloseClicked);

            _endlessBestText = CreateText(_endlessPanel, "EndlessBest", "", 20, 52, 310, 26,
                FontBody + 1, GameConfig.TextInfo, TextAnchor.MiddleLeft);
            _endlessGlobalText = CreateText(_endlessPanel, "EndlessGlobal", "", 350, 52, 310, 26,
                FontBody + 1, GameConfig.TextInfo, TextAnchor.MiddleLeft);
            _endlessStartText = CreateText(_endlessPanel, "EndlessStartInfo", "", 20, 86, 640, 24,
                FontSmall + 1, GameConfig.TextMuted, TextAnchor.MiddleLeft);

            CreateSectionTitle(_endlessPanel, "规则", 20, 118, 200, FontSection);

            _endlessRulesText = CreateText(_endlessPanel, "EndlessRules", "", 20, 146, 640, 126,
                FontSmall + 1, GameConfig.TextInfo, TextAnchor.UpperLeft);
            _endlessRulesText.lineSpacing = 1.18f;

            CreateText(_endlessPanel, "EndlessProgressHint", "本局进度", 20, 276, 200, 22,
                FontTiny + 2, GameConfig.TextMuted, TextAnchor.MiddleLeft);

            // 开始 / 继续按钮：未开局开始挑战，局内变成继续战斗。
            _endlessStartButton = CreateButton(_endlessPanel, "EndlessStart", "开始挑战", 20, 328, 200, 40,
                GameConfig.BtnGold, out _endlessStartLabel);
            StyleBar(_endlessStartButton, _endlessStartLabel, "frame_bar_round", "glyph_play", 21f, "开始 / 继续无尽挑战");
            _endlessStartButton.onClick.AddListener(OnEndlessStartClicked);

            // 从第 1 层开始：只在起始层 > 1 时显示。
            _endlessFreshButton = CreateButton(_endlessPanel, "EndlessFresh", "从第 1 层开始", 232, 328, 200, 40,
                GameConfig.BtnHeader, out _endlessFreshLabel);
            StyleBar(_endlessFreshButton, _endlessFreshLabel, "frame_bar_notched", "glyph_reset", 20f, "从第 1 层重新开始");
            _endlessFreshButton.onClick.AddListener(OnEndlessFreshClicked);

            // 撤退：局内才显示；第一次点击进入 3 秒确认。
            _endlessRetreatButton = CreateButton(_endlessPanel, "EndlessRetreat", "撤退", 232, 328, 140, 40,
                GameConfig.BtnDanger, out _endlessRetreatLabel);
            StyleBar(_endlessRetreatButton, _endlessRetreatLabel, "frame_pill", "glyph_back", 19f, "撤退并结束本局");
            _endlessRetreatButton.onClick.AddListener(OnEndlessRetreatClicked);

            // 返回战役：只在未开局的可浏览状态显示；局内要先撤退。
            _endlessReturnButton = CreateButton(_endlessPanel, "EndlessReturn", "返回战役", 444, 328, 120, 40,
                GameConfig.BtnHeader, out Text returnLabel);
            StyleBar(_endlessReturnButton, returnLabel, "frame_pill", "glyph_back", 19f, "返回战役");
            _endlessReturnButton.onClick.AddListener(OnEndlessReturnClicked);

            _endlessHintText = CreateText(_endlessPanel, "EndlessHint", "", 20, 382, 640, 38,
                FontTiny + 2, GameConfig.TextMuted, TextAnchor.UpperLeft);

            _endlessPanel.gameObject.SetActive(false);

            // ---- 结果面板 ----
            _endlessResultPanel = CreatePanel(root, "EndlessResultPanel", 360, 210, 560, 300,
                GameConfig.OverlayBg, true);

            CreateText(_endlessResultPanel, "EndlessResultCaption", "本局结束", 20, 14, 320, 24,
                FontSmall, GameConfig.TextMuted, TextAnchor.MiddleLeft);

            _endlessResultTitle = CreateText(_endlessResultPanel, "EndlessResultTitle", "", 20, 44, 520, 40,
                FontTitle, GameConfig.TextTitle, TextAnchor.MiddleLeft, FontStyle.Bold);

            _endlessResultBody = CreateText(_endlessResultPanel, "EndlessResultBody", "", 20, 94, 520, 124,
                FontSmall + 1, GameConfig.TextInfo, TextAnchor.UpperLeft);
            _endlessResultBody.lineSpacing = 1.2f;

            Button retry = CreateButton(_endlessResultPanel, "EndlessRetry", "再次挑战", 20, 238, 260, 42,
                GameConfig.BtnGold, out _endlessRetryLabel);
            StyleBar(retry, _endlessRetryLabel, "frame_bar_round", "glyph_reset", 22f, "再次挑战");
            retry.onClick.AddListener(OnEndlessRetryClicked);

            Button back = CreateButton(_endlessResultPanel, "EndlessResultReturn", "返回战役", 296, 238, 180, 42,
                GameConfig.BtnHeader, out _endlessResultReturnLabel);
            StyleBar(back, _endlessResultReturnLabel, "frame_pill", "glyph_back", 22f, "返回战役");
            back.onClick.AddListener(OnEndlessResultReturnClicked);

            _endlessResultPanel.gameObject.SetActive(false);
        }

        /// <summary>每帧刷新入口、面板与结果面板。文本量很小，直接按当前状态写。</summary>
        private void RefreshEndlessPanel()
        {
            if (_endlessPanel == null || _game == null || _game.Player == null) return;

            bool inRun = _game.EndlessActive;
            int best = _game.EndlessBestFloor;
            int globalBest = EndlessRecord.GlobalBest;

            if (_endlessButtonIcon != null)
            {
                // 图标即按钮：未解锁染灰，局内挑战染金，其余保持原色。
                _endlessButtonIcon.color = !inRun && !_game.EndlessUnlocked
                    ? GameConfig.TextDisabled
                    : (inRun ? GameConfig.LogGold : Color.white);
            }

            bool open = _game.EndlessPanelOpen;
            _endlessPanel.gameObject.SetActive(open);
            if (!open)
            {
                RefreshEndlessResult();
                return;
            }

            int startFloor = inRun && _game.EndlessRun != null
                ? _game.EndlessRun.startFloor
                : _game.EndlessSuggestedStartFloor;

            _endlessTitle.text = inRun && _game.EndlessRun != null
                ? $"无尽模式 — 第 {_game.EndlessRun.floor} 层"
                : "无尽模式";

            _endlessBestText.text = best > 0
                ? $"<color=#8C99B0>本档最高</color>  <b>第 {best} 层</b>"
                : "<color=#8C99B0>本档最高</color>  <b>—</b>";
            _endlessGlobalText.text = globalBest > 0
                ? $"<color=#8C99B0>全局最高</color>  <b>第 {globalBest} 层</b>"
                : "<color=#8C99B0>全局最高</color>  <b>—</b>";

            if (inRun && _game.EndlessRun != null)
            {
                EndlessRunState run = _game.EndlessRun;
                int target = EndlessCatalog.KillTargetOf(run.floor);
                _endlessStartText.text =
                    $"本局：第 {run.floor} 层   击杀 {run.killCount}/{target}   已清 {run.clearedThisRun} 层" +
                    $"   起始 第 {run.startFloor} 层";
            }
            else
            {
                if (best <= 0)
                {
                    _endlessStartText.text = "起始层：第 1 层（还没有纪录，从底层开始）";
                }
                else if (startFloor <= 1)
                {
                    _endlessStartText.text = "起始层：第 1 层（本世重通 11-10 后恢复检查点）";
                }
                else
                {
                    _endlessStartText.text = $"起始层：第 {startFloor} 层（本档最高 − {GameConfig.EndlessCheckpointOffset}）";
                }
            }

            _endlessRulesText.text =
                $"· 每层清完立刻进入下一层；每 {Mathf.Max(1, GameConfig.EndlessBossEvery)} 层一个 BOSS。\n" +
                "· 死亡或撤退结束本局；最高层只记「已清除」的层。\n" +
                $"· 掉落等级封顶 {Mathf.Max(1, GameConfig.EndlessDropLevelCap)}；层通过给金币，新纪录额外给首通金币。";

            bool unlocked = _game.EndlessUnlocked;
            bool canEnter = _game.CanEnterEndless;
            _endlessStartButton.gameObject.SetActive(true);
            _endlessStartButton.interactable = inRun || canEnter;
            _endlessStartLabel.text = inRun ? "继续战斗" : "开始挑战";
            _endlessStartLabel.color = inRun || canEnter ? Color.white : GameConfig.TextDisabled;

            _endlessFreshButton.gameObject.SetActive(!inRun && canEnter && startFloor > 1);
            _endlessFreshButton.interactable = canEnter;

            _endlessRetreatButton.gameObject.SetActive(inRun);
            float now = Time.unscaledTime;
            if (inRun && _endlessRetreatConfirmUntil > now)
            {
                _endlessRetreatLabel.text = $"确认撤退 {_endlessRetreatConfirmUntil - now:0.0}s";
            }
            else
            {
                _endlessRetreatConfirmUntil = 0f;
                _endlessRetreatLabel.text = "撤退";
            }

            _endlessReturnButton.gameObject.SetActive(!inRun);

            _endlessHintText.text = !unlocked
                ? "通关第 11 章 11-10 后解锁无尽模式。"
                : (!inRun && !canEnter)
                    ? $"需要等级 {GameConfig.MaxLevel} 才能进入无尽模式（当前 Lv.{_game.Player.level}）。"
                    : inRun
                        ? "退出游戏、切档或重置会结束本局；已经清完的层与首通金币会保留。"
                        : "退出游戏会放弃未完成的一局，但已清层的纪录会保留。";

            RefreshEndlessResult();
        }

        /// <summary>结果面板：只有阵亡/撤退会打开，必须选「再次挑战」或「返回战役」。</summary>
        private void RefreshEndlessResult()
        {
            if (_endlessResultPanel == null || _game == null) return;

            bool open = _game.EndlessResultOpen && _game.EndlessRun != null;
            _endlessResultPanel.gameObject.SetActive(open);
            if (!open) return;

            EndlessRunState run = _game.EndlessRun;
            int best = _game.EndlessBestFloor;
            int globalBest = EndlessRecord.GlobalBest;

            _endlessResultTitle.text = run.endReason == EndlessEndReason.Retreat
                ? $"已撤退（第 {run.reachedFloor} 层）"
                : $"第 {run.reachedFloor} 层阵亡";

            string cleared = run.clearedThisRun > 0
                ? $"本局清除 <color=#9FD8A0>{run.clearedThisRun}</color> 层"
                : "尚未清除任何层";
            _endlessResultBody.text =
                $"{cleared} · 本档最高 <color=#FFE08A>{best}</color> 层 · 全局最高 <color=#FFE08A>{globalBest}</color> 层\n" +
                $"起始层 第 {run.startFloor} 层 · 本局击杀 {run.runKills} 只";

            int retryFloor = EndlessCatalog.StartFloorFor(best);
            _endlessRetryLabel.text = $"再次挑战（第 {retryFloor} 层）";
            _endlessResultReturnLabel.text = "返回战役";
        }

        // ==================== 点击 ====================

        private void OnEndlessButtonClicked()
        {
            if (_game == null) return;
            // 撤退确认只属于「当前打开的这一轮面板」：关掉再打开不该还带着倒计时。
            _endlessRetreatConfirmUntil = 0f;
            bool open = _game.EndlessPanelOpen;
            _game.ToggleEndlessPanel();
            if (open) GameAudio.PlayUiClose();
            else GameAudio.PlayUiOpen();
            if (_game.EndlessPanelOpen && !_game.CanEnterEndless)
            {
                _game.AddLog("🔒 通关第 11 章 11-10 后解锁无尽模式", GameConfig.LogWarn);
            }
        }

        private void OnEndlessCloseClicked()
        {
            _endlessRetreatConfirmUntil = 0f;
            if (_game != null) _game.CloseEndlessPanel();
            GameAudio.PlayUiClose();
        }

        private void OnEndlessStartClicked()
        {
            if (_game == null) return;
            _endlessRetreatConfirmUntil = 0f;
            if (_game.EndlessActive)
            {
                _game.CloseEndlessPanel();
                return;
            }

            _game.StartEndlessRun(_game.EndlessSuggestedStartFloor);
        }

        private void OnEndlessFreshClicked()
        {
            if (_game != null) _game.StartEndlessRun(1);
        }

        private void OnEndlessRetreatClicked()
        {
            if (_game == null || !_game.EndlessActive) return;

            float now = Time.unscaledTime;
            if (_endlessRetreatConfirmUntil <= now)
            {
                // 第一次只上膛：按钮文字由 RefreshEndlessPanel 换成倒计时，3 秒内再点才真的撤。
                _endlessRetreatConfirmUntil = now + ConfirmWindow;
                return;
            }

            _endlessRetreatConfirmUntil = 0f;
            _game.RetreatEndlessRun();
        }

        private void OnEndlessReturnClicked()
        {
            if (_game == null) return;
            if (_game.EndlessActive || _game.CurrentState == GameController.State.Ended)
            {
                _game.ReturnToCampaign();
            }
            else
            {
                _game.CloseEndlessPanel();
            }
        }

        private void OnEndlessRetryClicked()
        {
            if (_game != null) _game.RetryEndlessRun();
        }

        private void OnEndlessResultReturnClicked()
        {
            if (_game != null) _game.ReturnToCampaign();
        }
    }
}
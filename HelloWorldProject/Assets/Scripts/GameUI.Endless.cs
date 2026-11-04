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

        // ---- 规则 / 历史榜 分页（同一块 640×160 区域，二选一显示） ----
        private Button _endlessRulesTab;
        private Text _endlessRulesTabLabel;
        private Button _endlessHistoryTab;
        private Text _endlessHistoryTabLabel;
        private Text _endlessHistoryHint;
        private Text _endlessHistoryEmpty;
        private Image[] _endlessHistoryRows;
        private Text[] _endlessHistoryRowTexts;

        /// <summary>清空本档历史榜：与撤退/删档同一套两次点击确认。</summary>
        private Button _endlessClearButton;
        private Text _endlessClearLabel;
        private float _endlessClearConfirmUntil;

        /// <summary>内容区当前显示历史榜还是规则。只影响显示，不参与存档。</summary>
        private bool _endlessHistoryOpen;

        // ---- 契约页（B 深层契约，docs/gameplay-expansion.md 第 5 节） ----
        private Button _endlessContractTab;
        private Text _endlessContractTabLabel;
        private RectTransform _endlessContractRoot;
        private Text _endlessContractSummary;
        private Image[] _endlessContractCells;
        private Button[,] _endlessContractTierButtons;
        private Text[,] _endlessContractTierLabels;
        private Image[,] _endlessContractTierMarks;

        /// <summary>内容区停在契约页时为 true（与 _endlessHistoryOpen 互斥）。</summary>
        private bool _endlessContractOpen;

        // ---- 本局结果面板（结算卡片） ----
        private RectTransform _endlessResultPanel;
        private Text _endlessResultTitle;
        private Text _endlessResultRank;
        private Image _endlessResultTagBg;
        private Text _endlessResultTag;
        private Text[] _endlessResultStats;
        private Text _endlessResultCompare;
        private Text _endlessResultReward;
        private Text _endlessRetryLabel;
        private Text _endlessResultReturnLabel;

        /// <summary>一条契约最多三档（决绝只有一档），按钮数组按三列开。</summary>
        private const int ContractTierSlots = 3;

        private static string ContractTierMark(int tier)
        {
            switch (tier)
            {
                case 1: return "Ⅰ";
                case 2: return "Ⅱ";
                case 3: return "Ⅲ";
                default: return tier.ToString();
            }
        }

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
            AttachTooltip(_endlessStartText.rectTransform,
                "起始层 = 本档最高层 − " + GameConfig.EndlessCheckpointOffset +
                "（成就点可再兑换 +1~+3 层，但不会超过本档最高层），最低从第 1 层开始。\n" +
                "例：本档最高 14 层 → 从第 10 层开始；成就 +3 层 → 第 13 层（仍低于 14）。\n" +
                "转生后本世要先重通 11-10 才恢复检查点；中途退出不计成绩，但已清层数会保留。");

            // 规则 / 历史榜 两个页签：共用下面 20..300 的内容区，省得再开一层浮层
            // （面板之间是 CloseAllPanels 互斥的，单开一个「历史榜」面板反而回不到无尽面板）。
            // 页签统一用生图底框（frame_tab），与成就/行商的页签同一套皮革+黄铜，
            // 选中态保持原色、未选中压暗，不再用 RowBg 那块平涂圆角矩形。
            _endlessRulesTab = CreateButton(_endlessPanel, "EndlessTabRules", "规则", 20, 112, 96, 28,
                GameConfig.BtnHeader, out _endlessRulesTabLabel);
            StyleTab(_endlessRulesTab, _endlessRulesTabLabel, "frame_tab", 12);
            _endlessRulesTab.onClick.AddListener(OnEndlessRulesTabClicked);

            _endlessHistoryTab = CreateButton(_endlessPanel, "EndlessTabHistory", "历史榜", 124, 112, 96, 28,
                GameConfig.BtnHeader, out _endlessHistoryTabLabel);
            StyleTab(_endlessHistoryTab, _endlessHistoryTabLabel, "frame_tab", 12);
            _endlessHistoryTab.onClick.AddListener(OnEndlessHistoryTabClicked);

            _endlessContractTab = CreateButton(_endlessPanel, "EndlessTabContract", "契约", 228, 112, 96, 28,
                GameConfig.BtnHeader, out _endlessContractTabLabel);
            StyleTab(_endlessContractTab, _endlessContractTabLabel, "frame_tab", 12);
            _endlessContractTab.onClick.AddListener(OnEndlessContractTabClicked);

            _endlessRulesText = CreateText(_endlessPanel, "EndlessRules", "", 20, 146, 640, 150,
                FontSmall + 1, GameConfig.TextInfo, TextAnchor.UpperLeft);
            _endlessRulesText.lineSpacing = 1.18f;

            // ---- 历史榜（Top-N，最多 5 行） ----
            _endlessHistoryHint = CreateText(_endlessPanel, "EndlessHistoryHint",
                "本档历史最好 5 局 · 按契约分 → 到达层数 → 本局清除排序", 20, 146, 640, 18,
                FontTiny + 1, GameConfig.TextMuted, TextAnchor.MiddleLeft);

            _endlessHistoryEmpty = CreateText(_endlessPanel, "EndlessHistoryEmpty",
                "还没有完成的无尽局。\n阵亡或撤退结束一局后，这里会记下成绩（中途退出/换档不算）。",
                20, 176, 640, 60, FontSmall + 1, GameConfig.TextMuted, TextAnchor.UpperLeft);
            _endlessHistoryEmpty.lineSpacing = 1.2f;

            _endlessHistoryRows = new Image[EndlessHistory.Capacity];
            _endlessHistoryRowTexts = new Text[EndlessHistory.Capacity];
            for (int i = 0; i < _endlessHistoryRows.Length; i++)
            {
                Image row = CreateCard(_endlessPanel, "EndlessHistoryRow" + i, 20, 170 + i * 26, 640, 24,
                    GameConfig.CardBgDim, RadiusButton, GameConfig.BorderSoft);
                _endlessHistoryRows[i] = row;
                _endlessHistoryRowTexts[i] = CreateText(row.transform, "RowText", "", 12, 0, 616, 24,
                    FontSmall + 1, GameConfig.TextInfo, TextAnchor.MiddleLeft);
                row.gameObject.SetActive(false);
            }

            // 清空榜单：只在历史榜有内容时出现；点两次才真的清（与撤退/删档同一套确认）。
            _endlessClearButton = CreateButton(_endlessPanel, "EndlessClear", "清空榜单", 528, 112, 132, 28,
                GameConfig.BtnDanger, out _endlessClearLabel);
            StyleBar(_endlessClearButton, _endlessClearLabel, "frame_pill", "glyph_delete", 15f,
                "清空本档历史榜（不影响最高层记录）", FontSmall + 1);
            _endlessClearButton.onClick.AddListener(OnEndlessClearHistoryClicked);
            _endlessClearButton.gameObject.SetActive(false);

            // ---- 契约页（B 深层契约，docs/gameplay-expansion.md 第 5 节） ----
            // 八条契约 2 列 × 4 行一次看全；每格右侧是 Ⅰ/Ⅱ/Ⅲ 三档小钮（决绝只有一档）。
            // 只改本局规则，所以选择结果要等开局才写进 EndlessRunState，这里只画勾选态。
            _endlessContractRoot = CreateRect(_endlessPanel, "EndlessContracts", 20, 146, 640, 176);
            _endlessContractCells = new Image[ContractCatalog.All.Length];
            _endlessContractTierButtons = new Button[ContractCatalog.All.Length, ContractTierSlots];
            _endlessContractTierLabels = new Text[ContractCatalog.All.Length, ContractTierSlots];
            _endlessContractTierMarks = new Image[ContractCatalog.All.Length, ContractTierSlots];

            for (int c = 0; c < ContractCatalog.All.Length; c++)
            {
                ContractDef def = ContractCatalog.All[c];
                if (def == null) continue;

                Image cell = CreateCard(_endlessContractRoot, "ContractCell" + c,
                    (c % 2) * 326f, (c / 2) * 40f, 312, 36,
                    GameConfig.CardBgDim, RadiusButton, GameConfig.BorderSoft);
                _endlessContractCells[c] = cell;

                CreateText(cell.transform, "ContractName", def.name, 10, 0, 152, 18,
                    FontTiny + 1, GameConfig.TextTitle, TextAnchor.MiddleLeft, FontStyle.Bold);
                CreateText(cell.transform, "ContractEffect", def.shortText, 10, 18, 154, 16,
                    FontTiny - 1, GameConfig.TextMuted, TextAnchor.MiddleLeft);

                int tiers = Mathf.Clamp(def.TierCount, 1, ContractTierSlots);
                for (int t = 1; t <= tiers; t++)
                {
                    bool single = tiers <= 1;
                    float bx = single ? 168f : 168f + (t - 1) * 48f;
                    float bw = single ? 132f : 44f;
                    Button tierButton = CreateButton(cell.transform, "ContractTier" + c + "_" + t,
                        single ? "本局启用" : ContractTierMark(t), bx, 7, bw, 22,
                        GameConfig.BtnHeader, out Text tierLabel);
                    StyleTab(tierButton, tierLabel, "frame_tab", 11);

                    // 选中标记：按钮底沿一条金线。只靠亮度差在小尺寸下看不出来。
                    Image mark = CreateImage(tierButton.transform, "TierMark", 4, bw > 60f ? 18 : 17, bw - 8, 2,
                        GameConfig.Border);
                    mark.gameObject.SetActive(false);
                    _endlessContractTierMarks[c, t - 1] = mark;

                    ContractKind kind = def.kind;
                    int tierIndex = t;
                    tierButton.onClick.AddListener(() => _game?.ToggleContractTier(kind, tierIndex));
                    AttachTooltip(tierButton,
                        $"{def.name} {ContractTierMark(tierIndex)}：{def.tierText[tierIndex - 1]}" +
                        $"\n契约分系数 +{def.coef[tierIndex - 1] * 100f:0}%");

                    _endlessContractTierButtons[c, t - 1] = tierButton;
                    _endlessContractTierLabels[c, t - 1] = tierLabel;
                }
            }

            // 底行摘要：已选几条、本局契约分倍率、累计契约分与下一个里程碑。
            _endlessContractSummary = CreateText(_endlessContractRoot, "ContractSummary", "",
                0, 158, 640, 18, FontTiny, GameConfig.TextMuted, TextAnchor.MiddleLeft);
            _endlessContractRoot.gameObject.SetActive(false);

            // 开始 / 继续按钮：未开局开始挑战，局内变成继续战斗。
            _endlessStartButton = CreateButton(_endlessPanel, "EndlessStart", "开始挑战", 20, 334, 200, 40,
                GameConfig.BtnGold, out _endlessStartLabel);
            StyleBar(_endlessStartButton, _endlessStartLabel, "frame_bar_round", "glyph_play", 21f, "开始 / 继续无尽挑战");
            _endlessStartButton.onClick.AddListener(OnEndlessStartClicked);

            // 从第 1 层开始：只在起始层 > 1 时显示。
            _endlessFreshButton = CreateButton(_endlessPanel, "EndlessFresh", "从第 1 层开始", 232, 334, 200, 40,
                GameConfig.BtnHeader, out _endlessFreshLabel);
            StyleBar(_endlessFreshButton, _endlessFreshLabel, "frame_bar_notched", "glyph_reset", 20f, "从第 1 层重新开始");
            _endlessFreshButton.onClick.AddListener(OnEndlessFreshClicked);

            // 撤退：局内才显示；第一次点击进入 3 秒确认。
            _endlessRetreatButton = CreateButton(_endlessPanel, "EndlessRetreat", "撤退", 232, 334, 140, 40,
                GameConfig.BtnDanger, out _endlessRetreatLabel);
            StyleBar(_endlessRetreatButton, _endlessRetreatLabel, "frame_pill", "glyph_back", 19f, "撤退并结束本局");
            _endlessRetreatButton.onClick.AddListener(OnEndlessRetreatClicked);

            // 返回战役：只在未开局的可浏览状态显示；局内要先撤退。
            _endlessReturnButton = CreateButton(_endlessPanel, "EndlessReturn", "返回战役", 444, 334, 120, 40,
                GameConfig.BtnHeader, out Text returnLabel);
            StyleBar(_endlessReturnButton, returnLabel, "frame_pill", "glyph_back", 19f, "返回战役");
            _endlessReturnButton.onClick.AddListener(OnEndlessReturnClicked);

            _endlessHintText = CreateText(_endlessPanel, "EndlessHint", "", 20, 386, 640, 38,
                FontTiny + 2, GameConfig.TextMuted, TextAnchor.UpperLeft);

            _endlessPanel.gameObject.SetActive(false);

            // ---- 结果面板（结算卡片） ----
            // 一局打完只会停在这一个界面上，所以它得有「结算」的分量：
            // 大号到达层数 + 结局标签 + 三格数据 + 本档/全局对照，往下给 A 遗物 / B 契约留位置。
            _endlessResultPanel = CreatePanel(root, "EndlessResultPanel", 300, 150, 680, 436,
                GameConfig.OverlayBg, true);

            CreateIcon(_endlessResultPanel, "ui_endless", 24, 16, 24);
            CreateText(_endlessResultPanel, "EndlessResultCaption", "无尽结算", 56, 16, 300, 24,
                FontSmall + 1, GameConfig.TextMuted, TextAnchor.MiddleLeft);

            // 结局标签：阵亡 / 撤退。深皮革底 + 亮金字，和左栏名牌同一套配方。
            _endlessResultTagBg = CreateCard(_endlessResultPanel, "EndlessResultTag", 552, 14, 104, 28,
                GameConfig.SlotBg, RadiusButton, GameConfig.Border);
            _endlessResultTag = CreateText(_endlessResultTagBg.transform, "EndlessResultTagText", "阵亡",
                0, 0, 104, 28, FontSmall + 1, GameConfig.BtnTextGold, TextAnchor.MiddleCenter, FontStyle.Bold);
            Stretch(_endlessResultTag.rectTransform);

            CreateText(_endlessResultPanel, "EndlessResultFloorLabel", "到达层数", 24, 42, 200, 18,
                FontTiny + 2, GameConfig.TextMuted, TextAnchor.MiddleLeft);

            _endlessResultTitle = CreateText(_endlessResultPanel, "EndlessResultTitle", "", 24, 58, 400, 50,
                34, GameConfig.TextTitle, TextAnchor.MiddleLeft, FontStyle.Bold);

            _endlessResultRank = CreateText(_endlessResultPanel, "EndlessResultRank", "", 404, 62, 252, 28,
                FontSmall + 2, GameConfig.TextGoldDeep, TextAnchor.MiddleRight, FontStyle.Bold);

            CreateImage(_endlessResultPanel, "EndlessResultDivider", 24, 116, 632, 2, GameConfig.Divider);

            // 三格数据：用时 / 击杀 / 清除层数。放大字号的数值是这张卡的主角。
            string[] statNames = { "用时", "本局击杀", "清除层数" };
            float[] statX = { 24f, 240f, 456f };
            _endlessResultStats = new Text[statNames.Length];
            for (int i = 0; i < statNames.Length; i++)
            {
                Image statCard = CreateCard(_endlessResultPanel, "EndlessResultStat" + i, statX[i], 132, 200, 88,
                    GameConfig.CardBg, RadiusCard, GameConfig.BorderSoft);
                CreateText(statCard.transform, "StatLabel", statNames[i], 16, 10, 168, 20,
                    FontTiny + 2, GameConfig.TextMuted, TextAnchor.MiddleLeft);
                _endlessResultStats[i] = CreateText(statCard.transform, "StatValue", "—", 16, 32, 168, 42,
                    FontTitle + 4, GameConfig.TextTitle, TextAnchor.MiddleLeft, FontStyle.Bold);
            }

            _endlessResultCompare = CreateText(_endlessResultPanel, "EndlessResultCompare", "", 24, 232, 632, 22,
                FontTiny + 2, GameConfig.TextMuted, TextAnchor.MiddleLeft);

            // 遗物 / 契约的奖励位：三行分别是遗物清单、本局契约与契约分、下一个里程碑。
            Image rewardCard = CreateCard(_endlessResultPanel, "EndlessResultReward", 24, 264, 632, 96,
                GameConfig.InsetBg, RadiusCard, GameConfig.BorderSoft);
            _endlessResultReward = CreateText(rewardCard.transform, "RewardText", "", 16, 7, 600, 82,
                FontTiny + 1, GameConfig.TextNormal, TextAnchor.UpperLeft);
            _endlessResultReward.lineSpacing = 1.2f;

            Button retry = CreateButton(_endlessResultPanel, "EndlessRetry", "再次挑战", 24, 368, 340, 44,
                GameConfig.BtnGold, out _endlessRetryLabel);
            StyleBar(retry, _endlessRetryLabel, "frame_bar_round", "glyph_reset", 22f, "再次挑战");
            retry.onClick.AddListener(OnEndlessRetryClicked);

            Button back = CreateButton(_endlessResultPanel, "EndlessResultReturn", "返回战役", 380, 368, 276, 44,
                GameConfig.BtnHeader, out _endlessResultReturnLabel);
            StyleBar(back, _endlessResultReturnLabel, "frame_pill", "glyph_back", 22f, "返回战役");
            back.onClick.AddListener(OnEndlessResultReturnClicked);

            _endlessResultPanel.gameObject.SetActive(false);

            // 遗物三选一最后建：它要压在这两张卡之上（同层级的后建者在上）。
            BuildRelicPanel(root);
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
                ? $"<color=#6F6252>本档最高</color>  <b>第 {best} 层</b>"
                : "<color=#6F6252>本档最高</color>  <b>—</b>";
            _endlessGlobalText.text = globalBest > 0
                ? $"<color=#6F6252>全局最高</color>  <b>第 {globalBest} 层</b>"
                : "<color=#6F6252>全局最高</color>  <b>—</b>";

            if (inRun && _game.EndlessRun != null)
            {
                EndlessRunState run = _game.EndlessRun;
                int target = run.killTarget;
                string contractNote = run.contractCount > 0
                    ? $"   契约 {ContractCatalog.Describe(run.contractTiers)}"
                    : string.Empty;
                _endlessStartText.text =
                    $"本局：第 {run.floor} 层   击杀 {run.killCount}/{target}   已清 {run.clearedThisRun} 层" +
                    $"   起始 第 {run.startFloor} 层{contractNote}";
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
                    int bonus = Mathf.Clamp(_game.Player.endlessStartRank, 0,
                        AchievementSystem.MaxEndlessStartRank);
                    string bonusNote = bonus > 0 ? $" + 成就 {bonus} 层" : string.Empty;
                    _endlessStartText.text =
                        $"起始层：第 {startFloor} 层（本档最高 − {GameConfig.EndlessCheckpointOffset}{bonusNote}）";
                }
            }

            int checkpointOffset = Mathf.Max(0, GameConfig.EndlessCheckpointOffset);
            _endlessRulesText.text =
                $"· 起始层 = 本档最高 − {checkpointOffset} 层（成就点最多再 +{AchievementSystem.MaxEndlessStartRank} 层，不越过最高层）；最高不足 {checkpointOffset + 1} 层时从第 1 层开始。\n" +
                $"· 每层清完立刻进入下一层；每 {Mathf.Max(1, GameConfig.EndlessBossEvery)} 层一个 BOSS。\n" +
                "· 死亡或撤退结束本局；最高层只记「已清除」的层。\n" +
                "· 每局结束会记进本档历史榜（最多 5 局）；中途退出/换档/重置不算。\n" +
                $"· 掉落等级封顶 {Mathf.Max(1, GameConfig.EndlessDropLevelCap)}；层通过给金币，新纪录额外给首通金币。\n" +
                $"· 深层契约：开局前最多勾 {ContractCatalog.MaxSelected} 条，只改敌人与规则；" +
                "结算契约分 = 已清层数 ×（1 + 各档系数之和），跨转生累计解锁遗物档位与槽位。";

            RefreshEndlessTabs();

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

        /// <summary>
        /// 规则 / 历史榜 两个页签共用同一块内容区：这里决定谁可见，并把本档 Top-5 写成行文本。
        /// 每帧调用——文本量只有 5 行，比做脏标记更省心。
        /// </summary>
        private void RefreshEndlessTabs()
        {
            if (_endlessRulesTab == null) return;

            // 契约是开局前勾的：局内与未解锁时藏掉这一页（局内改了也只影响下一局，容易误读）。
            bool canEditContracts = !_game.EndlessActive && _game.EndlessUnlocked;
            _endlessContractTab.gameObject.SetActive(canEditContracts);
            if (!canEditContracts) _endlessContractOpen = false;

            // 生图底框不染色：选中保持原皮革亮度，未选中整体压暗。
            Color dimTab = new Color(0.62f, 0.60f, 0.57f, 1f);
            bool rules = !_endlessHistoryOpen && !_endlessContractOpen;
            _endlessRulesTab.image.color = rules ? Color.white : dimTab;
            _endlessHistoryTab.image.color = _endlessHistoryOpen ? Color.white : dimTab;
            _endlessContractTab.image.color = _endlessContractOpen ? Color.white : dimTab;
            _endlessRulesTabLabel.color = rules ? Color.white : GameConfig.BtnText;
            _endlessHistoryTabLabel.color = _endlessHistoryOpen ? Color.white : GameConfig.BtnText;
            _endlessContractTabLabel.color = _endlessContractOpen ? Color.white : GameConfig.BtnText;

            _endlessRulesText.gameObject.SetActive(rules);
            _endlessHistoryHint.gameObject.SetActive(_endlessHistoryOpen);
            _endlessContractRoot.gameObject.SetActive(_endlessContractOpen);
            if (_endlessContractOpen) RefreshEndlessContracts();

            System.Collections.Generic.List<EndlessScoreEntry> top =
                _game.Player != null ? _game.Player.endlessTop : null;
            int count = top != null ? Mathf.Min(top.Count, _endlessHistoryRows.Length) : 0;
            _endlessHistoryEmpty.gameObject.SetActive(_endlessHistoryOpen && count == 0);

            bool canClear = _endlessHistoryOpen && count > 0;
            _endlessClearButton.gameObject.SetActive(canClear);
            if (canClear)
            {
                float now = Time.unscaledTime;
                if (_endlessClearConfirmUntil > now)
                    _endlessClearLabel.text = $"确认清空 {_endlessClearConfirmUntil - now:0.0}s";
                else
                {
                    _endlessClearConfirmUntil = 0f;
                    _endlessClearLabel.text = "清空榜单";
                }
            }
            else
            {
                _endlessClearConfirmUntil = 0f;
            }

            for (int i = 0; i < _endlessHistoryRows.Length; i++)
            {
                bool show = _endlessHistoryOpen && i < count;
                _endlessHistoryRows[i].gameObject.SetActive(show);
                if (!show) continue;
                _endlessHistoryRowTexts[i].text = DescribeEndlessScore(top[i], i + 1, _game.LastEndlessRecordUtc);
            }
        }

        /// <summary>
        /// 契约页：把面板上勾的档位画成选中态，并写底行摘要（已选几条 / 倍率 / 累计分）。
        /// 只读控制器里的选择态，点击由按钮回调直接转给 ToggleContractTier。
        /// </summary>
        private void RefreshEndlessContracts()
        {
            if (_endlessContractCells == null || _game == null || _game.Player == null) return;

            int[] tiers = _game.EndlessContractTiers;
            Color dimItem = new Color(0.62f, 0.60f, 0.57f, 1f);

            for (int c = 0; c < _endlessContractCells.Length; c++)
            {
                Image cell = _endlessContractCells[c];
                if (cell == null) continue;

                int chosen = tiers != null && c < tiers.Length ? tiers[c] : 0;
                cell.color = chosen > 0 ? GameConfig.CardBg : GameConfig.CardBgDim;

                for (int t = 0; t < ContractTierSlots; t++)
                {
                    Button tierButton = _endlessContractTierButtons[c, t];
                    if (tierButton == null) continue;

                    bool on = chosen == t + 1;
                    tierButton.image.color = on ? new Color(1f, 0.94f, 0.80f) : dimItem;
                    Text label = _endlessContractTierLabels[c, t];
                    if (label != null)
                        label.color = on ? new Color(1f, 0.95f, 0.78f) : GameConfig.BtnTextDim;
                    Image mark = _endlessContractTierMarks[c, t];
                    if (mark != null) mark.gameObject.SetActive(on);
                }
            }

            int count = _game.EndlessContractCount;
            float coef = _game.EndlessContractCoef;
            int total = _game.Player.contractScore;
            _endlessContractSummary.text =
                $"已选 <color=#8A5A12>{count}</color>/{ContractCatalog.MaxSelected} 条 · " +
                $"本局契约分 ×{1f + coef:0.00} · " +
                $"累计契约分 {total:N0}（{ContractCatalog.NextMilestoneText(total)}）" +
                (count > 0 ? " · 同档再点一次取消" : " · 只改敌人与规则，不动玩家数值");
        }

        /// <summary>
        /// 结算卡的「本局奖励」三行：遗物清单 / 本局契约与契约分 / 下一个里程碑。
        /// 遗物读结束时的快照（EndEndlessRun 已清空 relics）；累计契约分读玩家当前值（已经入账）。
        /// </summary>
        private string BuildEndlessRewardText(EndlessRunState run)
        {
            var sb = new System.Text.StringBuilder();

            var owned = run.resultRelics;
            int slots = Mathf.Max(1, run.RelicSlots);
            if (owned == null || owned.Count == 0)
            {
                sb.Append("本局遗物 0/");
                sb.Append(slots);
                sb.Append("：<color=#4E463C>");
                sb.Append(run.relicsEnabled ? "本局没拿到" : "首局不开放，下一局起生效");
                sb.Append("</color>");
            }
            else
            {
                sb.Append("本局遗物 ");
                sb.Append(owned.Count);
                sb.Append("/");
                sb.Append(slots);
                sb.Append("：");
                for (int i = 0; i < owned.Count; i++)
                {
                    RelicDef def = RelicCatalog.Get(owned[i]);
                    if (def == null) continue;
                    if (i > 0) sb.Append(" · ");
                    sb.Append("<color=#8A5A12>");
                    sb.Append(def.ClassName);
                    sb.Append("</color> ");
                    sb.Append(def.name);
                }
            }

            sb.Append("\n");
            if (run.contractCount > 0)
            {
                sb.Append("深层契约 ");
                sb.Append(run.contractCount);
                sb.Append(" 条（");
                sb.Append(ContractCatalog.Describe(run.contractTiers));
                sb.Append("）· 契约分 <color=#8A5A12>+");
                sb.Append(run.contractScore);
                sb.Append("</color>");
            }
            else
            {
                sb.Append("深层契约 <color=#4E463C>未选</color> · 契约分 +0");
            }

            int total = _game != null && _game.Player != null ? _game.Player.contractScore : 0;
            sb.Append("（累计 ");
            sb.Append(total);
            sb.Append("）");

            sb.Append("\n<color=#6F6252>");
            sb.Append(ContractCatalog.NextMilestoneText(total));
            sb.Append("</color>");
            return sb.ToString();
        }

        /// <summary>一行历史榜成绩：名次 · 到达层 · 清除/巅峰/第几世 · 用时 · 日期 · 结局。</summary>
        private static string DescribeEndlessScore(EndlessScoreEntry entry, int rank, long lastRecordUtc)
        {
            if (entry == null) return "";

            string floorText = entry.floor > 0 ? $"第 <b>{entry.floor}</b> 层" : "未进入第 1 层";
            string detail = $"清 {entry.cleared} 层 · P{entry.paragon}";
            if (entry.rebirth > 0) detail += $" · 第 {entry.rebirth + 1} 世";

            string reason;
            if (entry.IsRetreat) reason = "<color=#6F6252>撤退</color>";
            else if (entry.reason == (int)EndlessEndReason.Death) reason = "<color=#A2461E>阵亡</color>";
            else reason = "<color=#6F6252>中断</color>";

            // 老纪录没有 hasRelic 字段（v15 及更早），显示成「遗物前」：那会儿还没有遗物玩法。
            string relicMark = entry.hasRelic ? " · 遗物" : " · <color=#7A6A55>遗物前</color>";
            if (entry.contractCount > 0 || entry.contractScore > 0)
                relicMark = $" · <color=#8A5A12>契约 {entry.contractScore}分</color>" + relicMark;
            string mark = entry.endedUtc > 0 && entry.endedUtc == lastRecordUtc
                ? "   <color=#8A5A12>◀ 本次</color>"
                : "";
            return $"<color=#8A5A12><b>{rank}</b></color>    {floorText}    {detail}{relicMark} · " +
                $"{FormatEndlessDuration(entry.seconds)} · {FormatEndlessDate(entry.endedUtc)} · {reason}{mark}";
        }

        /// <summary>用时格式：1h05m / 12m34s / 42s。</summary>
        private static string FormatEndlessDuration(int seconds)
        {
            if (seconds <= 0) return "—";
            int hours = seconds / 3600;
            int minutes = seconds % 3600 / 60;
            if (hours > 0) return $"{hours}h{minutes:00}m";
            return minutes > 0 ? $"{minutes}m{seconds % 60:00}s" : $"{seconds}s";
        }

        /// <summary>结束时间：本地时区的 MM-dd HH:mm；时间戳异常时显示占位符。</summary>
        private static string FormatEndlessDate(long utcTicks)
        {
            if (utcTicks <= 0) return "—";
            try
            {
                return new System.DateTime(utcTicks, System.DateTimeKind.Utc)
                    .ToLocalTime().ToString("MM-dd HH:mm");
            }
            catch (System.ArgumentOutOfRangeException)
            {
                return "—";
            }
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
            bool retreat = run.endReason == EndlessEndReason.Retreat;

            _endlessResultTag.text = retreat ? "撤退" : "阵亡";
            _endlessResultTagBg.color = retreat ? GameConfig.BtnHeader : GameConfig.BtnDanger;

            // 层数是这张卡的主标题：只写「第 N 层」，结局交给右上角的标签。
            _endlessResultTitle.text = $"第 {run.reachedFloor} 层";

            _endlessResultRank.text = run.historyRank == 1
                ? "本档历史第一"
                : run.historyRank > 0
                    ? $"本档历史榜 第 {run.historyRank} 名"
                    : "未进历史榜前 5";

            _endlessResultStats[0].text = FormatEndlessDuration(run.finishSeconds);
            _endlessResultStats[1].text = run.runKills.ToString("N0");
            _endlessResultStats[2].text = run.clearedThisRun.ToString();

            // best 是「跑完之后」的本档最高层：本局追平或超过它时两者相等。
            string record = best <= 0
                ? "首次无尽记录"
                : run.reachedFloor >= best
                    ? "<color=#8A5A12>已达本档最高</color>"
                    : $"距本档最高 {best - run.reachedFloor} 层";
            _endlessResultCompare.text =
                $"起始层 第 {run.startFloor} 层 · 本档最高 {best} 层 · 全局最高 {globalBest} 层 · {record}";

            _endlessResultReward.text = BuildEndlessRewardText(run);

            int retryFloor = _game.EndlessSuggestedStartFloor;
            _endlessRetryLabel.text = $"再次挑战（第 {retryFloor} 层）";
            _endlessResultReturnLabel.text = "返回战役";
        }

        // ==================== 点击 ====================

        private void OnEndlessButtonClicked()
        {
            if (_game == null) return;
            // 撤退/清榜确认只属于「当前打开的这一轮面板」：关掉再打开不该还带着倒计时。
            _endlessRetreatConfirmUntil = 0f;
            _endlessClearConfirmUntil = 0f;
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
            _endlessClearConfirmUntil = 0f;
            if (_game != null) _game.CloseEndlessPanel();
            GameAudio.PlayUiClose();
        }

        private void OnEndlessRulesTabClicked()
        {
            _endlessHistoryOpen = false;
            _endlessContractOpen = false;
            _endlessClearConfirmUntil = 0f;
        }

        private void OnEndlessHistoryTabClicked()
        {
            _endlessHistoryOpen = true;
            _endlessContractOpen = false;
            _endlessClearConfirmUntil = 0f;
        }

        private void OnEndlessContractTabClicked()
        {
            _endlessHistoryOpen = false;
            _endlessContractOpen = true;
            _endlessClearConfirmUntil = 0f;
        }

        /// <summary>清空本档历史榜：第一次只上膛，3 秒内再点才真的清（与撤退/删档一致）。</summary>
        private void OnEndlessClearHistoryClicked()
        {
            if (_game == null) return;

            float now = Time.unscaledTime;
            if (_endlessClearConfirmUntil <= now)
            {
                _endlessClearConfirmUntil = now + ConfirmWindow;
                return;
            }

            _endlessClearConfirmUntil = 0f;
            _game.ClearEndlessHistory();
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
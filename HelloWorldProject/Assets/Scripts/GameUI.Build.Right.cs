using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.Events;

namespace AutoBattle
{
    /// <summary>
    /// 全部界面（要求 15、16），用代码构建 UGUI，不依赖场景里预先摆好的物件。
    /// 只读取 <see cref="GameController"/> 的状态来刷新，不反向修改战斗逻辑。
    /// </summary>
    public partial class GameUI : MonoBehaviour
    {
        // 右侧面板构建：怪物区、日志区与底部按钮区
        // ==================== 右侧：怪物 + 日志 + 按钮 ====================

        private void BuildRightPanel(Transform root)
        {
            RectTransform panel = CreatePanel(root, "RightPanel", RightPanelX, 12, RightPanelW, PanelH, GameConfig.PanelBg);

            // 怪物区整块做成一张卡：名字、血条、属性都在卡内，和下面的日志区分开层次。
            CreateCard(panel, "MonsterCard", 8, 6, 808, 108, GameConfig.CardBg);

            _monsterTitle = CreateText(panel, "MonsterTitle", "", 20, 10, 784, 30, 20, GameConfig.AccentMonsterTitle, TextAnchor.MiddleLeft, FontStyle.Bold);

            // 精英/Boss 的血条边框：先建外框，再建血条盖住中间，只露出边缘。
            _monsterHpBorder = CreateImage(panel, "MonsterHpBorder", 17, 43, 790, 32, GameConfig.Clear);
            Round(_monsterHpBorder, RadiusBar + 2);

            _monsterHpFill = CreateBar(panel, "MonsterHpBar", 20, 45, 784, 26, GameConfig.BarTrackHp, GameConfig.FillMonsterHp);
            _monsterHpTrack = _monsterHpFill.transform.parent.GetComponent<Image>();
            _monsterHpText = CreateText(panel, "MonsterHpText", "", 20, 45, 784, 26, 15, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);

            _monsterStatText = CreateText(panel, "MonsterStat", "", 20, 78, 784, 32, 15, GameConfig.TextNormal, TextAnchor.UpperLeft);

            // ---- Buff / Debuff 栏（步骤 6）：狂暴、免死窗口、护盾、技能冷却 ----
            CreateCard(panel, "BuffCard", 8, 116, 808, 32, GameConfig.InsetBg);
            _buffText = CreateText(panel, "BuffText", "", 20, 116, 784, 32, 15, Color.white, TextAnchor.MiddleLeft);

            // ---- P4 战斗舞台（布局 A）：原来的整块日志卡让位给「主角 vs 怪物」，
            // 日志压成底部两行 ticker，点 ▲ 才往上展开成可滚动的大日志。
            BuildBattleStage(panel);

            // ---- 实时 DPS（步骤 8）：做成舞台右上角的小胶囊，避开左上角的章节标签 ----
            _dpsPill = CreateCard(panel, "DpsPill", 592, 163, 216, 26, GameConfig.RowBg, 13, GameConfig.BorderSoft);
            _dpsText = CreateText(_dpsPill.rectTransform, "DpsText", "", 0, 0, 216, 26, 14, GameConfig.TextMint, TextAnchor.MiddleCenter);
            Stretch(_dpsText.rectTransform);

            // ---- 伤害飘字层（步骤 7）：不参与布局 ----
            // 覆盖怪物卡到舞台：伤害数字落在怪物身上（打的是谁，数字就在谁身上）。
            _floatLayer = CreateRect(panel, "FloatLayer", 8, 20, 800, 460);
            BuildFloatPool(_floatLayer);

            BuildShopPanel(panel);

            // ---- 按钮区 ----
            _startButton = CreateButton(panel, "StartButton", "开始", 16, 612, 200, 56, GameConfig.BtnPrimary, out _startButtonLabel);
            _startButtonIcon = StyleBar(_startButton, _startButtonLabel, "frame_bar_round", "glyph_play", 30f, "开始 / 暂停");
            // 开始/暂停是整屏唯一的主按钮，图标+文字整体居中；StyleBar 默认靠左，200 宽会显得右半边空。
            if (_startButtonIcon != null) _startButtonIcon.rectTransform.anchoredPosition = new Vector2(60f, -13f);
            _startButtonLabel.rectTransform.offsetMin = new Vector2(96f, 2f);
            _startButton.onClick.AddListener(OnStartClicked);

            var speedLabels = new string[GameConfig.SpeedOptions.Length];
            for (int i = 0; i < GameConfig.SpeedOptions.Length; i++)
                speedLabels[i] = $"×{GameConfig.SpeedOptions[i]:0}";

            _speedButtons = new Button[GameConfig.SpeedOptions.Length];
            _speedLabels = new Text[GameConfig.SpeedOptions.Length];
            _speedIcons = new Image[GameConfig.SpeedOptions.Length];
            for (int i = 0; i < GameConfig.SpeedOptions.Length; i++)
            {
                int captured = i; // 闭包捕获
                _speedButtons[i] = CreateButton(panel, $"Speed{i}", speedLabels[i], 232 + i * 88, 612, 84, 56,
                    GameConfig.BtnNeutral, out _speedLabels[i]);
                _speedIcons[i] = StyleStack(_speedButtons[i], _speedLabels[i], "frame_hex", "glyph_speed", 23f,
                    $"战斗速度 {speedLabels[i]}");
                _speedButtons[i].onClick.AddListener(() => _game.SetSpeedIndex(captured));
            }

            // 148 宽是为了「确认重置？」五个字在 15 号下能一行放下；右边缘仍对齐 808。
            Button reset = CreateButton(panel, "ResetButton", "重置", 660, 612, 148, 56, GameConfig.RowBg, out _resetLabel);
            StyleBar(reset, _resetLabel, "frame_bar_notched", "glyph_reset", 28f, "重置游戏", 15);
            _resetLabel.color = new Color(0.95f, 0.55f, 0.55f);
            reset.onClick.AddListener(OnResetClicked);
        }

        /// <summary>商店面板：目前只卖生命药剂（价格随主角等级变化）。</summary>
        private void BuildShopPanel(Transform panel)
        {
            _shopCard = CreateCard(panel, "ShopCard", 8, 492, 808, 116, GameConfig.InsetBg);

            CreateIcon(panel, "ui_shop", 28, 502, 20);
            CreateText(panel, "ShopTitle", "商店", 52, 498, 46, 28, 18,
                GameConfig.TextTitle, TextAnchor.MiddleLeft, FontStyle.Bold);

            CreateIcon(panel, "ui_gold", 100, 503, 18);
            _shopGoldText = CreateText(panel, "ShopGold", "", 122, 498, 148, 28, 16,
                GameConfig.TextGold, TextAnchor.MiddleLeft, FontStyle.Bold);

            CreateIcon(panel, "ui_potion", 274, 503, 18);
            _shopPotionText = CreateText(panel, "ShopPotion", "", 296, 498, 258, 28, 15,
                Color.white, TextAnchor.MiddleLeft);

            _buyButton = CreateButton(panel, "BuyPotion", "购买", 556, 496, 88, 32,
                GameConfig.BtnPrimary, out _buyButtonLabel);
            _buyIcon = StyleBar(_buyButton, _buyButtonLabel, "frame_pill", "glyph_buy", 18f, "购买生命药剂");
            _buyButton.onClick.AddListener(OnBuyClicked);

            Button autoBuy = CreateButton(panel, "AutoBuy", "自动购买 开", 652, 496, 144, 32,
                GameConfig.BtnNeutral, out _autoBuyLabel);
            _autoBuyButton = autoBuy;
            _autoBuyIcon = StyleBar(autoBuy, _autoBuyLabel, "frame_bar_notched", "glyph_auto_buy", 18f, "自动购买生命药剂");
            autoBuy.onClick.AddListener(OnAutoBuyClicked);

            _shopTipText = CreateText(panel, "ShopTip", "", 28, 532, 500, 26, 15,
                GameConfig.LogWarn, TextAnchor.MiddleLeft);

            CreateText(panel, "ShopHelp", "低于 40% 生命自动使用（冷却 3 秒）；商店只在精英/Boss 前开放，自动购买会补满",
                28, 566, 764, 28, 13, GameConfig.TextMuted, TextAnchor.MiddleLeft);
        }

        // ==================== 按钮回调 ====================

        private void OnStartClicked() => _game.ToggleRunning();

        /// <summary>
        /// 重置是破坏性的（当前进度直接清空、回 1-1 新档），所以要点两次：
        /// 第一次把按钮改成「确认重置？」，3 秒内再点才真的重置。倒计时由 TickConfirms 收尾。
        /// </summary>
        private void OnResetClicked()
        {
            if (Time.unscaledTime < _resetConfirmUntil)
            {
                _resetConfirmUntil = 0f;
                if (_resetLabel != null) { _resetLabel.text = "重置"; _resetLabel.fontSize = 15; }
                _game.ResetGame();
                return;
            }

            _resetConfirmUntil = Time.unscaledTime + ConfirmWindow;
            // 「确认重置？」5 个全角字在 108 宽的按钮里按 18 号会顶到边，缩短一号刚好留出内边距。
            if (_resetLabel != null) { _resetLabel.text = "确认重置？"; _resetLabel.fontSize = 15; }
            _game.AddLog("⚠ 重置会清空当前进度：3 秒内再点一次「确认重置？」才会生效", GameConfig.LogWarn);
        }

    }
}

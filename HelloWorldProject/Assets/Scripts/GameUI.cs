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
    public class GameUI : MonoBehaviour
    {
        // ---- 布局常量（基于 1280x720 参考分辨率）----
        private const float LeftPanelW = 420f;
        private const float RightPanelX = 444f;
        private const float RightPanelW = 824f;
        private const float PanelH = 696f;

        /// <summary>日志区最多同时显示的行数，超出部分靠只显示最新几条来截断。</summary>
        private const int MaxVisibleLogLines = 22;

        private GameController _game;
        private Font _font;

        // ---- 需要在刷新时更新的控件 ----
        private Text _levelText;
        private Text _hpText;
        private Image _hpFill;
        private Text _atkText;
        private Text _defText;
        private Text _spdText;
        private Text _expText;
        private Image _expFill;
        private Text _weaponText;
        private Text _armorText;
        private Text _goldText;
        private Text _critText;
        private Text _statText;

        private Text _monsterTitle;
        private Text _monsterHpText;
        private Image _monsterHpFill;
        private Image _monsterHpBorder;
        private Text _monsterStatText;

        private Text _logText;
        private Button _startButton;
        private Text _startButtonLabel;
        private Button[] _speedButtons;
        private Text[] _speedLabels;

        // ---- 商店 ----
        private Text _shopGoldText;
        private Text _shopPotionText;
        private Text _shopTipText;
        private Button _buyButton;
        private Text _autoBuyLabel;

        // ---- 技能栏与技能面板 ----
        private Button[] _skillSlotButtons;
        private Text[] _skillSlotLabels;
        private Text[] _skillSlotStates;
        private Image[] _skillSlotFills;

        private RectTransform _skillPanel;
        private Text _skillPanelTitle;
        private Button[] _skillPoolButtons;
        private Text[] _skillPoolLabels;

        // ---- 技能三选一面板 ----
        private RectTransform _skillChoicePanel;
        private Text _skillChoiceTitle;
        private Button[] _skillChoiceButtons;
        private Text[] _skillChoiceLabels;

        /// <summary>技能面板当前选中的槽位（0~5）。</summary>
        private int _selectedSkillSlot;

        // ---- 选关面板（4b）----
        private Button _stageButton;
        private Text _stageButtonLabel;

        private RectTransform _stagePanel;
        private Text _stagePanelTitle;
        private Button[] _stageChapterButtons;
        private Text[] _stageChapterLabels;
        private Button[] _stageSlotButtons;
        private Text[] _stageSlotLabels;
        private Text _stageInfoText;

        /// <summary>面板顶部的全局进度总览（步骤 4d）。</summary>
        private Text _stageSummaryText;

        /// <summary>选关面板里当前浏览的章节（点章节按钮切换）。</summary>
        private int _stageViewChapter = 1;

        /// <summary>上一帧选关面板是否开着，用来只在「刚打开」时重置浏览章节。</summary>
        private bool _stagePanelWasOpen;

        // ---- 存档槽面板 ----
        private RectTransform _savePanel;
        private Text _savePanelTitle;
        private Button[] _saveSlotButtons;
        private Text[] _saveSlotLabels;
        private Text[] _saveSlotDeleteLabels;
        private bool _savePanelWasOpen;
        private bool _savePanelDirty = true;
        private Text _saveButtonLabel;

        // ---- 装备行缓存 ----
        // EquipmentScore.Of 为了算增量评分，内部会临时换装再还原：每次调用 = 3 次改
        // PlayerData.weapon/armor + 2 次属性重算。评分只可能随「装备 / 等级 / 巅峰」变化，
        // 所以记住上次算过的这四个值，没变就直接复用字符串。
        private Equipment _scoreCachedWeapon;
        private Equipment _scoreCachedArmor;
        private int _scoreCachedLevel = -1;
        private int _scoreCachedParagon = -1;
        private string _scoreWeaponText;
        private string _scoreArmorText;

        /// <summary>Buff 栏复用的拼装缓冲：内容每帧都在变（倒计时），但没必要每帧新建一个。</summary>
        private readonly System.Text.StringBuilder _buffSb = new System.Text.StringBuilder(320);

        // ---- 居中横幅（首通 / 章节解锁，步骤 4c）----
        private RectTransform _bannerPanel;
        private CanvasGroup _bannerGroup;
        private Text _bannerTitle;
        private Text _bannerSubtitle;

        private int _lastLogVersion = -1;

        // ==================== P2：战斗可读性 ====================

        /// <summary>Buff / Debuff 栏。</summary>
        private Text _buffText;

        /// <summary>实时 DPS 行。</summary>
        private Text _dpsText;

        /// <summary>伤害飘字的容器。</summary>
        private RectTransform _floatLayer;

        /// <summary>飘字对象池大小。</summary>
        private const int FloatPoolSize = 14;

        private Text[] _floatTexts;
        private float[] _floatLife;
        private Vector2[] _floatPos;
        private Vector2[] _floatVel;
        private int _floatCursor;

        /// <summary>伤害日志的解析游标（飘字 + DPS 共用）。</summary>
        private int _lastParsedLogVersion = -1;
        private int _lastParsedLogCount;

        /// <summary>实时 DPS 的累计器（窗口内伤害与时长）。</summary>
        private float _dpsDamageAccum;
        private float _dpsWindowTime;
        private float _dpsSmoothed;

        // ==================== 初始化 ====================

        /// <summary>由 Bootstrap 调用，绑定控制器并搭建界面。</summary>
        public void Initialize(GameController game)
        {
            _game = game;
            _font = LoadFont();

            EnsureEventSystem();
            BuildCanvas();
            Refresh();
        }

        private static Font LoadFont()
        {
            // Unity 2022+ 的内置字体名；老版本回退到 Arial。
            Font f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (f == null) f = Resources.GetBuiltinResource<Font>("Arial.ttf");
            return f;
        }

        /// <summary>UGUI 的按钮点击依赖 EventSystem；工程可能启用新旧两套输入系统，这里都兼容。</summary>
        private void EnsureEventSystem()
        {
            if (FindAnyObjectByType<EventSystem>() != null) return;

            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
            go.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
            go.AddComponent<StandaloneInputModule>();
#endif
        }

        private void BuildCanvas()
        {
            var canvasGo = new GameObject("AutoBattleCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);

            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            // 以高度为基准缩放。设计稿高 720，视口变矮时整体等比缩小，
            // 面板底部的技能栏才不会被裁。原先的 0.5 折中在 1280×612 这类矮视口下
            // 只算出约 0.92 的缩放，696 高的面板缩完仍高于 612，底部照样被切掉。
            scaler.matchWidthOrHeight = 1f;

            Transform root = canvasGo.transform;

            // 背景
            Image bg = CreateImage(root, "Background", 0, 0, 1280, 720, new Color(0.11f, 0.12f, 0.15f), false);
            Stretch(bg.rectTransform);

            BuildLeftPanel(root);
            BuildRightPanel(root);
            BuildSkillPanel(root);
            BuildSkillChoicePanel(root);
            BuildStagePanel(root);
            BuildSavePanel(root);
            // 横幅必须最后建：它要盖在选关 / 技能面板之上。
            BuildBanner(root);
        }

        // ==================== 左侧：属性 + 装备 ====================

        /// <summary>
        /// 主角立绘（`Assets/Resources/HeroPortraitFade.png`）：铺在左面板中段当底纹。
        /// 左面板内容一直排到 y≈648，塞不下独立的立绘框，所以用背景水印的方式让立绘进画面。
        /// 用的是边缘径向淡出的那一版——硬边半透明看起来像贴纸穿帮，化开之后才像刻意的底纹，
        /// 所以 alpha 反而可以给得比硬边版更高。
        /// 另有一张未淡出的 `HeroPortrait.png`（同一张图，背景已抠成透明），留给以后正式的立绘框。
        /// 注意它必须在 BuildLeftPanel 里最先建，否则会盖在文字上面。
        /// </summary>
        private void BuildHeroPortrait(Transform panel)
        {
            Sprite hero = Resources.Load<Sprite>("HeroPortraitFade");
            if (hero == null)
            {
                Debug.LogWarning("[GameUI] 找不到 Resources/HeroPortraitFade.png，主角立绘跳过。");
                return;
            }

            // 源图 803×1024（竖版全身立绘，比例 0.784），Seedream 直接出的透明底。
            // 靠右放：左面板的属性行、装备行文字都偏左（约 x<320），右半留白多。
            // 纵向从 y=226 起——经验条在 y 236~278，立绘先建会被后建的进度条横切一道，
            // 但淡出版边缘本来就是糊的，切过去不明显；再往上顶到标题行反而更挤。
            Image img = CreateImage(panel, "HeroPortrait", 198, 226, 196, 250, Color.white);
            img.sprite = hero;
            img.preserveAspect = true;
            img.color = new Color(1f, 1f, 1f, 0.50f);
        }

        private void BuildLeftPanel(Transform root)
        {
            RectTransform panel = CreatePanel(root, "LeftPanel", 12, 12, LeftPanelW, PanelH, new Color(0.16f, 0.17f, 0.21f));

            // 立绘必须最先建：它是底纹，要压在所有文字下面。
            BuildHeroPortrait(panel);

            CreateText(panel, "Title", "角色属性", 16, 12, 260, 30, 20, new Color(0.95f, 0.95f, 0.8f), TextAnchor.MiddleLeft, FontStyle.Bold);

            // 存档入口：挤在标题行右侧。CreateText 的 raycastTarget 是关的，
            // 所以这里不用管两行文字的矩形重叠；标题实际只占左边约 90px。
            Button saveBtn = CreateButton(panel, "SaveButton", "存档 1", 300, 15, 88, 24,
                new Color(0.24f, 0.28f, 0.40f), out _saveButtonLabel);
            _saveButtonLabel.fontSize = 14;
            saveBtn.onClick.AddListener(OnSaveButtonClicked);

            _levelText = CreateText(panel, "Level", "", 16, 48, 224, 26, 17, Color.white, TextAnchor.MiddleLeft, FontStyle.Bold);

            // 选关入口：等级行右侧。4a 时关卡信息塞在等级行里，4b 挪到这里当按钮，
            // 顺便让等级行不至于被「关卡 11-10 · 怪 Lv.110」撑爆。
            _stageButton = CreateButton(panel, "StageButton", "", 248, 48, 156, 26,
                new Color(0.22f, 0.26f, 0.36f), out _stageButtonLabel);
            _stageButtonLabel.fontSize = 13;
            _stageButtonLabel.alignment = TextAnchor.MiddleCenter;
            _stageButton.onClick.AddListener(OnStageButtonClicked);

            _hpText = CreateText(panel, "HpText", "", 16, 78, 388, 22, 15, new Color(0.9f, 0.9f, 0.9f), TextAnchor.MiddleLeft);
            _hpFill = CreateBar(panel, "HpBar", 16, 104, 388, 18, new Color(0.22f, 0.1f, 0.12f), new Color(0.85f, 0.28f, 0.32f));

            _atkText = CreateText(panel, "Atk", "", 16, 130, 388, 24, 15, new Color(0.95f, 0.8f, 0.6f), TextAnchor.MiddleLeft);
            _defText = CreateText(panel, "Def", "", 16, 156, 388, 24, 15, new Color(0.7f, 0.85f, 0.95f), TextAnchor.MiddleLeft);
            _spdText = CreateText(panel, "Spd", "", 16, 182, 388, 24, 15, new Color(0.8f, 0.95f, 0.7f), TextAnchor.MiddleLeft);

            _critText = CreateText(panel, "Crit", "", 16, 208, 388, 24, 15, new Color(1f, 0.78f, 0.9f), TextAnchor.MiddleLeft);

            _expText = CreateText(panel, "ExpText", "", 16, 236, 388, 22, 15, new Color(0.85f, 0.8f, 1f), TextAnchor.MiddleLeft);
            _expFill = CreateBar(panel, "ExpBar", 16, 260, 388, 18, new Color(0.14f, 0.12f, 0.24f), new Color(0.55f, 0.45f, 0.95f));

            CreateImage(panel, "Divider", 16, 292, 388, 2, new Color(1f, 1f, 1f, 0.12f));

            CreateText(panel, "EquipTitle", "装备", 16, 302, 388, 28, 20, new Color(0.95f, 0.95f, 0.8f), TextAnchor.MiddleLeft, FontStyle.Bold);

            _weaponText = CreateText(panel, "Weapon", "", 16, 334, 388, 86, 15, Color.white, TextAnchor.UpperLeft);
            _armorText = CreateText(panel, "Armor", "", 16, 424, 388, 86, 15, Color.white, TextAnchor.UpperLeft);

            CreateImage(panel, "Divider2", 16, 514, 388, 2, new Color(1f, 1f, 1f, 0.12f));
            _goldText = CreateText(panel, "Gold", "", 16, 516, 388, 28, 19, new Color(1f, 0.82f, 0.3f), TextAnchor.MiddleLeft, FontStyle.Bold);

            // 统计压到 3 行，给底部技能栏腾位置。
            // 统计只占 2 行，紧跟金币行下方，互不重叠。
            _statText = CreateText(panel, "Stats", "", 16, 548, 388, 34, 13, new Color(0.75f, 0.78f, 0.85f), TextAnchor.UpperLeft);

            BuildSkillBar(panel);
        }

        /// <summary>
        /// 技能栏：6 个槽位（主动 2 / 被动 2 / 触发 2），横向铺满左面板宽度。
        /// 槽位内自带「主动 / 被动 / 触发」小标签，所以不再单独放标题。
        /// </summary>
        private void BuildSkillBar(Transform panel)
        {
            CreateImage(panel, "Divider3", 16, 586, 388, 2, new Color(1f, 1f, 1f, 0.12f));

            const int slotCount = 6;
            const float slotW = 62f;
            const float slotH = 58f;
            const float step = 66f;

            _skillSlotButtons = new Button[slotCount];
            _skillSlotLabels = new Text[slotCount];
            _skillSlotStates = new Text[slotCount];
            _skillSlotFills = new Image[slotCount];

            for (int i = 0; i < slotCount; i++)
            {
                int captured = i;
                float x = 16f + i * step;

                _skillSlotButtons[i] = CreateButton(panel, $"SkillSlot{i}", "", x, 590, slotW, slotH,
                    new Color(0.20f, 0.22f, 0.28f), out _skillSlotLabels[i]);

                // 槽位内自上而下四段：类别(距顶 0~14) / 名称(距顶 14~32) / 冷却(距顶 32~46) / 进度条(48~54)。
                // offsetMin.y 是「距父物体底部」、offsetMax.y 是「距顶部（负值）」，两者写反就会
                // 把名称压到距顶 21~37，正好和冷却的 30~42 重叠。
                // 槽位高 50 时名称和「就绪」只剩 2.4px 间隙，视觉上仍挤，于是把槽位加到 58。
                _skillSlotLabels[i].fontSize = 12;
                _skillSlotLabels[i].alignment = TextAnchor.MiddleCenter;
                _skillSlotLabels[i].rectTransform.offsetMin = new Vector2(2f, slotH - 32f);
                _skillSlotLabels[i].rectTransform.offsetMax = new Vector2(-2f, -14f);

                // 冷却秒数 / 就绪。
                _skillSlotStates[i] = CreateText(_skillSlotButtons[i].transform, $"SkillState{i}", "",
                    2, 32, slotW - 4f, 14, 10, new Color(0.85f, 0.9f, 1f), TextAnchor.MiddleCenter);

                // 槽位类别标记（主动 / 被动 / 触发）。
                CreateText(_skillSlotButtons[i].transform, $"SkillKind{i}",
                    KindShort(i / GameConfig.SkillSlotsPerKind),
                    2, 0, slotW - 4f, 14, 9, new Color(0.62f, 0.66f, 0.74f), TextAnchor.MiddleCenter);

                // 底部冷却进度条。
                Image barBg = CreateImage(_skillSlotButtons[i].transform, $"SkillCdBar{i}",
                    2, slotH - 8f, slotW - 4f, 6, new Color(0f, 0f, 0f, 0.45f));
                _skillSlotFills[i] = CreateImage(barBg.rectTransform, "Fill", 0, 0, 0, 6,
                    new Color(0.45f, 1f, 0.6f, 0.9f));

                RectTransform fill = _skillSlotFills[i].rectTransform;
                fill.anchorMin = new Vector2(0f, 0.5f);
                fill.anchorMax = new Vector2(0f, 0.5f);
                fill.pivot = new Vector2(0f, 0.5f);
                fill.anchoredPosition = Vector2.zero;
                fill.sizeDelta = new Vector2(0f, 6f);

                _skillSlotButtons[i].onClick.AddListener(() => OnSkillSlotClicked(captured));
            }
        }

        /// <summary>槽位类别简写。</summary>
        private static string KindShort(int kindIndex)
        {
            switch (kindIndex)
            {
                case 0: return "主动";
                case 1: return "被动";
                default: return "触发";
            }
        }

        // ==================== 右侧：怪物 + 日志 + 按钮 ====================

        private void BuildRightPanel(Transform root)
        {
            RectTransform panel = CreatePanel(root, "RightPanel", RightPanelX, 12, RightPanelW, PanelH, new Color(0.16f, 0.17f, 0.21f));

            _monsterTitle = CreateText(panel, "MonsterTitle", "", 16, 12, 792, 28, 19, new Color(1f, 0.85f, 0.55f), TextAnchor.MiddleLeft, FontStyle.Bold);

            // 精英/Boss 的血条边框：先建外框，再建血条盖住中间，只露出边缘。
            _monsterHpBorder = CreateImage(panel, "MonsterHpBorder", 13, 43, 798, 30, new Color(0f, 0f, 0f, 0f));

            _monsterHpFill = CreateBar(panel, "MonsterHpBar", 16, 46, 792, 24, new Color(0.22f, 0.1f, 0.12f), new Color(0.9f, 0.35f, 0.3f));
            _monsterHpText = CreateText(panel, "MonsterHpText", "", 16, 46, 792, 24, 15, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);

            _monsterStatText = CreateText(panel, "MonsterStat", "", 16, 76, 792, 32, 15, new Color(0.85f, 0.85f, 0.9f), TextAnchor.UpperLeft);

            // ---- Buff / Debuff 栏（步骤 6）：狂暴、免死窗口、护盾、技能冷却 ----
            CreateImage(panel, "BuffBarBg", 16, 110, 792, 30, new Color(0.12f, 0.13f, 0.17f));
            _buffText = CreateText(panel, "BuffText", "", 24, 110, 776, 30, 15, Color.white, TextAnchor.MiddleLeft);

            CreateImage(panel, "Divider", 16, 150, 792, 2, new Color(1f, 1f, 1f, 0.12f));
            CreateText(panel, "LogTitle", "战斗日志（最新在上）", 16, 158, 400, 26, 17, new Color(0.9f, 0.9f, 0.75f), TextAnchor.MiddleLeft, FontStyle.Bold);

            // ---- 实时 DPS（步骤 8），和日志标题同一行、靠右 ----
            _dpsText = CreateText(panel, "DpsText", "", 424, 158, 384, 26, 15, new Color(0.75f, 0.95f, 0.85f), TextAnchor.MiddleRight);

            RectTransform logRect = CreateRect(panel, "LogText", 16, 188, 792, 298);

            // ---- 伤害飘字层（步骤 7）：盖在日志区上面，不参与布局 ----
            _floatLayer = CreateRect(panel, "FloatLayer", 16, 188, 792, 298);
            BuildFloatPool(_floatLayer);
            _logText = logRect.gameObject.AddComponent<Text>();
            _logText.font = _font;
            _logText.fontSize = 14;
            _logText.color = Color.white;
            _logText.alignment = TextAnchor.UpperLeft;
            _logText.horizontalOverflow = HorizontalWrapMode.Wrap;
            _logText.verticalOverflow = VerticalWrapMode.Truncate;
            _logText.supportRichText = true;
            _logText.lineSpacing = 1.1f;

            BuildShopPanel(panel);

            // ---- 按钮区 ----
            _startButton = CreateButton(panel, "StartButton", "开始", 16, 612, 200, 56, new Color(0.20f, 0.45f, 0.28f), out _startButtonLabel);
            _startButton.onClick.AddListener(OnStartClicked);

            var speedLabels = new string[GameConfig.SpeedOptions.Length];
            for (int i = 0; i < GameConfig.SpeedOptions.Length; i++)
                speedLabels[i] = $"x{GameConfig.SpeedOptions[i]:0}";

            _speedButtons = new Button[GameConfig.SpeedOptions.Length];
            _speedLabels = new Text[GameConfig.SpeedOptions.Length];
            for (int i = 0; i < GameConfig.SpeedOptions.Length; i++)
            {
                int captured = i; // 闭包捕获
                _speedButtons[i] = CreateButton(panel, $"Speed{i}", speedLabels[i], 236 + i * 92, 612, 84, 56,
                    new Color(0.22f, 0.24f, 0.32f), out _speedLabels[i]);
                _speedButtons[i].onClick.AddListener(() => _game.SetSpeedIndex(captured));
            }

            Button reset = CreateButton(panel, "ResetButton", "重置", 700, 612, 108, 56, new Color(0.42f, 0.22f, 0.22f), out _);
            reset.onClick.AddListener(OnResetClicked);
        }

        /// <summary>商店面板：目前只卖生命药剂（价格随主角等级变化）。</summary>
        private void BuildShopPanel(Transform panel)
        {
            CreateImage(panel, "ShopBg", 16, 492, 792, 112, new Color(0.13f, 0.14f, 0.18f));

            CreateText(panel, "ShopTitle", "商店", 28, 498, 70, 28, 18,
                new Color(0.95f, 0.9f, 0.7f), TextAnchor.MiddleLeft, FontStyle.Bold);

            _shopGoldText = CreateText(panel, "ShopGold", "", 100, 498, 170, 28, 16,
                new Color(1f, 0.82f, 0.3f), TextAnchor.MiddleLeft, FontStyle.Bold);

            _shopPotionText = CreateText(panel, "ShopPotion", "", 274, 498, 280, 28, 15,
                Color.white, TextAnchor.MiddleLeft);

            _buyButton = CreateButton(panel, "BuyPotion", "购买", 556, 496, 88, 32,
                new Color(0.20f, 0.45f, 0.28f), out _);
            _buyButton.onClick.AddListener(OnBuyClicked);

            Button autoBuy = CreateButton(panel, "AutoBuy", "", 652, 496, 144, 32,
                new Color(0.22f, 0.24f, 0.32f), out _autoBuyLabel);
            autoBuy.onClick.AddListener(OnAutoBuyClicked);

            _shopTipText = CreateText(panel, "ShopTip", "", 28, 532, 500, 26, 15,
                new Color(1f, 0.5f, 0.45f), TextAnchor.MiddleLeft);

            CreateText(panel, "ShopHelp", "低于 40% 生命自动使用（冷却 3 秒）；商店只在精英/Boss 前开放，自动购买会补满",
                28, 566, 764, 28, 13, new Color(0.62f, 0.65f, 0.72f), TextAnchor.MiddleLeft);
        }

        // ==================== 按钮回调 ====================

        private void OnStartClicked() => _game.ToggleRunning();

        private void OnResetClicked() => _game.ResetGame();

        // ==================== 技能面板 ====================

        /// <summary>
        /// 技能三选一面板：巅峰升级发技能时弹出，从候选里挑一个。
        /// 居中显示，不选完不消失（战斗在 State.SkillChoice 下暂停）。
        /// </summary>
        private void BuildSkillChoicePanel(Transform root)
        {
            int count = GameConfig.SkillChoiceCount;

            _skillChoicePanel = CreatePanel(root, "SkillChoicePanel", 250, 232, 780, 244,
                new Color(0.10f, 0.11f, 0.15f, 1f));

            _skillChoiceTitle = CreateText(_skillChoicePanel, "SkillChoiceTitle", "", 24, 12, 732, 30, 20,
                new Color(1f, 0.88f, 0.5f), TextAnchor.MiddleCenter, FontStyle.Bold);

            CreateText(_skillChoicePanel, "SkillChoiceHint", "从下面选一个技能（选完自动装备并继续战斗）",
                24, 44, 732, 22, 14, new Color(0.7f, 0.74f, 0.82f), TextAnchor.MiddleCenter);

            _skillChoiceButtons = new Button[count];
            _skillChoiceLabels = new Text[count];

            const float cardW = 236f;
            const float cardH = 150f;
            float gap = count > 1 ? (732f - cardW * count) / (count - 1) : 0f;

            for (int i = 0; i < count; i++)
            {
                int captured = i;
                float x = 24f + i * (cardW + gap);

                _skillChoiceButtons[i] = CreateButton(_skillChoicePanel, $"SkillChoice{i}", "", x, 72f, cardW, cardH,
                    new Color(0.20f, 0.23f, 0.30f), out _skillChoiceLabels[i]);

                _skillChoiceLabels[i].fontSize = 15;
                _skillChoiceLabels[i].alignment = TextAnchor.UpperLeft;
                _skillChoiceLabels[i].rectTransform.offsetMin = new Vector2(12f, 10f);
                _skillChoiceLabels[i].rectTransform.offsetMax = new Vector2(-12f, -10f);

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

            _skillChoiceTitle.text = pending > 1
                ? $"✧ 巅峰奖励 — 选择技能（还有 {pending} 次待选）"
                : "✧ 巅峰奖励 — 选择技能";

            for (int i = 0; i < _skillChoiceButtons.Length; i++)
            {
                bool valid = i < choices.Count;
                _skillChoiceButtons[i].gameObject.SetActive(valid);
                if (!valid) continue;

                SkillDef def = SkillCatalog.Get(choices[i]);
                _skillChoiceLabels[i].text =
                    $"{def.name}\n<size=13><color=#AAB0BD>{KindShort((int)def.kind)}</color></size>\n\n{def.description}";
                _skillChoiceLabels[i].color = def.color;
            }
        }
        /// <summary>技能面板：点击技能栏槽位后弹出，列出整个技能池供装配。</summary>
        private void BuildSkillPanel(Transform root)
        {
            _skillPanel = CreatePanel(root, "SkillPanel", 236, 96, 808, 508, new Color(0.12f, 0.13f, 0.17f));

            _skillPanelTitle = CreateText(_skillPanel, "SkillPanelTitle", "", 20, 12, 768, 30, 19,
                new Color(0.95f, 0.92f, 0.7f), TextAnchor.MiddleLeft, FontStyle.Bold);

            CreateText(_skillPanel, "SkillPanelHint",
                "点击技能装到当前槽位（类别不符会自动放进同类空槽）；点击已装备的技能可卸下",
                20, 44, 768, 24, 13, new Color(0.68f, 0.72f, 0.8f), TextAnchor.MiddleLeft);

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
                float y = 74f + row * 82f;

                _skillPoolButtons[i] = CreateButton(_skillPanel, $"SkillPool{i}", "", x, y, 248, 74,
                    new Color(0.20f, 0.22f, 0.28f), out _skillPoolLabels[i]);

                _skillPoolLabels[i].fontSize = 14;
                _skillPoolLabels[i].alignment = TextAnchor.MiddleLeft;
                _skillPoolLabels[i].rectTransform.offsetMin = new Vector2(10f, 4f);
                _skillPoolLabels[i].rectTransform.offsetMax = new Vector2(-8f, -4f);

                _skillPoolButtons[i].onClick.AddListener(() => OnSkillPoolClicked(captured));
            }

            Button close = CreateButton(_skillPanel, "SkillPanelClose", "关闭", 698, 458, 90, 36,
                new Color(0.35f, 0.24f, 0.26f), out _);
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
            _savePanel = CreatePanel(root, "SavePanel", 300, 196, 680, 304, new Color(0.11f, 0.12f, 0.16f));

            _savePanelTitle = CreateText(_savePanel, "SavePanelTitle", "", 20, 12, 640, 30, 19,
                new Color(0.95f, 0.92f, 0.7f), TextAnchor.MiddleLeft, FontStyle.Bold);

            CreateText(_savePanel, "SavePanelHint",
                "点卡片切换存档，各槽进度互不影响；「删除」清空该槽。切档会结束当前这局。",
                20, 42, 640, 22, 13, new Color(0.78f, 0.84f, 0.95f), TextAnchor.MiddleLeft);

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
                    new Color(0.18f, 0.20f, 0.26f), out Text cardLabel);

                // CreateButton 把 label 拉满并居中；这里改成左上对齐 + 内缩。
                cardLabel.alignment = TextAnchor.UpperLeft;
                cardLabel.fontSize = 14;
                cardLabel.fontStyle = FontStyle.Normal;
                cardLabel.rectTransform.offsetMin = new Vector2(14f, 32f);
                cardLabel.rectTransform.offsetMax = new Vector2(-12f, -12f);

                card.onClick.AddListener(() => OnSaveSlotClicked(slot));

                Button del = CreateButton(_savePanel, "SaveSlotDel" + slot, "删除", x + 112f, y0 + cardH - 34f, 76f, 26f,
                    new Color(0.40f, 0.22f, 0.22f), out _saveSlotDeleteLabels[slot]);
                _saveSlotDeleteLabels[slot].fontSize = 13;
                del.onClick.AddListener(() => OnSaveSlotDeleteClicked(slot));

                _saveSlotButtons[slot] = card;
                _saveSlotLabels[slot] = cardLabel;
            }

            Button close = CreateButton(_savePanel, "SavePanelClose", "关闭", 586, 250, 74, 34,
                new Color(0.26f, 0.28f, 0.34f), out _);
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
                        ? new Color(0.20f, 0.34f, 0.30f)
                        : new Color(0.18f, 0.20f, 0.26f);
                }

                _saveSlotDeleteLabels[i].color = info.exists ? Color.white : new Color(0.55f, 0.55f, 0.58f);
            }
        }

        /// <summary>槽摘要排成卡片里的几行。</summary>
        private static string DescribeSlotText(SaveSystem.SlotInfo info, int totalStages)
        {
            if (!info.exists) return "\n（空档）\n\n点击开始新游戏";
            if (info.broken) return "\n⚠ 存档损坏\n\n点击重开新档";

            return $"\nLv.{info.level}   巅峰 {info.paragonLevel}\n" +
                   $"第 {info.chapter} 章 {info.chapter}-{info.stage}\n" +
                   $"金币 {info.gold:N0}\n" +
                   $"首通 {info.clearedCount}/{totalStages} 关";
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

        private void OnSaveSlotDeleteClicked(int slot)
        {
            _game.DeleteSlot(slot);
            _savePanelDirty = true;
        }

        private void BuildBanner(Transform root)
        {
            _bannerPanel = CreatePanel(root, "Banner", 340, 186, 600, 120,
                new Color(0.07f, 0.09f, 0.14f, 0.97f));

            // 金边：横幅要盖在日志上，光靠半透明底色压不住下面的字。
            CreatePanel(_bannerPanel, "BannerEdgeTop", 0, 0, 600, 3, new Color(1f, 0.78f, 0.32f, 0.95f));
            CreatePanel(_bannerPanel, "BannerEdgeBottom", 0, 117, 600, 3, new Color(1f, 0.78f, 0.32f, 0.95f));
            CreatePanel(_bannerPanel, "BannerEdgeLeft", 0, 0, 3, 120, new Color(1f, 0.78f, 0.32f, 0.95f));
            CreatePanel(_bannerPanel, "BannerEdgeRight", 597, 0, 3, 120, new Color(1f, 0.78f, 0.32f, 0.95f));

            _bannerGroup = _bannerPanel.gameObject.AddComponent<CanvasGroup>();
            _bannerGroup.blocksRaycasts = false;
            _bannerGroup.interactable = false;

            _bannerTitle = CreateText(_bannerPanel, "BannerTitle", "", 16, 20, 568, 46, 28,
                new Color(1f, 0.90f, 0.52f), TextAnchor.MiddleCenter, FontStyle.Bold);

            _bannerSubtitle = CreateText(_bannerPanel, "BannerSubtitle", "", 16, 68, 568, 34, 17,
                new Color(0.82f, 0.88f, 0.98f), TextAnchor.MiddleCenter);

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

            _stagePanel = CreatePanel(root, "StagePanel", 236, 96, 808, 508, new Color(0.11f, 0.12f, 0.16f));

            _stagePanelTitle = CreateText(_stagePanel, "StagePanelTitle", "", 20, 12, 768, 30, 19,
                new Color(0.95f, 0.92f, 0.7f), TextAnchor.MiddleLeft, FontStyle.Bold);

            // 这一行原来是静态操作提示，改成全局进度总览——操作提示在下方详情里已经有一份了。
            _stageSummaryText = CreateText(_stagePanel, "StagePanelSummary", "", 20, 44, 768, 24, 13,
                new Color(0.78f, 0.84f, 0.95f), TextAnchor.MiddleLeft);

            // 左栏：11 个章节按钮，每个 31 高、间距 3。
            _stageChapterButtons = new Button[chapters];
            _stageChapterLabels = new Text[chapters];

            for (int i = 0; i < chapters; i++)
            {
                int captured = i;
                float y = 76f + i * 34f;

                _stageChapterButtons[i] = CreateButton(_stagePanel, $"StageChapter{i}", "", 20f, y, 148f, 31f,
                    new Color(0.19f, 0.21f, 0.27f), out _stageChapterLabels[i]);

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
                    new Color(0.20f, 0.22f, 0.28f), out _stageSlotLabels[i]);

                _stageSlotLabels[i].fontSize = 14;
                _stageSlotLabels[i].alignment = TextAnchor.MiddleCenter;

                _stageSlotButtons[i].onClick.AddListener(() => OnStageSlotClicked(captured));
            }

            _stageInfoText = CreateText(_stagePanel, "StageInfo", "", 188, 252, 600, 180, 14,
                new Color(0.80f, 0.84f, 0.92f), TextAnchor.UpperLeft);

            Button close = CreateButton(_stagePanel, "StagePanelClose", "关闭", 698, 458, 90, 36,
                new Color(0.35f, 0.24f, 0.26f), out _);
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

            // 入口按钮上的关卡速览：面板不开也要对。
            if (_stageButtonLabel != null)
            {
                _stageButtonLabel.text = current != null
                    ? $"关卡 {current.Code} · 怪 Lv.{current.monsterLevel}"
                    : "关卡 —";
            }

            bool open = _game.StagePanelOpen;
            _stagePanel.gameObject.SetActive(open);

            // 只在「刚打开」这一帧把浏览章节拨到玩家当前所在的章；
            // 否则玩家一点别的章节，就会被每帧刷新弹回去。
            if (open && !_stagePanelWasOpen && current != null)
            {
                _stageViewChapter = Mathf.Clamp(current.chapter, 1, StageCatalog.ChapterCount);
            }
            _stagePanelWasOpen = open;

            if (!open) return;

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
                    ? (viewing ? new Color(1f, 0.92f, 0.6f) : new Color(0.82f, 0.86f, 0.94f))
                    : new Color(0.5f, 0.52f, 0.58f);
                _stageChapterLabels[i].fontStyle = current.chapter == chapter ? FontStyle.Bold : FontStyle.Normal;

                // Button.image 就是 targetGraphic，建按钮时已经设好；这里不必每帧再 GetComponent。
                Image bg = _stageChapterButtons[i].image;
                if (bg != null)
                {
                    bg.color = viewing
                        ? new Color(0.28f, 0.34f, 0.46f)
                        : new Color(0.19f, 0.21f, 0.27f);
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
                    ? (isCurrent ? Color.white : (passed ? new Color(0.85f, 0.93f, 0.85f) : new Color(0.9f, 0.92f, 0.96f)))
                    : new Color(0.55f, 0.57f, 0.62f);
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
            int slotKind = slot / GameConfig.SkillSlotsPerKind;

            if ((int)def.kind != slotKind)
            {
                // 类别不符：优先放进同类空槽，全满则替换该类的第一个槽。
                int baseSlot = (int)def.kind * GameConfig.SkillSlotsPerKind;
                slot = baseSlot;
                for (int i = 0; i < GameConfig.SkillSlotsPerKind; i++)
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
        private void RefreshSkillBar(PlayerData p)
        {
            for (int i = 0; i < _skillSlotButtons.Length; i++)
            {
                int id = p.GetSkillSlot(i);

                if (id < 0 || id >= SkillCatalog.All.Length)
                {
                    _skillSlotLabels[i].text = "空";
                    _skillSlotLabels[i].color = new Color(0.48f, 0.5f, 0.56f);
                    _skillSlotStates[i].text = "";
                    _skillSlotButtons[i].image.color = new Color(0.18f, 0.19f, 0.24f);
                    _skillSlotFills[i].rectTransform.sizeDelta = new Vector2(0f, 6f);
                    continue;
                }

                SkillDef def = SkillCatalog.All[id];
                _skillSlotLabels[i].text = def.name;
                _skillSlotLabels[i].color = def.color;
                _skillSlotButtons[i].image.color =
                    new Color(def.color.r * 0.30f, def.color.g * 0.30f, def.color.b * 0.34f);

                float cd = def.IsActive ? _game.SkillCooldown(i) : 0f;
                float total = def.cooldown;

                _skillSlotStates[i].text = cd > 0f ? $"{cd:0.0}s" : "就绪";
                _skillSlotStates[i].color = cd > 0f ? new Color(0.72f, 0.82f, 1f) : new Color(0.5f, 1f, 0.65f);

                float ratio = total > 0f ? Mathf.Clamp01(1f - cd / total) : 1f;
                _skillSlotFills[i].rectTransform.sizeDelta = new Vector2(60f * ratio, 6f);
                _skillSlotFills[i].color = cd > 0f
                    ? new Color(0.45f, 0.7f, 1f, 0.9f)
                    : new Color(0.45f, 1f, 0.6f, 0.9f);
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
                $"技能面板 — 槽位 {sel + 1}（{KindShort(sel / GameConfig.SkillSlotsPerKind)}）" +
                $"    巅峰 {p.paragonLevel}    已获得技能 {p.OwnedSkillCount}/{SkillCatalog.All.Length}";

            for (int i = 0; i < SkillCatalog.All.Length; i++)
            {
                SkillDef def = SkillCatalog.All[i];
                bool equipped = p.FindSkillSlot((int)def.id) >= 0;
                bool unlocked = p.IsSkillOwned(def.id);
                bool inSlot = p.GetSkillSlot(sel) == i;

                string mark = inSlot ? "▶ " : (equipped ? "● " : "");
                string lockNote = unlocked ? "" : "（未获得）";
                _skillPoolLabels[i].text = $"{mark}{def.name}{lockNote}\n{def.description}";

                if (!unlocked) _skillPoolLabels[i].color = new Color(0.5f, 0.5f, 0.56f);
                else if (inSlot) _skillPoolLabels[i].color = new Color(1f, 0.95f, 0.6f);
                else if (equipped) _skillPoolLabels[i].color = new Color(0.62f, 1f, 0.72f);
                else _skillPoolLabels[i].color = Color.white;

                _skillPoolButtons[i].image.color = unlocked
                    ? new Color(0.20f, 0.22f, 0.28f)
                    : new Color(0.15f, 0.16f, 0.19f);
            }
        }

        private void OnBuyClicked() => _game.BuyPotion();

        private void OnAutoBuyClicked() => _game.ToggleAutoBuyPotion();

        /// <summary>刷新商店区：金币、价格、持有数、按钮可用态与提示文本。</summary>
        private void RefreshShop(PlayerData p)
        {
            int price = _game.PotionPrice;
            bool open = _game.IsShopOpen;

            _shopGoldText.text = $"金币 {p.gold}";
            _shopPotionText.text = $"生命药剂    价格 {price}    持有 {p.potionCount}/{GameConfig.PotionMaxCount}";

            bool canBuy = open && p.gold >= price && p.potionCount < GameConfig.PotionMaxCount;
            _buyButton.image.color = canBuy
                ? new Color(0.20f, 0.45f, 0.28f)
                : new Color(0.28f, 0.30f, 0.36f);

            _autoBuyLabel.text = _game.AutoBuyPotion ? "[✓] 自动购买" : "[  ] 自动购买";
            _autoBuyLabel.color = _game.AutoBuyPotion
                ? new Color(0.6f, 1f, 0.7f)
                : new Color(0.7f, 0.72f, 0.8f);

            // 开放时显示倒计时；关闭时显示提示（没有提示就说明当前不开放）。
            if (open)
            {
                _shopTipText.color = new Color(1f, 0.88f, 0.45f);
                _shopTipText.text = $"商店开放中（剩 {_game.ShopTimeLeft:0.0} 秒）—— 精英/Boss 即将出现";
            }
            else
            {
                _shopTipText.color = new Color(0.95f, 0.6f, 0.5f);
                _shopTipText.text = string.IsNullOrEmpty(_game.ShopMessage)
                    ? "商店只在精英/Boss 出现前开放"
                    : _game.ShopMessage;
            }
        }

        // ==================== 刷新 ====================

        private void Update()
        {
            if (_game == null) return;

            // P2：飘字与实时 DPS 都需要按帧推进。游戏时间 = 真实时间 × 速度倍率。
            float dt = Time.deltaTime * Mathf.Max(1f, _game.SpeedMultiplier);
            TickFloats(dt);
            RefreshBuffBar(_game.Player);
            RefreshDps(dt);
            ParseNewLogs();

            Refresh();
        }

        // ==================== P2：伤害飘字 ====================

        /// <summary>预建飘字池，避免战斗中反复创建 GameObject。</summary>
        private void BuildFloatPool(RectTransform layer)
        {
            _floatTexts = new Text[FloatPoolSize];
            _floatLife = new float[FloatPoolSize];
            _floatPos = new Vector2[FloatPoolSize];
            _floatVel = new Vector2[FloatPoolSize];
            for (int i = 0; i < FloatPoolSize; i++)
            {
                RectTransform r = CreateRect(layer, $"Float{i}", 0, 0, 220, 30);
                Text txt = r.gameObject.AddComponent<Text>();
                txt.font = _font;
                txt.fontSize = 20;
                txt.fontStyle = FontStyle.Bold;
                txt.alignment = TextAnchor.MiddleCenter;
                txt.raycastTarget = false;
                txt.supportRichText = false;
                txt.horizontalOverflow = HorizontalWrapMode.Overflow;
                txt.verticalOverflow = VerticalWrapMode.Overflow;
                txt.text = string.Empty;
                _floatTexts[i] = txt;
                _floatLife[i] = 0f;
            }
        }

        /// <summary>弹出一条飘字。x 是水平落点（相对飘字层左边缘）。</summary>
        private void SpawnFloat(string label, Color color, int size, float x)
        {
            if (_floatTexts == null) return;
            int i = _floatCursor;
            _floatCursor = (_floatCursor + 1) % FloatPoolSize;

            Text txt = _floatTexts[i];
            txt.text = label;
            txt.color = color;
            txt.fontSize = size;

            _floatLife[i] = 1.15f;
            // 注意：CreateRect 的坐标系是「y 向下为正」，所以这里存的是向下偏移量。
            _floatPos[i] = new Vector2(Mathf.Clamp(x, 60f, 700f), 128f + UnityEngine.Random.Range(0f, 108f));
            _floatVel[i] = new Vector2(UnityEngine.Random.Range(-14f, 14f), -54f);
            _floatTexts[i].rectTransform.anchoredPosition = new Vector2(_floatPos[i].x, -_floatPos[i].y);
        }

        private void TickFloats(float dt)
        {
            if (_floatTexts == null) return;
            for (int i = 0; i < FloatPoolSize; i++)
            {
                if (_floatLife[i] <= 0f) continue;

                _floatLife[i] -= dt;
                if (_floatLife[i] <= 0f)
                {
                    _floatTexts[i].text = string.Empty;
                    continue;
                }

                _floatPos[i] += _floatVel[i] * dt;
                _floatTexts[i].rectTransform.anchoredPosition = new Vector2(_floatPos[i].x, -_floatPos[i].y);

                Color c = _floatTexts[i].color;
                c.a = Mathf.Clamp01(_floatLife[i] / 0.5f);
                _floatTexts[i].color = c;
            }
        }

        // ==================== P2：Buff 栏 ====================

        private void RefreshBuffBar(PlayerData p)
        {
            if (_buffText == null || p == null) return;

            BattleSystem b = _game.Battle;
            var sb = _buffSb;
            sb.Clear();

            if (b != null)
            {
                if (b.BerserkTimeLeft > 0f)
                {
                    sb.Append("<color=#FF9A3C>◆ 狂暴 ");
                    sb.Append(b.BerserkTimeLeft.ToString("0.0"));
                    sb.Append("s</color>    ");
                }
                if (b.NearDeathTimeLeft > 0f)
                {
                    sb.Append("<color=#FF6B9D>◆ 免死 ");
                    sb.Append(b.NearDeathTimeLeft.ToString("0.0"));
                    sb.Append("s</color>    ");
                }
            }

            if (p.shield > 0)
            {
                sb.Append("<color=#7FE8FF>◆ 护盾 ");
                sb.Append(p.shield);
                sb.Append("</color>    ");
            }

            for (int slot = 0; slot < GameConfig.SkillSlotsPerKind; slot++)
            {
                int id = p.GetSkillSlot(slot);
                if (id < 0) continue;

                SkillDef def = SkillCatalog.Get((SkillId)id);
                float cd = _game.SkillCooldown(slot);
                if (cd > 0f)
                {
                    sb.Append("<color=#8A8F9A>");
                    sb.Append(def.name);
                    sb.Append(' ');
                    sb.Append(cd.ToString("0.0"));
                    sb.Append("s</color>    ");
                }
                else
                {
                    sb.Append("<color=#9CFF9C>");
                    sb.Append(def.name);
                    sb.Append(" 就绪</color>    ");
                }
            }

            _buffText.text = sb.Length == 0 ? "<color=#5F6470>（无状态）</color>" : sb.ToString();
        }

        // ==================== P2：实时 DPS ====================

        private void RefreshDps(float dt)
        {
            if (_dpsText == null) return;

            bool fighting = _game.CurrentState == GameController.State.Fighting;
            if (fighting && dt > 0f)
            {
                _dpsWindowTime += dt;
                if (_dpsWindowTime >= 0.4f)
                {
                    float instant = _dpsDamageAccum / _dpsWindowTime;
                    _dpsSmoothed = _dpsSmoothed <= 0f ? instant : Mathf.Lerp(_dpsSmoothed, instant, 0.55f);
                    _dpsDamageAccum = 0f;
                    _dpsWindowTime = 0f;
                }
            }

            string expect = string.Empty;
            if (fighting && _game.Monster != null)
            {
                expect = "    期望 " + EquipmentScore.Dps(_game.Player, _game.Monster).ToString("0");
            }

            _dpsText.text = _dpsSmoothed > 0.5f
                ? "实时 DPS " + _dpsSmoothed.ToString("0") + expect
                : "实时 DPS —" + expect;
        }

        // ==================== P2：日志 → 飘字 / DPS ====================

        private static readonly System.Text.RegularExpressions.Regex RxPlayerHit =
            new System.Text.RegularExpressions.Regex(@"^主角 → 怪物  (?:暴击 )?(\d+) 伤害");
        private static readonly System.Text.RegularExpressions.Regex RxMonsterHit =
            new System.Text.RegularExpressions.Regex(@"^怪物 → 主角  (?:暴击 )?(\d+) 伤害");
        private static readonly System.Text.RegularExpressions.Regex RxSkillHit =
            new System.Text.RegularExpressions.Regex(@"^✦ [^！]+！(\d+) 伤害");

        /// <summary>把新增的日志行翻译成飘字；顺带累计主角造成的伤害给 DPS 用。</summary>
        private void ParseNewLogs()
        {
            if (_game == null) return;

            int count = _game.Log.Count;
            if (_game.LogVersion == _lastParsedLogVersion)
            {
                _lastParsedLogCount = count;
                return;
            }
            _lastParsedLogVersion = _game.LogVersion;

            int added = count - _lastParsedLogCount;
            _lastParsedLogCount = count;
            if (added <= 0 || added > 40) return;

            for (int i = count - added; i < count; i++)
            {
                if (i < 0) continue;
                string msg = _game.Log[i].message;
                if (string.IsNullOrEmpty(msg)) continue;

                var mPlayer = RxPlayerHit.Match(msg);
                if (mPlayer.Success)
                {
                    bool crit = msg.Contains("暴击 ");
                    int dmg = int.Parse(mPlayer.Groups[1].Value);
                    SpawnFloat(crit ? dmg + "!" : dmg.ToString(),
                        crit ? new Color(1f, 0.82f, 0.28f) : new Color(0.92f, 0.92f, 0.95f),
                        crit ? 26 : 19,
                        200f + UnityEngine.Random.Range(-70f, 70f));
                    _dpsDamageAccum += dmg;
                    continue;
                }

                var mSkill = RxSkillHit.Match(msg);
                if (mSkill.Success)
                {
                    int dmg = int.Parse(mSkill.Groups[1].Value);
                    SpawnFloat(dmg.ToString(), new Color(0.55f, 0.92f, 1f), 24,
                        420f + UnityEngine.Random.Range(-70f, 70f));
                    _dpsDamageAccum += dmg;
                    continue;
                }

                var mMonster = RxMonsterHit.Match(msg);
                if (mMonster.Success)
                {
                    bool crit = msg.Contains("暴击 ");
                    int dmg = int.Parse(mMonster.Groups[1].Value);
                    SpawnFloat("-" + dmg, crit ? new Color(1f, 0.35f, 0.42f) : new Color(1f, 0.55f, 0.55f),
                        crit ? 24 : 18, 640f + UnityEngine.Random.Range(-70f, 70f));
                }
            }
        }

        private void Refresh()
        {
            // RefreshStagePanel 会自己判空，这里以前没有：一旦 Player 为 null，
            // 下面第一行取值就会每帧抛 NRE。两处空值策略必须一致。
            if (_game == null || _game.Player == null) return;
            PlayerData p = _game.Player;

            // ---- 角色属性 ----
            // 关卡制（步骤 4a/4b）：等级行只报等级，关卡与关卡怪级挪到右边的选关按钮上
            // （按钮本身就是入口，一举两得）。
            _levelText.text = p.paragonLevel > 0
                ? $"等级 {p.level}    巅峰 {p.paragonLevel}"
                : (p.IsMaxLevel ? $"等级 {p.level}（满级）" : $"等级 {p.level}");
            _hpText.text = $"生命  {p.currentHp} / {p.MaxHp}";
            SetBar(_hpFill, p.HpProgress);

            _atkText.text = $"攻击  {p.Attack}    (基础 {p.BaseAttack} + 装备 {(p.weapon != null ? p.weapon.attack : 0)})";
            _defText.text = $"防御  {p.Defense}    (基础 {p.BaseDefense} + 装备 {(p.armor != null ? p.armor.defense : 0)})";
            _spdText.text = $"速度  {p.Speed}    攻击间隔 {BattleSystem.PlayerAttackInterval(p):0.00}s";
            _critText.text = $"暴击  {p.CritChance * 100f:0.#}%    暴击伤害  {p.CritDamage * 100f:0}%    穿透  {p.ArmorPenetration * 100f:0.#}%";

            if (p.IsMaxLevel)
            {
                _expText.text = p.IsMaxParagon
                    ? "巅峰  已达上限"
                    : $"巅峰经验  {p.paragonExp} / {p.ParagonExpToNext}    已获得技能 {p.OwnedSkillCount}/{SkillCatalog.All.Length}";
            }
            else
            {
                _expText.text = $"经验  {p.exp} / {p.ExpToNextLevel}";
            }
            SetBar(_expFill, p.ExpProgress);

            RefreshEquipmentLines(p);

            _goldText.text = $"金币  {p.gold}";

            string paragonNote = p.paragonLevel > 0 ? $"    巅峰 {p.paragonLevel}" : "";
            // 压成 2 行：3 行时统计区要占 47px，会和上方的金币行抢位置（曾经重叠）。
            _statText.text =
                $"击杀 {_game.TotalKills}（精英 {_game.EliteKills} / BOSS {_game.BossKills}）    掉落 {_game.TotalDrops}    换装 {_game.TotalEquipped}    卖出 {_game.TotalSold}\n" +
                $"阵亡 {_game.TotalDeaths}    累计 {_game.TotalGoldEarned} 金币    状态 {StateName(_game.CurrentState)}    速度 x{_game.SpeedMultiplier:0}{paragonNote}";

            RefreshShop(p);
            RefreshSkillBar(p);
            RefreshSkillPanel();
            RefreshSkillChoice();
            RefreshStagePanel();
            RefreshSavePanel();
            RefreshBanner();

            // 存档按钮上的槽号：跟当前槽走，切完槽立刻反映出来。
            if (_saveButtonLabel != null)
            {
                _saveButtonLabel.text = $"存档 {_game.SaveSlot + 1}";
            }

            // ---- 怪物 ----
            MonsterData m = _game.Monster;
            if (m != null)
            {
                _monsterTitle.text = $"⚔ {m.Title}";
                _monsterHpText.text = m.shield > 0
                    ? $"{m.currentHp} / {m.maxHp}   （护盾 {m.shield}）"
                    : $"{m.currentHp} / {m.maxHp}";
                SetBar(_monsterHpFill, m.HpProgress);

                // 精英金框、Boss 红框，普通怪无边框。
                _monsterHpBorder.color = MonsterBorderColor(m.tier);

                string affixText = m.HasAffix ? $"    词缀：{m.AffixName}" : "";
                _monsterStatText.text =
                    $"攻击 {m.attack}    防御 {m.defense}    速度 {m.speed:0.#}    攻击间隔 {GameConfig.AttackInterval(m.speed):0.00}s\n" +
                    $"暴击率 {m.critChance * 100f:0.#}%    暴击伤害 {m.critDamage * 100f:0}%{affixText}";
            }
            else
            {
                _monsterTitle.text = _game.CurrentState == GameController.State.Reviving
                    ? "— 主角阵亡，等待自动复活 —"
                    : "— 等待下一只怪物 —";
                _monsterHpText.text = "";
                SetBar(_monsterHpFill, 0f);
                _monsterHpBorder.color = new Color(0f, 0f, 0f, 0f);
                _monsterStatText.text = "";
            }

            // ---- 按钮状态 ----
            _startButtonLabel.text = _game.IsRunning ? "暂停" : "开始";

            for (int i = 0; i < _speedButtons.Length; i++)
            {
                bool active = i == _game.SpeedIndex;
                _speedButtons[i].image.color = active
                    ? new Color(0.30f, 0.55f, 0.85f)
                    : new Color(0.22f, 0.24f, 0.32f);
                _speedLabels[i].color = active ? Color.white : new Color(0.7f, 0.72f, 0.8f);
            }

            RefreshLog();
        }

        /// <summary>只有日志确实变化时才重建文本，避免每帧拼接大字符串。</summary>
        private void RefreshLog()
        {
            if (_game.LogVersion == _lastLogVersion) return;
            _lastLogVersion = _game.LogVersion;

            var sb = new System.Text.StringBuilder(4096);
            int count = _game.Log.Count;
            int from = Mathf.Max(0, count - MaxVisibleLogLines);

            // 倒序：最新的一条在最上面。
            for (int i = count - 1; i >= from; i--)
            {
                LogEntry e = _game.Log[i];
                sb.Append("<color=#");
                sb.Append(ColorUtility.ToHtmlStringRGB(e.color));
                sb.Append('>');
                sb.Append(e.message);
                sb.Append("</color>\n");
            }

            _logText.text = sb.ToString();
        }

        /// <summary>
        /// 装备两行文字。评分依赖角色属性，但只随「装备 / 等级 / 巅峰」变化，
        /// 所以这四个值没动就不重算——否则每帧会白跑两次 EquipmentScore.Of（6 次改玩家字段）。
        /// </summary>
        private void RefreshEquipmentLines(PlayerData p)
        {
            if (!ReferenceEquals(_scoreCachedWeapon, p.weapon)
                || !ReferenceEquals(_scoreCachedArmor, p.armor)
                || _scoreCachedLevel != p.level
                || _scoreCachedParagon != p.paragonLevel)
            {
                _scoreCachedWeapon = p.weapon;
                _scoreCachedArmor = p.armor;
                _scoreCachedLevel = p.level;
                _scoreCachedParagon = p.paragonLevel;
                _scoreWeaponText = FormatEquipment("武器", p.weapon);
                _scoreArmorText = FormatEquipment("衣服", p.armor);
            }
            _weaponText.text = _scoreWeaponText;
            _armorText.text = _scoreArmorText;
        }

        private string FormatEquipment(string slotName, Equipment e)
        {
            if (e == null) return $"<color=#808080>{slotName}：空</color>";

            string hex = ColorUtility.ToHtmlStringRGB(e.QualityColor);
            string affixLine = string.IsNullOrEmpty(e.AffixLine) ? string.Empty : "\n    " + e.AffixLine;
            // 评分口径见 EquipmentScore：必须带上角色当前属性算，所以这里要 _game。
            float score = _game != null && _game.Player != null ? EquipmentScore.Of(_game.Player, e, e.slot) : 0f;
            // 评分放在标题行：主属性行本身已经很长，再挂评分会折行。
            return $"<color=#{hex}>{e.Title}    {e.MultiplierText}    评分 {score:0}\n    {e.StatLine}{affixLine}</color>";
        }

        /// <summary>血条边框颜色：精英金色、Boss 红色、普通怪透明（不显示边框）。</summary>
        private static Color MonsterBorderColor(MonsterTier tier)
        {
            switch (tier)
            {
                case MonsterTier.Elite: return new Color(1f, 0.82f, 0.25f);
                case MonsterTier.Boss: return new Color(1f, 0.25f, 0.2f);
                default: return new Color(0f, 0f, 0f, 0f);
            }
        }

        private static string StateName(GameController.State s)
        {
            switch (s)
            {
                case GameController.State.Fighting: return "战斗中";
                case GameController.State.WaitingRespawn: return "等待重生";
                case GameController.State.Reviving: return "复活中";
                default: return s.ToString();
            }
        }

        /// <summary>按比例设置填充条：用 anchorMax.x 实现，不改变父级布局。</summary>
        private static void SetBar(Image fill, float progress)
        {
            if (fill == null) return;
            RectTransform rt = fill.rectTransform;
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(Mathf.Clamp01(progress), 1f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        // ==================== 控件工厂 ====================

        private static RectTransform CreateRect(Transform parent, string name, float x, float y, float w, float h)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, -y); // y 向下为正，写起来更直观
            rt.sizeDelta = new Vector2(w, h);
            return rt;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private static Image CreateImage(Transform parent, string name, float x, float y, float w, float h, Color color, bool raycast = false)
        {
            RectTransform rt = CreateRect(parent, name, x, y, w, h);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = raycast;
            return img;
        }

        private RectTransform CreatePanel(Transform parent, string name, float x, float y, float w, float h, Color color)
        {
            return CreateImage(parent, name, x, y, w, h, color).rectTransform;
        }

        private Text CreateText(Transform parent, string name, string content, float x, float y, float w, float h,
            int fontSize, Color color, TextAnchor anchor, FontStyle style = FontStyle.Normal)
        {
            RectTransform rt = CreateRect(parent, name, x, y, w, h);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = _font;
            t.text = content;
            t.fontSize = fontSize;
            t.color = color;
            t.alignment = anchor;
            t.fontStyle = style;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.supportRichText = true;
            t.raycastTarget = false;
            return t;
        }

        /// <summary>背景条 + 填充条；返回填充条供刷新时改比例。</summary>
        private Image CreateBar(Transform parent, string name, float x, float y, float w, float h, Color bgColor, Color fillColor)
        {
            Image bg = CreateImage(parent, name, x, y, w, h, bgColor);
            Image fill = CreateImage(bg.rectTransform, name + "Fill", 0, 0, w, h, fillColor);
            Stretch(fill.rectTransform);
            return fill;
        }

        private Button CreateButton(Transform parent, string name, string label, float x, float y, float w, float h,
            Color bgColor, out Text labelText)
        {
            Image img = CreateImage(parent, name, x, y, w, h, bgColor, true);
            var btn = img.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;

            var colors = btn.colors;
            colors.highlightedColor = new Color(1.1f * bgColor.r, 1.1f * bgColor.g, 1.1f * bgColor.b, 1f);
            colors.pressedColor = new Color(0.8f * bgColor.r, 0.8f * bgColor.g, 0.8f * bgColor.b, 1f);
            btn.colors = colors;

            labelText = CreateText(img.rectTransform, name + "Label", label, 0, 0, w, h, 18, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            Stretch(labelText.rectTransform);

            return btn;
        }
    }
}

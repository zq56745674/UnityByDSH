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
        private Button[] _equipCards;
        private Text[] _equipTitles;
        private Text[] _equipStats;
        private Text[] _equipMetas;
        private Text _goldText;
        private Text _critText;
        private Text _statText;

        private Text _monsterTitle;
        private Text _monsterHpText;
        private Image _monsterHpFill;
        private Image _monsterHpTrack;
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
        private Text _buyButtonLabel;
        private Text _autoBuyLabel;

        // ---- 技能栏与技能面板 ----
        private Button[] _skillSlotButtons;
        private Text[] _skillSlotLabels;
        private Text[] _skillSlotStates;
        private Image[] _skillSlotFills;

        /// <summary>冷却条当前画出来的宽度（px）：用来避免每帧都改 RectTransform。</summary>
        private float[] _skillCdDraw;
        private Image[] _skillSlotAccents;

        private RectTransform _skillPanel;
        private Image _modalDim;
        private Text _skillPanelTitle;
        private Button[] _skillPoolButtons;
        private Text[] _skillPoolLabels;

        // ---- 技能三选一面板 ----
        private RectTransform _skillChoicePanel;
        private Text _skillChoiceTitle;
        private Button[] _skillChoiceButtons;
        private Text[] _skillChoiceLabels;
        private Image[] _choiceAccents;

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

        /// <summary>选关面板上次重建时的数据签名（面板开着时整局冻结，数据不变就不必重建）。</summary>
        private int _stagePanelSig;

        /// <summary>入口按钮上次显示的关卡键（chapter*100+stage），省掉每帧重写字符串。</summary>
        private int _stageButtonStageKey = int.MinValue;

        // ---- 存档槽面板 ----
        private RectTransform _savePanel;
        private Text _savePanelTitle;
        private Button[] _saveSlotButtons;
        private Text[] _saveSlotLabels;
        private Text[] _saveSlotDeleteLabels;
        private bool _savePanelWasOpen;
        private bool _savePanelDirty = true;
        private Text _saveButtonLabel;

        // ---- 破坏性操作（重置 / 删档）的二次确认 ----
        // 一次误点就把整局进度归零、或把一整个存档槽清空，代价远高于多点一次：
        // 第一次点击只把按钮文字换成「确认…」，ConfirmWindow 秒内再点同处才真正执行，
        // 超时自动复原（由 GameUI.Core.cs 的 TickConfirms 每帧收尾）。
        private const float ConfirmWindow = 3f;
        private Text _resetLabel;
        private float _resetConfirmUntil;
        private int _deleteConfirmSlot = -1;
        private float _deleteConfirmUntil;

        // ---- 装备强化面板（步骤 2）----
        private RectTransform _enhancePanel;
        private Text _enhancePanelTitle;
        private Button[] _enhanceSlotButtons;
        private Text[] _enhanceSlotLabels;
        private Text _enhanceCardLabel;
        private Button _enhanceActionButton;
        private Text _enhanceActionLabel;
        private int _enhanceSlotIndex;
        private bool _enhancePanelWasOpen;
        private bool _enhancePanelDirty = true;

        // ---- 装备洗练面板（步骤 3）----
        private RectTransform _reforgePanel;
        private Text _reforgePanelTitle;
        private Button[] _reforgeSlotButtons;
        private Text[] _reforgeSlotLabels;
        private Text _reforgeCardLabel;
        private Button[] _reforgeAffixButtons;
        private Text[] _reforgeAffixLabels;
        private Image[] _reforgeAffixStrips;
        private Text _reforgeQualityLabel;
        private Text _reforgeCostLabel;
        private Button _reforgeActionButton;
        private Text _reforgeActionLabel;
        private int _reforgeSlotIndex;
        // 锁词条：当前选中那件的 5 行词条按钮（实际只显示 AffixCount 行），点击即切换锁定。
        // 锁定状态只活在界面层、不进存档——它是「这次准备怎么洗」的临时意图，
        // 跨存档/跨装备携带反而危险（换装后锁住的种类可能根本不存在了）。
        private readonly int[] _reforgeLockMasks = new int[EquipmentSlots.Count];
        private readonly Equipment[] _reforgeLockOwners = new Equipment[EquipmentSlots.Count];
        private bool _reforgePanelWasOpen;
        private bool _reforgePanelDirty = true;

        // ---- 装备行缓存 ----
        // EquipmentScore.Of 为了算增量评分，内部会临时换装再还原：每次调用都会改玩家字段并重算属性。
        // 评分只可能随「装备 / 等级 / 巅峰 / 强化」
        // 变化，所以记住上次算过的这几个值，没变就直接复用字符串。
        // 强化等级必须进缓存键：强化是在**同一个 Equipment 对象**上改字段，
        // 引用比较察觉不到，漏掉它会出现「强化完装备行数字没动」的假象。
        // 洗练同理：它也在同一个对象上换掉全部副词条，所以次数（reforgeCount）也要进键。
        // 五槽的缓存键：装备引用、等级、强化、洗练、锁、评分开关与技能状态全部拼成签名。
        // 比旧的两槽逐字段比较更不容易漏项（洗练会原地改词条数值，引用不变）。
        private string _scoreCacheSignature = string.Empty;
        private readonly string[] _scoreEquipTitles = new string[EquipmentSlots.Count];
        private readonly string[] _scoreEquipStats = new string[EquipmentSlots.Count];
        private readonly string[] _scoreEquipMetas = new string[EquipmentSlots.Count];

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
        private int _floatLane;

        /// <summary>
        /// 伤害日志的解析游标（飘字 + DPS 共用）：记的是控制器侧的「累计写入条数」。
        /// 不能用 Log.Count 差值——日志满 60 条后差值恒为 0，飘字与 DPS 会永久停摆。
        /// 累计条数由 BattleLog 自己在唯一的写入实现里推进（战斗行走 AddCombat、系统行走 AddSystem），
    /// 调用方不需要、也不允许再补记。
        /// </summary>
        private int _lastParsedLogTotal;

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
            // 建完之后来一次「全量重建」：这时所有缓存都是初值，textTick=true 才写得出文本。
            Refresh(true);

            // 新档第一次进游戏自动走一遍教学；设置里「重看引导」也走同一个入口。
            // Refresh 之后才开，保证高亮框量到的目标都已经完成布局。
            if (!GameSettings.GuideDone) BeginGuide();
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
            // 布局全是写死坐标的（左 420 + 间距 + 右 824 = 1268 宽），所以必须保证整块
            // 1280×720 设计区永远完整可见。Expand = 取宽、高两个缩放比里较小的那个：
            // 视口比设计稿更宽或更高时多出来的部分留空，而不是把右侧面板挤出屏幕。
            // 原先 matchWidthOrHeight = 1（纯按高度缩放）：1280×800 下参考宽只剩 1152，
            // 右侧 824 宽的面板直接出屏 116px；纯按宽度缩放又会在 21:9 下切掉底部。
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            scaler.matchWidthOrHeight = 0.5f;

            // 背景铺满整个画布（不参与居中，否则宽屏下会露边）。
            Image bg = CreateImage(canvasGo.transform, "Background", 0, 0, 1280, 720, GameConfig.WindowBg, false);
            Stretch(bg.rectTransform);

            // 设计稿是写死坐标的 1280×720。Expand 模式下视口比 16:9 宽或高时，画布会比设计稿大，
            // 面板直接挂画布上就全贴左上角、右侧留一条空带。装进一个居中的 1280×720 容器，
            // 多出来的空间左右/上下均分，宽屏和 4:3 都不会偏。
            var contentGo = new GameObject("ContentRoot", typeof(RectTransform));
            RectTransform content = contentGo.GetComponent<RectTransform>();
            content.SetParent(canvasGo.transform, false);
            content.anchorMin = new Vector2(0.5f, 0.5f);
            content.anchorMax = new Vector2(0.5f, 0.5f);
            content.pivot = new Vector2(0.5f, 0.5f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = new Vector2(1280f, 720f);

            Transform root = content;

            BuildLeftPanel(root);
            BuildRightPanel(root);

            // 浮层遮罩：建在所有常驻面板之后、所有浮层之前，靠层级顺序自然压在面板上、
            // 又被浮层压住。开着时吃点击，避免点到遮罩下面的按钮。
            _modalDim = CreateImage(root, "ModalDim", 0, 0, 1280, 720, new Color(0f, 0f, 0f, 0.55f), true);
            Stretch(_modalDim.rectTransform);
            _modalDim.gameObject.SetActive(false);

            BuildSkillPanel(root);
            BuildSkillChoicePanel(root);
            BuildStagePanel(root);
            BuildSavePanel(root);
            BuildEnhancePanel(root);
            BuildReforgePanel(root);
            BuildSettingsPanel(root);
            BuildComparePanel(root);
            BuildEndlessPanel(root);
            BuildRebirthPanel(root);
            BuildAchievements(root);
            // 引导最后建（横幅之下另算）：它的遮罩与高亮框要压住所有浮层。
            BuildGuide(root);
            // 横幅必须压住选关 / 技能面板；成就 toast 再建在横幅之后，保证它是最上层提示。
            BuildBanner(root);
            BuildAchievementToast(root);
        }

        // ==================== 左侧：属性 + 装备 ====================

        /// <summary>
        /// 主角立绘（`Assets/Resources/HeroPortrait.png`，803×1024 透明底全身像）。
        /// 它以前被当成半透明底纹铺在装备区、横穿文字，看着像贴纸；现在给一个正经的立绘框，
        /// 摆在左面板左上角，属性列挪到框右边（列坐标见 BuildLeftPanel 的 colX/colW）。
        /// 框做成 140×179 是贴着源图比例（0.782 vs 0.784）算的：preserveAspect 下既不留边也不裁切。
        /// 必须在属性文字之前建——文字那几行虽然起点在 x=168，但宽行（暴击/经验）是整幅宽的，
        /// 顺序反过来会被立绘盖住一截。
        /// </summary>
        private void BuildHeroPortrait(Transform panel)
        {
            Sprite hero = Resources.Load<Sprite>("HeroPortrait");
            if (hero == null)
            {
                Debug.LogWarning("[GameUI] 找不到 Resources/HeroPortrait.png，主角立绘跳过。");
                return;
            }

            const float x = 16f, y = 44f, w = 140f, h = 179f;

            // 先垫卡再放图：卡片给一层深色底和 1px 描边，图的透明背景才不会直接透到面板底色上，
            // 视觉上才是「装裱」而不是「贴上去」。
            CreateCard(panel, "PortraitCard", x, y, w, h, GameConfig.CardBg);

            // 卡底和面板底几乎同色，不打光的话立绘整块「陷」进去，像一张暗色贴纸；
            // 垫一层冷色光晕，人才会从底上浮起来。光晕留在框内，不外溢到面板上。
            Image glow = CreateImage(panel, "PortraitGlow", x + 2f, y + 2f, w - 4f, h - 4f,
                new Color(0.30f, 0.50f, 0.95f, 0.22f));
            glow.sprite = GlowSprite();

            // 脚下一小滩「地灯」：人物是站在框里的，不给底部一点光会像悬空。
            Image floorLight = CreateImage(panel, "PortraitFloor", x + 12f, y + h - 52f, w - 24f, 54f,
                new Color(0.40f, 0.60f, 1f, 0.20f));
            floorLight.sprite = GlowSprite();

            Image img = CreateImage(panel, "HeroPortrait", x, y, w, h, Color.white);
            img.sprite = hero;
            img.preserveAspect = true;

            // 待机动画：呼吸/浮动/眨眼。立绘本体只有一张图 + 一张只闭眼的衍生帧，
            // 变换幅度在 GameUI.Portrait.cs 里统一控制，避免在构建代码里散落魔法数。
            SetupHeroPortrait(img, glow, floorLight);
        }

        private void BuildLeftPanel(Transform root)
        {
            RectTransform panel = CreatePanel(root, "LeftPanel", 12, 12, LeftPanelW, PanelH, GameConfig.PanelBg);
            _leftPanel = panel;

            // 立绘占左上角，属性改成「L 形」两块：窄列贴在立绘右边，宽的暴击/经验行等立绘结束再铺开。
            const float colX = 168f, colW = 216f;
            BuildHeroPortrait(panel);

            CreateSectionTitle(panel, "角色属性", 16, 10, 260, FontTitle);

            // 装备区与金币区各垫一张卡：左栏文字多，没有底衬就是一整片同色的文字墙。
            _equipCard = CreateCard(panel, "EquipCard", 12, 320, 396, 192, GameConfig.CardBg);
            CreateCard(panel, "WalletCard", 12, 516, 396, 80, GameConfig.CardBg);

            // 存档入口：挤在标题行右侧。CreateText 的 raycastTarget 是关的，
            // 所以这里不用管两行文字的矩形重叠；标题实际只占左边约 90px。
            Button saveBtn = CreateButton(panel, "SaveButton", "存档 1", 300, 15, 88, 24,
                GameConfig.BtnHeader, out _saveButtonLabel);
            _saveButtonLabel.fontSize = 14;
            saveBtn.onClick.AddListener(OnSaveButtonClicked);

            // 设置入口：标题行左侧。标题「角色属性」右侧到存档按钮之间还有 100 多 px，
            // 72 宽刚好放得下「⚙ 设置」，并且和存档按钮之间留 8px 缝。
            _settingsButton = CreateButton(panel, "SettingsButton", "⚙ 设置", 220, 15, 72, 24,
                GameConfig.BtnHeader, out _settingsButtonLabel);
            _settingsButtonLabel.fontSize = 14;
            _settingsButton.onClick.AddListener(OnSettingsClicked);

            // 成就入口：挤在标题与设置之间。标题文字实际只占左边约 100px，140 起不压字。
            _achievementButton = CreateButton(panel, "AchievementButton", "🏆 成就", 140, 15, 72, 24,
                GameConfig.BtnHeader, out _achievementButtonLabel);
            _achievementButtonLabel.fontSize = 13;
            _achievementButton.onClick.AddListener(OnAchievementButtonClicked);

            _levelText = CreateText(panel, "Level", "", colX, 46, colW, 26, 17, Color.white, TextAnchor.MiddleLeft, FontStyle.Bold);

            // 选关入口：立绘右侧那列的等级行下面。4b 时把关卡信息从等级行里拿出来做成按钮，
            // 顺便让等级行不至于被「关卡 11-10 · 怪 Lv.110」撑爆。
            _stageButton = CreateButton(panel, "StageButton", "", colX, 76, 150, 24,
                GameConfig.BtnHeader, out _stageButtonLabel);
            _stageButtonLabel.fontSize = 13;
            _stageButtonLabel.alignment = TextAnchor.MiddleCenter;
            _stageButton.onClick.AddListener(OnStageButtonClicked);

            // 无尽入口：贴在关卡按钮右边。60 宽只放得下「无尽 / 记录」两个短词。
            _endlessButton = CreateButton(panel, "EndlessButton", "无尽", 324, 76, 60, 24,
                GameConfig.BtnHeader, out _endlessButtonLabel);
            _endlessButtonLabel.fontSize = 13;
            _endlessButtonLabel.alignment = TextAnchor.MiddleCenter;
            _endlessButton.onClick.AddListener(OnEndlessButtonClicked);

            _hpText = CreateText(panel, "HpText", "", colX, 104, colW, 20, 15, GameConfig.TextNormal, TextAnchor.MiddleLeft);
            _hpFill = CreateBar(panel, "HpBar", colX, 126, colW, 16, GameConfig.BarTrackHp, GameConfig.FillHp);

            _atkText = CreateText(panel, "Atk", "", colX, 146, colW, 22, FontSmall + 1, GameConfig.TextAttack, TextAnchor.MiddleLeft);
            _defText = CreateText(panel, "Def", "", colX, 172, colW, 22, FontSmall + 1, GameConfig.TextDefense, TextAnchor.MiddleLeft);
            _spdText = CreateText(panel, "Spd", "", colX, 198, colW, 22, FontSmall + 1, GameConfig.TextSpeed, TextAnchor.MiddleLeft);

            _critText = CreateText(panel, "Crit", "", 16, 226, 388, 24, 15, GameConfig.TextCrit, TextAnchor.MiddleLeft);

            _expText = CreateText(panel, "ExpText", "", 16, 250, 388, 20, 15, GameConfig.TextExp, TextAnchor.MiddleLeft);
            _expFill = CreateBar(panel, "ExpBar", 16, 272, 388, 14, GameConfig.BarTrackExp, GameConfig.FillExp);

            CreateImage(panel, "Divider", 16, 292, 388, 2, GameConfig.Divider);

            CreateSectionTitle(panel, "装备", 16, 300, 260, FontSection);

            // 强化 / 洗练入口。放在装备标题行右侧：标题「装备」只占约 40px，右边整片是空的。
            // 装备区本身已经排满（武器 334~420 / 衣服 424~510），塞不下每件一个按钮，
            // 所以两个操作都走浮层：点这里开面板，在面板里挑武器还是衣服。
            // 两个按钮各 76 宽并排（240 / 322），合起来正好落在原「强化」那 92px 占位的右侧，
            // 与顶部「存档」按钮右对齐（398 ≈ 388 + 10）。
            // 对比入口放在「装备」标题右侧、强化左边：没有待选掉落时压暗不可点，有待选时亮起。
            _compareButton = CreateButton(panel, "CompareEntry", "对比", 164, 303, 68, 26,
                GameConfig.BtnHeader, out _compareButtonLabel);
            _compareButtonLabel.fontSize = 13;
            _compareButton.onClick.AddListener(OnCompareEntryClicked);

            Button enhanceEntry = CreateButton(panel, "EnhanceEntry", "强化", 240, 303, 76, 26,
                GameConfig.BtnGold, out _);
            enhanceEntry.onClick.AddListener(OnEnhanceEntryClicked);

            Button reforgeEntry = CreateButton(panel, "ReforgeEntry", "洗练", 322, 303, 76, 26,
                GameConfig.BtnHeader, out _);
            reforgeEntry.onClick.AddListener(OnReforgeEntryClicked);

            // 五槽改成 2 列 × 3 行紧凑卡（190×58）。两张大卡（396 宽）放不下头盔/鞋/饰品，
            // 而且大卡里三行属性会把 192 高的装备区撑爆。卡面只留「标题 / 主属性 / 倍率+评分」，
            // 完整词条与逐项差走对比面板（点卡即开）。
            _equipCards = new Button[EquipmentSlots.Count];
            _equipTitles = new Text[EquipmentSlots.Count];
            _equipStats = new Text[EquipmentSlots.Count];
            _equipMetas = new Text[EquipmentSlots.Count];
            for (int i = 0; i < EquipmentSlots.Count; i++)
            {
                EquipmentSlot slot = EquipmentSlots.All[i];
                float cardX = 20f + (i % 2) * 198f;
                float cardY = 328f + (i / 2) * 64f;
                Button card = CreateButton(panel, "EquipCard" + slot, "", cardX, cardY, 190, 58,
                    GameConfig.CardBg, out Text hiddenLabel);
                hiddenLabel.text = string.Empty;
                int captured = i;
                card.onClick.AddListener(delegate { OnEquipCardClicked(captured); });
                _equipCards[i] = card;

                _equipTitles[i] = CreateText(card.transform, "Title", "", 8, 4, 118, 16, 12,
                    Color.white, TextAnchor.MiddleLeft, FontStyle.Bold);
                _equipMetas[i] = CreateText(card.transform, "Meta", "", 118, 4, 64, 16, 10,
                    GameConfig.TextMuted, TextAnchor.MiddleRight);
                _equipMetas[i].horizontalOverflow = HorizontalWrapMode.Overflow;
                _equipStats[i] = CreateText(card.transform, "Stats", "", 8, 21, 174, 33, 10,
                    GameConfig.TextNormal, TextAnchor.UpperLeft);
                _equipStats[i].lineSpacing = 1.02f;
            }

            CreateImage(panel, "Divider2", 16, 514, 388, 2, GameConfig.Divider);
            _goldText = CreateText(panel, "Gold", "", 24, 518, 376, 26, 19, GameConfig.TextGold, TextAnchor.MiddleLeft, FontStyle.Bold);

            // 统计压到 3 行，给底部技能栏腾位置。
            // 统计只占 2 行，紧跟金币行下方，互不重叠。
            _statText = CreateText(panel, "Stats", "", 22, 546, 378, 44, FontSmall, GameConfig.TextMuted, TextAnchor.UpperLeft);
            _statText.lineSpacing = 1.12f;

            // 引导高亮用的整块技能栏矩形：先建它、再建 6 个槽，槽位就压在它上面，
            // 高亮时框住整块、又不会挡住槽位自己的点击（它本身不接收 raycast，只是个定位锚）。
            _skillBarRoot = CreateRect(panel, "SkillBarRoot", 16, 592, 388, 96);
            BuildSkillBar(panel);
        }

        /// <summary>
        /// 技能栏：6 个槽位（主动 2 / 被动 2 / 触发 2），横向铺满左面板宽度。
        /// 槽位内自带「主动 / 被动 / 触发」小标签，所以不再单独放标题。
        /// </summary>
        private void BuildSkillBar(Transform panel)
        {
            const int slotCount = PlayerData.SkillSlotCount;
            int perKind = PlayerData.SkillSlotsPerKind;        // 2
            int kindCount = slotCount / perKind;               // 3
            const float slotW = 126f;
            const float slotH = 42f;
            const float stepX = 132f;
            const float rowY = 598f;
            const float rowStep = 46f;

            // 三列（主动 / 被动 / 触发）× 两行。类别标签放进格内左上角，
            // 不再单独占一行列头——那一行正好会压在统计文字上。

            _skillSlotButtons = new Button[slotCount];
            _skillSlotLabels = new Text[slotCount];
            _skillSlotStates = new Text[slotCount];
            _skillSlotFills = new Image[slotCount];
            _skillSlotAccents = new Image[slotCount];

            for (int i = 0; i < slotCount; i++)
            {
                int captured = i;
                int kind = i / perKind;      // 列：0 主动 / 1 被动 / 2 触发
                int row = i % perKind;       // 行
                float x = 16f + kind * stepX;
                float y = rowY + row * rowStep;

                _skillSlotButtons[i] = CreateButton(panel, $"SkillSlot{i}", "", x, y, slotW, slotH,
                    GameConfig.RowBg, out _skillSlotLabels[i]);

                // 左侧强调条：有技能时显示技能品质色，空槽时几乎看不见。
                // 它比「整块染成技能色」克制得多——原来 6 个槽一起变色像调色板。
                _skillSlotAccents[i] = CreateImage(_skillSlotButtons[i].transform, $"SkillAccent{i}",
                    0f, 8f, 3f, slotH - 16f, GameConfig.Border);

                // 类别标签（左上角，9 号灰字）：同类的两个槽上下相邻，扫一眼就知道这列是什么。
                CreateText(_skillSlotButtons[i].transform, $"SkillKind{i}", KindShort(kind),
                    7f, 5f, 48f, 12f, 9, GameConfig.TextMuted, TextAnchor.UpperLeft);

                // 技能名（中间主行）。
                _skillSlotLabels[i].fontSize = 13;
                _skillSlotLabels[i].alignment = TextAnchor.MiddleCenter;
                _skillSlotLabels[i].rectTransform.offsetMin = new Vector2(4f, 18f);
                _skillSlotLabels[i].rectTransform.offsetMax = new Vector2(-4f, -5f);

                // 冷却秒数 / 就绪（右上角）。
                _skillSlotStates[i] = CreateText(_skillSlotButtons[i].transform, $"SkillState{i}", "",
                    50f, 5f, slotW - 57f, 12f, 10, GameConfig.TextInfo, TextAnchor.UpperRight);

                // 底部冷却进度条。
                Image barBg = CreateImage(_skillSlotButtons[i].transform, $"SkillCdBar{i}",
                    10f, 36f, slotW - 20f, 3f, GameConfig.ShadeOverlay);
                _skillSlotFills[i] = CreateImage(barBg.rectTransform, "Fill", 0, 0, 0, 3,
                    GameConfig.AccentSlotReady);

                RectTransform fill = _skillSlotFills[i].rectTransform;
                fill.anchorMin = new Vector2(0f, 0.5f);
                fill.anchorMax = new Vector2(0f, 0.5f);
                fill.pivot = new Vector2(0f, 0.5f);
                fill.anchoredPosition = Vector2.zero;
                fill.sizeDelta = new Vector2(0f, 3f);

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

            _logCard = CreateCard(panel, "LogCard", 8, 154, 808, 332, GameConfig.InsetBg, RadiusCard, GameConfig.BorderSoft);
            CreateSectionTitle(panel, "战斗日志", 20, 160, 300, FontSection);

            // ---- 实时 DPS（步骤 8）：做成右侧的小胶囊，和左侧标题同一行 ----
            _dpsPill = CreateCard(panel, "DpsPill", 592, 163, 216, 26, GameConfig.RowBg, 13, GameConfig.BorderSoft);
            _dpsText = CreateText(_dpsPill.rectTransform, "DpsText", "", 0, 0, 216, 26, 14, GameConfig.TextMint, TextAnchor.MiddleCenter);
            Stretch(_dpsText.rectTransform);

            RectTransform logRect = CreateRect(panel, "LogText", 20, 196, 784, 282);

            // ---- 伤害飘字层（步骤 7）：不参与布局 ----
            // 覆盖怪物卡到日志区：伤害数字落在怪物血条上（打的是谁，数字就在谁身上），
            // 而不是糊在日志文字里——日志是要读的，飘字只是余光。
            _floatLayer = CreateRect(panel, "FloatLayer", 8, 20, 800, 460);
            BuildFloatPool(_floatLayer);
            _logText = logRect.gameObject.AddComponent<Text>();
            _logText.font = _font;
            _logText.fontSize = 14;
            _logText.lineSpacing = 1.28f;
            _logText.color = Color.white;
            _logText.alignment = TextAnchor.UpperLeft;
            _logText.horizontalOverflow = HorizontalWrapMode.Wrap;
            _logText.verticalOverflow = VerticalWrapMode.Truncate;
            _logText.supportRichText = true;
            _logText.lineSpacing = 1.1f;

            BuildShopPanel(panel);

            // ---- 按钮区 ----
            _startButton = CreateButton(panel, "StartButton", "开始", 16, 612, 200, 56, GameConfig.BtnPrimary, out _startButtonLabel);
            _startButton.onClick.AddListener(OnStartClicked);

            var speedLabels = new string[GameConfig.SpeedOptions.Length];
            for (int i = 0; i < GameConfig.SpeedOptions.Length; i++)
                speedLabels[i] = $"x{GameConfig.SpeedOptions[i]:0}";

            _speedButtons = new Button[GameConfig.SpeedOptions.Length];
            _speedLabels = new Text[GameConfig.SpeedOptions.Length];
            for (int i = 0; i < GameConfig.SpeedOptions.Length; i++)
            {
                int captured = i; // 闭包捕获
                _speedButtons[i] = CreateButton(panel, $"Speed{i}", speedLabels[i], 232 + i * 88, 612, 84, 56,
                    GameConfig.BtnNeutral, out _speedLabels[i]);
                _speedButtons[i].onClick.AddListener(() => _game.SetSpeedIndex(captured));
            }

            Button reset = CreateButton(panel, "ResetButton", "重置", 700, 612, 108, 56, GameConfig.RowBg, out _resetLabel);
            _resetLabel.color = new Color(0.95f, 0.55f, 0.55f);
            reset.onClick.AddListener(OnResetClicked);
        }

        /// <summary>商店面板：目前只卖生命药剂（价格随主角等级变化）。</summary>
        private void BuildShopPanel(Transform panel)
        {
            _shopCard = CreateCard(panel, "ShopCard", 8, 492, 808, 116, GameConfig.InsetBg);

            CreateText(panel, "ShopTitle", "商店", 28, 498, 70, 28, 18,
                GameConfig.TextTitle, TextAnchor.MiddleLeft, FontStyle.Bold);

            _shopGoldText = CreateText(panel, "ShopGold", "", 100, 498, 170, 28, 16,
                GameConfig.TextGold, TextAnchor.MiddleLeft, FontStyle.Bold);

            _shopPotionText = CreateText(panel, "ShopPotion", "", 274, 498, 280, 28, 15,
                Color.white, TextAnchor.MiddleLeft);

            _buyButton = CreateButton(panel, "BuyPotion", "购买", 556, 496, 88, 32,
                GameConfig.BtnPrimary, out _buyButtonLabel);
            _buyButton.onClick.AddListener(OnBuyClicked);

            Button autoBuy = CreateButton(panel, "AutoBuy", "", 652, 496, 144, 32,
                GameConfig.BtnNeutral, out _autoBuyLabel);
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
                if (_resetLabel != null) { _resetLabel.text = "重置"; _resetLabel.fontSize = 18; }
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

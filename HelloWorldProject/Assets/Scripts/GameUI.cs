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

        private GameController _game;
        private Font _font;

        // ---- 需要在刷新时更新的控件 ----
        private Text _levelText;
        private Text _titleText;
        private Text _hpText;
        private Image _hpFill;
        private Text _atkText;
        private Text _defText;
        private Text _spdText;
        private Text _expText;
        private Image _expFill;
        private Button[] _equipCards;
        private Image[] _equipIcons;
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
        private Image _startButtonIcon;
        private Button[] _speedButtons;
        private Text[] _speedLabels;
        private Image[] _speedIcons;

        // ---- 商店 ----
        private Text _shopGoldText;
        private Text _shopPotionText;
        private Text _shopTipText;
        private Button _buyButton;
        private Text _buyButtonLabel;
        private Image _buyIcon;
        private Text _autoBuyLabel;
        private Image _autoBuyIcon;
        private Button _autoBuyButton;

        // ---- 技能栏与技能面板 ----
        private Button[] _skillSlotButtons;
        private Text[] _skillSlotLabels;
        private Text[] _skillSlotStates;
        private Image[] _skillSlotFills;
        private Image[] _skillSlotIcons;

        /// <summary>冷却条当前画出来的宽度（px）：用来避免每帧都改 RectTransform。</summary>
        private float[] _skillCdDraw;
        private Image[] _skillSlotAccents;

        private RectTransform _skillPanel;
        private Image _modalDim;
        private Text _skillPanelTitle;
        private Button[] _skillPoolButtons;
        private Text[] _skillPoolLabels;
        private Image[] _skillPoolIcons;

        // ---- 技能三选一面板 ----
        private RectTransform _skillChoicePanel;
        private Text _skillChoiceTitle;
        private Button[] _skillChoiceButtons;
        private Text[] _skillChoiceLabels;
        private Image[] _choiceAccents;
        private Image[] _skillChoiceIcons;

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

        // 存档重命名 / 跨槽复制（2026-10-24 C10）：两个小弹层，都挂在存档面板内部。
        private RectTransform _saveNamePanel;
        private Text _saveNameTitle;
        private InputField _saveNameInput;
        private Text _saveNameHint;
        private int _saveNameSlot = -1;
        private RectTransform _saveCopyPanel;
        private Text _saveCopyTitle;
        private Button[] _saveCopyButtons;
        private Text[] _saveCopyLabels;
        private Text _saveCopyHint;
        private int _saveCopySource = -1;
        private int _saveCopyConfirmSlot = -1;
        private float _saveCopyConfirmUntil;
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
        private Image _enhanceActionIcon;
        private Button _enhanceBulk5Button;
        private Text _enhanceBulk5Label;
        private Button _enhanceBulkMaxButton;
        private Text _enhanceBulkMaxLabel;
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
        private Text _reforgeInfoText;
        private Text _reforgeQualityLabel;
        private Text _reforgeCostLabel;
        private Button _reforgeActionButton;
        private Text _reforgeActionLabel;
        private Image _reforgeActionIcon;
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
            // 舞台要在第一帧战斗之前就订上事件，否则第一只怪的入场特效会漏掉。
            EnsureBattleVisualHook();

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
            BuildMusicPanel(root);
            BuildAboutPanel(root);
            BuildDisplayPanel(root);
            BuildComparePanel(root);
            BuildEndlessPanel(root);
            BuildRebirthPanel(root);
            BuildAchievements(root);
            // 引导最后建（横幅之下另算）：它的遮罩与高亮框要压住所有浮层。
            BuildGuide(root);
            // 横幅必须压住选关 / 技能面板；成就 toast 再建在横幅之后，保证它是最上层提示。
            BuildBanner(root);
            BuildAchievementToast(root);
            // 悬停名称必须最后建：否则会被横幅/成就 toast 压住。
            BuildTooltip(root);
        }

    }
}

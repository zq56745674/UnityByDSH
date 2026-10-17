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
        private Button[] _enhanceActionButtons;
        private Text[] _enhanceActionLabels;
        private bool _enhancePanelWasOpen;
        private bool _enhancePanelDirty = true;

        // ---- 装备行缓存 ----
        // EquipmentScore.Of 为了算增量评分，内部会临时换装再还原：每次调用 = 3 次改
        // PlayerData.weapon/armor + 2 次属性重算。评分只可能随「装备 / 等级 / 巅峰 / 强化」
        // 变化，所以记住上次算过的这几个值，没变就直接复用字符串。
        // 强化等级必须进缓存键：强化是在**同一个 Equipment 对象**上改字段，
        // 引用比较察觉不到，漏掉它会出现「强化完装备行数字没动」的假象。
        private Equipment _scoreCachedWeapon;
        private Equipment _scoreCachedArmor;
        private int _scoreCachedLevel = -1;
        private int _scoreCachedParagon = -1;
        private int _scoreCachedWeaponEnhance = -1;
        private int _scoreCachedArmorEnhance = -1;
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

        /// <summary>
        /// 伤害日志的解析游标（飘字 + DPS 共用）：记的是控制器侧的「累计写入条数」。
        /// 不能用 Log.Count 差值——日志满 60 条后差值恒为 0，飘字与 DPS 会永久停摆。
        /// 战斗系统的伤害行是直接 append 进 Log 的，累计条数在 GameController.TickFighting 里补记。
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
            // 布局全是写死坐标的（左 420 + 间距 + 右 824 = 1268 宽），所以必须保证整块
            // 1280×720 设计区永远完整可见。Expand = 取宽、高两个缩放比里较小的那个：
            // 视口比设计稿更宽或更高时多出来的部分留空，而不是把右侧面板挤出屏幕。
            // 原先 matchWidthOrHeight = 1（纯按高度缩放）：1280×800 下参考宽只剩 1152，
            // 右侧 824 宽的面板直接出屏 116px；纯按宽度缩放又会在 21:9 下切掉底部。
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            scaler.matchWidthOrHeight = 0.5f;

            Transform root = canvasGo.transform;

            // 背景
            Image bg = CreateImage(root, "Background", 0, 0, 1280, 720, GameConfig.WindowBg, false);
            Stretch(bg.rectTransform);

            BuildLeftPanel(root);
            BuildRightPanel(root);
            BuildSkillPanel(root);
            BuildSkillChoicePanel(root);
            BuildStagePanel(root);
            BuildSavePanel(root);
            BuildEnhancePanel(root);
            // 横幅必须最后建：它要盖在选关 / 技能面板之上。
            BuildBanner(root);
        }

        // ==================== 左侧：属性 + 装备 ====================

        /// <summary>
        /// 主角立绘（`Assets/Resources/HeroPortraitFade.png`）：铺在左面板右半的装备区当底纹。
        /// 左面板内容一直排到 y≈648，塞不下独立的立绘框，所以用背景水印的方式让立绘进画面。
        /// 位置不是随手挑的：左面板上段有两条整幅宽的进度条和一个实心按钮，立绘落在那里会被横切，
        /// 所以整块挪到了它们下方的空白带（具体坐标推导见方法内注释）。
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
            //
            // 位置是「躲开所有不透明控件」算出来的，改之前先看这张左面板的 y 占位表：
            //   y 236~258 经验文字 / 260~278 经验条（整幅宽）← 横切立绘的元凶之一
            //   y 292~294 分隔线 / 302~330 装备标题行 / 303~329 「强化」按钮 ← 元凶之二
            //   y 334~420 武器行 / 424~510 衣服行（文字实际只到 x≈270，右半是空的）
            //   y 514~516 分隔线 / 516~544 金币行 / 548~582 统计行 / 586 分隔线 + 技能栏
            // 所以能整块放下的只有 y 332~548 这条右半带：上不碰经验条与强化按钮，
            // 下不碰金币与统计（那两行虽然是全宽矩形，但文字左对齐、实际不过 x≈260）。
            // 纵向压到 216 高就是为了卡在 y=548 之前——立绘一多出来就会盖住统计文字的右半。
            // 横向右移到 x=226：装备行的文字到 x≈270 为止，这样重叠只剩淡出边缘那 40 多像素。
            Image img = CreateImage(panel, "HeroPortrait", 226, 332, 169, 216, Color.white);
            img.sprite = hero;
            img.preserveAspect = true;
            img.color = GameConfig.ImageHalf;
        }

        private void BuildLeftPanel(Transform root)
        {
            RectTransform panel = CreatePanel(root, "LeftPanel", 12, 12, LeftPanelW, PanelH, GameConfig.PanelBg);

            // 立绘必须最先建：它是底纹，要压在所有文字下面。
            BuildHeroPortrait(panel);

            CreateText(panel, "Title", "角色属性", 16, 12, 260, 30, 20, GameConfig.TextTitle, TextAnchor.MiddleLeft, FontStyle.Bold);

            // 存档入口：挤在标题行右侧。CreateText 的 raycastTarget 是关的，
            // 所以这里不用管两行文字的矩形重叠；标题实际只占左边约 90px。
            Button saveBtn = CreateButton(panel, "SaveButton", "存档 1", 300, 15, 88, 24,
                GameConfig.BtnHeader, out _saveButtonLabel);
            _saveButtonLabel.fontSize = 14;
            saveBtn.onClick.AddListener(OnSaveButtonClicked);

            _levelText = CreateText(panel, "Level", "", 16, 48, 224, 26, 17, Color.white, TextAnchor.MiddleLeft, FontStyle.Bold);

            // 选关入口：等级行右侧。4a 时关卡信息塞在等级行里，4b 挪到这里当按钮，
            // 顺便让等级行不至于被「关卡 11-10 · 怪 Lv.110」撑爆。
            _stageButton = CreateButton(panel, "StageButton", "", 248, 48, 156, 26,
                GameConfig.BtnHeader, out _stageButtonLabel);
            _stageButtonLabel.fontSize = 13;
            _stageButtonLabel.alignment = TextAnchor.MiddleCenter;
            _stageButton.onClick.AddListener(OnStageButtonClicked);

            _hpText = CreateText(panel, "HpText", "", 16, 78, 388, 22, 15, GameConfig.TextNormal, TextAnchor.MiddleLeft);
            _hpFill = CreateBar(panel, "HpBar", 16, 104, 388, 18, GameConfig.BarTrackHp, GameConfig.FillHp);

            _atkText = CreateText(panel, "Atk", "", 16, 130, 388, 24, 15, GameConfig.TextAttack, TextAnchor.MiddleLeft);
            _defText = CreateText(panel, "Def", "", 16, 156, 388, 24, 15, GameConfig.TextDefense, TextAnchor.MiddleLeft);
            _spdText = CreateText(panel, "Spd", "", 16, 182, 388, 24, 15, GameConfig.TextSpeed, TextAnchor.MiddleLeft);

            _critText = CreateText(panel, "Crit", "", 16, 208, 388, 24, 15, GameConfig.TextCrit, TextAnchor.MiddleLeft);

            _expText = CreateText(panel, "ExpText", "", 16, 236, 388, 22, 15, GameConfig.TextExp, TextAnchor.MiddleLeft);
            _expFill = CreateBar(panel, "ExpBar", 16, 260, 388, 18, GameConfig.BarTrackExp, GameConfig.FillExp);

            CreateImage(panel, "Divider", 16, 292, 388, 2, GameConfig.Divider);

            CreateText(panel, "EquipTitle", "装备", 16, 302, 388, 28, 20, GameConfig.TextTitle, TextAnchor.MiddleLeft, FontStyle.Bold);

            // 强化入口。放在装备标题行右侧：标题「装备」只占约 40px，右边整片是空的，
            // 而 x=300 与顶部「存档」按钮同列，纵向对齐看着整齐。
            // 装备区本身已经排满（武器 334~420 / 衣服 424~510），塞不下每件一个按钮，
            // 所以走浮层：点这里开面板，在面板里挑武器还是衣服强化。
            Button enhanceEntry = CreateButton(panel, "EnhanceEntry", "强化", 300, 303, 92, 26,
                GameConfig.BtnGold, out _);
            enhanceEntry.onClick.AddListener(OnEnhanceEntryClicked);

            _weaponText = CreateText(panel, "Weapon", "", 16, 334, 388, 86, 15, Color.white, TextAnchor.UpperLeft);
            _armorText = CreateText(panel, "Armor", "", 16, 424, 388, 86, 15, Color.white, TextAnchor.UpperLeft);

            CreateImage(panel, "Divider2", 16, 514, 388, 2, GameConfig.Divider);
            _goldText = CreateText(panel, "Gold", "", 16, 516, 388, 28, 19, GameConfig.TextGold, TextAnchor.MiddleLeft, FontStyle.Bold);

            // 统计压到 3 行，给底部技能栏腾位置。
            // 统计只占 2 行，紧跟金币行下方，互不重叠。
            _statText = CreateText(panel, "Stats", "", 16, 548, 388, 34, 13, GameConfig.TextMuted, TextAnchor.UpperLeft);

            BuildSkillBar(panel);
        }

        /// <summary>
        /// 技能栏：6 个槽位（主动 2 / 被动 2 / 触发 2），横向铺满左面板宽度。
        /// 槽位内自带「主动 / 被动 / 触发」小标签，所以不再单独放标题。
        /// </summary>
        private void BuildSkillBar(Transform panel)
        {
            CreateImage(panel, "Divider3", 16, 586, 388, 2, GameConfig.Divider);

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
                    GameConfig.RowBg, out _skillSlotLabels[i]);

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
                    2, 32, slotW - 4f, 14, 10, GameConfig.TextInfo, TextAnchor.MiddleCenter);

                // 槽位类别标记（主动 / 被动 / 触发）。
                CreateText(_skillSlotButtons[i].transform, $"SkillKind{i}",
                    KindShort(i / GameConfig.SkillSlotsPerKind),
                    2, 0, slotW - 4f, 14, 9, GameConfig.TextMuted, TextAnchor.MiddleCenter);

                // 底部冷却进度条。
                Image barBg = CreateImage(_skillSlotButtons[i].transform, $"SkillCdBar{i}",
                    2, slotH - 8f, slotW - 4f, 6, GameConfig.ShadeOverlay);
                _skillSlotFills[i] = CreateImage(barBg.rectTransform, "Fill", 0, 0, 0, 6,
                    GameConfig.AccentSlotReady);

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
            RectTransform panel = CreatePanel(root, "RightPanel", RightPanelX, 12, RightPanelW, PanelH, GameConfig.PanelBg);

            _monsterTitle = CreateText(panel, "MonsterTitle", "", 16, 12, 792, 28, 19, GameConfig.AccentMonsterTitle, TextAnchor.MiddleLeft, FontStyle.Bold);

            // 精英/Boss 的血条边框：先建外框，再建血条盖住中间，只露出边缘。
            _monsterHpBorder = CreateImage(panel, "MonsterHpBorder", 13, 43, 798, 30, GameConfig.Clear);

            _monsterHpFill = CreateBar(panel, "MonsterHpBar", 16, 46, 792, 24, GameConfig.BarTrackHp, GameConfig.FillMonsterHp);
            _monsterHpText = CreateText(panel, "MonsterHpText", "", 16, 46, 792, 24, 15, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);

            _monsterStatText = CreateText(panel, "MonsterStat", "", 16, 76, 792, 32, 15, GameConfig.TextNormal, TextAnchor.UpperLeft);

            // ---- Buff / Debuff 栏（步骤 6）：狂暴、免死窗口、护盾、技能冷却 ----
            CreateImage(panel, "BuffBarBg", 16, 110, 792, 30, GameConfig.InsetBg);
            _buffText = CreateText(panel, "BuffText", "", 24, 110, 776, 30, 15, Color.white, TextAnchor.MiddleLeft);

            CreateImage(panel, "Divider", 16, 150, 792, 2, GameConfig.Divider);
            CreateText(panel, "LogTitle", "战斗日志（最新在上）", 16, 158, 400, 26, 17, GameConfig.TextTitle, TextAnchor.MiddleLeft, FontStyle.Bold);

            // ---- 实时 DPS（步骤 8），和日志标题同一行、靠右 ----
            _dpsText = CreateText(panel, "DpsText", "", 424, 158, 384, 26, 15, GameConfig.TextMint, TextAnchor.MiddleRight);

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
                _speedButtons[i] = CreateButton(panel, $"Speed{i}", speedLabels[i], 236 + i * 92, 612, 84, 56,
                    GameConfig.BtnNeutral, out _speedLabels[i]);
                _speedButtons[i].onClick.AddListener(() => _game.SetSpeedIndex(captured));
            }

            Button reset = CreateButton(panel, "ResetButton", "重置", 700, 612, 108, 56, GameConfig.BtnDanger, out _resetLabel);
            reset.onClick.AddListener(OnResetClicked);
        }

        /// <summary>商店面板：目前只卖生命药剂（价格随主角等级变化）。</summary>
        private void BuildShopPanel(Transform panel)
        {
            CreateImage(panel, "ShopBg", 16, 492, 792, 112, GameConfig.InsetBg);

            CreateText(panel, "ShopTitle", "商店", 28, 498, 70, 28, 18,
                GameConfig.TextTitle, TextAnchor.MiddleLeft, FontStyle.Bold);

            _shopGoldText = CreateText(panel, "ShopGold", "", 100, 498, 170, 28, 16,
                GameConfig.TextGold, TextAnchor.MiddleLeft, FontStyle.Bold);

            _shopPotionText = CreateText(panel, "ShopPotion", "", 274, 498, 280, 28, 15,
                Color.white, TextAnchor.MiddleLeft);

            _buyButton = CreateButton(panel, "BuyPotion", "购买", 556, 496, 88, 32,
                GameConfig.BtnPrimary, out _);
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

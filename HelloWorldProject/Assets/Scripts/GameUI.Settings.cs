using UnityEngine;
using UnityEngine.UI;

namespace AutoBattle
{
    /// <summary>
    /// 设置浮层 + 新手引导（2026-10-18 新增）。
    /// 两个浮层都走 GameController 的互斥开关：打开时 <see cref="GameController.SettingsOpen"/> /
    /// <see cref="GameController.GuideOpen"/> 为 true，控制器据此冻结战斗。
    /// </summary>
    public partial class GameUI
    {
        // ---- 设置面板 ----
        private RectTransform _settingsPanel;
        private readonly Button[] _settingsButtons = new Button[5];
        private readonly Text[] _settingsValueLabels = new Text[5];
        private Text _settingsGuideLabel;
        private bool _settingsPanelWasOpen;
        private bool _settingsPanelDirty;

        // ---- 常驻入口（设置按钮 / 对比按钮 / 引导高亮目标）----
        private Button _settingsButton;
        private Text _settingsButtonLabel;
        private Button _compareButton;
        private Text _compareButtonLabel;
        private RectTransform _leftPanel;
        private Image _equipCard;
        private Image _logCard;
        private Image _dpsPill;
        private Image _shopCard;
        private RectTransform _skillBarRoot;

        // ---- 新手引导 ----
        private struct GuideStep
        {
            public string title;
            public string body;
            public RectTransform target;
        }

        private RectTransform _guideRoot;
        private readonly Image[] _guideDims = new Image[4];
        private readonly Image[] _guideBorders = new Image[4];
        // 透明全屏拦截层：漏出的洞不挡住点击的话，引导时能顺手点开选关/技能/设置面板。
        private Image _guideBlocker;
        private RectTransform _guideCard;
        private Text _guideTitle;
        private Text _guideBody;
        private Text _guideStepText;
        private Text _guideNextLabel;
        private GuideStep[] _guideSteps;
        private int _guideStep;
        private int _guideShownStep = -1;
        private bool _guideDirty;

        private static readonly Color GuideDimColor = new Color(0.01f, 0.015f, 0.03f, 0.74f);

        // ==================== 设置 ====================

        private void BuildSettingsPanel(Transform root)
        {
            _settingsPanel = CreatePanel(root, "SettingsPanel", 300, 130, 680, 452, GameConfig.OverlayBg, true);

            CreateSectionTitle(_settingsPanel, "设置", 24, 14, 240, FontTitle);
            CreateText(_settingsPanel, "SettingsHint",
                "只影响显示与自动化，不改战斗数值。改动立即生效，并按本机保存（与存档槽无关）。",
                24, 44, 520, 44, FontSmall, GameConfig.TextMuted, TextAnchor.UpperLeft);

            Button close = CreateButton(_settingsPanel, "SettingsClose", "关闭", 566, 16, 90, 32,
                GameConfig.BtnNeutral, out _);
            close.onClick.AddListener(OnSettingsCloseClicked);

            for (int i = 0; i < 5; i++)
            {
                int captured = i;
                var id = (GameSettingId)i;
                float y = 78f + i * 50f;

                CreateCard(_settingsPanel, "SettingRow" + i, 20, y, 640, 44, GameConfig.CardBg,
                    RadiusButton, GameConfig.BorderSoft);

                CreateText(_settingsPanel, "SettingName" + i, GameSettings.Label(id),
                    34, y + 3, 240, 20, FontBody, GameConfig.TextNormal, TextAnchor.MiddleLeft);
                CreateText(_settingsPanel, "SettingHint" + i, GameSettings.Hint(id),
                    34, y + 23, 470, 18, FontTiny, GameConfig.TextMuted, TextAnchor.MiddleLeft);

                _settingsButtons[i] = CreateButton(_settingsPanel, "SettingToggle" + i, "开", 548, y + 6, 92, 32,
                    GameConfig.BtnPrimary, out _settingsValueLabels[i]);
                _settingsButtons[i].onClick.AddListener(() => OnSettingToggleClicked(captured));
            }

            // 引导行：它不是开关，而是「重看」入口，所以单独一行、单独排版。
            CreateCard(_settingsPanel, "GuideRow", 20, 332, 640, 44, GameConfig.CardBg,
                RadiusButton, GameConfig.BorderSoft);
            CreateText(_settingsPanel, "GuideName", "新手引导", 34, 335, 240, 20, FontBody,
                GameConfig.TextNormal, TextAnchor.MiddleLeft);
            _settingsGuideLabel = CreateText(_settingsPanel, "GuideState", "", 34, 355, 300, 18, FontTiny,
                GameConfig.TextMuted, TextAnchor.MiddleLeft);

            Button replay = CreateButton(_settingsPanel, "GuideReplay", "重看引导", 520, 338, 120, 32,
                GameConfig.BtnNeutral, out _);
            replay.onClick.AddListener(OnReplayGuideClicked);

            Button reset = CreateButton(_settingsPanel, "SettingsReset", "恢复默认", 20, 396, 120, 34,
                GameConfig.RowBg, out _);
            reset.onClick.AddListener(OnResetSettingsClicked);

            CreateText(_settingsPanel, "SettingsFoot", "提示：锁定装备与「自动换装」在对比面板里配合使用。",
                156, 402, 500, 22, FontTiny, GameConfig.TextMuted, TextAnchor.MiddleLeft);

            _settingsPanel.gameObject.SetActive(false);
        }

        private void RefreshSettingsPanel()
        {
            if (_settingsPanel == null || _game == null) return;

            bool open = _game.SettingsOpen;
            _settingsPanel.gameObject.SetActive(open);
            if (!open)
            {
                _settingsPanelWasOpen = false;
                return;
            }

            bool justOpened = !_settingsPanelWasOpen;
            _settingsPanelWasOpen = true;
            if (!justOpened && !_settingsPanelDirty) return;
            _settingsPanelDirty = false;

            for (int i = 0; i < 5; i++)
            {
                bool on = GameSettings.Get((GameSettingId)i);
                _settingsValueLabels[i].text = on ? "开" : "关";
                _settingsValueLabels[i].color = on ? GameConfig.LogHeal : GameConfig.TextMuted;
                _settingsButtons[i].image.color = on ? GameConfig.BtnPrimary : GameConfig.CardBtn;
            }

            _settingsGuideLabel.text = GameSettings.GuideDone
                ? "已完成。可以重看，不会重置任何进度。"
                : "还没走完，重看会从第一步开始。";
        }

        private void OnSettingsClicked()
        {
            _settingsPanelDirty = true;
            _game.ToggleSettingsPanel();
        }

        private void OnSettingsCloseClicked()
        {
            _settingsPanelDirty = true;
            _game.CloseSettingsPanel();
        }

        private void OnSettingToggleClicked(int index)
        {
            GameSettings.Toggle((GameSettingId)index);
            _settingsPanelDirty = true;
        }

        private void OnResetSettingsClicked()
        {
            GameSettings.ResetToDefaults();
            _settingsPanelDirty = true;
            _game.AddLog("↺ 显示与自动化设置已恢复默认。", GameConfig.LogInfo);
        }

        private void OnReplayGuideClicked()
        {
            // 设置面板若留着，会被引导的压暗层压在半透明底下，看起来像引导卡在设置里。
            _settingsPanelDirty = true;
            _game.CloseSettingsPanel();
            BeginGuide();
        }

        // ==================== 新手引导 ====================

        private void BuildGuide(Transform root)
        {
            _guideRoot = CreateRect(root, "GuideRoot", 0, 0, 1280, 720);

            for (int i = 0; i < _guideDims.Length; i++)
            {
                _guideDims[i] = CreateImage(_guideRoot, "GuideDim" + i, 0, 0, 1280, 720, GuideDimColor, true);
            }

            // 四块压暗条之外再盖一层全透明拦截层（排在压暗条之后、卡片之前）：
            // 洞里的目标本身也能被点，引导时不该让玩家顺手开出别的面板。
            _guideBlocker = CreateImage(_guideRoot, "GuideBlocker", 0, 0, 1280, 720,
                new Color(0f, 0f, 0f, 0.004f), true);

            for (int i = 0; i < _guideBorders.Length; i++)
            {
                _guideBorders[i] = CreateImage(_guideRoot, "GuideBorder" + i, 0, 0, 1, 1,
                    GameConfig.AccentBanner, false);
            }

            _guideCard = CreatePanel(_guideRoot, "GuideCard", 660, 28, 600, 190, GameConfig.OverlayBg, true);

            _guideTitle = CreateText(_guideCard, "GuideTitle", "", 24, 12, 420, 28, 19,
                GameConfig.TextTitle, TextAnchor.MiddleLeft, FontStyle.Bold);
            _guideStepText = CreateText(_guideCard, "GuideStep", "", 470, 15, 106, 22, FontSmall,
                GameConfig.TextMuted, TextAnchor.MiddleRight);
            _guideBody = CreateText(_guideCard, "GuideBody", "", 24, 46, 552, 92, 14,
                GameConfig.TextNormal, TextAnchor.UpperLeft);
            _guideBody.lineSpacing = 1.24f;

            Button skip = CreateButton(_guideCard, "GuideSkip", "跳过", 372, 146, 96, 32,
                GameConfig.RowBg, out _);
            skip.onClick.AddListener(OnGuideSkipClicked);

            Button next = CreateButton(_guideCard, "GuideNext", "下一步", 478, 146, 100, 32,
                GameConfig.BtnPrimary, out _guideNextLabel);
            next.onClick.AddListener(OnGuideNextClicked);

            _guideSteps = new[]
            {
                new GuideStep
                {
                    title = "欢迎来到自动战斗",
                    body = "点「开始」后角色会自动打怪、拾取装备，你只需要在关键节点做选择。"
                         + "\n接下来用几步带你逛一遍界面；这一步也会冻结战斗，不用担心被打。",
                    target = null,
                },
                new GuideStep
                {
                    title = "角色与装备",
                    body = "左上角是角色：等级、生命、攻击、防御、攻速、暴击都在这里。"
                         + "\n装备分武器和衣服两个槽位；掉落会按评分自动比较，你也可以锁住当前装备、改用手动对比。",
                    target = _equipCard != null ? _equipCard.rectTransform : _leftPanel,
                },
                new GuideStep
                {
                    title = "选关与推进",
                    body = "点关卡按钮可以挑章节和关卡。每关要击杀固定数量的怪，首通有金币奖励。"
                         + "\n打不过就回本关刷小怪吃经验——阵亡只会清空本关进度，不会结束游戏。",
                    target = _stageButton != null ? _stageButton.GetComponent<RectTransform>() : _leftPanel,
                },
                new GuideStep
                {
                    title = "技能栏（6 槽）",
                    body = "下方六个槽位：主动 / 被动 / 触发各两格。满级后的巅峰等级会给你技能三选一。"
                         + "\n技能卡片上的「推荐分」按你当前面板估算，只用来排序，不影响战斗结算。",
                    target = _skillBarRoot,
                },
                new GuideStep
                {
                    title = "日志、怪物与商店",
                    body = "右侧上方是怪物血量与词缀，中间是战斗日志，下方商店只在精英/Boss 前开放。"
                         + "\n生命低于 40% 会自动喝药；血量归零会在本关重来。",
                    target = _logCard != null ? _logCard.rectTransform : _leftPanel,
                },
                new GuideStep
                {
                    title = "设置与对比",
                    body = "左上角的「设置」可以关掉飘字 / DPS / 评分 / 推荐分，切换自动换装，或重看引导。"
                         + "\n装备行的「对比」在有待选掉落时会亮起，用来比较当前装备与更强掉落。",
                    target = _settingsButton != null ? _settingsButton.GetComponent<RectTransform>() : _leftPanel,
                },
            };

            _guideRoot.gameObject.SetActive(false);
        }

        /// <summary>首次启动或设置里点「重看引导」时调用。</summary>
        private void BeginGuide()
        {
            _guideStep = 0;
            _guideShownStep = -1;
            _guideDirty = true;
            _game.OpenGuide();
            RefreshGuide();
        }

        private void RefreshGuide()
        {
            if (_guideRoot == null || _game == null) return;

            bool open = _game.GuideOpen;
            _guideRoot.gameObject.SetActive(open);
            if (!open) return;

            if (!_guideDirty && _guideShownStep == _guideStep) return;
            _guideDirty = false;
            ApplyGuideStep();
        }

        private void ApplyGuideStep()
        {
            if (_guideSteps == null || _guideSteps.Length == 0) return;

            _guideStep = Mathf.Clamp(_guideStep, 0, _guideSteps.Length - 1);
            _guideShownStep = _guideStep;
            GuideStep step = _guideSteps[_guideStep];

            _guideTitle.text = step.title;
            _guideBody.text = step.body;
            _guideStepText.text = $"{_guideStep + 1} / {_guideSteps.Length}";
            _guideNextLabel.text = _guideStep >= _guideSteps.Length - 1 ? "完成" : "下一步";

            Rect rect = step.target != null
                ? DesignRectOf(step.target)
                : new Rect(0f, 0f, 1280f, 720f);
            float x = Mathf.Clamp(rect.x, 0f, 1278f);
            float y = Mathf.Clamp(rect.y, 0f, 718f);
            float w = Mathf.Clamp(rect.width, 2f, 1280f - x);
            float h = Mathf.Clamp(rect.height, 2f, 720f - y);

            // 没有目标（欢迎页）就整屏压暗；有目标则用四块遮罩围出一个洞，
            // 目标本身不被压暗，其余区域仍然挡住点击。
            bool hasHole = step.target != null;
            if (!hasHole)
            {
                SetGuideRect(_guideDims[0].rectTransform, 0f, 0f, 1280f, 720f);
                for (int i = 1; i < _guideDims.Length; i++) SetGuideRect(_guideDims[i].rectTransform, 0f, 0f, 0f, 0f);
                for (int i = 0; i < _guideBorders.Length; i++) SetGuideRect(_guideBorders[i].rectTransform, 0f, 0f, 0f, 0f);
            }
            else
            {
                SetGuideRect(_guideDims[0].rectTransform, 0f, 0f, 1280f, y);
                SetGuideRect(_guideDims[1].rectTransform, 0f, y + h, 1280f, 720f - (y + h));
                SetGuideRect(_guideDims[2].rectTransform, 0f, y, x, h);
                SetGuideRect(_guideDims[3].rectTransform, x + w, y, 1280f - (x + w), h);

                // 洞的四条边打一圈金色细框，指明「就是这里」。
                SetGuideRect(_guideBorders[0].rectTransform, x - 2f, y - 2f, w + 4f, 2f);
                SetGuideRect(_guideBorders[1].rectTransform, x - 2f, y + h, w + 4f, 2f);
                SetGuideRect(_guideBorders[2].rectTransform, x - 2f, y, 2f, h);
                SetGuideRect(_guideBorders[3].rectTransform, x + w, y, 2f, h);
            }
            for (int i = 0; i < _guideBorders.Length; i++) _guideBorders[i].enabled = hasHole;

            // 文案卡放到目标的斜对角，避开洞：目标在上半屏 → 卡在下方，反之亦然。
            float centerX = x + w * 0.5f;
            float centerY = y + h * 0.5f;
            float cardX = centerX < 640f ? 660f : 20f;
            float cardY = centerY > 360f ? 28f : 720f - 190f - 28f;
            // 欢迎页没有目标：整屏都压暗，卡片直接摆在正中间，别贴着底边的按钮区。
            if (!hasHole) { cardX = 340f; cardY = 250f; }
            SetGuideRect(_guideCard, cardX, cardY, 600f, 190f);
        }

        private Rect DesignRectOf(RectTransform target)
        {
            var corners = new Vector3[4];
            target.GetWorldCorners(corners);

            float minX = float.MaxValue, minY = float.MaxValue;
            float maxX = float.MinValue, maxY = float.MinValue;

            for (int i = 0; i < 4; i++)
            {
                Vector2 screen = RectTransformUtility.WorldToScreenPoint(null, corners[i]);
                if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_guideRoot, screen, null, out Vector2 local))
                    continue;
                float dx = local.x;
                float dy = -local.y; // anchoredPosition 的 y 向下为正
                minX = Mathf.Min(minX, dx);
                maxX = Mathf.Max(maxX, dx);
                minY = Mathf.Min(minY, dy);
                maxY = Mathf.Max(maxY, dy);
            }

            if (minX > maxX) return new Rect(0f, 0f, 1280f, 720f);
            return new Rect(minX, minY, maxX - minX, maxY - minY);
        }

        private static void SetGuideRect(RectTransform rt, float x, float y, float w, float h)
        {
            if (rt == null) return;
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(Mathf.Max(0f, w), Mathf.Max(0f, h));
        }

        private void OnGuideNextClicked()
        {
            if (_guideSteps == null) return;
            if (_guideStep >= _guideSteps.Length - 1)
            {
                CompleteGuide();
                return;
            }
            _guideStep++;
            _guideDirty = true;
            ApplyGuideStep();
        }

        private void OnGuideSkipClicked() => CompleteGuide();

        private void CompleteGuide()
        {
            GameSettings.GuideDone = true;
            _settingsPanelDirty = true;
            _guideDirty = true;
            _game.CloseGuide();
            _guideRoot.gameObject.SetActive(false);
            _game.AddLog("✓ 新手引导已完成，可在「设置 → 重看引导」再看一遍。", GameConfig.LogInfo);
        }
    }
}

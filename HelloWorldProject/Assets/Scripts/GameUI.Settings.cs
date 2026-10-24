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
        private readonly Image[] _settingsIcons = new Image[5];
        private readonly Image[] _masterVolumeCells = new Image[10];
        private readonly Image[] _sfxVolumeCells = new Image[10];
        private readonly Image[] _settingsMusicCells = new Image[10];
        private Text _masterVolumeValue;
        private Text _sfxVolumeValue;
        private Text _settingsMusicValue;
        private Text _settingsGuideLabel;
        private bool _settingsPanelWasOpen;
        private bool _settingsPanelDirty;

        // ---- 常驻入口（设置按钮 / 对比按钮 / 引导高亮目标）----
        private Button _settingsButton;
        private Text _settingsButtonLabel;
        private Button _compareButton;
        private Text _compareButtonLabel;
        private Image _compareButtonIcon;
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
            // 高度 560 → 620：多出一行「音乐」音量，底部再加一行快捷键提示（2026-10-23）。
            _settingsPanel = CreatePanel(root, "SettingsPanel", 300, 90, 680, 620, GameConfig.OverlayBg, true);

            CreateSectionTitle(_settingsPanel, "设置", 24, 14, 240, FontTitle, "ui_settings");
            CreateText(_settingsPanel, "SettingsHint",
                "只影响显示与自动化，不改战斗数值。改动立即生效，并按本机保存（与存档槽无关）。",
                24, 44, 520, 44, FontSmall, GameConfig.TextMuted, TextAnchor.UpperLeft);

            Button close = CreateButton(_settingsPanel, "SettingsClose", "关闭", 566, 16, 90, 32,
                GameConfig.BtnNeutral, out Text closeLabel);
            StyleBar(close, closeLabel, "frame_pill", "glyph_close", 14f, "关闭设置");
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
                _settingsIcons[i] = StyleBar(_settingsButtons[i], _settingsValueLabels[i], "frame_pill", "glyph_confirm", 14f,
                    "切换" + GameSettings.Label(id), 12);
                _settingsButtons[i].onClick.AddListener(() => OnSettingToggleClicked(captured));
            }

            // 音量不是开关：用十段格 + 百分比 + 加减按钮，改完立刻生效。
            CreateSectionTitle(_settingsPanel, "音量", 24, 326, 180, FontBody);
            _masterVolumeValue = BuildVolumeRow("主音量", "总音量；实际音量 = 主音量 × 音效音量",
                352, _masterVolumeCells, 0);
            _sfxVolumeValue = BuildVolumeRow("音效", "按钮、命中、拾取、升级等提示音",
                402, _sfxVolumeCells, 1);
            // 音乐音量原来只能在背景音乐浮层里调，设置面板反而没有——同一个设置项两个入口不一致。
            _settingsMusicValue = BuildVolumeRow("音乐", "背景音乐；与主音量相乘",
                452, _settingsMusicCells, 2);

            // 引导行：它不是开关，而是「重看」入口，所以单独一行、单独排版。
            // 三行音量之后整体下移 50：音量 452 行到 496 结束，留给引导行一点空隙。
            CreateCard(_settingsPanel, "GuideRow", 20, 504, 640, 44, GameConfig.CardBg,
                RadiusButton, GameConfig.BorderSoft);
            CreateText(_settingsPanel, "GuideName", "新手引导", 34, 507, 240, 20, FontBody,
                GameConfig.TextNormal, TextAnchor.MiddleLeft);
            _settingsGuideLabel = CreateText(_settingsPanel, "GuideState", "", 34, 527, 300, 18, FontTiny,
                GameConfig.TextMuted, TextAnchor.MiddleLeft);

            Button replay = CreateButton(_settingsPanel, "GuideReplay", "重看引导", 520, 510, 120, 32,
                GameConfig.BtnNeutral, out Text replayLabel);
            StyleBar(replay, replayLabel, "frame_bar_notched", "glyph_book", 16f, "重新观看新手引导");
            replay.onClick.AddListener(OnReplayGuideClicked);

            Button reset = CreateButton(_settingsPanel, "SettingsReset", "恢复默认", 20, 560, 120, 34,
                GameConfig.RowBg, out Text resetLabel);
            StyleBar(reset, resetLabel, "frame_pill", "glyph_reset", 16f, "恢复默认设置");
            reset.onClick.AddListener(OnResetSettingsClicked);

            // 关于入口（roadmap A4 / C8）：版本、存档版本、素材来源与数据位置。
            Button about = CreateButton(_settingsPanel, "SettingsAbout", "关于", 146, 560, 86, 34,
                GameConfig.RowBg, out Text aboutLabel);
            StyleBar(about, aboutLabel, "frame_pill", "glyph_book", 14f, "关于本作", 13);
            about.onClick.AddListener(OnAboutClicked);

            // 显示设置（roadmap C9/C11）：设置面板本身已经满了，单独开一个浮层。
            Button display = CreateButton(_settingsPanel, "SettingsDisplay", "显示", 238, 560, 86, 34,
                GameConfig.RowBg, out Text displayLabel);
            StyleBar(display, displayLabel, "frame_pill", "glyph_settings", 14f, "窗口、分辨率与色弱友好", 13);
            display.onClick.AddListener(OnDisplayClicked);

            CreateText(_settingsPanel, "SettingsFoot", "提示：锁定装备与「自动换装」在对比面板里配合使用。",
                330, 566, 326, 22, FontTiny, GameConfig.TextMuted, TextAnchor.MiddleLeft);

            // 快捷键目前没有任何入口提示（工程原来一个键都不认），写在设置面板最底下最容易被看到。
            CreateText(_settingsPanel, "SettingsShortcutHint",
                "快捷键：空格 暂停/继续 · 1/2/4 变速 · Esc 关浮层（无浮层时暂停）· R 重置",
                24, 590, 632, 20, FontTiny, GameConfig.TextMuted, TextAnchor.MiddleLeft);

            _settingsPanel.gameObject.SetActive(false);
        }

        /// <summary>一行音量：十段格 + 百分比 + 加减按钮。返回百分比文本，刷新时统一改颜色。</summary>
        private Text BuildVolumeRow(string name, string hint, float y, Image[] cells, int channel)
        {
            CreateCard(_settingsPanel, "VolumeRow" + channel, 20, y, 640, 44, GameConfig.CardBg,
                RadiusButton, GameConfig.BorderSoft);
            CreateText(_settingsPanel, "VolumeName" + channel, name, 34, y + 3, 120, 20, FontBody,
                GameConfig.TextNormal, TextAnchor.MiddleLeft);
            CreateText(_settingsPanel, "VolumeHint" + channel, hint, 34, y + 23, 190, 16, FontTiny,
                GameConfig.TextMuted, TextAnchor.MiddleLeft);

            Text value = CreateText(_settingsPanel, "VolumeValue" + channel, "80%",
                474, y + 13, 70, 18, FontSmall, GameConfig.TextNormal, TextAnchor.MiddleLeft);

            for (int i = 0; i < cells.Length; i++)
            {
                int captured = i;
                Image cell = CreateImage(_settingsPanel, "VolumeCell" + channel + "_" + i,
                    236 + i * 23, y + 15, 20, 14, GameConfig.VolumeCellOff, true);
                Round(cell, 3);
                AddBorder(cell, 3, GameConfig.VolumeCellBorder);
                Button button = cell.gameObject.AddComponent<Button>();
                button.targetGraphic = cell;
                button.onClick.AddListener(GameAudio.PlayUiClick);
                button.onClick.AddListener(() => OnVolumeCellClicked(channel, captured));
                cells[i] = cell;
            }

            // 用 U+2212 减号而不是 ASCII 连字符：连字符在 36px 的按钮里缩成一小横。
            Button down = CreateButton(_settingsPanel, "VolumeDown" + channel, "−", 548, y + 10, 36, 24,
                GameConfig.BtnNeutral, out Text downLabel);
            StyleTab(down, downLabel, "frame_pill", 20);
            down.onClick.AddListener(() => OnVolumeStepClicked(channel, -1));

            Button up = CreateButton(_settingsPanel, "VolumeUp" + channel, "+", 590, y + 10, 36, 24,
                GameConfig.BtnNeutral, out Text upLabel);
            StyleTab(up, upLabel, "frame_pill", 20);
            up.onClick.AddListener(() => OnVolumeStepClicked(channel, 1));

            return value;
        }

        private void OnVolumeCellClicked(int channel, int index)
        {
            ApplyVolume(channel, (index + 1) / 10f);
        }

        private void OnVolumeStepClicked(int channel, int step)
        {
            float current = channel == 0
                ? GameSettings.MasterVolume
                : channel == 1 ? GameSettings.SfxVolume : GameSettings.MusicVolume;
            float next = Mathf.Round((current + step * 0.1f) * 10f) / 10f;
            ApplyVolume(channel, Mathf.Clamp01(next));
        }

        private void ApplyVolume(int channel, float value)
        {
            if (channel == 0) GameSettings.MasterVolume = value;
            else if (channel == 1) GameSettings.SfxVolume = value;
            else GameSettings.MusicVolume = value;

            GameAudio.RefreshVolumes();
            // 主音量同时乘在音乐上，所以三行里任何一行改动都要把音乐音量重算一次。
            if (GameMusic.Instance != null) GameMusic.Instance.RefreshVolume();
            _settingsPanelDirty = true;
        }

        private static void RefreshVolumeRow(Image[] cells, Text value, float volume)
        {
            int filled = Mathf.RoundToInt(volume * 10f);
            for (int i = 0; i < cells.Length; i++)
                cells[i].color = i < filled ? GameConfig.LogGold : GameConfig.VolumeCellOff;
            if (value != null) value.text = Mathf.RoundToInt(volume * 100f) + "%";
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
                _settingsButtons[i].image.color = on
                    ? Color.white
                    : new Color(0.62f, 0.66f, 0.74f, 1f);
                SetIcon(_settingsIcons[i], on ? "glyph_confirm" : "glyph_close");
                if (_settingsIcons[i] != null)
                    _settingsIcons[i].color = on ? Color.white : new Color(1f, 1f, 1f, 0.55f);
            }

            RefreshVolumeRow(_masterVolumeCells, _masterVolumeValue, GameSettings.MasterVolume);
            RefreshVolumeRow(_sfxVolumeCells, _sfxVolumeValue, GameSettings.SfxVolume);
            RefreshVolumeRow(_settingsMusicCells, _settingsMusicValue, GameSettings.MusicVolume);

            _settingsGuideLabel.text = GameSettings.GuideDone
                ? "已完成。可以重看，不会重置任何进度。"
                : "还没走完，重看会从第一步开始。";
        }

        private void OnSettingsClicked()
        {
            _settingsPanelDirty = true;
            bool open = _game.SettingsOpen;
            _game.ToggleSettingsPanel();
            if (open) GameAudio.PlayUiClose();
            else GameAudio.PlayUiOpen();
        }

        private void OnSettingsCloseClicked()
        {
            _settingsPanelDirty = true;
            _game.CloseSettingsPanel();
            GameAudio.PlayUiClose();
        }

        private void OnSettingToggleClicked(int index)
        {
            GameSettings.Toggle((GameSettingId)index);
            _settingsPanelDirty = true;
        }

        private void OnResetSettingsClicked()
        {
            GameSettings.ResetToDefaults();
            if (_game != null) _game.ResetSpeedToDefault();
            GameAudio.RefreshVolumes();
            if (GameMusic.Instance != null) GameMusic.Instance.RefreshVolume();
            _settingsPanelDirty = true;
            _game.AddLog("↺ 显示、自动化与音量设置已恢复默认。", GameConfig.LogInfo);
        }

        private void OnReplayGuideClicked()
        {
            // 设置面板若留着，会被引导的压暗层压在半透明底下，看起来像引导卡在设置里。
            _settingsPanelDirty = true;
            _game.CloseSettingsPanel();
            BeginGuide();
        }


        // ==================== 显示（roadmap C9 / C11）====================

        private RectTransform _displayPanel;
        private Text _displayResolutionText;
        private Text _displayModeText;
        private Text _displayFullscreenLabel;
        private Text _displayResizableLabel;
        private Text _displayColorBlindLabel;
        private bool _displayPanelWasOpen;

        /// <summary>
        /// 显示设置浮层：全屏 / 分辨率预设 / 允许缩放 / 色弱友好。
        /// 没有塞进设置面板，是因为设置面板 680×620 已经顶到 710 的底边。
        /// </summary>
        private void BuildDisplayPanel(Transform root)
        {
            _displayPanel = CreatePanel(root, "DisplayPanel", 300, 100, 680, 520, GameConfig.OverlayBg, true);

            CreateSectionTitle(_displayPanel, "显示", 24, 14, 240, FontTitle, "glyph_settings");

            Button close = CreateButton(_displayPanel, "DisplayClose", "关闭", 566, 16, 90, 32,
                GameConfig.BtnNeutral, out Text closeLabel);
            StyleBar(close, closeLabel, "frame_pill", "glyph_close", 14f, "关闭显示设置");
            close.onClick.AddListener(OnDisplayCloseClicked);

            CreateText(_displayPanel, "DisplayHint",
                "窗口、分辨率与缩放只对构建后的窗口生效；编辑器里由 Game 视图控制，设置仍会记住。",
                24, 44, 632, 40, FontSmall, GameConfig.TextMuted, TextAnchor.UpperLeft);

            CreateCard(_displayPanel, "DisplayFullscreenRow", 20, 94, 640, 44, GameConfig.CardBg,
                RadiusButton, GameConfig.BorderSoft);
            CreateText(_displayPanel, "DisplayFullscreenName", "全屏", 34, 97, 200, 20, FontBody,
                GameConfig.TextNormal, TextAnchor.MiddleLeft);
            CreateText(_displayPanel, "DisplayFullscreenHint", "独占全屏；窗口模式下分辨率预设才会改窗口大小",
                34, 117, 480, 18, FontTiny, GameConfig.TextMuted, TextAnchor.MiddleLeft);
            Button fullscreen = CreateButton(_displayPanel, "DisplayFullscreenToggle", "关", 548, 100, 92, 32,
                GameConfig.BtnNeutral, out _displayFullscreenLabel);
            StyleBar(fullscreen, _displayFullscreenLabel, "frame_pill", "glyph_confirm", 14f, "切换全屏", 12);
            fullscreen.onClick.AddListener(OnDisplayFullscreenClicked);

            CreateCard(_displayPanel, "DisplayResolutionRow", 20, 144, 640, 44, GameConfig.CardBg,
                RadiusButton, GameConfig.BorderSoft);
            CreateText(_displayPanel, "DisplayResolutionName", "窗口分辨率", 34, 147, 200, 20, FontBody,
                GameConfig.TextNormal, TextAnchor.MiddleLeft);
            CreateText(_displayPanel, "DisplayResolutionHint", "16:9 / 16:10 / 21:9 都只会多出留边，不会裁掉 UI",
                34, 167, 480, 18, FontTiny, GameConfig.TextMuted, TextAnchor.MiddleLeft);
            _displayResolutionText = CreateText(_displayPanel, "DisplayResolutionValue", "", 360, 157, 148, 20,
                FontSmall, GameConfig.TextInfo, TextAnchor.MiddleRight);
            Button prev = CreateButton(_displayPanel, "DisplayResolutionPrev", "◀", 514, 151, 64, 26,
                GameConfig.BtnNeutral, out Text prevLabel);
            StyleTab(prev, prevLabel, "frame_pill", 14);
            prev.onClick.AddListener(() => OnDisplayResolutionStep(-1));
            Button next = CreateButton(_displayPanel, "DisplayResolutionNext", "▶", 582, 151, 64, 26,
                GameConfig.BtnNeutral, out Text nextLabel);
            StyleTab(next, nextLabel, "frame_pill", 14);
            next.onClick.AddListener(() => OnDisplayResolutionStep(1));

            CreateCard(_displayPanel, "DisplayResizableRow", 20, 194, 640, 44, GameConfig.CardBg,
                RadiusButton, GameConfig.BorderSoft);
            CreateText(_displayPanel, "DisplayResizableName", "允许缩放", 34, 197, 200, 20, FontBody,
                GameConfig.TextNormal, TextAnchor.MiddleLeft);
            CreateText(_displayPanel, "DisplayResizableHint", "窗口模式下可以拖边改变大小；内容等比缩放",
                34, 217, 480, 18, FontTiny, GameConfig.TextMuted, TextAnchor.MiddleLeft);
            Button resizable = CreateButton(_displayPanel, "DisplayResizableToggle", "开", 548, 200, 92, 32,
                GameConfig.BtnNeutral, out _displayResizableLabel);
            StyleBar(resizable, _displayResizableLabel, "frame_pill", "glyph_confirm", 14f, "切换允许缩放", 12);
            resizable.onClick.AddListener(OnDisplayResizableClicked);

            CreateCard(_displayPanel, "DisplayColorBlindRow", 20, 244, 640, 44, GameConfig.CardBg,
                RadiusButton, GameConfig.BorderSoft);
            CreateText(_displayPanel, "DisplayColorBlindName", "色弱友好", 34, 247, 200, 20, FontBody,
                GameConfig.TextNormal, TextAnchor.MiddleLeft);
            CreateText(_displayPanel, "DisplayColorBlindHint", "品质色换成蓝绿 / 天蓝 / 紫红 / 橙；品质名照旧显示，不只靠颜色",
                34, 267, 500, 18, FontTiny, GameConfig.TextMuted, TextAnchor.MiddleLeft);
            Button colorBlind = CreateButton(_displayPanel, "DisplayColorBlindToggle", "关", 548, 250, 92, 32,
                GameConfig.BtnNeutral, out _displayColorBlindLabel);
            StyleBar(colorBlind, _displayColorBlindLabel, "frame_pill", "glyph_confirm", 14f, "切换色弱友好配色", 12);
            colorBlind.onClick.AddListener(OnDisplayColorBlindClicked);

            CreateCard(_displayPanel, "DisplayScaleNote", 20, 298, 640, 100, GameConfig.InsetBg,
                RadiusButton, GameConfig.BorderSoft);
            CreateText(_displayPanel, "DisplayScaleNoteTitle", "为什么没有界面缩放滑杆", 34, 306, 400, 20,
                FontBody, GameConfig.TextTitle, TextAnchor.MiddleLeft);
            CreateText(_displayPanel, "DisplayScaleNoteBody",
                "UI 按 1280×720 设计，CanvasScaler 用 Expand：窗口比 16:9 更宽/更高时只会多出背景留边，" +
                "文字与按钮不会裁切。再叠 0.9 缩放会把 9~12px 的小字压到不可读，放大则必然裁边，" +
                "所以这里只保证 1280×720 以上等比可读，不提供自由缩放。",
                34, 328, 610, 64, FontTiny, GameConfig.TextMuted, TextAnchor.UpperLeft);

            _displayModeText = CreateText(_displayPanel, "DisplayModeText", "", 24, 410, 632, 20, FontTiny,
                GameConfig.TextInfo, TextAnchor.MiddleLeft);

            Button reset = CreateButton(_displayPanel, "DisplayReset", "恢复默认", 20, 452, 120, 34,
                GameConfig.RowBg, out Text resetLabel);
            StyleBar(reset, resetLabel, "frame_pill", "glyph_reset", 16f, "恢复默认显示设置");
            reset.onClick.AddListener(OnDisplayResetClicked);

            CreateText(_displayPanel, "DisplayFoot", "改动立即保存在本机，与存档槽无关；构建后下次启动应用。",
                160, 458, 390, 22, FontTiny, GameConfig.TextMuted, TextAnchor.MiddleLeft);

            _displayPanel.gameObject.SetActive(false);
        }

        private void RefreshDisplayPanel()
        {
            if (_displayPanel == null || _game == null) return;

            bool open = _game.DisplayOpen;
            _displayPanel.gameObject.SetActive(open);
            if (!open)
            {
                _displayPanelWasOpen = false;
                return;
            }

            bool justOpened = !_displayPanelWasOpen;
            _displayPanelWasOpen = true;
            if (!justOpened) return;

            _displayResolutionText.text = DisplaySettings.ResolutionLabel(DisplaySettings.ResolutionIndex);
            SetToggleLabel(_displayFullscreenLabel, DisplaySettings.Fullscreen);
            SetToggleLabel(_displayResizableLabel, DisplaySettings.Resizable);
            SetToggleLabel(_displayColorBlindLabel, DisplaySettings.ColorBlind);
            _displayModeText.text = Application.isEditor
                ? "编辑器内不应用窗口设置；当前存档与配色设置仍然生效。"
                : "当前：" + (DisplaySettings.Fullscreen ? "全屏" : "窗口");
        }

        private static void SetToggleLabel(Text label, bool on)
        {
            if (label == null) return;
            label.text = on ? "开" : "关";
            label.color = on ? GameConfig.LogHeal : GameConfig.TextMuted;
        }

        private void OnDisplayClicked()
        {
            _game.ToggleDisplayPanel();
            GameAudio.PlayUiOpen();
        }

        private void OnDisplayCloseClicked()
        {
            _game.CloseDisplayPanel();
            GameAudio.PlayUiClose();
        }

        private void OnDisplayFullscreenClicked()
        {
            DisplaySettings.Fullscreen = !DisplaySettings.Fullscreen;
            SetToggleLabel(_displayFullscreenLabel, DisplaySettings.Fullscreen);
            _displayModeText.text = Application.isEditor
                ? "编辑器内不应用窗口设置；当前存档与配色设置仍然生效。"
                : "当前：" + (DisplaySettings.Fullscreen ? "全屏" : "窗口");
            DisplaySettings.Apply();
            GameAudio.PlayUiClick();
        }

        private void OnDisplayResizableClicked()
        {
            DisplaySettings.Resizable = !DisplaySettings.Resizable;
            SetToggleLabel(_displayResizableLabel, DisplaySettings.Resizable);
            DisplaySettings.Apply();
            GameAudio.PlayUiClick();
        }

        private void OnDisplayResolutionStep(int delta)
        {
            int next = Mathf.Clamp(DisplaySettings.ResolutionIndex + delta, 0, DisplaySettings.ResolutionCount - 1);
            if (next == DisplaySettings.ResolutionIndex)
            {
                GameAudio.PlayUiClick();
                return;
            }
            DisplaySettings.ResolutionIndex = next;
            _displayResolutionText.text = DisplaySettings.ResolutionLabel(next);
            DisplaySettings.Apply();
            GameAudio.PlayUiClick();
        }

        private void OnDisplayColorBlindClicked()
        {
            DisplaySettings.ColorBlind = !DisplaySettings.ColorBlind;
            SetToggleLabel(_displayColorBlindLabel, DisplaySettings.ColorBlind);
            _settingsPanelDirty = true;
            GameAudio.PlayUiClick();
            _game.AddLog(DisplaySettings.ColorBlind
                ? "◐ 色弱友好配色已开启：品质色换成蓝绿 / 天蓝 / 紫红 / 橙，品质名照旧显示。"
                : "◑ 已恢复默认品质配色（白 / 绿 / 蓝 / 紫 / 橙）。", GameConfig.LogInfo);
        }

        private void OnDisplayResetClicked()
        {
            DisplaySettings.ResetToDefaults();
            DisplaySettings.Apply();
            _displayResolutionText.text = DisplaySettings.ResolutionLabel(DisplaySettings.ResolutionIndex);
            SetToggleLabel(_displayFullscreenLabel, DisplaySettings.Fullscreen);
            SetToggleLabel(_displayResizableLabel, DisplaySettings.Resizable);
            SetToggleLabel(_displayColorBlindLabel, DisplaySettings.ColorBlind);
            _settingsPanelDirty = true;
            GameAudio.PlayUiClick();
            _game.AddLog("↺ 显示设置已恢复默认。", GameConfig.LogInfo);
        }

        // ==================== 关于 ====================

        private RectTransform _aboutPanel;
        private Text _aboutVersionText;
        private Text _aboutSaveText;
        private Text _aboutRuntimeText;
        private bool _aboutPanelWasOpen;

        /// <summary>
        /// 关于面板（roadmap A4 / C8）：版本、存档版本与槽位、数据位置、素材来源、工程说明。
        /// 原来这些信息只散在 README 里，游戏内无处可查；版本号取 PlayerSettings 的 bundleVersion。
        /// </summary>
        private void BuildAboutPanel(Transform root)
        {
            _aboutPanel = CreatePanel(root, "AboutPanel", 300, 100, 680, 520, GameConfig.OverlayBg, true);

            CreateSectionTitle(_aboutPanel, "关于", 24, 14, 240, FontTitle, "glyph_book");

            Button close = CreateButton(_aboutPanel, "AboutClose", "关闭", 566, 16, 90, 32,
                GameConfig.BtnNeutral, out Text closeLabel);
            StyleBar(close, closeLabel, "frame_pill", "glyph_close", 14f, "关闭关于");
            close.onClick.AddListener(OnAboutCloseClicked);

            _aboutVersionText = CreateText(_aboutPanel, "AboutVersion", "", 24, 52, 630, 24, FontBody,
                GameConfig.TextTitle, TextAnchor.MiddleLeft);
            _aboutSaveText = CreateText(_aboutPanel, "AboutSave", "", 24, 78, 630, 20, FontSmall,
                GameConfig.TextInfo, TextAnchor.MiddleLeft);
            _aboutRuntimeText = CreateText(_aboutPanel, "AboutRuntime", "", 24, 100, 630, 20, FontSmall,
                GameConfig.TextMuted, TextAnchor.MiddleLeft);

            CreateSectionTitle(_aboutPanel, "数据", 24, 136, 120, FontBody);
            CreateText(_aboutPanel, "AboutData",
                "存档写在本机 PlayerPrefs（3 个槽，与账号无关），卸载或清注册表即失效。\n" +
                "「导出」写 " + SaveSystem.ExportFolder + "\\slotN.json；「导入」优先读剪贴板，读不到再读同名文件。",
                34, 166, 610, 46, FontSmall, GameConfig.TextMuted, TextAnchor.UpperLeft);

            CreateSectionTitle(_aboutPanel, "素材", 24, 224, 120, FontBody);
            CreateText(_aboutPanel, "AboutAssets",
                "主角立绘、怪物、Boss、光环与背景为 AI 生成（火山方舟 Seedream），非人工绘制。\n" +
                "背景音乐四首来自「星落落oi」，署名与来源见 docs/audio-credits.md。",
                34, 254, 610, 46, FontSmall, GameConfig.TextMuted, TextAnchor.UpperLeft);

            CreateSectionTitle(_aboutPanel, "工程", 24, 312, 120, FontBody);
            CreateText(_aboutPanel, "AboutProject",
                "单机自动战斗 Demo：不联网、不上传任何数据，全部数值可离线复现。\n" +
                "设计、平衡与验收记录见 README.md、docs/ 与 _balance/。",
                34, 342, 610, 46, FontSmall, GameConfig.TextMuted, TextAnchor.UpperLeft);

            CreateText(_aboutPanel, "AboutFoot",
                "回归：Unity 菜单「工具 → 回归 → 跑一遍快速回归」，详见 README 的「自动化回归」。",
                24, 470, 630, 20, FontTiny, GameConfig.TextDisabled, TextAnchor.MiddleLeft);

            _aboutPanel.gameObject.SetActive(false);
        }

        private void RefreshAbout()
        {
            if (_aboutPanel == null || _game == null) return;

            bool open = _game.AboutOpen;
            _aboutPanel.gameObject.SetActive(open);
            if (!open)
            {
                _aboutPanelWasOpen = false;
                return;
            }

            bool justOpened = !_aboutPanelWasOpen;
            _aboutPanelWasOpen = true;
            if (!justOpened) return;

            string product = string.IsNullOrEmpty(Application.productName) ? "AutoBattle" : Application.productName;
            _aboutVersionText.text = product + "   v" + Application.version;
            _aboutSaveText.text = "存档 v" + SaveSystem.SaveVersion + " · " + SaveSystem.SlotCount + " 槽"
                + " · 当前 存档 " + (_game.SaveSlot + 1);
            _aboutRuntimeText.text = "Unity " + Application.unityVersion + " · " + Application.platform
                + " · 设计分辨率 1280×720";
        }

        private void OnAboutClicked()
        {
            _game.ToggleAboutPanel();
            GameAudio.PlayUiOpen();
        }

        private void OnAboutCloseClicked()
        {
            _game.CloseAboutPanel();
            GameAudio.PlayUiClose();
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
                GameConfig.RowBg, out Text skipLabel);
            StyleBar(skip, skipLabel, "frame_pill", "glyph_skip", 14f, "跳过新手引导", 12);
            skip.onClick.AddListener(OnGuideSkipClicked);

            Button next = CreateButton(_guideCard, "GuideNext", "下一步", 478, 146, 100, 32,
                GameConfig.BtnPrimary, out _guideNextLabel);
            StyleBar(next, _guideNextLabel, "frame_bar_round", "glyph_next", 16f, "下一步", 12);
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
                         + "\n装备有五个槽位：武器与衣服一开始就有，头盔 3-10、鞋 6-10、饰品 11-10 解锁；点装备卡可对比，锁定后掉落会等你手动换。",
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
                    body = "左上角的齿轮按钮打开设置，可以关掉飘字 / DPS / 评分 / 推荐分，切换自动换装，调音量，或重看引导。"
                         + "\n齿轮左边的音符按钮是背景音乐，四首可自选、列表循环或单曲循环。"
                         + "\n装备标题右边的天平按钮在有待选掉落时会亮起，用来比较当前装备与更强掉落。",
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
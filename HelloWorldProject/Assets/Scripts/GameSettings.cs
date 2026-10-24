using System.Runtime.InteropServices;
using UnityEngine;

namespace AutoBattle
{
    /// <summary>
    /// 玩家可切换的显示/自动化选项。
    /// 新增一项时同步改这里的三处：枚举、默认值数组、Label/Hint。
    /// </summary>
    public enum GameSettingId
    {
        /// <summary>战斗伤害飘字。</summary>
        DamageFloat,

        /// <summary>日志区上方的实时 DPS 胶囊。</summary>
        ShowDps,

        /// <summary>装备行尾的战斗力评分。</summary>
        ShowScore,

        /// <summary>技能卡片上的推荐分。</summary>
        ShowSkillAdvice,

        /// <summary>掉落比当前装备强时是否自动穿戴。</summary>
        AutoEquip,
    }

    /// <summary>
    /// 轻量设置存储：直接落 PlayerPrefs（和存档槽无关，换档也沿用）。
    /// 读是懒加载 + 缓存，写立刻 Save——设置项很少，不必攒批。
    /// 新手引导的「已完成」也放这里，但不在设置面板的开关列表里，
    /// 它是「重看引导」按钮的状态，不是一个可关的显示项。
    /// </summary>
    public static class GameSettings
    {
        private const string Prefix = "autobattle.setting.";
        private const string GuideKey = "autobattle.guide.done";
        private const string MasterVolumeKey = "autobattle.volume.master";
        private const string SfxVolumeKey = "autobattle.volume.sfx";
        private const string MusicVolumeKey = "autobattle.volume.music";
        private const string SpeedKey = "autobattle.setting.speed";
        private const float DefaultVolume = 0.8f;

        private static readonly bool[] Defaults = { true, true, true, true, true };
        private static readonly bool[] Values = new bool[5];
        private static bool _loaded;
        private static bool _guideDone;
        private static float _masterVolume = DefaultVolume;
        private static float _sfxVolume = DefaultVolume;
        private static float _musicVolume = DefaultVolume;
        private static int _speedIndex;

        public static bool DamageFloat => Get(GameSettingId.DamageFloat);
        public static bool ShowDps => Get(GameSettingId.ShowDps);
        public static bool ShowScore => Get(GameSettingId.ShowScore);
        public static bool ShowSkillAdvice => Get(GameSettingId.ShowSkillAdvice);
        public static bool AutoEquip => Get(GameSettingId.AutoEquip);

        /// <summary>主音量 0~1：同时作用在音效与背景音乐上。</summary>
        public static float MasterVolume
        {
            get { EnsureLoaded(); return _masterVolume; }
            set
            {
                EnsureLoaded();
                _masterVolume = Mathf.Clamp01(value);
                PlayerPrefs.SetFloat(MasterVolumeKey, _masterVolume);
                PlayerPrefs.Save();
            }
        }

        /// <summary>音效音量 0~1：与主音量相乘后才是实际播放音量。</summary>
        public static float SfxVolume
        {
            get { EnsureLoaded(); return _sfxVolume; }
            set
            {
                EnsureLoaded();
                _sfxVolume = Mathf.Clamp01(value);
                PlayerPrefs.SetFloat(SfxVolumeKey, _sfxVolume);
                PlayerPrefs.Save();
            }
        }

        /// <summary>背景音乐音量 0~1：与主音量相乘后才是实际音乐音量。</summary>
        public static float MusicVolume
        {
            get { EnsureLoaded(); return _musicVolume; }
            set
            {
                EnsureLoaded();
                _musicVolume = Mathf.Clamp01(value);
                PlayerPrefs.SetFloat(MusicVolumeKey, _musicVolume);
                PlayerPrefs.Save();
            }
        }

        /// <summary>
        /// 战斗速度档位（0=×1、1=×2、2=×4），和存档槽无关。
        /// 以前速度只在本次运行内有效，每次进入 Play 都回到 ×1；现在落盘后启动即沿用。
        /// 上限跟着 GameConfig.SpeedOptions 走，改速度档位表不用再改这里。
        /// </summary>
        public static int SpeedIndex
        {
            get { EnsureLoaded(); return _speedIndex; }
            set
            {
                EnsureLoaded();
                _speedIndex = Mathf.Clamp(value, 0, GameConfig.SpeedOptions.Length - 1);
                PlayerPrefs.SetInt(SpeedKey, _speedIndex);
                PlayerPrefs.Save();
            }
        }

        /// <summary>新手引导是否已走完/跳过。首次启动为 false，自动弹一次。</summary>
        public static bool GuideDone
        {
            get { EnsureLoaded(); return _guideDone; }
            set
            {
                EnsureLoaded();
                _guideDone = value;
                PlayerPrefs.SetInt(GuideKey, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        public static bool Get(GameSettingId id)
        {
            EnsureLoaded();
            return Values[(int)id];
        }

        public static void Set(GameSettingId id, bool value)
        {
            EnsureLoaded();
            Values[(int)id] = value;
            PlayerPrefs.SetInt(Prefix + (int)id, value ? 1 : 0);
            PlayerPrefs.Save();
        }

        /// <summary>切一下并返回新值，供开关按钮用。</summary>
        public static bool Toggle(GameSettingId id)
        {
            Set(id, !Get(id));
            return Get(id);
        }

        public static string Label(GameSettingId id)
        {
            switch (id)
            {
                case GameSettingId.DamageFloat: return "伤害飘字";
                case GameSettingId.ShowDps: return "实时 DPS";
                case GameSettingId.ShowScore: return "装备评分";
                case GameSettingId.ShowSkillAdvice: return "技能推荐分";
                case GameSettingId.AutoEquip: return "自动换装";
                default: return "设置";
            }
        }

        /// <summary>设置卡片右侧的一行说明，解释关掉之后会发生什么。</summary>
        public static string Hint(GameSettingId id)
        {
            switch (id)
            {
                case GameSettingId.DamageFloat: return "关闭后不再弹出伤害数字，日志与 DPS 不受影响";
                case GameSettingId.ShowDps: return "关闭后隐藏日志区上方的实时/期望 DPS";
                case GameSettingId.ShowScore: return "关闭后装备行只显示品质倍率，不显示评分";
                case GameSettingId.ShowSkillAdvice: return "关闭后技能卡片不再显示推荐分与 ★ 标记";
                case GameSettingId.AutoEquip:
                    return "关闭后更强的掉落进入「待选」，由你在对比面板手动换装";
                default: return string.Empty;
            }
        }

        /// <summary>恢复默认（不含新手引导的已完成标记）。</summary>
        public static void ResetToDefaults()
        {
            EnsureLoaded();
            for (int i = 0; i < Values.Length; i++)
            {
                Values[i] = Defaults[i];
                PlayerPrefs.SetInt(Prefix + i, Defaults[i] ? 1 : 0);
            }
            _speedIndex = 0;
            PlayerPrefs.SetInt(SpeedKey, 0);
            _masterVolume = DefaultVolume;
            _sfxVolume = DefaultVolume;
            _musicVolume = DefaultVolume;
            PlayerPrefs.SetFloat(MasterVolumeKey, _masterVolume);
            PlayerPrefs.SetFloat(SfxVolumeKey, _sfxVolume);
            PlayerPrefs.SetFloat(MusicVolumeKey, _musicVolume);
            PlayerPrefs.Save();
        }

        private static void EnsureLoaded()
        {
            if (_loaded) return;
            for (int i = 0; i < Values.Length; i++)
            {
                Values[i] = PlayerPrefs.GetInt(Prefix + i, Defaults[i] ? 1 : 0) != 0;
            }
            _guideDone = PlayerPrefs.GetInt(GuideKey, 0) != 0;
            _speedIndex = Mathf.Clamp(PlayerPrefs.GetInt(SpeedKey, 0), 0, GameConfig.SpeedOptions.Length - 1);
            _masterVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(MasterVolumeKey, DefaultVolume));
            _sfxVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(SfxVolumeKey, DefaultVolume));
            _musicVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(MusicVolumeKey, DefaultVolume));
            _loaded = true;
        }
    }

    /// <summary>
    /// 窗口/显示设置（roadmap C9）：全屏、分辨率预设、是否允许拖边缩放，以及色弱友好配色（C11）。
    /// 与存档槽无关，写 PlayerPrefs；游戏启动时由 GameController.Awake 调一次 Apply。
    /// <para>编辑器里只读写设置、不碰窗口——Game 视图的尺寸由编辑器自己管，
    /// 真去 SetResolution 会把 Play 视图缩成一个小方块。构建出来的 standalone 才应用。</para>
    /// </summary>
    public static class DisplaySettings
    {
        private const string FullscreenKey = "autobattle.display.fullscreen";
        private const string ResolutionKey = "autobattle.display.resolution";
        private const string ResizableKey = "autobattle.display.resizable";
        private const string ColorBlindKey = "autobattle.display.colorblind";

        /// <summary>分辨率预设：宽、高。第一项是设计分辨率，最后两项给 21:9 / 带鱼屏。</summary>
        private static readonly int[,] Presets =
        {
            { 1280, 720 },   // 16:9 设计分辨率
            { 1600, 900 },   // 16:9
            { 1920, 1080 },  // 16:9（默认）
            { 2560, 1440 },  // 16:9
            { 1280, 800 },   // 16:10
            { 1680, 720 },   // 21:9
            { 2560, 1080 },  // 21:9
            { 3440, 1440 },  // 21:9 带鱼屏
        };

        private const int DefaultResolutionIndex = 2;
        private static bool _loaded;
        private static bool _fullscreen;
        private static int _resolutionIndex = DefaultResolutionIndex;
        private static bool _resizable = true;
        private static bool _colorBlind;

        public static int ResolutionCount => Presets.GetLength(0);

        public static int ResolutionWidth(int index) => Presets[ClampResolution(index), 0];

        public static int ResolutionHeight(int index) => Presets[ClampResolution(index), 1];

        public static string ResolutionLabel(int index)
        {
            int i = ClampResolution(index);
            int w = Presets[i, 0];
            int h = Presets[i, 1];
            float ratio = (float)w / h;
            string ratioText = Mathf.Abs(ratio - 16f / 9f) < 0.01f ? "16:9"
                : Mathf.Abs(ratio - 16f / 10f) < 0.01f ? "16:10"
                : Mathf.Abs(ratio - 21f / 9f) < 0.01f ? "21:9" : ratio.ToString("0.00") + ":1";
            return w + "×" + h + "  " + ratioText;
        }

        /// <summary>全屏（独占全屏）。窗口模式下分辨率预设才会改窗口大小。</summary>
        public static bool Fullscreen
        {
            get { EnsureLoaded(); return _fullscreen; }
            set { EnsureLoaded(); _fullscreen = value; PlayerPrefs.SetInt(FullscreenKey, value ? 1 : 0); PlayerPrefs.Save(); }
        }

        public static int ResolutionIndex
        {
            get { EnsureLoaded(); return _resolutionIndex; }
            set
            {
                EnsureLoaded();
                _resolutionIndex = ClampResolution(value);
                PlayerPrefs.SetInt(ResolutionKey, _resolutionIndex);
                PlayerPrefs.Save();
            }
        }

        /// <summary>窗口模式下是否允许拖边缩放。全屏时无意义，UI 里会置灰。</summary>
        public static bool Resizable
        {
            get { EnsureLoaded(); return _resizable; }
            set { EnsureLoaded(); _resizable = value; PlayerPrefs.SetInt(ResizableKey, value ? 1 : 0); PlayerPrefs.Save(); }
        }

        /// <summary>色弱友好配色：品质色换成 Okabe-Ito 风格（绿/蓝/紫红/橙），文字编码照旧（品质名始终显示）。</summary>
        public static bool ColorBlind
        {
            get { EnsureLoaded(); return _colorBlind; }
            set { EnsureLoaded(); _colorBlind = value; PlayerPrefs.SetInt(ColorBlindKey, value ? 1 : 0); PlayerPrefs.Save(); }
        }

        /// <summary>把当前设置应用到窗口。编辑器内是空操作；构建后启动时调用一次。</summary>
        public static void Apply()
        {
            EnsureLoaded();
            if (Application.isEditor) return;

            int index = ClampResolution(_resolutionIndex);
            Screen.SetResolution(Presets[index, 0], Presets[index, 1], _fullscreen);

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            if (!_fullscreen) ApplyResizableWindow(_resizable);
#endif
        }

        public static void ResetToDefaults()
        {
            EnsureLoaded();
            _fullscreen = false;
            _resolutionIndex = DefaultResolutionIndex;
            _resizable = true;
            _colorBlind = false;
            PlayerPrefs.SetInt(FullscreenKey, 0);
            PlayerPrefs.SetInt(ResolutionKey, _resolutionIndex);
            PlayerPrefs.SetInt(ResizableKey, 1);
            PlayerPrefs.SetInt(ColorBlindKey, 0);
            PlayerPrefs.Save();
        }

        /// <summary>色弱友好是否开启；GameConfig 的品质色表用它选色板。</summary>
        public static bool UseColorBlindPalette => ColorBlind;

        private static int ClampResolution(int index) => Mathf.Clamp(index, 0, Presets.GetLength(0) - 1);

        private static void EnsureLoaded()
        {
            if (_loaded) return;
            _fullscreen = PlayerPrefs.GetInt(FullscreenKey, 0) != 0;
            _resolutionIndex = ClampResolution(PlayerPrefs.GetInt(ResolutionKey, DefaultResolutionIndex));
            _resizable = PlayerPrefs.GetInt(ResizableKey, 1) != 0;
            _colorBlind = PlayerPrefs.GetInt(ColorBlindKey, 0) != 0;
            _loaded = true;
        }

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        private const int GwlStyle = -16;
        private const int WsThickFrame = 0x00040000;
        private const int WsMaximizeBox = 0x00010000;
        private const uint SwpNoSize = 0x0001;
        private const uint SwpNoMove = 0x0002;
        private const uint SwpNoZOrder = 0x0004;
        private const uint SwpFrameChanged = 0x0020;

        [DllImport("user32.dll")]
        private static extern System.IntPtr GetActiveWindow();

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(System.IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(System.IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(System.IntPtr hWnd, System.IntPtr insertAfter,
            int x, int y, int cx, int cy, uint flags);

        private static void ApplyResizableWindow(bool resizable)
        {
            System.IntPtr hwnd = GetActiveWindow();
            if (hwnd == System.IntPtr.Zero) return;
            int style = GetWindowLong(hwnd, GwlStyle);
            int wanted = resizable ? (style | WsThickFrame | WsMaximizeBox) : (style & ~WsThickFrame);
            if (wanted == style) return;
            SetWindowLong(hwnd, GwlStyle, wanted);
            SetWindowPos(hwnd, System.IntPtr.Zero, 0, 0, 0, 0,
                SwpNoMove | SwpNoSize | SwpNoZOrder | SwpFrameChanged);
        }
#endif
    }
}

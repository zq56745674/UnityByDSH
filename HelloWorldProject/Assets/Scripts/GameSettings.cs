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
        private const float DefaultVolume = 0.8f;

        private static readonly bool[] Defaults = { true, true, true, true, true };
        private static readonly bool[] Values = new bool[5];
        private static bool _loaded;
        private static bool _guideDone;
        private static float _masterVolume = DefaultVolume;
        private static float _sfxVolume = DefaultVolume;
        private static float _musicVolume = DefaultVolume;

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
            _masterVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(MasterVolumeKey, DefaultVolume));
            _sfxVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(SfxVolumeKey, DefaultVolume));
            _musicVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(MusicVolumeKey, DefaultVolume));
            _loaded = true;
        }
    }
}

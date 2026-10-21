using UnityEngine;

namespace AutoBattle
{
    /// <summary>一首背景音乐的静态描述。ResourcePath 不带扩展名，供 Resources.Load 使用。</summary>
    public sealed class MusicTrack
    {
        public readonly string Title;
        public readonly string Artist;
        public readonly string ResourcePath;
        public readonly float Length;

        public MusicTrack(string title, string artist, string resourcePath, float length)
        {
            Title = title;
            Artist = artist;
            ResourcePath = resourcePath;
            Length = length;
        }

        public string DurationText
        {
            get
            {
                int total = Mathf.RoundToInt(Length);
                return $"{total / 60}:{total % 60:00}";
            }
        }
    }

    /// <summary>
    /// 背景音乐清单。四首歌都来自 D:\QQMusic，转成 MP3 后放在 Resources/Music 下。
    /// 资源缺失时对应条目不可播，不会让游戏报错。
    /// </summary>
    public static class MusicCatalog
    {
        public static readonly MusicTrack[] Tracks =
        {
            new MusicTrack("Let me go", "星落落oi", "Music/let_me_go", 218.6f),
            new MusicTrack("大肥鱼人是猫 (Remix)", "星落落oi", "Music/big_fish_cat", 134.8f),
            new MusicTrack("我会去拥抱他", "星落落oi", "Music/embrace", 167.3f),
            new MusicTrack("我的悲伤是水做的（大肥鱼版）", "星落落oi", "Music/sadness_water", 224.8f),
        };

        public static int Count => Tracks.Length;
    }

    /// <summary>
    /// 背景音乐播放器：单路 AudioSource + 手动播放列表循环。
    /// 默认自动播放；上次的曲目 / 播放状态 / 单曲循环设置写入 PlayerPrefs。
    /// 音乐音量 = GameSettings.MasterVolume × GameSettings.MusicVolume。
    /// </summary>
    public class GameMusic : MonoBehaviour
    {
        public static GameMusic Instance { get; private set; }

        private const string TrackKey = "autobattle.music.track";
        private const string PlayingKey = "autobattle.music.playing";
        private const string RepeatKey = "autobattle.music.repeatOne";

        private AudioSource _source;
        private readonly AudioClip[] _clips = new AudioClip[MusicCatalog.Count];
        private readonly bool[] _checked = new bool[MusicCatalog.Count];

        private int _index;
        private bool _wantedPlaying;
        private bool _repeatOne;
        private float _startedAt;

        public int CurrentIndex => _index;
        public bool RepeatOne => _repeatOne;

        /// <summary>UI 用「打算在播」而不是 AudioSource.isPlaying：切歌后第一帧 source 还没启动。</summary>
        public bool IsPlaying => _wantedPlaying;

        public MusicTrack CurrentTrack =>
            MusicCatalog.Tracks[Mathf.Clamp(_index, 0, MusicCatalog.Count - 1)];

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }

            Instance = this;

            _source = gameObject.AddComponent<AudioSource>();
            _source.playOnAwake = false;
            _source.loop = false;
            _source.spatialBlend = 0f;
            // 和音效一样不受 AudioListener.pause 影响：游戏内暂停只停战斗。
            _source.ignoreListenerPause = true;

            _index = Mathf.Clamp(PlayerPrefs.GetInt(TrackKey, 0), 0, MusicCatalog.Count - 1);
            _repeatOne = PlayerPrefs.GetInt(RepeatKey, 0) != 0;
            _wantedPlaying = PlayerPrefs.GetInt(PlayingKey, 1) != 0;
            LoadClip(_index);
        }

        private void Start()
        {
            RefreshVolume();

            if (_wantedPlaying && !TrackAvailable(_index))
            {
                // 当前曲目缺失时，自动找一首能播的；所有曲目都不在就安静地停着。
                _wantedPlaying = false;
                for (int i = 0; i < MusicCatalog.Count; i++)
                {
                    if (!TrackAvailable(i)) continue;
                    _index = i;
                    _wantedPlaying = true;
                    break;
                }
            }

            if (_wantedPlaying) PlayIndex(_index, true);
            else LoadClip(_index);
        }

        private void Update()
        {
            if (!_wantedPlaying || _source == null || _source.clip == null) return;
            if (_source.isPlaying) return;
            if (Time.unscaledTime - _startedAt < 0.25f) return;

            // 播放到末尾后 isPlaying 变 false；time 在部分平台上会归零，
            // 所以同时用「真实时间已经超过曲长」兜底，避免停住不切歌。
            bool ended = _source.time >= _source.clip.length - 0.08f
                         || Time.unscaledTime - _startedAt >= _source.clip.length + 0.2f;
            if (ended) AdvanceAfterEnd();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>播放指定曲目。restart=false 时，同一首歌从暂停位置继续。</summary>
        public bool PlayIndex(int index, bool restart)
        {
            if (_source == null) return false;
            if (index < 0 || index >= MusicCatalog.Count) return false;
            if (!TrackAvailable(index))
            {
                if (index == _index)
                {
                    _wantedPlaying = false;
                    SaveState();
                }
                return false;
            }

            bool same = index == _index;
            _index = index;
            if (_source.clip != _clips[index]) _source.clip = _clips[index];

            if (!same || restart || !_source.isPlaying)
            {
                _source.Stop();
                _source.time = 0f;
                _source.Play();
            }

            _wantedPlaying = true;
            _startedAt = Time.unscaledTime;
            RefreshVolume();
            SaveState();
            return true;
        }

        /// <summary>播放 / 暂停当前曲目。</summary>
        public bool TogglePlay()
        {
            if (_source == null) return false;

            if (_wantedPlaying)
            {
                _wantedPlaying = false;
                _source.Pause();
                SaveState();
                return true;
            }

            if (!TrackAvailable(_index)) return false;
            if (_source.clip == null) _source.clip = _clips[_index];
            _wantedPlaying = true;

            // 播完之后再点播放：从头开始；暂停后继续：从原位继续。
            if (_source.time <= 0.01f || _source.time >= _source.clip.length - 0.05f)
            {
                _source.Stop();
                _source.time = 0f;
                _source.Play();
            }
            else
            {
                _source.UnPause();
                if (!_source.isPlaying) _source.Play();
            }

            _startedAt = Time.unscaledTime;
            RefreshVolume();
            SaveState();
            return true;
        }

        public bool Next() => PlayRelative(1);
        public bool Previous() => PlayRelative(-1);

        private bool PlayRelative(int direction)
        {
            for (int n = 1; n <= MusicCatalog.Count; n++)
            {
                int next = (_index + direction * n) % MusicCatalog.Count;
                if (next < 0) next += MusicCatalog.Count;
                if (TrackAvailable(next)) return PlayIndex(next, true);
            }
            return false;
        }

        public bool ToggleRepeatOne()
        {
            _repeatOne = !_repeatOne;
            SaveState();
            return _repeatOne;
        }

        public void RefreshVolume()
        {
            if (_source != null)
                _source.volume = Mathf.Clamp01(GameSettings.MasterVolume * GameSettings.MusicVolume);
        }

        public bool TrackAvailable(int index)
        {
            if (index < 0 || index >= MusicCatalog.Count) return false;
            return LoadClip(index) != null;
        }

        private AudioClip LoadClip(int index)
        {
            if (index < 0 || index >= MusicCatalog.Count) return null;
            if (_checked[index]) return _clips[index];
            _checked[index] = true;
            _clips[index] = Resources.Load<AudioClip>(MusicCatalog.Tracks[index].ResourcePath);
            return _clips[index];
        }

        private void AdvanceAfterEnd()
        {
            if (_repeatOne)
            {
                PlayIndex(_index, true);
                return;
            }

            for (int n = 1; n <= MusicCatalog.Count; n++)
            {
                int next = (_index + n) % MusicCatalog.Count;
                if (TrackAvailable(next))
                {
                    PlayIndex(next, true);
                    return;
                }
            }

            // 所有曲目都不可用：停在当前曲目，不反复空转。
            _wantedPlaying = false;
            if (_source != null) _source.Stop();
            SaveState();
        }

        private void SaveState()
        {
            PlayerPrefs.SetInt(TrackKey, _index);
            PlayerPrefs.SetInt(PlayingKey, _wantedPlaying ? 1 : 0);
            PlayerPrefs.SetInt(RepeatKey, _repeatOne ? 1 : 0);
            PlayerPrefs.Save();
        }
    }
}

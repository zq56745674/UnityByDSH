using System.Collections.Generic;
using UnityEngine;

namespace AutoBattle
{
    /// <summary>
    /// 极简音效播放器：固定数量的 AudioSource 轮转，短音效全部走 PlayOneShot。
    /// 音效资源放在 Resources/Sfx 下，缺文件时静默——探针/无音频环境也能直接跑。
    /// 音量统一由 GameSettings.MasterVolume × SfxVolume 控制。
    /// </summary>
    public class GameAudio : MonoBehaviour
    {
        public static GameAudio Instance { get; private set; }

        private const string ResourceRoot = "Sfx/";
        private const int VoiceCount = 16;

        private readonly AudioSource[] _sources = new AudioSource[VoiceCount];
        private readonly Dictionary<string, AudioClip> _cache = new Dictionary<string, AudioClip>();
        private int _nextVoice;
        private float _lastHitTime = -1f;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }

            Instance = this;

            for (int i = 0; i < _sources.Length; i++)
            {
                var voice = new GameObject("SfxVoice" + i);
                voice.transform.SetParent(transform, false);
                AudioSource source = voice.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.loop = false;
                source.spatialBlend = 0f;
                source.ignoreListenerPause = true;
                source.volume = GameSettings.MasterVolume * GameSettings.SfxVolume;
                _sources[i] = source;
            }

            AchievementSystem.Unlocked += OnAchievementUnlocked;
        }

        private void OnDestroy()
        {
            AchievementSystem.Unlocked -= OnAchievementUnlocked;
            if (Instance == this) Instance = null;
        }

        /// <summary>设置面板改完音量后调用，让正在播放的短音效也立刻跟随。</summary>
        public static void RefreshVolumes()
        {
            if (Instance == null) return;
            float volume = GameSettings.MasterVolume * GameSettings.SfxVolume;
            for (int i = 0; i < Instance._sources.Length; i++)
            {
                if (Instance._sources[i] != null) Instance._sources[i].volume = volume;
            }

            // 主音量同样影响背景音乐；音乐还有自己的 MusicVolume。
            if (GameMusic.Instance != null) GameMusic.Instance.RefreshVolume();
        }

        public static void PlayUiClick() => Play("ui_click", 0.50f, 0.96f, 1.04f);
        public static void PlayUiOpen() => Play("ui_open", 0.55f);
        public static void PlayUiClose() => Play("ui_close", 0.55f);
        public static void PlayCoin() => Play("coin", 0.55f, 0.96f, 1.05f);
        public static void PlayLevelUp() => Play("level_up", 0.65f);
        public static void PlayAchievement() => Play("achievement", 0.70f);
        public static void PlayBossSpawn() => Play("boss_spawn", 0.70f, 0.82f, 0.88f);
        public static void PlayDeath() => Play("death", 0.72f, 0.78f, 0.86f);
        public static void PlayEnhance() => Play("enhance", 0.62f, 0.96f, 1.04f);
        public static void PlayReforge() => Play("reforge", 0.62f, 0.94f, 1.02f);
        public static void PlayBuy() => Play("buy", 0.58f);
        public static void PlayPotion() => Play("potion", 0.58f);

        /// <summary>命中音。追帧时一次 Tick 可能结算很多刀，这里按真实时间节流。</summary>
        public static void PlayHit(bool crit)
        {
            if (Instance == null) return;
            float now = Time.unscaledTime;
            if (now - Instance._lastHitTime < 0.045f) return;
            Instance._lastHitTime = now;
            Play(crit ? "crit" : "hit_light", crit ? 0.62f : 0.42f,
                crit ? 0.98f : 0.94f, crit ? 1.02f : 1.06f);
        }

        public static void Play(string key, float localVolume = 1f, float pitchMin = 1f, float pitchMax = 1f)
        {
            if (Instance == null || string.IsNullOrEmpty(key)) return;
            if (GameSettings.MasterVolume <= 0f || GameSettings.SfxVolume <= 0f) return;

            AudioClip clip = Instance.Load(key);
            if (clip == null) return;

            float volume = GameSettings.MasterVolume * GameSettings.SfxVolume;
            AudioSource source = Instance._sources[Instance._nextVoice];
            Instance._nextVoice = (Instance._nextVoice + 1) % Instance._sources.Length;
            source.volume = volume;
            source.pitch = pitchMin == pitchMax ? pitchMin : GameRandom.Range(pitchMin, pitchMax);
            source.PlayOneShot(clip, Mathf.Clamp01(localVolume));
        }

        private AudioClip Load(string key)
        {
            if (_cache.TryGetValue(key, out AudioClip cached)) return cached;
            AudioClip clip = Resources.Load<AudioClip>(ResourceRoot + key);
            _cache[key] = clip;
            return clip;
        }

        private void OnAchievementUnlocked(AchievementDef def, int gold)
        {
            PlayAchievement();
        }
    }
}

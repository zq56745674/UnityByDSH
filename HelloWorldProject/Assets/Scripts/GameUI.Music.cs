using UnityEngine;
using UnityEngine.UI;

namespace AutoBattle
{
    /// <summary>
    /// 背景音乐面板与顶部入口。
    /// 曲目列表、播放/暂停、上一首/下一首、单曲循环、音乐音量都在这一块；
    /// 播放状态与音量设置由 GameMusic / GameSettings 持有，这里只读状态并转发操作。
    /// </summary>
    public partial class GameUI
    {
        private Button _musicButton;
        private Text _musicButtonLabel;

        private RectTransform _musicPanel;
        private Text _musicNowText;
        private Button[] _musicTrackButtons;
        private Text[] _musicTrackLabels;
        private Image[] _musicTrackIcons;
        private Image[] _musicTrackAccents;
        private Button _musicPlayButton;
        private Text _musicPlayLabel;
        private Image _musicPlayIcon;
        private Button _musicLoopButton;
        private Text _musicLoopLabel;
        private Image[] _musicVolumeCells;
        private Text _musicVolumeValue;
        private Image _musicProgressFill;
        private Text _musicProgressText;

        private bool _musicPanelWasOpen;
        private int _musicUiSig = int.MinValue;

        private void BuildMusicPanel(Transform root)
        {
            _musicPanel = CreatePanel(root, "MusicPanel", 340, 126, 600, 524, GameConfig.OverlayBg, true);

            CreateSectionTitle(_musicPanel, "背景音乐", 24, 14, 300, FontTitle, "glyph_music");

            Button close = CreateButton(_musicPanel, "MusicClose", "关闭", 486, 16, 90, 32,
                GameConfig.BtnNeutral, out Text closeLabel);
            StyleBar(close, closeLabel, "frame_pill", "glyph_close", 16f, "关闭", 12);
            close.onClick.AddListener(OnMusicCloseClicked);

            _musicNowText = CreateText(_musicPanel, "MusicNow", "", 24, 52, 552, 22, 13,
                GameConfig.TextMuted, TextAnchor.MiddleLeft);

            _musicTrackButtons = new Button[MusicCatalog.Count];
            _musicTrackLabels = new Text[MusicCatalog.Count];
            _musicTrackIcons = new Image[MusicCatalog.Count];
            _musicTrackAccents = new Image[MusicCatalog.Count];

            for (int i = 0; i < MusicCatalog.Count; i++)
            {
                float y = 84f + i * 60f;
                Button track = CreateButton(_musicPanel, "MusicTrack" + i, "", 24, y, 552, 54,
                    GameConfig.CardBg, out Text trackLabel);

                int captured = i;
                track.onClick.AddListener(delegate { OnMusicTrackClicked(captured); });
                _musicTrackButtons[i] = track;

                // 左侧播放/暂停小图，当前曲目用金色强调条标出来。
                _musicTrackIcons[i] = CreateIcon(track.transform, "glyph_play", 12f, 13f, 28f);
                _musicTrackAccents[i] = CreateImage(track.transform, "Accent", 0f, 7f, 4f, 40f,
                    GameConfig.BorderSoft);
                Round(_musicTrackAccents[i], 2);

                trackLabel.fontSize = 15;
                trackLabel.fontStyle = FontStyle.Bold;
                trackLabel.alignment = TextAnchor.MiddleLeft;
                trackLabel.color = GameConfig.TextTitle;
                trackLabel.resizeTextForBestFit = false;
                trackLabel.lineSpacing = 1.1f;
                trackLabel.rectTransform.offsetMin = new Vector2(50f, 4f);
                trackLabel.rectTransform.offsetMax = new Vector2(-14f, -4f);
                _musicTrackLabels[i] = trackLabel;
            }

            // 控制行：上一首 / 播放暂停 / 下一首 / 循环模式。
            Button prev = CreateButton(_musicPanel, "MusicPrev", "", 24, 330, 52, 38,
                GameConfig.BtnNeutral, out Text prevLabel);
            StyleIcon(prev, prevLabel, "frame_pill", "glyph_prev", 22f, "上一首");
            prev.onClick.AddListener(OnMusicPrevClicked);

            _musicPlayButton = CreateButton(_musicPanel, "MusicPlay", "播放", 84, 330, 160, 38,
                GameConfig.BtnPrimary, out _musicPlayLabel);
            _musicPlayIcon = StyleBar(_musicPlayButton, _musicPlayLabel, "frame_pill", "glyph_play",
                22f, "播放 / 暂停", 15);
            _musicPlayButton.onClick.AddListener(OnMusicPlayClicked);

            Button next = CreateButton(_musicPanel, "MusicNext", "", 252, 330, 52, 38,
                GameConfig.BtnNeutral, out Text nextLabel);
            StyleIcon(next, nextLabel, "frame_pill", "glyph_next", 22f, "下一首");
            next.onClick.AddListener(OnMusicNextClicked);

            _musicLoopButton = CreateButton(_musicPanel, "MusicLoop", "列表循环", 316, 330, 236, 38,
                GameConfig.BtnNeutral, out _musicLoopLabel);
            StyleTab(_musicLoopButton, _musicLoopLabel, "frame_pill", 13);
            _musicLoopButton.onClick.AddListener(OnMusicLoopClicked);

            // 播放进度：细条 + 「当前 / 总长」。暂停时 AudioSource.time 不变，条自然冻结。
            Image progressBg = CreateImage(_musicPanel, "MusicProgress", 24, 380, 552, 6,
                GameConfig.RowBgDim, false);
            Round(progressBg, RadiusBar);
            _musicProgressFill = CreateImage(progressBg.rectTransform, "MusicProgressFill", 0, 0, 552, 6,
                GameConfig.LogGold, false);
            Round(_musicProgressFill, RadiusBar);
            Stretch(_musicProgressFill.rectTransform);
            _musicProgressText = CreateText(_musicPanel, "MusicProgressText", "0:00 / 0:00", 24, 392, 552, 18,
                11, GameConfig.TextMuted, TextAnchor.MiddleLeft);

            // 音乐音量：10 格 + 百分比 + ±。和设置面板的音量条保持同一种交互。
            CreateText(_musicPanel, "MusicVolumeLabel", "音乐音量", 24, 420, 100, 20, 13,
                GameConfig.TextNormal, TextAnchor.MiddleLeft);

            _musicVolumeCells = new Image[10];
            for (int i = 0; i < _musicVolumeCells.Length; i++)
            {
                float x = 130f + i * 21f;
                Image cell = CreateImage(_musicPanel, "MusicVol" + i, x, 423f, 18f, 14f,
                    GameConfig.VolumeCellOff, true);
                Round(cell, 3);
                AddBorder(cell, 3, GameConfig.VolumeCellBorder);

                var button = cell.gameObject.AddComponent<Button>();
                button.targetGraphic = cell;
                int captured = i;
                button.onClick.AddListener(delegate { OnMusicVolumeCellClicked(captured); });
                button.onClick.AddListener(GameAudio.PlayUiClick);

                _musicVolumeCells[i] = cell;
            }

            _musicVolumeValue = CreateText(_musicPanel, "MusicVolumeValue", "80%", 350, 420, 50, 20, 13,
                GameConfig.TextGold, TextAnchor.MiddleRight, FontStyle.Bold);

            Button down = CreateButton(_musicPanel, "MusicVolDown", "−", 410, 418, 32, 24,
                GameConfig.BtnNeutral, out Text downLabel);
            StyleTab(down, downLabel, "frame_pill", 18);
            down.onClick.AddListener(delegate { OnMusicVolumeStepClicked(-1); });

            Button up = CreateButton(_musicPanel, "MusicVolUp", "+", 450, 418, 32, 24,
                GameConfig.BtnNeutral, out Text upLabel);
            StyleTab(up, upLabel, "frame_pill", 18);
            up.onClick.AddListener(delegate { OnMusicVolumeStepClicked(1); });

            CreateText(_musicPanel, "MusicHint",
                "列表循环会按顺序播完四首再回到第一首；切到「单曲循环」只重复当前曲目。\n" +
                "音乐音量与主音量相乘；音乐文件在 Assets/Resources/Music 下。",
                24, 452, 552, 48, 13, GameConfig.TextNormal, TextAnchor.UpperLeft);

            _musicPanel.gameObject.SetActive(false);
        }

        private void OnMusicButtonClicked()
        {
            _game.ToggleMusicPanel();
            if (_game.MusicPanelOpen) GameAudio.PlayUiOpen();
            else GameAudio.PlayUiClose();
            _musicPanelWasOpen = false;
            _musicUiSig = int.MinValue;
        }

        private void OnMusicCloseClicked()
        {
            _game.CloseMusicPanel();
            GameAudio.PlayUiClose();
        }

        private void OnMusicTrackClicked(int index)
        {
            if (GameMusic.Instance != null && GameMusic.Instance.PlayIndex(index, true))
                _musicUiSig = int.MinValue;
        }

        private void OnMusicPlayClicked()
        {
            if (GameMusic.Instance != null) GameMusic.Instance.TogglePlay();
            _musicUiSig = int.MinValue;
        }

        private void OnMusicPrevClicked()
        {
            if (GameMusic.Instance != null) GameMusic.Instance.Previous();
            _musicUiSig = int.MinValue;
        }

        private void OnMusicNextClicked()
        {
            if (GameMusic.Instance != null) GameMusic.Instance.Next();
            _musicUiSig = int.MinValue;
        }

        private void OnMusicLoopClicked()
        {
            if (GameMusic.Instance != null) GameMusic.Instance.ToggleRepeatOne();
            _musicUiSig = int.MinValue;
        }

        private void OnMusicVolumeCellClicked(int index)
        {
            GameSettings.MusicVolume = (index + 1) / 10f;
            if (GameMusic.Instance != null) GameMusic.Instance.RefreshVolume();
            _musicUiSig = int.MinValue;
        }

        private void OnMusicVolumeStepClicked(int direction)
        {
            float next = Mathf.Round((GameSettings.MusicVolume + direction * 0.1f) * 10f) / 10f;
            GameSettings.MusicVolume = next;
            if (GameMusic.Instance != null) GameMusic.Instance.RefreshVolume();
            _musicUiSig = int.MinValue;
        }

        private void RefreshMusicPanel()
        {
            if (_musicPanel == null || _game == null) return;

            bool open = _game.MusicPanelOpen;
            _musicPanel.gameObject.SetActive(open);
            if (!open)
            {
                _musicPanelWasOpen = false;
                _musicUiSig = int.MinValue;
                return;
            }

            bool justOpened = !_musicPanelWasOpen;
            _musicPanelWasOpen = true;

            GameMusic music = GameMusic.Instance;
            int availableMask = 0;
            for (int i = 0; i < MusicCatalog.Count; i++)
            {
                if (music != null && music.TrackAvailable(i)) availableMask |= 1 << i;
            }

            int volumeStep = Mathf.RoundToInt(GameSettings.MusicVolume * 10f);
            int sig = (music != null ? music.CurrentIndex + 1 : 0)
                      | ((music != null && music.IsPlaying) ? 1 << 8 : 0)
                      | ((music != null && music.RepeatOne) ? 1 << 9 : 0)
                      | (volumeStep << 10)
                      | (availableMask << 16);
            // 进度条每帧都要动，所以放在「签名没变就早退」之前。
            UpdateMusicProgress(music);

            if (!justOpened && sig == _musicUiSig) return;
            _musicUiSig = sig;

            bool playing = music != null && music.IsPlaying;
            int current = music != null ? music.CurrentIndex : -1;
            bool repeat = music != null && music.RepeatOne;

            for (int i = 0; i < MusicCatalog.Count; i++)
            {
                bool available = (availableMask & (1 << i)) != 0;
                bool isCurrent = i == current;
                bool isPlaying = isCurrent && playing;

                _musicTrackButtons[i].interactable = available;
                _musicTrackButtons[i].image.color = isCurrent
                    ? Color.Lerp(GameConfig.CardBg, GameConfig.LogGold, 0.12f)
                    : GameConfig.CardBg;

                MusicTrack track = MusicCatalog.Tracks[i];
                string missing = available ? "" : "  <color=#FF8A80>（缺少文件）</color>";
                _musicTrackLabels[i].text =
                    $"<b>{track.Title}</b>{missing}\n" +
                    $"<size=11><color=#8C99B0>{track.Artist} · {track.DurationText}</color></size>";
                _musicTrackLabels[i].color = isCurrent ? GameConfig.TextGold : GameConfig.TextTitle;

                SetIcon(_musicTrackIcons[i], available
                    ? (isPlaying ? "glyph_pause" : "glyph_play")
                    : "glyph_close");
                if (_musicTrackIcons[i] != null)
                    _musicTrackIcons[i].color = available
                        ? (isCurrent ? Color.white : new Color(1f, 1f, 1f, 0.82f))
                        : new Color(1f, 1f, 1f, 0.35f);

                _musicTrackAccents[i].color = isCurrent ? GameConfig.LogGold : GameConfig.BorderSoft;
            }

            if (music == null)
                _musicNowText.text = "背景音乐组件未初始化。";
            else if (!music.TrackAvailable(music.CurrentIndex))
                _musicNowText.text = "当前曲目文件缺失（应放在 Assets/Resources/Music）。";
            else if (playing)
                _musicNowText.text = $"♪ 正在播放  {music.CurrentIndex + 1}/{MusicCatalog.Count}  ·  {music.CurrentTrack.Title}";
            else
                _musicNowText.text = $"已暂停  ·  {music.CurrentTrack.Title}";

            _musicPlayLabel.text = playing ? "暂停" : "播放";
            SetIcon(_musicPlayIcon, playing ? "glyph_pause" : "glyph_play");
            _musicLoopLabel.text = repeat ? "单曲循环" : "列表循环";

            for (int i = 0; i < _musicVolumeCells.Length; i++)
            {
                _musicVolumeCells[i].color = i < volumeStep ? GameConfig.LogGold : GameConfig.VolumeCellOff;
            }
            _musicVolumeValue.text = $"{Mathf.RoundToInt(GameSettings.MusicVolume * 100f)}%";
        }

        /// <summary>播放进度：暂停时 AudioSource.time 不会走，进度条与数字一起冻结。</summary>
        private void UpdateMusicProgress(GameMusic music)
        {
            if (_musicProgressFill == null) return;

            float elapsed = music != null ? music.ElapsedSeconds : 0f;
            float length = music != null ? music.TrackLengthSeconds : 0f;
            SetBar(_musicProgressFill, length > 0.01f ? Mathf.Clamp01(elapsed / length) : 0f);

            if (_musicProgressText != null)
                _musicProgressText.text = $"{FormatClock(elapsed)} / {FormatClock(length)}";
        }

        private static string FormatClock(float seconds)
        {
            int total = Mathf.Max(0, Mathf.FloorToInt(seconds));
            return $"{total / 60}:{total % 60:00}";
        }
    }
}

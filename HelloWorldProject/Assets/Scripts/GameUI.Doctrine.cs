using UnityEngine;
using UnityEngine.UI;

namespace AutoBattle
{
    /// <summary>
    /// 技能 2.0 的流派选择面板和左栏资源条（2026-10-26）。
    /// 面板不能关闭：P10 必须二选一，选完才能去技能面板花技能点；旧 v14 档也会在这里补选。
    /// </summary>
    public partial class GameUI
    {
        private RectTransform _doctrinePanel;
        private Text _doctrineTitle;
        private Text _doctrineSubtitle;
        private Button[] _doctrineCards;
        private Image[] _doctrineBorders;
        private Image[] _doctrineAccents;
        private Button _doctrineConfirm;
        private Text _doctrineConfirmLabel;
        private int _doctrineSelected;
        private bool _doctrineWasOpen;

        // ---- 左栏流派资源条 ----
        private RectTransform _resourceBarRoot;
        private Image[] _resourceDots;
        private Text _resourceText;
        private int _resourceSig = int.MinValue;

        private void BuildDoctrinePanel(Transform root)
        {
            _doctrinePanel = CreatePanel(root, "DoctrinePanel", 250, 200, 780, 320,
                GameConfig.OverlayBg, true);

            _doctrineTitle = CreateText(_doctrinePanel, "DoctrineTitle", "选择流派", 24, 16, 480, 32, 22,
                GameConfig.TextTitle, TextAnchor.MiddleLeft, FontStyle.Bold);
            _doctrineSubtitle = CreateText(_doctrinePanel, "DoctrineSubtitle",
                "本世锁定，转生后可重选；旧档会补发本流派技能。", 24, 48, 680, 20, 12,
                GameConfig.TextMuted, TextAnchor.MiddleLeft);

            _doctrineCards = new Button[2];
            _doctrineBorders = new Image[2];
            _doctrineAccents = new Image[2];

            for (int i = 0; i < 2; i++)
            {
                int captured = i;
                int doctrine = i + 1;
                Color school = SkillCatalog.SchoolColor(doctrine);
                float x = 24f + i * 356f;

                _doctrineCards[i] = CreateButton(_doctrinePanel, $"DoctrineCard{i}", "", x, 78f, 340f, 180f,
                    GameConfig.CardBg, out Text unused);
                if (unused != null) unused.text = string.Empty;

                _doctrineAccents[i] = CreateImage(_doctrineCards[i].transform, "Accent", 0f, 12f, 5f, 156f, school);

                _doctrineBorders[i] = CreateImage(_doctrineCards[i].transform, "Selected", 0f, 0f, 340f, 180f,
                    GameConfig.Border);
                _doctrineBorders[i].sprite = RingSprite(RadiusCard);
                _doctrineBorders[i].type = Image.Type.Sliced;
                _doctrineBorders[i].pixelsPerUnitMultiplier = 1f;
                _doctrineBorders[i].raycastTarget = false;
                Stretch(_doctrineBorders[i].rectTransform);

                CreateText(_doctrineCards[i].transform, "Name", doctrine == 1 ? "风暴之眼" : "山崩之心",
                    20f, 12f, 292f, 28f, 20, school, TextAnchor.MiddleLeft, FontStyle.Bold);
                CreateText(_doctrineCards[i].transform, "Tag", doctrine == 1 ? "风暴 · 高频暴击" : "山崩 · 重击破甲",
                    20f, 43f, 292f, 18f, 12, GameConfig.TextMuted, TextAnchor.MiddleLeft);

                string body = doctrine == 1
                    ? "雷印 0~8：普攻 +1，暴击额外 +1；满 8 层下一击变连锁闪电。\n每层 +1.5% 攻速，连锁命中还会感电。"
                    : "岩层 0~3：受击与每第 2 次普攻 +1；满 3 层下一击变崩山。\n每层 +2% 减伤，崩山会破甲与压制。";
                Text desc = CreateText(_doctrineCards[i].transform, "Desc", body, 20f, 66f, 300f, 66f, 12,
                    GameConfig.TextNormal, TextAnchor.UpperLeft);
                desc.lineSpacing = 1.12f;

                string build = doctrine == 1
                    ? "毕业：雷霆突袭 / 雷神降临 / 静电共鸣 / 风暴加速 / 电弧 / 雷殛"
                    : "毕业：崩山击 / 磐石壁垒 / 山岳之躯 / 沉重打击 / 余震 / 岩甲";
                CreateText(_doctrineCards[i].transform, "Build", build, 20f, 136f, 300f, 38f, 11,
                    GameConfig.TextMuted, TextAnchor.UpperLeft);

                _doctrineCards[i].onClick.AddListener(() => OnDoctrineCardClicked(captured));
            }

            _doctrineConfirm = CreateButton(_doctrinePanel, "DoctrineConfirm", "确认流派", 310f, 270f, 160f, 38f,
                GameConfig.BtnGold, out _doctrineConfirmLabel);
            _doctrineConfirm.onClick.AddListener(OnDoctrineConfirmClicked);

            _doctrinePanel.gameObject.SetActive(false);
        }

        private void OnDoctrineCardClicked(int index)
        {
            _doctrineSelected = index + 1;
            GameAudio.PlayUiClick();
            RefreshDoctrinePanel();
        }

        private void OnDoctrineConfirmClicked()
        {
            if (_doctrineSelected == 0) return;
            _game.ChooseDoctrine(_doctrineSelected);
            RefreshDoctrinePanel();
            RefreshSkillPanel();
            RefreshSkillBar(_game.Player, true);
        }

        private void RefreshDoctrinePanel()
        {
            if (_doctrinePanel == null) return;

            bool open = _game.DoctrinePanelOpen;
            _doctrinePanel.gameObject.SetActive(open);
            if (!open)
            {
                _doctrineWasOpen = false;
                return;
            }

            if (!_doctrineWasOpen)
            {
                _doctrineSelected = 0;
                _doctrineWasOpen = true;
            }

            PlayerData p = _game.Player;
            _doctrineSubtitle.text = p != null && p.pendingLegacyDoctrineGrant
                ? "旧档迁移：确认后获得 9 技能点，在技能面板里自选 9 个技能（不再随机送技能）。"
                : "本世锁定，转生后可重选；确认后按巅峰获得技能点，在技能面板里自选。";

            for (int i = 0; i < 2; i++)
            {
                bool selected = _doctrineSelected == i + 1;
                _doctrineBorders[i].color = selected ? GameConfig.LogGold : GameConfig.Border;
                _doctrineCards[i].image.color = selected
                    ? Color.Lerp(GameConfig.CardBg, SkillCatalog.SchoolColor(i + 1), 0.12f)
                    : GameConfig.CardBg;
            }

            _doctrineConfirm.interactable = _doctrineSelected > 0;
            _doctrineConfirmLabel.color = _doctrineSelected > 0 ? Color.white : GameConfig.TextDisabled;
        }

        /// <summary>左栏资源条：雷印 8 格 / 岩层 3 格，只随数值变化重画。</summary>
        private void BuildResourceBar(Transform panel)
        {
            _resourceBarRoot = CreateRect(panel, "ResourceBar", 16, 570, 388, 18);
            _resourceDots = new Image[8];
            for (int i = 0; i < _resourceDots.Length; i++)
            {
                _resourceDots[i] = CreateImage(_resourceBarRoot, $"Dot{i}", 0, 2, 30, 14, GameConfig.SlotTrough);
                Round(_resourceDots[i], RadiusBar);
            }

            _resourceText = CreateText(_resourceBarRoot, "ResourceText", "", 274, 0, 112, 18, 10,
                GameConfig.TextNormal, TextAnchor.MiddleLeft);
            _resourceText.horizontalOverflow = HorizontalWrapMode.Overflow;
            _resourceBarRoot.gameObject.SetActive(false);
        }

        private void RefreshResourceBar(PlayerData p)
        {
            if (_resourceBarRoot == null) return;

            bool show = p != null && p.HasDoctrine && _game.Battle != null;
            _resourceBarRoot.gameObject.SetActive(show);
            if (!show)
            {
                _resourceSig = int.MinValue;
                return;
            }

            int marks = p.doctrine == 1 ? _game.Battle.StormMarks : _game.Battle.MountainLayers;
            bool ready = p.doctrine == 1 ? _game.Battle.StormReady : _game.Battle.MountainReady;
            int sig = p.doctrine * 1000 + marks * 10 + (ready ? 1 : 0);
            if (sig == _resourceSig) return;
            _resourceSig = sig;

            int count = p.doctrine == 1 ? 8 : 3;
            float dotW = p.doctrine == 1 ? 30f : 84f;
            float gap = p.doctrine == 1 ? 4f : 6f;
            Color school = SkillCatalog.SchoolColor(p.doctrine);

            for (int i = 0; i < _resourceDots.Length; i++)
            {
                bool active = i < count;
                _resourceDots[i].gameObject.SetActive(active);
                if (!active) continue;

                var rt = _resourceDots[i].rectTransform;
                rt.sizeDelta = new Vector2(dotW, 14f);
                rt.anchoredPosition = new Vector2(i * (dotW + gap), -2f);

                bool filled = i < marks;
                bool edge = ready && i == count - 1;
                _resourceDots[i].color = edge ? GameConfig.LogGold : (filled ? school : GameConfig.SlotTrough);
            }

            if (p.doctrine == 1)
            {
                _resourceText.text = ready
                    ? "雷印 8/8 · 下一击连锁"
                    : $"雷印 {marks}/8 · 攻速 +{marks * 1.5f:0.#}%";
            }
            else
            {
                _resourceText.text = ready
                    ? "岩层 3/3 · 下一击崩山"
                    : $"岩层 {marks}/3 · 减伤 +{marks * 2}%";
            }
            _resourceText.color = ready ? GameConfig.LogGold : GameConfig.TextNormal;
        }
    }
}

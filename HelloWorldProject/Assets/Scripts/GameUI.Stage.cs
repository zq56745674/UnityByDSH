using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace AutoBattle
{
    /// <summary>
    /// P4 战斗舞台：把「日志卡」换成能看见主角和怪物的舞台（布局 A），日志收成两行 ticker，
    /// 点 ▲ 往上展开成可滚动的大日志，点 ▼ 收回原样。
    /// <para>
    /// 为什么这么做：挂机游戏 90% 的时间没人看屏幕，但抬头的那一眼必须能立刻回答
    /// 「我在打谁、打得痛不痛、我要不要切关」。纯文字日志读起来像控制台，看不出「谁在赢」。
    /// </para>
    /// <para>
    /// 动画全部是程序化的（呼吸、前冲、受击抖动、死亡淡出），没有骨骼也没有序列帧：
    /// 每只怪一帧美术，靠位移/缩放/旋转就能让它「活」起来，资源体积也就几张 PNG。
    /// </para>
    /// </summary>
    public partial class GameUI
    {
        // ==================== 舞台几何 ====================
        // 全部是舞台卡内部坐标：原点左上、y 向下（和 CreateRect 的约定一致）。
        private const float StageW = 808f;
        private const float StageH = 332f;
        private const float StageGroundY = 250f;      // 脚底线：主角/怪物都站在这里
        private const float StageHeroCx = 178f;
        private const float StageMonsterCx = 604f;
        private const float StageHeroH = 190f;
        private const float StageMonsterH = 192f;
        private const float StageEliteH = 206f;
        private const float StageBossH = 226f;
        private const float StageTickerY = 262f;
        private const float StageTickerH = 56f;
        private const float StageLogPanelH = 256f;
        private const int StageFxPool = 12;
        private const int MaxDetailLogLines = 180;

        /// <summary>同一个特效两次触发的最小间隔（秒）：连击/多段技能同帧爆一堆会糊成白光。</summary>
        private const float FxMinInterval = 0.07f;
        private const float BuffFxMinInterval = 0.45f;

        // ---- 舞台控件 ----
        private Image _battleStageCard;
        private RectTransform _battleStageRoot;
        private Image _battleHero;
        private Image _battleMonster;
        private Image _battleHeroShadow;
        private Image _battleMonsterShadow;
        private Text _battleChapterText;

        // ---- 日志：ticker（两行） + 展开面板 ----
        private RectTransform _battleLogPanel;
        private RectTransform _battleLogContent;
        private Text _battleLogDetail;
        private Text _battleLogHeader;
        private ScrollRect _battleLogScroll;
        private Button _battleLogToggle;
        private Text _battleLogToggleLabel;
        private bool _battleLogOpen;
        private float _battleLogExpand;   // 0 = 收起，1 = 展开；每帧插值，不看游戏速度

        // ---- 特效池 ----
        private RectTransform _battleFxRoot;
        private Image[] _fxImages;
        private float[] _fxT;
        private float[] _fxLife;
        private float[] _fxSize;
        private float[] _fxGrow;
        private float[] _fxSpin;
        private readonly Dictionary<BattleFx, float> _fxLastPlay = new Dictionary<BattleFx, float>();

        // ---- 运行时状态 ----
        private float _battleAnimTime;
        private MonsterData _battleMonsterData;
        private float _battleMonsterHeight = StageMonsterH;
        private float _battleMonsterSpawn = 1f;
        private float _battleMonsterShake;
        private bool _battleMonsterDying;
        private float _battleMonsterDeathT;
        private float _battleHeroLunge;
        private float _battleHeroShake;
        private bool _battleHeroDead;
        private float _battleHeroDeathT;
        private int _battleLastLevel = -1;
        private string _battleChapterLabel;
        private Sprite _battleHeroSprite;
        private BattleSystem _battleVisualSource;

        private readonly Dictionary<string, Sprite> _spriteCache = new Dictionary<string, Sprite>();

        // ==================== 搭建 ====================

        /// <summary>建战斗舞台（替换原来的日志卡），同时把 _logCard 指向它——引导高亮仍打在这一块上。</summary>
        private void BuildBattleStage(Transform panel)
        {
            _battleStageCard = CreateCard(panel, "BattleStageCard", 8, 154, StageW, StageH,
                GameConfig.InsetBg, RadiusCard, GameConfig.BorderSoft);
            _logCard = _battleStageCard;

            _battleStageRoot = CreateRect(panel, "BattleStage", 8, 154, StageW, StageH);
            // 舞台是「窗口」：特效应在卡片边缘被裁掉，而不是糊到描边外面。
            _battleStageRoot.gameObject.AddComponent<RectMask2D>();

            // ---- 背景：暗底 + 冷青光雾 + 地面 ----
            Image backdrop = CreateImage(_battleStageRoot, "StageBackdrop", 0, 0, StageW, StageH, GameConfig.Clear);
            backdrop.sprite = null;
            backdrop.color = new Color(0.031f, 0.039f, 0.060f, 1f);
            Stretch(backdrop.rectTransform);

            Image haze = CreateImage(_battleStageRoot, "StageHaze", -60, 96, 928, 300,
                new Color(0.27f, 0.66f, 0.60f, 0.075f));
            haze.sprite = GlowSprite();   // 一片很淡的冷青光，给背景一点纵深
            Stretch(haze.rectTransform);

            Image floor = CreateImage(_battleStageRoot, "StageFloor", 0, StageGroundY, StageW, StageH - StageGroundY,
                new Color(0.070f, 0.086f, 0.128f, 0.55f));
            floor.sprite = null;

            Image floorLine = CreateImage(_battleStageRoot, "StageFloorLine", 0, StageGroundY, StageW, 2f,
                new Color(0.55f, 0.85f, 0.82f, 0.16f));
            floorLine.sprite = null;

            _battleMonsterShadow = CreateStageShadow("MonsterShadow", StageMonsterCx, 150f);
            _battleHeroShadow = CreateStageShadow("HeroShadow", StageHeroCx, 120f);

            _battleHero = CreateStageActor("BattleHero", StageHeroCx);
            _battleMonster = CreateStageActor("BattleMonster", StageMonsterCx);

            _battleFxRoot = CreateRect(_battleStageRoot, "StageFx", 0, 0, StageW, StageH);
            _fxImages = new Image[StageFxPool];
            _fxT = new float[StageFxPool];
            _fxLife = new float[StageFxPool];
            _fxSize = new float[StageFxPool];
            _fxGrow = new float[StageFxPool];
            _fxSpin = new float[StageFxPool];
            for (int i = 0; i < StageFxPool; i++)
            {
                Image fx = CreateImage(_battleFxRoot, "Fx" + i, 0, 0, 128, 128, Color.white);
                RectTransform rt = fx.rectTransform;
                rt.anchorMin = new Vector2(0f, 1f);
                rt.anchorMax = new Vector2(0f, 1f);
                rt.pivot = new Vector2(0.5f, 0.5f);   // 以中心缩放/旋转，特效不会「跑偏」
                fx.enabled = false;
                _fxImages[i] = fx;
            }

            _battleChapterText = CreateText(_battleStageRoot, "StageChapter", "", 16, 8, 460, 22, FontSmall,
                new Color(0.70f, 0.80f, 0.90f, 0.85f), TextAnchor.MiddleLeft);

            // ---- 日志 ticker：两行，点一下往上展开 ----
            Image tickerHit = CreateImage(_battleStageRoot, "LogTickerHit", 8, StageTickerY - 6, 748, StageTickerH,
                GameConfig.Clear, true);
            var tickerButton = tickerHit.gameObject.AddComponent<Button>();
            tickerButton.transition = Selectable.Transition.None;
            tickerButton.onClick.AddListener(ToggleBattleLog);

            _logText = CreateText(_battleStageRoot, "LogTicker", "", 14, StageTickerY, 744, StageTickerH,
                FontSmall, GameConfig.TextNormal, TextAnchor.LowerLeft);
            _logText.horizontalOverflow = HorizontalWrapMode.Wrap;
            _logText.verticalOverflow = VerticalWrapMode.Truncate;
            _logText.lineSpacing = 1.12f;

            _battleLogToggle = CreateButton(_battleStageRoot, "LogToggle", "▲", 764, StageTickerY + 8, 28, 30,
                GameConfig.RowBg, out _battleLogToggleLabel);
            _battleLogToggleLabel.fontSize = 13;
            _battleLogToggle.onClick.AddListener(ToggleBattleLog);

            BuildBattleLogPanel();
        }

        private Image CreateStageShadow(string name, float cx, float w)
        {
            Image shadow = CreateImage(_battleStageRoot, name, 0, 0, w, 26, new Color(0f, 0f, 0f, 0.55f));
            shadow.sprite = GlowSprite();   // 软边椭圆：精灵脚底垫一层影子才「站得住」
            RectTransform rt = shadow.rectTransform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(cx, -(StageGroundY + 6f));
            rt.sizeDelta = new Vector2(w, 26f);
            shadow.enabled = false;
            return shadow;
        }

        /// <summary>战斗单位：锚在舞台左上、轴心在脚底，所以缩放是「绕脚长高」而不是绕中心鼓起来。</summary>
        private Image CreateStageActor(string name, float cx)
        {
            Image img = CreateImage(_battleStageRoot, name, 0, 0, 10, 10, Color.white);
            RectTransform rt = img.rectTransform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(cx, -StageGroundY);
            rt.sizeDelta = new Vector2(10f, 10f);
            img.preserveAspect = true;
            img.enabled = false;
            return img;
        }

        /// <summary>
        /// 展开面板：底部起长、盖住整个舞台。用 ScrollRect 而不是自己算偏移，
        /// 是为了白拿惯性滚动和滚轮/拖动；内容高度用 TextGenerator 量。
        /// </summary>
        private void BuildBattleLogPanel()
        {
            _battleLogPanel = CreateRect(_battleStageRoot, "BattleLogPanel", 0, 0, StageW, 0f);
            RectTransform panelRt = _battleLogPanel;
            panelRt.anchorMin = new Vector2(0f, 0f);
            panelRt.anchorMax = new Vector2(0f, 0f);
            panelRt.pivot = new Vector2(0f, 0f);
            // 底边贴住 ticker 上沿，向上生长：收起时高度 0 → 只剩 ticker，展开时盖住舞台。
            panelRt.anchoredPosition = new Vector2(0f, StageH - StageTickerY + 2f);
            panelRt.sizeDelta = new Vector2(StageW, 0f);

            var bg = _battleLogPanel.gameObject.AddComponent<Image>();
            // 必须完全不透明：0.985 的 alpha 会让底下的主角/怪物透出来，日志文字叠在角色剪影上很难读。
            bg.color = new Color(0.035f, 0.043f, 0.066f, 1f);
            Round(bg, RadiusCard);
            AddBorder(bg, RadiusCard, GameConfig.Border);
            bg.raycastTarget = true;   // 吃点击：展开时不该点穿到底下的舞台按钮

            _battleLogHeader = CreateText(_battleLogPanel, "BattleLogHeader", "", 16, 6, 620, 22, FontSmall,
                GameConfig.TextMuted, TextAnchor.MiddleLeft);

            RectTransform viewport = CreateRect(_battleLogPanel, "BattleLogViewport", 0, 0, 0, 0);
            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
            viewport.offsetMin = new Vector2(16f, 10f);
            viewport.offsetMax = new Vector2(-16f, -30f);
            viewport.gameObject.AddComponent<RectMask2D>();
            Image viewportHit = viewport.gameObject.AddComponent<Image>();
            viewportHit.color = GameConfig.Clear;
            viewportHit.raycastTarget = true;

            _battleLogContent = CreateRect(viewport, "BattleLogContent", 0, 0, 0, 10f);
            _battleLogContent.anchorMin = new Vector2(0f, 1f);
            _battleLogContent.anchorMax = new Vector2(1f, 1f);
            _battleLogContent.pivot = new Vector2(0.5f, 1f);
            _battleLogContent.anchoredPosition = Vector2.zero;
            _battleLogContent.sizeDelta = new Vector2(0f, 10f);

            // 整段日志用一个 Text：180 条行式布局的 GameObject 开销比一个 12k 字的文本高得多。
            _battleLogDetail = CreateText(_battleLogContent, "BattleLogDetail", "", 0, 0, 0, 0,
                FontSmall, GameConfig.TextNormal, TextAnchor.UpperLeft);
            Stretch(_battleLogDetail.rectTransform);
            _battleLogDetail.horizontalOverflow = HorizontalWrapMode.Wrap;
            _battleLogDetail.verticalOverflow = VerticalWrapMode.Overflow;
            _battleLogDetail.lineSpacing = 1.15f;
            _battleLogDetail.raycastTarget = true;

            _battleLogScroll = _battleLogPanel.gameObject.AddComponent<ScrollRect>();
            _battleLogScroll.viewport = viewport;
            _battleLogScroll.content = _battleLogContent;
            _battleLogScroll.horizontal = false;
            _battleLogScroll.vertical = true;
            _battleLogScroll.movementType = ScrollRect.MovementType.Clamped;
            _battleLogScroll.inertia = true;
            _battleLogScroll.decelerationRate = 0.12f;
            _battleLogScroll.scrollSensitivity = 26f;

            _battleLogPanel.gameObject.SetActive(false);
        }

        // ==================== 每帧推进 ====================

        /// <summary>
        /// 舞台的全部表现都在这里按**真实时间**推进：战斗可以 x4，但呼吸和抖动按战斗速度走
        /// 会变成抽搐。dt 上限 0.05 秒，卡一帧时不会瞬移。
        /// </summary>
        private void TickBattleStage(float realDt)
        {
            if (_battleStageRoot == null) return;

            EnsureBattleVisualHook();

            float dt = Mathf.Min(realDt, 0.05f);
            _battleAnimTime += dt;

            TickBattleLogPanel(dt);
            BindBattleActors();
            TickBattleHero(dt);
            TickBattleMonster(dt);
            TickBattleFx(dt);
            CheckStageLevelUp();
            RefreshBattleChapterLabel();
        }

        /// <summary>怪物引用只在「换怪 / 怪物消失」时变；精灵缺失（美术没导入）时宁可空着也不放个白框。</summary>
        private void BindBattleActors()
        {
            if (_game == null) return;

            MonsterData live = _game.Monster;
            if (live != null && !ReferenceEquals(live, _battleMonsterData))
            {
                _battleMonsterData = live;
                _battleMonsterDying = false;
                _battleMonsterDeathT = 0f;
                _battleMonsterSpawn = 0f;
                _battleMonsterShake = 0f;
                _battleMonsterHeight = MonsterDisplayHeight(live);
                ApplyActorSprite(_battleMonster, MonsterSpriteFor(live), _battleMonsterHeight, StageMonsterCx);
                if (_battleMonsterShadow != null)
                {
                    _battleMonsterShadow.enabled = _battleMonster.enabled;
                    _battleMonsterShadow.rectTransform.sizeDelta =
                        new Vector2(_battleMonsterHeight * 0.78f, 26f);
                }
            }
            else if (live == null && _battleMonsterData != null && !_battleMonsterDying)
            {
                // 怪物引用被清空 = 打死了（或主角阵亡）。先放死亡特效，再淡出。
                _battleMonsterDying = true;
                _battleMonsterDeathT = 0f;
                PlayBattleFxSprite("fx_death", StageMonsterCx,
                    StageGroundY - _battleMonsterHeight * 0.42f, 190f, 0.5f, 1.7f, 0f);
            }

            if (_battleHero != null && _battleHeroSprite == null)
            {
                _battleHeroSprite = SpriteOf("Sprites/Hero/hero_battle");
                if (_battleHeroSprite != null)
                {
                    ApplyActorSprite(_battleHero, _battleHeroSprite, StageHeroH, StageHeroCx);
                    if (_battleHeroShadow != null) _battleHeroShadow.enabled = true;
                }
            }
        }

        private void TickBattleHero(float dt)
        {
            if (_battleHero == null || !_battleHero.enabled) return;

            bool dead = _game != null && _game.Player != null && _game.Player.currentHp <= 0;
            if (dead && !_battleHeroDead)
            {
                _battleHeroDead = true;
                _battleHeroDeathT = 0f;
                PlayBattleFxSprite("fx_death", StageHeroCx, StageGroundY - 70f, 150f, 0.45f, 1.55f, 0f);
            }
            else if (!dead)
            {
                _battleHeroDead = false;
                _battleHeroDeathT = 0f;
            }

            if (_battleHeroDead) _battleHeroDeathT += dt;
            float deathK = _battleHeroDead ? Mathf.Clamp01(_battleHeroDeathT / 0.4f) : 0f;

            // 前冲：0 → 18px（60ms）→ 0（220ms）。挂机 x4 下每秒好几次，所以只给一点点位移。
            _battleHeroLunge += dt;
            float lunge = _battleHeroLunge < 0.06f
                ? Mathf.Lerp(0f, 18f, _battleHeroLunge / 0.06f)
                : Mathf.Lerp(18f, 0f, Mathf.Clamp01((_battleHeroLunge - 0.06f) / 0.22f));
            if (_battleHeroLunge > 0.30f) lunge = 0f;

            _battleHeroShake = Mathf.MoveTowards(_battleHeroShake, 0f, dt * 6.5f);
            float shake = Mathf.Sin(_battleAnimTime * 42f) * 6f * _battleHeroShake;

            float breath = Mathf.Sin(_battleAnimTime * 1.7f);
            RectTransform rt = _battleHero.rectTransform;
            rt.localScale = new Vector3(
                (1f - breath * 0.006f) * (1f - deathK * 0.10f),
                (1f + breath * 0.010f) * (1f - deathK * 0.16f), 1f);
            rt.anchoredPosition = new Vector2(StageHeroCx + lunge + shake, -StageGroundY);
            rt.localRotation = Quaternion.Euler(0f, 0f, deathK * -14f);

            SetImageAlpha(_battleHero, 1f - deathK);
            if (_battleHeroShadow != null) SetImageAlpha(_battleHeroShadow, 0.9f * (1f - deathK));
        }

        private void TickBattleMonster(float dt)
        {
            if (_battleMonster == null) return;

            _battleMonsterShake = Mathf.MoveTowards(_battleMonsterShake, 0f, dt * 7f);

            float deathK = 0f;
            if (_battleMonsterDying)
            {
                _battleMonsterDeathT += dt;
                deathK = Mathf.Clamp01(_battleMonsterDeathT / 0.42f);
                if (deathK >= 1f)
                {
                    _battleMonsterData = null;
                    _battleMonsterDying = false;
                    _battleMonster.enabled = false;
                    if (_battleMonsterShadow != null) _battleMonsterShadow.enabled = false;
                    return;
                }
            }

            if (!_battleMonster.enabled) return;

            float spawnK = Mathf.Clamp01(_battleMonsterSpawn * 3f);   // 入场淡入 + 上浮
            _battleMonsterSpawn = Mathf.Min(1f, _battleMonsterSpawn + dt * 3.6f);

            float breath = Mathf.Sin(_battleAnimTime * 1.45f + 1.7f);
            float shake = Mathf.Sin(_battleAnimTime * 46f) * 7f * _battleMonsterShake;
            RectTransform rt = _battleMonster.rectTransform;
            rt.localScale = new Vector3(
                (1f + breath * 0.008f) * (1f - deathK * 0.20f) * (0.92f + 0.08f * spawnK),
                (1f - breath * 0.010f) * (1f - deathK * 0.30f) * (0.92f + 0.08f * spawnK), 1f);
            rt.anchoredPosition = new Vector2(StageMonsterCx + shake,
                -StageGroundY + (1f - spawnK) * 14f);
            rt.localRotation = Quaternion.Euler(0f, 0f, -deathK * 18f);

            SetImageAlpha(_battleMonster, Mathf.Clamp01(spawnK * 1.6f) * (1f - deathK));
            if (_battleMonsterShadow != null)
            {
                SetImageAlpha(_battleMonsterShadow,
                    0.85f * Mathf.Clamp01(_battleMonsterSpawn * 1.4f) * (1f - deathK * 0.7f));
            }
        }

        private void TickBattleFx(float dt)
        {
            if (_fxImages == null) return;

            for (int i = 0; i < _fxImages.Length; i++)
            {
                Image img = _fxImages[i];
                if (img == null || !img.enabled) continue;

                _fxT[i] += dt;
                float k = _fxLife[i] <= 0f ? 1f : Mathf.Clamp01(_fxT[i] / _fxLife[i]);
                if (k >= 1f)
                {
                    img.enabled = false;
                    continue;
                }

                float scale = _fxSize[i] * Mathf.Lerp(0.68f, _fxGrow[i], Mathf.Sqrt(k));
                img.rectTransform.sizeDelta = new Vector2(scale, scale);
                img.rectTransform.localRotation = Quaternion.Euler(0f, 0f, _fxSpin[i] * k);
                SetImageAlpha(img, k < 0.55f ? 1f : Mathf.Lerp(1f, 0f, (k - 0.55f) / 0.45f));
            }
        }

        /// <summary>章节/场地标签只在真的换章时重写文本（每帧拼字符串没必要）。</summary>
        private void RefreshBattleChapterLabel()
        {
            if (_battleChapterText == null || _game == null || _game.Player == null) return;

            string label;
            if (_game.EndlessActive && _game.EndlessRun != null)
            {
                label = $"无尽 · 第 {_game.EndlessRun.floor} 层";
            }
            else
            {
                StageDef stage = _game.Player.CurrentStage;
                int chapter = Mathf.Clamp(stage.chapter, 1, GameConfig.ChapterCount);
                label = $"第 {chapter} 章 · {GameConfig.ChapterFamilyNames[chapter - 1]}";
            }

            if (label == _battleChapterLabel) return;
            _battleChapterLabel = label;
            _battleChapterText.text = label;
        }

        // ==================== 日志展开 / 收起 ====================

        private void ToggleBattleLog()
        {
            _battleLogOpen = !_battleLogOpen;
            if (_battleLogToggleLabel != null) _battleLogToggleLabel.text = _battleLogOpen ? "▼" : "▲";

            // 展开时强制重排一次：收起状态下内容宽度可能还是 0，量的高度不作数。
            _lastLogVersion = -1;
        }

        private void TickBattleLogPanel(float dt)
        {
            _battleLogExpand = Mathf.MoveTowards(_battleLogExpand, _battleLogOpen ? 1f : 0f, dt * 6.5f);

            if (_battleLogPanel != null)
            {
                float h = StageLogPanelH * Mathf.SmoothStep(0f, 1f, _battleLogExpand);
                _battleLogPanel.sizeDelta = new Vector2(StageW, h);

                bool visible = h > 2f;
                if (_battleLogPanel.gameObject.activeSelf != visible)
                    _battleLogPanel.gameObject.SetActive(visible);
            }

            // 展开日志时把伤害飘字层收掉：读日志的时候不想被一堆数字糊在脸上。
            if (_floatLayer != null)
            {
                bool showFloats = _battleLogExpand < 0.05f;
                if (_floatLayer.gameObject.activeSelf != showFloats)
                    _floatLayer.gameObject.SetActive(showFloats);
            }

        }

        /// <summary>
        /// 把整段日志塞进展开面板并按实际换行量出高度。
        /// 用 cachedTextGeneratorForLayout 而不是 Canvas.ForceUpdateCanvases：
        /// 后者会强制整块画布重排，日志每秒更新几十次时就是几毫秒的纯浪费。
        /// </summary>
        private void FillBattleLogDetail(string richText, int totalEntries)
        {
            if (_battleLogDetail == null || _battleLogContent == null) return;

            _battleLogDetail.text = richText;

            float width = _battleLogContent.rect.width;
            if (width < 10f) width = StageW - 32f;

            TextGenerationSettings settings = _battleLogDetail.GetGenerationSettings(new Vector2(width, 0f));
            settings.horizontalOverflow = HorizontalWrapMode.Wrap;
            settings.verticalOverflow = VerticalWrapMode.Overflow;
            float height = _battleLogDetail.cachedTextGeneratorForLayout
                .GetPreferredHeight(richText, settings) / _battleLogDetail.pixelsPerUnit;

            _battleLogContent.sizeDelta = new Vector2(0f, Mathf.Max(10f, height + 8f));

            if (_battleLogHeader != null)
            {
                _battleLogHeader.text = totalEntries > MaxDetailLogLines
                    ? $"战斗日志 · 最近 {MaxDetailLogLines} 条（共 {totalEntries} 条，滚轮或拖动查看，点 ▼ 收回）"
                    : $"战斗日志 · 共 {totalEntries} 条（滚轮或拖动查看，点 ▼ 收回）";
            }

            if (_battleLogOpen && _battleLogScroll != null)
                _battleLogScroll.verticalNormalizedPosition = 0f;   // 新日志在最下面，跟到底
        }

        // ==================== 战斗事件 → 表现 ====================

        /// <summary>
        /// 订阅战斗表现事件。每帧调用一次而不是只在 Initialize 里订一次：
        /// 读档 / 重置有可能换掉 BattleSystem 实例，按引用比较既能自动跟上新实例，
        /// 也不会重复订阅（重复订阅 = 每个特效放两遍）。
        /// </summary>
        private void EnsureBattleVisualHook()
        {
            BattleSystem battle = _game != null ? _game.Battle : null;
            if (ReferenceEquals(battle, _battleVisualSource)) return;

            if (_battleVisualSource != null) _battleVisualSource.Visual -= OnBattleVisual;
            _battleVisualSource = battle;
            if (battle != null) battle.Visual += OnBattleVisual;
        }

        private void UnhookBattleVisual()
        {
            if (_battleVisualSource != null) _battleVisualSource.Visual -= OnBattleVisual;
            _battleVisualSource = null;
        }

        /// <summary>BattleSystem 在每次结算后抛出的事件入口（订阅见 <see cref="EnsureBattleVisualHook"/>）。</summary>
        private void OnBattleVisual(BattleFx fx, int value)
        {
            if (_battleStageRoot == null) return;

            // 同一特效 70ms 内只放一次：x4 下连击 + 多段技能会一帧抛好几次。
            // 吸血/狂暴/剧毒/护盾这类「每次命中都来一下」的 buff 效果再放宽一档，
            // 否则吸血鬼流会在主角身上糊一团永远消不掉的红光。
            float now = Time.unscaledTime;
            float minInterval = fx == BattleFx.Drain || fx == BattleFx.Berserk ||
                                fx == BattleFx.Poison || fx == BattleFx.Shield
                ? BuffFxMinInterval
                : FxMinInterval;
            if (_fxLastPlay.TryGetValue(fx, out float last) && now - last < minInterval) return;
            _fxLastPlay[fx] = now;

            float monTop = StageGroundY - _battleMonsterHeight;
            float monCy = monTop + _battleMonsterHeight * 0.52f;     // 躯干中部，不是脚底
            float heroCy = StageGroundY - StageHeroH * 0.55f;
            float jitter = Random.Range(-12f, 12f);

            switch (fx)
            {
                case BattleFx.PlayerHit:
                    _battleHeroLunge = 0f;
                    _battleMonsterShake = 1f;
                    PlayBattleFxSprite("fx_hit", StageMonsterCx + jitter, monCy, 110f, 0.26f, 1.45f, 0f);
                    break;

                case BattleFx.PlayerCrit:
                    _battleHeroLunge = 0f;
                    _battleMonsterShake = 1.25f;
                    PlayBattleFxSprite("fx_crit", StageMonsterCx, monCy, 148f, 0.34f, 1.45f, 0f);
                    break;

                case BattleFx.SkillHeavy:
                    _battleHeroLunge = 0f;
                    _battleMonsterShake = 1.2f;
                    PlayBattleFxSprite("fx_heavy", StageMonsterCx, monCy, 168f, 0.38f, 1.4f, -10f);
                    break;

                case BattleFx.SkillWhirlwind:
                    _battleMonsterShake = 0.8f;
                    PlayBattleFxSprite("fx_whirlwind", StageHeroCx + 40f, StageGroundY - 96f, 186f, 0.4f, 1.4f, 120f);
                    break;

                case BattleFx.SkillExecute:
                    _battleMonsterShake = 1.3f;
                    PlayBattleFxSprite("fx_execute", StageMonsterCx, StageGroundY - _battleMonsterHeight * 0.35f,
                        198f, 0.46f, 1.35f, 0f);
                    break;

                case BattleFx.SkillCounter:
                    PlayBattleFxSprite("fx_counter", StageHeroCx + 34f, heroCy, 118f, 0.3f, 1.45f, 0f);
                    break;

                case BattleFx.SkillOther:
                    _battleMonsterShake = 1f;
                    PlayBattleFxSprite("fx_heavy", StageMonsterCx + jitter, monCy, 132f, 0.3f, 1.42f, 0f);
                    break;

                case BattleFx.MonsterHit:
                    _battleHeroShake = 1f;
                    PlayBattleFxSprite("fx_hit", StageHeroCx + jitter, heroCy, 104f, 0.26f, 1.42f, 0f);
                    break;

                case BattleFx.MonsterCrit:
                    _battleHeroShake = 1.35f;
                    PlayBattleFxSprite("fx_crit", StageHeroCx, heroCy, 132f, 0.34f, 1.42f, 0f);
                    break;

                case BattleFx.Drain:
                    PlayBattleFxSprite("fx_drain", StageHeroCx - 18f, heroCy, 92f, 0.4f, 1.35f, 0f);
                    break;

                case BattleFx.Shield:
                    PlayBattleFxSprite("fx_shield", StageHeroCx, StageGroundY - 30f, 118f, 0.5f, 1.3f, 0f);
                    break;

                case BattleFx.Poison:
                    PlayBattleFxSprite("fx_poison", StageHeroCx + jitter, StageGroundY - 24f, 104f, 0.45f, 1.35f, 0f);
                    break;

                case BattleFx.Berserk:
                    PlayBattleFxSprite("fx_berserk", StageHeroCx, heroCy, 148f, 0.55f, 1.28f, 0f);
                    break;
            }
        }

        /// <summary>升级/转生这类非战斗事件：舞台自己比对等级变化就够了，不用再往战斗里塞事件。</summary>
        private void CheckStageLevelUp()
        {
            if (_game == null || _game.Player == null) return;

            int level = _game.Player.level;
            if (_battleLastLevel >= 0 && level > _battleLastLevel)
                PlayBattleFxSprite("fx_levelup", StageHeroCx, StageGroundY - 80f, 200f, 0.62f, 1.45f, 0f);
            _battleLastLevel = level;
        }

        private void PlayBattleFxSprite(string key, float cx, float cy, float size, float life, float grow, float spin)
        {
            if (_fxImages == null) return;

            Sprite sprite = SpriteOf("Sprites/Fx/" + key);
            if (sprite == null) return;

            int slot = -1;
            float oldest = -1f;
            for (int i = 0; i < _fxImages.Length; i++)
            {
                if (_fxImages[i] == null) continue;
                if (!_fxImages[i].enabled) { slot = i; break; }
                if (_fxT[i] > oldest) { oldest = _fxT[i]; slot = i; }   // 池满就顶掉最老的那张
            }
            if (slot < 0) return;

            Image img = _fxImages[slot];
            img.sprite = sprite;
            img.enabled = true;
            img.rectTransform.anchoredPosition = new Vector2(cx, -cy);
            img.rectTransform.sizeDelta = new Vector2(size, size);
            img.rectTransform.localRotation = Quaternion.identity;
            SetImageAlpha(img, 1f);

            _fxT[slot] = 0f;
            _fxLife[slot] = Mathf.Max(0.05f, life);
            _fxSize[slot] = size;
            _fxGrow[slot] = Mathf.Max(1.05f, grow);
            _fxSpin[slot] = spin;
        }

        // ==================== 美术查表 ====================

        /// <summary>
        /// 章节 → 有美术的族。第 4 章以后还没有专属美术（TODO），先按 3 族轮换，
        /// 至少做到「换章 = 换脸」，而不是 8 章都打同一只骷髅。
        /// </summary>
        private static string MonsterFamilyKey(int chapter)
        {
            int index = Mathf.Abs(chapter - 1) % 3;
            return index == 0 ? "goblin" : index == 1 ? "kobold" : "skeleton";
        }

        private static string MonsterTypeKey(MonsterType type)
        {
            return type == MonsterType.Melee ? "melee" : type == MonsterType.Ranged ? "ranged" : "crit";
        }

        private static float MonsterDisplayHeight(MonsterData monster)
        {
            if (monster == null) return StageMonsterH;
            switch (monster.tier)
            {
                case MonsterTier.Boss: return StageBossH;
                case MonsterTier.Elite: return StageEliteH;
                default: return StageMonsterH;
            }
        }

        private Sprite MonsterSpriteFor(MonsterData monster)
        {
            if (monster == null) return null;

            int chapter = GameConfig.ChapterOfMonsterLevel(monster.level);
            string family = MonsterFamilyKey(chapter);
            if (monster.tier == MonsterTier.Boss)
                return SpriteOf("Sprites/Bosses/" + family + "_boss");
            return SpriteOf("Sprites/Monsters/" + family + "_" + MonsterTypeKey(monster.type));
        }

        /// <summary>Resources 里的精灵缓存；找不到时只告警一次（每帧警告会刷爆控制台）。</summary>
        private Sprite SpriteOf(string path)
        {
            if (_spriteCache.TryGetValue(path, out Sprite cached)) return cached;

            Sprite loaded = Resources.Load<Sprite>(path);
            if (loaded == null) Debug.LogWarning($"[GameUI] 战斗舞台缺少精灵：Resources/{path}");
            _spriteCache[path] = loaded;
            return loaded;
        }

        private static void ApplyActorSprite(Image img, Sprite sprite, float height, float cx)
        {
            if (img == null) return;

            img.sprite = sprite;
            if (sprite == null)
            {
                img.enabled = false;
                return;
            }

            // 只给高度，宽度按原始宽高比推：同一族的三种兵种才能并排站得住。
            float width = height * (sprite.rect.width / Mathf.Max(1f, sprite.rect.height));
            RectTransform rt = img.rectTransform;
            rt.sizeDelta = new Vector2(width, height);
            rt.anchoredPosition = new Vector2(cx, -StageGroundY);
            rt.localRotation = Quaternion.identity;
            img.enabled = true;
            SetImageAlpha(img, 1f);
        }

        private static void SetImageAlpha(Image img, float alpha)
        {
            if (img == null) return;
            Color c = img.color;
            c.a = Mathf.Clamp01(alpha);
            img.color = c;
        }
    }
}

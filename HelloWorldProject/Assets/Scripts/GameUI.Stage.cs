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
        // ---- 地面光环：俯视看是扁椭圆，高度按宽度乘这个系数 ----
        private const float AuraSquash = 0.42f;
        private const float StageAuraEliteW = 176f;
        private const float StageAuraBossW = 208f;
        private const float StageAuraAffixW = 84f;
        private const int MaxDetailLogLines = 180;

        /// <summary>同一个特效两次触发的最小间隔（秒）：连击/多段技能同帧爆一堆会糊成白光。</summary>
        private const float FxMinInterval = 0.07f;
        private const float BuffFxMinInterval = 0.45f;

        // ---- 舞台控件 ----
        private Image _battleStageCard;
        private RectTransform _battleStageRoot;
        private Image _battleBackdropArt;      // 章节背景图（Resources/Sprites/Backgrounds）
        private string _battleBackdropKey;
        private Image _battleMonsterAura;      // 精英/Boss 档次光环
        private Image _battleAffixAura;        // 词缀光环（吸血 / 护盾 / 剧毒）
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
        private int _battleLastSkillMask = -1;
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

            // 章节背景图：铺满舞台、压暗一点，让角色仍然是最亮的主体
            _battleBackdropArt = CreateImage(_battleStageRoot, "StageBackdropArt", 0, 0, StageW, StageH, Color.white);
            Stretch(_battleBackdropArt.rectTransform);
            _battleBackdropArt.preserveAspect = false;
            _battleBackdropArt.enabled = false;

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

            // 光环先于角色创建：UGUI 按兄弟顺序叠，光环才会压在脚下而不是盖住身体
            _battleMonsterAura = CreateStageAura("MonsterAura", StageMonsterCx, StageAuraEliteW);
            _battleAffixAura = CreateStageAura("MonsterAffixAura", StageMonsterCx, StageAuraAffixW);

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

        /// <summary>地面光环：轴心在脚下、横向压扁成椭圆，只靠缩放/透明度做呼吸。</summary>
        private Image CreateStageAura(string name, float cx, float width)
        {
            Image aura = CreateImage(_battleStageRoot, name, 0, 0, width, width * AuraSquash, Color.white);
            RectTransform rt = aura.rectTransform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(cx, -(StageGroundY - width * AuraSquash * 0.32f));
            rt.sizeDelta = new Vector2(width, width * AuraSquash);
            aura.enabled = false;
            return aura;
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

    }
}

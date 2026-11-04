using UnityEngine;
using UnityEngine.UI;

namespace AutoBattle
{
    /// <summary>
    /// 营地行商（B1）。和战斗内 3 秒补给站完全分开：
    /// 这里是暂停无计时的局外商店，只卖操作便利与外观，不卖战斗数值。
    /// </summary>
    public partial class GameUI
    {
        private RectTransform _merchantPanel;
        private Text _merchantGoldText;
        private Button[] _merchantTabButtons;
        private Text[] _merchantTabLabels;
        private RectTransform[] _merchantPages;
        private int _merchantTab;
        private bool _merchantWasOpen;
        private int _merchantRevisionSeen = -1;
        private float _merchantRefreshTimer;
        private string _merchantConfirmSeen = string.Empty;

        // 服务页
        private Text[] _merchantSvcNames;
        private Text[] _merchantSvcPrices;
        private Text[] _merchantSvcDescs;
        private Button[] _merchantSvcButtons;
        private Text[] _merchantSvcBtnLabels;

        // 珍藏页
        private Text[] _merchantCosNames;
        private Text[] _merchantCosPrices;
        private Text[] _merchantCosDescs;
        private Button[] _merchantCosButtons;
        private Text[] _merchantCosBtnLabels;
        private Image[] _merchantCosSwatches;

        // 声望页
        private Text _merchantRankText;
        private Text _merchantRankTotalText;
        private Text _merchantRankNextText;
        private Image _merchantRankBar;
        private Button _merchantRankButton;
        private Text _merchantRankBtnLabel;
        private Text[] _merchantRewardTexts;
        private Text[] _merchantRewardMarks;

        // 战绩页
        private Text _merchantSummaryText;
        private Text _merchantStatsText;
        private Text _merchantCodexText;
        private Text _merchantRecordsNote;

        private static readonly int[] MerchantRewardRanks = { 1, 5, 10, 15, 20 };
        private static readonly string[] MerchantRewardLines =
        {
            "称号前缀「行商」+ 商店徽章",
            "头像框·青铜（自动授予）",
            "头像框·白银 + 背景色调·暖阳",
            "头像框·赤金 + 伤害数字·赤金",
            "称号「黄金旅人」+ 背景色调·极光",
        };

        private void OnMerchantButtonClicked()
        {
            bool open = _game.ShopPanelOpen;
            _game.ToggleShopPanel();
            if (open) GameAudio.PlayUiClose();
            else GameAudio.PlayUiOpen();
        }

        // ==================== 构建 ====================

        private void BuildShopPanel2(Transform root)
        {
            _merchantPanel = CreatePanel(root, "MerchantPanel", 220, 60, 840, 600, GameConfig.OverlayBg, true);
            RectTransform panel = _merchantPanel;

            CreateSectionTitle(panel, "营地行商", 24, 10, 320, FontTitle, "ui_shop");
            CreateText(panel, "MerchantSub", "只改外观与操作，不影响战斗数值。打开面板时战斗暂停。",
                24, 36, 540, 20, 12, GameConfig.TextMuted, TextAnchor.MiddleLeft);

            _merchantGoldText = CreateText(panel, "MerchantGold", "", 584, 24, 142, 22, 13,
                GameConfig.TextGold, TextAnchor.MiddleRight, FontStyle.Bold);

            Button close = CreateButton(panel, "MerchantClose", "关闭", 736, 12, 80, 30,
                GameConfig.BtnNeutral, out Text closeLabel);
            StyleBar(close, closeLabel, "frame_pill", "glyph_close", 14f, "关闭商店", 12);
            close.onClick.AddListener(() => _game.CloseShopPanel());

            string[] tabs = { "服务", "珍藏", "声望", "战绩" };
            _merchantTabButtons = new Button[tabs.Length];
            _merchantTabLabels = new Text[tabs.Length];
            for (int i = 0; i < tabs.Length; i++)
            {
                int tab = i;
                _merchantTabButtons[i] = CreateButton(panel, "MerchantTab" + i, tabs[i],
                    24 + i * 130, 60, 120, 30, GameConfig.BtnHeader, out Text label);
                StyleTab(_merchantTabButtons[i], label, "frame_tab", 12);
                _merchantTabLabels[i] = label;
                _merchantTabButtons[i].onClick.AddListener(() => SetMerchantTab(tab));
            }

            CreateImage(panel, "MerchantDivider", 24, 96, 792, 1, GameConfig.Border);

            _merchantPages = new RectTransform[4];
            for (int i = 0; i < 4; i++)
            {
                _merchantPages[i] = CreateRect(panel, "MerchantPage" + i, 0, 0, 840, 600);
                _merchantPages[i].gameObject.SetActive(false);
            }

            BuildMerchantServices(_merchantPages[0]);
            BuildMerchantCosmetics(_merchantPages[1]);
            BuildMerchantRank(_merchantPages[2]);
            BuildMerchantRecords(_merchantPages[3]);

            CreateText(panel, "MerchantFoot",
                "购买后立即写入当前存档；单价 100 万以上的条目需要连点两次确认。",
                24, 559, 560, 22, 12, GameConfig.TextMuted, TextAnchor.MiddleLeft);
            CreateText(panel, "MerchantVersion", "收藏家等级按存档槽独立", 600, 559, 216, 22, 12,
                GameConfig.TextMuted, TextAnchor.MiddleRight);

            SetMerchantTab(0);
            panel.gameObject.SetActive(false);
        }

        private void BuildMerchantServices(RectTransform page)
        {
            _merchantSvcNames = new Text[ShopCatalog.ServiceCount];
            _merchantSvcPrices = new Text[ShopCatalog.ServiceCount];
            _merchantSvcDescs = new Text[ShopCatalog.ServiceCount];
            _merchantSvcButtons = new Button[ShopCatalog.ServiceCount];
            _merchantSvcBtnLabels = new Text[ShopCatalog.ServiceCount];

            for (int i = 0; i < ShopCatalog.ServiceCount; i++)
            {
                int index = i;
                float x = 24f + (i % 2) * 404f;
                float y = 106f + (i / 2) * 126f;
                Image card = CreateCard(page, "MerchantSvc" + i, x, y, 388, 116,
                    GameConfig.CardBg, RadiusCard, GameConfig.BorderSoft);

                _merchantSvcNames[i] = CreateText(card.transform, "Name", ShopCatalog.ServiceNames[i],
                    14, 8, 240, 22, 15, GameConfig.TextTitle, TextAnchor.MiddleLeft, FontStyle.Bold);
                _merchantSvcPrices[i] = CreateText(card.transform, "Price", "",
                    246, 9, 128, 20, 13, GameConfig.TextGoldDeep, TextAnchor.MiddleRight, FontStyle.Bold);
                _merchantSvcDescs[i] = CreateText(card.transform, "Desc", ShopCatalog.ServiceDescs[i],
                    14, 32, 370, 46, 12, GameConfig.TextNormal, TextAnchor.UpperLeft);
                _merchantSvcDescs[i].lineSpacing = 1.05f;

                _merchantSvcButtons[i] = CreateButton(card.transform, "Buy", "购买",
                    272, 80, 102, 28, GameConfig.BtnNeutral, out Text label);
                StyleBar(_merchantSvcButtons[i], label, "frame_pill", "glyph_buy", 13f, "购买服务", 12);
                _merchantSvcBtnLabels[i] = label;
                _merchantSvcButtons[i].onClick.AddListener(() => OnMerchantServiceButton(index));
            }
        }

        private void BuildMerchantCosmetics(RectTransform page)
        {
            int n = ShopCatalog.CosmeticCount;
            _merchantCosNames = new Text[n];
            _merchantCosPrices = new Text[n];
            _merchantCosDescs = new Text[n];
            _merchantCosButtons = new Button[n];
            _merchantCosBtnLabels = new Text[n];
            _merchantCosSwatches = new Image[n];

            for (int i = 0; i < n; i++)
            {
                int index = i;
                float x = 24f + (i % 3) * 266f;
                float y = 106f + (i / 3) * 90f;
                Image card = CreateCard(page, "MerchantCos" + i, x, y, 258, 84,
                    GameConfig.CardBg, RadiusCard, GameConfig.BorderSoft);

                _merchantCosNames[i] = CreateText(card.transform, "Name", ShopCatalog.CosmeticNames[i],
                    10, 5, 134, 20, 13, GameConfig.TextTitle, TextAnchor.MiddleLeft, FontStyle.Bold);
                _merchantCosPrices[i] = CreateText(card.transform, "Price", "",
                    146, 5, 102, 20, 12, GameConfig.TextGoldDeep, TextAnchor.MiddleRight, FontStyle.Bold);
                _merchantCosDescs[i] = CreateText(card.transform, "Desc", ShopCatalog.CosmeticDescs[i],
                    10, 25, 238, 28, 11, GameConfig.TextNormal, TextAnchor.UpperLeft);
                _merchantCosDescs[i].lineSpacing = 1.02f;

                _merchantCosButtons[i] = CreateButton(card.transform, "Buy", "购买",
                    10, 53, 112, 24, GameConfig.BtnNeutral, out Text label);
                StyleBar(_merchantCosButtons[i], label, "frame_pill", "glyph_buy", 12f, "购买 / 装备", 11);
                _merchantCosBtnLabels[i] = label;
                _merchantCosButtons[i].onClick.AddListener(() => OnMerchantCosmeticClicked(index));

                _merchantCosSwatches[i] = CreateImage(card.transform, "Swatch", 216, 53, 26, 24,
                    ShopCatalog.CosmeticColor(i));
                Round(_merchantCosSwatches[i], 5);
            }
        }

        private void BuildMerchantRank(RectTransform page)
        {
            Image top = CreateCard(page, "MerchantRankCard", 24, 106, 792, 132,
                GameConfig.CardBg, RadiusCard, GameConfig.BorderSoft);

            _merchantRankText = CreateText(top.transform, "Rank", "收藏家 Lv.0", 20, 8, 320, 32, 22,
                GameConfig.TextGold, TextAnchor.MiddleLeft, FontStyle.Bold);
            _merchantRankTotalText = CreateText(top.transform, "Total", "", 20, 44, 520, 20, 12,
                GameConfig.TextMuted, TextAnchor.MiddleLeft);
            _merchantRankBar = CreateBar(top.transform, "RankBar", 20, 74, 500, 14,
                new Color(0.04f, 0.05f, 0.07f, 0.95f), GameConfig.TextGold);
            _merchantRankNextText = CreateText(top.transform, "Next", "", 20, 96, 560, 20, 12,
                GameConfig.TextNormal, TextAnchor.MiddleLeft);

            _merchantRankButton = CreateButton(top.transform, "RankBuy", "升级",
                620, 44, 148, 42, GameConfig.BtnGold, out Text rankLabel);
            StyleBar(_merchantRankButton, rankLabel, "frame_pill", "glyph_confirm", 16f, "提升收藏家等级", 13);
            _merchantRankBtnLabel = rankLabel;
            _merchantRankButton.onClick.AddListener(OnMerchantRankClicked);

            _merchantRewardTexts = new Text[MerchantRewardRanks.Length];
            _merchantRewardMarks = new Text[MerchantRewardRanks.Length];
            for (int i = 0; i < MerchantRewardRanks.Length; i++)
            {
                float y = 252f + i * 56f;
                Image row = CreateCard(page, "MerchantReward" + i, 24, y, 792, 48,
                    GameConfig.CardBgDim, RadiusCard, GameConfig.BorderSoft);
                CreateText(row.transform, "Lv", "Lv." + MerchantRewardRanks[i],
                    16, 13, 80, 22, 13, GameConfig.TextGold, TextAnchor.MiddleLeft, FontStyle.Bold);
                _merchantRewardTexts[i] = CreateText(row.transform, "Text", MerchantRewardLines[i],
                    100, 13, 590, 22, 12, GameConfig.TextNormal, TextAnchor.MiddleLeft);
                _merchantRewardMarks[i] = CreateText(row.transform, "Mark", "",
                    700, 13, 76, 22, 11, GameConfig.TextMuted, TextAnchor.MiddleRight);
            }
        }

        private void BuildMerchantRecords(RectTransform page)
        {
            Image summary = CreateCard(page, "MerchantSummaryCard", 24, 106, 792, 56,
                GameConfig.CardBg, RadiusCard, GameConfig.BorderSoft);
            _merchantSummaryText = CreateText(summary.transform, "Summary", "", 16, 7, 760, 42, 12,
                GameConfig.TextNormal, TextAnchor.MiddleLeft);
            _merchantSummaryText.lineSpacing = 1.15f;

            Image stats = CreateCard(page, "MerchantStatsCard", 24, 174, 384, 340,
                GameConfig.CardBg, RadiusCard, GameConfig.BorderSoft);
            CreateSectionTitle(stats.transform, "战斗统计", 16, 10, 320, FontSection, "ui_compare");
            _merchantStatsText = CreateText(stats.transform, "Stats", "", 16, 42, 352, 282, 12,
                GameConfig.TextNormal, TextAnchor.UpperLeft);
            _merchantStatsText.lineSpacing = 1.3f;

            Image codex = CreateCard(page, "MerchantCodexCard", 432, 174, 384, 340,
                GameConfig.CardBg, RadiusCard, GameConfig.BorderSoft);
            CreateSectionTitle(codex.transform, "装备图鉴", 16, 10, 320, FontSection, "ui_enhance");
            _merchantCodexText = CreateText(codex.transform, "Codex", "", 16, 42, 352, 282, 12,
                GameConfig.TextNormal, TextAnchor.UpperLeft);
            _merchantCodexText.lineSpacing = 1.3f;

            _merchantRecordsNote = CreateText(page, "Note", "", 24, 522, 792, 20, 11,
                GameConfig.TextMuted, TextAnchor.MiddleLeft);
        }

        // ==================== 刷新 ====================

        private void SetMerchantTab(int tab)
        {
            _merchantTab = Mathf.Clamp(tab, 0, _merchantPages.Length - 1);
            if (_merchantPages == null) return;
            for (int i = 0; i < _merchantPages.Length; i++)
                _merchantPages[i].gameObject.SetActive(i == _merchantTab);
            RefreshMerchantContent();
        }

        private void RefreshShop2()
        {
            if (_merchantPanel == null || _game == null) return;

            bool open = _game.ShopPanelOpen;
            if (_merchantPanel.gameObject.activeSelf != open) _merchantPanel.gameObject.SetActive(open);
            if (!open)
            {
                _merchantWasOpen = false;
                return;
            }

            bool justOpened = !_merchantWasOpen;
            _merchantWasOpen = true;
            if (justOpened)
            {
                _merchantRevisionSeen = -1;
                _merchantRefreshTimer = 0f;
                SetMerchantTab(0);
            }

            bool confirmActive = _game.ShopConfirmLeft > 0f;
            bool confirmChanged = _merchantConfirmSeen != _game.ShopConfirmKey;
            _merchantConfirmSeen = _game.ShopConfirmKey;
            _merchantRefreshTimer -= Time.unscaledDeltaTime;

            if (justOpened || confirmActive || confirmChanged ||
                _merchantRevisionSeen != _game.ShopRevision || _merchantRefreshTimer <= 0f)
            {
                _merchantRefreshTimer = 0.12f;
                _merchantRevisionSeen = _game.ShopRevision;
                RefreshMerchantContent();
            }
        }

        private void RefreshMerchantContent()
        {
            PlayerData p = _game != null ? _game.Player : null;
            if (p == null || _merchantGoldText == null) return;

            _merchantGoldText.text = "金币 " + p.gold.ToString("N0");

            for (int i = 0; i < _merchantTabButtons.Length; i++)
            {
                bool active = i == _merchantTab;
                _merchantTabButtons[i].image.color = active ? Color.white : GameConfig.FrameTintDim;
                _merchantTabLabels[i].color = active ? Color.white : GameConfig.BtnText;
            }

            if (_merchantPages != null)
            {
                for (int i = 0; i < _merchantPages.Length; i++)
                    _merchantPages[i].gameObject.SetActive(i == _merchantTab);
            }

            switch (_merchantTab)
            {
                case 0: RefreshMerchantServices(p); break;
                case 1: RefreshMerchantCosmetics(p); break;
                case 2: RefreshMerchantRank(p); break;
                default: RefreshMerchantRecords(p); break;
            }
        }

        private void RefreshMerchantServices(PlayerData p)
        {
            for (int i = 0; i < ShopCatalog.ServiceCount; i++)
            {
                bool owned = ShopCatalog.HasService(p, i);
                int price = ShopCatalog.ServicePrices[i];
                _merchantSvcNames[i].text = ShopCatalog.ServiceNames[i];
                _merchantSvcPrices[i].text = owned ? "已拥有" : ShopPriceText(price);
                _merchantSvcPrices[i].color = owned ? GameConfig.TextMuted : GameConfig.TextGold;
                _merchantSvcDescs[i].text = ShopCatalog.ServiceDescs[i];

                // 服务 3（自动出售规则）买下之后按钮变成开关：循环 关 → 普通 → 优秀 → 稀有。
                if (owned && i == ShopCatalog.AutoSellServiceIndex)
                {
                    Button toggle = _merchantSvcButtons[i];
                    Text toggleLabel = _merchantSvcBtnLabels[i];
                    toggle.interactable = true;
                    toggle.image.color = Color.white;
                    toggleLabel.text = AutoSellShort(p.autoSellQuality);
                    toggleLabel.color = p.autoSellQuality >= 0 ? GameConfig.TextGold : Color.white;
                    continue;
                }

                bool armed = _game.ShopConfirmLeft > 0f && _game.ShopConfirmKey == "svc:" + i;
                SetMerchantButton(_merchantSvcButtons[i], _merchantSvcBtnLabels[i],
                    owned, armed, price, p.gold, "已拥有", "购买");
            }
        }

        private void RefreshMerchantCosmetics(PlayerData p)
        {
            for (int i = 0; i < ShopCatalog.CosmeticCount; i++)
            {
                bool owned = ShopCatalog.HasCosmetic(p, i);
                ShopCatalog.CosmeticKind kind = ShopCatalog.KindOf(i);
                bool equipped = owned && ShopCatalog.EquippedCosmetic(p, kind) == i;
                int price = ShopCatalog.CosmeticPrices[i];

                _merchantCosNames[i].text = ShopCatalog.CosmeticNames[i];
                _merchantCosDescs[i].text = ShopCatalog.CosmeticDescs[i];
                _merchantCosSwatches[i].color = ShopCatalog.CosmeticColor(i);
                _merchantCosPrices[i].text = owned ? (equipped ? "已装备" : "已拥有") : ShopPriceText(price);
                _merchantCosPrices[i].color = owned ? GameConfig.TextMuted : GameConfig.TextGold;

                Button button = _merchantCosButtons[i];
                Text label = _merchantCosBtnLabels[i];
                if (owned)
                {
                    button.interactable = true;
                    button.image.color = Color.white;
                    label.text = equipped ? "卸下" : "装备";
                    label.color = equipped ? GameConfig.TextGold : Color.white;
                    continue;
                }

                bool armed = _game.ShopConfirmLeft > 0f && _game.ShopConfirmKey == "cos:" + i;
                SetMerchantButton(button, label, false, armed, price, p.gold, "", "购买");
            }
        }

        private void SetMerchantButton(Button button, Text label, bool owned, bool armed,
            int price, int gold, string ownedText, string buyText)
        {
            if (owned)
            {
                button.interactable = false;
                button.image.color = Color.white;
                label.text = ownedText;
                label.color = GameConfig.BtnDisabledText;
                return;
            }

            button.interactable = true;
            if (armed)
            {
                button.image.color = new Color(0.86f, 0.64f, 0.20f, 1f);
                label.text = $"确认 {_game.ShopConfirmLeft:0.0}s";
                label.color = Color.white;
                return;
            }

            button.image.color = Color.white;
            label.text = gold >= price ? buyText : "差 " + ShopPriceText(price - gold);
            label.color = gold >= price ? Color.white : GameConfig.TextGold;
        }

        private void RefreshMerchantRank(PlayerData p)
        {
            int rank = Mathf.Clamp(p.collectorRank, 0, ShopCatalog.MaxCollectorRank);
            bool max = rank >= ShopCatalog.MaxCollectorRank;
            int total = ShopCatalog.RankTotal(rank);
            int maxTotal = ShopCatalog.RankTotal(ShopCatalog.MaxCollectorRank);

            _merchantRankText.text = $"收藏家 Lv.{rank}";
            _merchantRankTotalText.text =
                $"累计投入 <b>{total:N0}</b> / {maxTotal:N0} 金币   ·   称号：{ShopCatalog.CollectorTitle(p, "无名旅人")}";

            float ratio = maxTotal > 0 ? Mathf.Clamp01((float)total / maxTotal) : 0f;
            _merchantRankBar.rectTransform.anchorMax = new Vector2(ratio, 1f);

            if (max)
            {
                _merchantRankNextText.text = "已满级：行商的最高等级称号「黄金旅人」已解锁。";
                _merchantRankButton.interactable = false;
                _merchantRankButton.image.color = Color.white;
                _merchantRankBtnLabel.text = "已满级";
                _merchantRankBtnLabel.color = GameConfig.BtnDisabledText;
            }
            else
            {
                int next = ShopCatalog.NextRankCost(p);
                _merchantRankNextText.text =
                    $"下一级需要 <b>{next:N0}</b> 金币   ·   当前 {p.gold:N0}   ·   还差 {Mathf.Max(0, next - p.gold):N0}";
                bool armed = _game.ShopConfirmLeft > 0f && _game.ShopConfirmKey == "rank";
                _merchantRankButton.interactable = true;
                if (armed)
                {
                    _merchantRankButton.image.color = new Color(0.86f, 0.64f, 0.20f, 1f);
                    _merchantRankBtnLabel.text = $"确认 {_game.ShopConfirmLeft:0.0}s";
                    _merchantRankBtnLabel.color = Color.white;
                }
                else
                {
                    _merchantRankButton.image.color = Color.white;
                    _merchantRankBtnLabel.text = "升级 " + ShopPriceText(next);
                    _merchantRankBtnLabel.color = p.gold >= next ? Color.white : GameConfig.TextGold;
                }
            }

            for (int i = 0; i < MerchantRewardRanks.Length; i++)
            {
                bool done = rank >= MerchantRewardRanks[i];
                _merchantRewardTexts[i].color = done ? GameConfig.TextTitle : GameConfig.TextMuted;
                _merchantRewardMarks[i].text = done ? "已达成" : "Lv." + MerchantRewardRanks[i];
                _merchantRewardMarks[i].color = done ? GameConfig.TextGold : GameConfig.TextMuted;
            }
        }

        private void RefreshMerchantRecords(PlayerData p)
        {
            var st = p.stats;
            _merchantSummaryText.text =
                $"击杀 <b>{_game.TotalKills}</b> · 阵亡 <b>{_game.TotalDeaths}</b> · 精英 <b>{_game.EliteKills}</b> · BOSS <b>{_game.BossKills}</b>   " +
                $"掉落 {_game.TotalDrops} · 穿戴 {_game.TotalEquipped} · 卖出 {_game.TotalSold}\n" +
                $"累计金币 {_game.TotalGoldEarned:N0}   ·   强化 {st.enhances} 次   ·   洗练 {st.reforges} 次   ·   传说掉落 {st.legendaryDrops}";

            bool hasStats = ShopCatalog.HasService(p, 5);
            if (hasStats)
            {
                _merchantStatsText.text =
                    $"击杀总数        {st.kills:N0}\n" +
                    $"阵亡次数        {st.deaths:N0}\n" +
                    $"精英击杀        {st.eliteKills:N0}\n" +
                    $"BOSS 击杀      {st.bossKills:N0}\n" +
                    $"掉落总数        {st.drops:N0}\n" +
                    $"穿戴次数        {st.equipped:N0}\n" +
                    $"卖出件数        {st.sold:N0}\n" +
                    $"累计金币        {st.goldEarned:N0}\n" +
                    $"强化次数        {st.enhances:N0}\n" +
                    $"洗练次数        {st.reforges:N0}（三锁 {st.threeLockReforges}）";
                _merchantStatsText.color = GameConfig.TextNormal;
            }
            else
            {
                _merchantStatsText.text =
                    "尚未解锁。\n\n购买服务「战斗统计」后，这里会显示击杀、阵亡、精英、BOSS、掉落、金币与强化/洗练的完整累计。\n\n" +
                    "现在仍可在左侧日志里看到本局的即时记录。";
                _merchantStatsText.color = GameConfig.TextMuted;
            }

            bool hasCodex = ShopCatalog.HasService(p, 4);
            if (hasCodex)
            {
                _merchantCodexText.text =
                    $"最高强化等级    +{st.bestEnhanceLevel}\n" +
                    $"最佳词条质量    {st.bestAffixQuality * 100f:0}%\n" +
                    $"传说掉落        {st.legendaryDrops:N0}\n" +
                    $"三锁洗练        {st.threeLockReforges:N0}\n\n" +
                    "说明：当前版本没有背包，替换下来的装备会立即卖出，所以这里记录的是历史最高值，" +
                    "不是当前装备的快照。";
                _merchantCodexText.color = GameConfig.TextNormal;
            }
            else
            {
                _merchantCodexText.text =
                    "尚未解锁。\n\n购买服务「装备图鉴」后，这里会记录最高强化、最佳词条、传说掉落与三锁洗练等历史。\n\n" +
                    "当前装备的详细数值仍可在强化 / 洗练 / 对比面板查看。";
                _merchantCodexText.color = GameConfig.TextMuted;
            }

            _merchantRecordsNote.text = hasStats && hasCodex
                ? "战绩记录随存档保存，转生时保留。"
                : "解锁「装备图鉴 / 战斗统计」后，这两张卡显示完整内容。";
        }

        // ==================== 点击 ====================

        private void OnMerchantServiceButton(int index)
        {
            if (ShopCatalog.HasService(_game.Player, index))
            {
                // 已拥有的服务按钮：自动出售规则循环阈值，其余服务不再响应。
                if (index == ShopCatalog.AutoSellServiceIndex)
                {
                    int current = _game.Player.autoSellQuality;
                    int next = current >= 2 ? -1 : current + 1;
                    _game.SetAutoSellQuality(next);
                    _merchantRevisionSeen = -1;
                    RefreshMerchantContent();
                }
                return;
            }

            ShopPurchaseResult result = _game.RequestPurchase("svc:" + index);
            AfterMerchantPurchase(result);
        }

        private static string AutoSellShort(int quality)
        {
            switch (quality)
            {
                case 0: return "收购·普通";
                case 1: return "收购·优秀";
                case 2: return "收购·稀有";
                default: return "收购·关";
            }
        }

        private void OnMerchantCosmeticClicked(int index)
        {
            PlayerData p = _game.Player;
            if (p == null) return;
            if (ShopCatalog.HasCosmetic(p, index))
            {
                _game.EquipCosmetic(index);
                _merchantRevisionSeen = -1;
                RefreshMerchantContent();
                return;
            }

            ShopPurchaseResult result = _game.RequestPurchase("cos:" + index);
            AfterMerchantPurchase(result);
        }

        private void OnMerchantRankClicked()
        {
            ShopPurchaseResult result = _game.RequestPurchase("rank");
            AfterMerchantPurchase(result);
        }

        private void AfterMerchantPurchase(ShopPurchaseResult result)
        {
            if (result == ShopPurchaseResult.Success)
            {
                _merchantRevisionSeen = -1;
                RefreshMerchantContent();
            }
            else if (result == ShopPurchaseResult.NeedConfirm)
            {
                RefreshMerchantContent();
            }
        }

        private static string ShopPriceText(int value)
        {
            if (value >= 100000000)
                return (value / 100000000f).ToString("0.##") + " 亿";
            if (value >= 10000)
                return (value / 10000f).ToString("0.#") + " 万";
            return value.ToString("N0");
        }
    }
}

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.Events;

namespace AutoBattle
{
    /// <summary>
    /// 全部界面（要求 15、16），用代码构建 UGUI，不依赖场景里预先摆好的物件。
    /// 只读取 <see cref="GameController"/> 的状态来刷新，不反向修改战斗逻辑。
    /// </summary>
    public partial class GameUI : MonoBehaviour
    {
        // 左侧面板构建：角色属性、装备卡与金币行
        // ==================== 左侧：属性 + 装备 ====================

        /// <summary>
        /// 主角立绘（`Assets/Resources/HeroPortrait.png`，803×1024 透明底全身像）。
        /// 它以前被当成半透明底纹铺在装备区、横穿文字，看着像贴纸；现在给一个正经的立绘框，
        /// 摆在左面板左上角，属性列挪到框右边（列坐标见 BuildLeftPanel 的 colX/colW）。
        /// 框做成 140×179 是贴着源图比例（0.782 vs 0.784）算的：preserveAspect 下既不留边也不裁切。
        /// 必须在属性文字之前建——文字那几行虽然起点在 x=168，但宽行（暴击/经验）是整幅宽的，
        /// 顺序反过来会被立绘盖住一截。
        /// </summary>
        private void BuildHeroPortrait(Transform panel)
        {
            Sprite hero = Resources.Load<Sprite>("HeroPortrait");
            if (hero == null)
            {
                Debug.LogWarning("[GameUI] 找不到 Resources/HeroPortrait.png，主角立绘跳过。");
                return;
            }

            const float x = 16f, y = 44f, w = 140f, h = 179f;

            // 先垫卡再放图：卡片给一层深色底和 1px 描边，图的透明背景才不会直接透到面板底色上，
            // 视觉上才是「装裱」而不是「贴上去」。
            Image portraitCard = CreateCard(panel, "PortraitCard", x, y, w, h, GameConfig.CardBg);
            Transform frameBorder = portraitCard.transform.Find("Border");
            if (frameBorder != null) _portraitFrameBorder = frameBorder.GetComponent<Image>();

            // 卡底和面板底几乎同色，不打光的话立绘整块「陷」进去，像一张暗色贴纸；
            // 垫一层冷色光晕，人才会从底上浮起来。光晕留在框内，不外溢到面板上。
            Image glow = CreateImage(panel, "PortraitGlow", x + 2f, y + 2f, w - 4f, h - 4f,
                new Color(0.30f, 0.50f, 0.95f, 0.22f));
            glow.sprite = GlowSprite();

            // 脚下一小滩「地灯」：人物是站在框里的，不给底部一点光会像悬空。
            Image floorLight = CreateImage(panel, "PortraitFloor", x + 12f, y + h - 52f, w - 24f, 54f,
                new Color(0.40f, 0.60f, 1f, 0.20f));
            floorLight.sprite = GlowSprite();

            Image img = CreateImage(panel, "HeroPortrait", x, y, w, h, Color.white);
            img.sprite = hero;
            img.preserveAspect = true;

            // 待机动画：呼吸/浮动/眨眼。立绘本体只有一张图 + 一张只闭眼的衍生帧，
            // 变换幅度在 GameUI.Portrait.cs 里统一控制，避免在构建代码里散落魔法数。
            SetupHeroPortrait(img, glow, floorLight);
        }

        private void BuildLeftPanel(Transform root)
        {
            RectTransform panel = CreatePanel(root, "LeftPanel", 12, 12, LeftPanelW, PanelH, GameConfig.PanelBg);
            _leftPanel = panel;

            // 立绘占左上角，属性改成「L 形」两块：窄列贴在立绘右边，宽的暴击/经验行等立绘结束再铺开。
            const float colX = 168f, colW = 216f;
            BuildHeroPortrait(panel);

            // 称号条压在立绘底边上（立绘 16,44,140,179，底边 223）：半透明暗底让文字
            // 在任何背景上都读得出来，又不用把立绘缩短。未佩戴时显示默认称谓。
            // 名牌：底色改成不透明深皮革 + 黄铜细边。原来是 0.58 的纯黑，压在立绘
            // 浅色裙摆上仍然透光，12 号金字读不出来。
            Image titlePlate = CreateImage(panel, "TitleStrip", 16, 194, 140, 28,
                new Color(0.118f, 0.090f, 0.067f, 0.94f));
            Round(titlePlate, RadiusBar);
            AddBorder(titlePlate, RadiusBar,
                new Color(GameConfig.Border.r, GameConfig.Border.g, GameConfig.Border.b, 0.55f));
            _titleText = CreateText(panel, "Title", "无名旅人", 16, 194, 140, 28, 13,
                GameConfig.BannerTitle, TextAnchor.MiddleCenter, FontStyle.Bold);
            AttachTooltip(_titleText.rectTransform,
                "称号：解锁带称号的成就后可在这里佩戴，只影响显示，不提供战斗属性。\n" +
                "在成就面板底部的「称号」一栏点选佩戴，再点一次即可卸下。");

            CreateSectionTitle(panel, "角色属性", 16, 10, 260, FontTitle, "ui_health");

            // 装备区与金币区各垫一张卡：左栏文字多，没有底衬就是一整片同色的文字墙。
            // 装备卡：留白改成左右各 8px（20..400），两列 186 宽、列间 8px。
            // 原来是 190+198 的排法，第二列右边缘 408 正好压在卡片描边上。
            // 装备卡从 196 收到 180（2026-10-28）：卡本身就是 3 行小卡，196 里空着 14px 纯留白，
            // 而左侧三枚入口钮当时只能挤在 296 一线、底边越过卡顶 4px。卡顶下移到 334 后，
            // 三钮（296~326）与卡顶之间终于有 8px 空隙，这一列也就不挤了。
            _equipCard = CreateCard(panel, "EquipCard", 12, 334, 396, 168, GameConfig.CardBg);
            // 金币卡同步收到 74：卡内是「金币行 24 + 两行统计 26 + 资源条 18」，
            // 原来 92 高的下沿（渲染 626）被第一行技能槽（618 起）压住，是用户说的「技能按钮压线」。
            CreateCard(panel, "WalletCard", 12, 510, 396, 84, GameConfig.CardBg);

            // 五个 40×40 图标入口共用 6px 间距，整组右对齐到 x=400。
            // 顶栏纵向中心仍是 y=34；存档槽与成就只保留下角数字徽章。
            Button saveBtn = CreateButton(panel, "SaveButton", "1", 360, 14, 40, 40,
                GameConfig.BtnHeader, out _saveButtonLabel);
            StyleIcon(saveBtn, _saveButtonLabel, "frame_oct", "glyph_save", 24f, "存档槽", true);
            saveBtn.onClick.AddListener(OnSaveButtonClicked);

            _settingsButton = CreateButton(panel, "SettingsButton", "", 314, 14, 40, 40,
                GameConfig.BtnHeader, out _settingsButtonLabel);
            StyleIcon(_settingsButton, _settingsButtonLabel, "frame_oct", "glyph_settings", 24f, "设置");
            _settingsButton.onClick.AddListener(OnSettingsClicked);

            _achievementButton = CreateButton(panel, "AchievementButton", "", 268, 14, 40, 40,
                GameConfig.BtnHeader, out _achievementButtonLabel);
            StyleIcon(_achievementButton, _achievementButtonLabel, "frame_oct", "glyph_achievement", 24f, "成就", true);
            _achievementButton.onClick.AddListener(OnAchievementButtonClicked);

            // 音乐入口排在成就左边；按钮带与角色属性标题之间保留明确空隙。
            _musicButton = CreateButton(panel, "MusicButton", "", 222, 14, 40, 40,
                GameConfig.BtnHeader, out _musicButtonLabel);
            StyleIcon(_musicButton, _musicButtonLabel, "frame_oct", "glyph_music", 24f, "背景音乐");
            _musicButton.onClick.AddListener(OnMusicButtonClicked);

            // 营地行商（B1）排在按钮带最左侧。
            // 打开即暂停，所以不需要额外的「战斗中不可点」判断。
            Button shopButton = CreateButton(panel, "MerchantButton", "", 176, 14, 40, 40,
                GameConfig.BtnHeader, out Text shopLabel);
            StyleIcon(shopButton, shopLabel, "frame_oct", "glyph_shop", 24f, "营地行商");
            shopButton.onClick.AddListener(OnMerchantButtonClicked);

            // 等级行：数字 22 号（原来 24 号塞在 26 高的框里，字形基线扎到关卡按钮上）。
            _levelText = CreateText(panel, "Level", "", colX, 44, colW, 30, 17, GameConfig.TextTitle, TextAnchor.MiddleLeft, FontStyle.Bold);

            // 选关入口：立绘右侧那列的等级行下面。4b 时把关卡信息从等级行里拿出来做成按钮，
            // 顺便让等级行不至于被「关卡 11-10 · 怪 Lv.110」撑爆。
            // 页签从 24 加到 28 高：24 高时 12 号字的字底会贴住下缘金边（看起来像出界）。
            // 78 → 76 起，底边 104，与右侧无尽徽章（74~106）几乎同高，也不碰 108 的 HP 行。
            _stageButton = CreateButton(panel, "StageButton", "", colX, 76, 150, 28,
                GameConfig.BtnHeader, out _stageButtonLabel);
            // 28 高的页签上放 13px 图标还是会被厚金边切掉半把剑，干脆只留文字居中。
            StyleTab(_stageButton, _stageButtonLabel, "frame_tab", 12);
            AttachTooltip(_stageButton, "选择关卡");
            _stageButton.onClick.AddListener(OnStageButtonClicked);

            // 无尽入口：32×32 的无限徽章，放在关卡按钮右侧原来的位置上。
            // 未解锁时整枚染灰；局内挑战时染金，用颜色代替「无尽 / 记录」文字。
            _endlessButton = CreateButton(panel, "EndlessButton", "", 336, 74, 32, 32,
                GameConfig.BtnHeader, out _endlessButtonLabel);
            _endlessButtonIcon = StyleIcon(_endlessButton, _endlessButtonLabel, "frame_hex", "glyph_infinity", 20f, "无尽模式");
            _endlessButton.onClick.AddListener(OnEndlessButtonClicked);

            _hpText = CreateText(panel, "HpText", "", colX, 108, colW, 20, 15, GameConfig.TextNormal, TextAnchor.MiddleLeft);
            _hpFill = CreateBar(panel, "HpBar", colX, 130, colW, 16, GameConfig.BarTrackHp, GameConfig.FillHp);

            _atkText = CreateText(panel, "Atk", "", colX, 150, colW, 22, FontSmall + 1, GameConfig.TextAttack, TextAnchor.MiddleLeft);
            _defText = CreateText(panel, "Def", "", colX, 176, colW, 22, FontSmall + 1, GameConfig.TextDefense, TextAnchor.MiddleLeft);
            _spdText = CreateText(panel, "Spd", "", colX, 202, colW, 22, FontSmall + 1, GameConfig.TextSpeed, TextAnchor.MiddleLeft);

            _critText = CreateText(panel, "Crit", "", 16, 228, 388, 24, 15, GameConfig.TextCrit, TextAnchor.MiddleLeft);
            AttachTooltip(_critText.rectTransform,
                "暴击率：每次普攻按该概率打出暴击，暴击伤害 = 普通伤害 × 暴伤。\n" +
                "暴伤：暴击时的伤害倍率（150% 即 1.5 倍）。\n" +
                "穿透：无视目标该比例的护甲，对高防怪收益更大。\n" +
                "三个数值都来自装备主/副词条与技能，互不改写。");

            _expText = CreateText(panel, "ExpText", "", 16, 254, 388, 20, 15, GameConfig.TextExp, TextAnchor.MiddleLeft);
            AttachTooltip(_expText.rectTransform,
                "经验：击杀怪物获得，攒满升一级，上限 Lv.100。\n" +
                "满级后经验转为巅峰等级：每级 +" + GameConfig.ParagonAttackPerLevel + " 攻击 / +" + GameConfig.ParagonDefensePerLevel +
                " 防御 / +" + GameConfig.ParagonHpPerLevel + " 生命，每 " + GameConfig.ParagonPassiveEvery +
                " 级给 1 个技能点（P10 起共 9 点，可在技能面板自选本流派技能），上限 " + GameConfig.MaxParagonLevel + " 级。\n" +
                "经验与巅峰等级只影响角色成长，不影响掉率。");
            _expFill = CreateBar(panel, "ExpBar", 16, 276, 388, 14, GameConfig.BarTrackExp, GameConfig.FillExp);

            // 分隔线整条删掉（2026-10-28）：它渲染在 306~308，而三枚入口钮从 308 起——
            // 中间 0 间隙，看起来就是「按钮压住上面那条线」。经验条本身已经把两段隔开，
            // 少一条线比来回挪 2px 更干净。

            CreateSectionTitle(panel, "装备", 16, 297, 244, FontSection, "ui_defense");

            // 强化 / 洗练入口。放在装备标题行右侧：标题「装备」只占约 40px，右边整片是空的。
            // 装备区本身已经排满（武器 334~420 / 衣服 424~510），塞不下每件一个按钮，
            // 所以两个操作都走浮层：点这里开面板，在面板里挑武器还是衣服。
            // 两个按钮各 76 宽并排（240 / 322），合起来正好落在原「强化」那 92px 占位的右侧，
            // 与顶部「存档」按钮右对齐（398 ≈ 388 + 10）。
            // 装备操作改成三枚 34×34 图标按钮（对比 / 强化 / 洗练），不再带文字。
            // 右对齐到 x=400（装备卡右边缘 408 内缩 8px），底边停在 y=328 的卡顶上方，
            // 不再压住分隔线或戳出装备区。三枚之间留 7px，避免金边互相贴住。
            // 对比没有待选时正常色，有待选时整枚染色（见 GameUI.Compare.cs）。
            // 尺寸从 34 收到 30、y 从 296 提到 290（底边 320）：原来的 34@296 底边到 330，
            // 越过装备卡顶边 322，视觉上就是「装备文字右边的三个按钮压住了下边的线」。
            // x 也从 272 右移到 278，让分隔线（16~266）与标题（16~260）都不再被骑住。
            _compareButton = CreateButton(panel, "CompareEntry", "", 278, 296, 30, 30,
                GameConfig.BtnHeader, out _compareButtonLabel);
            _compareButtonIcon = StyleIcon(_compareButton, _compareButtonLabel, "frame_square_small", "glyph_scales", 20f, "装备对比");
            _compareButton.onClick.AddListener(OnCompareEntryClicked);

            Button enhanceEntry = CreateButton(panel, "EnhanceEntry", "", 316, 296, 30, 30,
                GameConfig.BtnGold, out Text enhanceLabel);
            StyleIcon(enhanceEntry, enhanceLabel, "frame_square_small", "glyph_anvil", 20f, "强化装备");
            enhanceEntry.onClick.AddListener(OnEnhanceEntryClicked);

            Button reforgeEntry = CreateButton(panel, "ReforgeEntry", "", 354, 296, 30, 30,
                GameConfig.BtnHeader, out Text reforgeLabel);
            StyleIcon(reforgeEntry, reforgeLabel, "frame_square_small", "glyph_flame", 20f, "洗练装备");
            reforgeEntry.onClick.AddListener(OnReforgeEntryClicked);

            // 五槽改成 2 列 × 3 行紧凑卡（190×58）。两张大卡（396 宽）放不下头盔/鞋/饰品，
            // 而且大卡里三行属性会把 192 高的装备区撑爆。卡面只留「标题 / 主属性 / 倍率+评分」，
            // 完整词条与逐项差走对比面板（点卡即开）。
            _equipCards = new Button[EquipmentSlots.Count];
            _equipIcons = new Image[EquipmentSlots.Count];
            _equipTitles = new Text[EquipmentSlots.Count];
            _equipStats = new Text[EquipmentSlots.Count];
            _equipMetas = new Text[EquipmentSlots.Count];
            for (int i = 0; i < EquipmentSlots.Count; i++)
            {
                EquipmentSlot slot = EquipmentSlots.All[i];
                float cardX = 20f + (i % 2) * 194f;
                // 单张栏位卡 58 → 48（2026-10-28 用户要求压缩栏位高度）：标题 16 + 两行属性 25 足够，
                // 58 里下面空着 12px。五行卡跟着从 196 缩到 168，左栏一整列就松下来了。
                float cardY = 342f + (i / 2) * 52f;
                Button card = CreateButton(panel, "EquipCard" + slot, "", cardX, cardY, 186, 48,
                    GameConfig.CardBg, out Text hiddenLabel);
                hiddenLabel.text = string.Empty;
                int captured = i;
                card.onClick.AddListener(delegate { OnEquipCardClicked(captured); });
                _equipCards[i] = card;

                _equipIcons[i] = CreateIcon(card.transform, SlotIconKey(slot), 7, 4, 16);
                _equipTitles[i] = CreateText(card.transform, "Title", "", 28, 3, 84, 18, 12,
                    GameConfig.TextTitle, TextAnchor.MiddleLeft, FontStyle.Bold);
                // 标题现在带「+N」强化等级（+N 用 <size=9> 缩小）。90px 仍然紧张，
                // 所以关掉换行：宁可横向溢出一点，也不能折行把下面的属性行顶下去（Unity 的 bestFit 只缩字不挡折行）。
                _equipTitles[i].horizontalOverflow = HorizontalWrapMode.Overflow;
                _equipTitles[i].resizeTextForBestFit = true;
                _equipTitles[i].resizeTextMinSize = 9;
                _equipTitles[i].resizeTextMaxSize = 12;
                _equipMetas[i] = CreateText(card.transform, "Meta", "", 112, 3, 70, 18, 11,
                    GameConfig.TextMuted, TextAnchor.MiddleRight);
                _equipMetas[i].horizontalOverflow = HorizontalWrapMode.Wrap;
                _equipMetas[i].verticalOverflow = VerticalWrapMode.Truncate;
                _equipMetas[i].resizeTextForBestFit = true;
                _equipMetas[i].resizeTextMinSize = 9;
                _equipMetas[i].resizeTextMaxSize = 11;
                _equipStats[i] = CreateText(card.transform, "Stats", "", 8, 21, 170, 22, 11,
                    GameConfig.TextNormal, TextAnchor.MiddleLeft);
                _equipStats[i].lineSpacing = 0.9f;
                // 常规单行用 11 号字；长词条只在当前卡内缩字，绝不越界盖住下一张卡。
                // 完整词条仍可在装备对比面板查看。
                _equipStats[i].verticalOverflow = VerticalWrapMode.Truncate;
                _equipStats[i].resizeTextForBestFit = true;
                _equipStats[i].resizeTextMinSize = 9;
                _equipStats[i].resizeTextMaxSize = 11;
            }

            // 金币卡：原来只有 80 高，金币行顶着卡顶、资源条压在卡的下边框上。
            // 现在金币行 / 两行统计 / 资源条都排在卡内，Divider2 删掉（两卡之间本来就有边）。
            CreateIcon(panel, "ui_gold", 24, 513, 18);
            _goldText = CreateText(panel, "Gold", "", 46, 506, 354, 28, 19, GameConfig.TextGold, TextAnchor.MiddleLeft); // 金额保留正文数字字形，强调交给富文本。

            // 金币、两行统计和流派资源条依次排列，各自保留完整文字空间。
            _statText = CreateText(panel, "Stats", "", 22, 538, 378, 28, 11, GameConfig.TextNormal, TextAnchor.UpperLeft);
            _statText.lineSpacing = 0.8f;
            _statText.verticalOverflow = VerticalWrapMode.Overflow;

            // 技能 2.0 的流派资源条（雷印 / 岩层）插在统计与技能栏之间。
            BuildResourceBar(panel);

            // 引导高亮用的整块技能栏矩形：先建它、再建 6 个槽，槽位就压在它上面，
            // 高亮时框住整块、又不会挡住槽位自己的点击（它本身不接收 raycast，只是个定位锚）。
            // 引导高亮框跟着技能栏一起上移收高（原来 618+90 正好顶到面板底边 708）。
            _skillBarRoot = CreateRect(panel, "SkillBarRoot", 16, 604, 388, 82);
            BuildSkillBar(panel);
        }

        /// <summary>
        /// 技能栏：6 个槽位（主动 2 / 被动 2 / 触发 2），横向铺满左面板宽度。
        /// 槽位内自带「主动 / 被动 / 触发」小标签，所以不再单独放标题。
        /// </summary>
        private void BuildSkillBar(Transform panel)
        {
            const int slotCount = PlayerData.SkillSlotCount;
            int perKind = PlayerData.SkillSlotsPerKind;        // 2
            int kindCount = slotCount / perKind;               // 3
            // 三列各 124 宽，列缝 8；两行各 38 高，行缝 6。
            // 整体 y=604~686，与钱包卡和面板底边分别留 10px。
            const float slotW = 124f;
            const float slotH = 38f;
            const float stepX = 132f;
            const float rowY = 604f;
            const float rowStep = 44f;

            // 三列（主动 / 被动 / 触发）× 两行。类别标签放进格内左上角，
            // 不再单独占一行列头——那一行正好会压在统计文字上。

            _skillSlotButtons = new Button[slotCount];
            _skillSlotLabels = new Text[slotCount];
            _skillSlotStates = new Text[slotCount];
            _skillSlotFills = new Image[slotCount];
            _skillSlotAccents = new Image[slotCount];
            _skillSlotIcons = new Image[slotCount];

            for (int i = 0; i < slotCount; i++)
            {
                int captured = i;
                int kind = i / perKind;      // 列：0 主动 / 1 被动 / 2 触发
                int row = i % perKind;       // 行
                float x = 16f + kind * stepX;
                float y = rowY + row * rowStep;

                _skillSlotButtons[i] = CreateButton(panel, $"SkillSlot{i}", "", x, y, slotW, slotH,
                    GameConfig.SlotBg, out _skillSlotLabels[i]);
                // 六个槽共用一条说明：推荐分是折算分，不是战斗数值。
                AttachTooltip(_skillSlotButtons[i],
                    "技能卡右上角的 ★ 是推荐分：把技能折算成「相对你当前面板的 DPS 增益」与「有效生命增益」，合成 35~99 分。\n" +
                    "80+ 核心 / 66+ 好用 / 52+ 一般 / 其余凑数，分数随攻击、暴击、防御变化。\n" +
                    "推荐分只用于排序提示，不参与战斗计算；设置里可以关掉。");

                // 左侧强调条：有技能时显示技能品质色，空槽时几乎看不见。
                // 它比「整块染成技能色」克制得多——原来 6 个槽一起变色像调色板。
                _skillSlotAccents[i] = CreateImage(_skillSlotButtons[i].transform, $"SkillAccent{i}",
                    0f, 3f, 3f, slotH - 6f, GameConfig.Border);

                // 技能图标改成 34×34 的主视觉，占满左侧；类别与状态文字让到右边。
                _skillSlotIcons[i] = CreateIconSlot(_skillSlotButtons[i].transform, $"SkillIcon{i}", 5f, 3f, 34f);

                // 类别标签（图标右侧，9 号灰字）：同类的两个槽上下相邻，扫一眼就知道这列是什么。
                CreateText(_skillSlotButtons[i].transform, $"SkillKind{i}", KindShort(kind),
                    42f, 1f, 44f, 14f, 10, GameConfig.SlotKindText, TextAnchor.MiddleLeft);

                // 技能名（图标右侧主行）。
                _skillSlotLabels[i].fontSize = 13;
                _skillSlotLabels[i].alignment = TextAnchor.MiddleCenter;
                _skillSlotLabels[i].rectTransform.offsetMin = new Vector2(42f, 13f);
                _skillSlotLabels[i].rectTransform.offsetMax = new Vector2(-4f, -5f);

                // 冷却秒数 / 就绪（右上角）。
                _skillSlotStates[i] = CreateText(_skillSlotButtons[i].transform, $"SkillState{i}", "",
                    82f, 1f, slotW - 86f, 14f, 10, GameConfig.SlotCooldownText, TextAnchor.MiddleRight);

                // 底部冷却进度条。
                Image barBg = CreateImage(_skillSlotButtons[i].transform, $"SkillCdBar{i}",
                    5f, slotH - 4f, slotW - 10f, 3f, GameConfig.ShadeOverlay);
                _skillSlotFills[i] = CreateImage(barBg.rectTransform, "Fill", 0, 0, 0, 3,
                    GameConfig.AccentSlotReady);

                RectTransform fill = _skillSlotFills[i].rectTransform;
                fill.anchorMin = new Vector2(0f, 0.5f);
                fill.anchorMax = new Vector2(0f, 0.5f);
                fill.pivot = new Vector2(0f, 0.5f);
                fill.anchoredPosition = Vector2.zero;
                fill.sizeDelta = new Vector2(0f, 3f);

                _skillSlotButtons[i].onClick.AddListener(() => OnSkillSlotClicked(captured));
            }
        }

        /// <summary>槽位类别简写。</summary>
        private static string KindShort(int kindIndex)
        {
            switch (kindIndex)
            {
                case 0: return "主动";
                case 1: return "被动";
                default: return "触发";
            }
        }

    }
}

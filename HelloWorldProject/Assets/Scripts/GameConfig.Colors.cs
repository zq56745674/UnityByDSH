using UnityEngine;

namespace AutoBattle
{
    /// <summary>
    /// 全局配色常量表。所有 UI 与日志颜色都应引用这里，不再在调用点写
    /// <c>new Color(...)</c> 字面量。
    ///
    /// 主题（2026-10-26 起）：「桌面实体卡牌」——深色木桌 + 米白羊皮纸 + 墨色文字 +
    /// 烫金细边。三条铁律：
    ///  1. 容器一律是纸（亮），文字一律是墨（暗），对比度靠明暗而不是靠颜色数。
    ///  2. 按钮是压在纸上的皮片 / 木片（暗底 + 浅字），这样 CreateButton 的白色标签才立得住。
    ///  3. 语义色（金币金、伤害红、经验紫）保留色相，但整体压深到能在纸上读。
    /// 只出现在深色战斗舞台上的颜色（飘字、怪物名牌）保持高亮，不要跟着压深。
    /// </summary>
    public static partial class GameConfig
    {
        // ==================== 容器与底板 ====================

        /// <summary>左右主面板底色（羊皮纸）。</summary>
        public static readonly Color PanelBg = new Color(0.929f, 0.886f, 0.792f);

        /// <summary>浮层面板底色（选关 / 技能 / 存档 / 强化），比主面板略深一档。</summary>
        public static readonly Color OverlayBg = new Color(0.886f, 0.835f, 0.729f);

        /// <summary>面板内更暗的一层（商店区、buff 栏底）。</summary>
        public static readonly Color InsetBg = new Color(0.847f, 0.788f, 0.671f);

        /// <summary>卡片底色。</summary>
        public static readonly Color CardBg = new Color(0.945f, 0.910f, 0.827f);

        /// <summary>卡片底色（不可用 / 未解锁）。</summary>
        public static readonly Color CardBgDim = new Color(0.855f, 0.816f, 0.735f);

        /// <summary>卡片底色（已选中 / 当前槽）。</summary>
        public static readonly Color CardBgActive = new Color(0.902f, 0.847f, 0.678f);

        /// <summary>列表行 / 槽位底色。</summary>
        public static readonly Color RowBg = new Color(0.898f, 0.855f, 0.761f);

        /// <summary>列表行底色（次级、未选中）。</summary>
        public static readonly Color RowBgDim = new Color(0.863f, 0.820f, 0.729f);

        /// <summary>音量条未填充格：比 RowBg 暗一档，否则 80% 只能数出 6~8 格。</summary>
        public static readonly Color VolumeCellOff = new Color(0.741f, 0.686f, 0.580f);

        /// <summary>音量条格子的细描边，让相邻空格之间也有边界。</summary>
        public static readonly Color VolumeCellBorder = new Color(0.35f, 0.27f, 0.18f, 0.35f);

        /// <summary>生命条底槽。</summary>
        public static readonly Color BarTrackHp = new Color(0.545f, 0.454f, 0.365f);

        /// <summary>经验条底槽。</summary>
        public static readonly Color BarTrackExp = new Color(0.404f, 0.337f, 0.255f);

        /// <summary>1 像素分隔线。</summary>
        public static readonly Color Divider = new Color(0.32f, 0.24f, 0.16f, 0.28f);

        /// <summary>全屏遮罩（横幅背后）。</summary>
        public static readonly Color ScreenDim = new Color(0.080f, 0.065f, 0.050f, 0.86f);

        /// <summary>只有窗口底色那么亮的全屏背景（木桌贴图没加载时的兜底）。</summary>
        public static readonly Color WindowBg = new Color(0.120f, 0.090f, 0.070f);

        /// <summary>技能槽 / 插槽底（深皮革凹槽，压在羊皮纸上像真的嵌进去）。</summary>
        public static readonly Color SlotBg = new Color(0.176f, 0.137f, 0.106f);

        /// <summary>纸面上的警示橙（比 LogWarn 深一档，压在羊皮纸上才读得清）。</summary>
        public static readonly Color TextWarningDeep = new Color(0.435f, 0.259f, 0.078f);

        /// <summary>纸面上的信息蓝（GoalMain 用，比 TextInfo 深一档）。</summary>
        public static readonly Color TextInfoDeep = new Color(0.235f, 0.290f, 0.360f);

        /// <summary>空资源格的凹槽底（比纸面深一档，读出「槽还在、只是没填」）。</summary>
        public static readonly Color SlotTrough = new Color(0.796f, 0.741f, 0.635f);

        /// <summary>槽位类别小标签（深槽上的浅灰褐）。</summary>
        public static readonly Color SlotKindText = new Color(0.784f, 0.714f, 0.627f);

        /// <summary>空槽的「＋」占位。</summary>
        public static readonly Color SlotEmptyText = new Color(0.573f, 0.522f, 0.451f);

        /// <summary>深槽上的冷却中状态（亮蓝）。</summary>
        public static readonly Color SlotCooldownText = new Color(0.616f, 0.729f, 0.953f);

        /// <summary>深槽上的就绪状态（亮绿）。</summary>
        public static readonly Color SlotReadyText = new Color(0.553f, 0.867f, 0.612f);

        /// <summary>怪物名牌底板（深皮革）。</summary>
        public static readonly Color NamePlateBg = new Color(0.161f, 0.122f, 0.094f);

        /// <summary>技能槽左下角的冷却压暗层。</summary>
        public static readonly Color ShadeOverlay = new Color(0f, 0f, 0f, 0.42f);

        /// <summary>半透明白（图标底衬）。</summary>
        public static readonly Color ImageHalf = new Color(1f, 1f, 1f, 0.50f);

        /// <summary>完全透明（占位、擦除边框）。</summary>
        public static readonly Color Clear = new Color(0f, 0f, 0f, 0f);

        // ==================== 文字 ====================

        /// <summary>面板标题（墨色，略带暖调）。</summary>
        public static readonly Color TextTitle = new Color(0.157f, 0.118f, 0.086f);

        /// <summary>正文。</summary>
        public static readonly Color TextNormal = new Color(0.227f, 0.180f, 0.137f);

        /// <summary>次要说明、提示行。</summary>
        public static readonly Color TextMuted = new Color(0.396f, 0.333f, 0.255f);

        /// <summary>禁用 / 未解锁的文字。</summary>
        public static readonly Color TextDisabled = new Color(0.576f, 0.522f, 0.443f);

        /// <summary>金币等高价值数字。</summary>
        public static readonly Color TextGold = new Color(0.643f, 0.435f, 0.102f);
    /// <summary>压深一档的金色：16px 以下或压在 InsetBg 深纸上的金币数字用它，否则对比度只有 2.5。</summary>
    public static readonly Color TextGoldDeep = new Color(0.478f, 0.318f, 0.055f);

        /// <summary>冷色信息文字（蓝墨）。</summary>
        public static readonly Color TextInfo = new Color(0.243f, 0.412f, 0.549f);

        /// <summary>攻击力一行。</summary>
        public static readonly Color TextAttack = new Color(0.612f, 0.255f, 0.125f);

        /// <summary>防御力一行。</summary>
        public static readonly Color TextDefense = new Color(0.180f, 0.353f, 0.502f);

        /// <summary>速度一行。</summary>
        public static readonly Color TextSpeed = new Color(0.220f, 0.427f, 0.243f);

        /// <summary>暴击一行。</summary>
        public static readonly Color TextCrit = new Color(0.588f, 0.208f, 0.361f);

        /// <summary>经验一行。</summary>
        public static readonly Color TextExp = new Color(0.353f, 0.255f, 0.110f);

        /// <summary>
        /// 把任意亮色压成「纸面墨色」：保留色相与饱和度，只把明度压到 0.42 以下。
        /// 品质色 / 流派色 / 技能色这些语义色本来是为深底准备的亮色，
        /// 直接画在羊皮纸上对比度只有 1.0~2.5（等于看不见），一律先过这个函数。
        /// </summary>
        public static Color InkOf(Color c)
        {
            float h, s, v;
            Color.RGBToHSV(c, out h, out s, out v);
            v = Mathf.Min(v, 0.42f);
            s = Mathf.Clamp(s, 0.12f, 0.85f);
            return Color.HSVToRGB(h, s, v);
        }

        /// <summary>同上，直接给富文本用的 "#RRGGBB"。</summary>
        public static string InkHex(Color c) => ColorUtility.ToHtmlStringRGB(InkOf(c));

        // ==================== 悬停提示 ====================

        /// <summary>提示框底：深墨棕（不是纯黑，和木桌/皮革一个色系）。</summary>
        public static readonly Color TooltipBg = new Color(0.129f, 0.098f, 0.071f, 0.97f);

        /// <summary>提示框文字：羊皮纸白。原来用的是 TextTitle（深墨），压在深色底上几乎看不清。</summary>
        public static readonly Color TooltipText = new Color(0.969f, 0.929f, 0.839f);

        /// <summary>提示框文字里需要强调的数字（浅金）。</summary>
        public static readonly Color TooltipGold = new Color(0.949f, 0.800f, 0.478f);

        // ==================== 条状填充 ====================

        /// <summary>玩家生命条。</summary>
        public static readonly Color FillHp = new Color(0.722f, 0.180f, 0.176f);

        /// <summary>怪物生命条。</summary>
        public static readonly Color FillMonsterHp = new Color(0.788f, 0.435f, 0.145f);

        /// <summary>经验条。</summary>
        public static readonly Color FillExp = new Color(0.729f, 0.553f, 0.259f);

        /// <summary>冷却中的技能槽状态文字。</summary>
        public static readonly Color FillSkillCooldown = new Color(0.325f, 0.396f, 0.549f);

        /// <summary>就绪的技能槽状态文字。</summary>
        public static readonly Color FillSkillReady = new Color(0.196f, 0.510f, 0.267f);

        // ==================== 按钮 ====================

        /// <summary>主行动按钮（开始 / 购买）。</summary>
        public static readonly Color BtnPrimary = new Color(0.216f, 0.353f, 0.243f);

        /// <summary>中性次要按钮（加速、自动购买）。</summary>
        public static readonly Color BtnNeutral = new Color(0.208f, 0.169f, 0.133f);

        /// <summary>金色按钮（强化等成长操作）。</summary>
        public static readonly Color BtnGold = new Color(0.361f, 0.286f, 0.133f);

        /// <summary>危险按钮（重置 / 删除）。</summary>
        public static readonly Color BtnDanger = new Color(0.396f, 0.169f, 0.149f);

        /// <summary>不可点击时的按钮底色。</summary>
        public static readonly Color BtnDisabled = new Color(0.474f, 0.443f, 0.396f);

        /// <summary>不可点击时的按钮文字。</summary>
        public static readonly Color BtnDisabledText = new Color(0.792f, 0.769f, 0.722f);

        /// <summary>
        /// 深皮革底框上的「未选中 / 次要」文字：暖米白。
        /// 别用 TextNormal/TextMuted —— 那两支是给羊皮纸的深墨，画在深皮革上对比度只有 2:1 左右，
        /// 表现就是「按钮上的字发灰看不清」（成就页签、行商页签、强化/洗练槽位都踩过）。
        /// </summary>
        public static readonly Color BtnText = new Color(0.871f, 0.816f, 0.686f);

        /// <summary>深皮革底框上的禁用 / 未解锁文字：同一支暖色压暗两档，仍要明显亮于底色。</summary>
        public static readonly Color BtnTextDim = new Color(0.612f, 0.573f, 0.494f);

        /// <summary>深皮革底框上的「已选中但非佩戴」文字：浅金。</summary>
        public static readonly Color BtnTextGold = new Color(0.949f, 0.800f, 0.478f);

        /// <summary>
        /// 未选中的皮革底框着色（与 Texture 相乘）。旧的 (0.60,0.64,0.72) 偏冷蓝，
        /// 压在羊皮纸上像蒙了层灰；换暖中性，只降明度不改色相。
        /// </summary>
        public static readonly Color FrameTintDim = new Color(0.760f, 0.718f, 0.646f);

        /// <summary>顶部状态按钮（存档 / 选关）。</summary>
        public static readonly Color BtnHeader = new Color(0.180f, 0.145f, 0.114f);

        /// <summary>选中的速度档。</summary>
        public static readonly Color BtnSpeedActive = new Color(0.263f, 0.376f, 0.514f);

        // ==================== 强调 ====================

        /// <summary>卡片 / 面板的 1px 细描边（烫金）。</summary>
        public static readonly Color Border = new Color(0.706f, 0.573f, 0.353f);

        /// <summary>更弱的一层描边（内嵌区块、分隔）。</summary>
        public static readonly Color BorderSoft = new Color(0.40f, 0.31f, 0.20f, 0.22f);

        /// <summary>强调色（分区条、选中态、关键数字）。</summary>
        public static readonly Color Accent = new Color(0.769f, 0.612f, 0.286f);

        /// <summary>半透明强调色（分区条、焦点圈）。</summary>
        public static readonly Color AccentSoft = new Color(0.769f, 0.612f, 0.286f, 0.85f);

        /// <summary>横幅描边（琥珀金）。</summary>
        public static readonly Color AccentBanner = new Color(0.706f, 0.447f, 0.153f, 0.95f);
        /// <summary>居中横幅：深皮革底（不透明，压住下面的羊皮纸与战斗图）。</summary>
        public static readonly Color BannerBg = new Color(0.133f, 0.102f, 0.075f, 0.985f);
        /// <summary>横幅标题：亮金，压在 BannerBg 上。</summary>
        public static readonly Color BannerTitle = new Color(0.968f, 0.780f, 0.400f);
        /// <summary>横幅副标题：接近羊皮纸的浅墨，压在 BannerBg 上。</summary>
        public static readonly Color BannerSub = new Color(0.815f, 0.855f, 0.910f);

        /// <summary>横幅主标题。</summary>
        public static readonly Color AccentBannerTitle = new Color(0.745f, 0.478f, 0.145f);

        /// <summary>怪物名牌（暖金）——只出现在深色战斗舞台上，保持高亮。</summary>
        public static readonly Color AccentMonsterTitle = new Color(0.933f, 0.780f, 0.435f);

        /// <summary>技能槽高亮态（选中 / 可拖放）。</summary>
        public static readonly Color AccentSlotHighlight = new Color(0.769f, 0.612f, 0.286f, 0.95f);

        /// <summary>技能槽就绪态边框。</summary>
        public static readonly Color AccentSlotReady = new Color(0.220f, 0.580f, 0.310f, 0.90f);

        // ==================== 日志语义色（墨色版） ====================

        /// <summary>成功：开始战斗、获得、升级、自动换装更优。</summary>
        public static readonly Color LogSuccess = new Color(0.227f, 0.451f, 0.212f);

        /// <summary>回复生命、复活、战前回满。</summary>
        public static readonly Color LogHeal = new Color(0.180f, 0.463f, 0.329f);

        /// <summary>中性信息：装备、技能、卸下、存档切换。</summary>
        public static readonly Color LogInfo = new Color(0.243f, 0.353f, 0.478f);

        /// <summary>平淡信息：开局提示、无特殊色彩的叙述。</summary>
        public static readonly Color LogNeutral = new Color(0.322f, 0.282f, 0.243f);

        /// <summary>收益与奖励：金币、掉落、经验。</summary>
        public static readonly Color LogGold = new Color(0.616f, 0.404f, 0.078f);

        /// <summary>系统事件：暂停、切速、商店开放。</summary>
        public static readonly Color LogSystem = new Color(0.573f, 0.427f, 0.098f);

        /// <summary>失败与条件不足：金币不够、槽位冲突。</summary>
        public static readonly Color LogWarn = new Color(0.635f, 0.337f, 0.098f);

        /// <summary>破坏性操作与死亡：重置、删档、阵亡。</summary>
        public static readonly Color LogDanger = new Color(0.655f, 0.208f, 0.157f);

        /// <summary>巅峰相关（紫墨）。</summary>
        public static readonly Color LogParagon = new Color(0.435f, 0.286f, 0.604f);

        /// <summary>新章节解锁。</summary>
        public static readonly Color LogUnlock = new Color(0.196f, 0.443f, 0.404f);

        // ==================== 棋盘格与飘字 ====================

        /// <summary>伤害飘字（玩家造成）——叠在深色战斗图上，保持高亮。</summary>
        public static readonly Color FloatDamage = new Color(0.55f, 0.92f, 1f);

        /// <summary>暴击飘字（玩家造成）。</summary>
        public static readonly Color FloatCrit = new Color(1f, 0.82f, 0.28f);

        /// <summary>受伤飘字（玩家承受）。</summary>
        public static readonly Color FloatTaken = new Color(1f, 0.55f, 0.55f);

        /// <summary>受暴击飘字（玩家承受）。</summary>
        public static readonly Color FloatTakenCrit = new Color(1f, 0.35f, 0.42f);

        /// <summary>DPS 读数（青绿墨）。</summary>
        public static readonly Color TextMint = new Color(0.149f, 0.447f, 0.380f);

        /// <summary>已通关关卡标记。</summary>
        public static readonly Color TextPassed = new Color(0.337f, 0.451f, 0.318f);

        /// <summary>列表行被查看 / 悬停。</summary>
        public static readonly Color RowBgSelected = new Color(0.855f, 0.784f, 0.612f);

        /// <summary>卡片内的次级按钮（皮片）。</summary>
        public static readonly Color CardBtn = new Color(0.278f, 0.235f, 0.180f);

        /// <summary>卡片内的危险色（删除存档）。</summary>
        public static readonly Color CardDanger = new Color(0.435f, 0.227f, 0.204f);
    }
}

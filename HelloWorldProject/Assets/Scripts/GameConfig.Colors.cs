using UnityEngine;

namespace AutoBattle
{
    /// <summary>
    /// 全局配色常量表。所有 UI 与日志颜色都应引用这里，不再在调用点写
    /// <c>new Color(...)</c> 字面量。
    ///
    /// 为什么要归并：改动前全工程有 193 处颜色字面量、139 个不同的值，其中
    /// 「金黄色文字」有 9 个几乎无法区分的变体（1f,0.85f,0.5f / 1f,0.9f,0.55f /
    /// 1f,0.95f,0.5f …），「警告橙」有 5 个，「信息蓝」有 8 个。玩家分辨不出这些
    /// 差异，它们只是不同时期随手挑值的沉积物；语义相同就该是同一个颜色。
    ///
    /// 取值原则：**每个语义取该族中出现次数最多的那个字面量**，这样归并后绝大
    /// 多数位置像素不变，少数近似值只会微调，不会造成视觉回归。
    /// </summary>
    public static partial class GameConfig
    {
        // ==================== 容器与底板 ====================

        /// <summary>左右主面板底色。</summary>
        public static readonly Color PanelBg = new Color(0.075f, 0.086f, 0.118f);

        /// <summary>浮层面板底色（选关 / 技能 / 存档 / 强化）。</summary>
        public static readonly Color OverlayBg = new Color(0.055f, 0.063f, 0.086f);

        /// <summary>面板内更暗的一层（商店区、buff 栏底）。</summary>
        public static readonly Color InsetBg = new Color(0.062f, 0.071f, 0.098f);

        /// <summary>卡片底色。</summary>
        public static readonly Color CardBg = new Color(0.094f, 0.110f, 0.153f);

        /// <summary>卡片底色（不可用 / 未解锁）。</summary>
        public static readonly Color CardBgDim = new Color(0.078f, 0.090f, 0.122f);

        /// <summary>卡片底色（已选中 / 当前槽）。</summary>
        public static readonly Color CardBgActive = new Color(0.075f, 0.190f, 0.180f);

        /// <summary>列表行 / 槽位底色。</summary>
        public static readonly Color RowBg = new Color(0.106f, 0.125f, 0.173f);

        /// <summary>列表行底色（次级、未选中）。</summary>
        public static readonly Color RowBgDim = new Color(0.086f, 0.102f, 0.145f);

        /// <summary>生命条底槽。</summary>
        public static readonly Color BarTrackHp = new Color(0.145f, 0.075f, 0.090f);

        /// <summary>经验条底槽。</summary>
        public static readonly Color BarTrackExp = new Color(0.090f, 0.078f, 0.160f);

        /// <summary>1 像素分隔线。</summary>
        public static readonly Color Divider = new Color(1f, 1f, 1f, 0.07f);

        /// <summary>全屏遮罩（横幅背后）。</summary>
        public static readonly Color ScreenDim = new Color(0.040f, 0.050f, 0.080f, 0.97f);

        /// <summary>只有窗口底色那么亮的全屏背景。</summary>
        public static readonly Color WindowBg = new Color(0.043f, 0.051f, 0.071f);

        /// <summary>技能槽左下角的冷却压暗层。</summary>
        public static readonly Color ShadeOverlay = new Color(0f, 0f, 0f, 0.50f);

        /// <summary>半透明白（图标底衬）。</summary>
        public static readonly Color ImageHalf = new Color(1f, 1f, 1f, 0.50f);

        /// <summary>完全透明（占位、擦除边框）。</summary>
        public static readonly Color Clear = new Color(0f, 0f, 0f, 0f);

        // ==================== 文字 ====================

        /// <summary>面板标题（略带暖白）。</summary>
        public static readonly Color TextTitle = new Color(0.960f, 0.930f, 0.820f);

        /// <summary>正文。</summary>
        public static readonly Color TextNormal = new Color(0.900f, 0.920f, 0.960f);

        /// <summary>次要说明、提示行。</summary>
        public static readonly Color TextMuted = new Color(0.550f, 0.600f, 0.690f);

        /// <summary>禁用 / 未解锁的文字。</summary>
        public static readonly Color TextDisabled = new Color(0.360f, 0.390f, 0.460f);

        /// <summary>金币等高价值数字。</summary>
        public static readonly Color TextGold = new Color(1f, 0.800f, 0.350f);

        /// <summary>冷色信息文字（蓝白）。</summary>
        public static readonly Color TextInfo = new Color(0.680f, 0.790f, 1f);

        /// <summary>攻击力一行。</summary>
        public static readonly Color TextAttack = new Color(1f, 0.700f, 0.480f);

        /// <summary>防御力一行。</summary>
        public static readonly Color TextDefense = new Color(0.580f, 0.830f, 1f);

        /// <summary>速度一行。</summary>
        public static readonly Color TextSpeed = new Color(0.620f, 0.890f, 0.600f);

        /// <summary>暴击一行。</summary>
        public static readonly Color TextCrit = new Color(1f, 0.620f, 0.780f);

        /// <summary>经验一行。</summary>
        public static readonly Color TextExp = new Color(0.720f, 0.650f, 1f);

        // ==================== 条状填充 ====================

        /// <summary>玩家生命条。</summary>
        public static readonly Color FillHp = new Color(0.900f, 0.280f, 0.320f);

        /// <summary>怪物生命条。</summary>
        public static readonly Color FillMonsterHp = new Color(0.950f, 0.400f, 0.250f);

        /// <summary>经验条。</summary>
        public static readonly Color FillExp = new Color(0.580f, 0.420f, 0.950f);

        /// <summary>冷却中的技能槽状态文字。</summary>
        public static readonly Color FillSkillCooldown = new Color(0.700f, 0.800f, 1f);

        /// <summary>就绪的技能槽状态文字。</summary>
        public static readonly Color FillSkillReady = new Color(0.500f, 1f, 0.650f);

        // ==================== 按钮 ====================

        /// <summary>主行动按钮（开始 / 购买）。</summary>
        public static readonly Color BtnPrimary = new Color(0.130f, 0.450f, 0.350f);

        /// <summary>中性次要按钮（加速、自动购买）。</summary>
        public static readonly Color BtnNeutral = new Color(0.120f, 0.150f, 0.210f);

        /// <summary>金色按钮（强化等成长操作）。</summary>
        public static readonly Color BtnGold = new Color(0.320f, 0.260f, 0.130f);

        /// <summary>危险按钮（重置 / 删除）。</summary>
        public static readonly Color BtnDanger = new Color(0.380f, 0.150f, 0.180f);

        /// <summary>不可点击时的按钮底色。</summary>
        public static readonly Color BtnDisabled = new Color(0.140f, 0.150f, 0.180f);

        /// <summary>不可点击时的按钮文字。</summary>
        public static readonly Color BtnDisabledText = new Color(0.450f, 0.470f, 0.530f);

        /// <summary>顶部状态按钮（存档 / 选关）。</summary>
        public static readonly Color BtnHeader = new Color(0.120f, 0.150f, 0.220f);

        /// <summary>选中的速度档。</summary>
        public static readonly Color BtnSpeedActive = new Color(0.200f, 0.520f, 0.920f);

        // ==================== 强调 ====================

        /// <summary>卡片 / 面板的 1px 细描边。</summary>
        public static readonly Color Border = new Color(0.140f, 0.170f, 0.230f);

        /// <summary>更弱的一层描边（内嵌区块、分隔）。</summary>
        public static readonly Color BorderSoft = new Color(1f, 1f, 1f, 0.06f);

        /// <summary>强调色（分区条、选中态、关键数字）。</summary>
        public static readonly Color Accent = new Color(0.300f, 0.600f, 1f);

        /// <summary>半透明强调色（分区条、焦点圈）。</summary>
        public static readonly Color AccentSoft = new Color(0.320f, 0.620f, 1f, 0.85f);

        /// <summary>横幅描边（琥珀金）。</summary>
        public static readonly Color AccentBanner = new Color(1f, 0.78f, 0.32f, 0.95f);

        /// <summary>横幅主标题。</summary>
        public static readonly Color AccentBannerTitle = new Color(1f, 0.90f, 0.52f);

        /// <summary>怪物名牌（暖金）。</summary>
        public static readonly Color AccentMonsterTitle = new Color(1f, 0.820f, 0.500f);

        /// <summary>技能槽高亮态（选中 / 可拖放）。</summary>
        public static readonly Color AccentSlotHighlight = new Color(0.400f, 0.680f, 1f, 0.90f);

        /// <summary>技能槽就绪态边框。</summary>
        public static readonly Color AccentSlotReady = new Color(0.450f, 1f, 0.600f, 0.90f);

        // ==================== 日志语义色 ====================

        /// <summary>成功：开始战斗、获得、升级、自动换装更优。</summary>
        public static readonly Color LogSuccess = new Color(0.6f, 1f, 0.6f);

        /// <summary>回复生命、复活、战前回满。</summary>
        public static readonly Color LogHeal = new Color(0.6f, 1f, 0.7f);

        /// <summary>中性信息：装备、技能、卸下、存档切换。</summary>
        public static readonly Color LogInfo = new Color(0.8f, 0.9f, 1f);

        /// <summary>平淡信息：开局提示、无特殊色彩的叙述。</summary>
        public static readonly Color LogNeutral = new Color(0.8f, 0.8f, 0.84f);

        /// <summary>收益与奖励：金币、掉落、经验。</summary>
        public static readonly Color LogGold = new Color(1f, 0.85f, 0.4f);

        /// <summary>系统事件：暂停、切速、商店开放。</summary>
        public static readonly Color LogSystem = new Color(1f, 0.9f, 0.55f);

        /// <summary>失败与条件不足：金币不够、槽位冲突。</summary>
        public static readonly Color LogWarn = new Color(1f, 0.7f, 0.45f);

        /// <summary>破坏性操作与死亡：重置、删档、阵亡。</summary>
        public static readonly Color LogDanger = new Color(1f, 0.5f, 0.42f);

        /// <summary>巅峰相关（紫）。</summary>
        public static readonly Color LogParagon = new Color(0.85f, 0.75f, 1f);

        /// <summary>新章节解锁。</summary>
        public static readonly Color LogUnlock = new Color(0.7f, 1f, 0.85f);

        // ==================== 棋盘格与飘字 ====================

        /// <summary>伤害飘字（玩家造成）。</summary>
        public static readonly Color FloatDamage = new Color(0.55f, 0.92f, 1f);

        /// <summary>暴击飘字（玩家造成）。</summary>
        public static readonly Color FloatCrit = new Color(1f, 0.82f, 0.28f);

        /// <summary>受伤飘字（玩家承受）。</summary>
        public static readonly Color FloatTaken = new Color(1f, 0.55f, 0.55f);

        /// <summary>受暴击飘字（玩家承受）。</summary>
        public static readonly Color FloatTakenCrit = new Color(1f, 0.35f, 0.42f);

        /// <summary>DPS 读数（青绿）。</summary>
        public static readonly Color TextMint = new Color(0.620f, 0.950f, 0.820f);

        /// <summary>已通关关卡标记。</summary>
        public static readonly Color TextPassed = new Color(0.85f, 0.93f, 0.85f);

        /// <summary>列表行被查看 / 悬停。</summary>
        public static readonly Color RowBgSelected = new Color(0.200f, 0.290f, 0.450f);

        /// <summary>卡片内的次级按钮。</summary>
        public static readonly Color CardBtn = new Color(0.26f, 0.28f, 0.34f);

        /// <summary>卡片内的危险色（删除存档）。</summary>
        public static readonly Color CardDanger = new Color(0.35f, 0.24f, 0.26f);
    }
}

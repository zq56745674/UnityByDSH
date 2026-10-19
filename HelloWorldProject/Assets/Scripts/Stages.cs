using UnityEngine;

namespace AutoBattle
{
    /// <summary>
    /// 一个关卡的静态定义（步骤 4a）。
    /// 关卡怪物等级固定，不随主角成长——这是关卡制与「怪物等级 = 主角等级」的根本区别。
    /// </summary>
    public class StageDef
    {
        /// <summary>章节号，1 起。</summary>
        public int chapter;

        /// <summary>章节内的关卡号，1 起。</summary>
        public int stage;

        /// <summary>本关所有怪物的等级（固定值）。</summary>
        public int monsterLevel;

        /// <summary>通关需要击杀的怪物数量。</summary>
        public int killTarget;

        /// <summary>本关最后一只（关底）的档次；普通关是精英，章末关是 Boss。</summary>
        public MonsterTier clearTier;

        /// <summary>形如 "3-4"。</summary>
        public string Code => chapter + "-" + stage;

        /// <summary>形如 "第 3 章 3-4"。</summary>
        public string Title => "第 " + chapter + " 章 " + Code;

        /// <summary>是不是本章最后一关。</summary>
        public bool IsChapterFinal => StageCatalog.IsChapterFinal(stage);
    }

    /// <summary>
    /// 章节 / 关卡目录（步骤 4a）。全部按规则程序化生成，不落地配置表——
    /// 规则本身（章数、每章关数、等级步长、击杀数曲线）都在 GameConfig 里，可以热改。
    /// </summary>
    public static class StageCatalog
    {
        /// <summary>章节总数：第 N 章的怪物等级 = ChapterMonsterLevelStep × N。</summary>
        public static int ChapterCount => GameConfig.ChapterCount;

        /// <summary>每章的关卡数。</summary>
        public static int StagesPerChapter => GameConfig.StagesPerChapter;

        /// <summary>主角起始关卡。</summary>
        public const int FirstChapter = 1;

        /// <summary>
        /// 是不是本章最后一关。这条判断原先散在四个地方各写一遍
        /// （StageDef.IsChapterFinal、FirstClearGold、GameController 的发奖与横幅），
        /// 收敛到一处，改每章关数时不会漏改。
        /// </summary>
        public static bool IsChapterFinal(int stage) => stage >= StagesPerChapter;

        /// <summary>
        /// 取一个关卡的定义。章节 / 关卡号会被夹到合法范围，所以界面上越界点击不会炸。
        /// </summary>
        public static StageDef Get(int chapter, int stage)
        {
            int c = Mathf.Clamp(chapter, FirstChapter, ChapterCount);
            int s = Mathf.Clamp(stage, 1, StagesPerChapter);

            return new StageDef
            {
                chapter = c,
                stage = s,
                monsterLevel = GameConfig.ChapterMonsterLevelStep * (c - 1) + s,
                killTarget = KillTargetOf(c),
                clearTier = ClearTierOf(c, s),
            };
        }

        /// <summary>
        /// 每关的击杀目标：章节越靠后怪越多。
        /// 1~3 章 3 只、4~6 章 4 只、7~9 章 5 只、10 章起 6 只。
        /// </summary>
        public static int KillTargetOf(int chapter)
        {
            // 负上限会让 Clamp 的 max < min，killTarget 变成负数并让 IsStageCleared 恒真。
            int maxBonus = Mathf.Max(0, GameConfig.StageKillTargetMaxBonus);
            int extra = Mathf.Clamp((Mathf.Max(1, chapter) - 1) / 3, 0, maxBonus);
            return Mathf.Max(1, GameConfig.StageKillTargetBase) + extra;
        }

        /// <summary>
        /// 关底怪的档次：普通关是精英，章末关是 Boss；
        /// 第 1 章作为新手章不给 Boss，章末仍然是精英。
        /// </summary>
        public static MonsterTier ClearTierOf(int chapter, int stage)
        {
            if (stage < StagesPerChapter) return MonsterTier.Elite;
            return chapter <= GameConfig.FirstBossChapter - 1 ? MonsterTier.Elite : MonsterTier.Boss;
        }

        /// <summary>
        /// 首通某一关的金币奖励（步骤 4c）。章末关额外给一笔，因为那等于打通一整章。
        /// 公式只放在这一处：发奖（GameController）和界面预览（GameUI）都调它，
        /// 免得改了配置之后两边对不上。
        /// </summary>
        public static int FirstClearGold(int chapter, int stage)
        {
            int gold = GameConfig.FirstClearGoldBase
                + GameConfig.FirstClearGoldPerChapter * Mathf.Max(0, chapter - 1);
            if (IsChapterFinal(stage)) gold += GameConfig.ChapterClearGoldBonus;
            return gold;
        }

        /// <summary>关卡编码（PlayerData.StageKey）还原成章节 / 关卡号后再算奖励。</summary>
        public static int FirstClearGoldOfKey(int stageKey)
        {
            int chapter = PlayerData.ChapterOfKey(stageKey);
            int stage = PlayerData.StageOfKey(stageKey);
            // 首通记录是 List<int>，读档已清洗；这里再挡一次直接改列表/旧档残留的非法 key，
            // 避免它们被当成“越靠后的章节”反复算进首通总额。
            if (chapter < FirstChapter || chapter > ChapterCount) return 0;
            if (stage < 1 || stage > StagesPerChapter) return 0;
            return FirstClearGold(chapter, stage);
        }

        /// <summary>前 chapters 章全部首通能拿到的金币总额（用于进度总览的分母）。</summary>
        public static int TotalFirstClearGold(int chapters)
        {
            int sum = 0;
            int last = Mathf.Clamp(chapters, 0, ChapterCount);
            for (int c = 1; c <= last; c++)
            {
                for (int s = 1; s <= StagesPerChapter; s++) sum += FirstClearGold(c, s);
            }
            return sum;
        }

        /// <summary>该章是不是已经打完了（章末关通关）。</summary>
        public static bool IsChapterCleared(int chapter, int stage)
        {
            return chapter >= ChapterCount && stage >= StagesPerChapter;
        }

        /// <summary>
        /// 把关卡进度往前推一关：章末关之后进入下一章第一关。
        /// 返回 false 表示已经打到最后一关（终局）。
        /// </summary>
        public static bool Advance(ref int chapter, ref int stage)
        {
            if (IsChapterCleared(chapter, stage)) return false;

            stage++;
            if (stage > StagesPerChapter)
            {
                chapter++;
                stage = 1;
            }
            return true;
        }
    }
}

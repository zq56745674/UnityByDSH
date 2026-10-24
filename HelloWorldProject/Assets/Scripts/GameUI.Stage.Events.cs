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
        // 战斗事件到表现层的桥接（入场/受击/死亡/飘字）
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
            float jitter = GameRandom.Range(-12f, 12f);

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

                // ---- 21：15 个技能各自的特效（不再全都借 fx_heavy） ----
                case BattleFx.SkillCritChain:
                    _battleMonsterShake = 0.7f;
                    PlayBattleFxSprite("fx_crit_chain", StageMonsterCx + jitter, monCy, 150f, 0.3f, 1.4f, 0f);
                    break;

                case BattleFx.SkillNearDeath:
                    PlayBattleFxSprite("fx_near_death", StageHeroCx, StageGroundY - 34f, 200f, 0.6f, 1.3f, 0f);
                    break;

                case BattleFx.SkillUndying:
                    PlayBattleFxSprite("fx_undying", StageHeroCx, heroCy, 160f, 0.5f, 1.3f, 0f);
                    break;

                case BattleFx.SkillGiantSlayer:
                    PlayBattleFxSprite("fx_giant_slayer", StageMonsterCx, monCy, 176f, 0.45f, 1.35f, -12f);
                    break;

                // 纯属性被动没有战斗事件，只在学会那一帧由 CheckStageSkillLearn 直接放
                case BattleFx.PassiveIronBody:
                    PlayBattleFxSprite("fx_iron_body", StageHeroCx, heroCy, 176f, 0.7f, 1.25f, 0f);
                    break;

                case BattleFx.PassiveBerserkHeart:
                    PlayBattleFxSprite("fx_berserk_heart", StageHeroCx, heroCy, 176f, 0.7f, 1.25f, 0f);
                    break;

                case BattleFx.PassiveLethalStrike:
                    PlayBattleFxSprite("fx_lethal_strike", StageHeroCx, heroCy, 176f, 0.7f, 1.25f, 0f);
                    break;

                case BattleFx.PassiveSwiftness:
                    PlayBattleFxSprite("fx_swiftness", StageHeroCx, heroCy, 176f, 0.7f, 1.25f, 0f);
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

        /// <summary>章节 → 怪的族，和 GameConfig.ChapterFamilyNames 一一对应（11 章 11 族）。</summary>
        private static readonly string[] StageFamilyKeys =
        {
            "goblin", "kobold", "skeleton", "ogre", "orc", "spider",
            "undead", "gargoyle", "demon", "abyss", "dragon"
        };

        private static string MonsterFamilyKey(int chapter)
        {
            int index = Mathf.Clamp(chapter, 1, StageFamilyKeys.Length) - 1;
            return StageFamilyKeys[index];
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

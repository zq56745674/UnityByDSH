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
        // 舞台每帧推进与怪物/角色布局
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
            CheckStageSkillLearn();
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
                ApplyMonsterAuras(live);
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
                    if (_battleMonsterAura != null) _battleMonsterAura.enabled = false;
                    if (_battleAffixAura != null) _battleAffixAura.enabled = false;
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

            // 光环跟入场一起淡入、跟死亡一起淡出，并缓慢呼吸
            float auraK = Mathf.Clamp01(_battleMonsterSpawn) * (1f - deathK);
            float auraPulse = 1f + Mathf.Sin(_battleAnimTime * 2.2f) * 0.045f;
            ApplyAuraPose(_battleMonsterAura, 0.92f * auraK, auraPulse);
            ApplyAuraPose(_battleAffixAura, 0.86f * auraK, auraPulse * 1.03f);
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

            StageDef stage = _game.Player.CurrentStage;
            int chapter = Mathf.Clamp(stage.chapter, 1, GameConfig.ChapterCount);

            string label;
            if (_game.EndlessActive && _game.EndlessRun != null)
            {
                label = $"无尽 · 第 {_game.EndlessRun.floor} 层";
            }
            else
            {
                label = $"第 {chapter} 章 · {GameConfig.ChapterFamilyNames[chapter - 1]}";
            }

            RefreshStageBackdrop(chapter);

            if (label == _battleChapterLabel) return;
            _battleChapterLabel = label;
            _battleChapterText.text = label;
        }

    }
}

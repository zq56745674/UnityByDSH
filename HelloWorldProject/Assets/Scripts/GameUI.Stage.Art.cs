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
        // 章节背景、档次光环与技能被动特效
        // ==================== 章节背景 / 档次光环 / 被动特效 ====================

        /// <summary>按章挑背景图。无尽也落在同一套图里（按当前章节）。</summary>
        private static string BackdropFor(int chapter)
        {
            switch (Mathf.Clamp(chapter, 1, 11))
            {
                case 1:
                case 2:
                case 6: return "bg_cavern";
                case 3:
                case 7: return "bg_crypt";
                case 4:
                case 5: return "bg_wilds";
                default: return "bg_citadel";
            }
        }

        /// <summary>
        /// 同一张底图在不同章上色温，避免 8~11 章（都是 bg_citadel）看起来完全同一关。
        /// 值都很轻，只做「这是另一片区域」的暗示，不抢角色。
        /// </summary>
        private static Color BackdropTint(int chapter)
        {
            switch (Mathf.Clamp(chapter, 1, 11))
            {
                case 1: return new Color(0.92f, 1.00f, 1.05f);   // 洞窟：冷、潮湿
                case 2: return new Color(1.05f, 0.98f, 0.90f);   // 洞窟：火把偏暖
                case 6: return new Color(0.84f, 0.94f, 1.02f);   // 洞窟：更深更蓝
                case 3: return new Color(0.86f, 0.98f, 1.06f);   // 陵墓：幽蓝
                case 7: return new Color(0.78f, 0.86f, 1.02f);   // 陵墓：更暗
                case 4: return new Color(1.06f, 0.96f, 0.86f);   // 荒野：黄昏暖
                case 5: return new Color(1.00f, 0.90f, 0.90f);   // 荒野：偏赤
                case 8: return new Color(0.94f, 0.96f, 1.04f);   // 魔域：石青
                case 9: return new Color(1.10f, 0.82f, 0.80f);   // 魔域：恶魔赤
                case 10: return new Color(0.94f, 0.82f, 1.10f);  // 魔域：深渊紫
                default: return new Color(1.08f, 0.96f, 0.90f);  // 龙裔：金
            }
        }

        /// <summary>只在换章时换图：Resources.Load 有缓存，但每帧重设 sprite 会一直刷 UI。</summary>
        private void RefreshStageBackdrop(int chapter)
        {
            if (_battleBackdropArt == null) return;

            int ch = Mathf.Clamp(chapter, 1, 11);
            string key = BackdropFor(ch);
            string cacheKey = key + ":" + ch;   // 同图不同章也要重设色温
            if (cacheKey == _battleBackdropKey) return;
            _battleBackdropKey = cacheKey;

            if (string.IsNullOrEmpty(key))
            {
                _battleBackdropArt.sprite = null;
                _battleBackdropArt.enabled = false;
                return;
            }

            Sprite sprite = SpriteOf("Sprites/Backgrounds/" + key);
            if (sprite == null)
            {
                _battleBackdropArt.sprite = null;
                _battleBackdropArt.enabled = false;
                return;
            }

            _battleBackdropArt.sprite = sprite;
            Color tint = BackdropTint(ch);
            _battleBackdropArt.color = new Color(tint.r, tint.g, tint.b, 0.68f);   // 压暗一档，角色仍是主体
            _battleBackdropArt.enabled = true;
        }

        /// <summary>精英/Boss 档次光环 + 词缀光环（吸血/护盾/剧毒）只在对应怪身上亮。</summary>
        private void ApplyMonsterAuras(MonsterData monster)
        {
            string tierKey = null;
            float tierWidth = StageAuraEliteW;
            if (monster != null)
            {
                if (monster.tier == MonsterTier.Boss)
                {
                    tierKey = "Sprites/Auras/aura_boss";
                    tierWidth = StageAuraBossW;
                }
                else if (monster.tier == MonsterTier.Elite)
                {
                    tierKey = "Sprites/Auras/aura_elite";
                }
            }
            ApplyAuraSprite(_battleMonsterAura, tierKey, tierWidth);

            string affixKey = null;
            if (monster != null && monster.HasAffix)
            {
                switch (monster.affix)
                {
                    case MonsterAffix.Lifesteal: affixKey = "Sprites/Auras/affix_drain"; break;
                    case MonsterAffix.Shield: affixKey = "Sprites/Auras/affix_shield"; break;
                    case MonsterAffix.Poison: affixKey = "Sprites/Auras/affix_poison"; break;
                }
            }
            ApplyAuraSprite(_battleAffixAura, affixKey, StageAuraAffixW);
        }

        private void ApplyAuraSprite(Image aura, string path, float width)
        {
            if (aura == null) return;

            float height = width * AuraSquash;
            aura.rectTransform.sizeDelta = new Vector2(width, height);
            aura.rectTransform.anchoredPosition =
                new Vector2(StageMonsterCx, -(StageGroundY - height * 0.32f));
            aura.rectTransform.localScale = Vector3.one;

            if (string.IsNullOrEmpty(path))
            {
                aura.enabled = false;
                return;
            }

            Sprite sprite = SpriteOf(path);
            if (sprite == null)
            {
                aura.enabled = false;
                return;
            }

            aura.sprite = sprite;
            aura.color = new Color(1f, 1f, 1f, 0f);   // 透明度交给 Tick 淡入
            aura.enabled = true;
        }

        private static void ApplyAuraPose(Image aura, float alpha, float pulse)
        {
            if (aura == null || !aura.enabled) return;
            SetImageAlpha(aura, Mathf.Clamp01(alpha));
            aura.rectTransform.localScale = new Vector3(pulse, pulse, 1f);
        }

        /// <summary>
        /// 纯属性被动（钢铁之躯/狂暴之心/致命打击/迅捷）在战斗里没有事件可挂，
        /// 所以只在「学会的那一瞬间」放一次特效：比对技能位掩码，增量就是刚学的。
        /// </summary>
        private void CheckStageSkillLearn()
        {
            if (_game == null || _game.Player == null) return;

            int mask = _game.Player.ownedSkillMask;
            if (_battleLastSkillMask < 0)
            {
                _battleLastSkillMask = mask;   // 首帧只记录，避免进游戏就放一串特效
                return;
            }

            int added = mask & ~_battleLastSkillMask;
            _battleLastSkillMask = mask;
            if (added == 0) return;

            float cy = StageGroundY - StageHeroH * 0.55f;
            if ((added & (1 << (int)SkillId.IronBody)) != 0)
                PlayBattleFxSprite("fx_iron_body", StageHeroCx, cy, 176f, 0.7f, 1.25f, 0f);
            if ((added & (1 << (int)SkillId.BerserkHeart)) != 0)
                PlayBattleFxSprite("fx_berserk_heart", StageHeroCx, cy, 176f, 0.7f, 1.25f, 0f);
            if ((added & (1 << (int)SkillId.LethalStrike)) != 0)
                PlayBattleFxSprite("fx_lethal_strike", StageHeroCx, cy, 176f, 0.7f, 1.25f, 0f);
            if ((added & (1 << (int)SkillId.Swiftness)) != 0)
                PlayBattleFxSprite("fx_swiftness", StageHeroCx, cy, 176f, 0.7f, 1.25f, 0f);
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

    }
}

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.Events;

namespace AutoBattle
{
    public partial class GameUI : MonoBehaviour
    {
        // 刷新实现：商店/技能栏/浮层的判脏与伤害日志解析
        /// <summary>商店与技能栏走 10Hz 文本，七个浮层各自在内部判脏。</summary>
        private void RefreshPanels(PlayerData p, bool textTick)
        {
            RefreshShop(p, textTick);
            RefreshSkillBar(p, textTick);
            RefreshSkillPanel();
            RefreshSkillChoice();
            RefreshStagePanel();
            RefreshSavePanel();
            RefreshEnhancePanel();
            RefreshReforgePanel();
            RefreshSettingsPanel();
            RefreshMusicPanel();
            RefreshComparePanel();
            RefreshEndlessPanel();
            RefreshRebirthPanel();
            RefreshAchievementPanel();
            RefreshAbout();
            RefreshDisplayPanel();
            RefreshGuide();
            RefreshBanner();
        }

        /// <summary>
        /// 日志双写：底部 ticker 只留最新两行（旧在上、新在下，和聊天一样从下往上读），
        /// 展开面板写最近 180 条。只有 LogVersion 变了才重建，避免每帧拼大字符串。
        /// </summary>
        private void RefreshLog()
        {
            if (_game.LogVersion == _lastLogVersion) return;
            _lastLogVersion = _game.LogVersion;

            int count = _game.Log.Count;

            // ---- ticker：两行 ----
            var ticker = new System.Text.StringBuilder(256);
            int tickerFrom = Mathf.Max(0, count - 2);
            for (int i = tickerFrom; i < count; i++)
            {
                if (i > tickerFrom) ticker.Append('\n');
                AppendTickerLine(ticker, _game.Log[i]);
            }
            if (_logText != null) _logText.text = ticker.ToString();

            // ---- 展开面板：旧在上、新在下，读完自动跟到底 ----
            var detail = new System.Text.StringBuilder(16384);
            int detailFrom = Mathf.Max(0, count - MaxDetailLogLines);
            for (int i = detailFrom; i < count; i++)
            {
                AppendLogLine(detail, _game.Log[i]);
                detail.Append('\n');
            }
            FillBattleLogDetail(detail.ToString(), count);
        }

        /// <summary>
        /// ticker 单行：压掉日志自带的换行（掉落类会写第二行「◆ 词条」），过长截断。
        /// 不压就该占两行，ticker 只留两行时后一条会被挤掉。
        /// </summary>
        private static void AppendTickerLine(System.Text.StringBuilder sb, LogEntry entry)
        {
            const int MaxChars = 78;
            sb.Append("<color=#");
            sb.Append(ColorUtility.ToHtmlStringRGB(entry.color));
            sb.Append('>');

            string msg = entry.message;
            int shown = 0;
            for (int i = 0; i < msg.Length; i++)
            {
                char c = msg[i];
                if (c == '\n' || c == '\r')
                {
                    sb.Append("  ");
                    continue;
                }
                if (c == '\t') continue;
                if (shown >= MaxChars)
                {
                    sb.Append('…');
                    break;
                }
                sb.Append(c);
                shown++;
            }

            sb.Append("</color>");
        }

        /// <summary>日志一行（按条目的颜色包裹）。ticker 与展开面板共用，颜色口径不会走岔。</summary>
        private static void AppendLogLine(System.Text.StringBuilder sb, LogEntry entry)
        {
            sb.Append("<color=#");
            sb.Append(ColorUtility.ToHtmlStringRGB(entry.color));
            sb.Append('>');
            sb.Append(entry.message);
            sb.Append("</color>");
        }

        /// <summary>
        /// 五槽装备卡：标题 / 主属性 / 倍率+评分。
        /// 评分依赖角色属性，但只随「装备 / 等级 / 巅峰 / 强化 / 洗练 / 锁 / 技能」变化，
        /// 所以把这些值拼成签名，签名没动就不重算——否则每帧会白跑五次 EquipmentScore.Of。
        /// 洗练会原地改词条数值（引用不变），因此签名必须带上词条字段本身，不能只比引用。
        /// </summary>
        private void RefreshEquipmentLines(PlayerData p)
        {
            if (_equipTitles == null || _equipStats == null || _equipMetas == null) return;

            var sig = new System.Text.StringBuilder(320);
            sig.Append(p.level).Append('|').Append(p.paragonLevel).Append('|')
               .Append(p.ownedSkillMask).Append('|').Append(GameSettings.ShowScore ? 1 : 0);
            for (int i = 0; i < PlayerData.SkillSlotCount; i++) sig.Append(',').Append(p.GetSkillSlot(i));

            for (int i = 0; i < EquipmentSlots.Count; i++)
            {
                Equipment e = p.GetEquipment(EquipmentSlots.All[i]);
                sig.Append('|');
                if (e == null) { sig.Append('_'); continue; }
                sig.Append(e.level).Append(',').Append((int)e.quality).Append(',')
                   .Append(e.qualityMultiplier).Append(',').Append(e.enhanceLevel).Append(',')
                   .Append(e.reforgeCount).Append(',').Append(e.locked ? 1 : 0).Append(',')
                   .Append(e.attack).Append(',').Append(e.defense).Append(',').Append(e.hp).Append(',')
                   .Append(e.speed).Append(',').Append(e.haste).Append(',').Append(e.armorPen).Append(',')
                   .Append(e.critChance).Append(',').Append(e.critDamage).Append(',')
                   .Append(e.affixCritChance).Append(',').Append(e.affixCritDamage).Append(',')
                   .Append(e.affixAttackSpeed).Append(',').Append(e.affixAllStats).Append(',')
                   .Append(e.affixArmorPen).Append(',').Append(e.affixScale);
            }

            string signature = sig.ToString();
            if (signature == _scoreCacheSignature) return;
            _scoreCacheSignature = signature;

            for (int i = 0; i < EquipmentSlots.Count; i++)
            {
                EquipmentSlot slot = EquipmentSlots.All[i];
                Equipment e = p.GetEquipment(slot);
                if (e == null)
                {
                    _scoreEquipTitles[i] = $"<color=#5F6779>{EquipmentSlots.Name(slot)} · 空</color>";
                    _scoreEquipStats[i] = string.Empty;
                    _scoreEquipMetas[i] = string.Empty;
                    continue;
                }

                string hex = ColorUtility.ToHtmlStringRGB(e.QualityColor);
                // 卡面只有 190 宽：标题只放「槽位 + 等级」，品质靠颜色，锁定用图标补一位。
                // 强化等级直接接在等级后面（用模型里的 EnhanceText，自带前导空格）：
                // 原来这里漏了 +N，同一件装备在对比/强化面板写 +5、在主界面却看不出来。
                // +N 用 <size=9> 收窄：12 号字下 90px 只够「槽位 + Lv.110」，加号后必须缩一点才不挤。
                _scoreEquipTitles[i] = $"<color=#{hex}><b>{EquipmentSlots.Name(slot)} Lv.{e.level}</b></color>"
                    + (e.EnhanceText.Length > 0 ? $"<size=9><b>{e.EnhanceText}</b></size>" : string.Empty)
                    + (e.locked ? " <color=#FFD966>🔒</color>" : string.Empty);
                _scoreEquipStats[i] = e.StatLine;
                _scoreEquipMetas[i] = FormatEquipmentMeta(e);
            }

            for (int i = 0; i < EquipmentSlots.Count; i++)
            {
                if (_equipTitles[i] != null) _equipTitles[i].text = _scoreEquipTitles[i];
                if (_equipStats[i] != null) _equipStats[i].text = _scoreEquipStats[i];
                if (_equipMetas[i] != null) _equipMetas[i].text = _scoreEquipMetas[i];

                // 卡面右上角那两个数（×倍率 / 评分）没有地方写标签，改在悬停说明里讲。
                if (_equipCards != null && i < _equipCards.Length)
                    SetTooltip(_equipCards[i], EquipmentCardTooltip(p.GetEquipment(EquipmentSlots.All[i])));
            }
        }

        /// <summary>装备卡悬停说明：解释右上角那两个没有标签的数字，并提示点卡片能对比。</summary>
        private string EquipmentCardTooltip(Equipment e)
        {
            if (e == null) return string.Empty;

            // 悬停说明按「短行」写：提示框是单行排版的小控件，一行讲一件事比长句好读得多。
            var sb = new System.Text.StringBuilder(200);
            sb.Append(e.Title);
            sb.Append("\n<color=#8C99B0>品质倍率 ×").Append(e.qualityMultiplier.ToString("0.00"));
            if (GameSettings.ShowScore)
            {
                float score = _game != null && _game.Player != null ? EquipmentScore.Of(_game.Player, e, e.slot) : 0f;
                sb.Append("   评分 ").Append(score.ToString("0"));
            }
            sb.Append("</color>");
            sb.Append("\n<color=#8C99B0>倍率越高属性越强；评分 = 相对同槽空着的收益。</color>");
            sb.Append("\n<color=#5F6779>点卡片可与「待选」逐项对比。</color>");
            return sb.ToString();
        }

        /// <summary>装备卡右上角：品质倍率 + 评分（评分可以关，见设置面板）。</summary>
        private string FormatEquipmentMeta(Equipment e)
        {
            if (e == null) return string.Empty;

            if (!GameSettings.ShowScore) return $"×{e.qualityMultiplier:0.00}";

            float score = _game != null && _game.Player != null ? EquipmentScore.Of(_game.Player, e, e.slot) : 0f;
            return $"<color=#8C99B0>×{e.qualityMultiplier:0.00}</color> <b>{score:0}</b>";
        }


        /// <summary>血条边框颜色：精英金色、Boss 红色、普通怪透明（不显示边框）。</summary>
        private static Color MonsterBorderColor(MonsterTier tier)
        {
            switch (tier)
            {
                case MonsterTier.Elite: return new Color(1f, 0.82f, 0.25f);
                case MonsterTier.Boss: return new Color(1f, 0.25f, 0.2f);
                default: return GameConfig.Clear;
            }
        }

        private static bool IsOpen(RectTransform panel)
        {
            return panel != null && panel.gameObject.activeSelf;
        }

        private static string StateName(GameController.State s)
        {
            switch (s)
            {
                case GameController.State.Fighting: return "战斗中";
                case GameController.State.WaitingRespawn: return "等待重生";
                case GameController.State.Reviving: return "复活中";
                case GameController.State.Shopping: return "商店中";
                case GameController.State.SkillChoice: return "选技能";
                case GameController.State.Ended: return "本局结束";
                default: return s.ToString();
            }
        }

        /// <summary>按比例设置填充条：用 anchorMax.x 实现，不改变父级布局。</summary>
        private static void SetBar(Image fill, float progress)
        {
            if (fill == null) return;
            RectTransform rt = fill.rectTransform;
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(Mathf.Clamp01(progress), 1f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

    }
}

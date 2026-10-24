using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.Events;

namespace AutoBattle
{
    public partial class GameUI : MonoBehaviour
    {
        // 表现层每帧效果：伤害飘字、实时 DPS 与 Buff 文本
        // ==================== P2：伤害飘字 ====================

        /// <summary>预建飘字池，避免战斗中反复创建 GameObject。</summary>
        private void BuildFloatPool(RectTransform layer)
        {
            _floatTexts = new Text[FloatPoolSize];
            _floatLife = new float[FloatPoolSize];
            _floatPos = new Vector2[FloatPoolSize];
            _floatVel = new Vector2[FloatPoolSize];
            for (int i = 0; i < FloatPoolSize; i++)
            {
                RectTransform r = CreateRect(layer, $"Float{i}", 0, 0, 220, 30);
                Text txt = r.gameObject.AddComponent<Text>();
                txt.font = _font;
                txt.fontSize = 20;
                txt.fontStyle = FontStyle.Bold;
                txt.alignment = TextAnchor.MiddleCenter;
                txt.raycastTarget = false;
                txt.supportRichText = false;
                txt.horizontalOverflow = HorizontalWrapMode.Overflow;
                txt.verticalOverflow = VerticalWrapMode.Overflow;
                txt.text = string.Empty;
                _floatTexts[i] = txt;
                _floatLife[i] = 0f;
            }
        }

        /// <summary>弹出一条飘字。x / y 是落点（相对飘字层左上角，y 向下为正）。</summary>
        private void SpawnFloat(string label, Color color, int size, float x, float y)
        {
            // 设置里关掉飘字就整条链路短路：伤害仍在结算，只是不再往屏幕上丢数字。
            if (!GameSettings.DamageFloat) return;
            if (_floatTexts == null) return;
            int i = _floatCursor;
            _floatCursor = (_floatCursor + 1) % FloatPoolSize;

            Text txt = _floatTexts[i];
            txt.text = label;
            txt.color = color;
            txt.fontSize = size;

            // 寿命压到 0.55 秒：攻速 0.27 秒时同屏会叠 3~4 个数字，停留久了就糊成一团。
            _floatLife[i] = 0.55f;
            // 三条纵向轨道轮流落点：只靠随机 x 仍会撞字（5 位数的字宽就有 70px），
            // 分轨后同一时刻最多两层，读得清。
            int lane = _floatLane;
            _floatLane = (_floatLane + 1) % 3;
            // 注意：CreateRect 的坐标系是「y 向下为正」，所以这里存的是向下偏移量。
            // 上限 690：五位数伤害的字宽约 70px，再往右会被画布右缘切掉。
            _floatPos[i] = new Vector2(Mathf.Clamp(x, 40f, 690f), Mathf.Clamp(y + lane * 15f, 12f, 450f));
            _floatVel[i] = new Vector2(GameRandom.Range(-8f, 8f), -58f);
            _floatTexts[i].rectTransform.anchoredPosition = new Vector2(_floatPos[i].x, -_floatPos[i].y);
        }

        private void TickFloats(float dt)
        {
            if (_floatTexts == null) return;
            for (int i = 0; i < FloatPoolSize; i++)
            {
                if (_floatLife[i] <= 0f) continue;

                _floatLife[i] -= dt;
                if (_floatLife[i] <= 0f)
                {
                    _floatTexts[i].text = string.Empty;
                    continue;
                }

                _floatPos[i] += _floatVel[i] * dt;
                _floatTexts[i].rectTransform.anchoredPosition = new Vector2(_floatPos[i].x, -_floatPos[i].y);

                Color c = _floatTexts[i].color;
                c.a = Mathf.Clamp01(_floatLife[i] / 0.4f);
                _floatTexts[i].color = c;
            }
        }

        // ==================== P2：Buff 栏 ====================

        private void RefreshBuffBar(PlayerData p, bool textTick)
        {
            if (_buffText == null || p == null) return;
            // 读数按 10Hz 走：逐帧重建只会在「状态：无」和真 Buff 之间反复拼同一份字符串。
            if (!textTick) return;

            BattleSystem b = _game.Battle;
            var sb = _buffSb;
            sb.Clear();

            if (b != null)
            {
                if (b.BerserkTimeLeft > 0f)
                {
                    sb.Append("<color=#FF9A3C>◆ 狂暴 ");
                    sb.Append(b.BerserkTimeLeft.ToString("0.0"));
                    sb.Append("s</color>    ");
                }
                if (b.NearDeathTimeLeft > 0f)
                {
                    sb.Append("<color=#FF6B9D>◆ 免死 ");
                    sb.Append(b.NearDeathTimeLeft.ToString("0.0"));
                    sb.Append("s</color>    ");
                }
            }

            if (p.shield > 0)
            {
                sb.Append("<color=#7FE8FF>◆ 护盾 ");
                sb.Append(p.shield);
                sb.Append("</color>    ");
            }

            // 这里以前还会把每个技能的冷却/就绪也拼进来，跟下方技能栏重复：
            // 空状态时整行是「重击 就绪 吸血鬼 就绪 …」，看着像坏掉的日志。
            _buffText.text = sb.Length == 0
                ? "<color=#5F6470>状态：无（技能冷却在下方技能栏）</color>"
                : sb.ToString();
        }

        // ==================== P2：实时 DPS ====================

        private void RefreshDps(float dt, bool textTick)
        {
            if (_dpsText == null) return;

            // 非战斗态把平滑值清掉：否则停在「等待下一只怪」或阵亡界面时，
            // 胶囊会一直显示上一场战斗的 DPS，看起来像还在输出。
            bool fighting = _game.CurrentState == GameController.State.Fighting;
            if (!fighting)
            {
                _dpsSmoothed = 0f;
                _dpsDamageAccum = 0f;
                _dpsWindowTime = 0f;
            }

            // DPS 胶囊整个隐藏：只清文本不够，胶囊底与边框还留在日志标题行上。
            // 日志展开时也一起藏起来——展开面板会盖住舞台顶栏，胶囊留在上面会压住标题。
            if (!GameSettings.ShowDps || _battleLogOpen)
            {
                if (_dpsPill != null && _dpsPill.gameObject.activeSelf) _dpsPill.gameObject.SetActive(false);
                return;
            }
            if (_dpsPill != null && !_dpsPill.gameObject.activeSelf) _dpsPill.gameObject.SetActive(true);

            // 积分必须逐帧做：这是这段时间窗里的真实跳数，和文本节拍不是一个东西。
            if (fighting && dt > 0f)
            {
                _dpsWindowTime += dt;
                if (_dpsWindowTime >= 0.4f)
                {
                    float instant = _dpsDamageAccum / _dpsWindowTime;
                    _dpsSmoothed = _dpsSmoothed <= 0f ? instant : Mathf.Lerp(_dpsSmoothed, instant, 0.55f);
                    _dpsDamageAccum = 0f;
                    _dpsWindowTime = 0f;
                }
            }

            if (!textTick) return;

            // 期望 DPS 是纯静态估算（每次要跑两遍 EquipmentScore.Of），
            // 只随「换了怪 / 角色变强」变化，4Hz 重算足够。
            bool hasExpect = fighting && _game.Monster != null;
            if (hasExpect)
            {
                _dpsExpectTimer += 0.1f;
                if (!ReferenceEquals(_dpsExpectMonster, _game.Monster) || _dpsExpectTimer >= 0.25f)
                {
                    _dpsExpectMonster = _game.Monster;
                    _dpsExpectTimer = 0f;
                    _dpsExpectValue = Mathf.RoundToInt(EquipmentScore.Dps(_game.Player, _game.Monster));
                }
            }
            else
            {
                _dpsExpectMonster = null;
            }

            int shown = _dpsSmoothed > 0.5f ? Mathf.RoundToInt(_dpsSmoothed) : -1;
            int expect = hasExpect ? _dpsExpectValue : -2;
            if (shown == _dpsShown && expect == _dpsExpect) return;
            _dpsShown = shown;
            _dpsExpect = expect;

            string expectText = hasExpect ? "    期望 " + _dpsExpectValue : string.Empty;
            _dpsText.text = shown >= 0
                ? "<color=#7C869D>实时 DPS</color>  <b>" + shown + "</b>" + expectText
                : "<color=#7C869D>实时 DPS</color>  <color=#5F6779>—</color>" + expectText;
        }

        // ==================== P2：日志 → 飘字 / DPS ====================

        private static readonly System.Text.RegularExpressions.Regex RxPlayerHit =
            new System.Text.RegularExpressions.Regex(@"^主角 → 怪物  (?:暴击 )?(\d+) 伤害");
        private static readonly System.Text.RegularExpressions.Regex RxSkillHit =
            new System.Text.RegularExpressions.Regex(@"^✦ [^！]+！(\d+) 伤害");

        /// <summary>把新增的日志行翻译成飘字；顺带累计主角造成的伤害给 DPS 用。</summary>
        private void ParseNewLogs()
        {
            if (_game == null) return;

            int count = _game.Log.Count;
            int total = _game.TotalLogEntries;

            // 用累计条数算新增：Log 被裁到上限后 count 不变，差值法会永远算出 0。
            int added = total - _lastParsedLogTotal;
            _lastParsedLogTotal = total;
            if (added <= 0) return;

            // 一帧内合并太多条（或刚换档清空日志）时只解析还留在缓冲里的这些。
            if (added > count) added = count;

            for (int i = count - added; i < count; i++)
            {
                if (i < 0) continue;
                string msg = _game.Log[i].message;
                if (string.IsNullOrEmpty(msg)) continue;

                var mPlayer = RxPlayerHit.Match(msg);
                if (mPlayer.Success)
                {
                    bool crit = msg.Contains("暴击 ");
                    int dmg = int.Parse(mPlayer.Groups[1].Value);
                    // 落在怪物血条右半段：血条中央是「260 / 2115」的读数，别压它。
                    // 普通命中按 1/3 概率丢弃——飘字是节奏感，不是逐条回执，日志里有全量。
                    if (crit || GameRandom.Value > 0.5f)
                    {
                        // 落点跟着怪物走：飘字层压在舞台上方，怪物躯干在层内的 y 约 250~290。
                        SpawnFloat(crit ? dmg + "!" : dmg.ToString(),
                            crit ? GameConfig.FloatCrit : GameConfig.TextNormal,
                            crit ? 19 : 14,
                            522f + GameRandom.Range(0f, 140f),
                            250f + GameRandom.Range(0f, 34f));
                    }
                    _dpsDamageAccum += dmg;
                    continue;
                }

                var mSkill = RxSkillHit.Match(msg);
                if (mSkill.Success)
                {
                    int dmg = int.Parse(mSkill.Groups[1].Value);
                    SpawnFloat(dmg.ToString(), GameConfig.FloatDamage, 17,
                        548f + GameRandom.Range(0f, 104f),
                        246f + GameRandom.Range(0f, 40f));
                    _dpsDamageAccum += dmg;
                    continue;
                }

                // 怪物打主角不飘字：日志里「怪物 → 主角 262 伤害」已经写明，
                // 左边血条也在掉，飘字只会多一处噪音。
            }
        }

    }
}

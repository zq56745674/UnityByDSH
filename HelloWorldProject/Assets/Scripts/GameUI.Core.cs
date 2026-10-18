using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.Events;

namespace AutoBattle
{
    public partial class GameUI : MonoBehaviour
    {
        // ==================== 刷新 ====================

        private void Update()
        {
            if (_game == null) return;

            // P2：飘字与实时 DPS 都需要按帧推进。
            // 飘字是纯表现层，**不吃速度倍率**：1.15 秒的寿命在 x4 下会被压到 0.29 秒，
            // 肉眼基本抓不到（挂机默认就是 x4，等于飘字白做）。DPS 窗口则按游戏时间走，
            // 这样和「期望 DPS」是同一个时间基准。
            float dt = Time.deltaTime * Mathf.Max(1f, _game.SpeedMultiplier);

            // 10Hz 文本节拍：倒计时、冷却秒数、Buff 剩余这些「一位小数」的读数，
            // 逐帧刷新与 10Hz 刷新肉眼看不出区别，但每帧拼一遍字符串的代价是实打实的。
            // 条状填充（血条、冷却条、经验条）仍然逐帧跟随，所以手感不会变钝。
            bool textTick = TickTextClock();

            TickFloats(Time.deltaTime);
            RefreshBuffBar(_game.Player, textTick);
            RefreshDps(dt, textTick);
            ParseNewLogs();
            TickConfirms();

            Refresh(textTick);
        }

        /// <summary>
        /// 破坏性操作的确认倒计时（重置 / 删档）：超时就把「确认…」的按钮文字收回原样，
        /// 免得玩家过一会儿回来点时已经不是同一个语义。
        /// </summary>
        private void TickConfirms()
        {
            float now = Time.unscaledTime;
            if (_resetConfirmUntil > 0f && now >= _resetConfirmUntil)
            {
                _resetConfirmUntil = 0f;
                if (_resetLabel != null) { _resetLabel.text = "重置"; _resetLabel.fontSize = 18; }
            }

            if (_deleteConfirmUntil > 0f && now >= _deleteConfirmUntil) CancelDeleteConfirm();
        }

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
            _floatVel[i] = new Vector2(UnityEngine.Random.Range(-8f, 8f), -58f);
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

            // DPS 胶囊整个隐藏：只清文本不够，胶囊底与边框还留在日志标题行上。
            if (!GameSettings.ShowDps)
            {
                if (_dpsPill != null && _dpsPill.gameObject.activeSelf) _dpsPill.gameObject.SetActive(false);
                return;
            }
            if (_dpsPill != null && !_dpsPill.gameObject.activeSelf) _dpsPill.gameObject.SetActive(true);

            // 积分必须逐帧做：这是这段时间窗里的真实跳数，和文本节拍不是一个东西。
            bool fighting = _game.CurrentState == GameController.State.Fighting;
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
                    if (crit || UnityEngine.Random.value > 0.5f)
                    {
                        SpawnFloat(crit ? dmg + "!" : dmg.ToString(),
                            crit ? GameConfig.FloatCrit : GameConfig.TextNormal,
                            crit ? 19 : 14,
                            470f + UnityEngine.Random.Range(0f, 190f),
                            20f + UnityEngine.Random.Range(0f, 44f));
                    }
                    _dpsDamageAccum += dmg;
                    continue;
                }

                var mSkill = RxSkillHit.Match(msg);
                if (mSkill.Success)
                {
                    int dmg = int.Parse(mSkill.Groups[1].Value);
                    SpawnFloat(dmg.ToString(), GameConfig.FloatDamage, 17,
                        660f + UnityEngine.Random.Range(0f, 70f),
                        20f + UnityEngine.Random.Range(0f, 44f));
                    _dpsDamageAccum += dmg;
                    continue;
                }

                // 怪物打主角不飘字：日志里「怪物 → 主角 262 伤害」已经写明，
                // 左边血条也在掉，飘字只会多一处噪音。
            }
        }

        // ==================== 刷新实现 ====================

        /// <summary>10Hz 文本节拍：返回 true 表示这一帧该重建「读数」类文本（倒计时、冷却秒数、Buff 剩余）。</summary>
        private bool TickTextClock()
        {
            int tenths = Mathf.FloorToInt(Time.unscaledTime * 10f);
            if (tenths == _lastTextTenths) return false;
            _lastTextTenths = tenths;
            TextRefreshCount++;
            return true;
        }

        /// <summary>
        /// 把一串「会进文字」的数值混成 64 位签名：签名没变就跳过整段字符串重建。
        /// 用签名而不是逐字段缓存，是因为要展示的字段会一直增减——加一行 Add 比再加一个缓存字段省事；
        /// 64 位空间下相邻两帧撞车的概率可以忽略（撞了也只是这一帧不重绘，下一次数值变化仍会重绘）。
        /// </summary>
        private struct Sig
        {
            private ulong _hash;

            public void Add(long value) { unchecked { _hash = (_hash ^ (ulong)value) * 1099511628211UL; } }
            public void Add(float value, float scale = 1000f) => Add((long)Mathf.RoundToInt(value * scale));
            public void Add(bool value) => Add(value ? 1L : 0L);
            public void Add(string value) => Add(value == null ? 0L : value.GetHashCode());
            public ulong Value => _hash;
        }

        /// <summary>10Hz 文本节拍上一次的拍号（unscaledTime × 10）。</summary>
        private int _lastTextTenths;

        /// <summary>诊断：静态区（签名门控）与文本区（10Hz 节拍）各重建过多少次。</summary>
        public int StaticRefreshCount { get; private set; }
        public int TextRefreshCount { get; private set; }

        private ulong _sigChar;
        private ulong _sigMonsterCard;
        private ulong _sigHud;
        private ulong _sigShopState;
        private ulong _sigSkillSlots;
        private int _cHp = -1, _cHpMax = -1, _cGold = -1, _cSaveSlot = -1, _cSpeedIndex = -1;
        private int _cMonHp = -1, _cMonMax = -1, _cMonShield = -1;
        private string _cMonTitle;
        private bool _cStartLabel;
        // 初始值和「非战斗态」的结果（-1 / -2）故意错开：否则第一帧就命中去重提前返回，胶囊永远空白。
        private int _dpsShown = int.MinValue, _dpsExpect = int.MinValue;
        private MonsterData _dpsExpectMonster;
        private int _dpsExpectValue;
        private float _dpsExpectTimer;

        /// <summary>
        /// 主刷新。拆成四段，让「重建代价」和「真的会变的频率」对齐：
        ///   1. <see cref="RefreshCharacter"/>：等级/属性/装备明细——只在数值变化时重建（签名门控）；
        ///   2. <see cref="RefreshVitals"/>：血条与生命读数——读数真变了才拼串，条永远逐帧跟随；
        ///   3. <see cref="RefreshMonsterCard"/>：怪物标题/属性静态，血量按值、复活倒计时按 10Hz；
        ///   4. <see cref="RefreshHud"/> / <see cref="RefreshPanels"/>：金币、统计行、按钮态与六个浮层。
        /// 以前这些全塞在一个每帧无条件执行的方法里：三十来条插值串 + 两遍装备评分，
        /// 挂机时每秒白烧六十次，而画面上一分钟都没变。
        /// </summary>
        private void Refresh(bool textTick)
        {
            // RefreshStagePanel 会自己判空，这里以前没有：一旦 Player 为 null，
            // 下面第一行取值就会每帧抛 NRE。两处空值策略必须一致。
            if (_game == null || _game.Player == null) return;
            PlayerData p = _game.Player;

            RefreshCharacter(p);
            // 装备行自己带缓存（含「显示评分」开关）：不能挂在 RefreshCharacter 的签名门控里，
            // 否则只改设置、不换装备时签名不变，评分开关看起来没生效。
            RefreshEquipmentLines(p);
            RefreshVitals(p);
            RefreshMonsterCard(textTick);
            RefreshHud(p);
            RefreshPanels(p, textTick);
            RefreshLog();
        }

        /// <summary>属性区：升级、换装、强化、获得技能才会动，其余帧整段跳过。</summary>
        private void RefreshCharacter(PlayerData p)
        {
            var sig = new Sig();
            sig.Add(p.level);
            sig.Add(p.paragonLevel);
            sig.Add(p.IsMaxLevel);
            sig.Add(p.exp);
            sig.Add(p.ExpToNextLevel);
            sig.Add(p.paragonExp);
            sig.Add(p.ParagonExpToNext);
            sig.Add(p.OwnedSkillCount);
            sig.Add(p.Attack);
            sig.Add(p.BaseAttack);
            sig.Add(p.weapon != null ? p.weapon.EnhancedAttack : 0);
            sig.Add(p.Defense);
            sig.Add(p.BaseDefense);
            sig.Add(p.armor != null ? p.armor.EnhancedDefense : 0);
            sig.Add(p.Speed);
            sig.Add(p.CritChance, 10000f);
            sig.Add(p.CritDamage, 10000f);
            sig.Add(p.ArmorPenetration, 10000f);
            if (sig.Value == _sigChar) return;
            _sigChar = sig.Value;
            StaticRefreshCount++;

            // 关卡制（步骤 4a/4b）：等级行只报等级，关卡与关卡怪级挪到右边的选关按钮上
            // （按钮本身就是入口，一举两得）。
            string lvSuffix = p.paragonLevel > 0
                ? $"   <color=#8C99B0>巅峰</color> <color=#C9B6FF><b>{p.paragonLevel}</b></color>"
                : (p.IsMaxLevel ? "   <color=#8C99B0>满级</color>" : string.Empty);
            _levelText.text = $"<color=#8C99B0>等级</color> <size=24><b>{p.level}</b></size>{lvSuffix}";

            // 「装备」这一项用强化后的主属性：不然强化完总攻击涨了、括号里的分解却没动，看着像算错了。
            _atkText.text = $"<color=#8C99B0>攻击</color>   <b>{p.Attack}</b>   <color=#5F6779>基础 {p.BaseAttack} + 装备 {(p.weapon != null ? p.weapon.EnhancedAttack : 0)}</color>";
            _defText.text = $"<color=#8C99B0>防御</color>   <b>{p.Defense}</b>   <color=#5F6779>基础 {p.BaseDefense} + 装备 {(p.armor != null ? p.armor.EnhancedDefense : 0)}</color>";
            _spdText.text = $"<color=#8C99B0>速度</color>   <b>{p.Speed}</b>   <color=#5F6779>攻击间隔 {BattleSystem.PlayerAttackInterval(p):0.00}s</color>";
            _critText.text = $"<color=#8C99B0>暴击</color> <b>{p.CritChance * 100f:0.#}%</b>    <color=#8C99B0>暴伤</color> <b>{p.CritDamage * 100f:0}%</b>    <color=#8C99B0>穿透</color> <b>{p.ArmorPenetration * 100f:0.#}%</b>";

            if (p.IsMaxLevel)
            {
                _expText.text = p.IsMaxParagon
                    ? "<color=#8C99B0>巅峰</color>   已达上限"
                    : $"<color=#8C99B0>巅峰经验</color>   <b>{p.paragonExp}</b><color=#5F6779> / {p.ParagonExpToNext}</color>   <color=#8C99B0>技能</color> {p.OwnedSkillCount}/{SkillCatalog.All.Length}";
            }
            else
            {
                _expText.text = $"<color=#8C99B0>经验</color>   <b>{p.exp}</b><color=#5F6779> / {p.ExpToNextLevel}</color>";
            }

        }

        /// <summary>生命读数只在真掉血/升级时重建；三条横条逐帧跟随。</summary>
        private void RefreshVitals(PlayerData p)
        {
            if (p.currentHp != _cHp || p.MaxHp != _cHpMax)
            {
                _cHp = p.currentHp;
                _cHpMax = p.MaxHp;
                _hpText.text = $"<color=#8C99B0>生命</color>   <b>{p.currentHp}</b><color=#5F6779> / {p.MaxHp}</color>";
            }

            SetBar(_hpFill, p.HpProgress);
            SetBar(_expFill, p.ExpProgress);
        }

        /// <summary>怪物卡：标题与属性行随「换了一只怪」重建，血量数字按值，空场倒计时按 10Hz。</summary>
        private void RefreshMonsterCard(bool textTick)
        {
            MonsterData m = _game.Monster;
            if (m != null)
            {
                // 用身份数值做签名而不是比引用：这几项相同基本就是同一只怪，
                // 掉落与词缀都在生成时定死，打到一半不会变。
                var sig = new Sig();
                sig.Add(m.level);
                sig.Add((int)m.tier);
                sig.Add(m.maxHp);
                sig.Add(m.attack);
                sig.Add(m.defense);
                sig.Add(m.speed, 100f);
                sig.Add(m.HasAffix ? m.AffixName : string.Empty);
                if (sig.Value != _sigMonsterCard)
                {
                    _sigMonsterCard = sig.Value;
                    StaticRefreshCount++;

                    // 缓存要和写出去的内容同步：空场分支靠 _cMonTitle 判重，
                    // 这里不同步的话「怪物→空场」会被判成没变化，标题就一直停在上一只怪的名字上。
                    _cMonTitle = $"<color=#FFC857>⚔</color>  {m.Title}";
                    _monsterTitle.text = _cMonTitle;

                    // 精英金框、Boss 红框，普通怪无边框。
                    _monsterHpBorder.color = MonsterBorderColor(m.tier);
                    _monsterHpTrack.color = GameConfig.BarTrackHp;

                    string affixText = m.HasAffix ? $"    词缀：{m.AffixName}" : "";
                    _monsterStatText.text =
                        $"攻击 {m.attack}    防御 {m.defense}    速度 {m.speed:0.#}    攻击间隔 {GameConfig.AttackInterval(m.speed):0.00}s\n" +
                        $"暴击率 {m.critChance * 100f:0.#}%    暴击伤害 {m.critDamage * 100f:0}%{affixText}";
                }

                if (m.currentHp != _cMonHp || m.maxHp != _cMonMax || m.shield != _cMonShield)
                {
                    _cMonHp = m.currentHp;
                    _cMonMax = m.maxHp;
                    _cMonShield = m.shield;
                    _monsterHpText.text = m.shield > 0
                        ? $"<b>{m.currentHp}</b><color=#E8D2D2> / {m.maxHp}</color>   <color=#7FE8FF>护盾 {m.shield}</color>"
                        : $"<b>{m.currentHp}</b><color=#E8D2D2> / {m.maxHp}</color>";
                }

                SetBar(_monsterHpFill, m.HpProgress);
                return;
            }

            // ---- 空场 ----
            string title = _game.CurrentState == GameController.State.Reviving
                ? "— 主角阵亡，等待自动复活 —"
                : "— 等待下一只怪物 —";
            if (!string.Equals(title, _cMonTitle))
            {
                _cMonTitle = title;
                StaticRefreshCount++;
                _monsterTitle.text = title;
            }

            if (_sigMonsterCard != 0)
            {
                _sigMonsterCard = 0;
                _cMonHp = _cMonMax = _cMonShield = -1;
                // 空场时属性行留白：一行解释性文字反而像开发备注。
                _monsterStatText.text = "";
                // 空场时血槽换成中性灰：整条暗红看起来像「满血怪」，容易误读。
                _monsterHpTrack.color = GameConfig.RowBg;
                _monsterHpBorder.color = GameConfig.Clear;
            }

            if (textTick)
            {
                float respawn = _game.RespawnCountdown;
                _monsterHpText.text = respawn > 0.05f
                    ? $"<color=#8C99B0>下一只怪物</color>  <b>{respawn:0.0}s</b>"
                    : "";
            }

            SetBar(_monsterHpFill, 0f);
        }

        /// <summary>金币、统计行、开始/速度按钮、存档槽文字与浮层遮罩。</summary>
        private void RefreshHud(PlayerData p)
        {
            if (p.gold != _cGold)
            {
                _cGold = p.gold;
                _goldText.text = $"<color=#8C99B0>金币</color>   <b>{p.gold}</b>";
            }

            var sig = new Sig();
            sig.Add(_game.TotalKills);
            sig.Add(_game.EliteKills);
            sig.Add(_game.BossKills);
            sig.Add(_game.TotalDrops);
            sig.Add(_game.TotalEquipped);
            sig.Add(_game.TotalSold);
            sig.Add(_game.TotalDeaths);
            sig.Add(_game.TotalGoldEarned);
            sig.Add((int)_game.CurrentState);
            sig.Add(p.paragonLevel);
            sig.Add(_game.SpeedMultiplier, 100f);
            if (sig.Value != _sigHud)
            {
                _sigHud = sig.Value;
                StaticRefreshCount++;

                string paragonNote = p.paragonLevel > 0 ? $"    巅峰 {p.paragonLevel}" : "";
                // 压成 2 行：3 行时统计区要占 47px，会和上方的金币行抢位置（曾经重叠）。
                _statText.text =
                    $"击杀 {_game.TotalKills}（精英 {_game.EliteKills} / BOSS {_game.BossKills}）    掉落 {_game.TotalDrops}    换装 {_game.TotalEquipped}    卖出 {_game.TotalSold}\n" +
                    $"阵亡 {_game.TotalDeaths}    累计 {_game.TotalGoldEarned} 金币    状态 {StateName(_game.CurrentState)}    速度 x{_game.SpeedMultiplier:0}{paragonNote}";
            }

            // 存档按钮上的槽号：跟当前槽走，切完槽立刻反映出来。
            if (_game.SaveSlot != _cSaveSlot)
            {
                _cSaveSlot = _game.SaveSlot;
                if (_saveButtonLabel != null) _saveButtonLabel.text = $"存档 {_game.SaveSlot + 1}";
            }

            if (_game.IsRunning != _cStartLabel)
            {
                _cStartLabel = _game.IsRunning;
                _startButtonLabel.text = _game.IsRunning ? "暂停" : "开始";
            }

            if (_game.SpeedIndex != _cSpeedIndex)
            {
                _cSpeedIndex = _game.SpeedIndex;
                for (int i = 0; i < _speedButtons.Length; i++)
                {
                    bool active = i == _game.SpeedIndex;
                    _speedButtons[i].image.color = active
                        ? GameConfig.BtnSpeedActive
                        : GameConfig.BtnNeutral;
                    _speedLabels[i].color = active ? Color.white : GameConfig.TextMuted;
                }
            }

            // 任意浮层打开时压一层遮罩，视线自然落到浮层上。
            if (_modalDim != null)
            {
                bool modalOpen = IsOpen(_skillPanel) || IsOpen(_skillChoicePanel) || IsOpen(_stagePanel)
                    || IsOpen(_savePanel) || IsOpen(_enhancePanel) || IsOpen(_reforgePanel)
                    || IsOpen(_settingsPanel) || IsOpen(_comparePanel) || IsOpen(_guideRoot);
                if (_modalDim.gameObject.activeSelf != modalOpen) _modalDim.gameObject.SetActive(modalOpen);
            }
        }

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
            RefreshComparePanel();
            RefreshGuide();
            RefreshBanner();
        }

        /// <summary>只有日志确实变化时才重建文本，避免每帧拼接大字符串。</summary>
        private void RefreshLog()
        {
            if (_game.LogVersion == _lastLogVersion) return;
            _lastLogVersion = _game.LogVersion;

            var sb = new System.Text.StringBuilder(4096);
            int count = _game.Log.Count;
            int from = Mathf.Max(0, count - MaxVisibleLogLines);

            // 倒序：最新的一条在最上面。
            for (int i = count - 1; i >= from; i--)
            {
                LogEntry e = _game.Log[i];
                sb.Append("<color=#");
                sb.Append(ColorUtility.ToHtmlStringRGB(e.color));
                sb.Append('>');
                sb.Append(e.message);
                sb.Append("</color>\n");
            }

            _logText.text = sb.ToString();
        }

        /// <summary>
        /// 装备两行文字。评分依赖角色属性，但只随「装备 / 等级 / 巅峰 / 强化等级」变化，
        /// 所以这几个值没动就不重算——否则每帧会白跑两次 EquipmentScore.Of（6 次改玩家字段）。
        /// </summary>
        private void RefreshEquipmentLines(PlayerData p)
        {
            int weaponEnhance = p.weapon != null ? p.weapon.enhanceLevel : -1;
            int armorEnhance = p.armor != null ? p.armor.enhanceLevel : -1;
            int weaponReforge = p.weapon != null ? p.weapon.reforgeCount : -1;
            int armorReforge = p.armor != null ? p.armor.reforgeCount : -1;
            bool showScore = GameSettings.ShowScore;

            // 被动技能加成会进评分公式，但换技能不改装备引用，必须把技能状态也放进缓存键。
            bool skillChanged = _scoreCachedSkillMask != p.ownedSkillMask;
            for (int i = 0; i < _scoreCachedSkillSlots.Length; i++)
            {
                if (_scoreCachedSkillSlots[i] != p.GetSkillSlot(i))
                {
                    skillChanged = true;
                    break;
                }
            }

            if (!ReferenceEquals(_scoreCachedWeapon, p.weapon)
                || !ReferenceEquals(_scoreCachedArmor, p.armor)
                || _scoreCachedLevel != p.level
                || _scoreCachedParagon != p.paragonLevel
                || _scoreCachedWeaponEnhance != weaponEnhance
                || _scoreCachedArmorEnhance != armorEnhance
                || _scoreCachedWeaponReforge != weaponReforge
                || _scoreCachedArmorReforge != armorReforge
                || _scoreCachedShowScore != showScore
                || skillChanged)
            {
                _scoreCachedWeapon = p.weapon;
                _scoreCachedArmor = p.armor;
                _scoreCachedLevel = p.level;
                _scoreCachedParagon = p.paragonLevel;
                _scoreCachedWeaponEnhance = weaponEnhance;
                _scoreCachedArmorEnhance = armorEnhance;
                _scoreCachedWeaponReforge = weaponReforge;
                _scoreCachedArmorReforge = armorReforge;
                _scoreCachedShowScore = showScore;
                _scoreCachedSkillMask = p.ownedSkillMask;
                for (int i = 0; i < _scoreCachedSkillSlots.Length; i++)
                {
                    _scoreCachedSkillSlots[i] = p.GetSkillSlot(i);
                }
                _scoreWeaponText = FormatEquipment("武器", p.weapon);
                _scoreArmorText = FormatEquipment("衣服", p.armor);
                _scoreWeaponMeta = FormatEquipmentMeta(p.weapon);
                _scoreArmorMeta = FormatEquipmentMeta(p.armor);
            }
            _weaponText.text = _scoreWeaponText;
            _armorText.text = _scoreArmorText;
            _weaponMetaText.text = _scoreWeaponMeta;
            _armorMetaText.text = _scoreArmorMeta;
        }

        private string FormatEquipment(string slotName, Equipment e)
        {
            if (e == null) return $"<color=#5F6779>{slotName}：空</color>";

            string hex = ColorUtility.ToHtmlStringRGB(e.QualityColor);
            string affixLine = string.IsNullOrEmpty(e.AffixLine)
                ? string.Empty
                : "\n    <color=#9AA6BD>" + e.AffixLine + "</color>";
            // 评分口径见 EquipmentScore：必须带上角色当前属性算，所以这里要 _game。
            float score = _game != null && _game.Player != null ? EquipmentScore.Of(_game.Player, e, e.slot) : 0f;
            // 标题行：品质色只给名字，倍率与评分退到灰阶，眼睛先看到「这是什么品质」。
            // 明细压到 13 号：装备词条一行能到「暴击率 +7.8% 暴击伤害 +112%」，15 号必然折行压到下一件装备。
            return $"<color=#{hex}><b>{e.Title}</b></color>\n<size=13>    <color=#C6CEE0>{e.StatLine}</color>{affixLine}</size>";
        }

        /// <summary>装备标题行右列：倍率 + 评分（评分带角色当前属性，见 EquipmentScore）。</summary>
        private string FormatEquipmentMeta(Equipment e)
        {
            if (e == null) return string.Empty;

            // 评分可以关：有人只想知道「品质倍率」，不想被一个抽象分数牵着走。
            if (!GameSettings.ShowScore) return $"×{e.qualityMultiplier:0.00}";

            float score = _game != null && _game.Player != null ? EquipmentScore.Of(_game.Player, e, e.slot) : 0f;
            return $"×{e.qualityMultiplier:0.00}   <color=#8C99B0>评分</color> <b>{score:0}</b>";
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

        // ==================== 控件工厂 ====================

        private static RectTransform CreateRect(Transform parent, string name, float x, float y, float w, float h)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, -y); // y 向下为正，写起来更直观
            rt.sizeDelta = new Vector2(w, h);
            return rt;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private static Image CreateImage(Transform parent, string name, float x, float y, float w, float h, Color color, bool raycast = false)
        {
            RectTransform rt = CreateRect(parent, name, x, y, w, h);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = raycast;
            return img;
        }

        /// <summary>
        /// 浮层面板底板。blockClicks=true 时底板自己吃点击：UGUI 的射线只会落在最上层的
        /// raycastTarget 上，底板如果不吃，点面板空白处会穿透到下层按钮（选关面板曾因此
        /// 一点空白就误买药剂、误扣金币）。
        /// </summary>
        private RectTransform CreatePanel(Transform parent, string name, float x, float y, float w, float h,
            Color color, bool blockClicks = false)
        {
            Image img = CreateImage(parent, name, x, y, w, h, color, blockClicks);
            Round(img, RadiusPanel);
            AddBorder(img, RadiusPanel, GameConfig.Border);
            return img.rectTransform;
        }

        private Text CreateText(Transform parent, string name, string content, float x, float y, float w, float h,
            int fontSize, Color color, TextAnchor anchor, FontStyle style = FontStyle.Normal)
        {
            RectTransform rt = CreateRect(parent, name, x, y, w, h);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = _font;
            t.text = content;
            t.fontSize = fontSize;
            t.color = color;
            t.alignment = anchor;
            t.fontStyle = style;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.supportRichText = true;
            t.raycastTarget = false;
            return t;
        }

        /// <summary>背景条 + 填充条；返回填充条供刷新时改比例。</summary>
        private Image CreateBar(Transform parent, string name, float x, float y, float w, float h, Color bgColor, Color fillColor)
        {
            Image bg = CreateImage(parent, name, x, y, w, h, bgColor);
            Round(bg, RadiusBar);
            Image fill = CreateImage(bg.rectTransform, name + "Fill", 0, 0, w, h, fillColor);
            Round(fill, RadiusBar);
            Stretch(fill.rectTransform);
            AddBarGloss(fill);
            return fill;
        }

        private Button CreateButton(Transform parent, string name, string label, float x, float y, float w, float h,
            Color bgColor, out Text labelText)
        {
            Image img = CreateImage(parent, name, x, y, w, h, bgColor, true);
            Round(img, RadiusButton);
            AddBorder(img, RadiusButton, new Color(1f, 1f, 1f, 0.10f));
            var btn = img.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;

            // 注意：这几个颜色是**乘数**，乘在 Image.color（也就是 bgColor）上。
            // 原来写成 1.18f * bgColor，对深色按钮等于又乘一遍暗色，
            // 悬停/按下时整块掉到接近纯黑——x4 停在鼠标下面时就是这么变黑的。
            var colors = btn.colors;
            colors.highlightedColor = new Color(1.30f, 1.30f, 1.30f, 1f);
            colors.pressedColor = new Color(0.78f, 0.78f, 0.78f, 1f);
            colors.selectedColor = new Color(1.10f, 1.10f, 1.10f, 1f);
            colors.disabledColor = new Color(1f, 1f, 1f, 0.45f);
            colors.fadeDuration = 0.08f;
            btn.colors = colors;

            labelText = CreateText(img.rectTransform, name + "Label", label, 0, 0, w, h, 17, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            Stretch(labelText.rectTransform);

            return btn;
        }
    }
}

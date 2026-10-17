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
            TickFloats(Time.deltaTime);
            RefreshBuffBar(_game.Player);
            RefreshDps(dt);
            ParseNewLogs();
            TickConfirms();

            Refresh();
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

        /// <summary>弹出一条飘字。x 是水平落点（相对飘字层左边缘）。</summary>
        private void SpawnFloat(string label, Color color, int size, float x)
        {
            if (_floatTexts == null) return;
            int i = _floatCursor;
            _floatCursor = (_floatCursor + 1) % FloatPoolSize;

            Text txt = _floatTexts[i];
            txt.text = label;
            txt.color = color;
            txt.fontSize = size;

            _floatLife[i] = 1.15f;
            // 注意：CreateRect 的坐标系是「y 向下为正」，所以这里存的是向下偏移量。
            _floatPos[i] = new Vector2(Mathf.Clamp(x, 60f, 700f), 128f + UnityEngine.Random.Range(0f, 108f));
            _floatVel[i] = new Vector2(UnityEngine.Random.Range(-14f, 14f), -54f);
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
                c.a = Mathf.Clamp01(_floatLife[i] / 0.5f);
                _floatTexts[i].color = c;
            }
        }

        // ==================== P2：Buff 栏 ====================

        private void RefreshBuffBar(PlayerData p)
        {
            if (_buffText == null || p == null) return;

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

            for (int slot = 0; slot < GameConfig.SkillSlotsPerKind; slot++)
            {
                int id = p.GetSkillSlot(slot);
                // 存档被写坏时槽位里可能是个越界 id：SkillCatalog.Get 是裸下标，这里必须先兜住，
                // 否则每帧一次 IndexOutOfRange（Panels 那侧本来就有这个上界检查）。
                if (id < 0 || id >= SkillCatalog.All.Length) continue;

                SkillDef def = SkillCatalog.Get((SkillId)id);
                float cd = _game.SkillCooldown(slot);
                if (cd > 0f)
                {
                    sb.Append("<color=#8A8F9A>");
                    sb.Append(def.name);
                    sb.Append(' ');
                    sb.Append(cd.ToString("0.0"));
                    sb.Append("s</color>    ");
                }
                else
                {
                    sb.Append("<color=#9CFF9C>");
                    sb.Append(def.name);
                    sb.Append(" 就绪</color>    ");
                }
            }

            _buffText.text = sb.Length == 0 ? "<color=#5F6470>（无状态）</color>" : sb.ToString();
        }

        // ==================== P2：实时 DPS ====================

        private void RefreshDps(float dt)
        {
            if (_dpsText == null) return;

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

            string expect = string.Empty;
            if (fighting && _game.Monster != null)
            {
                expect = "    期望 " + EquipmentScore.Dps(_game.Player, _game.Monster).ToString("0");
            }

            _dpsText.text = _dpsSmoothed > 0.5f
                ? "实时 DPS " + _dpsSmoothed.ToString("0") + expect
                : "实时 DPS —" + expect;
        }

        // ==================== P2：日志 → 飘字 / DPS ====================

        private static readonly System.Text.RegularExpressions.Regex RxPlayerHit =
            new System.Text.RegularExpressions.Regex(@"^主角 → 怪物  (?:暴击 )?(\d+) 伤害");
        private static readonly System.Text.RegularExpressions.Regex RxMonsterHit =
            new System.Text.RegularExpressions.Regex(@"^怪物 → 主角  (?:暴击 )?(\d+) 伤害");
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
                    SpawnFloat(crit ? dmg + "!" : dmg.ToString(),
                        crit ? GameConfig.FloatCrit : GameConfig.TextNormal,
                        crit ? 26 : 19,
                        200f + UnityEngine.Random.Range(-70f, 70f));
                    _dpsDamageAccum += dmg;
                    continue;
                }

                var mSkill = RxSkillHit.Match(msg);
                if (mSkill.Success)
                {
                    int dmg = int.Parse(mSkill.Groups[1].Value);
                    SpawnFloat(dmg.ToString(), GameConfig.FloatDamage, 24,
                        420f + UnityEngine.Random.Range(-70f, 70f));
                    _dpsDamageAccum += dmg;
                    continue;
                }

                var mMonster = RxMonsterHit.Match(msg);
                if (mMonster.Success)
                {
                    bool crit = msg.Contains("暴击 ");
                    int dmg = int.Parse(mMonster.Groups[1].Value);
                    SpawnFloat("-" + dmg, crit ? GameConfig.FloatTakenCrit : GameConfig.FloatTaken,
                        crit ? 24 : 18, 640f + UnityEngine.Random.Range(-70f, 70f));
                }
            }
        }

        private void Refresh()
        {
            // RefreshStagePanel 会自己判空，这里以前没有：一旦 Player 为 null，
            // 下面第一行取值就会每帧抛 NRE。两处空值策略必须一致。
            if (_game == null || _game.Player == null) return;
            PlayerData p = _game.Player;

            // ---- 角色属性 ----
            // 关卡制（步骤 4a/4b）：等级行只报等级，关卡与关卡怪级挪到右边的选关按钮上
            // （按钮本身就是入口，一举两得）。
            _levelText.text = p.paragonLevel > 0
                ? $"等级 {p.level}    巅峰 {p.paragonLevel}"
                : (p.IsMaxLevel ? $"等级 {p.level}（满级）" : $"等级 {p.level}");
            _hpText.text = $"生命  {p.currentHp} / {p.MaxHp}";
            SetBar(_hpFill, p.HpProgress);

            // 「装备」这一项用强化后的主属性：不然强化完总攻击涨了、括号里的分解却没动，看着像算错了。
            _atkText.text = $"攻击  {p.Attack}    (基础 {p.BaseAttack} + 装备 {(p.weapon != null ? p.weapon.EnhancedAttack : 0)})";
            _defText.text = $"防御  {p.Defense}    (基础 {p.BaseDefense} + 装备 {(p.armor != null ? p.armor.EnhancedDefense : 0)})";
            _spdText.text = $"速度  {p.Speed}    攻击间隔 {BattleSystem.PlayerAttackInterval(p):0.00}s";
            _critText.text = $"暴击  {p.CritChance * 100f:0.#}%    暴击伤害  {p.CritDamage * 100f:0}%    穿透  {p.ArmorPenetration * 100f:0.#}%";

            if (p.IsMaxLevel)
            {
                _expText.text = p.IsMaxParagon
                    ? "巅峰  已达上限"
                    : $"巅峰经验  {p.paragonExp} / {p.ParagonExpToNext}    已获得技能 {p.OwnedSkillCount}/{SkillCatalog.All.Length}";
            }
            else
            {
                _expText.text = $"经验  {p.exp} / {p.ExpToNextLevel}";
            }
            SetBar(_expFill, p.ExpProgress);

            RefreshEquipmentLines(p);

            _goldText.text = $"金币  {p.gold}";

            string paragonNote = p.paragonLevel > 0 ? $"    巅峰 {p.paragonLevel}" : "";
            // 压成 2 行：3 行时统计区要占 47px，会和上方的金币行抢位置（曾经重叠）。
            _statText.text =
                $"击杀 {_game.TotalKills}（精英 {_game.EliteKills} / BOSS {_game.BossKills}）    掉落 {_game.TotalDrops}    换装 {_game.TotalEquipped}    卖出 {_game.TotalSold}\n" +
                $"阵亡 {_game.TotalDeaths}    累计 {_game.TotalGoldEarned} 金币    状态 {StateName(_game.CurrentState)}    速度 x{_game.SpeedMultiplier:0}{paragonNote}";

            RefreshShop(p);
            RefreshSkillBar(p);
            RefreshSkillPanel();
            RefreshSkillChoice();
            RefreshStagePanel();
            RefreshSavePanel();
            RefreshEnhancePanel();
            RefreshBanner();

            // 存档按钮上的槽号：跟当前槽走，切完槽立刻反映出来。
            if (_saveButtonLabel != null)
            {
                _saveButtonLabel.text = $"存档 {_game.SaveSlot + 1}";
            }

            // ---- 怪物 ----
            MonsterData m = _game.Monster;
            if (m != null)
            {
                _monsterTitle.text = $"⚔ {m.Title}";
                _monsterHpText.text = m.shield > 0
                    ? $"{m.currentHp} / {m.maxHp}   （护盾 {m.shield}）"
                    : $"{m.currentHp} / {m.maxHp}";
                SetBar(_monsterHpFill, m.HpProgress);

                // 精英金框、Boss 红框，普通怪无边框。
                _monsterHpBorder.color = MonsterBorderColor(m.tier);

                string affixText = m.HasAffix ? $"    词缀：{m.AffixName}" : "";
                _monsterStatText.text =
                    $"攻击 {m.attack}    防御 {m.defense}    速度 {m.speed:0.#}    攻击间隔 {GameConfig.AttackInterval(m.speed):0.00}s\n" +
                    $"暴击率 {m.critChance * 100f:0.#}%    暴击伤害 {m.critDamage * 100f:0}%{affixText}";
            }
            else
            {
                _monsterTitle.text = _game.CurrentState == GameController.State.Reviving
                    ? "— 主角阵亡，等待自动复活 —"
                    : "— 等待下一只怪物 —";
                _monsterHpText.text = "";
                SetBar(_monsterHpFill, 0f);
                _monsterHpBorder.color = GameConfig.Clear;
                _monsterStatText.text = "";
            }

            // ---- 按钮状态 ----
            _startButtonLabel.text = _game.IsRunning ? "暂停" : "开始";

            for (int i = 0; i < _speedButtons.Length; i++)
            {
                bool active = i == _game.SpeedIndex;
                _speedButtons[i].image.color = active
                    ? GameConfig.BtnSpeedActive
                    : GameConfig.BtnNeutral;
                _speedLabels[i].color = active ? Color.white : GameConfig.TextMuted;
            }

            RefreshLog();
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

            if (!ReferenceEquals(_scoreCachedWeapon, p.weapon)
                || !ReferenceEquals(_scoreCachedArmor, p.armor)
                || _scoreCachedLevel != p.level
                || _scoreCachedParagon != p.paragonLevel
                || _scoreCachedWeaponEnhance != weaponEnhance
                || _scoreCachedArmorEnhance != armorEnhance)
            {
                _scoreCachedWeapon = p.weapon;
                _scoreCachedArmor = p.armor;
                _scoreCachedLevel = p.level;
                _scoreCachedParagon = p.paragonLevel;
                _scoreCachedWeaponEnhance = weaponEnhance;
                _scoreCachedArmorEnhance = armorEnhance;
                _scoreWeaponText = FormatEquipment("武器", p.weapon);
                _scoreArmorText = FormatEquipment("衣服", p.armor);
            }
            _weaponText.text = _scoreWeaponText;
            _armorText.text = _scoreArmorText;
        }

        private string FormatEquipment(string slotName, Equipment e)
        {
            if (e == null) return $"<color=#808080>{slotName}：空</color>";

            string hex = ColorUtility.ToHtmlStringRGB(e.QualityColor);
            string affixLine = string.IsNullOrEmpty(e.AffixLine) ? string.Empty : "\n    " + e.AffixLine;
            // 评分口径见 EquipmentScore：必须带上角色当前属性算，所以这里要 _game。
            float score = _game != null && _game.Player != null ? EquipmentScore.Of(_game.Player, e, e.slot) : 0f;
            // 评分放在标题行：主属性行本身已经很长，再挂评分会折行。
            return $"<color=#{hex}>{e.Title}    {e.MultiplierText}    评分 {score:0}\n    {e.StatLine}{affixLine}</color>";
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

        private static string StateName(GameController.State s)
        {
            switch (s)
            {
                case GameController.State.Fighting: return "战斗中";
                case GameController.State.WaitingRespawn: return "等待重生";
                case GameController.State.Reviving: return "复活中";
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
            return CreateImage(parent, name, x, y, w, h, color, blockClicks).rectTransform;
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
            Image fill = CreateImage(bg.rectTransform, name + "Fill", 0, 0, w, h, fillColor);
            Stretch(fill.rectTransform);
            return fill;
        }

        private Button CreateButton(Transform parent, string name, string label, float x, float y, float w, float h,
            Color bgColor, out Text labelText)
        {
            Image img = CreateImage(parent, name, x, y, w, h, bgColor, true);
            var btn = img.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;

            var colors = btn.colors;
            colors.highlightedColor = new Color(1.1f * bgColor.r, 1.1f * bgColor.g, 1.1f * bgColor.b, 1f);
            colors.pressedColor = new Color(0.8f * bgColor.r, 0.8f * bgColor.g, 0.8f * bgColor.b, 1f);
            btn.colors = colors;

            labelText = CreateText(img.rectTransform, name + "Label", label, 0, 0, w, h, 18, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            Stretch(labelText.rectTransform);

            return btn;
        }
    }
}

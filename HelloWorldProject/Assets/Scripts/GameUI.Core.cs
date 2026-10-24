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

            // 键盘快捷键先处理：Esc 关浮层、空格暂停、1/2/4 变速、R 重置（见文件末尾）。
            TickShortcuts();

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
            TickHeroPortrait();
            RefreshBuffBar(_game.Player, textTick);
            RefreshDps(dt, textTick);
            ParseNewLogs();
            TickConfirms();
            // 舞台用真实时间：呼吸和抖动不能跟着 x4 一起抽搐。
            TickBattleStage(Time.deltaTime);

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
                if (_resetLabel != null) { _resetLabel.text = "重置"; _resetLabel.fontSize = 15; }
            }

            if (_deleteConfirmUntil > 0f && now >= _deleteConfirmUntil) CancelDeleteConfirm();
        }

        // ==================== 键盘快捷键（2026-10-23 新增） ====================

        /// <summary>
        /// 任意浮层开着：这时除了 Esc 之外快捷键一律不响应，否则空格会在设置面板底下把战斗跑起来。
        /// </summary>
        private bool AnyOverlayOpen()
        {
            if (_game == null) return false;
            return _game.SettingsOpen || _game.MusicPanelOpen || _game.StagePanelOpen || _game.SkillPanelOpen
                || _game.SavePanelOpen || _game.EnhancePanelOpen || _game.ReforgePanelOpen
                || _game.CompareOpen || _game.AchievementPanelOpen || _game.EndlessPanelOpen
                || _game.RebirthPanelOpen || _game.GuideOpen || _game.EndlessResultOpen
                || _game.AboutOpen || _game.DisplayOpen;
        }

        /// <summary>
        /// Esc：按「最上层」关一个浮层；一个浮层都没有时当作暂停 / 继续。
        /// 三选一、无尽结算、新手引导不代关——这三层必须由玩家自己做选择。
        /// </summary>
        private void OnEscapeShortcut()
        {
            HideTooltip();
            if (_game == null) return;

            if (_game.PendingSkillPicks > 0 || _game.CurrentState == GameController.State.SkillChoice
                || _game.EndlessResultOpen || _game.GuideOpen)
                return;

            // 关法照抄各面板自己的关闭按钮：有的要顺手清掉确认倒计时（存档 / 无尽 / 轮回）。
            if (_game.SettingsOpen) { OnSettingsCloseClicked(); return; }
            if (_game.MusicPanelOpen) { OnMusicCloseClicked(); return; }
            if (_game.StagePanelOpen) { OnStagePanelCloseClicked(); return; }
            if (_game.SkillPanelOpen) { OnSkillPanelCloseClicked(); return; }
            if (_game.CompareOpen) { OnCompareCloseClicked(); return; }
            if (_game.AchievementPanelOpen) { OnAchievementCloseClicked(); return; }
            if (_game.EndlessPanelOpen) { OnEndlessCloseClicked(); return; }
            if (_game.RebirthPanelOpen) { OnRebirthCloseClicked(); return; }
            if (_game.SavePanelOpen)
            {
                CancelDeleteConfirm();
                _game.CloseSavePanel();
                GameAudio.PlayUiClose();
                return;
            }
            if (_game.EnhancePanelOpen) { _game.CloseEnhancePanel(); GameAudio.PlayUiClose(); return; }
            if (_game.ReforgePanelOpen) { _game.CloseReforgePanel(); GameAudio.PlayUiClose(); return; }
            if (_game.AboutOpen) { _game.CloseAboutPanel(); GameAudio.PlayUiClose(); return; }
            if (_game.DisplayOpen) { _game.CloseDisplayPanel(); GameAudio.PlayUiClose(); return; }

            // 没有浮层：暂停 / 继续。
            _game.ToggleRunning();
        }

        /// <summary>
        /// 快捷键：空格 开始/暂停 · 1/2/4 战斗速度 · Esc 关闭浮层（无浮层则暂停）· R 重置。
        /// 工程 Active Input Handling = Input System，所以走新输入包；留一条老输入的退路，
        /// 免得以后切回旧输入系统时这里每帧抛异常。
        /// </summary>
        private void TickShortcuts()
        {
#if ENABLE_INPUT_SYSTEM
            UnityEngine.InputSystem.Keyboard kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb == null) return;

            // 先把这一帧所有按键都算出来：这样早退（Esc 关浮层）也不会漏更新边沿状态，
            // 否则在浮层里按住过某个键，出来时会立刻误触发一次。
            bool n1 = KeyDown(kb.numpad1Key, ref _kbNumpad1);
            bool n2 = KeyDown(kb.numpad2Key, ref _kbNumpad2);
            bool n4 = KeyDown(kb.numpad4Key, ref _kbNumpad4);
            bool esc = KeyDown(kb.escapeKey, ref _kbEscape);
            bool space = KeyDown(kb.spaceKey, ref _kbSpace);
            bool d1 = KeyDown(kb.digit1Key, ref _kbDigit1) || n1;
            bool d2 = KeyDown(kb.digit2Key, ref _kbDigit2) || n2;
            bool d4 = KeyDown(kb.digit4Key, ref _kbDigit4) || n4;
            bool r = KeyDown(kb.rKey, ref _kbR);

            if (esc) { OnEscapeShortcut(); return; }
            if (AnyOverlayOpen()) return;

            if (space) { OnStartClicked(); return; }
            if (d1) { OnSpeedShortcut(0); return; }
            if (d2) { OnSpeedShortcut(1); return; }
            if (d4) { OnSpeedShortcut(2); return; }
            if (r) OnResetClicked();
#else
            if (Input.GetKeyDown(KeyCode.Escape)) { OnEscapeShortcut(); return; }
            if (AnyOverlayOpen()) return;

            if (Input.GetKeyDown(KeyCode.Space)) { OnStartClicked(); return; }
            if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1)) { OnSpeedShortcut(0); return; }
            if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2)) { OnSpeedShortcut(1); return; }
            if (Input.GetKeyDown(KeyCode.Alpha4) || Input.GetKeyDown(KeyCode.Keypad4)) { OnSpeedShortcut(2); return; }
            if (Input.GetKeyDown(KeyCode.R)) OnResetClicked();
#endif
        }

#if ENABLE_INPUT_SYSTEM
        /// <summary>
        /// 取「这一帧按下」：wasPressedThisFrame 为主，isPressed 的上升沿兜底。
        /// 兜底不是为了替代它，而是为了覆盖面：极短按键只有 wasPressedThisFrame 抓得到，
        /// 而合成事件 / 编辑器未聚焦时它不一定置位，isPressed 那条路反而稳。
        /// prev 由调用方持有，每帧都会更新；两层同时成立也只返回一次 true。
        /// </summary>
        private static bool KeyDown(UnityEngine.InputSystem.Controls.ButtonControl key, ref bool prev)
        {
            bool down = key.isPressed;
            bool edge = down && !prev;
            prev = down;
            return key.wasPressedThisFrame || edge;
        }

        // 上一帧的 isPressed：只给上面的边沿兜底用。
        private bool _kbEscape, _kbSpace, _kbDigit1, _kbNumpad1, _kbDigit2, _kbNumpad2, _kbDigit4, _kbNumpad4, _kbR;
#endif

        /// <summary>快捷键变速：已经在这一档就不再调，SetSpeedIndex 每调一次都会写一行 ⚡ 日志。</summary>
        private void OnSpeedShortcut(int index)
        {
            if (_game == null || _game.SpeedIndex == index) return;
            _game.SetSpeedIndex(index);
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
            sig.Add(p.AttackSpeedBonus, 10000f);
            sig.Add(p.equippedTitle);
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
            if (_titleText != null)
            {
                string title = string.IsNullOrEmpty(p.equippedTitle)
                    ? "无名旅人"
                    : AchievementCatalog.TitleNameOf(p.equippedTitle);
                if (string.IsNullOrEmpty(title)) title = "无名旅人";
                _titleText.text = title;
                _titleText.color = string.IsNullOrEmpty(p.equippedTitle) ? GameConfig.TextMuted : GameConfig.TextGold;
            }

            // 「装备」这一项用强化后的主属性：不然强化完总攻击涨了、括号里的分解却没动，看着像算错了。
            _atkText.text = $"<color=#8C99B0>攻击</color>   <b>{p.Attack}</b>   <color=#5F6779>基础 {p.BaseAttack} + 装备 {(p.weapon != null ? p.weapon.EnhancedAttack : 0)}</color>";
            _defText.text = $"<color=#8C99B0>防御</color>   <b>{p.Defense}</b>   <color=#5F6779>基础 {p.BaseDefense} + 装备 {(p.armor != null ? p.armor.EnhancedDefense : 0) + (p.helmet != null ? p.helmet.EnhancedDefense : 0)}</color>";
            // 速度进软上限后，单看「速度 109」已经推不出攻击间隔（高速度段收益递减），
            // 所以这里直接报「次/秒 + 间隔」，玩家看到的才是实际出手频率。
            float rate = GameConfig.AttacksPerSecond(p.Speed, p.AttackSpeedBonus);
            float interval = GameConfig.AttackInterval(p.Speed, p.AttackSpeedBonus);
            _spdText.text = $"<color=#8C99B0>速度</color>   <b>{p.Speed}</b>   <color=#5F6779>攻速 {rate:0.00}/s · {interval:0.00}s</color>";
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
                sig.Add((int)m.type);
                sig.Add(m.maxHp);
                sig.Add(m.attack);
                sig.Add(m.defense);
                sig.Add(m.speed, 100f);
                sig.Add(m.critChance);
                sig.Add(m.critDamage);
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
                        $"攻击 {m.attack}    防御 {m.defense}    速度 {m.speed:0.#}    攻速 {GameConfig.AttacksPerSecond(m.speed):0.00}/s · {GameConfig.AttackInterval(m.speed):0.00}s\n" +
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
                // 金币会涨到七位数，不写千分位读起来要数零（商店那边同一个数也补了）。
                _goldText.text = $"<color=#8C99B0>金币</color>   <b>{p.gold:N0}</b>";
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
                    $"阵亡 {_game.TotalDeaths}    累计 {_game.TotalGoldEarned:N0} 金币    状态 {StateName(_game.CurrentState)}    速度 x{_game.SpeedMultiplier:0}{paragonNote}";
            }

            // 存档按钮上的槽号：跟当前槽走，切完槽立刻反映出来。
            if (_game.SaveSlot != _cSaveSlot)
            {
                _cSaveSlot = _game.SaveSlot;
                if (_saveButtonLabel != null) _saveButtonLabel.text = (_game.SaveSlot + 1).ToString();
            }

            if (_game.IsRunning != _cStartLabel)
            {
                _cStartLabel = _game.IsRunning;
                _startButtonLabel.text = _game.IsRunning ? "暂停" : "开始";
                SetIcon(_startButtonIcon, _game.IsRunning ? "glyph_pause" : "glyph_play");
            }

            if (_game.SpeedIndex != _cSpeedIndex)
            {
                _cSpeedIndex = _game.SpeedIndex;
                for (int i = 0; i < _speedButtons.Length; i++)
                {
                    bool active = i == _game.SpeedIndex;
                    // 底框是生图资源，不能整块染成绿色；用亮度区分当前速度。
                    _speedButtons[i].image.color = active
                        ? Color.white
                        : new Color(0.58f, 0.62f, 0.70f, 1f);
                    _speedLabels[i].color = active ? GameConfig.TextGold : GameConfig.TextMuted;
                    if (_speedIcons != null && _speedIcons[i] != null)
                        _speedIcons[i].color = active ? Color.white : new Color(1f, 1f, 1f, 0.55f);
                }
            }

            // 任意浮层打开时压一层遮罩，视线自然落到浮层上。
            if (_modalDim != null)
            {
                bool modalOpen = IsOpen(_skillPanel) || IsOpen(_skillChoicePanel) || IsOpen(_stagePanel)
                    || IsOpen(_savePanel) || IsOpen(_enhancePanel) || IsOpen(_reforgePanel)
                    || IsOpen(_settingsPanel) || IsOpen(_musicPanel) || IsOpen(_comparePanel) || IsOpen(_endlessPanel)
                    || IsOpen(_endlessResultPanel) || IsOpen(_rebirthPanel) || IsOpen(_achievementPanel)
                    || IsOpen(_guideRoot);
                if (_modalDim.gameObject.activeSelf != modalOpen) _modalDim.gameObject.SetActive(modalOpen);
            }
        }

    }
}

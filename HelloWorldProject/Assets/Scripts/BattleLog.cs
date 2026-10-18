using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace AutoBattle
{
    /// <summary>
    /// 战斗日志的唯一写入点（要求 15）。
    ///
    /// 为什么要有这个类：日志原先是一个裸 List，谁都能往里 Add。主角走
    /// <see cref="GameController.AddLog"/>（自己维护累计条数与版本号），而 BattleSystem 直接往
    /// 传入的 List 里 Add，两个计数器只能靠 GameController.TickFighting 里的「前后 Count 差值」
    /// 补记。那个补记有两个毛病：
    ///   1. 满 <see cref="GameConfig.MaxLogEntries"/> 条之后差值恒为 0，漏记就静默发生
    ///      （飘字与实时 DPS 曾因此永久失效）；
    ///   2. 新增任何一处直接 Add 都会重新踩进同一个坑，而编译器不会提醒。
    ///
    /// 现在 Add / Clear / 裁剪全在这里，累计条数（<see cref="TotalEntries"/>）与版本号
    /// （<see cref="Version"/>）由同一个 <see cref="Append"/> 一起推进，写日志只有这一条路径。
    /// 索引器只读、集合只实现 <see cref="IReadOnlyList{T}"/>，UI 拿到的视图改不了内容。
    ///
    /// 频道：写入分 <see cref="AddSystem"/>（系统/进度/商店）与 <see cref="AddCombat"/>
    /// （战斗过程）两个入口。分成两种不是为了计数，而是为了「以后要按频道过滤/配色时有地方挂」，
    /// 同时让新写日志的人被迫回答「这条属于哪一类」，而不是继续随手加一个通用 Add。
    /// 只读侧实现 <see cref="IReadOnlyList{T}"/>，UI 拿到的就是不许改的视图。
    /// </summary>
    public sealed class BattleLog : IReadOnlyList<LogEntry>
    {
        private readonly List<LogEntry> _entries = new List<LogEntry>(128);

        /// <summary>累计写入过多少条（只增不减，Clear 也不清零）：UI 用它算「这一帧新增了几条」。</summary>
        public int TotalEntries { get; private set; }

        /// <summary>内容有任何变化就自增（写入 / 裁剪 / 清空），UI 用它决定要不要重绘。</summary>
        public int Version { get; private set; }

        public int Count => _entries.Count;

        public LogEntry this[int index] => _entries[index];

        /// <summary>
        /// 写入一条「系统 / 进度 / 商店 / 界面」日志（升级、掉落、购买、开关面板…）。
        /// 与 <see cref="AddCombat"/> 走同一个 <see cref="Append"/>，累计条数只在这里推进一次。
        /// </summary>
        public void AddSystem(string message, Color color) => Append(message, color);

        /// <summary>
        /// 写入一条「战斗过程」日志（谁打了谁、触发哪个技能）。
        /// 给战斗循环专用：这些行以后可能整体被「只看系统消息」的开关过滤掉，
        /// 或者按频道做不同的配色/缩进，所以必须在写入时就知道它是战斗行。
        /// </summary>
        public void AddCombat(string message, Color color) => Append(message, color);

        /// <summary>
        /// 唯一的写入实现（私有）：累计条数、裁剪、版本号都在这里推一次。
        /// 外面只认 AddSystem / AddCombat 两个入口 —— 加一条新日志时先想清楚它属于哪个频道，
        /// 而不是随手调一个「谁都像能用」的 Add。
        /// </summary>
        private void Append(string message, Color color)
        {
            if (string.IsNullOrEmpty(message)) return;

            _entries.Add(new LogEntry(message, color));
            TotalEntries++;

            int overflow = _entries.Count - GameConfig.MaxLogEntries;
            if (overflow > 0) _entries.RemoveRange(0, overflow);

            Version++;
        }

        /// <summary>清空（换档 / 删档 / 重置）。累计条数保留，它统计的是「写过多少条」。</summary>
        public void Clear()
        {
            if (_entries.Count == 0) return;
            _entries.Clear();
            Version++;
        }

        public IEnumerator<LogEntry> GetEnumerator() => _entries.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}

using System;
using System.Diagnostics;
using System.Text;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace AutoBattle.EditorTools
{
    /// <summary>
    /// 一键快速回归（roadmap A5 / C3）：静态哨兵 + 成就核心 + 无尽冒烟。
    /// 三步都在编辑器里跑，不需要进 Play 模式；完整报告返回字符串，便于 eval / CLI 与
    /// tools/quick-regression.ps1 直接解析 PASS / FAIL。
    /// </summary>
    public static class QuickRegression
    {
        public const string MenuPath = "工具/回归/跑一遍快速回归";

        private static readonly string[] CheckNames = { "静态哨兵", "成就核心", "无尽冒烟" };

        [MenuItem(MenuPath)]
        public static void RunAllQuickMenu()
        {
            string report = RunAllQuick();
            if (report.Contains("FAIL")) Debug.LogError(report);
            else Debug.Log(report);
        }

        /// <summary>
        /// 三步快速回归，返回多行报告。任何一步抛异常都记成 FAIL 并继续下一步——
        /// 只报第一条错误会掩盖后面的结果，排查时反而要多跑几轮。
        /// </summary>
        public static string RunAllQuick()
        {
            var sb = new StringBuilder();
            var total = Stopwatch.StartNew();
            int pass = 0;
            int fail = 0;

            sb.AppendLine("[QuickRegression] 快速回归：静态哨兵 / 成就核心 / 无尽冒烟");
            sb.AppendLine("Unity " + Application.unityVersion + " · 工程 " + Application.dataPath);

            for (int i = 0; i < CheckNames.Length; i++)
            {
                var watch = Stopwatch.StartNew();
                string detail;
                bool ok;
                try
                {
                    detail = RunStep(i);
                    ok = detail != null && detail.IndexOf("FAIL", StringComparison.OrdinalIgnoreCase) < 0;
                }
                catch (Exception e)
                {
                    detail = "异常：" + e.GetType().Name + " " + e.Message;
                    ok = false;
                }

                watch.Stop();
                if (ok) pass++; else fail++;
                sb.AppendLine(string.Format("{0} — {1}（{2:0.0}s）",
                    ok ? "PASS" : "FAIL", CheckNames[i], watch.Elapsed.TotalSeconds));
                sb.AppendLine("  " + FirstLine(detail));
            }

            total.Stop();
            sb.AppendLine();
            sb.AppendLine(string.Format("汇总：{0} 通过 / {1} 失败，用时 {2:0.0}s",
                pass, fail, total.Elapsed.TotalSeconds));
            sb.AppendLine(fail == 0 ? "结果：PASS" : "结果：FAIL");
            return sb.ToString();
        }

        /// <summary>batchmode 入口：跑完按结果给退出码，坏在 CI 里静默通过。</summary>
        public static void RunAllQuickBatch()
        {
            int code;
            try
            {
                string report = RunAllQuick();
                Debug.Log(report);
                code = report.Contains("结果：PASS") ? 0 : 1;
            }
            catch (Exception e)
            {
                Debug.LogError("[QuickRegression] 批量回归异常：" + e);
                code = 1;
            }

            EditorApplication.Exit(code);
        }

        private static string RunStep(int index)
        {
            switch (index)
            {
                case 0:
                    return BalanceRegression.RunStaticOnly();
                case 1:
                    return AchievementRegression.RunCore();
                case 2:
                    // 冒烟只看「能不能跑完」：2 个种子、40 层，跑不动或抛异常就是 FAIL。
                    string endless = EndlessPacing.Run(2, 40);
                    return endless.Contains("首死") ? "无尽 2 种子 / 40 层跑通" : endless;
                default:
                    return "未知步骤";
            }
        }

        private static string FirstLine(string text)
        {
            if (string.IsNullOrEmpty(text)) return "（无输出）";
            int cut = text.IndexOf('\n');
            string line = (cut < 0 ? text : text.Substring(0, cut)).Trim();
            return line.Length > 200 ? line.Substring(0, 200) + "…" : line;
        }
    }
}

using System;
using System.Globalization;
using System.IO;
using System.Windows.Forms;

namespace CCleanGuard
{
    /// <summary>
    /// The mode the scheduled task runs. Decides whether a prompt is due, asks,
    /// and cleans if the answer is yes.
    /// </summary>
    internal static class DailyGuard
    {
        public const int MaxPromptsPerCycle = 3;
        public const int SnoozeHours = 3;
        private const int PromptTimeoutSeconds = 180;

        public static int Run(bool dryRun)
        {
            Log.Rotate(180);

            DateTime now = DateTime.Now;

            // The window is 06:00 today until 02:00 tomorrow, so it is closed 02:00-05:59.
            bool inWindow = !(now.Hour >= 2 && now.Hour < 6);
            string cycle = State.CurrentCycleKey();

            State st = State.Load();

            if (!dryRun)
            {
                if (!inWindow)
                {
                    Log.Write("daily: outside window (" + now.ToString("HH:mm") + ")");
                    return 0;
                }
                if (st.LastRunCycle == cycle)
                {
                    Log.Write("daily: already cleaned for cycle " + cycle);
                    return 0;
                }
                if (st.SnoozeUntil > now)
                {
                    Log.Write("daily: snoozed until " + st.SnoozeUntil.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture));
                    return 0;
                }
                if (st.PromptCountCycle == cycle && st.PromptCount >= MaxPromptsPerCycle)
                {
                    Log.Write("daily: prompt budget spent for " + cycle);
                    return 0;
                }
            }

            long free = Cleaner.FreeSpace();
            long total = 0;
            try { total = new DriveInfo("C").TotalSize; } catch { }

            string freeText = Cleaner.FormatBytes(free);
            string totalText = Cleaner.FormatBytes(total);

            string message =
                "C 盘现在可用 " + freeText + " / 共 " + totalText + "。\r\n\r\n" +
                "今天清理一次缓存吗？大约 10-30 秒。\r\n\r\n" +
                "只删除缓存和安装残留（浏览器缓存、编辑器索引、显卡着色器缓存、更新包等），\r\n" +
                "不会碰你的文件，也不会碰页面文件。";

            if (dryRun) message = "【预览模式，不会删除任何东西】\r\n\r\n" + message;

            bool yes = PromptForm.Ask("C 盘清理", message, PromptTimeoutSeconds);

            if (!dryRun)
            {
                st.PromptCountCycle = cycle;
                st.PromptCount = st.PromptCount + 1;

                if (!yes)
                {
                    st.SnoozeUntil = now.AddHours(SnoozeHours);
                    st.Save();
                    Log.Write("daily: declined or timed out, snoozed " + SnoozeHours + "h");
                    return 0;
                }
            }

            if (!yes) return 0;

            Log.Write("daily: user confirmed, cleaning (dry=" + dryRun + ")");
            CleanResult result = Cleaner.RunAll(dryRun, Log.Write);

            if (!dryRun)
            {
                st.LastRunCycle = cycle;
                st.PromptCountCycle = cycle;
                st.PromptCount = 0;
                st.SnoozeUntil = DateTime.MinValue;
                st.Save();

                string done =
                    (dryRun ? "【预览】" : "清理完成。") + "\r\n\r\n" +
                    "本次释放: " + Cleaner.FormatBytes(result.DeletedBytes) + "\r\n" +
                    "当前可用: " + Cleaner.FormatBytes(result.FreeAfter) + "\r\n\r\n" +
                    "明细日志: " + Paths.LogDir;

                MessageBox.Show(done, "C 盘清理完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }

            return 0;
        }
    }
}

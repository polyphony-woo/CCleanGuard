using System;
using System.Windows.Forms;

namespace CCleanGuard
{
    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            bool daily = false;
            bool dryRun = false;
            bool install = false;
            bool uninstall = false;
            bool clean = false;

            foreach (string raw in args)
            {
                string a = raw.ToLowerInvariant();
                if (a == "--daily") daily = true;
                else if (a == "--dry-run" || a == "--dry") dryRun = true;
                else if (a == "--install") install = true;
                else if (a == "--uninstall") uninstall = true;
                else if (a == "--clean") clean = true;
            }

            Log.Rotate(180);

            if (daily) return DailyGuard.Run(dryRun);

            if (install)
            {
                string error = TaskInstaller.Install(Application.ExecutablePath);
                MessageBox.Show(error == null ? "已安装每日提醒。" : error,
                    "CCleanGuard", MessageBoxButtons.OK,
                    error == null ? MessageBoxIcon.Information : MessageBoxIcon.Error);
                return error == null ? 0 : 1;
            }

            if (uninstall)
            {
                string error = TaskInstaller.Uninstall();
                MessageBox.Show(error == null ? "已移除每日提醒。" : error,
                    "CCleanGuard", MessageBoxButtons.OK,
                    error == null ? MessageBoxIcon.Information : MessageBoxIcon.Error);
                return error == null ? 0 : 1;
            }

            if (clean)
            {
                CleanResult result = Cleaner.RunAll(dryRun, Log.Write);
                MessageBox.Show("释放 " + Cleaner.FormatBytes(result.DeletedBytes) + "\r\n当前可用 " + Cleaner.FormatBytes(result.FreeAfter),
                    "CCleanGuard", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return 0;
            }

            Application.Run(new MainForm());
            return 0;
        }
    }
}

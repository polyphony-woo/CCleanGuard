using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace CCleanGuard
{
    /// <summary>Every runtime path the tool needs. Nothing is hard-coded to a machine.</summary>
    internal static class Paths
    {
        private static string _home;

        public static string Home
        {
            get
            {
                if (_home == null)
                {
                    _home = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "CCleanGuard");
                    try { Directory.CreateDirectory(_home); } catch { }
                }
                return _home;
            }
        }

        public static string ExePath { get { return Path.Combine(Home, "CCleanGuard.exe"); } }
        public static string LogDir { get { return Path.Combine(Home, "logs"); } }
        public static string StateFile { get { return Path.Combine(Home, "state.ini"); } }

        public static string Combine(string a, string b) { return Path.Combine(a, b); }

        /// <summary>True when child is equal to or below parent (case-insensitive, normalised).</summary>
        public static bool IsSameOrInside(string child, string parent)
        {
            if (string.IsNullOrEmpty(child) || string.IsNullOrEmpty(parent)) return false;
            try
            {
                string c = Path.GetFullPath(child).TrimEnd('\\') + "\\";
                string p = Path.GetFullPath(parent).TrimEnd('\\') + "\\";
                return c.StartsWith(p, StringComparison.OrdinalIgnoreCase);
            }
            catch { return false; }
        }
    }

    /// <summary>Monthly-rotated plain text log.</summary>
    internal static class Log
    {
        private static readonly object Sync = new object();

        public static void Write(string message)
        {
            lock (Sync)
            {
                try
                {
                    Directory.CreateDirectory(Paths.LogDir);
                    string file = Path.Combine(Paths.LogDir, "cclean-" + DateTime.Now.ToString("yyyyMM", CultureInfo.InvariantCulture) + ".log");
                    string line = "[" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) + "] " + message + Environment.NewLine;
                    File.AppendAllText(file, line, new UTF8Encoding(false));
                }
                catch { }
            }
        }

        public static void Rotate(int days)
        {
            try
            {
                Directory.CreateDirectory(Paths.LogDir);
                foreach (string f in Directory.GetFiles(Paths.LogDir, "*.log"))
                {
                    try { if (File.GetLastWriteTime(f) < DateTime.Now.AddDays(-days)) File.Delete(f); } catch { }
                }
            }
            catch { }
        }
    }

    /// <summary>Small key=value state file: when we last cleaned, and how many times we asked.</summary>
    internal sealed class State
    {
        public string LastRunCycle = string.Empty;
        public DateTime SnoozeUntil = DateTime.MinValue;
        public string PromptCountCycle = string.Empty;
        public int PromptCount;

        public static string CurrentCycleKey()
        {
            DateTime now = DateTime.Now;
            DateTime cycle = now.Hour < 6 ? now.Date.AddDays(-1) : now.Date;
            return cycle.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        public static State Load()
        {
            State s = new State();
            try
            {
                if (!File.Exists(Paths.StateFile)) return s;
                foreach (string raw in File.ReadAllLines(Paths.StateFile))
                {
                    string line = raw.Trim();
                    if (line.Length == 0 || line[0] == '#') continue;
                    int eq = line.IndexOf('=');
                    if (eq <= 0) continue;
                    string k = line.Substring(0, eq).Trim();
                    string v = line.Substring(eq + 1).Trim();
                    switch (k)
                    {
                        case "lastRunCycle": s.LastRunCycle = v; break;
                        case "promptCountCycle": s.PromptCountCycle = v; break;
                        case "promptCount":
                            int n;
                            if (int.TryParse(v, out n)) s.PromptCount = n;
                            break;
                        case "snoozeUntil":
                            DateTime dt;
                            if (DateTime.TryParse(v, CultureInfo.InvariantCulture, DateTimeStyles.None, out dt)) s.SnoozeUntil = dt;
                            break;
                    }
                }
            }
            catch { }
            return s;
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(Paths.Home);
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("# CCleanGuard state - safe to delete, it only resets the daily reminder");
                sb.AppendLine("lastRunCycle=" + LastRunCycle);
                sb.AppendLine("promptCountCycle=" + PromptCountCycle);
                sb.AppendLine("promptCount=" + PromptCount.ToString(CultureInfo.InvariantCulture));
                sb.AppendLine("snoozeUntil=" + (SnoozeUntil == DateTime.MinValue ? "" : SnoozeUntil.ToString("s", CultureInfo.InvariantCulture)));
                File.WriteAllText(Paths.StateFile, sb.ToString(), new UTF8Encoding(false));
            }
            catch { }
        }
    }
}

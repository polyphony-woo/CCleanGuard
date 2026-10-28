using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Security;
using System.Security.Principal;
using System.Text;

namespace CCleanGuard
{
    /// <summary>Registers the hourly 06:00-02:00 reminder as a Windows scheduled task.</summary>
    internal static class TaskInstaller
    {
        public const string TaskName = "CCleanGuard-Daily";

        private static string CurrentSid()
        {
            try { return WindowsIdentity.GetCurrent().User.Value; }
            catch { return Environment.UserName; }
        }

        private static DateTime NextSixAm()
        {
            DateTime now = DateTime.Now;
            DateTime t = now.Date.AddHours(6);
            if (t <= now) t = t.AddDays(1);
            return t;
        }

        private static string BuildXml(string exePath, string arguments)
        {
            string sid = SecurityElement.Escape(CurrentSid());
            string exe = SecurityElement.Escape(exePath);
            string args = SecurityElement.Escape(arguments);
            string start = NextSixAm().ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture);
            string user = SecurityElement.Escape(Environment.UserDomainName + "\\" + Environment.UserName);

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-16\"?>");
            sb.AppendLine("<Task version=\"1.4\" xmlns=\"http://schemas.microsoft.com/windows/2004/02/mit/task\">");
            sb.AppendLine("  <RegistrationInfo>");
            sb.AppendLine("    <Description>Asks once a day, between 06:00 and 02:00, whether to clean C: caches.</Description>");
            sb.AppendLine("    <URI>\\" + TaskName + "</URI>");
            sb.AppendLine("  </RegistrationInfo>");
            sb.AppendLine("  <Principals>");
            sb.AppendLine("    <Principal id=\"Author\">");
            sb.AppendLine("      <UserId>" + sid + "</UserId>");
            sb.AppendLine("      <LogonType>InteractiveToken</LogonType>");
            sb.AppendLine("      <RunLevel>HighestAvailable</RunLevel>");
            sb.AppendLine("    </Principal>");
            sb.AppendLine("  </Principals>");
            sb.AppendLine("  <Settings>");
            sb.AppendLine("    <DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>");
            sb.AppendLine("    <StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>");
            sb.AppendLine("    <ExecutionTimeLimit>PT30M</ExecutionTimeLimit>");
            sb.AppendLine("    <MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>");
            sb.AppendLine("    <StartWhenAvailable>true</StartWhenAvailable>");
            sb.AppendLine("    <IdleSettings>");
            sb.AppendLine("      <StopOnIdleEnd>false</StopOnIdleEnd>");
            sb.AppendLine("      <RestartOnIdle>false</RestartOnIdle>");
            sb.AppendLine("    </IdleSettings>");
            sb.AppendLine("    <UseUnifiedSchedulingEngine>true</UseUnifiedSchedulingEngine>");
            sb.AppendLine("    <WakeToRun>false</WakeToRun>");
            sb.AppendLine("  </Settings>");
            sb.AppendLine("  <Triggers>");
            sb.AppendLine("    <CalendarTrigger>");
            sb.AppendLine("      <StartBoundary>" + start + "</StartBoundary>");
            sb.AppendLine("      <Enabled>true</Enabled>");
            sb.AppendLine("      <ScheduleByDay><DaysInterval>1</DaysInterval></ScheduleByDay>");
            sb.AppendLine("      <Repetition>");
            sb.AppendLine("        <Interval>PT1H</Interval>");
            sb.AppendLine("        <Duration>PT20H</Duration>");
            sb.AppendLine("        <StopAtDurationEnd>true</StopAtDurationEnd>");
            sb.AppendLine("      </Repetition>");
            sb.AppendLine("    </CalendarTrigger>");
            sb.AppendLine("  </Triggers>");
            sb.AppendLine("  <Actions Context=\"Author\">");
            sb.AppendLine("    <Exec>");
            sb.AppendLine("      <Command>" + exe + "</Command>");
            sb.AppendLine("      <Arguments>" + args + "</Arguments>");
            sb.AppendLine("    </Exec>");
            sb.AppendLine("  </Actions>");
            sb.AppendLine("</Task>");
            return sb.ToString();
        }

        private static int Run(string file, string args, out string output)
        {
            output = string.Empty;
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo(file, args);
                psi.UseShellExecute = false;
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;
                psi.CreateNoWindow = true;
                using (Process p = Process.Start(psi))
                {
                    string stdout = p.StandardOutput.ReadToEnd();
                    string stderr = p.StandardError.ReadToEnd();
                    p.WaitForExit();
                    output = (stdout + stderr).Trim();
                    return p.ExitCode;
                }
            }
            catch (Exception ex)
            {
                output = ex.Message;
                return -1;
            }
        }

        public static bool IsInstalled()
        {
            string output;
            return Run("schtasks.exe", "/query /tn \"" + TaskName + "\"", out output) == 0;
        }

        /// <summary>Returns null on success, otherwise the schtasks message.</summary>
        public static string Install(string exePath)
        {
            if (!Cleaner.IsAdmin())
                return "注册计划任务需要管理员权限，请以管理员身份运行 CCleanGuard。";

            string xml = BuildXml(exePath, "--daily");
            string temp = Path.Combine(Path.GetTempPath(), "ccleanguard-task.xml");
            try
            {
                // schtasks wants UTF-16 for /xml
                File.WriteAllText(temp, xml, Encoding.Unicode);
            }
            catch (Exception ex) { return "无法写入临时任务文件: " + ex.Message; }

            string output;
            int code = Run("schtasks.exe", "/create /tn \"" + TaskName + "\" /xml \"" + temp + "\" /f", out output);
            try { File.Delete(temp); } catch { }

            Log.Write("task install exit=" + code);
            if (code != 0) return string.IsNullOrEmpty(output) ? "schtasks 返回 " + code : output;
            return null;
        }

        public static string Uninstall()
        {
            if (!Cleaner.IsAdmin())
                return "移除计划任务需要管理员权限，请以管理员身份运行 CCleanGuard。";

            string output;
            int code = Run("schtasks.exe", "/delete /tn \"" + TaskName + "\" /f", out output);
            Log.Write("task remove exit=" + code);
            if (code != 0 && !output.ToLowerInvariant().Contains("cannot find"))
                return string.IsNullOrEmpty(output) ? "schtasks 返回 " + code : output;
            return null;
        }
    }
}

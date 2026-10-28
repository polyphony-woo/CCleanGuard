using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Principal;

namespace CCleanGuard
{
    internal sealed class CleanTarget
    {
        public readonly string Path;
        public readonly string Label;
        /// <summary>true: delete the children but keep the folder; false: delete the folder itself.</summary>
        public readonly bool ContentsOnly;
        /// <summary>Child names starting with one of these are left alone.</summary>
        public readonly string[] KeepPrefixes;
        public readonly bool AdminOnly;

        public CleanTarget(string path, string label, bool contentsOnly, bool adminOnly, params string[] keepPrefixes)
        {
            Path = path;
            Label = label;
            ContentsOnly = contentsOnly;
            AdminOnly = adminOnly;
            KeepPrefixes = keepPrefixes ?? new string[0];
        }
    }

    internal sealed class CleanResult
    {
        public bool DryRun;
        public long FreeBefore;
        public long FreeAfter;
        public long DeletedBytes;
        public int ItemsTouched;
        public int ItemsSkipped;

        public long GainedBytes { get { return FreeAfter > FreeBefore ? FreeAfter - FreeBefore : 0; } }
        public double GainedGB { get { return GainedBytes / 1024.0 / 1024.0 / 1024.0; } }
        public double FreeAfterGB { get { return FreeAfter / 1024.0 / 1024.0 / 1024.0; } }
    }

    /// <summary>
    /// The whitelist. Nothing outside this list is ever touched, and pagefile.sys is
    /// deliberately absent so the tool can never disturb virtual memory.
    /// </summary>
    internal static class Cleaner
    {
        public static bool IsAdmin()
        {
            try
            {
                WindowsIdentity id = WindowsIdentity.GetCurrent();
                WindowsPrincipal p = new WindowsPrincipal(id);
                return p.IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch { return false; }
        }

        public static long FreeSpace()
        {
            try { return new DriveInfo("C").AvailableFreeSpace; } catch { return 0; }
        }

        public static string FormatBytes(long bytes)
        {
            if (bytes >= 1024L * 1024L * 1024L) return (bytes / 1024.0 / 1024.0 / 1024.0).ToString("N2", CultureInfo.CurrentCulture) + " GB";
            if (bytes >= 1024L * 1024L) return (bytes / 1024.0 / 1024.0).ToString("N0", CultureInfo.CurrentCulture) + " MB";
            if (bytes >= 1024L) return (bytes / 1024.0).ToString("N0", CultureInfo.CurrentCulture) + " KB";
            return bytes.ToString(CultureInfo.CurrentCulture) + " B";
        }

        public static List<CleanTarget> BuildTargets()
        {
            string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

            List<CleanTarget> list = new List<CleanTarget>();

            // ---- user scope: no elevation needed for these, but we run elevated anyway ----
            list.Add(new CleanTarget(Path.Combine(local, @"Google\Chrome\User Data\OptGuideOnDeviceModel"), "Chrome 端侧 AI 模型", false, false));
            list.Add(new CleanTarget(Path.Combine(local, @"Google\Chrome\User Data\ShaderCache"), "Chrome 着色器缓存", false, false));
            list.Add(new CleanTarget(Path.Combine(local, @"Microsoft\vscode-cpptools"), "VS Code C++ 索引缓存", false, false));
            list.Add(new CleanTarget(Path.Combine(local, @"Microsoft\WinGet\Packages"), "WinGet 安装包缓存", false, false));
            list.Add(new CleanTarget(Path.Combine(local, @"Microsoft\Office\SolutionPackages"), "Office 解决方案缓存", false, false));
            list.Add(new CleanTarget(Path.Combine(local, @"NVIDIA\DXCache"), "NVIDIA DX 着色器缓存", false, false));
            list.Add(new CleanTarget(Path.Combine(local, @"NVIDIA\GLCache"), "NVIDIA GL 缓存", false, false));
            list.Add(new CleanTarget(Path.Combine(profile, @"AppData\LocalLow\NVIDIA\DXCache"), "NVIDIA DX 缓存 (LocalLow)", false, false));
            list.Add(new CleanTarget(Path.Combine(local, @"Temp"), "用户临时文件", true, false));
            list.Add(new CleanTarget(Path.Combine(local, @"npm-cache\_cacache"), "npm 缓存", false, false));
            list.Add(new CleanTarget(Path.Combine(local, @"npm-cache\_npx"), "npm npx 缓存", false, false));
            list.Add(new CleanTarget(Path.Combine(local, @"pip\cache"), "pip 缓存", false, false));
            list.Add(new CleanTarget(Path.Combine(local, @"Steam\htmlcache"), "Steam 客户端缓存", false, false));
            list.Add(new CleanTarget(Path.Combine(roaming, @"Code\CachedExtensionVSIXs"), "VS Code 扩展安装包缓存", false, false));
            list.Add(new CleanTarget(Path.Combine(roaming, @"Code\CachedData"), "VS Code CachedData", false, false));
            list.Add(new CleanTarget(Path.Combine(roaming, @"Code\Code Cache"), "VS Code Code Cache", false, false));
            list.Add(new CleanTarget(Path.Combine(roaming, @"Code\GPUCache"), "VS Code GPU 缓存", false, false));
            list.Add(new CleanTarget(Path.Combine(roaming, @"Code\logs"), "VS Code 日志", false, false));

            // ---- admin scope ----
            list.Add(new CleanTarget(@"C:\Windows\Temp", "Windows 临时文件", true, true));
            list.Add(new CleanTarget(@"C:\Windows\SoftwareDistribution\Download", "Windows 更新下载缓存", true, true));
            list.Add(new CleanTarget(@"C:\ProgramData\NVIDIA\NGX", "NVIDIA NGX 模型缓存", true, true));
            list.Add(new CleanTarget(@"C:\Program Files (x86)\Google\GoogleUpdater\crx_cache", "Google 更新器缓存", false, true));
            list.Add(new CleanTarget(@"C:\Program Files (x86)\Microsoft Office\Updates", "Office 更新包缓存", true, true));

            return list;
        }

        public static long PathSize(string path)
        {
            try
            {
                if (Directory.Exists(path)) return DirectorySize(path);
                if (File.Exists(path))
                {
                    try { return new FileInfo(path).Length; } catch { return 0; }
                }
            }
            catch { }
            return 0;
        }

        private static long DirectorySize(string dir)
        {
            long total = 0;
            try
            {
                foreach (string f in Directory.GetFiles(dir))
                {
                    try { total += new FileInfo(f).Length; } catch { }
                }
            }
            catch { }
            try
            {
                foreach (string d in Directory.GetDirectories(dir))
                {
                    total += DirectorySize(d);
                }
            }
            catch { }
            return total;
        }

        private static bool IsKept(string name, string[] prefixes)
        {
            if (prefixes == null) return false;
            foreach (string p in prefixes)
            {
                if (p.Length == 0) continue;
                if (name.StartsWith(p, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        /// <summary>Delete a tree, tolerating individual locked files instead of aborting.</summary>
        private static void DeleteTree(string dir)
        {
            try { Directory.Delete(dir, true); return; } catch { }

            try
            {
                foreach (string f in Directory.GetFiles(dir))
                {
                    try { File.SetAttributes(f, FileAttributes.Normal); File.Delete(f); } catch { }
                }
            }
            catch { }

            try
            {
                foreach (string d in Directory.GetDirectories(dir))
                {
                    DeleteTree(d);
                }
            }
            catch { }

            try { Directory.Delete(dir, true); } catch { }
        }

        private static void ClearContents(string dir, string[] keepPrefixes)
        {
            try
            {
                foreach (string f in Directory.GetFiles(dir))
                {
                    if (IsKept(Path.GetFileName(f), keepPrefixes)) continue;
                    try { File.SetAttributes(f, FileAttributes.Normal); File.Delete(f); } catch { }
                }
            }
            catch { }

            try
            {
                foreach (string d in Directory.GetDirectories(dir))
                {
                    if (IsKept(Path.GetFileName(d), keepPrefixes)) continue;
                    // never delete ourselves, whatever happens
                    if (Paths.IsSameOrInside(Paths.Home, d)) continue;
                    DeleteTree(d);
                }
            }
            catch { }
        }

        public static CleanResult RunAll(bool dryRun, Action<string> report)
        {
            if (report == null) report = delegate(string s) { };

            CleanResult result = new CleanResult();
            result.DryRun = dryRun;
            result.FreeBefore = FreeSpace();

            report("=== " + (dryRun ? "预览模式（不会删除任何东西）" : "开始清理") + " ===");
            report("清理前 C 盘可用: " + FormatBytes(result.FreeBefore));

            bool admin = IsAdmin();
            if (!admin) report("提示: 未以管理员运行，管理员级目标会被跳过。");

            foreach (CleanTarget t in BuildTargets())
            {
                long before;
                try { before = PathSize(t.Path); } catch { before = 0; }

                if (before <= 0)
                {
                    report("  跳过  " + t.Label + "（不存在或为空）");
                    result.ItemsSkipped++;
                    continue;
                }

                if (t.AdminOnly && !admin)
                {
                    report("  跳过  " + t.Label + "（需要管理员权限）");
                    result.ItemsSkipped++;
                    continue;
                }

                if (dryRun)
                {
                    report("  预览  " + t.Label + "  " + FormatBytes(before));
                    continue;
                }

                if (t.ContentsOnly) ClearContents(t.Path, t.KeepPrefixes);
                else DeleteTree(t.Path);

                long after = PathSize(t.Path);
                long freed = before - after;
                if (freed < 0) freed = 0;
                result.DeletedBytes += freed;

                if (freed > 0) result.ItemsTouched++;
                else result.ItemsSkipped++;

                string note = (after > 0 && freed < before) ? "   [" + FormatBytes(after) + " 仍被占用]" : string.Empty;
                report("  释放  " + t.Label + "  " + FormatBytes(freed) + " / " + FormatBytes(before) + note);
            }

            result.FreeAfter = FreeSpace();
            report("清理后 C 盘可用: " + FormatBytes(result.FreeAfter));
            report("本次净增: " + FormatBytes(result.GainedBytes));
            report("=== 完成 ===");

            Log.Write("run dry=" + dryRun + " freed=" + FormatBytes(result.DeletedBytes) + " gained=" + FormatBytes(result.GainedBytes) + " free=" + FormatBytes(result.FreeAfter));
            return result;
        }
    }
}

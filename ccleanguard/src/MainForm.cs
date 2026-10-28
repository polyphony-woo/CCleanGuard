using System;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace CCleanGuard
{
    internal sealed class MainForm : Form
    {
        private readonly Label _freeLabel;
        private readonly Label _taskLabel;
        private readonly RichTextBox _log;
        private readonly Button _btnClean;
        private readonly Button _btnPreview;
        private readonly Button _btnInstall;
        private readonly Button _btnRemove;

        public MainForm()
        {
            Text = "CCleanGuard - C 盘清理守卫";
            ClientSize = new Size(760, 560);
            MinimumSize = new Size(680, 500);
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
            BackColor = Color.White;

            Label title = new Label();
            title.Text = "CCleanGuard";
            title.Font = new Font("Microsoft YaHei UI", 15F, FontStyle.Bold, GraphicsUnit.Point);
            title.AutoSize = true;
            title.Location = new Point(18, 14);
            title.ForeColor = Color.FromArgb(30, 30, 30);
            Controls.Add(title);

            Label sub = new Label();
            sub.Text = "只删缓存和安装残留，白名单之外的任何东西都不会动，页面文件永不触碰。";
            sub.AutoSize = true;
            sub.Location = new Point(20, 52);
            sub.ForeColor = Color.Gray;
            Controls.Add(sub);

            _freeLabel = new Label();
            _freeLabel.AutoSize = false;
            _freeLabel.Location = new Point(20, 78);
            _freeLabel.Size = new Size(700, 24);
            _freeLabel.Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Bold, GraphicsUnit.Point);
            Controls.Add(_freeLabel);

            _taskLabel = new Label();
            _taskLabel.AutoSize = false;
            _taskLabel.Location = new Point(20, 104);
            _taskLabel.Size = new Size(700, 24);
            _taskLabel.ForeColor = Color.DimGray;
            Controls.Add(_taskLabel);

            _log = new RichTextBox();
            _log.Location = new Point(20, 136);
            _log.Size = new Size(714, 340);
            _log.ReadOnly = true;
            _log.BackColor = Color.FromArgb(248, 248, 248);
            _log.BorderStyle = BorderStyle.FixedSingle;
            _log.Font = new Font("Consolas", 9F, FontStyle.Regular, GraphicsUnit.Point);
            _log.WordWrap = false;
            _log.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;
            Controls.Add(_log);

            _btnClean = MakeButton("立即清理", 20, 492, 110);
            _btnClean.Click += delegate { ConfirmAndClean(false); };
            Controls.Add(_btnClean);

            _btnPreview = MakeButton("预览（不删除）", 140, 492, 130);
            _btnPreview.Click += delegate { ConfirmAndClean(true); };
            Controls.Add(_btnPreview);

            _btnInstall = MakeButton("安装每日提醒", 300, 492, 120);
            _btnInstall.Click += delegate { InstallTask(); };
            Controls.Add(_btnInstall);

            _btnRemove = MakeButton("移除提醒", 430, 492, 100);
            _btnRemove.Click += delegate { RemoveTask(); };
            Controls.Add(_btnRemove);

            Button openLog = MakeButton("打开日志", 540, 492, 90);
            openLog.Click += delegate { OpenLog(); };
            Controls.Add(openLog);

            Button close = MakeButton("退出", 640, 492, 90);
            close.Click += delegate { Close(); };
            Controls.Add(close);

            foreach (Control c in new Control[] { _log, _btnClean, _btnPreview, _btnInstall, _btnRemove, openLog, close })
            {
                c.Anchor = c.Anchor | AnchorStyles.Bottom;
            }

            Load += delegate { RefreshStatus(); };
        }

        private static Button MakeButton(string text, int x, int y, int width)
        {
            Button b = new Button();
            b.Text = text;
            b.Location = new Point(x, y);
            b.Size = new Size(width, 32);
            b.FlatStyle = FlatStyle.System;
            b.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            return b;
        }

        private void RefreshStatus()
        {
            try
            {
                DriveInfo d = new DriveInfo("C");
                double freeGB = d.AvailableFreeSpace / 1024.0 / 1024.0 / 1024.0;
                double totalGB = d.TotalSize / 1024.0 / 1024.0 / 1024.0;
                double pct = 100.0 * d.AvailableFreeSpace / d.TotalSize;
                _freeLabel.Text = string.Format(CultureInfo.CurrentCulture,
                    "C 盘 可用 {0:N2} GB / 共 {1:N2} GB  ({2:N1}% 可用)", freeGB, totalGB, pct);
            }
            catch
            {
                _freeLabel.Text = "C 盘信息读取失败";
            }

            bool installed = TaskInstaller.IsInstalled();
            _taskLabel.Text = installed
                ? "每日提醒: 已安装（06:00 - 次日 02:00 之间问一次）"
                : "每日提醒: 未安装";

            bool admin = Cleaner.IsAdmin();
            _taskLabel.Text += admin ? "     · 管理员权限: 是" : "     · 管理员权限: 否（管理员级目标会被跳过）";
        }

        private void Append(string line)
        {
            if (InvokeRequired)
            {
                BeginInvoke((MethodInvoker)delegate { Append(line); });
                return;
            }
            _log.AppendText(line + Environment.NewLine);
            _log.SelectionStart = _log.TextLength;
            _log.ScrollToCaret();
        }

        private void SetBusy(bool busy)
        {
            _btnClean.Enabled = !busy;
            _btnPreview.Enabled = !busy;
            _btnInstall.Enabled = !busy;
            _btnRemove.Enabled = !busy;
            Cursor = busy ? Cursors.WaitCursor : Cursors.Default;
        }

        private void ConfirmAndClean(bool dryRun)
        {
            if (!dryRun)
            {
                DialogResult r = MessageBox.Show(
                    "将删除白名单内的缓存和安装残留。\r\n\r\n这是不可撤销的，但删掉的都是会自动重建的缓存。\r\n\r\n继续吗？",
                    "确认清理", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);
                if (r != DialogResult.Yes) return;
            }

            SetBusy(true);
            _log.Clear();

            Thread t = new Thread(delegate()
            {
                try
                {
                    CleanResult result = Cleaner.RunAll(dryRun, Append);
                    if (dryRun)
                    {
                        Append(string.Empty);
                        Append("预览结束，没有删除任何文件。");
                    }
                }
                catch (Exception ex)
                {
                    Append("出错: " + ex.Message);
                    Log.Write("error: " + ex);
                }
                finally
                {
                    if (IsHandleCreated && !IsDisposed)
                    {
                        BeginInvoke((MethodInvoker)delegate
                        {
                            SetBusy(false);
                            RefreshStatus();
                        });
                    }
                }
            });
            t.IsBackground = true;
            t.Start();
        }

        private void InstallTask()
        {
            string self = Application.ExecutablePath;
            string target = Paths.ExePath;

            try
            {
                if (!string.Equals(self, target, StringComparison.OrdinalIgnoreCase))
                {
                    File.Copy(self, target, true);
                    Append("已部署到: " + target);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("无法把程序复制到 " + target + "\r\n\r\n" + ex.Message, "安装失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            string error = TaskInstaller.Install(target);
            if (error != null)
            {
                MessageBox.Show(error, "注册计划任务失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            RefreshStatus();
            MessageBox.Show("已安装每日提醒。\r\n\r\n每天 06:00 起每小时检查一次，到次日 02:00 结束；\r\n命中后弹窗问你要不要清理，每天最多问 3 次。",
                "安装完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void RemoveTask()
        {
            string error = TaskInstaller.Uninstall();
            if (error != null)
            {
                MessageBox.Show(error, "移除失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            RefreshStatus();
            MessageBox.Show("已移除每日提醒。", "完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void OpenLog()
        {
            try
            {
                Directory.CreateDirectory(Paths.LogDir);
                Process.Start("explorer.exe", "\"" + Paths.LogDir + "\"");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "无法打开日志目录", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}

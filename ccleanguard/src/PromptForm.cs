using System;
using System.Drawing;
using System.Windows.Forms;

namespace CCleanGuard
{
    /// <summary>A modal yes/no dialog that closes itself after a timeout.</summary>
    internal sealed class PromptForm : Form
    {
        private readonly Timer _timer;
        private int _secondsLeft;
        private readonly Label _countdown;

        public bool Confirmed { get; private set; }

        public static bool Ask(string title, string message, int timeoutSeconds)
        {
            using (PromptForm f = new PromptForm(title, message, timeoutSeconds))
            {
                f.ShowDialog();
                return f.Confirmed;
            }
        }

        private PromptForm(string title, string message, int timeoutSeconds)
        {
            Text = title;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterScreen;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = true;
            TopMost = true;
            ClientSize = new Size(520, 250);
            Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
            BackColor = Color.White;

            Label body = new Label();
            body.Text = message;
            body.AutoSize = false;
            body.Location = new Point(20, 18);
            body.Size = new Size(480, 140);
            body.TextAlign = ContentAlignment.TopLeft;
            Controls.Add(body);

            _countdown = new Label();
            _countdown.AutoSize = false;
            _countdown.Size = new Size(200, 22);
            _countdown.Location = new Point(20, 205);
            _countdown.ForeColor = Color.DimGray;
            Controls.Add(_countdown);

            Button yes = new Button();
            yes.Text = "立即清理";
            yes.Size = new Size(110, 34);
            yes.Location = new Point(270, 200);
            yes.DialogResult = DialogResult.None;
            yes.Click += delegate { Confirmed = true; Close(); };
            Controls.Add(yes);

            Button no = new Button();
            no.Text = "今天不用";
            no.Size = new Size(110, 34);
            no.Location = new Point(390, 200);
            no.Click += delegate { Confirmed = false; Close(); };
            Controls.Add(no);

            AcceptButton = yes;
            CancelButton = no;

            _secondsLeft = timeoutSeconds;
            _timer = new Timer();
            _timer.Interval = 1000;
            _timer.Tick += delegate
            {
                _secondsLeft--;
                if (_secondsLeft <= 0) { Confirmed = false; Close(); return; }
                UpdateCountdown();
            };
            UpdateCountdown();
        }

        private void UpdateCountdown()
        {
            if (_secondsLeft <= 0) { _countdown.Text = string.Empty; return; }
            int min = _secondsLeft / 60;
            int sec = _secondsLeft % 60;
            _countdown.Text = "无操作将自动关闭（" + min.ToString("0") + ":" + sec.ToString("00") + "）";
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            if (_secondsLeft > 0) _timer.Start();
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            if (_timer != null) { _timer.Stop(); _timer.Dispose(); }
            base.OnFormClosed(e);
        }
    }
}

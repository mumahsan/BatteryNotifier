using System;
using System.Drawing;
using System.Windows.Forms;
using Microsoft.Win32;

namespace BatteryTrayApp
{
    public class BatteryTrayContext : ApplicationContext
    {
        private NotifyIcon _tray;
        private System.Windows.Forms.Timer _timer;
        private OverlayForm _overlay;
        private bool _notifiedHigh = false;
        private bool _notifiedLow = false;

        public BatteryTrayContext()
        {
            _tray = new NotifyIcon
            {
                Visible = true,
                ContextMenuStrip = new ContextMenuStrip()
            };

            _tray.ContextMenuStrip.Items.Add("Exit", null, (s, e) => ExitApplication());

            _overlay = new OverlayForm();
            _overlay.Show();

            _timer = new System.Windows.Forms.Timer { Interval = 10000 }; // every 10 sec
            _timer.Tick += (s, e) => UpdateBatteryStatus();
            _timer.Start();

            AddToStartup();
            UpdateBatteryStatus();
        }

        private void ExitApplication()
        {
            _timer?.Stop();
            _overlay?.Close();
            _tray.Visible = false;
            Application.Exit();
        }

        private void AddToStartup()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true))
                {
                    key.SetValue("BatteryTrayApp", Application.ExecutablePath);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to add to startup: " + ex.Message,
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private Icon CreateTrayIcon(int percent)
        {
            Color textColor;
            if (percent > 50) textColor = Color.LimeGreen;
            else if (percent > 30) textColor = Color.Gold;
            else textColor = Color.Red;

            using (Bitmap bmp = new Bitmap(16, 16))
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.Transparent);
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

                string text = percent.ToString();
                using (Font font = new Font("Segoe UI", 8, FontStyle.Bold, GraphicsUnit.Pixel))
                using (Brush brush = new SolidBrush(textColor))
                using (Pen outline = new Pen(Color.Black, 2f) { LineJoin = System.Drawing.Drawing2D.LineJoin.Round })
                {
                    var gp = new System.Drawing.Drawing2D.GraphicsPath();
                    gp.AddString(text, font.FontFamily, (int)FontStyle.Bold,
                                 font.SizeInPoints * 96f / 72f, new PointF(0, 0),
                                 StringFormat.GenericTypographic);
                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                    g.DrawPath(outline, gp);
                    g.FillPath(brush, gp);
                }

                return Icon.FromHandle(bmp.GetHicon());
            }
        }

        private void UpdateBatteryStatus()
        {
            var ps = SystemInformation.PowerStatus;
            int percent = (int)Math.Round(ps.BatteryLifePercent * 100.0);
            bool charging = ps.PowerLineStatus == PowerLineStatus.Online;

            _tray.Text = $"{percent}% {(charging ? "Charging" : "On battery")}";

            Icon oldIcon = _tray.Icon;
            _tray.Icon = CreateTrayIcon(percent);
            oldIcon?.Dispose();

            _overlay.UpdateText(percent + "%");

            if (percent > 80 && !_notifiedHigh)
            {
                ShowBalloon("Battery Alert", $"Battery is above 80% ({percent}%)");
                _notifiedHigh = true;
            }
            else if (percent <= 80) _notifiedHigh = false;

            if (percent < 30 && !_notifiedLow)
            {
                ShowBalloon("Battery Alert", $"Battery is below 30% ({percent}%)");
                _notifiedLow = true;
            }
            else if (percent >= 30) _notifiedLow = false;
        }

        private void ShowBalloon(string title, string message)
        {
            _tray.BalloonTipTitle = title;
            _tray.BalloonTipText = message;
            _tray.BalloonTipIcon = ToolTipIcon.Info;
            _tray.ShowBalloonTip(5000);
        }
    }
}

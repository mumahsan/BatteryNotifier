// Build: dotnet new winforms -n BatteryTrayTmp --framework net8.0-windows --no-restore
//        Replace Program.cs content with this file's content (or add as a new .cs file and remove default Form).
//        dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true
//        The published EXE will be in bin/Release/net8.0-windows/win-x64/publish
//
// Alternatively, compile with Visual Studio (Windows Forms, .NET 8, Single-file optional).

using System;
using System.Drawing;
using System.Windows.Forms;
using Microsoft.Win32;
using System.Diagnostics;

internal static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new BatteryTrayContext());
    }
}

internal sealed class BatteryTrayContext : ApplicationContext
{
    private readonly NotifyIcon _tray;
    private readonly System.Windows.Forms.Timer _timer;
    private bool _notifiedHigh = false; // lock until we leave the high band
    private bool _notifiedLow = false;  // lock until we leave the low band
    private const int HIGH_THRESHOLD = 80;
    private const int LOW_THRESHOLD = 30;

    // Registry Run path for per-user startup
    private const string RUN_KEY_PATH = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RUN_VALUE_NAME = "BatteryTray";

    public BatteryTrayContext()
    {
        // Context menu
        var menu = new ContextMenuStrip();
        var startOnBootItem = new ToolStripMenuItem("Start with Windows") { Checked = IsRegisteredForStartup() };
        startOnBootItem.Click += (s, e) =>
        {
            if (startOnBootItem.Checked)
            { // currently on, turn off
                RemoveFromStartup();
                startOnBootItem.Checked = false;
            }
            else
            {
                AddToStartup();
                startOnBootItem.Checked = true;
            }
        };
        var showNowItem = new ToolStripMenuItem("Test notification");
        showNowItem.Click += (s, e) =>
        {
            var ps = SystemInformation.PowerStatus;
            int percent = (int)Math.Round(ps.BatteryLifePercent * 100.0);
            bool charging = ps.PowerLineStatus == PowerLineStatus.Online;
            ShowBalloon("Battery Tray", $"Current battery: {percent}% {(charging ? "(Charging)" : "(On battery)")}");
        };

        var exitItem = new ToolStripMenuItem("Exit");
        exitItem.Click += (s, e) => ExitThread();

        menu.Items.Add(startOnBootItem);
        menu.Items.Add(showNowItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(exitItem);

        // Tray icon
        _tray = new NotifyIcon
        {
            Visible = true,
            ContextMenuStrip = menu
        };
        _tray.DoubleClick += (s, e) => ShowBalloon("Battery", ComposeStatusText());

        // Update immediately, then on an interval
        _timer = new System.Windows.Forms.Timer { Interval = 30_000 }; // 30 seconds, low CPU, no flicker
        _timer.Tick += (s, e) => SafeUpdate();
        SafeUpdate();
        _timer.Start();

    }
    private string ComposeStatusText()
    {
        var ps = SystemInformation.PowerStatus;
        int percent = (int)Math.Round(ps.BatteryLifePercent * 100.0);
        bool charging = ps.PowerLineStatus == PowerLineStatus.Online;
        return $"{percent}% {(charging ? "(Charging)" : "(On battery)")}";
    }

    protected override void ExitThreadCore()
    {
        _timer?.Stop();
        _timer?.Dispose();
        if (_tray != null)
        {
            _tray.Visible = false;
            _tray.Icon?.Dispose();
            _tray.Dispose();
        }
        base.ExitThreadCore();
    }

    private void SafeUpdate()
    {
        try
        {
            UpdateTray();
        }
        catch
        {
            // Avoid any unexpected crash in background; swallow to keep the app running.
        }
    }

    private void UpdateTray()
    {
        var ps = SystemInformation.PowerStatus;
        int percent = (int)Math.Round(ps.BatteryLifePercent * 100.0);
        bool charging = ps.PowerLineStatus == PowerLineStatus.Online;

        // Tooltip shows time & date + detailed status
        _tray.Text = BuildTooltip(percent, charging);

        // Draw a tiny text-based icon with a small battery glyph
        var icon = CreateBatteryIcon(percent, charging);
        var old = _tray.Icon;
        _tray.Icon = icon;
        old?.Dispose();

        // Threshold notifications with hysteresis: notify once per crossing
        HandleNotifications(percent, charging);
    }

    private string BuildTooltip(int percent, bool charging)
    {
        return $"{percent}% {(charging ? "(Charging)" : "(On battery)")}";
    }

    private void HandleNotifications(int percent, bool charging)
    {
        // High threshold
        if (percent >= HIGH_THRESHOLD && !_notifiedHigh)
        {
            ShowBalloon("Battery Status", $"Battery is above {HIGH_THRESHOLD}% (Currently: {percent}%).");
            _notifiedHigh = true;
        }
        else if (percent <= HIGH_THRESHOLD - 2) // hysteresis
        {
            _notifiedHigh = false;
        }

        // Low threshold
        if (percent <= LOW_THRESHOLD && !_notifiedLow)
        {
            ShowBalloon("Battery Warning", $"Battery is below {LOW_THRESHOLD}% (Currently: {percent}%).");
            _notifiedLow = true;
        }
        else if (percent >= LOW_THRESHOLD + 2)
        {
            _notifiedLow = false;
        }
    }


    private void ShowBalloon(string title, string message, int timeoutMs = 4000)
    {
        // Uses classic tray balloon (lightweight; no extra packages; works fine on Win11)
        _tray.BalloonTipTitle = title;
        _tray.BalloonTipText = message;
        _tray.BalloonTipIcon = ToolTipIcon.Info;
        _tray.ShowBalloonTip(timeoutMs);
    }

    private static Icon CreateBatteryIcon(int percent, bool charging)
    {
        const int size = 32;
        using var bmp = new Bitmap(size, size);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);

            // Battery outline
            var bodyRect = new Rectangle(3, 8, 24, 16);
            using var outlinePen = new Pen(Color.Black, 2f);
            g.DrawRoundedRectangle(outlinePen, bodyRect, 3);

            // Battery nub
            using var nubBrush = new SolidBrush(Color.Black);
            g.FillRectangle(nubBrush, new Rectangle(27, 12, 4, 8));

            // Fill
            int fillWidth = (int)Math.Round((bodyRect.Width - 4) * Math.Clamp(percent, 0, 100) / 100.0);
            var fillRect = new Rectangle(bodyRect.X + 2, bodyRect.Y + 2, Math.Max(2, fillWidth), bodyRect.Height - 4);

            Color fillColor = percent <= 20 ? Color.Red
                              : percent <= 40 ? Color.Orange
                              : percent <= 80 ? Color.Green
                              : Color.ForestGreen;

            using var fillBrush = new SolidBrush(fillColor);
            g.FillRectangle(fillBrush, fillRect);

            // Charging bolt
            if (charging)
            {
                using var boltBrush = new SolidBrush(Color.Yellow);
                Point[] bolt =
                {
                new Point(14, 10), new Point(12, 17), new Point(16, 17),
                new Point(14, 22), new Point(20, 14), new Point(16, 14)
            };
                g.FillPolygon(boltBrush, bolt);
                using var boltPen = new Pen(Color.Black, 1f);
                g.DrawPolygon(boltPen, bolt);
            }

            // Bigger percentage text
            using var font = new Font(SystemFonts.MessageBoxFont.FontFamily, 14.0f, FontStyle.Bold, GraphicsUnit.Pixel);
            string text = percent.ToString();
            var sizeF = g.MeasureString(text, font);

            float x = (size - sizeF.Width) / 2f;
            float y = (size - sizeF.Height) / 2f + 6;

            // Draw black outline for contrast
            using var gp = new System.Drawing.Drawing2D.GraphicsPath();
            gp.AddString(text, font.FontFamily, (int)FontStyle.Bold,
                         font.SizeInPoints * 96f / 72f,
                         new PointF(x, y),
                         StringFormat.GenericTypographic);

            using var outlinePenText = new Pen(Color.Black, 3f) { LineJoin = System.Drawing.Drawing2D.LineJoin.Round };
            g.DrawPath(outlinePenText, gp);

            using var whiteBrush = new SolidBrush(Color.White);
            g.FillPath(whiteBrush, gp);
        }

        IntPtr hIcon = bmp.GetHicon();
        try
        {
            using var tempIcon = Icon.FromHandle(hIcon);
            return (Icon)tempIcon.Clone();
        }
        finally
        {
            NativeMethods.DestroyIcon(hIcon);
        }
    }


    // --- Startup registration (per-user HKCU\...\Run) ---
    private static void AddToStartup()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RUN_KEY_PATH, writable: true)
                          ?? Registry.CurrentUser.CreateSubKey(RUN_KEY_PATH, true);
            string exe = Application.ExecutablePath;
            key.SetValue(RUN_VALUE_NAME, $"\"{exe}\"");
        }
        catch { /* ignore (permissions, etc.) */ }
    }

    private static bool IsRegisteredForStartup()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RUN_KEY_PATH, writable: false);
            if (key == null) return false;
            var val = key.GetValue(RUN_VALUE_NAME) as string;
            return !string.IsNullOrWhiteSpace(val);
        }
        catch { return false; }
    }

    private static void RemoveFromStartup()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RUN_KEY_PATH, writable: true);
            key?.DeleteValue(RUN_VALUE_NAME, false);
        }
        catch { /* ignore */ }
    }
}

internal static class NativeMethods
{
    [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
    internal static extern bool DestroyIcon(IntPtr hIcon);
}

// Small helper for rounded rectangles on Graphics
internal static class GraphicsExtensions
{
    public static void DrawRoundedRectangle(this Graphics g, Pen pen, Rectangle bounds, int radius)
    {
        using var path = RoundedRect(bounds, radius);
        g.DrawPath(pen, path);
    }

    public static System.Drawing.Drawing2D.GraphicsPath RoundedRect(Rectangle bounds, int radius)
    {
        int d = radius * 2;
        var path = new System.Drawing.Drawing2D.GraphicsPath();
        path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
        path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
        path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
}

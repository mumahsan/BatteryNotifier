using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace BatteryTrayApp
{
    public class OverlayForm : Form
    {
        private Label _label;

        // WinAPI constants & imports for click-through
        private const int WS_EX_TRANSPARENT = 0x20;
        private const int WS_EX_LAYERED = 0x80000;

        [DllImport("user32.dll", SetLastError = true)]
        static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll")]
        static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        public OverlayForm()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            BackColor = Color.Black;
            TransparencyKey = Color.Black;

            _label = new Label
            {
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 20, FontStyle.Bold),
                AutoSize = true,
                BackColor = Color.Transparent
            };

            Controls.Add(_label);

            Load += (s, e) =>
            {
                EnableClickThrough();
                PositionOverlay();
            };
            Resize += (s, e) => PositionOverlay();
        }

        public void UpdateText(string text)
        {
            _label.Text = text;
            PositionOverlay();
        }

        private void EnableClickThrough()
        {
            int exStyle = GetWindowLong(this.Handle, -20);
            SetWindowLong(this.Handle, -20, exStyle | WS_EX_LAYERED | WS_EX_TRANSPARENT);
        }

        private void PositionOverlay()
        {
            // Get taskbar position
            var taskbarHandle = FindWindow("Shell_TrayWnd", null);
            if (taskbarHandle != IntPtr.Zero && GetWindowRect(taskbarHandle, out RECT rect))
            {
                var screen = Screen.PrimaryScreen.Bounds;
                int margin = 8;

                // Detect where taskbar is located
                if (rect.Top > screen.Top) // Bottom
                {
                    Location = new Point(rect.Right - 80 - margin, rect.Top - 40 - margin);
                }
                else if (rect.Bottom < screen.Bottom) // Top
                {
                    Location = new Point(rect.Right - 80 - margin, rect.Bottom + margin);
                }
                else if (rect.Left > screen.Left) // Right
                {
                    Location = new Point(rect.Left - 80 - margin, rect.Bottom - 40 - margin);
                }
                else // Left
                {
                    Location = new Point(rect.Right + margin, rect.Bottom - 40 - margin);
                }
            }
            else
            {
                // Fallback: bottom-right
                var screen = Screen.PrimaryScreen.WorkingArea;
                Location = new Point(screen.Right - 80, screen.Bottom - 40);
            }

            Size = new Size(80, 40);
            _label.Location = new Point(0, 0);
        }
    }
}

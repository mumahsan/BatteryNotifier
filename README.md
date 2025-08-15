Build & run

In Visual Studio, create Windows Forms App (.NET 6/7/8).

Replace the auto-generated Program.cs with the code above (or add a new .cs file and remove the default Program).

Build → Run.

Tray icon appears with menu (Start with Windows / Test notification / Redock overlay / Exit).

Overlay shows big XX% beside the taskbar, color-coded.

CTRL+Drag the overlay to place it anywhere (position is remembered).

Clicks pass through the overlay normally (no pointer flicker).

Notifications appear when >80% or <30%, including current %.

Registers itself in HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Run.

Tips / tweaks

Change poll interval: modify _pollTimer.Interval (ms).

Change thresholds: edit HIGH_THRESHOLD / LOW_THRESHOLD.

Make overlay always docked: remove custom-position logic and the CTRL-drag handlers.

If you prefer not to auto-enable startup on first launch, comment out:

if (!IsStartupEnabled()) EnableStartup();

using System;
using System.Drawing;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace TrayPingMonitor;

public sealed class MainApplicationContext : ApplicationContext
{
    private readonly NotifyIcon _notifyIcon = new();
    private readonly ToolStripMenuItem _miStartStop = new("Start");
    private readonly ToolStripMenuItem _miRunAtStartup = new("Run at startup") { CheckOnClick = true };
    private readonly Control _uiInvoker = new();

    private readonly Icon _iconGray;
    private readonly Icon _iconGreen;
    private readonly Icon _iconYellow;
    private readonly Icon _iconRed;
    private Icon? _dynamicIcon;

    private AppSettings _settings;
    private readonly PingMonitor _monitor = new();

    public MainApplicationContext()
    {
        _settings = SettingsStore.Load();

        // Create icons once.
        _iconGray = TrayIconFactory.CreateCircleIcon(Color.Gray);
        _iconGreen = TrayIconFactory.CreateCircleIcon(Color.LimeGreen);
        _iconYellow = TrayIconFactory.CreateCircleIcon(Color.Gold);
        _iconRed = TrayIconFactory.CreateCircleIcon(Color.IndianRed);

        _notifyIcon.Icon = _iconGray;
        _notifyIcon.Visible = true;
        _notifyIcon.Text = "Tray Ping Monitor";

        // Menu
        var menu = new ContextMenuStrip();
        menu.Items.Add(new ToolStripMenuItem("Set IP / Settings…", null, (_, _) => ShowSettings()));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(_miStartStop);
        menu.Items.Add(_miRunAtStartup);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(new ToolStripMenuItem("Exit", null, async (_, _) => await ExitAsync()));

        _miStartStop.Click += async (_, _) => await ToggleStartStopAsync();
        _miRunAtStartup.Checked = SafeGetStartupCheckedInitial();
        _miRunAtStartup.CheckedChanged += (_, _) => OnRunAtStartupToggled();

        _notifyIcon.ContextMenuStrip = menu;
        _notifyIcon.DoubleClick += (_, _) => ShowSettings();

        _monitor.Updated += MonitorOnUpdated;

        ApplySettingsToMonitor();

        // First run / no host: show settings window.
        if (string.IsNullOrWhiteSpace(_settings.Host))
        {
            ShowSettings(firstRun: true);
        }
        else
        {
            // Start automatically when host is set.
            _monitor.Start();
            UpdateStartStopMenu();
            UpdateTrayVisuals();
        }
    }

    private bool SafeGetStartupCheckedInitial()
    {
        // Trust persisted setting, but reconcile with registry if possible.
        bool registry = false;
        try { registry = StartupManager.IsRunAtStartupEnabled(); } catch { }
        if (_settings.RunAtStartup != registry)
        {
            _settings.RunAtStartup = registry;
            SettingsStore.Save(_settings);
        }
        return _settings.RunAtStartup;
    }

    private void ApplySettingsToMonitor()
    {
        _monitor.Configure(
            host: _settings.Host ?? "",
            intervalMs: _settings.IntervalMs,
            latencyThresholdMs: _settings.LatencyThresholdMs,
            windowSize: _settings.WindowSize
        );
    }

    private void ShowSettings(bool firstRun = false)
    {
        using var f = new SettingsForm(_settings);
        var result = f.ShowDialog();

        if (result == DialogResult.OK)
        {
            _settings.Host = f.Host;
            _settings.IntervalMs = f.IntervalMs;
            _settings.LatencyThresholdMs = f.LatencyThresholdMs;

            SettingsStore.Save(_settings);
            ApplySettingsToMonitor();

            // If we were stopped, keep stopped; otherwise keep running.
            if (!_monitor.IsRunning && !string.IsNullOrWhiteSpace(_settings.Host))
            {
                _monitor.Start();
                UpdateStartStopMenu();
            }

            UpdateTrayVisuals();
        }
        else if (firstRun)
        {
            // If first run and they cancel, keep running but in gray "no IP configured".
            UpdateTrayVisuals();
        }
    }

    private async Task ToggleStartStopAsync()
    {
        if (_monitor.IsRunning)
            await _monitor.StopAsync();
        else
            _monitor.Start();

        UpdateStartStopMenu();
        UpdateTrayVisuals();
    }

    private void UpdateStartStopMenu()
    {
        _miStartStop.Text = _monitor.IsRunning ? "Stop" : "Start";
    }

    private void OnRunAtStartupToggled()
    {
        _settings.RunAtStartup = _miRunAtStartup.Checked;
        try
        {
            StartupManager.SetRunAtStartup(_settings.RunAtStartup);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Failed to update startup setting:\n{ex.Message}",
                "Tray Ping Monitor",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);

            // Revert checkbox
            _miRunAtStartup.CheckedChanged -= (_, _) => OnRunAtStartupToggled();
            _miRunAtStartup.Checked = !_miRunAtStartup.Checked;
            _miRunAtStartup.CheckedChanged += (_, _) => OnRunAtStartupToggled();
            _settings.RunAtStartup = _miRunAtStartup.Checked;
        }

        SettingsStore.Save(_settings);
    }

    private void MonitorOnUpdated()
    {
        // PingMonitor raises Updated from a background thread.
        // Marshal to UI thread.
        if (Application.MessageLoop)
        {
            try
            {
                var _ = _notifyIcon?.ContextMenuStrip; // simple access to keep object alive
                // BeginInvoke via a hidden control: use the tray icon's menu as invoke target.
                _notifyIcon.ContextMenuStrip?.BeginInvoke((Action)UpdateTrayVisuals);
            }
            catch
            {
                // fallback: try direct
                UpdateTrayVisuals();
            }
        }
        else
        {
            UpdateTrayVisuals();
        }
    }

    private void UpdateTrayVisuals()
    {
        var state = _monitor.GetHealthState();

        // Pick background color by state
        var bg = state switch
        {
            PingMonitor.HealthState.GreenOk => Color.LimeGreen,
            PingMonitor.HealthState.YellowDegraded => Color.Gold,
            PingMonitor.HealthState.RedDown => Color.IndianRed,
            _ => Color.Gray
        };


        // Text to show inside icon
        // - success: show ms (rounded)
        // - fail: show "X"
        // - no host: "--"
        var (_, last) = _monitor.Snapshot();
        double loss = _monitor.LossPercent();

        string text;    
        if (string.IsNullOrWhiteSpace(_settings.Host))
        {
            text = "--";
        }
        else if (last is null)
        {
            text = "..";
        }
        else if (state == PingMonitor.HealthState.RedDown || state == PingMonitor.HealthState.YellowDegraded)
        {
            // Show loss for yellow/red
            int lossInt = (int)Math.Round(loss, MidpointRounding.AwayFromZero);
            lossInt = Math.Clamp(lossInt, 0, 99);

            if (lossInt > 0)
            {
                // e.g. L5, L25, L99
                text = $"L{lossInt}";
            }
            else
            {
                // Yellow can also be due to slow (loss 0). Red with loss 0 is possible early.
                // Keep it simple:
                text = state == PingMonitor.HealthState.RedDown ? "X" : "S";
                // "S" = slow
            }
        }
        else
        {
            // Green/Gray: show compact latency
            if (!last.Success)
            {
                text = "X";
            }
            else
            {
                var ms = (int)(last.RttMs ?? 0);

                if (ms < 100)
                {
                    text = ms.ToString(); // 0..99
                }
                else if (ms < 1000)
                {
                    // show ms/10 as two digits: 150ms => "15"
                    int tens = (ms + 5) / 10; // rounded
                    tens = Math.Clamp(tens, 10, 99);
                    text = tens.ToString("00");
                }
                else
                {
                    int s = (ms + 500) / 1000; // rounded seconds
                    s = Math.Clamp(s, 1, 9);
                    text = $"{s}s";
                }
            }
        }
        // Create + assign dynamic icon
        var newIcon = TrayIconFactory.CreateStatusIcon(bg, text);

        // Dispose old dynamic icon safely
        var old = _dynamicIcon;
        _dynamicIcon = newIcon;
        _notifyIcon.Icon = newIcon;
        old?.Dispose();

        _notifyIcon.Text = BuildTooltipSafe();
    }


    private string BuildTooltipSafe()
    {
        // NotifyIcon.Text max is ~63 chars in many Windows versions.
        // Requirement wants a rich tooltip; we'll keep it concise but informative.
        // You can also show full details via BalloonTip if desired, but not required.
        var host = string.IsNullOrWhiteSpace(_settings.Host) ? "(none)" : _settings.Host.Trim();

        var (samples, last) = _monitor.Snapshot();
        var loss = _monitor.LossPercent();
        var updated = (last?.At.LocalDateTime ?? DateTime.Now).ToString("HH:mm:ss", CultureInfo.InvariantCulture);

        string lastText = last is null
            ? "n/a"
            : last.Success
                ? $"{last.RttMs}ms"
                : "timeout";

        // We’ll fit as much as possible; Windows may truncate automatically.
        var tooltip = $"IP: {host} | Last: {lastText} | Loss(20): {loss:0}% | Updated: {updated}";
        return tooltip.Length <= 63 ? tooltip : tooltip[..63];
    }

    private async Task ExitAsync()
    {
        _notifyIcon.Visible = false;

        try { await _monitor.StopAsync(); } catch { }
        _monitor.Dispose();

        _notifyIcon.Dispose();

        _iconGray.Dispose();
        _iconGreen.Dispose();
        _iconYellow.Dispose();
        _iconRed.Dispose();
        _dynamicIcon?.Dispose();
        _dynamicIcon = null;
        ExitThread();
    }
}

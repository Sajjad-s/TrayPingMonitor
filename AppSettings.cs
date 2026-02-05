namespace TrayPingMonitor;

public sealed class AppSettings
{
    public string Host { get; set; } = "";               // IP/hostname
    public int IntervalMs { get; set; } = 1000;          // default 1s
    public int LatencyThresholdMs { get; set; } = 150;   // default 150ms
    public bool RunAtStartup { get; set; } = false;

    // Rolling window size is fixed to 20 per requirements.
    public int WindowSize { get; set; } = 20;
}

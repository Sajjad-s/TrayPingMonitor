using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;

namespace TrayPingMonitor;

public static class StartupManager
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "TrayPingMonitor";

    public static void SetRunAtStartup(bool enabled)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true)
                      ?? Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);

        if (key is null) return;

        if (!enabled)
        {
            key.DeleteValue(ValueName, throwOnMissingValue: false);
            return;
        }

        // Path to current exe; quote it for safety.
        var exePath = GetExecutablePath();
        key.SetValue(ValueName, $"\"{exePath}\"");
    }

    public static bool IsRunAtStartupEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
        if (key is null) return false;
        var v = key.GetValue(ValueName) as string;
        return !string.IsNullOrWhiteSpace(v);
    }

    private static string GetExecutablePath()
    {
        // Works for typical dotnet run + published app.
        var p = Process.GetCurrentProcess().MainModule?.FileName;
        if (!string.IsNullOrWhiteSpace(p) && File.Exists(p))
            return p;

        return Environment.ProcessPath ?? "TrayPingMonitor.exe";
    }
}

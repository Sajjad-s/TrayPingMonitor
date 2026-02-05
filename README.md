# TrayPingMonitor

A minimal Windows 10/11 system-tray (notification area) app that continuously pings a user-defined host (IPv4/IPv6/hostname) and shows status via a colored tray icon:

- **Green** = reachable
- **Yellow** = degraded (slow > threshold and/or packet loss in last N pings)
- **Red** = unreachable
- **Gray** = unknown / starting / no host configured

The tray icon can also display:
- **Latency** (compact) when **Green/Gray**
- **Loss** (e.g. `L5`, `L25`) when **Yellow/Red**

Right-click menu:
- **Set IP / Settings…**
- **Start / Stop**
- **Run at startup** (HKCU Run key)
- **Exit**

Ping runs asynchronously (no UI freezes) using `System.Net.NetworkInformation.Ping`.

---

## Requirements

- Windows 10/11
- .NET SDK 8.x (LTS recommended)

Check your SDK:

```powershell
dotnet --list-sdks

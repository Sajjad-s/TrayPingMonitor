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
```

---

## Build & Run (CLI)

From the repo folder:

```powershell
dotnet restore
dotnet build
dotnet run
```

On first run, the settings window will appear. Enter:
- Host (e.g. `4.2.2.4`, `1.1.1.1`, `google.com`, IPv6 is supported)
- Interval (default 1000ms)
- Slow threshold (default 150ms)

Then the app runs in the system tray.

---

## Build & Run (Visual Studio)

1. Open the folder or the `.csproj` in Visual Studio
2. Build and run

---

## Settings / Persistence

Settings are saved to:

`%AppData%\TrayPingMonitor\settings.json`

Stored values:
- Host
- Interval (ms)
- Latency threshold (ms)
- Run-at-startup toggle

---

## Run at Startup

The “Run at startup” toggle registers/unregisters the app using:

`HKCU\Software\Microsoft\Windows\CurrentVersion\Run`

This is per-user and does not require admin privileges.

---

## Publish (portable EXE)

### Self-contained single-file (recommended)

Build a single EXE that runs on machines without .NET installed:

```powershell
dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true
```

Output:

`bin\Release\net8.0-windows\win-x64\publish\`

Distribute `TrayPingMonitor.exe` (and any other files in that folder if present).

### Framework-dependent (smaller output)

Requires .NET runtime on the target machine:

```powershell
dotnet publish -c Release -r win-x64 --self-contained false
```

---

## Create an Installer

### Option A: Inno Setup (simple .exe installer)

1. Publish first (see above).
2. Create `installer.iss` in the repo root:

```ini
[Setup]
AppName=Tray Ping Monitor
AppVersion=1.0.0
DefaultDirName={pf}\TrayPingMonitor
DefaultGroupName=Tray Ping Monitor
OutputBaseFilename=TrayPingMonitorSetup
Compression=lzma
SolidCompression=yes

[Files]
Source: "bin\Release\net8.0-windows\win-x64\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\Tray Ping Monitor"; Filename: "{app}\TrayPingMonitor.exe"
Name: "{userdesktop}\Tray Ping Monitor"; Filename: "{app}\TrayPingMonitor.exe"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop icon"; GroupDescription: "Additional icons:"; Flags: unchecked

[Run]
Filename: "{app}\TrayPingMonitor.exe"; Description: "Launch Tray Ping Monitor"; Flags: nowait postinstall skipifsilent
```

3. Compile with the Inno Setup Compiler.
4. Distribute `TrayPingMonitorSetup.exe`.

### Option B: MSIX (modern packaging)

MSIX provides clean install/uninstall + Start Menu integration. Best created via Visual Studio’s packaging project.  
(Requires signing for distribution.)

---

## Troubleshooting

### `dotnet` not found / “No .NET SDKs were found”
Install the .NET SDK (not just runtime), then reopen PowerShell:

```powershell
dotnet --info
```

### Tray tooltip text looks cut off
Windows tray tooltips have a small character limit. The full details are still tracked internally; consider adding a menu item or balloon tip if you want full text always visible.

---


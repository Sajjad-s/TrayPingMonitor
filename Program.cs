using System;
using System.Windows.Forms;

namespace TrayPingMonitor;

internal static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new MainApplicationContext());
    }
}

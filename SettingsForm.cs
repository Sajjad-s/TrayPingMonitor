using System;
using System.Net;
using System.Windows.Forms;

namespace TrayPingMonitor;

public sealed class SettingsForm : Form
{
    private readonly TextBox _txtHost = new();
    private readonly NumericUpDown _numInterval = new();
    private readonly NumericUpDown _numLatency = new();
    private readonly Button _btnSave = new();
    private readonly Button _btnCancel = new();
    private readonly Label _lblError = new();

    public string Host => _txtHost.Text.Trim();
    public int IntervalMs => (int)_numInterval.Value;
    public int LatencyThresholdMs => (int)_numLatency.Value;

    public SettingsForm(AppSettings settings)
    {
        Text = "Tray Ping Monitor - Settings";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        Width = 420;
        Height = 240;

        var lblHost = new Label { Left = 12, Top = 16, Width = 140, Text = "IP / Hostname:" };
        _txtHost.SetBounds(160, 12, 230, 24);

        var lblInterval = new Label { Left = 12, Top = 54, Width = 140, Text = "Ping interval (ms):" };
        _numInterval.SetBounds(160, 50, 120, 24);
        _numInterval.Minimum = 250;
        _numInterval.Maximum = 60000;
        _numInterval.Increment = 250;

        var lblLatency = new Label { Left = 12, Top = 92, Width = 140, Text = "Slow threshold (ms):" };
        _numLatency.SetBounds(160, 88, 120, 24);
        _numLatency.Minimum = 1;
        _numLatency.Maximum = 5000;
        _numLatency.Increment = 10;

        _lblError.SetBounds(12, 124, 378, 32);
        _lblError.ForeColor = System.Drawing.Color.Firebrick;

        _btnSave.Text = "Save";
        _btnSave.SetBounds(232, 164, 76, 28);
        _btnSave.Click += (_, _) => OnSave();

        _btnCancel.Text = "Cancel";
        _btnCancel.SetBounds(314, 164, 76, 28);
        _btnCancel.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };

        Controls.AddRange(new Control[]
        {
            lblHost, _txtHost,
            lblInterval, _numInterval,
            lblLatency, _numLatency,
            _lblError, _btnSave, _btnCancel
        });

        // Load settings
        _txtHost.Text = settings.Host ?? "";
        _numInterval.Value = Math.Clamp(settings.IntervalMs, (int)_numInterval.Minimum, (int)_numInterval.Maximum);
        _numLatency.Value = Math.Clamp(settings.LatencyThresholdMs, (int)_numLatency.Minimum, (int)_numLatency.Maximum);

        AcceptButton = _btnSave;
        CancelButton = _btnCancel;
    }

    private void OnSave()
    {
        _lblError.Text = "";

        var host = Host;
        if (string.IsNullOrWhiteSpace(host))
        {
            _lblError.Text = "Please enter an IP address, IPv6 address, or hostname.";
            return;
        }

        if (!IsValidHost(host))
        {
            _lblError.Text = "Host looks invalid. Use a valid IPv4/IPv6 or a hostname.";
            return;
        }

        DialogResult = DialogResult.OK;
        Close();
    }

    private static bool IsValidHost(string host)
    {
        // Accept IPv4/IPv6:
        if (IPAddress.TryParse(host, out _)) return true;

        // Accept hostname (basic):
        var kind = Uri.CheckHostName(host);
        return kind == UriHostNameType.Dns;
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;

namespace TrayPingMonitor;

public sealed class PingMonitor : IDisposable
{
    public sealed record PingSample(DateTimeOffset At, bool Success, long? RttMs, string StatusText);

    private readonly object _lock = new();
    private readonly List<PingSample> _samples = new();

    private CancellationTokenSource? _cts;
    private Task? _loopTask;

    public bool IsRunning => _loopTask is { IsCompleted: false };

    public event Action? Updated;

    public string Host { get; private set; } = "";
    public int IntervalMs { get; private set; } = 1000;
    public int LatencyThresholdMs { get; private set; } = 150;
    public int WindowSize { get; private set; } = 20;

    public void Configure(string host, int intervalMs, int latencyThresholdMs, int windowSize = 20)
    {
        Host = host?.Trim() ?? "";
        IntervalMs = Math.Clamp(intervalMs, 250, 60_000);            // keep sane
        LatencyThresholdMs = Math.Clamp(latencyThresholdMs, 1, 5000);
        WindowSize = Math.Clamp(windowSize, 5, 200);
    }

    public void Start()
    {
        if (IsRunning) return;
        _cts = new CancellationTokenSource();
        _loopTask = Task.Run(() => LoopAsync(_cts.Token));
    }

    public async Task StopAsync()
    {
        if (!IsRunning) return;
        try
        {
            _cts?.Cancel();
            if (_loopTask != null) await _loopTask.ConfigureAwait(false);
        }
        catch { /* ignore */ }
        finally
        {
            _cts?.Dispose();
            _cts = null;
            _loopTask = null;
            OnUpdated();
        }
    }

    public (IReadOnlyList<PingSample> Samples, PingSample? Last) Snapshot()
    {
        lock (_lock)
        {
            var copy = _samples.ToList();
            var last = copy.LastOrDefault();
            return (copy, last);
        }
    }

    public double LossPercent()
    {
        lock (_lock)
        {
            if (_samples.Count == 0) return 0;
            var window = _samples.TakeLast(WindowSize).ToList();
            if (window.Count == 0) return 0;
            var fails = window.Count(s => !s.Success);
            return 100.0 * fails / window.Count;
        }
    }

    public double? AvgLatencyMs()
    {
        lock (_lock)
        {
            var window = _samples.TakeLast(WindowSize).Where(s => s.Success && s.RttMs.HasValue).ToList();
            if (window.Count == 0) return null;
            return window.Average(s => s.RttMs!.Value);
        }
    }

    public enum HealthState { GrayUnknown, GreenOk, YellowDegraded, RedDown }

    public HealthState GetHealthState()
    {
        if (string.IsNullOrWhiteSpace(Host)) return HealthState.GrayUnknown;

        var (_, last) = Snapshot();
        if (last is null) return HealthState.GrayUnknown;

        if (!last.Success) return HealthState.RedDown;

        // Ping succeeded:
        var loss = LossPercent();
        var slow = (last.RttMs ?? long.MaxValue) > LatencyThresholdMs;
        var lossDetected = loss > 0.0; // "packet loss detected over last N pings"
        if (slow || lossDetected) return HealthState.YellowDegraded;

        return HealthState.GreenOk;
    }

    private async Task LoopAsync(CancellationToken ct)
    {
        // Use a single Ping instance per attempt (Ping isn't guaranteed thread-safe across calls).
        while (!ct.IsCancellationRequested)
        {
            if (string.IsNullOrWhiteSpace(Host))
            {
                AddSample(new PingSample(DateTimeOffset.Now, false, null, "no host"));
                await DelaySafe(IntervalMs, ct).ConfigureAwait(false);
                continue;
            }

            PingSample sample = await DoPingAsync(Host, ct).ConfigureAwait(false);
            AddSample(sample);

            await DelaySafe(IntervalMs, ct).ConfigureAwait(false);
        }
    }

    private static async Task DelaySafe(int ms, CancellationToken ct)
    {
        try { await Task.Delay(ms, ct).ConfigureAwait(false); }
        catch { /* canceled */ }
    }

    private async Task<PingSample> DoPingAsync(string host, CancellationToken ct)
    {
        try
        {
            using var ping = new Ping();

            // Timeout slightly under interval, but never < 250ms, never > 5000ms.
            int timeout = Math.Clamp(IntervalMs - 50, 250, 5000);

            // Small payload to keep it light.
            var buffer = new byte[8];
            var opts = new PingOptions(dontFragment: true, ttl: 64);

            var reply = await ping.SendPingAsync(host, timeout, buffer, opts).WaitAsync(ct).ConfigureAwait(false);

            if (reply.Status == IPStatus.Success)
                return new PingSample(DateTimeOffset.Now, true, reply.RoundtripTime, $"{reply.RoundtripTime} ms");

            return new PingSample(DateTimeOffset.Now, false, null, reply.Status.ToString());
        }
        catch (OperationCanceledException)
        {
            return new PingSample(DateTimeOffset.Now, false, null, "canceled");
        }
        catch (Exception ex)
        {
            // DNS issues, invalid host, permissions, etc.
            return new PingSample(DateTimeOffset.Now, false, null, ex.GetType().Name);
        }
    }

    private void AddSample(PingSample sample)
    {
        lock (_lock)
        {
            _samples.Add(sample);
            // Keep a bit more than WindowSize so we still have recent context if WindowSize changes.
            int max = Math.Max(WindowSize * 3, 60);
            if (_samples.Count > max)
                _samples.RemoveRange(0, _samples.Count - max);
        }

        OnUpdated();
    }

    private void OnUpdated() => Updated?.Invoke();

    public void Dispose()
    {
        try { StopAsync().GetAwaiter().GetResult(); } catch { }
    }
}

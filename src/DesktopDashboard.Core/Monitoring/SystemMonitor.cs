using System.Diagnostics;
using DesktopDashboard.Core.Models;

namespace DesktopDashboard.Core.Monitoring;

/// <summary>
/// A source of one metric. <see cref="Read"/> is only ever called from the monitor's polling
/// loop, one call at a time, so implementations may keep unsynchronized state between calls
/// (previous counter values for rates, lazily opened native handles).
/// </summary>
public interface IMetricSource<T> : IDisposable
{
    MetricReading<T> Read();
}

/// <summary>
/// Polls all enabled metric sources on a single background loop and publishes one
/// <see cref="SystemSnapshot"/> per tick. Disabled metrics are not read at all.
/// </summary>
public sealed class SystemMonitor : IAsyncDisposable
{
    private readonly IMetricSource<CpuSample> _cpu;
    private readonly IMetricSource<MemorySample> _memory;
    private readonly IMetricSource<GpuSample> _gpu;
    private readonly IMetricSource<BatterySample> _battery;
    private readonly IMetricSource<NetworkSample> _network;
    private readonly TimeProvider _time;
    private readonly CancellationTokenSource _cts = new();

    private PeriodicTimer? _timer;
    private Task? _loop;
    private TimeSpan _interval = TimeSpan.FromSeconds(1);
    private volatile bool _paused;
    private volatile MetricKind _enabled = MetricKind.All;

    public SystemMonitor(
        IMetricSource<CpuSample> cpu,
        IMetricSource<MemorySample> memory,
        IMetricSource<GpuSample> gpu,
        IMetricSource<BatterySample> battery,
        IMetricSource<NetworkSample> network,
        TimeProvider time)
    {
        _cpu = cpu;
        _memory = memory;
        _gpu = gpu;
        _battery = battery;
        _network = network;
        _time = time;
    }

    /// <summary>Raised on a background thread.</summary>
    public event EventHandler<SystemSnapshot>? SnapshotAvailable;

    public MetricKind Enabled
    {
        get => _enabled;
        set => _enabled = value;
    }

    public TimeSpan Interval
    {
        get => _interval;
        set
        {
            _interval = value;
            if (_timer is { } timer)
            {
                timer.Period = value;
            }
        }
    }

    /// <summary>While paused (e.g. the window is hidden) ticks are skipped and no source is read.</summary>
    public bool IsPaused
    {
        get => _paused;
        set => _paused = value;
    }

    public void Start()
    {
        if (_loop is not null)
        {
            return;
        }

        _timer = new PeriodicTimer(_interval, _time);
        _loop = Task.Run(() => RunAsync(_timer, _cts.Token));
    }

    private async Task RunAsync(PeriodicTimer timer, CancellationToken ct)
    {
        try
        {
            do
            {
                if (!_paused)
                {
                    SnapshotAvailable?.Invoke(this, Collect());
                }
            }
            while (await timer.WaitForNextTickAsync(ct).ConfigureAwait(false));
        }
        catch (OperationCanceledException)
        {
        }
    }

    /// <summary>Reads every enabled source once. Public for tests and diagnostics.</summary>
    public SystemSnapshot Collect()
    {
        var enabled = _enabled;
        return new SystemSnapshot(
            _time.GetLocalNow(),
            ReadSafe(_cpu, enabled.HasFlag(MetricKind.Cpu)),
            ReadSafe(_memory, enabled.HasFlag(MetricKind.Memory)),
            ReadSafe(_gpu, enabled.HasFlag(MetricKind.Gpu)),
            ReadSafe(_battery, enabled.HasFlag(MetricKind.Battery)),
            ReadSafe(_network, enabled.HasFlag(MetricKind.Network)));
    }

    private static MetricReading<T>? ReadSafe<T>(IMetricSource<T> source, bool enabled)
    {
        if (!enabled)
        {
            return null;
        }

        try
        {
            return source.Read();
        }
        catch (Exception ex)
        {
            Trace.TraceError($"{source.GetType().Name} failed: {ex}");
            return MetricReading<T>.Error(ex.Message);
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _cts.CancelAsync().ConfigureAwait(false);
        if (_loop is not null)
        {
            await _loop.ConfigureAwait(false);
        }

        _timer?.Dispose();
        _cts.Dispose();
        _cpu.Dispose();
        _memory.Dispose();
        _gpu.Dispose();
        _battery.Dispose();
        _network.Dispose();
    }
}

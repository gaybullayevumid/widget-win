using System.Diagnostics;
using DesktopDashboard.Core.Interop;
using DesktopDashboard.Core.Models;

namespace DesktopDashboard.Core.Monitoring;

/// <summary>
/// GPU metrics for one adapter, from the same sources Task Manager uses:
/// utilization from the "GPU Engine" counters (busiest engine, summed over processes),
/// dedicated VRAM from "GPU Adapter Memory" plus the DXGI total, and temperature from
/// D3DKMT adapter perf data. Each part degrades to Unsupported on its own.
/// </summary>
public sealed class GpuSource : IMetricSource<GpuSample>
{
    private const string EngineCounter = @"\GPU Engine(*)\Utilization Percentage";
    private const string DedicatedCounter = @"\GPU Adapter Memory(*)\Dedicated Usage";
    private static readonly TimeSpan AdapterRefreshInterval = TimeSpan.FromMinutes(5);

    private IReadOnlyList<GpuAdapterInfo>? _adapters;
    private long _adaptersTimestamp;
    private PdhQuery? _query;
    private nint _engine;
    private nint _dedicated;
    private bool _countersUnsupported;
    private bool _primed;
    private volatile string? _preferredAdapter;

    /// <summary>Adapter name chosen in settings; null picks the GPU with the most dedicated memory.</summary>
    public string? PreferredAdapter
    {
        get => _preferredAdapter;
        set => _preferredAdapter = value;
    }

    public MetricReading<GpuSample> Read()
    {
        var adapter = GpuAdapters.Select(GetAdapters(), _preferredAdapter);
        if (adapter is null)
        {
            return MetricReading<GpuSample>.Unsupported("No hardware GPU found");
        }

        var (utilization, dedicatedUsed) = ReadCounters(adapter);

        var dedicatedTotal = adapter.IsIntegrated
            ? MetricReading<ulong>.Unsupported("Integrated GPU uses shared system memory")
            : MetricReading<ulong>.Ok(adapter.DedicatedVideoMemory);
        if (adapter.IsIntegrated)
        {
            dedicatedUsed = MetricReading<ulong>.Unsupported("Integrated GPU uses shared system memory");
        }

        var temperature = D3dkmt.QueryTemperature(adapter.Luid) is { } celsius
            ? MetricReading<double>.Ok(celsius)
            : MetricReading<double>.Unsupported("Driver does not report GPU temperature");

        return MetricReading<GpuSample>.Ok(new GpuSample(
            adapter.Name,
            adapter.IsIntegrated,
            utilization,
            dedicatedUsed,
            dedicatedTotal,
            temperature));
    }

    private IReadOnlyList<GpuAdapterInfo> GetAdapters()
    {
        if (_adapters is null || Stopwatch.GetElapsedTime(_adaptersTimestamp) > AdapterRefreshInterval)
        {
            _adapters = GpuAdapters.Enumerate();
            _adaptersTimestamp = Stopwatch.GetTimestamp();
        }

        return _adapters;
    }

    private (MetricReading<double> Utilization, MetricReading<ulong> DedicatedUsed) ReadCounters(GpuAdapterInfo adapter)
    {
        if (_countersUnsupported)
        {
            const string reason = "GPU performance counters are not available";
            return (MetricReading<double>.Unsupported(reason), MetricReading<ulong>.Unsupported(reason));
        }

        try
        {
            if (_query is null)
            {
                _query = PdhQuery.Open();
                _engine = _query.AddCounter(EngineCounter);
                _dedicated = _query.AddCounter(DedicatedCounter);
            }

            _query.Collect();
        }
        catch (PdhException ex)
        {
            Trace.TraceWarning($"GPU counters unavailable: {ex.Message}");
            _countersUnsupported = true;
            _query?.Dispose();
            _query = null;
            return ReadCounters(adapter);
        }

        var luid = adapter.CounterLuid;
        var dedicated = PdhQuery.GetArray(_dedicated, Pdh.PDH_FMT_LARGE)
            .Where(i => i.Instance.StartsWith(luid, StringComparison.OrdinalIgnoreCase))
            .Select(i => (ulong?)i.Value)
            .FirstOrDefault();
        var dedicatedReading = dedicated is { } bytes
            ? MetricReading<ulong>.Ok(bytes)
            : MetricReading<ulong>.Unavailable("No memory counter for this adapter");

        // Utilization is a rate counter: the first collection only establishes a baseline.
        if (!_primed)
        {
            _primed = true;
            return (MetricReading<double>.Unavailable("Collecting first sample"), dedicatedReading);
        }

        var engines = PdhQuery.GetArray(_engine, Pdh.PDH_FMT_DOUBLE);
        return (MetricReading<double>.Ok(BusiestEngine(engines, luid)), dedicatedReading);
    }

    /// <summary>
    /// Task Manager's GPU %: sum each engine's utilization across processes, then take the
    /// busiest engine. Instance names look like
    /// <c>pid_1234_luid_0x00000000_0x0001335d_phys_0_eng_3_engtype_VideoDecode</c>.
    /// </summary>
    internal static double BusiestEngine(IEnumerable<(string Instance, double Value)> engines, string counterLuid)
    {
        var perEngine = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        foreach (var (instance, value) in engines)
        {
            var luidAt = instance.IndexOf(counterLuid, StringComparison.OrdinalIgnoreCase);
            if (luidAt < 0)
            {
                continue;
            }

            var engtypeAt = instance.IndexOf("_engtype_", luidAt, StringComparison.OrdinalIgnoreCase);
            var engineKey = engtypeAt < 0 ? instance[luidAt..] : instance[luidAt..engtypeAt];
            perEngine[engineKey] = perEngine.GetValueOrDefault(engineKey) + value;
        }

        return perEngine.Count == 0 ? 0 : Math.Clamp(perEngine.Values.Max(), 0, 100);
    }

    public void Dispose() => _query?.Dispose();
}

using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using DesktopDashboard.Core.Interop;
using DesktopDashboard.Core.Models;

namespace DesktopDashboard.Core.Monitoring;

/// <summary>
/// CPU usage from the "% Processor Utility" counter, the value Task Manager shows (it accounts for
/// frequency scaling, so it can exceed 100 under turbo and is clamped like Task Manager does).
/// Falls back to <c>GetSystemTimes</c> when performance counters are disabled or corrupted.
/// </summary>
public sealed class CpuSource : IMetricSource<CpuSample>
{
    private const string UtilityCounter = @"\Processor Information(_Total)\% Processor Utility";

    private PdhQuery? _query;
    private nint _counter;
    private bool _usePdh = true;
    private (long Idle, long Kernel, long User)? _lastTimes;

    public MetricReading<CpuSample> Read()
    {
        if (_usePdh)
        {
            try
            {
                return ReadPdh();
            }
            catch (PdhException ex)
            {
                Trace.TraceWarning($"CPU counter unavailable, falling back to GetSystemTimes: {ex.Message}");
                _usePdh = false;
                _query?.Dispose();
                _query = null;
            }
        }

        return ReadSystemTimes();
    }

    private MetricReading<CpuSample> ReadPdh()
    {
        if (_query is null)
        {
            _query = PdhQuery.Open();
            _counter = _query.AddCounter(UtilityCounter);
            _query.Collect();
            return MetricReading<CpuSample>.Unavailable("Collecting first sample");
        }

        _query.Collect();
        return PdhQuery.GetDouble(_counter, allowAbove100: true) is { } value
            ? MetricReading<CpuSample>.Ok(new CpuSample(Math.Clamp(value, 0, 100)))
            : MetricReading<CpuSample>.Unavailable("Counter has no data yet");
    }

    private MetricReading<CpuSample> ReadSystemTimes()
    {
        if (!Kernel32.GetSystemTimes(out var idle, out var kernel, out var user))
        {
            throw new Win32Exception(Marshal.GetLastPInvokeError());
        }

        var previous = _lastTimes;
        _lastTimes = (idle, kernel, user);
        if (previous is not { } last)
        {
            return MetricReading<CpuSample>.Unavailable("Collecting first sample");
        }

        // Kernel time includes idle time.
        var total = (kernel - last.Kernel) + (user - last.User);
        if (total <= 0)
        {
            return MetricReading<CpuSample>.Unavailable("No time elapsed between samples");
        }

        var busy = total - (idle - last.Idle);
        return MetricReading<CpuSample>.Ok(new CpuSample(Math.Clamp(busy * 100.0 / total, 0, 100)));
    }

    public void Dispose() => _query?.Dispose();
}

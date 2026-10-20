using System.Diagnostics;
using DesktopDashboard.Core.Models;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.NetworkManagement.IpHelper;
using Windows.Win32.NetworkManagement.Ndis;

namespace DesktopDashboard.Core.Monitoring;

/// <summary>
/// Download/upload rate from the per-interface byte counters of <c>GetIfTable2</c>.
/// Only physical, connected, non-filter interfaces are summed: virtual adapters (VPN, Hyper-V,
/// WSL) and NDIS filter layers carry the same bytes again and would double count.
/// </summary>
public sealed class NetworkSource : IMetricSource<NetworkSample>
{
    private readonly Dictionary<ulong, (ulong In, ulong Out)> _previous = [];
    private long _previousTimestamp;

    public MetricReading<NetworkSample> Read()
    {
        var timestamp = Stopwatch.GetTimestamp();
        var current = ReadCounters();

        var hadPrevious = _previousTimestamp != 0;
        var elapsed = Stopwatch.GetElapsedTime(_previousTimestamp, timestamp).TotalSeconds;
        double received = 0;
        double sent = 0;
        foreach (var (luid, counters) in current)
        {
            // Interfaces that just appeared, or whose counters were reset, contribute nothing this tick.
            if (_previous.TryGetValue(luid, out var last) && counters.In >= last.In && counters.Out >= last.Out)
            {
                received += counters.In - last.In;
                sent += counters.Out - last.Out;
            }
        }

        _previous.Clear();
        foreach (var (luid, counters) in current)
        {
            _previous[luid] = counters;
        }

        _previousTimestamp = timestamp;

        if (current.Count == 0)
        {
            return MetricReading<NetworkSample>.Unavailable("No connected network adapter");
        }

        if (!hadPrevious || elapsed <= 0)
        {
            return MetricReading<NetworkSample>.Unavailable("Collecting first sample");
        }

        return MetricReading<NetworkSample>.Ok(new NetworkSample(received / elapsed, sent / elapsed));
    }

    private static unsafe List<(ulong Luid, (ulong In, ulong Out) Counters)> ReadCounters()
    {
        MIB_IF_TABLE2* table;
        var error = PInvoke.GetIfTable2(&table);
        if (error != WIN32_ERROR.NO_ERROR)
        {
            throw new System.ComponentModel.Win32Exception((int)error);
        }

        try
        {
            var result = new List<(ulong, (ulong, ulong))>();
            var rows = table->Table.AsSpan((int)table->NumEntries);
            foreach (ref readonly var row in rows)
            {
                var flags = row.InterfaceAndOperStatusFlags;
                if (!flags.HardwareInterface
                    || flags.FilterInterface
                    || row.Type == PInvoke.IF_TYPE_SOFTWARE_LOOPBACK
                    || row.OperStatus != IF_OPER_STATUS.IfOperStatusUp
                    || row.MediaConnectState != NET_IF_MEDIA_CONNECT_STATE.MediaConnectStateConnected)
                {
                    continue;
                }

                result.Add((row.InterfaceLuid.Value, (row.InOctets, row.OutOctets)));
            }

            return result;
        }
        finally
        {
            PInvoke.FreeMibTable(table);
        }
    }

    public void Dispose()
    {
    }
}

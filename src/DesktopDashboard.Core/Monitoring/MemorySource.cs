using System.ComponentModel;
using System.Runtime.InteropServices;
using DesktopDashboard.Core.Interop;
using DesktopDashboard.Core.Models;

namespace DesktopDashboard.Core.Monitoring;

/// <summary>Physical memory from <c>GlobalMemoryStatusEx</c>; "used" is total minus available, as in Task Manager.</summary>
public sealed class MemorySource : IMetricSource<MemorySample>
{
    public MetricReading<MemorySample> Read()
    {
        var status = new Kernel32.MEMORYSTATUSEX { Length = (uint)Marshal.SizeOf<Kernel32.MEMORYSTATUSEX>() };
        if (!Kernel32.GlobalMemoryStatusEx(ref status))
        {
            throw new Win32Exception(Marshal.GetLastPInvokeError());
        }

        return MetricReading<MemorySample>.Ok(new MemorySample(status.TotalPhys, status.AvailPhys));
    }

    public void Dispose()
    {
    }
}

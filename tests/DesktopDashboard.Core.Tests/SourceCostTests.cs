using System.Diagnostics;
using DesktopDashboard.Core.Monitoring;

namespace DesktopDashboard.Core.Tests;

/// <summary>Reports the CPU time each source costs per read, to keep polling cheap.</summary>
public class SourceCostTests(ITestOutputHelper output)
{
    [Fact]
    public void Report_cpu_cost_per_read()
    {
        Measure("CPU", new CpuSource());
        Measure("Memory", new MemorySource());
        Measure("Battery", new BatterySource());
        Measure("Network", new NetworkSource());
        Measure("GPU", new GpuSource());
    }

    private void Measure<T>(string name, IMetricSource<T> source)
    {
        using (source)
        {
            source.Read();
            var process = Process.GetCurrentProcess();
            var cpuBefore = process.TotalProcessorTime;
            var wall = Stopwatch.StartNew();
            const int reads = 20;
            for (var i = 0; i < reads; i++)
            {
                source.Read();
            }

            process.Refresh();
            var cpu = (process.TotalProcessorTime - cpuBefore).TotalMilliseconds / reads;
            output.WriteLine($"{name,-8} {cpu,7:0.00} ms CPU/read  {wall.Elapsed.TotalMilliseconds / reads,7:0.00} ms wall/read");
        }
    }
}

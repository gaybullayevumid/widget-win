using DesktopDashboard.Core.Models;
using DesktopDashboard.Core.Monitoring;

namespace DesktopDashboard.Core.Tests;

public class GpuSourceTests(ITestOutputHelper output)
{
    private const string Luid = "luid_0x00000000_0x0001335d";

    [Fact]
    public void Busiest_engine_sums_processes_per_engine_and_takes_max()
    {
        (string, double)[] engines =
        [
            ($"pid_10_{Luid}_phys_0_eng_0_engtype_3D", 20),
            ($"pid_11_{Luid}_phys_0_eng_0_engtype_3D", 15),
            ($"pid_12_{Luid}_phys_0_eng_3_engtype_VideoDecode", 30),
            ("pid_13_luid_0x00000000_0x0001301f_phys_0_eng_0_engtype_3D", 90),
        ];

        Assert.Equal(35, GpuSource.BusiestEngine(engines, Luid));
    }

    [Fact]
    public void Busiest_engine_is_clamped_and_zero_when_idle()
    {
        Assert.Equal(0, GpuSource.BusiestEngine([], Luid));
        Assert.Equal(100, GpuSource.BusiestEngine([($"pid_1_{Luid}_phys_0_eng_0_engtype_3D", 140)], Luid));
    }

    [Fact]
    public void Counter_luid_matches_pdh_instance_format()
    {
        var adapter = new GpuAdapterInfo("x", 0x0000_0001_0001_335d, 0);

        Assert.Equal("luid_0x00000001_0x0001335d", adapter.CounterLuid);
    }

    [Fact]
    public void Prefers_named_adapter_then_most_dedicated_memory()
    {
        GpuAdapterInfo[] adapters =
        [
            new("Intel(R) UHD Graphics", 1, 128UL << 20),
            new("NVIDIA GeForce RTX 3050", 2, 6UL << 30),
        ];

        Assert.Equal(2, GpuAdapters.Select(adapters, null)!.Luid);
        Assert.Equal(1, GpuAdapters.Select(adapters, "intel(r) uhd graphics")!.Luid);
        Assert.Equal(2, GpuAdapters.Select(adapters, "Removed GPU")!.Luid);
        Assert.True(adapters[0].IsIntegrated);
        Assert.False(adapters[1].IsIntegrated);
    }

    [Fact]
    public async Task Live_gpu_read()
    {
        foreach (var a in GpuAdapters.Enumerate())
        {
            output.WriteLine($"{a.Name} {a.CounterLuid} dedicated={a.DedicatedVideoMemory >> 20} MB integrated={a.IsIntegrated}");
        }

        using var source = new GpuSource();
        var first = source.Read();
        await Task.Delay(1000, TestContext.Current.CancellationToken);
        var second = source.Read();

        output.WriteLine(second.ToString());
        Assert.True(first.HasValue, first.ToString());
        Assert.Equal(MetricStatus.Unavailable, first.Value.UtilizationPercent.Status);

        var gpu = second.Value;
        output.WriteLine($"util={gpu.UtilizationPercent} used={gpu.DedicatedUsedBytes} total={gpu.DedicatedTotalBytes} temp={gpu.TemperatureCelsius}");
        Assert.True(gpu.UtilizationPercent.HasValue);
        Assert.InRange(gpu.UtilizationPercent.Value, 0, 100);
        if (gpu.TemperatureCelsius.HasValue)
        {
            Assert.InRange(gpu.TemperatureCelsius.Value, 5, 120);
        }
    }
}

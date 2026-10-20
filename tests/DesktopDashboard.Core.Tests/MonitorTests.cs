using DesktopDashboard.Core.Models;
using DesktopDashboard.Core.Monitoring;

namespace DesktopDashboard.Core.Tests;

public class SystemMonitorTests
{
    private sealed class FixedSource<T>(Func<MetricReading<T>> read) : IMetricSource<T>
    {
        public int Reads { get; private set; }

        public MetricReading<T> Read()
        {
            Reads++;
            return read();
        }

        public void Dispose()
        {
        }
    }

    private static SystemMonitor Create(
        out FixedSource<CpuSample> cpu,
        Func<MetricReading<MemorySample>>? memory = null)
    {
        cpu = new FixedSource<CpuSample>(() => MetricReading<CpuSample>.Ok(new CpuSample(12)));
        return new SystemMonitor(
            cpu,
            new FixedSource<MemorySample>(memory ?? (() => MetricReading<MemorySample>.Ok(new MemorySample(100, 40)))),
            new NotImplementedSource<GpuSample>(),
            new NotImplementedSource<BatterySample>(),
            new NotImplementedSource<NetworkSample>(),
            TimeProvider.System);
    }

    [Fact]
    public void Disabled_metrics_are_not_read()
    {
        var monitor = Create(out var cpu);
        monitor.Enabled = MetricKind.Memory;

        var snapshot = monitor.Collect();

        Assert.Null(snapshot.Cpu);
        Assert.Equal(0, cpu.Reads);
        Assert.Equal(60UL, snapshot.Memory!.Value.Value.UsedBytes);
    }

    [Fact]
    public void Throwing_source_becomes_error_reading()
    {
        var monitor = Create(out _, () => throw new InvalidOperationException("boom"));

        var snapshot = monitor.Collect();

        Assert.Equal(MetricStatus.Error, snapshot.Memory!.Value.Status);
        Assert.Equal("boom", snapshot.Memory.Value.Reason);
        Assert.True(snapshot.Cpu!.Value.HasValue);
    }

    [Fact]
    public async Task Publishes_snapshots_until_disposed()
    {
        var monitor = Create(out _);
        monitor.Interval = TimeSpan.FromMilliseconds(50);
        var received = 0;
        var gotTwo = new TaskCompletionSource();
        monitor.SnapshotAvailable += (_, _) =>
        {
            if (Interlocked.Increment(ref received) == 2)
            {
                gotTwo.TrySetResult();
            }
        };

        monitor.Start();
        await gotTwo.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        await monitor.DisposeAsync();
        var afterDispose = Volatile.Read(ref received);
        await Task.Delay(200, TestContext.Current.CancellationToken);

        Assert.Equal(afterDispose, Volatile.Read(ref received));
    }

    [Fact]
    public async Task Paused_monitor_reads_nothing()
    {
        var monitor = Create(out var cpu);
        monitor.Interval = TimeSpan.FromMilliseconds(30);
        monitor.IsPaused = true;

        monitor.Start();
        await Task.Delay(200, TestContext.Current.CancellationToken);
        await monitor.DisposeAsync();

        Assert.Equal(0, cpu.Reads);
    }
}

/// <summary>Runs the real sources against this machine. Values are checked for plausibility, not exact numbers.</summary>
public class LiveSourceTests
{
    [Fact]
    public async Task Cpu_reports_percentage_after_warm_up()
    {
        using var source = new CpuSource();

        var first = source.Read();
        await Task.Delay(1000, TestContext.Current.CancellationToken);
        var second = source.Read();

        Assert.Equal(MetricStatus.Unavailable, first.Status);
        Assert.True(second.HasValue, second.ToString());
        Assert.InRange(second.Value.UsagePercent, 0, 100);
    }

    [Fact]
    public void Memory_matches_runtime_view_of_physical_memory()
    {
        using var source = new MemorySource();

        var reading = source.Read();

        Assert.True(reading.HasValue);
        var memory = reading.Value;
        Assert.InRange(memory.AvailableBytes, 1UL, memory.TotalBytes);

        // The GC reports total physical memory independently of GlobalMemoryStatusEx.
        var gcTotal = (ulong)GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;
        Assert.InRange((double)memory.TotalBytes, gcTotal * 0.98, gcTotal * 1.02);
    }
}

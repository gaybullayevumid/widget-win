namespace DesktopDashboard.Core.Models;

public sealed record CpuSample(double UsagePercent);

public sealed record MemorySample(ulong TotalBytes, ulong AvailableBytes)
{
    public ulong UsedBytes => TotalBytes - AvailableBytes;

    public double UsedPercent => TotalBytes == 0 ? 0 : UsedBytes * 100.0 / TotalBytes;
}

public sealed record GpuSample(
    string Name,
    bool IsIntegrated,
    MetricReading<double> UtilizationPercent,
    MetricReading<ulong> DedicatedUsedBytes,
    MetricReading<ulong> DedicatedTotalBytes,
    MetricReading<double> TemperatureCelsius);

public enum PowerSource
{
    Unknown,
    Battery,
    AC,
}

public sealed record BatterySample(
    PowerSource PowerSource,
    bool IsCharging,
    MetricReading<int> ChargePercent,
    MetricReading<TimeSpan> TimeRemaining);

public sealed record NetworkSample(double DownloadBytesPerSecond, double UploadBytesPerSecond);

/// <summary>All metrics collected in one polling tick.</summary>
public sealed record SystemSnapshot(
    DateTimeOffset Timestamp,
    MetricReading<CpuSample>? Cpu,
    MetricReading<MemorySample>? Memory,
    MetricReading<GpuSample>? Gpu,
    MetricReading<BatterySample>? Battery,
    MetricReading<NetworkSample>? Network);

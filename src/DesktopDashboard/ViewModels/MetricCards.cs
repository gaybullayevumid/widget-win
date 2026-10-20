using DesktopDashboard.Core.Formatting;
using DesktopDashboard.Core.Models;

namespace DesktopDashboard.ViewModels;

public sealed class CpuCardViewModel() : CardViewModel(MetricKind.Cpu, "CPU", "", hasHistory: true, historyMaximum: 100)
{
    public void Apply(MetricReading<CpuSample> reading)
    {
        if (ShowStatusIfMissing(reading))
        {
            return;
        }

        var cpu = reading.Value;
        PrimaryText = UnitFormatter.Percent(cpu.UsagePercent);
        PrimaryUnit = "%";
        SecondaryText = $"{Environment.ProcessorCount} logical processors";
        ToolTip = $"CPU utility {cpu.UsagePercent:0.0}%";
        History!.Add(cpu.UsagePercent);
    }
}

public sealed class MemoryCardViewModel() : CardViewModel(MetricKind.Memory, "Memory", "", hasHistory: true, historyMaximum: 100)
{
    public void Apply(MetricReading<MemorySample> reading)
    {
        if (ShowStatusIfMissing(reading))
        {
            return;
        }

        var memory = reading.Value;
        PrimaryText = UnitFormatter.Percent(memory.UsedPercent);
        PrimaryUnit = "%";
        SecondaryText = UnitFormatter.UsedOfTotal(memory.UsedBytes, memory.TotalBytes);
        ToolTip = $"In use {UnitFormatter.Bytes(memory.UsedBytes)}, available {UnitFormatter.Bytes(memory.AvailableBytes)}";
        History!.Add(memory.UsedPercent);
    }
}

public sealed class GpuCardViewModel() : CardViewModel(MetricKind.Gpu, "GPU", "", hasHistory: true, historyMaximum: 100)
{
    public void Apply(MetricReading<GpuSample> reading)
    {
        if (ShowStatusIfMissing(reading))
        {
            return;
        }

        var gpu = reading.Value;
        if (gpu.UtilizationPercent.HasValue)
        {
            PrimaryText = UnitFormatter.Percent(gpu.UtilizationPercent.Value);
            PrimaryUnit = "%";
        }
        else
        {
            PrimaryText = "—";
            PrimaryUnit = "";
        }

        SecondaryText = gpu.DedicatedUsedBytes.HasValue && gpu.DedicatedTotalBytes.HasValue
            ? "VRAM " + UnitFormatter.UsedOfTotal(gpu.DedicatedUsedBytes.Value, gpu.DedicatedTotalBytes.Value)
            : gpu.IsIntegrated ? "Integrated graphics" : "VRAM not available";

        if (gpu.TemperatureCelsius.HasValue)
        {
            BadgeGlyph = "";
            BadgeText = $"{gpu.TemperatureCelsius.Value:0}°C";
        }
        else
        {
            BadgeGlyph = "";
            BadgeText = "";
        }

        ToolTip = string.Join(Environment.NewLine,
            gpu.Name,
            $"Utilization: {gpu.UtilizationPercent}",
            $"Temperature: {gpu.TemperatureCelsius}");
        History!.Add(gpu.UtilizationPercent.HasValue ? gpu.UtilizationPercent.Value : null);
    }
}

public sealed class BatteryCardViewModel() : CardViewModel(MetricKind.Battery, "Battery", "", hasHistory: false)
{
    public void Apply(MetricReading<BatterySample> reading)
    {
        if (ShowStatusIfMissing(reading))
        {
            Glyph = "";
            return;
        }

        var battery = reading.Value;
        var level = battery.ChargePercent.HasValue ? battery.ChargePercent.Value : (int?)null;
        var step = Math.Clamp((level ?? 100) / 10, 0, 10);
        Glyph = char.ConvertFromUtf32((battery.IsCharging ? 0xEBAB : 0xEBA0) + step);

        PrimaryText = level?.ToString() ?? "—";
        PrimaryUnit = level is null ? "" : "%";
        GaugeValue = level ?? 0;
        SecondaryText = (battery.PowerSource, battery.IsCharging) switch
        {
            (_, true) => "Charging",
            (PowerSource.AC, false) when level >= 95 => "Plugged in, full",
            (PowerSource.AC, false) => "Plugged in, not charging",
            (PowerSource.Battery, _) when battery.TimeRemaining.HasValue => $"{FormatRemaining(battery.TimeRemaining.Value)} left",
            (PowerSource.Battery, _) => "On battery",
            _ => "Power source unknown",
        };
        ToolTip = SecondaryText;
    }

    private static string FormatRemaining(TimeSpan remaining) =>
        remaining.TotalHours >= 1 ? $"{(int)remaining.TotalHours} h {remaining.Minutes} min" : $"{remaining.Minutes} min";
}

public sealed class NetworkCardViewModel() : CardViewModel(MetricKind.Network, "Network", "", hasHistory: true)
{
    public void Apply(MetricReading<NetworkSample> reading)
    {
        if (ShowStatusIfMissing(reading))
        {
            return;
        }

        var network = reading.Value;
        var down = UnitFormatter.BytesPerSecond(network.DownloadBytesPerSecond);
        var up = UnitFormatter.BytesPerSecond(network.UploadBytesPerSecond);
        PrimaryText = "↓ " + down.Value;
        PrimaryUnit = down.Unit;
        SecondaryText = $"↑ {up}";
        ToolTip = $"Download {down}{Environment.NewLine}Upload {up}";
        History!.Add(network.DownloadBytesPerSecond);
    }
}

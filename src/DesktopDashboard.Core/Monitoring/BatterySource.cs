using System.ComponentModel;
using System.Runtime.InteropServices;
using DesktopDashboard.Core.Interop;
using DesktopDashboard.Core.Models;

namespace DesktopDashboard.Core.Monitoring;

/// <summary>Battery state from <c>GetSystemPowerStatus</c>. Desktops without a battery report Unsupported.</summary>
public sealed class BatterySource : IMetricSource<BatterySample>
{
    private const byte FlagCharging = 8;
    private const byte FlagNoBattery = 128;
    private const byte Unknown = 255;

    public MetricReading<BatterySample> Read()
    {
        if (!Kernel32.GetSystemPowerStatus(out var status))
        {
            throw new Win32Exception(Marshal.GetLastPInvokeError());
        }

        return Interpret(status);
    }

    internal static MetricReading<BatterySample> Interpret(Kernel32.SYSTEM_POWER_STATUS status)
    {
        if (status.BatteryFlag == Unknown)
        {
            return MetricReading<BatterySample>.Unavailable("Battery status unknown");
        }

        if ((status.BatteryFlag & FlagNoBattery) != 0)
        {
            return MetricReading<BatterySample>.Unsupported("No battery installed");
        }

        var source = status.ACLineStatus switch
        {
            0 => PowerSource.Battery,
            1 => PowerSource.AC,
            _ => PowerSource.Unknown,
        };

        var percent = status.BatteryLifePercent <= 100
            ? MetricReading<int>.Ok(status.BatteryLifePercent)
            : MetricReading<int>.Unavailable("Charge level unknown");

        // Windows only estimates remaining time while discharging; -1 means "no estimate yet".
        var remaining = source == PowerSource.Battery && status.BatteryLifeTime >= 0
            ? MetricReading<TimeSpan>.Ok(TimeSpan.FromSeconds(status.BatteryLifeTime))
            : MetricReading<TimeSpan>.Unavailable("No estimate");

        return MetricReading<BatterySample>.Ok(new BatterySample(
            source,
            (status.BatteryFlag & FlagCharging) != 0,
            percent,
            remaining));
    }

    public void Dispose()
    {
    }
}

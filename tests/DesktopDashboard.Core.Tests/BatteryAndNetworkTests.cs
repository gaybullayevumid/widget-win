using DesktopDashboard.Core.Interop;
using DesktopDashboard.Core.Models;
using DesktopDashboard.Core.Monitoring;

namespace DesktopDashboard.Core.Tests;

public class BatterySourceTests
{
    private static Kernel32.SYSTEM_POWER_STATUS Status(byte ac, byte flag, byte percent, int lifeTime = -1) => new()
    {
        ACLineStatus = ac,
        BatteryFlag = flag,
        BatteryLifePercent = percent,
        BatteryLifeTime = lifeTime,
    };

    [Fact]
    public void Desktop_without_battery_is_unsupported()
    {
        var reading = BatterySource.Interpret(Status(ac: 1, flag: 128, percent: 255));

        Assert.Equal(MetricStatus.Unsupported, reading.Status);
    }

    [Fact]
    public void Unknown_flag_is_unavailable_not_a_value()
    {
        Assert.Equal(MetricStatus.Unavailable, BatterySource.Interpret(Status(255, 255, 255)).Status);
    }

    [Fact]
    public void Charging_on_ac()
    {
        var battery = BatterySource.Interpret(Status(ac: 1, flag: 8 | 1, percent: 73)).Value;

        Assert.Equal(PowerSource.AC, battery.PowerSource);
        Assert.True(battery.IsCharging);
        Assert.Equal(73, battery.ChargePercent.Value);
        Assert.False(battery.TimeRemaining.HasValue);
    }

    [Fact]
    public void Discharging_reports_remaining_time_only_when_estimated()
    {
        var estimated = BatterySource.Interpret(Status(ac: 0, flag: 2, percent: 40, lifeTime: 5400)).Value;
        var notYet = BatterySource.Interpret(Status(ac: 0, flag: 2, percent: 40, lifeTime: -1)).Value;

        Assert.Equal(TimeSpan.FromMinutes(90), estimated.TimeRemaining.Value);
        Assert.False(notYet.TimeRemaining.HasValue);
    }

    [Fact]
    public void Unknown_percentage_is_not_reported()
    {
        var battery = BatterySource.Interpret(Status(ac: 1, flag: 1, percent: 255)).Value;

        Assert.Equal(MetricStatus.Unavailable, battery.ChargePercent.Status);
    }

    [Fact]
    public void Live_battery_read_succeeds()
    {
        using var source = new BatterySource();

        var reading = source.Read();

        Assert.NotEqual(MetricStatus.Error, reading.Status);
        if (reading.HasValue && reading.Value.ChargePercent.HasValue)
        {
            Assert.InRange(reading.Value.ChargePercent.Value, 0, 100);
        }
    }
}

public class NetworkSourceTests
{
    [Fact]
    public async Task Rates_are_non_negative_after_warm_up()
    {
        using var source = new NetworkSource();

        var first = source.Read();
        await Task.Delay(500, TestContext.Current.CancellationToken);
        var second = source.Read();

        Assert.NotEqual(MetricStatus.Ok, first.Status);
        Assert.True(second.HasValue, second.ToString());
        Assert.True(second.Value.DownloadBytesPerSecond >= 0);
        Assert.True(second.Value.UploadBytesPerSecond >= 0);
    }
}

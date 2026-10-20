using System.Globalization;
using DesktopDashboard.Core.Formatting;
using DesktopDashboard.Core.Models;

namespace DesktopDashboard.Core.Tests;

public class MetricReadingTests
{
    [Fact]
    public void Ok_reading_exposes_value()
    {
        var reading = MetricReading<int>.Ok(42);

        Assert.True(reading.HasValue);
        Assert.Equal(42, reading.Value);
    }

    [Theory]
    [InlineData(MetricStatus.Unsupported)]
    [InlineData(MetricStatus.Unavailable)]
    [InlineData(MetricStatus.Error)]
    public void Reading_without_value_never_yields_a_number(MetricStatus status)
    {
        var reading = status switch
        {
            MetricStatus.Unsupported => MetricReading<double>.Unsupported("x"),
            MetricStatus.Unavailable => MetricReading<double>.Unavailable("x"),
            _ => MetricReading<double>.Error("x"),
        };

        Assert.False(reading.HasValue);
        Assert.Equal(status, reading.Status);
        Assert.Throws<InvalidOperationException>(() => reading.Value);
        Assert.Equal("x", reading.Reason);
    }

    [Fact]
    public void Map_preserves_missing_status()
    {
        var mapped = MetricReading<int>.Unsupported("no sensor").Map(v => v * 2.0);

        Assert.Equal(MetricStatus.Unsupported, mapped.Status);
        Assert.Equal("no sensor", mapped.Reason);
    }
}

public class MetricHistoryTests
{
    [Fact]
    public void Keeps_most_recent_values_oldest_first()
    {
        var history = new MetricHistory(3);
        history.Add(1);
        history.Add(2);
        history.Add(3);
        history.Add(4);

        Assert.Equal([2.0, 3.0, 4.0], Enumerable.Range(0, 3).Select(i => history[i]));
    }

    [Fact]
    public void Missing_samples_are_gaps_not_zero()
    {
        var history = new MetricHistory(2);
        history.Add(5);
        history.Add(null);

        Assert.Equal(5, history[0]);
        Assert.True(double.IsNaN(history[1]));
        Assert.Equal(5, history.MaxOrDefault(-1));
    }

    [Fact]
    public void Empty_history_uses_fallback_maximum()
    {
        Assert.Equal(7, new MetricHistory(4).MaxOrDefault(7));
    }
}

public class UnitFormatterTests
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    [Theory]
    [InlineData(0, "0", "B")]
    [InlineData(999, "999", "B")]
    [InlineData(1536, "1.5", "KB")]
    [InlineData(10 * 1024 * 1024, "10.0", "MB")]
    [InlineData(512.0 * 1024 * 1024, "512", "MB")]
    [InlineData(6.0 * 1024 * 1024 * 1024, "6.0", "GB")]
    public void Bytes_uses_binary_units(double bytes, string value, string unit)
    {
        Assert.Equal(new FormattedValue(value, unit), UnitFormatter.Bytes(bytes, Invariant));
    }

    [Fact]
    public void Rate_appends_per_second()
    {
        Assert.Equal("1.5 KB/s", UnitFormatter.BytesPerSecond(1536, Invariant).ToString());
    }

    [Fact]
    public void Used_of_total_shares_the_total_unit()
    {
        const ulong GiB = 1024UL * 1024 * 1024;
        Assert.Equal("0.5 / 16.0 GB", UnitFormatter.UsedOfTotal(GiB / 2, 16 * GiB, Invariant));
    }
}

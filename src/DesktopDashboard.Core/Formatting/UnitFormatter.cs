using System.Globalization;

namespace DesktopDashboard.Core.Formatting;

/// <summary>A number and its unit kept apart so the UI can style them differently.</summary>
public readonly record struct FormattedValue(string Value, string Unit)
{
    public override string ToString() => $"{Value} {Unit}";
}

public static class UnitFormatter
{
    private static readonly string[] ByteUnits = ["B", "KB", "MB", "GB", "TB"];

    /// <summary>Binary (1024-based) units, labelled the way Windows labels them.</summary>
    public static FormattedValue Bytes(double bytes, CultureInfo? culture = null)
    {
        var unit = 0;
        while (Math.Abs(bytes) >= 1000 && unit < ByteUnits.Length - 1)
        {
            bytes /= 1024;
            unit++;
        }

        return new FormattedValue(Number(bytes, unit == 0, culture), ByteUnits[unit]);
    }

    public static FormattedValue BytesPerSecond(double bytesPerSecond, CultureInfo? culture = null)
    {
        var bytes = Bytes(bytesPerSecond, culture);
        return bytes with { Unit = bytes.Unit + "/s" };
    }

    /// <summary>"9.7 / 15.6 GB"; both values share the unit of the total.</summary>
    public static string UsedOfTotal(ulong used, ulong total, CultureInfo? culture = null)
    {
        var totalFormatted = Bytes(total, culture);
        var unitIndex = Array.IndexOf(ByteUnits, totalFormatted.Unit);
        var usedScaled = used / Math.Pow(1024, unitIndex);
        return $"{Number(usedScaled, unitIndex == 0, culture)} / {totalFormatted.Value} {totalFormatted.Unit}";
    }

    public static string Percent(double percent, CultureInfo? culture = null) =>
        Math.Round(percent).ToString("0", culture ?? CultureInfo.CurrentCulture);

    private static string Number(double value, bool integer, CultureInfo? culture)
    {
        var format = integer || Math.Abs(value) >= 100 ? "0" : "0.0";
        return value.ToString(format, culture ?? CultureInfo.CurrentCulture);
    }
}

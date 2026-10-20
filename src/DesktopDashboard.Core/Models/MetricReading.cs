namespace DesktopDashboard.Core.Models;

public enum MetricStatus
{
    /// <summary>The value was measured successfully.</summary>
    Ok,

    /// <summary>The hardware, driver or OS does not expose this metric.</summary>
    Unsupported,

    /// <summary>The metric exists but has no value yet (e.g. first sample of a rate counter, device asleep).</summary>
    Unavailable,

    /// <summary>Reading the metric failed unexpectedly.</summary>
    Error,
}

/// <summary>
/// A single metric value together with how it was obtained. A reading that is not
/// <see cref="MetricStatus.Ok"/> never carries a value, so the UI cannot show a made-up number.
/// </summary>
public readonly record struct MetricReading<T>
{
    private readonly T? _value;

    private MetricReading(MetricStatus status, T? value, string? reason)
    {
        Status = status;
        _value = value;
        Reason = reason;
    }

    public MetricStatus Status { get; }

    public string? Reason { get; }

    public bool HasValue => Status == MetricStatus.Ok;

    public T Value => HasValue
        ? _value!
        : throw new InvalidOperationException($"Metric has no value (status {Status}: {Reason}).");

    public T? ValueOrDefault => HasValue ? _value : default;

    public static MetricReading<T> Ok(T value) => new(MetricStatus.Ok, value, null);

    public static MetricReading<T> Unsupported(string reason) => new(MetricStatus.Unsupported, default, reason);

    public static MetricReading<T> Unavailable(string reason) => new(MetricStatus.Unavailable, default, reason);

    public static MetricReading<T> Error(string reason) => new(MetricStatus.Error, default, reason);

    /// <summary>Carries a non-Ok status over to a reading of another type.</summary>
    public MetricReading<TOther> As<TOther>() => HasValue
        ? throw new InvalidOperationException("Use Map for readings that have a value.")
        : new MetricReading<TOther>(Status, default, Reason);

    public MetricReading<TOther> Map<TOther>(Func<T, TOther> map) =>
        HasValue ? MetricReading<TOther>.Ok(map(_value!)) : As<TOther>();

    public override string ToString() => HasValue ? $"{_value}" : $"{Status}: {Reason}";
}

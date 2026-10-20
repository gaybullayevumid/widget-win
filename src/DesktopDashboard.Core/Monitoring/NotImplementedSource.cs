using DesktopDashboard.Core.Models;

namespace DesktopDashboard.Core.Monitoring;

/// <summary>Placeholder used until a real source is wired in; reports Unsupported, never a value.</summary>
public sealed class NotImplementedSource<T> : IMetricSource<T>
{
    public MetricReading<T> Read() => MetricReading<T>.Unsupported("Not implemented yet");

    public void Dispose()
    {
    }
}

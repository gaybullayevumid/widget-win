namespace DesktopDashboard.Core.Models;

/// <summary>
/// Fixed-size ring buffer of recent values for sparklines. Missing samples are stored as
/// <see cref="double.NaN"/> so a gap is drawn as a gap rather than as a fake zero.
/// </summary>
public sealed class MetricHistory
{
    private readonly double[] _values;
    private int _start;

    public MetricHistory(int capacity)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 2);
        _values = new double[capacity];
        Array.Fill(_values, double.NaN);
    }

    public int Capacity => _values.Length;

    public event EventHandler? Changed;

    /// <summary>Value at <paramref name="index"/>, oldest first.</summary>
    public double this[int index] => _values[(_start + index) % _values.Length];

    public void Add(double? value)
    {
        _values[_start] = value is { } v && double.IsFinite(v) ? v : double.NaN;
        _start = (_start + 1) % _values.Length;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public double MaxOrDefault(double fallback)
    {
        var max = double.NaN;
        foreach (var v in _values)
        {
            if (!double.IsNaN(v) && (double.IsNaN(max) || v > max))
            {
                max = v;
            }
        }

        return double.IsNaN(max) ? fallback : max;
    }
}

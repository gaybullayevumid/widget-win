using CommunityToolkit.Mvvm.ComponentModel;
using DesktopDashboard.Core.Models;

namespace DesktopDashboard.ViewModels;

/// <summary>
/// Presentation state shared by every metric card. Subclasses translate a typed reading into
/// these strings; a reading without a value always ends up as "—" plus a status explanation.
/// </summary>
public abstract partial class CardViewModel : ObservableObject
{
    public const int HistoryLength = 60;

    protected CardViewModel(MetricKind kind, string title, string glyph, bool hasHistory, double historyMaximum = double.NaN)
    {
        Kind = kind;
        Title = title;
        Glyph = glyph;
        History = hasHistory ? new MetricHistory(HistoryLength) : null;
        HistoryMaximum = historyMaximum;
        SecondaryText = "Waiting for data…";
    }

    public MetricKind Kind { get; }

    public string Title { get; }

    public MetricHistory? History { get; }

    /// <summary>Fixed sparkline scale, or NaN to scale to the largest recent value.</summary>
    public double HistoryMaximum { get; }

    [ObservableProperty]
    public partial string Glyph { get; set; }

    [ObservableProperty]
    public partial string PrimaryText { get; set; } = "—";

    [ObservableProperty]
    public partial string PrimaryUnit { get; set; } = "";

    [ObservableProperty]
    public partial string SecondaryText { get; set; }

    [ObservableProperty]
    public partial string BadgeGlyph { get; set; } = "";

    [ObservableProperty]
    public partial string BadgeText { get; set; } = "";

    /// <summary>0–100 for the thin bar at the bottom of the card, or NaN to hide it.</summary>
    [ObservableProperty]
    public partial double GaugeValue { get; set; } = double.NaN;

    [ObservableProperty]
    public partial bool IsAvailable { get; set; }

    [ObservableProperty]
    public partial string ToolTip { get; set; } = "";

    /// <summary>Shows why there is no value. Returns true when the caller should stop.</summary>
    protected bool ShowStatusIfMissing<T>(MetricReading<T> reading)
    {
        if (reading.HasValue)
        {
            IsAvailable = true;
            return false;
        }

        IsAvailable = false;
        PrimaryText = "—";
        PrimaryUnit = "";
        BadgeGlyph = "";
        BadgeText = "";
        SecondaryText = reading.Status switch
        {
            MetricStatus.Unsupported => "Not supported",
            MetricStatus.Unavailable => "Unavailable",
            _ => "Error reading value",
        };
        ToolTip = reading.Reason ?? SecondaryText;
        if (!double.IsNaN(GaugeValue))
        {
            GaugeValue = 0;
        }

        History?.Add(null);
        return true;
    }
}

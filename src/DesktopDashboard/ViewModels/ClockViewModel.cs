using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using DesktopDashboard.Core.Settings;
using Microsoft.UI.Dispatching;

namespace DesktopDashboard.ViewModels;

/// <summary>
/// Updates exactly on second (or minute) boundaries instead of on a free-running 1 s timer,
/// so the displayed time never lags the system clock by up to a second.
/// </summary>
public sealed partial class ClockViewModel : ObservableObject, IDisposable
{
    private readonly TimeProvider _time;
    private readonly SettingsService _settings;
    private readonly DispatcherQueueTimer _timer;

    public ClockViewModel(TimeProvider time, DispatcherQueue dispatcher, SettingsService settings)
    {
        _time = time;
        _settings = settings;
        _timer = dispatcher.CreateTimer();
        _timer.IsRepeating = false;
        _timer.Tick += (_, _) => Update();
    }

    [ObservableProperty]
    public partial string TimeText { get; set; } = "";

    [ObservableProperty]
    public partial string SecondsText { get; set; } = "";

    [ObservableProperty]
    public partial string MeridiemText { get; set; } = "";

    [ObservableProperty]
    public partial string DateText { get; set; } = "";

    public bool IsRunning { get; private set; }

    public void Start()
    {
        IsRunning = true;
        Update();
    }

    public void Stop()
    {
        IsRunning = false;
        _timer.Stop();
    }

    /// <summary>Re-renders immediately, e.g. after the 12/24-hour setting changed.</summary>
    public void Refresh()
    {
        if (IsRunning)
        {
            Update();
        }
    }

    private void Update()
    {
        var now = _time.GetLocalNow();
        var culture = CultureInfo.CurrentCulture;
        var settings = _settings.Current;

        if (settings.Use24HourClock)
        {
            TimeText = now.ToString("HH:mm", culture);
            MeridiemText = "";
        }
        else
        {
            TimeText = now.ToString("h:mm", culture);
            MeridiemText = now.ToString("tt", culture);
        }

        SecondsText = settings.ShowSeconds ? now.ToString("ss", culture) : "";
        DateText = now.ToString("dddd, d MMMM", culture);

        var untilNext = settings.ShowSeconds
            ? TimeSpan.FromMilliseconds(1000 - now.Millisecond)
            : TimeSpan.FromSeconds(60 - now.Second) - TimeSpan.FromMilliseconds(now.Millisecond);

        // A few ms of slack so the tick lands just after the boundary, not just before it.
        _timer.Interval = untilNext + TimeSpan.FromMilliseconds(15);
        if (IsRunning)
        {
            _timer.Start();
        }
    }

    public void Dispose() => _timer.Stop();
}

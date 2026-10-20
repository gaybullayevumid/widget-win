using System.Diagnostics;
using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DesktopDashboard.Core.Monitoring;
using DesktopDashboard.Core.Settings;
using DesktopDashboard.Services;

namespace DesktopDashboard.ViewModels;

public sealed record AccentSwatch(string Name, string? Hex);

/// <summary>
/// Two-way view of <see cref="AppSettings"/> for the settings window. Every edit is written
/// through immediately so the dashboard previews it live.
/// </summary>
public sealed partial class SettingsViewModel : ObservableObject, IDisposable
{
    private const string AutomaticGpu = "Automatic (most dedicated memory)";
    private static readonly int[] PollIntervals = [500, 1000, 2000, 5000];

    private readonly SettingsService _settings;
    private readonly IAppCommands _app;
    private bool _loading;

    public SettingsViewModel(SettingsService settings, IAppCommands app)
    {
        _settings = settings;
        _app = app;

        GpuAdapters = [AutomaticGpu];
        try
        {
            GpuAdapters.AddRange(Core.Monitoring.GpuAdapters.Enumerate().Select(a => a.Name).Distinct(StringComparer.OrdinalIgnoreCase));
        }
        catch (Exception ex)
        {
            Trace.TraceWarning($"Listing GPUs failed: {ex.Message}");
        }

        Load();
        _settings.Changed += OnSettingsChanged;
    }

    public List<string> GpuAdapters { get; }

    public IReadOnlyList<AccentSwatch> Swatches { get; } =
    [
        new("Windows accent", null),
        new("Blue", "#4CC2FF"),
        new("Teal", "#2DD4BF"),
        new("Green", "#6CCB5F"),
        new("Amber", "#FFB900"),
        new("Orange", "#FF8C42"),
        new("Rose", "#FF6B8B"),
        new("Violet", "#B38CFF"),
    ];

    public string SettingsFilePath => JsonSettingsStore.DefaultPath;

    public string Version =>
        Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0]
        ?? "unknown";

    [ObservableProperty] public partial int ThemeIndex { get; set; }
    [ObservableProperty] public partial int BackdropIndex { get; set; }
    [ObservableProperty] public partial double OpacityPercent { get; set; }
    [ObservableProperty] public partial int SizeIndex { get; set; }
    [ObservableProperty] public partial int AccentIndex { get; set; }
    [ObservableProperty] public partial bool ShowClock { get; set; }
    [ObservableProperty] public partial bool ShowCpu { get; set; }
    [ObservableProperty] public partial bool ShowMemory { get; set; }
    [ObservableProperty] public partial bool ShowGpu { get; set; }
    [ObservableProperty] public partial bool ShowBattery { get; set; }
    [ObservableProperty] public partial bool ShowNetwork { get; set; }
    [ObservableProperty] public partial bool Use24HourClock { get; set; }
    [ObservableProperty] public partial bool ShowSeconds { get; set; }
    [ObservableProperty] public partial int GpuIndex { get; set; }
    [ObservableProperty] public partial int LayerIndex { get; set; }
    [ObservableProperty] public partial bool ShowInTaskbar { get; set; }
    [ObservableProperty] public partial bool LaunchAtStartup { get; set; }
    [ObservableProperty] public partial int PollIntervalIndex { get; set; }

    public bool IsOpacityEnabled => BackdropIndex != (int)BackdropKind.Solid;

    private void OnSettingsChanged(object? sender, EventArgs e) => Load();

    /// <summary>Copies settings into the properties without writing them back.</summary>
    private void Load()
    {
        var s = _settings.Current;
        _loading = true;
        try
        {
            ThemeIndex = (int)s.Theme;
            BackdropIndex = (int)s.Backdrop;
            OpacityPercent = Math.Round(s.Opacity * 100);
            SizeIndex = (int)s.Size;
            AccentIndex = Math.Max(0, Swatches.ToList().FindIndex(w => string.Equals(w.Hex, s.AccentColor, StringComparison.OrdinalIgnoreCase)));
            ShowClock = s.Cards.Clock;
            ShowCpu = s.Cards.Cpu;
            ShowMemory = s.Cards.Memory;
            ShowGpu = s.Cards.Gpu;
            ShowBattery = s.Cards.Battery;
            ShowNetwork = s.Cards.Network;
            Use24HourClock = s.Use24HourClock;
            ShowSeconds = s.ShowSeconds;
            GpuIndex = Math.Max(0, GpuAdapters.FindIndex(n => string.Equals(n, s.GpuAdapter, StringComparison.OrdinalIgnoreCase)));
            LayerIndex = (int)s.Layer;
            ShowInTaskbar = s.ShowInTaskbar;
            LaunchAtStartup = s.LaunchAtStartup;
            PollIntervalIndex = Math.Max(0, Array.IndexOf(PollIntervals, s.PollIntervalMs));
        }
        finally
        {
            _loading = false;
        }
    }

    private void Save(Action<AppSettings> change)
    {
        if (!_loading)
        {
            _settings.Update(change);
        }
    }

    partial void OnThemeIndexChanged(int value) => Save(s => s.Theme = (ThemePreference)value);

    partial void OnBackdropIndexChanged(int value)
    {
        OnPropertyChanged(nameof(IsOpacityEnabled));
        Save(s => s.Backdrop = (BackdropKind)value);
    }

    partial void OnOpacityPercentChanged(double value) => Save(s => s.Opacity = value / 100);

    partial void OnSizeIndexChanged(int value) => Save(s => s.Size = (WidgetSize)value);

    partial void OnAccentIndexChanged(int value)
    {
        if (value >= 0 && value < Swatches.Count)
        {
            Save(s => s.AccentColor = Swatches[value].Hex);
        }
    }

    partial void OnShowClockChanged(bool value) => Save(s => s.Cards.Clock = value);

    partial void OnShowCpuChanged(bool value) => Save(s => s.Cards.Cpu = value);

    partial void OnShowMemoryChanged(bool value) => Save(s => s.Cards.Memory = value);

    partial void OnShowGpuChanged(bool value) => Save(s => s.Cards.Gpu = value);

    partial void OnShowBatteryChanged(bool value) => Save(s => s.Cards.Battery = value);

    partial void OnShowNetworkChanged(bool value) => Save(s => s.Cards.Network = value);

    partial void OnUse24HourClockChanged(bool value) => Save(s => s.Use24HourClock = value);

    partial void OnShowSecondsChanged(bool value) => Save(s => s.ShowSeconds = value);

    partial void OnGpuIndexChanged(int value) =>
        Save(s => s.GpuAdapter = value <= 0 || value >= GpuAdapters.Count ? null : GpuAdapters[value]);

    partial void OnLayerIndexChanged(int value) => Save(s => s.Layer = (WindowLayer)value);

    partial void OnShowInTaskbarChanged(bool value) => Save(s => s.ShowInTaskbar = value);

    partial void OnLaunchAtStartupChanged(bool value)
    {
        if (!_loading)
        {
            _app.SetLaunchAtStartup(value);
        }
    }

    partial void OnPollIntervalIndexChanged(int value)
    {
        if (value >= 0 && value < PollIntervals.Length)
        {
            Save(s => s.PollIntervalMs = PollIntervals[value]);
        }
    }

    [RelayCommand]
    private void OpenSettingsFolder() =>
        Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{SettingsFilePath}\"") { UseShellExecute = true });

    [RelayCommand]
    private void ResetAppearance() => _settings.Update(s =>
    {
        var defaults = new AppSettings();
        s.Theme = defaults.Theme;
        s.Backdrop = defaults.Backdrop;
        s.Opacity = defaults.Opacity;
        s.Size = defaults.Size;
        s.AccentColor = defaults.AccentColor;
    });

    [RelayCommand]
    private void ExitApp() => _app.Shutdown();

    public void Dispose() => _settings.Changed -= OnSettingsChanged;
}

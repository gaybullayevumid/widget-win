using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using DesktopDashboard.Core.Models;
using DesktopDashboard.Core.Monitoring;
using DesktopDashboard.Core.Settings;
using Microsoft.UI.Dispatching;

namespace DesktopDashboard.ViewModels;

public sealed partial class DashboardViewModel : ObservableObject, IDisposable
{
    private const double BaseCardWidth = 176;
    private const double BaseCardHeight = 120;

    private readonly SystemMonitor _monitor;
    private readonly GpuSource _gpuSource;
    private readonly SettingsService _settings;
    private readonly DispatcherQueue _dispatcher;
    private readonly CpuCardViewModel _cpu = new();
    private readonly MemoryCardViewModel _memory = new();
    private readonly GpuCardViewModel _gpu = new();
    private readonly BatteryCardViewModel _battery = new();
    private readonly NetworkCardViewModel _network = new();
    private readonly CardViewModel[] _allCards;
    private int _pendingSnapshot;

    public DashboardViewModel(SystemMonitor monitor, GpuSource gpuSource, SettingsService settings, ClockViewModel clock, DispatcherQueue dispatcher)
    {
        _monitor = monitor;
        _gpuSource = gpuSource;
        _settings = settings;
        _dispatcher = dispatcher;
        Clock = clock;
        _allCards = [_cpu, _memory, _gpu, _battery, _network];
    }

    public ClockViewModel Clock { get; }

    public ObservableCollection<CardViewModel> Cards { get; } = [];

    [ObservableProperty]
    public partial bool ShowClock { get; set; } = true;

    [ObservableProperty]
    public partial double CardWidth { get; set; } = BaseCardWidth;

    [ObservableProperty]
    public partial double CardHeight { get; set; } = BaseCardHeight;

    [ObservableProperty]
    public partial double ClockHeight { get; set; } = 64;

    public double Scale { get; private set; } = 1;

    public void Start()
    {
        _settings.Changed += OnSettingsChanged;
        _monitor.SnapshotAvailable += OnSnapshotAvailable;
        ApplySettings();
        _monitor.Start();
    }

    /// <summary>Stops all polling while the dashboard is not visible.</summary>
    public void SetActive(bool active)
    {
        _monitor.IsPaused = !active;
        if (active && ShowClock)
        {
            Clock.Start();
        }
        else
        {
            Clock.Stop();
        }
    }

    private void OnSettingsChanged(object? sender, EventArgs e) => ApplySettings();

    private void ApplySettings()
    {
        var settings = _settings.Current;
        Scale = settings.Size switch
        {
            WidgetSize.Small => 0.85,
            WidgetSize.Large => 1.2,
            _ => 1.0,
        };
        CardWidth = BaseCardWidth * Scale;
        CardHeight = BaseCardHeight * Scale;
        ClockHeight = 64 * Scale;

        ShowClock = settings.Cards.Clock;
        if (ShowClock && !_monitor.IsPaused)
        {
            Clock.Start();
        }
        else if (!ShowClock)
        {
            Clock.Stop();
        }

        Clock.Refresh();

        var visible = MetricKind.None;
        if (settings.Cards.Cpu) visible |= MetricKind.Cpu;
        if (settings.Cards.Memory) visible |= MetricKind.Memory;
        if (settings.Cards.Gpu) visible |= MetricKind.Gpu;
        if (settings.Cards.Battery) visible |= MetricKind.Battery;
        if (settings.Cards.Network) visible |= MetricKind.Network;

        _gpuSource.PreferredAdapter = settings.GpuAdapter;
        _monitor.Enabled = visible;
        _monitor.Interval = TimeSpan.FromMilliseconds(settings.PollIntervalMs);
        SyncCards(visible);
    }

    private void SyncCards(MetricKind visible)
    {
        var wanted = _allCards.Where(c => visible.HasFlag(c.Kind)).ToList();
        if (wanted.SequenceEqual(Cards))
        {
            return;
        }

        Cards.Clear();
        foreach (var card in wanted)
        {
            Cards.Add(card);
        }
    }

    private void OnSnapshotAvailable(object? sender, SystemSnapshot snapshot)
    {
        // If the UI thread has fallen behind, drop stale snapshots rather than queueing them.
        if (Interlocked.Exchange(ref _pendingSnapshot, 1) == 1)
        {
            return;
        }

        _dispatcher.TryEnqueue(DispatcherQueuePriority.Low, () =>
        {
            Volatile.Write(ref _pendingSnapshot, 0);
            Apply(snapshot);
        });
    }

    private void Apply(SystemSnapshot snapshot)
    {
        if (snapshot.Cpu is { } cpu) _cpu.Apply(cpu);
        if (snapshot.Memory is { } memory) _memory.Apply(memory);
        if (snapshot.Gpu is { } gpu) _gpu.Apply(gpu);
        if (snapshot.Battery is { } battery) _battery.Apply(battery);
        if (snapshot.Network is { } network) _network.Apply(network);
    }

    public void Dispose()
    {
        _settings.Changed -= OnSettingsChanged;
        _monitor.SnapshotAvailable -= OnSnapshotAvailable;
        Clock.Dispose();
    }
}

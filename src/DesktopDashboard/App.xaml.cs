using DesktopDashboard.Core.Models;
using DesktopDashboard.Core.Monitoring;
using DesktopDashboard.Core.Settings;
using DesktopDashboard.ViewModels;
using DesktopDashboard.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;

namespace DesktopDashboard;

public partial class App : Application
{
    private readonly ServiceProvider _services;
    private MainWindow? _window;

    public App()
    {
        InitializeComponent();
        _services = ConfigureServices();
    }

    private static ServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();

        services.AddSingleton(TimeProvider.System);
        services.AddSingleton(DispatcherQueue.GetForCurrentThread());

        services.AddSingleton<ISettingsStore>(_ => new JsonSettingsStore(JsonSettingsStore.DefaultPath));
        services.AddSingleton<SettingsService>();

        services.AddSingleton<IMetricSource<CpuSample>, CpuSource>();
        services.AddSingleton<IMetricSource<MemorySample>, MemorySource>();
        services.AddSingleton<GpuSource>();
        services.AddSingleton<IMetricSource<GpuSample>>(sp => sp.GetRequiredService<GpuSource>());
        services.AddSingleton<IMetricSource<BatterySample>, BatterySource>();
        services.AddSingleton<IMetricSource<NetworkSample>, NetworkSource>();
        services.AddSingleton<SystemMonitor>();

        services.AddSingleton<ClockViewModel>();
        services.AddSingleton<DashboardViewModel>();
        services.AddSingleton<MainWindow>();

        return services.BuildServiceProvider();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        var dashboard = _services.GetRequiredService<DashboardViewModel>();
        _window = _services.GetRequiredService<MainWindow>();
        dashboard.Start();
        dashboard.SetActive(true);
        _window.Activate();
    }
}

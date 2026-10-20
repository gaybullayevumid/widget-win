using System.Diagnostics;
using DesktopDashboard.Core.Models;
using DesktopDashboard.Core.Monitoring;
using DesktopDashboard.Core.Settings;
using DesktopDashboard.Services;
using DesktopDashboard.ViewModels;
using DesktopDashboard.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;

namespace DesktopDashboard;

public partial class App : Application, IAppCommands
{
    private readonly bool _isStartupLaunch;
    private readonly ServiceProvider _services;
    private MainWindow? _window;
    private SettingsWindow? _settingsWindow;
    private bool _exiting;

    public App(bool isStartupLaunch)
    {
        _isStartupLaunch = isStartupLaunch;
        InitializeComponent();
        _services = ConfigureServices();
        UnhandledException += (_, e) => Trace.TraceError($"Unhandled UI exception: {e.Exception}");
    }

    public static new App? Current => Application.Current as App;

    private ServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();

        services.AddSingleton<IAppCommands>(this);
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton(DispatcherQueue.GetForCurrentThread());

        services.AddSingleton<ISettingsStore>(_ => new JsonSettingsStore(JsonSettingsStore.DefaultPath));
        services.AddSingleton<SettingsService>();
        services.AddSingleton<IStartupRegistration>(_ => new RunKeyStartupRegistration(Environment.ProcessPath!));

        services.AddSingleton<IMetricSource<CpuSample>, CpuSource>();
        services.AddSingleton<IMetricSource<MemorySample>, MemorySource>();
        services.AddSingleton<GpuSource>();
        services.AddSingleton<IMetricSource<GpuSample>>(sp => sp.GetRequiredService<GpuSource>());
        services.AddSingleton<IMetricSource<BatterySample>, BatterySource>();
        services.AddSingleton<IMetricSource<NetworkSample>, NetworkSource>();
        services.AddSingleton<SystemMonitor>();

        services.AddSingleton<TrayIconService>();
        services.AddSingleton<ClockViewModel>();
        services.AddSingleton<DashboardViewModel>();
        services.AddSingleton<MainWindow>();
        services.AddTransient<SettingsViewModel>();

        return services.BuildServiceProvider();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        var settings = _services.GetRequiredService<SettingsService>();
        var startup = _services.GetRequiredService<IStartupRegistration>();
        var dashboard = _services.GetRequiredService<DashboardViewModel>();
        var tray = _services.GetRequiredService<TrayIconService>();

        // The Run key is the source of truth; it can be changed outside the app (Task Manager > Startup).
        settings.UpdateSilently(s => s.LaunchAtStartup = startup.IsEnabled);

        _window = _services.GetRequiredService<MainWindow>();
        _window.SettingsRequested += (_, _) => OpenSettings();

        tray.Initialize(new TrayActions(
            ToggleDashboard: () => _window.ToggleVisibility(),
            ToggleAlwaysOnTop: () => settings.Update(s => s.Layer = s.Layer == WindowLayer.OnTop ? WindowLayer.Desktop : WindowLayer.OnTop),
            ToggleStartup: () => SetLaunchAtStartup(!startup.IsEnabled),
            OpenSettings: OpenSettings,
            Exit: Shutdown));
        tray.Update(settings.Current.Layer == WindowLayer.OnTop, startup.IsEnabled);
        settings.Changed += (_, _) => tray.Update(settings.Current.Layer == WindowLayer.OnTop, startup.IsEnabled);

        dashboard.Start();

        // At sign-in, appear without stealing focus from whatever the user opens first.
        _window.ShowWidget(activate: !_isStartupLaunch);
    }

    /// <summary>A second launch was redirected here; bring the dashboard forward. Called on a background thread.</summary>
    internal void OnRedirectedActivation() =>
        _services.GetRequiredService<DispatcherQueue>().TryEnqueue(() => _window?.ShowWidget(activate: true));

    public void SetLaunchAtStartup(bool enable)
    {
        var startup = _services.GetRequiredService<IStartupRegistration>();
        try
        {
            if (enable)
            {
                startup.Enable();
            }
            else
            {
                startup.Disable();
            }
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException or IOException)
        {
            Trace.TraceError($"Changing startup registration failed: {ex.Message}");
        }

        // Mirror the registry state in settings so every view refreshes.
        _services.GetRequiredService<SettingsService>().Update(s => s.LaunchAtStartup = startup.IsEnabled);
    }

    private void OpenSettings()
    {
        if (_settingsWindow is null)
        {
            _settingsWindow = new SettingsWindow(
                _services.GetRequiredService<SettingsViewModel>(),
                _services.GetRequiredService<SettingsService>());
            _settingsWindow.Closed += (_, _) => _settingsWindow = null;
        }

        _settingsWindow.Activate();
    }

    public async void Shutdown()
    {
        if (_exiting)
        {
            return;
        }

        _exiting = true;
        _settingsWindow?.Close();
        _services.GetRequiredService<TrayIconService>().Dispose();
        _services.GetRequiredService<DashboardViewModel>().Dispose();
        await _services.GetRequiredService<SystemMonitor>().DisposeAsync();
        _services.GetRequiredService<SettingsService>().Dispose();
        _window?.CloseForExit();
        Application.Current.Exit();
    }
}

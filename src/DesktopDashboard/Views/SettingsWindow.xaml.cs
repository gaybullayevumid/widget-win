using DesktopDashboard.Core.Settings;
using DesktopDashboard.Services;
using DesktopDashboard.ViewModels;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace DesktopDashboard.Views;

public sealed partial class SettingsWindow : Window
{
    private readonly SettingsService _settings;

    public SettingsWindow(SettingsViewModel viewModel, SettingsService settings)
    {
        ViewModel = viewModel;
        _settings = settings;
        InitializeComponent();

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(TitleBar);
        AppWindow.Title = "Dashboard settings";
        AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico"));

        var scale = NativeMethods.GetDpiForWindow(WinRT.Interop.WindowNative.GetWindowHandle(this)) / 96.0;
        AppWindow.Resize(new Windows.Graphics.SizeInt32((int)(620 * scale), (int)(760 * scale)));
        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsMinimizable = false;
        }

        ApplyTheme();
        _settings.Changed += OnSettingsChanged;
        Closed += (_, _) =>
        {
            _settings.Changed -= OnSettingsChanged;
            ViewModel.Dispose();
        };
    }

    public SettingsViewModel ViewModel { get; }

    public static Brush SwatchBrush(string? hex) => hex is null
        ? (Brush)Application.Current.Resources["DashboardAccentBrush"]
        : new SolidColorBrush(MainWindow.ParseHex(hex));

    private void OnSettingsChanged(object? sender, EventArgs e) => ApplyTheme();

    private void ApplyTheme()
    {
        var theme = _settings.Current.Theme switch
        {
            ThemePreference.Light => ElementTheme.Light,
            ThemePreference.Dark => ElementTheme.Dark,
            _ => ElementTheme.Default,
        };
        Root.RequestedTheme = theme;
        AppWindow.TitleBar.PreferredTheme = theme switch
        {
            ElementTheme.Light => TitleBarTheme.Light,
            ElementTheme.Dark => TitleBarTheme.Dark,
            _ => TitleBarTheme.UseDefaultAppMode,
        };
    }
}

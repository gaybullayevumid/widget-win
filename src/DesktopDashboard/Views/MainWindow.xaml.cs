using DesktopDashboard.Core.Settings;
using DesktopDashboard.Services;
using DesktopDashboard.ViewModels;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI;
using Windows.UI.ViewManagement;

namespace DesktopDashboard.Views;

public sealed partial class MainWindow : Window
{
    private readonly SettingsService _settings;
    private readonly WidgetBackdrop _backdrop = new();
    private readonly UISettings _uiSettings = new();

    public MainWindow(DashboardViewModel viewModel, SettingsService settings)
    {
        ViewModel = viewModel;
        _settings = settings;
        InitializeComponent();

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(DragRegion);

        var presenter = OverlappedPresenter.Create();
        presenter.SetBorderAndTitleBar(hasBorder: true, hasTitleBar: false);
        presenter.IsMaximizable = false;
        presenter.IsMinimizable = false;
        presenter.PreferredMinimumWidth = 220;
        presenter.PreferredMinimumHeight = 160;
        AppWindow.SetPresenter(presenter);
        AppWindow.Title = "Desktop Dashboard";
        var scale = NativeMethods.GetDpiForWindow(WinRT.Interop.WindowNative.GetWindowHandle(this)) / 96.0;
        AppWindow.Resize(new Windows.Graphics.SizeInt32((int)(420 * scale), (int)(560 * scale)));

        SystemBackdrop = _backdrop;
        ApplyAppearance();

        _settings.Changed += (_, _) => ApplyAppearance();
        Root.ActualThemeChanged += (_, _) => ApplyAppearance();
        _uiSettings.ColorValuesChanged += (_, _) => DispatcherQueue.TryEnqueue(ApplyAppearance);
    }

    public DashboardViewModel ViewModel { get; }

    private void ApplyAppearance()
    {
        var settings = _settings.Current;
        Root.RequestedTheme = settings.Theme switch
        {
            ThemePreference.Light => ElementTheme.Light,
            ThemePreference.Dark => ElementTheme.Dark,
            _ => ElementTheme.Default,
        };

        var isDark = Root.ActualTheme == ElementTheme.Dark;
        _backdrop.Apply(settings.Backdrop, settings.Opacity, isDark);

        // Without a material there is nothing behind the content, so paint a solid background.
        Root.Background = _backdrop.IsActive
            ? null
            : (Brush)Application.Current.Resources["SolidBackgroundFillColorBaseBrush"];

        var accent = (SolidColorBrush)Application.Current.Resources["DashboardAccentBrush"];
        accent.Color = settings.AccentColor is { } hex
            ? ParseHex(hex)
            : _uiSettings.GetColorValue(isDark ? UIColorType.AccentLight2 : UIColorType.AccentDark1);

        PinIcon.Glyph = settings.AlwaysOnTop ? "" : "";
        ToolTipService.SetToolTip(PinButton, settings.AlwaysOnTop ? "Stop keeping on top" : "Keep on top");
        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsAlwaysOnTop = settings.AlwaysOnTop;
        }
    }

    private static Color ParseHex(string hex)
    {
        var value = Convert.ToUInt32(hex[1..], 16);
        return Color.FromArgb(255, (byte)(value >> 16), (byte)(value >> 8), (byte)value);
    }

    private void OnPinClick(object sender, RoutedEventArgs e) =>
        _settings.Update(s => s.AlwaysOnTop = !s.AlwaysOnTop);

    private void OnSettingsClick(object sender, RoutedEventArgs e)
    {
    }

    private void OnHideClick(object sender, RoutedEventArgs e) => AppWindow.Hide();
}

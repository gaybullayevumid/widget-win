using DesktopDashboard.Core.Settings;
using DesktopDashboard.Services;
using DesktopDashboard.ViewModels;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Graphics;
using Windows.UI;
using Windows.UI.ViewManagement;

namespace DesktopDashboard.Views;

public sealed partial class MainWindow : Window
{
    private const int DefaultWidth = 420;
    private const int DefaultHeight = 560;
    private const int DefaultMargin = 24;

    private readonly SettingsService _settings;
    private readonly WidgetBackdrop _backdrop = new();
    private readonly UISettings _uiSettings = new();
    private bool _allowClose;

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
        AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico"));
        RestorePlacement();

        SystemBackdrop = _backdrop;
        ApplyAppearance();

        AppWindow.Changed += OnAppWindowChanged;
        AppWindow.Closing += OnClosing;
        _settings.Changed += (_, _) => ApplyAppearance();
        Root.ActualThemeChanged += (_, _) => ApplyAppearance();
        _uiSettings.ColorValuesChanged += (_, _) => DispatcherQueue.TryEnqueue(ApplyAppearance);
    }

    public DashboardViewModel ViewModel { get; }

    public event EventHandler? SettingsRequested;

    public void ShowWidget(bool activate)
    {
        AppWindow.Show(activate);
        if (activate)
        {
            NativeMethods.SetForegroundWindow(WinRT.Interop.WindowNative.GetWindowHandle(this));
        }
    }

    public void ToggleVisibility()
    {
        if (AppWindow.IsVisible)
        {
            AppWindow.Hide();
        }
        else
        {
            ShowWidget(activate: true);
        }
    }

    public void CloseForExit()
    {
        _allowClose = true;
        Close();
    }

    private void RestorePlacement()
    {
        var workAreas = new List<PixelRect>();
        var displays = DisplayArea.FindAll();
        for (var i = 0; i < displays.Count; i++)
        {
            var area = displays[i].WorkArea;
            workAreas.Add(new PixelRect(area.X, area.Y, area.Width, area.Height));
        }

        var placement = PlacementFitter.Fit(_settings.Current.Placement, workAreas);
        if (placement is null)
        {
            var primary = DisplayArea.Primary.WorkArea;
            var scale = NativeMethods.GetDpiForWindow(WinRT.Interop.WindowNative.GetWindowHandle(this)) / 96.0;
            placement = PlacementFitter.Default(
                new PixelRect(primary.X, primary.Y, primary.Width, primary.Height),
                (int)(DefaultWidth * scale),
                (int)(DefaultHeight * scale),
                (int)(DefaultMargin * scale));
        }

        AppWindow.MoveAndResize(new RectInt32(placement.X, placement.Y, placement.Width, placement.Height));
    }

    private void OnAppWindowChanged(AppWindow sender, AppWindowChangedEventArgs args)
    {
        if (args.DidVisibilityChange)
        {
            ViewModel.SetActive(sender.IsVisible);
        }

        if ((args.DidPositionChange || args.DidSizeChange) && sender.IsVisible)
        {
            var position = sender.Position;
            var size = sender.Size;
            _settings.UpdateSilently(s => s.Placement = new WindowPlacement(position.X, position.Y, size.Width, size.Height));
        }
    }

    /// <summary>Alt+F4 and similar hide the widget to the tray; only Exit really closes it.</summary>
    private void OnClosing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (!_allowClose)
        {
            args.Cancel = true;
            sender.Hide();
        }
    }

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

        AppWindow.IsShownInSwitchers = settings.ShowInTaskbar;
    }

    internal static Color ParseHex(string hex)
    {
        var value = Convert.ToUInt32(hex[1..], 16);
        return Color.FromArgb(255, (byte)(value >> 16), (byte)(value >> 8), (byte)value);
    }

    private void OnPinClick(object sender, RoutedEventArgs e) =>
        _settings.Update(s => s.AlwaysOnTop = !s.AlwaysOnTop);

    private void OnSettingsClick(object sender, RoutedEventArgs e) => SettingsRequested?.Invoke(this, EventArgs.Empty);

    private void OnHideClick(object sender, RoutedEventArgs e) => AppWindow.Hide();
}

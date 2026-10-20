using CommunityToolkit.Mvvm.Input;
using H.NotifyIcon;
using Microsoft.UI.Xaml.Controls;

namespace DesktopDashboard.Services;

public sealed record TrayActions(
    Action ToggleDashboard,
    Action ToggleAlwaysOnTop,
    Action ToggleStartup,
    Action OpenSettings,
    Action Exit);

/// <summary>Notification-area icon: left click shows/hides the dashboard, right click opens the menu.</summary>
public sealed class TrayIconService : IDisposable
{
    private TaskbarIcon? _icon;
    private ToggleMenuFlyoutItem? _alwaysOnTop;
    private ToggleMenuFlyoutItem? _startup;

    public void Initialize(TrayActions actions)
    {
        _alwaysOnTop = new ToggleMenuFlyoutItem { Text = "Keep on top", Command = new RelayCommand(actions.ToggleAlwaysOnTop) };
        _startup = new ToggleMenuFlyoutItem { Text = "Launch at startup", Command = new RelayCommand(actions.ToggleStartup) };

        var menu = new MenuFlyout();
        menu.Items.Add(new MenuFlyoutItem { Text = "Show / hide dashboard", Command = new RelayCommand(actions.ToggleDashboard) });
        menu.Items.Add(new MenuFlyoutItem { Text = "Settings…", Command = new RelayCommand(actions.OpenSettings) });
        menu.Items.Add(new MenuFlyoutSeparator());
        menu.Items.Add(_alwaysOnTop);
        menu.Items.Add(_startup);
        menu.Items.Add(new MenuFlyoutSeparator());
        menu.Items.Add(new MenuFlyoutItem { Text = "Exit", Command = new RelayCommand(actions.Exit) });

        _icon = new TaskbarIcon
        {
            ToolTipText = "Desktop Dashboard",
            Icon = new System.Drawing.Icon(Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico")),
            ContextMenuMode = ContextMenuMode.PopupMenu,
            ContextFlyout = menu,
            NoLeftClickDelay = true,
            LeftClickCommand = new RelayCommand(actions.ToggleDashboard),
        };

        // Efficiency mode would throttle the polling loop; the dashboard is meant to stay live.
        _icon.ForceCreate(enablesEfficiencyMode: false);
    }

    /// <summary>Keeps the check marks in the menu in sync with the current state.</summary>
    public void Update(bool alwaysOnTop, bool launchAtStartup)
    {
        if (_alwaysOnTop is not null)
        {
            _alwaysOnTop.IsChecked = alwaysOnTop;
        }

        if (_startup is not null)
        {
            _startup.IsChecked = launchAtStartup;
        }
    }

    public void Dispose()
    {
        _icon?.Dispose();
        _icon = null;
    }
}

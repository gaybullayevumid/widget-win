namespace DesktopDashboard.Services;

/// <summary>Application-level actions that view models may trigger.</summary>
public interface IAppCommands
{
    void SetLaunchAtStartup(bool enable);

    /// <summary>Stops monitoring, saves settings, removes the tray icon and ends the process.</summary>
    void Shutdown();
}

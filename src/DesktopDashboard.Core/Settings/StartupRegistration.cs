using Microsoft.Win32;

namespace DesktopDashboard.Core.Settings;

public interface IStartupRegistration
{
    bool IsEnabled { get; }

    void Enable();

    void Disable();
}

/// <summary>
/// Registers the app under the per-user Run key (no admin rights needed). The stored command
/// is compared with the current executable path so a moved install is detected as disabled.
/// </summary>
public sealed class RunKeyStartupRegistration : IStartupRegistration
{
    public const string StartupArgument = "--startup";
    public const string DefaultRunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

    private readonly string _runKeyPath;
    private readonly string _valueName;
    private readonly string _command;

    public RunKeyStartupRegistration(string executablePath, string valueName = "DesktopDashboard", string runKeyPath = DefaultRunKeyPath)
    {
        _runKeyPath = runKeyPath;
        _valueName = valueName;
        _command = $"\"{executablePath}\" {StartupArgument}";
    }

    public bool IsEnabled
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(_runKeyPath);
            return string.Equals(key?.GetValue(_valueName) as string, _command, StringComparison.OrdinalIgnoreCase);
        }
    }

    public void Enable()
    {
        using var key = Registry.CurrentUser.CreateSubKey(_runKeyPath, writable: true);
        key.SetValue(_valueName, _command, RegistryValueKind.String);
    }

    public void Disable()
    {
        using var key = Registry.CurrentUser.OpenSubKey(_runKeyPath, writable: true);
        key?.DeleteValue(_valueName, throwOnMissingValue: false);
    }
}

using DesktopDashboard.Core.Settings;
using Microsoft.Win32;

namespace DesktopDashboard.Core.Tests;

public sealed class JsonSettingsStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "DesktopDashboardTests", Guid.NewGuid().ToString("N"));

    private string SettingsPath => Path.Combine(_dir, "settings.json");

    [Fact]
    public void Missing_file_gives_defaults()
    {
        var settings = new JsonSettingsStore(SettingsPath).Load();

        Assert.Equal(1000, settings.PollIntervalMs);
        Assert.True(settings.Cards.Cpu);
    }

    [Fact]
    public void Round_trips_all_values()
    {
        var store = new JsonSettingsStore(SettingsPath);
        var original = new AppSettings
        {
            Placement = new WindowPlacement(10, 20, 300, 400),
            AlwaysOnTop = true,
            Theme = ThemePreference.Light,
            Backdrop = BackdropKind.Mica,
            AccentColor = "#FF8800",
            Opacity = 0.5,
            Size = WidgetSize.Large,
            PollIntervalMs = 2000,
            Use24HourClock = false,
        };
        original.Cards.Gpu = false;

        store.Save(original);
        var loaded = store.Load();

        Assert.Equal(original.Placement, loaded.Placement);
        Assert.True(loaded.AlwaysOnTop);
        Assert.Equal(ThemePreference.Light, loaded.Theme);
        Assert.Equal(BackdropKind.Mica, loaded.Backdrop);
        Assert.Equal("#FF8800", loaded.AccentColor);
        Assert.Equal(0.5, loaded.Opacity);
        Assert.Equal(WidgetSize.Large, loaded.Size);
        Assert.Equal(2000, loaded.PollIntervalMs);
        Assert.False(loaded.Use24HourClock);
        Assert.False(loaded.Cards.Gpu);
        Assert.True(loaded.Cards.Cpu);
        Assert.False(File.Exists(SettingsPath + ".tmp"));
    }

    [Fact]
    public void Corrupt_file_falls_back_to_defaults_and_is_kept()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(SettingsPath, "{ not json");

        var settings = new JsonSettingsStore(SettingsPath).Load();

        Assert.Equal(1000, settings.PollIntervalMs);
        Assert.Equal("{ not json", File.ReadAllText(SettingsPath + ".corrupt"));
    }

    [Fact]
    public void Hand_edited_values_are_clamped()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(SettingsPath, """
            { "pollIntervalMs": 5, "opacity": 7, "accentColor": "red", "placement": { "x": 0, "y": 0, "width": 0, "height": 10 } }
            """);

        var settings = new JsonSettingsStore(SettingsPath).Load();

        Assert.Equal(AppSettings.MinPollIntervalMs, settings.PollIntervalMs);
        Assert.Equal(1.0, settings.Opacity);
        Assert.Null(settings.AccentColor);
        Assert.Null(settings.Placement);
    }

    [Fact]
    public void Settings_service_debounces_and_flushes_on_dispose()
    {
        var store = new JsonSettingsStore(SettingsPath);
        using (var service = new SettingsService(store))
        {
            service.Update(s => s.Opacity = 0.4);
            service.Update(s => s.Opacity = 0.6);
            Assert.False(File.Exists(SettingsPath));
        }

        Assert.Equal(0.6, store.Load().Opacity);
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }
}

public sealed class StartupRegistrationTests : IDisposable
{
    private const string TestKey = @"Software\DesktopDashboard.Tests\Run";

    [Fact]
    public void Enable_and_disable_round_trip()
    {
        var registration = new RunKeyStartupRegistration(@"C:\Apps\Dashboard\DesktopDashboard.exe", "Test", TestKey);

        Assert.False(registration.IsEnabled);
        registration.Enable();
        Assert.True(registration.IsEnabled);
        using (var key = Registry.CurrentUser.OpenSubKey(TestKey))
        {
            Assert.Equal("\"C:\\Apps\\Dashboard\\DesktopDashboard.exe\" --startup", key!.GetValue("Test"));
        }

        registration.Disable();
        Assert.False(registration.IsEnabled);
    }

    [Fact]
    public void Entry_for_another_path_does_not_count_as_enabled()
    {
        new RunKeyStartupRegistration(@"C:\Old\DesktopDashboard.exe", "Test", TestKey).Enable();

        Assert.False(new RunKeyStartupRegistration(@"C:\New\DesktopDashboard.exe", "Test", TestKey).IsEnabled);
    }

    public void Dispose() => Registry.CurrentUser.DeleteSubKeyTree(@"Software\DesktopDashboard.Tests", throwOnMissingSubKey: false);
}

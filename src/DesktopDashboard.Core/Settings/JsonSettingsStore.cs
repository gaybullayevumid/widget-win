using System.Diagnostics;
using System.Text.Json;

namespace DesktopDashboard.Core.Settings;

public interface ISettingsStore
{
    AppSettings Load();

    void Save(AppSettings settings);
}

/// <summary>
/// Stores settings as JSON. Writes go to a temporary file that then replaces the real one,
/// so a crash or power loss mid-write cannot leave a half-written settings file.
/// </summary>
public sealed class JsonSettingsStore : ISettingsStore
{
    private readonly string _path;
    private readonly Lock _gate = new();

    public JsonSettingsStore(string path)
    {
        _path = path;
    }

    public static string DefaultPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "DesktopDashboard",
        "settings.json");

    public AppSettings Load()
    {
        lock (_gate)
        {
            if (!File.Exists(_path))
            {
                return new AppSettings();
            }

            try
            {
                using var stream = File.OpenRead(_path);
                var settings = JsonSerializer.Deserialize(stream, SettingsJsonContext.Default.AppSettings);
                return (settings ?? new AppSettings()).Normalize();
            }
            catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
            {
                Trace.TraceWarning($"Settings file unreadable, using defaults: {ex.Message}");
                TryBackUpCorruptFile();
                return new AppSettings();
            }
        }
    }

    public void Save(AppSettings settings)
    {
        lock (_gate)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            var tempPath = _path + ".tmp";
            using (var stream = File.Create(tempPath))
            {
                JsonSerializer.Serialize(stream, settings, SettingsJsonContext.Default.AppSettings);
            }

            File.Move(tempPath, _path, overwrite: true);
        }
    }

    private void TryBackUpCorruptFile()
    {
        try
        {
            File.Copy(_path, _path + ".corrupt", overwrite: true);
        }
        catch (IOException)
        {
        }
    }
}

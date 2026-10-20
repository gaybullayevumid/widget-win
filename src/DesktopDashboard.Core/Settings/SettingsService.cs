using System.Diagnostics;

namespace DesktopDashboard.Core.Settings;

/// <summary>
/// Owns the live <see cref="AppSettings"/> instance. Changes are announced immediately and
/// written to disk after a short quiet period, so dragging a slider or the window does not
/// write the file dozens of times per second.
/// </summary>
public sealed class SettingsService : IDisposable
{
    private static readonly TimeSpan SaveDelay = TimeSpan.FromMilliseconds(750);

    private readonly ISettingsStore _store;
    private readonly Timer _saveTimer;
    private readonly Lock _gate = new();
    private bool _dirty;

    public SettingsService(ISettingsStore store)
    {
        _store = store;
        Current = store.Load();
        _saveTimer = new Timer(_ => Flush(), null, Timeout.Infinite, Timeout.Infinite);
    }

    public AppSettings Current { get; }

    /// <summary>Raised on the caller's thread after <see cref="Update"/> applied a change.</summary>
    public event EventHandler? Changed;

    public void Update(Action<AppSettings> change)
    {
        UpdateSilently(change);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Persists a change without raising <see cref="Changed"/>, for state nobody needs to react to
    /// (e.g. the window position, which changes many times per second while dragging).
    /// </summary>
    public void UpdateSilently(Action<AppSettings> change)
    {
        lock (_gate)
        {
            change(Current);
            Current.Normalize();
            _dirty = true;
            _saveTimer.Change(SaveDelay, Timeout.InfiniteTimeSpan);
        }
    }

    public void Flush()
    {
        lock (_gate)
        {
            if (!_dirty)
            {
                return;
            }

            try
            {
                _store.Save(Current);
                _dirty = false;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Trace.TraceError($"Saving settings failed: {ex.Message}");
            }
        }
    }

    public void Dispose()
    {
        _saveTimer.Dispose();
        Flush();
    }
}

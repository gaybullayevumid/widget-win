using System.Text.Json.Serialization;

namespace DesktopDashboard.Core.Settings;

public enum ThemePreference
{
    System,
    Light,
    Dark,
}

public enum BackdropKind
{
    Acrylic,
    Mica,
    Solid,
}

public enum WidgetSize
{
    Small,
    Medium,
    Large,
}

/// <summary>Where the widget sits in the window stack.</summary>
public enum WindowLayer
{
    /// <summary>Always below other windows, like part of the desktop.</summary>
    Desktop,

    /// <summary>An ordinary window that comes forward when clicked.</summary>
    Normal,

    /// <summary>Always above other windows.</summary>
    OnTop,
}

public sealed record WindowPlacement(int X, int Y, int Width, int Height);

public sealed class CardVisibility
{
    public bool Clock { get; set; } = true;
    public bool Cpu { get; set; } = true;
    public bool Memory { get; set; } = true;
    public bool Gpu { get; set; } = true;
    public bool Battery { get; set; } = true;
    public bool Network { get; set; } = true;
}

public sealed class AppSettings
{
    public const int MinPollIntervalMs = 500;
    public const int MaxPollIntervalMs = 10_000;
    public const double MinOpacity = 0.2;

    public int SchemaVersion { get; set; } = 1;

    public WindowPlacement? Placement { get; set; }

    public WindowLayer Layer { get; set; } = WindowLayer.Desktop;

    public bool ShowInTaskbar { get; set; }

    public bool LaunchAtStartup { get; set; }

    public ThemePreference Theme { get; set; } = ThemePreference.System;

    public BackdropKind Backdrop { get; set; } = BackdropKind.Acrylic;

    /// <summary>Accent color as #RRGGBB, or null to follow the Windows accent color.</summary>
    public string? AccentColor { get; set; }

    /// <summary>0.2 (mostly see-through) to 1.0 (opaque).</summary>
    public double Opacity { get; set; } = 0.85;

    public WidgetSize Size { get; set; } = WidgetSize.Medium;

    public CardVisibility Cards { get; set; } = new();

    public int PollIntervalMs { get; set; } = 1000;

    public bool Use24HourClock { get; set; } = true;

    public bool ShowSeconds { get; set; } = true;

    /// <summary>GPU adapter name to show, or null for the one with the most dedicated memory.</summary>
    public string? GpuAdapter { get; set; }

    /// <summary>Clamps values that may have been hand-edited in the JSON file.</summary>
    public AppSettings Normalize()
    {
        PollIntervalMs = Math.Clamp(PollIntervalMs, MinPollIntervalMs, MaxPollIntervalMs);
        Opacity = double.IsFinite(Opacity) ? Math.Clamp(Opacity, MinOpacity, 1.0) : 0.85;
        Cards ??= new CardVisibility();
        if (AccentColor is not null && !IsHexColor(AccentColor))
        {
            AccentColor = null;
        }

        if (Placement is { Width: <= 0 } or { Height: <= 0 })
        {
            Placement = null;
        }

        return this;
    }

    public static bool IsHexColor(string value) =>
        value.Length == 7 && value[0] == '#'
        && uint.TryParse(value.AsSpan(1), System.Globalization.NumberStyles.AllowHexSpecifier, null, out _);
}

[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UseStringEnumConverter = true)]
[JsonSerializable(typeof(AppSettings))]
internal sealed partial class SettingsJsonContext : JsonSerializerContext;

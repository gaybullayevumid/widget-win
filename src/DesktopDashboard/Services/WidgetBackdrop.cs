using DesktopDashboard.Core.Settings;
using Microsoft.UI.Composition;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace DesktopDashboard.Services;

/// <summary>
/// Acrylic or Mica backdrop whose tint follows the widget opacity setting.
/// <para>
/// The stock <see cref="DesktopAcrylicBackdrop"/> falls back to a flat color whenever the window
/// is inactive. A desktop widget is inactive almost all the time, so this backdrop keeps its own
/// configuration with <see cref="SystemBackdropConfiguration.IsInputActive"/> pinned to true.
/// </para>
/// </summary>
public sealed partial class WidgetBackdrop : SystemBackdrop
{
    private static readonly Color DarkTint = Color.FromArgb(255, 0x1C, 0x1C, 0x1C);
    private static readonly Color LightTint = Color.FromArgb(255, 0xF3, 0xF3, 0xF3);

    private readonly SystemBackdropConfiguration _configuration = new() { IsInputActive = true };
    private ICompositionSupportsSystemBackdrop? _target;
    private ISystemBackdropControllerWithTargets? _controller;
    private BackdropKind _kind = BackdropKind.Acrylic;
    private double _opacity = 0.85;
    private bool _isDark = true;

    /// <summary>False when the requested material is unavailable and nothing is drawn behind the content.</summary>
    public bool IsActive => _controller is not null;

    public void Apply(BackdropKind kind, double opacity, bool isDark)
    {
        var kindChanged = kind != _kind;
        _kind = kind;
        _opacity = opacity;
        _isDark = isDark;

        if (kindChanged)
        {
            CreateController();
        }
        else
        {
            ApplyTint();
        }
    }

    protected override void OnTargetConnected(ICompositionSupportsSystemBackdrop connectedTarget, XamlRoot xamlRoot)
    {
        base.OnTargetConnected(connectedTarget, xamlRoot);
        _target = connectedTarget;
        CreateController();
    }

    protected override void OnTargetDisconnected(ICompositionSupportsSystemBackdrop disconnectedTarget)
    {
        base.OnTargetDisconnected(disconnectedTarget);
        DestroyController();
        _target = null;
    }

    private void CreateController()
    {
        DestroyController();
        if (_target is null)
        {
            return;
        }

        _controller = _kind switch
        {
            BackdropKind.Acrylic when DesktopAcrylicController.IsSupported() => new DesktopAcrylicController(),
            BackdropKind.Mica when MicaController.IsSupported() => new MicaController(),
            _ => null,
        };

        if (_controller is null)
        {
            return;
        }

        _controller.AddSystemBackdropTarget(_target);
        _controller.SetSystemBackdropConfiguration(_configuration);
        ApplyTint();
    }

    private void ApplyTint()
    {
        _configuration.Theme = _isDark ? SystemBackdropTheme.Dark : SystemBackdropTheme.Light;
        var tint = _isDark ? DarkTint : LightTint;

        // Opacity 1.0 should look nearly solid; the minimum should still keep text readable.
        switch (_controller)
        {
            case DesktopAcrylicController acrylic:
                acrylic.TintColor = tint;
                acrylic.FallbackColor = tint;
                acrylic.TintOpacity = (float)Lerp(0.0, 0.85, _opacity);
                acrylic.LuminosityOpacity = (float)Lerp(0.15, 0.96, _opacity);
                break;
            case MicaController mica:
                mica.TintColor = tint;
                mica.FallbackColor = tint;
                mica.TintOpacity = (float)Lerp(0.2, 0.9, _opacity);
                mica.LuminosityOpacity = (float)Lerp(0.4, 1.0, _opacity);
                break;
        }
    }

    private void DestroyController()
    {
        if (_controller is null)
        {
            return;
        }

        if (_target is not null)
        {
            _controller.RemoveSystemBackdropTarget(_target);
        }

        _controller.Dispose();
        _controller = null;
    }

    private static double Lerp(double from, double to, double t) =>
        from + ((to - from) * Math.Clamp((t - AppSettings.MinOpacity) / (1 - AppSettings.MinOpacity), 0, 1));
}

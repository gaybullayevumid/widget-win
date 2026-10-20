using Microsoft.UI.Xaml;

namespace DesktopDashboard.Controls;

/// <summary>Static helpers for x:Bind function bindings.</summary>
public static class Converters
{
    public static Visibility VisibleIfText(string? text) =>
        string.IsNullOrEmpty(text) ? Visibility.Collapsed : Visibility.Visible;

    public static Visibility VisibleIfNotNull(object? value) =>
        value is null ? Visibility.Collapsed : Visibility.Visible;

    public static Visibility VisibleIfNumber(double value) =>
        double.IsNaN(value) ? Visibility.Collapsed : Visibility.Visible;

    public static double NanToZero(double value) => double.IsNaN(value) ? 0 : value;
}

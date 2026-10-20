namespace DesktopDashboard.Core.Settings;

/// <summary>A rectangle in physical screen pixels.</summary>
public readonly record struct PixelRect(int X, int Y, int Width, int Height)
{
    public int Right => X + Width;

    public int Bottom => Y + Height;

    public long IntersectionArea(PixelRect other)
    {
        var width = Math.Min(Right, other.Right) - Math.Max(X, other.X);
        var height = Math.Min(Bottom, other.Bottom) - Math.Max(Y, other.Y);
        return width > 0 && height > 0 ? (long)width * height : 0;
    }
}

/// <summary>
/// Restores a saved window placement only where it can still be seen: a monitor may have been
/// unplugged, rearranged or changed resolution since the position was saved.
/// </summary>
public static class PlacementFitter
{
    /// <summary>
    /// Moves (and if needed shrinks) <paramref name="saved"/> fully onto the work area it overlaps
    /// most. Returns null when it overlaps no work area, so the caller can use a default position.
    /// </summary>
    public static WindowPlacement? Fit(WindowPlacement? saved, IReadOnlyList<PixelRect> workAreas)
    {
        if (saved is null || workAreas.Count == 0)
        {
            return null;
        }

        var rect = new PixelRect(saved.X, saved.Y, saved.Width, saved.Height);
        var best = workAreas.MaxBy(rect.IntersectionArea);
        if (best.IntersectionArea(rect) == 0)
        {
            return null;
        }

        var width = Math.Min(saved.Width, best.Width);
        var height = Math.Min(saved.Height, best.Height);
        var x = Math.Clamp(saved.X, best.X, best.Right - width);
        var y = Math.Clamp(saved.Y, best.Y, best.Bottom - height);
        return new WindowPlacement(x, y, width, height);
    }

    /// <summary>Top-right corner of the work area, where desktop widgets conventionally sit.</summary>
    public static WindowPlacement Default(PixelRect workArea, int width, int height, int margin)
    {
        width = Math.Min(width, workArea.Width);
        height = Math.Min(height, workArea.Height);
        return new WindowPlacement(
            Math.Max(workArea.X, workArea.Right - width - margin),
            Math.Min(workArea.Y + margin, workArea.Bottom - height),
            width,
            height);
    }
}

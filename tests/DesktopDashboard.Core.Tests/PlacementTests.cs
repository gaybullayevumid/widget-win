using DesktopDashboard.Core.Settings;

namespace DesktopDashboard.Core.Tests;

public class PlacementFitterTests
{
    private static readonly PixelRect Primary = new(0, 0, 1920, 1032);
    private static readonly PixelRect Secondary = new(1920, 0, 2560, 1400);

    [Fact]
    public void Visible_placement_is_kept_as_is()
    {
        var saved = new WindowPlacement(100, 100, 400, 500);

        Assert.Equal(saved, PlacementFitter.Fit(saved, [Primary, Secondary]));
    }

    [Fact]
    public void Placement_on_unplugged_monitor_is_dropped()
    {
        var onSecondary = new WindowPlacement(2500, 200, 400, 500);

        Assert.Null(PlacementFitter.Fit(onSecondary, [Primary]));
    }

    [Fact]
    public void Partly_off_screen_placement_is_pulled_back_in()
    {
        var saved = new WindowPlacement(1700, 900, 400, 500);

        Assert.Equal(new WindowPlacement(1520, 532, 400, 500), PlacementFitter.Fit(saved, [Primary]));
    }

    [Fact]
    public void Oversized_placement_is_shrunk_to_the_work_area()
    {
        var saved = new WindowPlacement(10, 10, 3000, 2000);

        Assert.Equal(new WindowPlacement(0, 0, 1920, 1032), PlacementFitter.Fit(saved, [Primary]));
    }

    [Fact]
    public void Straddling_placement_goes_to_the_monitor_it_overlaps_most()
    {
        var saved = new WindowPlacement(1800, 100, 400, 500);

        Assert.Equal(new WindowPlacement(1920, 100, 400, 500), PlacementFitter.Fit(saved, [Primary, Secondary]));
    }

    [Fact]
    public void Default_is_top_right_with_margin()
    {
        Assert.Equal(new WindowPlacement(1496, 24, 400, 560), PlacementFitter.Default(Primary, 400, 560, 24));
    }
}

using DesktopDashboard.Core.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;
using Path = Microsoft.UI.Xaml.Shapes.Path;

namespace DesktopDashboard.Controls;

/// <summary>
/// Area chart of a <see cref="MetricHistory"/>. Missing samples break the line instead of
/// being drawn as zero.
/// </summary>
public sealed partial class Sparkline : Grid
{
    public static readonly DependencyProperty SourceProperty = DependencyProperty.Register(
        nameof(Source), typeof(MetricHistory), typeof(Sparkline), new PropertyMetadata(null, OnSourceChanged));

    public static readonly DependencyProperty MaximumProperty = DependencyProperty.Register(
        nameof(Maximum), typeof(double), typeof(Sparkline), new PropertyMetadata(double.NaN, (d, _) => ((Sparkline)d).Redraw()));

    public static readonly DependencyProperty StrokeProperty = DependencyProperty.Register(
        nameof(Stroke), typeof(Brush), typeof(Sparkline), new PropertyMetadata(null, OnStrokeChanged));

    private readonly Path _area = new() { Opacity = 0.22 };
    private readonly Path _line = new() { StrokeThickness = 1.5, StrokeLineJoin = PenLineJoin.Round };
    private MetricHistory? _subscribed;

    public Sparkline()
    {
        Children.Add(_area);
        Children.Add(_line);
        SizeChanged += (_, _) => Redraw();
        Loaded += (_, _) => Subscribe(Source);
        Unloaded += (_, _) => Subscribe(null);
    }

    public MetricHistory? Source
    {
        get => (MetricHistory?)GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    /// <summary>Top of the scale, or NaN to fit the largest visible value.</summary>
    public double Maximum
    {
        get => (double)GetValue(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    public Brush? Stroke
    {
        get => (Brush?)GetValue(StrokeProperty);
        set => SetValue(StrokeProperty, value);
    }

    private static void OnSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var sparkline = (Sparkline)d;
        if (sparkline.IsLoaded)
        {
            sparkline.Subscribe(e.NewValue as MetricHistory);
        }
    }

    private static void OnStrokeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var sparkline = (Sparkline)d;
        sparkline._line.Stroke = (Brush?)e.NewValue;
        sparkline._area.Fill = (Brush?)e.NewValue;
    }

    private void Subscribe(MetricHistory? history)
    {
        if (_subscribed is not null)
        {
            _subscribed.Changed -= OnHistoryChanged;
        }

        _subscribed = history;
        if (history is not null)
        {
            history.Changed += OnHistoryChanged;
        }

        Redraw();
    }

    private void OnHistoryChanged(object? sender, EventArgs e) => Redraw();

    private void Redraw()
    {
        var history = _subscribed;
        var width = ActualWidth;
        var height = ActualHeight;
        if (history is null || width <= 0 || height <= 0)
        {
            _line.Data = null;
            _area.Data = null;
            return;
        }

        var max = double.IsNaN(Maximum) ? Math.Max(history.MaxOrDefault(1), 1) * 1.15 : Maximum;
        var step = width / (history.Capacity - 1);
        var line = new PathGeometry();
        var area = new PathGeometry();
        PolyLineSegment? lineSegment = null;
        PolyLineSegment? areaSegment = null;
        var lastX = 0.0;

        for (var i = 0; i < history.Capacity; i++)
        {
            var value = history[i];
            if (double.IsNaN(value))
            {
                CloseArea();
                lineSegment = null;
                continue;
            }

            var x = i * step;
            var y = height - Math.Clamp(value / max, 0, 1) * (height - 1.5) - 0.75;
            var point = new Point(x, y);
            if (lineSegment is null)
            {
                lineSegment = new PolyLineSegment();
                line.Figures.Add(new PathFigure { StartPoint = point, Segments = { lineSegment } });
                areaSegment = new PolyLineSegment();
                area.Figures.Add(new PathFigure { StartPoint = new Point(x, height), IsClosed = true, Segments = { areaSegment } });
            }
            else
            {
                lineSegment.Points.Add(point);
            }

            areaSegment!.Points.Add(point);
            lastX = x;
        }

        CloseArea();
        _line.Data = line;
        _area.Data = area;

        void CloseArea()
        {
            if (areaSegment is not null)
            {
                areaSegment.Points.Add(new Point(lastX, height));
                areaSegment = null;
            }
        }
    }
}

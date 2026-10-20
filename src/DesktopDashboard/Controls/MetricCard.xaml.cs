using System.Numerics;
using DesktopDashboard.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace DesktopDashboard.Controls;

public sealed partial class MetricCard : UserControl
{
    public static readonly DependencyProperty ViewModelProperty = DependencyProperty.Register(
        nameof(ViewModel), typeof(CardViewModel), typeof(MetricCard), new PropertyMetadata(null, (d, _) => ((MetricCard)d).Bindings.Update()));

    public MetricCard()
    {
        InitializeComponent();
        Chrome.SizeChanged += OnChromeSizeChanged;
    }

    public double DesignHeight => 120;

    public CardViewModel ViewModel
    {
        get => (CardViewModel)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    private void OnChromeSizeChanged(object sender, SizeChangedEventArgs e)
    {
        var size = e.NewSize;
        Chrome.CenterPoint = new Vector3((float)size.Width / 2, (float)size.Height / 2, 0);
        if (size.Height > 0)
        {
            Body.Width =Math.Max(120, size.Width * DesignHeight / size.Height);
        }
    }

    private void OnPointerEntered(object sender, PointerRoutedEventArgs e) => Chrome.Scale = new Vector3(1.025f, 1.025f, 1);

    private void OnPointerExited(object sender, PointerRoutedEventArgs e) => Chrome.Scale = Vector3.One;
}

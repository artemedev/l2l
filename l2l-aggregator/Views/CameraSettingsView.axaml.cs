using Avalonia.Controls;
using l2l_aggregator.ViewModels;

namespace l2l_aggregator.Views;

public partial class CameraSettingsView : UserControl
{
    public CameraSettingsView()
    {
        InitializeComponent();
    }
    private void OnImageSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        if (DataContext is AggregationViewModel vm)
        {
            vm.ImageSize = e.NewSize;
        }
    }
}
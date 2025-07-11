using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace l2l_aggregator.ViewModels.VisualElements
{
    public partial class DmSquareViewModel : ObservableObject
    {
        [ObservableProperty] private double x;
        [ObservableProperty] private double y;
        [ObservableProperty] private double sizeWidth;
        [ObservableProperty] private double sizeHeight;
        [ObservableProperty] private double angle;
        [ObservableProperty] private bool isValid;

        [ObservableProperty] private string data;

        public IBrush BorderColor => IsValid ? Brushes.Green : Brushes.Red;
        partial void OnIsValidChanged(bool value) => OnPropertyChanged(nameof(BorderColor));
    }
}

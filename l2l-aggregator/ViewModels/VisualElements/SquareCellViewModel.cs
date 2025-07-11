using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace l2l_aggregator.ViewModels.VisualElements
{
    public partial class SquareCellViewModel : ObservableObject
    {
        // Координаты OCR-квадрата
        [ObservableProperty] private double x;
        [ObservableProperty] private double y;
        [ObservableProperty] private double sizeHeight;
        [ObservableProperty] private double sizeWidth;
        [ObservableProperty] private double angle;
        [ObservableProperty] private string ocrName;
        [ObservableProperty] private string ocrText;

        // Если нужно отмечать валидность
        [ObservableProperty] private bool isValid;

        public IBrush BorderColor => IsValid ? Brushes.Green : Brushes.Red;
        partial void OnIsValidChanged(bool value) => OnPropertyChanged(nameof(BorderColor));
    }
}

using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Transformation;
using Avalonia.Styling;
using l2l_aggregator.ViewModels;
using l2l_aggregator.ViewModels.VisualElements;
using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace l2l_aggregator.Views;

public partial class AggregationView : UserControl
{
    public AggregationView()
    {
        InitializeComponent();
        var vm = DataContext as AggregationViewModel;
        this.DataContextChanged += OnDataContextChanged;

        // Подписка на изменение Bounds у ScannedImageControl:
    }
    private AggregationViewModel? _lastViewModel;

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_lastViewModel != null)
            _lastViewModel.DMCells.CollectionChanged -= OnCellsCollectionChanged;

        if (DataContext is AggregationViewModel vm)
        {
            vm.DMCells.CollectionChanged += OnCellsCollectionChanged;
            _lastViewModel = vm;
        }
    }
    private void OnCellsCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
    {
        MainCanvas.Children.Clear(); // ОЧИСТКА перед добавлением новых

        foreach (var cellVM in ((ObservableCollection<DmCellViewModel>)sender))
        {
            var border = new Border
            {
                BorderBrush = cellVM.BorderColor,
                BorderThickness = new Thickness(3),
                Background = Brushes.Transparent,
                Width = cellVM.SizeWidth,
                Height = cellVM.SizeHeight,
                DataContext = cellVM
            };

            // Установка координат
            Canvas.SetLeft(border, cellVM.X);
            Canvas.SetTop(border, cellVM.Y);

            //// Только поворот
            border.RenderTransform = new RotateTransform(cellVM.Angle, 0, 0);

            border.PointerPressed += (s, e) =>
                {
                    //((AggregationViewModel)DataContext).OnCellClicked(cellVM);
                    if (cellVM.DmSquareClickedCommand.CanExecute(null))
                        cellVM.DmSquareClickedCommand.Execute(null);
                };

            MainCanvas.Children.Add(border);
        }
    }
    private void OnImageSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        if (DataContext is AggregationViewModel vm)
        {
            vm.ImageSize = e.NewSize;
        }
    }
    private void OnImageSizeCellChanged(object? sender, SizeChangedEventArgs e)
    {
        if (DataContext is AggregationViewModel vm)
        {
            vm.ImageSizeCell = e.NewSize;
        }
    }
    //private void OnImageGridSizeCellChanged(object? sender, SizeChangedEventArgs e)
    //{
    //    if (DataContext is AggregationViewModel vm)
    //    {
    //        vm.ImageSizeGridCell = e.NewSize;
    //    }
    //}
    private void Popup_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is AggregationViewModel vm)
        {
            vm.IsPopupOpen = false; // Закрываем Popup через привязанное свойство
        }
    }

    public void OnRawScanReceived(string scannedData)
    {
        // Найдём активную ViewModel
        if (DataContext is AggregationViewModel vm)
        {
            vm.HandleScannedBarcode(scannedData);
        }
    }
}
using Avalonia.Controls;
using l2l_aggregator.ViewModels;

namespace l2l_aggregator.Views;

public partial class TaskDetailsView : UserControl
{
    private bool _layoutInitialized = false;
    public TaskDetailsView()
    {
        InitializeComponent();
        this.LayoutUpdated += (_, __) =>
        {
            if (_layoutInitialized)
                return;

            if (DataContext is ViewModelBase vm)
            {
                var width = this.Bounds.Width;
                var height = this.Bounds.Height;

                if (width > 0 && height > 0)
                {
                    vm.WindowWidth = width;
                    vm.WindowHeight = height;
                    _layoutInitialized = true; // один раз
                }
            }
        };
    }
}
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using System.Linq;

namespace l2l_aggregator.Views;

public partial class InitializationView : UserControl
{
    private KeyboardWindow? _keyboard;
    public InitializationView()
    {
        InitializeComponent();
    }
}
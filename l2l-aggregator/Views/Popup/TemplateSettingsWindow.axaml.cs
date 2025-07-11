using Avalonia.Controls;
using Avalonia.Interactivity;

namespace l2l_aggregator.Views.Popup;

public partial class TemplateSettingsWindow : Window
{
    public TemplateSettingsWindow()
    {
        InitializeComponent();
    }
    private void OnClose(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
using Avalonia.Markup.Xaml;

namespace l2l_aggregator.Controls.Keyboard.Layout;
public partial class VirtualKeyboardLayoutNumpad : KeyboardLayout
{
    public VirtualKeyboardLayoutNumpad()
    {
        LayoutName = "numpad";
        InitializeComponent();
    }
    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
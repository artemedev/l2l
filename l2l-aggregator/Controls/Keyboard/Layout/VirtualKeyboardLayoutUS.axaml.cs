using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace l2l_aggregator.Controls.Keyboard.Layout;
public partial class VirtualKeyboardLayoutUS : KeyboardLayout
{
    public VirtualKeyboardLayoutUS()
    {
        LayoutName = "en-US";
        InitializeComponent();
        var keyOpenBrace = this.FindControl<VirtualKey>("KeyOpenBrace");
        var keyCloseBrace = this.FindControl<VirtualKey>("KeyCloseBrace");

        if (keyOpenBrace != null) keyOpenBrace.ShiftKey = "{";
        if (keyCloseBrace != null) keyCloseBrace.ShiftKey = "}";
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using System;

namespace l2l_aggregator.Controls.Keyboard;

public partial class CoporateWindow : Window
{
    public object? CoporateContent
    {
        get => coporateContent;
        set => SetAndRaise(CoporateContentProperty, ref coporateContent, value);
    }

    private void SetAndRaise(StyledProperty<object?> coporateContentProperty, ref object? coporateContent, object? value)
    {
        throw new NotImplementedException();
    }

    private object? coporateContent;

    public static readonly StyledProperty<object?> CoporateContentProperty =
        AvaloniaProperty.Register<CoporateWindow, object?>(nameof(CoporateContent));

    private Window? ParentWindow => Owner as Window;

    public CoporateWindow()
    {
        InitializeComponent();

        SystemDecorations = SystemDecorations.None;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Opened += OnOpened;

#if DEBUG
        this.AttachDevTools();
#endif
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void OnOpened(object? sender, EventArgs e)
    {
        CenterDialog();
    }

    private void CenterDialog()
    {
        if (ParentWindow == null)
            return;

        double x = ParentWindow.Position.X + (ParentWindow.Bounds.Width - Width) / 2;
        double y = ParentWindow.Position.Y + (ParentWindow.Bounds.Height - Height);

        Position = new PixelPoint((int)x, (int)y);
    }
}
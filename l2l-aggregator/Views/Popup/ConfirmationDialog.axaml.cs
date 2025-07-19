using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Material.Icons;
using System.Threading.Tasks;
using System;

namespace l2l_aggregator.Views.Popup;

public partial class ConfirmationDialog : UserControl
{
    public static readonly StyledProperty<string> TitleProperty =
                AvaloniaProperty.Register<ConfirmationDialog, string>(nameof(Title), "�������������");

    public static readonly StyledProperty<string> MessageProperty =
        AvaloniaProperty.Register<ConfirmationDialog, string>(nameof(Message), "�� �������?");

    public static readonly StyledProperty<string> ConfirmTextProperty =
        AvaloniaProperty.Register<ConfirmationDialog, string>(nameof(ConfirmText), "��");

    public static readonly StyledProperty<string> CancelTextProperty =
        AvaloniaProperty.Register<ConfirmationDialog, string>(nameof(CancelText), "������");

    public static readonly StyledProperty<MaterialIconKind> IconKindProperty =
        AvaloniaProperty.Register<ConfirmationDialog, MaterialIconKind>(nameof(IconKind), MaterialIconKind.HelpCircle);

    public static readonly StyledProperty<IBrush> IconColorProperty =
        AvaloniaProperty.Register<ConfirmationDialog, IBrush>(nameof(IconColor), Brushes.Orange);

    public static readonly StyledProperty<IBrush> ConfirmButtonColorProperty =
        AvaloniaProperty.Register<ConfirmationDialog, IBrush>(nameof(ConfirmButtonColor), Brushes.Red);

    public string Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string Message
    {
        get => GetValue(MessageProperty);
        set => SetValue(MessageProperty, value);
    }

    public string ConfirmText
    {
        get => GetValue(ConfirmTextProperty);
        set => SetValue(ConfirmTextProperty, value);
    }

    public string CancelText
    {
        get => GetValue(CancelTextProperty);
        set => SetValue(CancelTextProperty, value);
    }

    public MaterialIconKind IconKind
    {
        get => GetValue(IconKindProperty);
        set => SetValue(IconKindProperty, value);
    }

    public IBrush IconColor
    {
        get => GetValue(IconColorProperty);
        set => SetValue(IconColorProperty, value);
    }

    public IBrush ConfirmButtonColor
    {
        get => GetValue(ConfirmButtonColorProperty);
        set => SetValue(ConfirmButtonColorProperty, value);
    }

    // ������� ��� ��������� ����������
    public event EventHandler<bool>? DialogResult;

    // �������� ��� ���������� ����������
    public bool IsDialogVisible
    {
        get => IsVisible;
        set => IsVisible = value;
    }

    public ConfirmationDialog()
    {
        InitializeComponent();
        IsVisible = false; // �� ��������� �����
    }

    // ������ ��� ����������� � ������� �������
    public void ShowDialog()
    {
        IsVisible = true;
    }

    public void HideDialog()
    {
        IsVisible = false;
    }

    // ����������� ����� ��� �������� ����������� ��������
    public static ConfirmationDialog CreateExitConfirmation()
    {
        return new ConfirmationDialog
        {
            Title = "������������� ������",
            Message = "�� ������������� ������ ����� �� �������?",
            ConfirmText = "�����",
            CancelText = "������",
            IconKind = MaterialIconKind.ExitToApp,
            IconColor = Brushes.Orange,
            ConfirmButtonColor = Brushes.OrangeRed
        };
    }

    public static ConfirmationDialog CreateDeleteConfirmation(string itemName = "�������")
    {
        return new ConfirmationDialog
        {
            Title = "������������� ��������",
            Message = $"�� ������������� ������ ������� {itemName}?",
            ConfirmText = "�������",
            CancelText = "������",
            IconKind = MaterialIconKind.Delete,
            IconColor = Brushes.Red,
            ConfirmButtonColor = Brushes.Red
        };
    }

    public static ConfirmationDialog CreateSaveConfirmation()
    {
        return new ConfirmationDialog
        {
            Title = "���������� ���������",
            Message = "� ��� ���� ������������� ���������. ��������� ��?",
            ConfirmText = "���������",
            CancelText = "�� ���������",
            IconKind = MaterialIconKind.ContentSave,
            IconColor = Brushes.Blue,
            ConfirmButtonColor = Brushes.Green
        };
    }

    // ����������� ������� ������
    private void ConfirmButton_Click(object? sender, RoutedEventArgs e)
    {
        DialogResult?.Invoke(this, true);
        HideDialog();
    }

    private void CancelButton_Click(object? sender, RoutedEventArgs e)
    {
        DialogResult?.Invoke(this, false);
        HideDialog();
    }

    private void CloseButton_Click(object? sender, RoutedEventArgs e)
    {
        DialogResult?.Invoke(this, false);
        HideDialog();
    }

    // ����������� ����� ��� �������� ����������
    public Task<bool> ShowAndWaitAsync()
    {
        var tcs = new TaskCompletionSource<bool>();

        EventHandler<bool>? handler = null;
        handler = (s, result) =>
        {
            DialogResult -= handler;
            tcs.SetResult(result);
        };

        DialogResult += handler;
        ShowDialog();

        return tcs.Task;
    }
}
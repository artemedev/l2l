using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Input.TextInput;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Avalonia.VisualTree;
using CommunityToolkit.Mvvm.Input;
using l2l_aggregator.Controls.Keyboard.Layout;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace l2l_aggregator.Controls.Keyboard;

public enum VirtualKeyboardState
{
    Default,
    Shift,
    Capslock,
    AltCtrl
}

public partial class VirtualKeyboard : UserControl
{
    private static List<Type> Layouts { get; } = new List<Type>();
    private static Func<Type> DefaultLayout { get; set; }

    public static void AddLayout<TLayout>() where TLayout : KeyboardLayout => Layouts.Add(typeof(TLayout));

    public static void SetDefaultLayout(Func<Type> getDefaultLayout) => DefaultLayout = getDefaultLayout;

    public static async Task<string?> ShowDialog(TextInputOptions options, Window? owner = null)
    {
        var keyboard = new VirtualKeyboard();
        var window = new CoporateWindow();
        window.CoporateContent = keyboard;
        window.Title = "Keyboard";
        var mw = ((IClassicDesktopStyleApplicationLifetime)App.Current.ApplicationLifetime).MainWindow;
        await window.ShowDialog(owner ?? mw);
        if (window.Tag is string s)
        {
            return s;
        }
        return null;
    }

    public TextBox TextBox_ { get; }
    public Button AcceptButton_ { get; }
    public Button CloseButton_ { get; }
    public string targetLayout { get; set; }
    public TransitioningContentControl TransitioningContentControl_ { get; }

    public TextBox source { get; set; }


    // Поле состояния
    private VirtualKeyboardState _keyboardState = VirtualKeyboardState.Default;
    public VirtualKeyboardState KeyboardState
    {
        get => _keyboardState;
        private set
        {
            if (_keyboardState != value)
            {
                _keyboardState = value;
                OnKeyboardStateChanged?.Invoke(this, _keyboardState);
            }
        }
    }

    // Событие для оповещения об изменении состояния
    public event EventHandler<VirtualKeyboardState>? OnKeyboardStateChanged;
    // Команда закрытия
    public IRelayCommand CloseCommand { get; }

    public VirtualKeyboard()
    {
        InitializeComponent();
        TextBox_ = this.Get<TextBox>("TextBox");
        TransitioningContentControl_ = this.Get<Avalonia.Controls.TransitioningContentControl>("TransitioningContentControl");
        AcceptButton_ = this.Get<Button>("AcceptButton");
        CloseButton_ = this.Get<Button>("CloseButton");

        AcceptButton_.AddHandler(Button.ClickEvent, acceptClicked);
        CloseButton_.AddHandler(Button.ClickEvent, closeClicked);
        CloseCommand = new RelayCommand(() => Close());

        Initialized += async (sender, args) =>
        {

            if (targetLayout == null)
            {
                TransitioningContentControl_.Content = Activator.CreateInstance(DefaultLayout.Invoke());
            }
            else
            {
                var layout = Layouts.FirstOrDefault(x => x.Name.ToLower().Contains(targetLayout.ToLower()));
                if (layout != null)
                {
                    TransitioningContentControl_.Content = Activator.CreateInstance(layout);
                }
                else
                {
                    TransitioningContentControl_.Content = Activator.CreateInstance(DefaultLayout.Invoke());
                }
            }
            // Обновить визуал после смены лейаута
            Dispatcher.UIThread.Post(() =>
            {
                UpdateKeyVisuals(this.Bounds.Width);
            }, DispatcherPriority.Render);
        };

        KeyDown += (sender, args) =>
        {
            TextBox_.Focus();
            if (args.Key == Key.Escape)
            {
                TextBox_.Text = "";
            }
            else if (args.Key == Key.Enter)
            {

                source.Text = TextBox_.Text;

                Close();
            }
        };
        this.PropertyChanged += (_, e) =>
        {
            if (e.Property == BoundsProperty)
            {
                var rect = this.Bounds;
                UpdateKeyVisuals(rect.Width);
            }
        };
        Loaded += (sender, args) =>
        {
            AdjustTextBoxFontSize();
        };

        TextBox_.PropertyChanged += (_, e) =>
        {
            if (e.Property == TextBox.TextProperty || e.Property == BoundsProperty)
            {
                AdjustTextBoxFontSize();
            }
        };
        _keyboardState = VirtualKeyboardState.Default;
    }
    private void UpdateKeyVisuals(double width)
    {
        double margin = Math.Clamp(width * 0.003, 0.5, 10);
        double fontSize = Math.Clamp(width * 0.02, 12, 48);       // Клавиши
        double iconSize = Math.Clamp(width * 0.025, 16, 48);      // Иконки

        if (TransitioningContentControl_.Content is Control layoutRoot)
        {
            var keys = layoutRoot.GetVisualDescendants().OfType<VirtualKey>();
            foreach (var key in keys)
            {
                key.Margin = new Thickness(margin);
                key.FontSize = fontSize;

                // MaterialIcon
                var icon = key.GetVisualDescendants().OfType<Control>()
                              .FirstOrDefault(c => c.GetType().Name == "MaterialIcon");
                if (icon != null)
                {
                    icon.Width = icon.Height = iconSize;
                }

                // Image (если вдруг иконка через PNG)
                var image = key.GetVisualDescendants().OfType<Image>().FirstOrDefault();
                if (image != null)
                {
                    image.Width = image.Height = iconSize;
                }
            }
        }
    }
   
    private void AdjustTextBoxFontSize()
    {
        if (TextBox_ == null)
            return;

        double availableWidth = TextBox_.Bounds.Width - 20;
        double availableHeight = TextBox_.Bounds.Height - 10;

        // Учитываем пустой текст, используя пробел для расчета
        string text = string.IsNullOrEmpty(TextBox_.Text) ? " " : TextBox_.Text;
        double fontSize = 80;

        while (fontSize >= 8)
        {
            double estimatedWidth = text.Length * fontSize * 0.6;
            double estimatedHeight = fontSize * 1.5;

            if (estimatedWidth <= availableWidth && estimatedHeight <= availableHeight)
                break;

            fontSize -= 1;
        }

        TextBox_.FontSize = fontSize;
    }

    private void closeClicked(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    public void Close()
    {
        TextBox_.Text = "";
        IsVisible = false;
        ((Control)this.Parent).IsVisible = false;
    }

    public void ShowKeyboard(TextBox source)
    {
        Dispatcher.UIThread.Post(() =>
        {
            TextBox_.Focus();
        });
        TextBox_.Focus();
        this.source = source;

        this.TextBox_.PasswordChar = source.PasswordChar;
        this.TextBox_.Text = source.Text == null ? "" : source.Text;
        this.IsVisible = true;
        ((Control)this.Parent).IsVisible = true;
        TransitioningContentControl_.Content = Activator.CreateInstance(DefaultLayout.Invoke());
        // Обновить визуал после смены лейаута
        Dispatcher.UIThread.Post(() =>
        {
            UpdateKeyVisuals(this.Bounds.Width);
        }, DispatcherPriority.Render);
    }
    public void ShowKeyboard(TextBox source, Type layout)
    {
        Dispatcher.UIThread.Post(() =>
        {
            TextBox_.Focus();
        });

        TextBox_.Focus();
        this.source = source;
        this.TextBox_.PasswordChar = source.PasswordChar;
        this.TextBox_.Text = source.Text == null ? "" : source.Text;

        TextBox_.CaretIndex = TextBox_.Text.Length;
        this.IsVisible = true;
        ((Control)this.Parent).IsVisible = true;
        TransitioningContentControl_.Content = Activator.CreateInstance(layout);
        // Обновить визуал после смены лейаута
        Dispatcher.UIThread.Post(() =>
        {
            UpdateKeyVisuals(this.Bounds.Width);
        }, DispatcherPriority.Render);
    }


    private void acceptClicked(object? sender, RoutedEventArgs e)
    {
        source.Text = TextBox_.Text;
        Close();
    }

    public void ProcessText(string text)
    {
        TextBox_.Focus();
        if (TextBox_.CaretIndex <= TextBox_.Text.Length)
        {
            TextBox_.Text = TextBox_.Text.Insert(TextBox_.CaretIndex, text);
            TextBox_.CaretIndex = Math.Clamp(TextBox_.CaretIndex + 1, 0, TextBox_.Text.Length);
        }

        TextBox_.Focus();

        if (_keyboardState == VirtualKeyboardState.Shift)
        {
            KeyboardState = VirtualKeyboardState.Default; // это вызовет событие
        }
    }

    public void ProcessKey(Key key)
    {
        if (key == Key.LeftShift || key == Key.RightShift)
        {
            if (_keyboardState == VirtualKeyboardState.Shift)
            {
                KeyboardState = VirtualKeyboardState.Default;
            }
            else
            {
                KeyboardState = VirtualKeyboardState.Shift;
            }
        }
        else if (key == Key.RightAlt)
        {
            if (_keyboardState == VirtualKeyboardState.AltCtrl)
            {
                KeyboardState = VirtualKeyboardState.Default;
            }
            else
            {
                KeyboardState = VirtualKeyboardState.AltCtrl;
            }
        }
        else if (key == Key.CapsLock)
        {
            if (_keyboardState == VirtualKeyboardState.Capslock)
            {
                KeyboardState = VirtualKeyboardState.Default;
            }
            else
            {
                KeyboardState = VirtualKeyboardState.Capslock;
            }
        }
        else
        {
            if (key == Key.Clear)
            {
                TextBox_.Text = "";
                TextBox_.Focus();
            }
            else if (key == Key.Enter || key == Key.ImeAccept)
            {
                if (TextBox_.Text?.Length > 0)
                {
                    source.Text = TextBox_.Text;
                }
                Close();
            }
            else if (key == Key.Help)
            {
                _keyboardState = VirtualKeyboardState.Default;
                if (TransitioningContentControl_.Content is KeyboardLayout layout)
                {
                    var index = Layouts.IndexOf(layout.GetType());
                    if (Layouts.Count - 1 > index)
                    {
                        TransitioningContentControl_.Content = Activator.CreateInstance(Layouts[index + 1]);
                    }
                    else
                    {
                        TransitioningContentControl_.Content = Activator.CreateInstance(Layouts[0]);
                    }
                    // Обновить визуал после смены лейаута
                    Dispatcher.UIThread.Post(() =>
                    {
                        UpdateKeyVisuals(this.Bounds.Width);
                    }, DispatcherPriority.Render);
                }
            }
            else if (key == Key.Back)
            {

                if (TextBox_.Text != null && TextBox_.CaretIndex <= TextBox_.Text.Length && TextBox_.CaretIndex > 0)
                {
                    int dd = 0;
                    if (TextBox_.CaretIndex != TextBox_.Text.Length)
                    {
                        dd = TextBox_.CaretIndex - 1;
                    }

                    TextBox_.Text = TextBox_.Text.Remove(TextBox_.CaretIndex - 1, 1);
                    if (dd != 0)
                    {
                        TextBox_.CaretIndex = dd;
                    }


                }
                TextBox_.Focus();

            }
            else if (key == Key.BrowserFavorites)
            {
                _keyboardState = VirtualKeyboardState.Default;

                if (TransitioningContentControl_.Content is VirtualKeyboardLayoutNumpad)
                {
                    TransitioningContentControl_.Content = Activator.CreateInstance(DefaultLayout());
                }
                else
                {
                    TransitioningContentControl_.Content = Activator.CreateInstance(typeof(VirtualKeyboardLayoutNumpad));
                }
                // Обновить визуал после смены лейаута
                Dispatcher.UIThread.Post(() =>
                {
                    UpdateKeyVisuals(this.Bounds.Width);
                }, DispatcherPriority.Render);
            }
            else if (key == Key.LaunchApplication1)
            {
                _keyboardState = VirtualKeyboardState.Default;

                if (TransitioningContentControl_.Content is VirtualKeyboardLayoutUS)
                {
                    TransitioningContentControl_.Content = Activator.CreateInstance(DefaultLayout());
                }
                else
                {
                    TransitioningContentControl_.Content = Activator.CreateInstance(typeof(VirtualKeyboardLayoutUS));
                }
                // Обновить визуал после смены лейаута
                Dispatcher.UIThread.Post(() =>
                {
                    UpdateKeyVisuals(this.Bounds.Width);
                }, DispatcherPriority.Render);
            }
            else if (key == Key.LaunchApplication2)
            {
                _keyboardState = VirtualKeyboardState.Default;

                if (TransitioningContentControl_.Content is VirtualKeyboardLayoutRU)
                {
                    TransitioningContentControl_.Content = Activator.CreateInstance(DefaultLayout());
                }
                else
                {
                    TransitioningContentControl_.Content = Activator.CreateInstance(typeof(VirtualKeyboardLayoutRU));
                }
                // Обновить визуал после смены лейаута
                Dispatcher.UIThread.Post(() =>
                {
                    UpdateKeyVisuals(this.Bounds.Width);
                }, DispatcherPriority.Render);
            }
            else if (key == Key.Left)
            {
                TextBox_.CaretIndex = Math.Clamp(TextBox_.CaretIndex - 1, 0, TextBox_.Text.Length);
                TextBox_.Focus();
            }
            else if (key == Key.Right)
            {
                TextBox_.CaretIndex = Math.Clamp(TextBox_.CaretIndex + 1, 0, TextBox_.Text.Length);
                TextBox_.Focus();
            }
            else
            {
                TextBox_.Focus();
            }
        }
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
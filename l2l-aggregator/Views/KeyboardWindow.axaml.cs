using Avalonia.Animation.Easings;
using Avalonia.Animation;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using System.Collections.Generic;
using System;
using System.Linq;
using Avalonia.VisualTree;
namespace l2l_aggregator.Views;

public partial class KeyboardWindow : Window
{
    private bool _isRussian = false;
    private bool _isSymbols = false;
    private TextBox? _targetTextBox;
    private DispatcherTimer _focusTimer;

    private readonly List<string> _englishLayout = new() { "Q", "W", "E", "R", "T", "Y", "U", "I", "O", "P", "A", "S", "D", "F", "G", "H", "J", "K", "L", "Z", "X", "C", "V", "B", "N", "M" };
    private readonly List<string> _russianLayout = new() { "Й", "Ц", "У", "К", "Е", "Н", "Г", "Ш", "Щ", "З", "Ф", "Ы", "В", "А", "П", "Р", "О", "Л", "Д", "Я", "Ч", "С", "М", "И", "Т", "Ь" };
    private readonly List<string> _numbersLayout = new() { "1", "2", "3", "4", "5", "6", "7", "8", "9", "0" };
    private readonly List<string> _symbolsLayout = new() { "!", "@", "#", "$", "%", "^", "&", "*", "(", ")" };

    public KeyboardWindow()
    {
        InitializeComponent();
        BuildKeyboard();

        this.Opened += (_, _) =>
        {
            AnimateShow();
            AdaptToScreen();
        };

        this.PropertyChanged += (sender, e) =>
        {
            if (e.Property == ClientSizeProperty)
            {
                AdaptToScreen();
            }
        };

        _focusTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(300)
        };
        _focusTimer.Tick += FocusCheck;
        _focusTimer.Start();
    }

    private void AnimateShow()
    {
        var animation = new Animation
        {
            Duration = TimeSpan.FromMilliseconds(300),
            Easing = new CubicEaseOut(),
            Children =
                {
                    new KeyFrame
                    {
                        Cue = new Cue(0d),
                        Setters = { new Setter(Window.OpacityProperty, 0d) }
                    },
                    new KeyFrame
                    {
                        Cue = new Cue(1d),
                        Setters = { new Setter(Window.OpacityProperty, 1d) }
                    }
                }
        };
        animation.RunAsync(this);
    }

    private void FocusCheck(object? sender, EventArgs e)
    {
        if (_targetTextBox != null && !_targetTextBox.IsFocused && !IsFocused)
        {
            _focusTimer.Stop();
        }
    }

    private void BuildKeyboard()
    {
        KeysPanel.Children.Clear();

        if (_isSymbols)
        {
            foreach (var key in _numbersLayout.Concat(_symbolsLayout))
            {
                AddButton(key);
            }
        }
        else
        {
            var layout = _isRussian ? _russianLayout : _englishLayout;
            foreach (var key in layout)
            {
                AddButton(key);
            }
        }

        // Нижняя строка специальных кнопок
        var bottomRow = new WrapPanel
        {
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            Margin = new Thickness(5)
        };

        bottomRow.Children.Add(CreateSpecialButton("Space", 300, () => InjectText(" ")));
        bottomRow.Children.Add(CreateSpecialButton(_isSymbols ? "ABC" : "123", 100, ToggleSymbols));
        bottomRow.Children.Add(CreateSpecialButton("Backspace", 100, Backspace));
        bottomRow.Children.Add(CreateSpecialButton("Enter", 100, Enter));

        KeysPanel.Children.Add(bottomRow);
    }

    private void AddButton(string key)
    {
        var button = new Button
        {
            Content = key,
            Width = 60,
            Height = 60,
            Margin = new Thickness(4),
            Background = Brushes.LightGray,
            CornerRadius = new CornerRadius(10)
        };

        button.PointerPressed += (s, e) => button.Background = Brushes.DarkGray;
        button.PointerReleased += (s, e) => button.Background = Brushes.LightGray;
        button.Click += OnKeyClick;
        KeysPanel.Children.Add(button);
    }

    private Button CreateSpecialButton(string text, double width, Action onClick)
    {
        var button = new Button
        {
            Content = text,
            Width = width,
            Height = 60,
            Margin = new Thickness(4),
            Background = Brushes.LightBlue,
            CornerRadius = new CornerRadius(15)
        };

        button.PointerPressed += (s, e) => button.Background = Brushes.SteelBlue;
        button.PointerReleased += (s, e) => button.Background = Brushes.LightBlue;
        button.Click += (_, _) => onClick();

        return button;
    }

    private void OnKeyClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Content is string text)
        {
            InjectText(text);
        }
    }

    private void InjectText(string text)
    {
        if (_targetTextBox != null)
        {
            var caret = _targetTextBox.SelectionStart;
            _targetTextBox.Text = _targetTextBox.Text.Insert(caret, text);
            _targetTextBox.SelectionStart = caret + text.Length;
        }
    }

    private void ToggleSymbols()
    {
        _isSymbols = !_isSymbols;
        BuildKeyboard();
    }

    private void Backspace()
    {
        if (_targetTextBox != null && !string.IsNullOrEmpty(_targetTextBox.Text))
        {
            var caret = _targetTextBox.SelectionStart;
            if (caret > 0)
            {
                _targetTextBox.Text = _targetTextBox.Text.Remove(caret - 1, 1);
                _targetTextBox.SelectionStart = caret - 1;
            }
        }
    }

    private void Enter()
    {
        InjectText(Environment.NewLine);
    }

    private void OnSwitchLanguage()
    {
        _isRussian = !_isRussian;
        _isSymbols = false;
        BuildKeyboard();
    }

    public void SetTarget(TextBox textBox)
    {
        _targetTextBox = textBox;

        textBox.KeyDown -= OnTextBoxKeyDown;
        textBox.KeyDown += OnTextBoxKeyDown;
    }

    private void OnTextBoxKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyModifiers.HasFlag(KeyModifiers.Control) && e.Key == Key.Space)
        {
            OnSwitchLanguage();
            e.Handled = true;
        }
    }

    private void AdaptToScreen()
    {
        if (Screens.Primary != null)
        {
            var screenHeight = Screens.Primary.Bounds.Height;
            var screenWidth = Screens.Primary.Bounds.Width;

            Height = screenHeight * 0.5; // 50% высоты экрана
            Width = screenWidth; // по ширине полностью

            // Немного масштабируем шрифт
            double scale = Math.Min(screenWidth / 1200.0, 1.0);

            foreach (var button in KeysPanel.GetVisualDescendants().OfType<Button>())
            {
                button.FontSize = 20 * scale;
                button.Width = 60 * scale;
                button.Height = 60 * scale;
            }
        }
    }
}
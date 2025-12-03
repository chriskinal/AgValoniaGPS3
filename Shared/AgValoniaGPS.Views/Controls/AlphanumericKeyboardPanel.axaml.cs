using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;

namespace AgValoniaGPS.Views.Controls;

/// <summary>
/// Reusable alphanumeric keyboard panel that can be embedded in dialogs.
/// Uses AvaloniaProperty bindings to communicate with parent controls.
/// </summary>
public partial class AlphanumericKeyboardPanel : UserControl
{
    private string _currentValue = "";
    private bool _isShiftActive = false;
    private readonly List<Button> _letterButtons = new();

    /// <summary>
    /// The current text value being entered.
    /// </summary>
    public static readonly StyledProperty<string> TextProperty =
        AvaloniaProperty.Register<AlphanumericKeyboardPanel, string>(nameof(Text), "");

    public string Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    /// <summary>
    /// Maximum text length allowed.
    /// </summary>
    public static readonly StyledProperty<int> MaxLengthProperty =
        AvaloniaProperty.Register<AlphanumericKeyboardPanel, int>(nameof(MaxLength), 200);

    public int MaxLength
    {
        get => GetValue(MaxLengthProperty);
        set => SetValue(MaxLengthProperty, value);
    }

    public AlphanumericKeyboardPanel()
    {
        InitializeComponent();
        CollectLetterButtons();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == TextProperty)
        {
            // When Text property changes externally, update internal state
            var newValue = change.GetNewValue<string>() ?? "";
            if (_currentValue != newValue)
            {
                _currentValue = newValue;
            }
        }
    }

    private void CollectLetterButtons()
    {
        // Find all letter buttons by name after control is initialized
        Loaded += (s, e) =>
        {
            var letterNames = new[] { "KeyQ", "KeyW", "KeyE", "KeyR", "KeyT", "KeyY", "KeyU", "KeyI", "KeyO", "KeyP",
                                       "KeyA", "KeyS", "KeyD", "KeyF", "KeyG", "KeyH", "KeyJ", "KeyK", "KeyL",
                                       "KeyZ", "KeyX", "KeyC", "KeyV", "KeyB", "KeyN", "KeyM" };

            foreach (var name in letterNames)
            {
                var button = this.FindControl<Button>(name);
                if (button != null)
                {
                    _letterButtons.Add(button);
                }
            }
        };
    }

    private void UpdateText()
    {
        Text = _currentValue;
    }

    private void UpdateLetterCase()
    {
        foreach (var button in _letterButtons)
        {
            if (button.Content is string content && content.Length == 1)
            {
                button.Content = _isShiftActive ? content.ToUpper() : content.ToLower();
            }
        }

        // Update shift button appearance
        if (_isShiftActive)
        {
            ShiftButton.Classes.Add("ShiftActive");
            ShiftButton.Classes.Remove("ShiftButton");
        }
        else
        {
            ShiftButton.Classes.Remove("ShiftActive");
            ShiftButton.Classes.Add("ShiftButton");
        }
    }

    private void OnKeyClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Content is string key)
        {
            // Check max length
            if (_currentValue.Length >= MaxLength)
            {
                return;
            }

            _currentValue += key;
            UpdateText();

            // Auto-disable shift after typing a letter (like real keyboard)
            if (_isShiftActive && key.Length == 1 && char.IsLetter(key[0]))
            {
                _isShiftActive = false;
                UpdateLetterCase();
            }
        }
    }

    private void OnShiftClick(object? sender, RoutedEventArgs e)
    {
        _isShiftActive = !_isShiftActive;
        UpdateLetterCase();
    }

    private void OnSpaceClick(object? sender, RoutedEventArgs e)
    {
        // Check max length
        if (_currentValue.Length >= MaxLength)
        {
            return;
        }

        _currentValue += " ";
        UpdateText();
    }

    private void OnBackspaceClick(object? sender, RoutedEventArgs e)
    {
        if (_currentValue.Length > 0)
        {
            _currentValue = _currentValue[..^1];
            UpdateText();
        }
    }

    private void OnClearClick(object? sender, RoutedEventArgs e)
    {
        _currentValue = "";
        UpdateText();
    }

    private async void OnPasteClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel?.Clipboard is IClipboard clipboard)
            {
                var text = await clipboard.TryGetTextAsync();
                if (!string.IsNullOrEmpty(text))
                {
                    // Append clipboard text, respecting max length
                    var remaining = MaxLength - _currentValue.Length;
                    if (remaining > 0)
                    {
                        var toAdd = text.Length <= remaining ? text : text.Substring(0, remaining);
                        _currentValue += toAdd;
                        UpdateText();
                    }
                }
            }
        }
        catch
        {
            // Clipboard access may fail in some environments
        }
    }
}

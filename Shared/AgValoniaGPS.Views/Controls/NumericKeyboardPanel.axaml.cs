using System;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace AgValoniaGPS.Views.Controls;

/// <summary>
/// Reusable numeric keyboard panel that can be embedded in dialogs.
/// Uses AvaloniaProperty bindings to communicate with parent controls.
/// </summary>
public partial class NumericKeyboardPanel : UserControl
{
    private string _currentValue = "";
    private bool _hasDecimal = false;

    /// <summary>
    /// The current numeric value being entered.
    /// </summary>
    public static readonly StyledProperty<decimal?> ValueProperty =
        AvaloniaProperty.Register<NumericKeyboardPanel, decimal?>(nameof(Value));

    public decimal? Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    /// <summary>
    /// The current display text (includes partial input like "-" or "0.").
    /// </summary>
    public static readonly StyledProperty<string> DisplayTextProperty =
        AvaloniaProperty.Register<NumericKeyboardPanel, string>(nameof(DisplayText), "");

    public string DisplayText
    {
        get => GetValue(DisplayTextProperty);
        private set => SetValue(DisplayTextProperty, value);
    }

    /// <summary>
    /// Maximum number of decimal places allowed.
    /// </summary>
    public static readonly StyledProperty<int> MaxDecimalPlacesProperty =
        AvaloniaProperty.Register<NumericKeyboardPanel, int>(nameof(MaxDecimalPlaces), 7);

    public int MaxDecimalPlaces
    {
        get => GetValue(MaxDecimalPlacesProperty);
        set => SetValue(MaxDecimalPlacesProperty, value);
    }

    /// <summary>
    /// Whether negative values are allowed.
    /// </summary>
    public static readonly StyledProperty<bool> AllowNegativeProperty =
        AvaloniaProperty.Register<NumericKeyboardPanel, bool>(nameof(AllowNegative), true);

    public bool AllowNegative
    {
        get => GetValue(AllowNegativeProperty);
        set => SetValue(AllowNegativeProperty, value);
    }

    /// <summary>
    /// Whether only integer values are allowed (hides decimal button).
    /// </summary>
    public static readonly StyledProperty<bool> IntegerOnlyProperty =
        AvaloniaProperty.Register<NumericKeyboardPanel, bool>(nameof(IntegerOnly), false);

    public bool IntegerOnly
    {
        get => GetValue(IntegerOnlyProperty);
        set => SetValue(IntegerOnlyProperty, value);
    }

    public NumericKeyboardPanel()
    {
        InitializeComponent();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == ValueProperty)
        {
            // When Value property changes externally, update internal state
            var newValue = change.GetNewValue<decimal?>();
            if (newValue.HasValue)
            {
                _currentValue = newValue.Value.ToString(CultureInfo.InvariantCulture);
                _hasDecimal = _currentValue.Contains('.');
            }
            else
            {
                _currentValue = "";
                _hasDecimal = false;
            }
            DisplayText = _currentValue;
        }
        else if (change.Property == IntegerOnlyProperty)
        {
            // Hide decimal button if integer only
            if (DecimalButton != null)
            {
                DecimalButton.IsVisible = !IntegerOnly;
            }
        }
    }

    private void UpdateValue()
    {
        // Always update display text to show current input (including partial like "-")
        DisplayText = _currentValue;

        if (string.IsNullOrEmpty(_currentValue) || _currentValue == "-" || _currentValue == ".")
        {
            Value = null;
        }
        else if (decimal.TryParse(_currentValue, NumberStyles.Any, CultureInfo.InvariantCulture, out var value))
        {
            Value = value;
        }
    }

    private void OnDigitClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Content is string digit)
        {
            // Prevent leading zeros (except for "0.")
            if (_currentValue == "0" && digit != ".")
            {
                _currentValue = digit;
            }
            else if (_currentValue == "-0" && digit != ".")
            {
                _currentValue = "-" + digit;
            }
            else
            {
                // Check decimal places limit
                if (_hasDecimal)
                {
                    var parts = _currentValue.Split('.');
                    if (parts.Length > 1 && parts[1].Length >= MaxDecimalPlaces)
                    {
                        return; // Don't add more decimal places
                    }
                }

                _currentValue += digit;
            }

            UpdateValue();
        }
    }

    private void OnDecimalClick(object? sender, RoutedEventArgs e)
    {
        if (IntegerOnly || _hasDecimal) return;

        if (string.IsNullOrEmpty(_currentValue) || _currentValue == "-")
        {
            _currentValue += "0.";
        }
        else
        {
            _currentValue += ".";
        }

        _hasDecimal = true;
        UpdateValue();
    }

    private void OnBackspaceClick(object? sender, RoutedEventArgs e)
    {
        if (_currentValue.Length > 0)
        {
            var removed = _currentValue[^1];
            _currentValue = _currentValue[..^1];

            if (removed == '.')
            {
                _hasDecimal = false;
            }

            UpdateValue();
        }
    }

    private void OnClearClick(object? sender, RoutedEventArgs e)
    {
        _currentValue = "";
        _hasDecimal = false;
        UpdateValue();
    }

    private void OnMinusClick(object? sender, RoutedEventArgs e)
    {
        if (!AllowNegative) return;

        // If empty or just starting, add minus at beginning
        if (string.IsNullOrEmpty(_currentValue))
        {
            _currentValue = "-";
        }
        // If already has minus, remove it (toggle behavior)
        else if (_currentValue.StartsWith('-'))
        {
            _currentValue = _currentValue[1..];
        }
        // If doesn't have minus, add it at beginning
        else
        {
            _currentValue = "-" + _currentValue;
        }

        UpdateValue();
    }
}

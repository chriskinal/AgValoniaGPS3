using System;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace AgValoniaGPS.iOS.Controls;

/// <summary>
/// Inline numeric keypad for iOS - embeddable UserControl version
/// </summary>
public partial class NumericKeypad : UserControl
{
    private string _currentValue = "";
    private bool _hasDecimal = false;
    private int _maxDecimalPlaces = 7;
    private bool _allowNegative = true;

    /// <summary>
    /// Event fired when OK is clicked with the entered value
    /// </summary>
    public event EventHandler<double?>? ValueEntered;

    /// <summary>
    /// Event fired when Cancel is clicked
    /// </summary>
    public event EventHandler? Cancelled;

    public NumericKeypad()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Initialize the keypad with a value and description
    /// </summary>
    public void Initialize(string description, double? initialValue, int maxDecimalPlaces = 7, bool allowNegative = true)
    {
        _maxDecimalPlaces = maxDecimalPlaces;
        _allowNegative = allowNegative;

        DescriptionLabel.Text = description;

        if (initialValue.HasValue)
        {
            _currentValue = initialValue.Value.ToString("F7", CultureInfo.InvariantCulture).TrimEnd('0').TrimEnd('.');
            _hasDecimal = _currentValue.Contains('.');
        }
        else
        {
            _currentValue = "";
            _hasDecimal = false;
        }

        UpdateDisplay();
    }

    private void UpdateDisplay()
    {
        if (string.IsNullOrEmpty(_currentValue) || _currentValue == "-")
        {
            DisplayText.Text = _currentValue.Length > 0 ? _currentValue : "0";
        }
        else
        {
            DisplayText.Text = _currentValue;
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
                    if (parts.Length > 1 && parts[1].Length >= _maxDecimalPlaces)
                    {
                        return; // Don't add more decimal places
                    }
                }

                _currentValue += digit;
            }

            UpdateDisplay();
        }
    }

    private void OnDecimalClick(object? sender, RoutedEventArgs e)
    {
        if (_hasDecimal) return;

        if (string.IsNullOrEmpty(_currentValue) || _currentValue == "-")
        {
            _currentValue += "0.";
        }
        else
        {
            _currentValue += ".";
        }

        _hasDecimal = true;
        UpdateDisplay();
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

            UpdateDisplay();
        }
    }

    private void OnClearClick(object? sender, RoutedEventArgs e)
    {
        _currentValue = "";
        _hasDecimal = false;
        UpdateDisplay();
    }

    private void OnNegateClick(object? sender, RoutedEventArgs e)
    {
        if (!_allowNegative) return;

        if (_currentValue.StartsWith('-'))
        {
            _currentValue = _currentValue[1..];
        }
        else if (!string.IsNullOrEmpty(_currentValue))
        {
            _currentValue = "-" + _currentValue;
        }
        else
        {
            _currentValue = "-";
        }

        UpdateDisplay();
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        Cancelled?.Invoke(this, EventArgs.Empty);
    }

    private void OnOkClick(object? sender, RoutedEventArgs e)
    {
        double? result = null;

        if (string.IsNullOrEmpty(_currentValue) || _currentValue == "-" || _currentValue == ".")
        {
            result = 0;
        }
        else if (double.TryParse(_currentValue, NumberStyles.Any, CultureInfo.InvariantCulture, out var value))
        {
            result = value;
        }

        ValueEntered?.Invoke(this, result);
    }
}

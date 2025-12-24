using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace AgValoniaGPS.Views.Converters;

/// <summary>
/// Converts a value to bool based on whether it equals the parameter.
/// Use for radio-button-style selection in XAML.
/// Example: Classes.Selected="{Binding Value, Converter={StaticResource EqualityConverter}, ConverterParameter=1}"
/// Returns true if Value == 1
/// </summary>
public class EqualityConverter : IValueConverter
{
    public static readonly EqualityConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value == null || parameter == null)
            return false;

        // Handle numeric comparisons
        if (value is int intVal)
        {
            if (parameter is int paramInt)
                return intVal == paramInt;
            if (int.TryParse(parameter.ToString(), out var parsedParam))
                return intVal == parsedParam;
        }

        if (value is double dblVal)
        {
            if (parameter is double paramDbl)
                return Math.Abs(dblVal - paramDbl) < 0.0001;
            if (double.TryParse(parameter.ToString(), out var parsedParam))
                return Math.Abs(dblVal - parsedParam) < 0.0001;
        }

        // Fall back to string comparison
        return value.ToString() == parameter.ToString();
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

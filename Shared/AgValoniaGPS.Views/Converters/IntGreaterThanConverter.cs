using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace AgValoniaGPS.Views.Converters;

/// <summary>
/// Converts an integer to a boolean indicating whether it is greater than the parameter.
/// Used for visibility bindings based on NumSections.
/// </summary>
public class IntGreaterThanConverter : IValueConverter
{
    public static readonly IntGreaterThanConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is int intValue && parameter != null)
        {
            if (int.TryParse(parameter.ToString(), out int threshold))
            {
                return intValue > threshold;
            }
        }
        return false;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

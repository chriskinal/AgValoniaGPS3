using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace AgValoniaGPS.Views.Converters;

/// <summary>
/// Converts GPS fix quality integer to human-readable name.
/// Fix Quality values:
/// 0 = Invalid
/// 1 = GPS Fix
/// 2 = DGPS
/// 4 = RTK Fixed
/// 5 = RTK Float
/// </summary>
public class FixQualityNameConverter : IValueConverter
{
    public static readonly FixQualityNameConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not int fixQuality)
            return "Unknown";

        return fixQuality switch
        {
            0 => "Invalid",
            1 => "GPS Fix (1)",
            2 => "DGPS (2)",
            3 => "PPS (3)",
            4 => "RTK Fixed (4)",
            5 => "RTK Float (5)",
            6 => "Estimated (6)",
            _ => $"Unknown ({fixQuality})"
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

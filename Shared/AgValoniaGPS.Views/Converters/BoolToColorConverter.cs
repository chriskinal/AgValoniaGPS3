using System;
using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace AgValoniaGPS.Views.Converters;

public class BoolToColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            return boolValue ? Brushes.LimeGreen : Brushes.Gray;
        }
        return Brushes.Gray;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts bool to toggle button background brush.
/// True = green (#DD1E8449), False = gray (#DD34495E)
/// </summary>
public class BoolToToggleBackgroundConverter : IValueConverter
{
    public static readonly BoolToToggleBackgroundConverter Instance = new();

    private static readonly IBrush ActiveBrush = new SolidColorBrush(Color.Parse("#DD1E8449"));
    private static readonly IBrush InactiveBrush = new SolidColorBrush(Color.Parse("#DD34495E"));

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isActive)
        {
            return isActive ? ActiveBrush : InactiveBrush;
        }
        return InactiveBrush;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return BindingOperations.DoNothing;
    }
}

/// <summary>
/// Converts bool to toggle button border brush.
/// True = green border (#4A9A7E), False = gray border (#555)
/// </summary>
public class BoolToToggleBorderConverter : IValueConverter
{
    public static readonly BoolToToggleBorderConverter Instance = new();

    private static readonly IBrush ActiveBrush = new SolidColorBrush(Color.Parse("#4A9A7E"));
    private static readonly IBrush InactiveBrush = new SolidColorBrush(Color.Parse("#555555"));

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isActive)
        {
            return isActive ? ActiveBrush : InactiveBrush;
        }
        return InactiveBrush;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return BindingOperations.DoNothing;
    }
}

/// <summary>
/// Converts int to toggle button background brush by comparing to parameter.
/// Matching = green (#DD1E8449), Not matching = gray (#DD34495E)
/// </summary>
public class IntToToggleBackgroundConverter : IValueConverter
{
    public static readonly IntToToggleBackgroundConverter Instance = new();

    private static readonly IBrush ActiveBrush = new SolidColorBrush(Color.Parse("#DD1E8449"));
    private static readonly IBrush InactiveBrush = new SolidColorBrush(Color.Parse("#DD34495E"));

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is int intValue && parameter != null)
        {
            if (int.TryParse(parameter.ToString(), out int paramValue))
            {
                return intValue == paramValue ? ActiveBrush : InactiveBrush;
            }
        }
        return InactiveBrush;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return BindingOperations.DoNothing;
    }
}

/// <summary>
/// Converts int to toggle button border brush by comparing to parameter.
/// Matching = green border (#4A9A7E), Not matching = gray border (#555)
/// </summary>
public class IntToToggleBorderConverter : IValueConverter
{
    public static readonly IntToToggleBorderConverter Instance = new();

    private static readonly IBrush ActiveBrush = new SolidColorBrush(Color.Parse("#4A9A7E"));
    private static readonly IBrush InactiveBrush = new SolidColorBrush(Color.Parse("#555555"));

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is int intValue && parameter != null)
        {
            if (int.TryParse(parameter.ToString(), out int paramValue))
            {
                return intValue == paramValue ? ActiveBrush : InactiveBrush;
            }
        }
        return InactiveBrush;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return BindingOperations.DoNothing;
    }
}

public class BoolToStatusConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            return boolValue ? "Connected" : "Disconnected";
        }
        return "Unknown";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class FixQualityToColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string fixQuality)
        {
            return fixQuality switch
            {
                "RTK Fixed" => Brushes.LimeGreen,      // Green for RTK Fixed
                "RTK Float" => Brushes.Yellow,          // Yellow for RTK Float
                _ => Brushes.Red                        // Red for everything else (No Fix, GPS Fix, DGPS)
            };
        }
        return Brushes.Red;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class BoolToSteerColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isActive)
        {
            return isActive ? new SolidColorBrush(Color.Parse("#27AE60")) : new SolidColorBrush(Color.Parse("#7F8C8D"));
        }
        return new SolidColorBrush(Color.Parse("#7F8C8D"));
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class BoolToSteerTextConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isActive)
        {
            return isActive ? "STEER" : "OFF";
        }
        return "OFF";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class BoolToSectionColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isActive)
        {
            return isActive ? new SolidColorBrush(Color.Parse("#E74C3C")) : new SolidColorBrush(Color.Parse("#2C3E50"));
        }
        return new SolidColorBrush(Color.Parse("#2C3E50"));
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class InverseBoolConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            return !boolValue;
        }
        return true;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            return !boolValue;
        }
        return false;
    }
}

/// <summary>
/// Compares an int value with a parameter for equality (useful for tab selection).
/// Returns true (or visibility) when values match.
/// </summary>
public class IntEqualsConverter : IValueConverter
{
    public static readonly IntEqualsConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is int intValue && parameter != null)
        {
            if (int.TryParse(parameter.ToString(), out int paramValue))
            {
                return intValue == paramValue;
            }
        }
        return false;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool boolValue && boolValue && parameter != null)
        {
            if (int.TryParse(parameter.ToString(), out int paramValue))
            {
                return paramValue;
            }
        }
        return 0;
    }
}

/// <summary>
/// Compares an enum value with a parameter for equality.
/// Returns true when values match.
/// </summary>
public class EnumEqualsConverter : IValueConverter
{
    public static readonly EnumEqualsConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value == null || parameter == null)
            return false;

        var enumType = value.GetType();
        if (!enumType.IsEnum)
            return false;

        var paramString = parameter.ToString();
        if (string.IsNullOrEmpty(paramString))
            return false;

        if (Enum.TryParse(enumType, paramString, true, out var paramValue))
        {
            return value.Equals(paramValue);
        }

        return false;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool boolValue && boolValue && parameter != null && targetType.IsEnum)
        {
            var paramString = parameter.ToString();
            if (!string.IsNullOrEmpty(paramString) && Enum.TryParse(targetType, paramString, true, out var enumValue))
            {
                return enumValue;
            }
        }
        return BindingOperations.DoNothing;
    }
}

/// <summary>
/// Compares an enum value with a parameter for equality.
/// Returns a highlight brush when matched, transparent when not.
/// </summary>
public class EnumToSelectionBrushConverter : IValueConverter
{
    public static readonly EnumToSelectionBrushConverter Instance = new();

    private static readonly IBrush SelectedBrush = new SolidColorBrush(Color.Parse("#4A9A7E"));
    private static readonly IBrush UnselectedBrush = Brushes.Transparent;

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value == null || parameter == null)
            return UnselectedBrush;

        var enumType = value.GetType();
        if (!enumType.IsEnum)
            return UnselectedBrush;

        var paramString = parameter.ToString();
        if (string.IsNullOrEmpty(paramString))
            return UnselectedBrush;

        if (Enum.TryParse(enumType, paramString, true, out var paramValue))
        {
            return value.Equals(paramValue) ? SelectedBrush : UnselectedBrush;
        }

        return UnselectedBrush;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return BindingOperations.DoNothing;
    }
}

/// <summary>
/// Converts int to bool (for tab index selection bindings)
/// </summary>
public class IntToBoolConverter : IValueConverter
{
    public static readonly IntToBoolConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is int intValue && parameter != null)
        {
            if (int.TryParse(parameter.ToString(), out int paramValue))
            {
                return intValue == paramValue;
            }
        }
        return false;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts a string path (avares://) to a Bitmap image.
/// Used for dynamic image source binding.
/// </summary>
public class StringToImageConverter : IValueConverter
{
    public static readonly StringToImageConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string path && !string.IsNullOrEmpty(path))
        {
            try
            {
                var uri = new Uri(path);
                using var stream = AssetLoader.Open(uri);
                return new Bitmap(stream);
            }
            catch
            {
                return null;
            }
        }
        return null;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return BindingOperations.DoNothing;
    }
}

/// <summary>
/// Converts bool to opacity value (double).
/// Parameter format: "trueOpacity|falseOpacity" e.g. "1|0.3"
/// </summary>
public class BoolToOpacityConverter : IValueConverter
{
    public static readonly BoolToOpacityConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool boolValue = value is bool b && b;

        if (parameter is string param && param.Contains('|'))
        {
            var parts = param.Split('|');
            if (parts.Length == 2 &&
                double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out double trueOpacity) &&
                double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double falseOpacity))
            {
                return boolValue ? trueOpacity : falseOpacity;
            }
        }

        // Default: 1.0 for true, 0.3 for false
        return boolValue ? 1.0 : 0.3;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return BindingOperations.DoNothing;
    }
}

/// <summary>
/// Converts section color code to background brush for section buttons.
/// Color codes: 0=Red (Off), 1=Yellow (Manual On), 2=Green (Auto On), 3=Gray (Auto Off)
/// </summary>
public class SectionColorCodeToBackgroundConverter : IValueConverter
{
    public static readonly SectionColorCodeToBackgroundConverter Instance = new();

    // Match the exact colors used in DrawingContextMapControl
    private static readonly IBrush RedBrush = new SolidColorBrush(Color.FromRgb(242, 51, 51));     // #F23333 - Off
    private static readonly IBrush YellowBrush = new SolidColorBrush(Color.FromRgb(247, 247, 0)); // #F7F700 - Manual On
    private static readonly IBrush GreenBrush = new SolidColorBrush(Color.FromRgb(0, 242, 0));    // #00F200 - Auto On
    private static readonly IBrush GrayBrush = new SolidColorBrush(Color.FromRgb(100, 100, 100)); // #646464 - Auto Off

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is int colorCode)
        {
            return colorCode switch
            {
                0 => RedBrush,    // Off
                1 => YellowBrush, // Manual On
                2 => GreenBrush,  // Auto On
                3 => GrayBrush,   // Auto Off
                _ => GrayBrush
            };
        }
        return GrayBrush;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return BindingOperations.DoNothing;
    }
}

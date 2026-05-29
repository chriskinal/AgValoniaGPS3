// AgValoniaGPS
// Copyright (C) 2024-2026 AgValoniaGPS Contributors
//
// Licensed under GNU GPL v3. See LICENSE.md.

using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;

namespace AgValoniaGPS.Views.Converters;

/// <summary>
/// true  → the Thickness parsed from ConverterParameter (e.g. "0,0,100,110");
/// false → Thickness.Zero. Used to inset the on-map camera pad inward while the
/// Field Tools ring is open, so the pad slides into the map hole instead of
/// hiding behind the ring's edge bands.
/// </summary>
public class BoolToThicknessConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool b && b && parameter is string s)
        {
            try { return Thickness.Parse(s); }
            catch { return new Thickness(0); }
        }
        return new Thickness(0);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

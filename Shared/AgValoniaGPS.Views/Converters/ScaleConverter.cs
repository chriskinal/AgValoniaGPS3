// AgValoniaGPS
// Copyright (C) 2024-2025 AgValoniaGPS Contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program. If not, see <https://www.gnu.org/licenses/>.

using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace AgValoniaGPS.Views.Converters;

/// <summary>
/// Multiplies a numeric value by the parameter. Use for percentage-of-parent
/// sizing without arithmetic in XAML bindings.
/// Example:
///   Width="{Binding $parent[Window].Bounds.Width,
///                   Converter={x:Static c:ScaleConverter.Instance},
///                   ConverterParameter=0.75}"
/// The parameter is parsed using the invariant culture so "0.75" works on
/// machines with a comma decimal separator.
/// </summary>
public class ScaleConverter : IValueConverter
{
    public static readonly ScaleConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value == null || parameter == null) return null;

        double v;
        if (value is double d) v = d;
        else if (value is int i) v = i;
        else if (value is float f) v = f;
        else if (value is long l) v = l;
        else if (!double.TryParse(value.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out v)) return null;

        double factor;
        if (parameter is double dp) factor = dp;
        else if (parameter is int ip) factor = ip;
        else if (!double.TryParse(parameter.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out factor)) return null;

        return v * factor;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

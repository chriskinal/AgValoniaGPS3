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

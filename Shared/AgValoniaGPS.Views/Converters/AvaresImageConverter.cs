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
using System.Collections.Concurrent;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace AgValoniaGPS.Views.Converters;

/// <summary>
/// Converts an avares:// URI string to a Bitmap for Image.Source binding.
/// Caches loaded bitmaps to avoid repeated disk I/O.
/// </summary>
public class AvaresImageConverter : IValueConverter
{
    public static readonly AvaresImageConverter Instance = new();

    private static readonly ConcurrentDictionary<string, Bitmap> Cache = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string path || string.IsNullOrEmpty(path))
            return null;

        return Cache.GetOrAdd(path, static p =>
        {
            var uri = new Uri(p);
            using var stream = AssetLoader.Open(uri);
            return new Bitmap(stream);
        });
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

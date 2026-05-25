// AgValoniaGPS
// Copyright (C) 2024-2026 AgValoniaGPS Contributors
//
// Licensed under GNU GPL v3. See LICENSE.md.

using System;
using System.Globalization;
using System.IO;
using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;

namespace AgValoniaGPS.Views.Converters;

/// <summary>
/// Decodes a PNG/JPEG byte[] into an Avalonia <see cref="Bitmap"/> for binding
/// to Image.Source. Keeps bitmap decoding in the View layer so ViewModels can
/// hold raw bytes (e.g. the map thumbnail PNG) without an Avalonia dependency.
///
/// Caches the last (bytes -> bitmap) pair so repeated conversions of the same
/// array don't re-decode.
///
/// The previous bitmap is intentionally NOT disposed: an Image control can
/// still reference it during a layout/measure pass when the binding updates,
/// and disposing it out from under the control throws ObjectDisposedException
/// from Image.MeasureOverride. Thumbnails are small and infrequent, so letting
/// the GC reclaim the old bitmap is the safe trade.
/// </summary>
public class BytesToBitmapConverter : IValueConverter
{
    public static readonly BytesToBitmapConverter Instance = new();

    private byte[]? _lastBytes;
    private Bitmap? _lastBitmap;

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not byte[] bytes || bytes.Length == 0)
            return null;

        if (ReferenceEquals(bytes, _lastBytes) && _lastBitmap != null)
            return _lastBitmap;

        try
        {
            using var ms = new MemoryStream(bytes);
            var bitmap = new Bitmap(ms);
            _lastBytes = bytes;
            _lastBitmap = bitmap;
            return bitmap;
        }
        catch
        {
            return null;
        }
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

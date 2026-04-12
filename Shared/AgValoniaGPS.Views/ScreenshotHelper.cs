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
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;

namespace AgValoniaGPS.Views;

public static class ScreenshotHelper
{
    /// <summary>
    /// Captures a PNG screenshot of the given control (or its TopLevel).
    /// Returns the PNG bytes, or null on failure.
    /// </summary>
    public static byte[]? CaptureScreenshotPng(Control control)
    {
        try
        {
            var target = TopLevel.GetTopLevel(control) as Visual ?? control;
            var bounds = (target as Control)?.Bounds ?? control.Bounds;
            var w = Math.Max((int)bounds.Width, 1);
            var h = Math.Max((int)bounds.Height, 1);
            var bmp = new RenderTargetBitmap(
                new PixelSize(w, h), new Vector(96, 96));
            bmp.Render(target);

            using var ms = new System.IO.MemoryStream();
            bmp.Save(ms);
            return ms.ToArray();
        }
        catch
        {
            return null;
        }
    }
}

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
using System.IO;

namespace AgValoniaGPS.Services;

/// <summary>
/// Cross-platform "is the dev overlay turned on?" check. Reads once at startup;
/// users toggle by creating/removing the marker file and relaunching.
///
/// <para>
/// Hotkeys can't be used on iPad / Android, and we don't want a Settings entry
/// that a non-dev user could trip over — a file in the same Documents folder
/// AgValoniaGPS already uses keeps the toggle out of the UI entirely.
/// </para>
///
/// <para>Marker file: <c>&lt;Documents&gt;/AgValoniaGPS/.show_dev_overlay</c></para>
/// </summary>
public static class DevOverlayMarker
{
    public const string MarkerFileName = ".show_dev_overlay";

    /// <summary>
    /// Returns true when the marker file exists in the AgValoniaGPS Documents
    /// folder. Safe to call before any service is wired; falls back to
    /// <c>false</c> on any I/O exception.
    /// </summary>
    public static bool IsEnabled()
    {
        try
        {
            var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            if (string.IsNullOrEmpty(documents))
            {
                documents = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
            }
            if (string.IsNullOrEmpty(documents))
            {
                return false;
            }
            var path = Path.Combine(documents, "AgValoniaGPS", MarkerFileName);
            return File.Exists(path);
        }
        catch
        {
            return false;
        }
    }
}

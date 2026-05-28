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
using AgValoniaGPS.Models;

namespace AgValoniaGPS.Services;

/// <summary>
/// Dev-mode override that fakes a battery reading so the strip icon renders
/// on machines without a battery (Mac mini, desktop tower, etc.). Drop a
/// <c>.force_battery</c> file into the AgValoniaGPS Documents folder and
/// every platform's <see cref="Interfaces.IBatteryService"/> short-circuits
/// to the fake reading instead of reporting Unavailable.
/// </summary>
public static class BatteryForceMarker
{
    public const string MarkerFileName = ".force_battery";

    /// <summary>Fake reading published when the marker file is present.</summary>
    public static readonly BatteryStatus FakeStatus = new()
    {
        IsAvailable = true,
        Level = 0.65,
        IsCharging = true,
    };

    public static bool IsEnabled()
    {
        try
        {
            var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            if (string.IsNullOrEmpty(documents))
            {
                documents = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
            }
            if (string.IsNullOrEmpty(documents)) return false;
            return File.Exists(Path.Combine(documents, "AgValoniaGPS", MarkerFileName));
        }
        catch
        {
            return false;
        }
    }
}

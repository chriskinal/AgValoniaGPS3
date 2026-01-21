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
using System.IO;
using AgValoniaGPS.Models;

namespace AgValoniaGPS.Services;

/// <summary>
/// Service for reading and writing BackPic.Txt (background image metadata)
/// </summary>
public class BackgroundImageFileService
{
    /// <summary>
    /// Load background image metadata from BackPic.Txt
    /// </summary>
    public BackgroundImage? LoadBackgroundImage(string fieldDirectory)
    {
        var backPicFilePath = Path.Combine(fieldDirectory, "BackPic.Txt");
        var backPicImagePath = Path.Combine(fieldDirectory, "BackPic.png");

        if (!File.Exists(backPicFilePath))
        {
            return null;
        }

        var backgroundImage = new BackgroundImage
        {
            ImagePath = backPicImagePath
        };

        using (var reader = new StreamReader(backPicFilePath))
        {
            // Line 1: $BackPic header
            reader.ReadLine();

            // Line 2: IsEnabled (True/False)
            var enabledLine = reader.ReadLine();
            if (!string.IsNullOrWhiteSpace(enabledLine))
            {
                backgroundImage.IsEnabled = bool.Parse(enabledLine.Trim());
            }

            // Line 3: Max Easting
            var maxEastingLine = reader.ReadLine();
            if (!string.IsNullOrWhiteSpace(maxEastingLine))
            {
                if (double.TryParse(maxEastingLine, NumberStyles.Float, CultureInfo.InvariantCulture, out double maxEasting))
                {
                    backgroundImage.MaxEasting = maxEasting;
                }
            }

            // Line 4: Min Easting
            var minEastingLine = reader.ReadLine();
            if (!string.IsNullOrWhiteSpace(minEastingLine))
            {
                if (double.TryParse(minEastingLine, NumberStyles.Float, CultureInfo.InvariantCulture, out double minEasting))
                {
                    backgroundImage.MinEasting = minEasting;
                }
            }

            // Line 5: Max Northing
            var maxNorthingLine = reader.ReadLine();
            if (!string.IsNullOrWhiteSpace(maxNorthingLine))
            {
                if (double.TryParse(maxNorthingLine, NumberStyles.Float, CultureInfo.InvariantCulture, out double maxNorthing))
                {
                    backgroundImage.MaxNorthing = maxNorthing;
                }
            }

            // Line 6: Min Northing
            var minNorthingLine = reader.ReadLine();
            if (!string.IsNullOrWhiteSpace(minNorthingLine))
            {
                if (double.TryParse(minNorthingLine, NumberStyles.Float, CultureInfo.InvariantCulture, out double minNorthing))
                {
                    backgroundImage.MinNorthing = minNorthing;
                }
            }
        }

        return backgroundImage;
    }

    /// <summary>
    /// Save background image metadata to BackPic.Txt
    /// </summary>
    public void SaveBackgroundImage(BackgroundImage backgroundImage, string fieldDirectory)
    {
        if (string.IsNullOrWhiteSpace(fieldDirectory))
        {
            throw new ArgumentNullException(nameof(fieldDirectory));
        }

        if (!Directory.Exists(fieldDirectory))
        {
            Directory.CreateDirectory(fieldDirectory);
        }

        var backPicFilePath = Path.Combine(fieldDirectory, "BackPic.Txt");

        using (var writer = new StreamWriter(backPicFilePath, false))
        {
            writer.WriteLine("$BackPic");
            writer.WriteLine(backgroundImage.IsEnabled.ToString());
            writer.WriteLine(backgroundImage.MaxEasting.ToString(CultureInfo.InvariantCulture));
            writer.WriteLine(backgroundImage.MinEasting.ToString(CultureInfo.InvariantCulture));
            writer.WriteLine(backgroundImage.MaxNorthing.ToString(CultureInfo.InvariantCulture));
            writer.WriteLine(backgroundImage.MinNorthing.ToString(CultureInfo.InvariantCulture));
        }
    }
}

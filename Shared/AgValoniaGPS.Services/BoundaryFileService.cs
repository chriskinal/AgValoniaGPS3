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
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using AgValoniaGPS.Models;

namespace AgValoniaGPS.Services;

/// <summary>
/// Service for reading and writing Boundary.txt files
/// Ported from AgOpenGPS GPS/IO/BoundaryFiles.cs
/// Supports legacy format quirks (duplicate isDriveThru lines, extra whitespace)
/// </summary>
public class BoundaryFileService
{
    /// <summary>
    /// Load boundary from Boundary.txt and Headland.Txt
    /// </summary>
    public Boundary LoadBoundary(string fieldDirectory)
    {
        var boundary = new Boundary();
        var boundaryFilePath = Path.Combine(fieldDirectory, "Boundary.txt");

        if (!File.Exists(boundaryFilePath))
        {
            return boundary; // Return empty boundary if file doesn't exist
        }

        using (var reader = new StreamReader(boundaryFilePath))
        {
            // Skip optional header
            string? line = reader.ReadLine();
            if (line != null && !line.TrimStart().StartsWith("$", StringComparison.OrdinalIgnoreCase))
            {
                // First line was not header -> treat as first data line
                reader.BaseStream.Seek(0, SeekOrigin.Begin);
                reader.DiscardBufferedData();
            }

            // Read polygons (first is outer, rest are inner)
            bool isFirst = true;
            while (!reader.EndOfStream)
            {
                var polygon = ReadBoundaryPolygon(reader);
                if (polygon != null && polygon.IsValid)
                {
                    if (isFirst)
                    {
                        boundary.OuterBoundary = polygon;
                        isFirst = false;
                    }
                    else
                    {
                        boundary.InnerBoundaries.Add(polygon);
                    }
                }
                else if (polygon == null)
                {
                    // End of file or error
                    break;
                }
            }
        }

        // Load headland as inner boundary
        LoadHeadland(fieldDirectory, boundary);

        return boundary;
    }

    /// <summary>
    /// Load headland from Headland.Txt as the working area boundary.
    /// The headland polygon defines where sections CAN work (inside = work area).
    /// The area between this and outer boundary is the headland zone (no work until headland passes).
    /// </summary>
    private void LoadHeadland(string fieldDirectory, Boundary boundary)
    {
        var headlandFilePath = Path.Combine(fieldDirectory, "Headland.Txt");
        if (!File.Exists(headlandFilePath))
        {
            return; // No headland file
        }

        using (var reader = new StreamReader(headlandFilePath))
        {
            // Skip optional header
            string? line = reader.ReadLine();
            if (line != null && !line.TrimStart().StartsWith("$", StringComparison.OrdinalIgnoreCase))
            {
                // First line was not header -> treat as first data line
                reader.BaseStream.Seek(0, SeekOrigin.Begin);
                reader.DiscardBufferedData();
            }

            // Read headland polygon - store as HeadlandPolygon, NOT as inner boundary
            var headland = ReadBoundaryPolygon(reader);
            if (headland != null && headland.IsValid)
            {
                boundary.HeadlandPolygon = headland;
            }
        }
    }

    /// <summary>
    /// Read a single boundary polygon from the reader
    /// </summary>
    private BoundaryPolygon? ReadBoundaryPolygon(StreamReader reader)
    {
        var polygon = new BoundaryPolygon();
        string? line = reader.ReadLine();

        // Skip empty lines
        while (line != null && string.IsNullOrWhiteSpace(line))
        {
            line = reader.ReadLine();
        }

        if (line == null) return null;

        // Handle legacy duplicate isDriveThru lines (some versions wrote it twice)
        for (int pass = 0; pass < 2; pass++)
        {
            if (bool.TryParse(line?.Trim(), out bool isDriveThru))
            {
                polygon.IsDriveThrough = isDriveThru;
                line = reader.ReadLine();
                if (line == null) return null;
                continue;
            }
            break;
        }

        if (line == null) return null;

        // Read point count
        if (!int.TryParse(line.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int count))
        {
            return null; // Malformed count
        }

        // Read points
        for (int i = 0; i < count; i++)
        {
            line = reader.ReadLine();
            if (line == null) break;

            var parts = line.Split(',');
            if (parts.Length < 3) continue;

            if (double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out double easting) &&
                double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double northing) &&
                double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out double heading))
            {
                polygon.Points.Add(new BoundaryPoint(easting, northing, heading));
            }
        }

        // Update bounding box cache for fast boundary checks
        polygon.UpdateBounds();

        return polygon;
    }

    /// <summary>
    /// Save boundary to Boundary.txt
    /// </summary>
    public void SaveBoundary(Boundary boundary, string fieldDirectory)
    {
        if (string.IsNullOrWhiteSpace(fieldDirectory))
        {
            throw new ArgumentNullException(nameof(fieldDirectory));
        }

        if (!Directory.Exists(fieldDirectory))
        {
            Directory.CreateDirectory(fieldDirectory);
        }

        var boundaryFilePath = Path.Combine(fieldDirectory, "Boundary.txt");

        using (var writer = new StreamWriter(boundaryFilePath, false))
        {
            writer.WriteLine("$Boundary");

            // Write outer boundary
            if (boundary.OuterBoundary != null && boundary.OuterBoundary.IsValid)
            {
                WriteBoundaryPolygon(writer, boundary.OuterBoundary);
            }

            // Write inner boundaries (holes)
            foreach (var innerBoundary in boundary.InnerBoundaries)
            {
                if (innerBoundary.IsValid)
                {
                    WriteBoundaryPolygon(writer, innerBoundary);
                }
            }
        }
    }

    /// <summary>
    /// Write a single boundary polygon
    /// </summary>
    private void WriteBoundaryPolygon(StreamWriter writer, BoundaryPolygon polygon)
    {
        // Write isDriveThru flag
        writer.WriteLine(polygon.IsDriveThrough.ToString());

        // Write point count
        writer.WriteLine(polygon.Points.Count.ToString(CultureInfo.InvariantCulture));

        // Write points (Easting, Northing, Heading)
        foreach (var point in polygon.Points)
        {
            writer.WriteLine($"{FormatDouble(point.Easting, 3)},{FormatDouble(point.Northing, 3)},{FormatDouble(point.Heading, 5)}");
        }
    }

    /// <summary>
    /// Create an empty Boundary.txt
    /// </summary>
    public void CreateEmptyBoundary(string fieldDirectory)
    {
        if (string.IsNullOrWhiteSpace(fieldDirectory))
        {
            throw new ArgumentNullException(nameof(fieldDirectory));
        }

        if (!Directory.Exists(fieldDirectory))
        {
            Directory.CreateDirectory(fieldDirectory);
        }

        var boundaryFilePath = Path.Combine(fieldDirectory, "Boundary.txt");
        File.WriteAllText(boundaryFilePath, "$Boundary" + Environment.NewLine, Encoding.UTF8);
    }

    /// <summary>
    /// Format double with specified decimal places
    /// </summary>
    private string FormatDouble(double value, int decimalPlaces)
    {
        return value.ToString($"F{decimalPlaces}", CultureInfo.InvariantCulture);
    }
}

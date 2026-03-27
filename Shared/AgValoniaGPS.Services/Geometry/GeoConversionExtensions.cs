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

using System.Collections.Generic;
using AgValoniaGPS.Models.Base;

namespace AgValoniaGPS.Services.Geometry;

/// <summary>
/// Batch coordinate conversion helpers that delegate to <see cref="GeoConversion"/>.
/// </summary>
public static class GeoConversionExtensions
{
    /// <summary>
    /// Convert a list of local easting/northing points to GeoJSON [lon, lat] arrays.
    /// </summary>
    public static List<double[]> ToGeoJsonCoordinates(this GeoConversion geo, IReadOnlyList<Vec2> localPoints)
    {
        var coords = new List<double[]>(localPoints.Count);
        foreach (var pt in localPoints)
        {
            var (lat, lon) = geo.ToWgs84(pt);
            coords.Add(new[] { lon, lat });
        }
        return coords;
    }

    /// <summary>
    /// Convert a list of local Vec3 points to GeoJSON [lon, lat, heading] arrays.
    /// Heading is preserved as the third element (radians).
    /// </summary>
    public static List<double[]> ToGeoJsonCoordinates(this GeoConversion geo, IReadOnlyList<Vec3> localPoints)
    {
        var coords = new List<double[]>(localPoints.Count);
        foreach (var pt in localPoints)
        {
            var (lat, lon) = geo.ToWgs84(new Vec2(pt.Easting, pt.Northing));
            coords.Add(new[] { lon, lat, pt.Heading });
        }
        return coords;
    }

    /// <summary>
    /// Convert GeoJSON [lon, lat] arrays back to local Vec2 points.
    /// </summary>
    public static List<Vec2> FromGeoJsonCoordinates(this GeoConversion geo, IReadOnlyList<double[]> geoCoords)
    {
        var points = new List<Vec2>(geoCoords.Count);
        foreach (var coord in geoCoords)
        {
            if (coord.Length < 2) continue;
            points.Add(geo.ToLocal(coord[1], coord[0])); // lat = [1], lon = [0]
        }
        return points;
    }

    /// <summary>
    /// Convert GeoJSON [lon, lat, heading] arrays back to local Vec3 points.
    /// If only [lon, lat] is provided, heading defaults to 0.
    /// </summary>
    public static List<Vec3> FromGeoJsonCoordinatesVec3(this GeoConversion geo, IReadOnlyList<double[]> geoCoords)
    {
        var points = new List<Vec3>(geoCoords.Count);
        foreach (var coord in geoCoords)
        {
            if (coord.Length < 2) continue;
            var local = geo.ToLocal(coord[1], coord[0]);
            double heading = coord.Length >= 3 ? coord[2] : 0;
            points.Add(new Vec3(local.Easting, local.Northing, heading));
        }
        return points;
    }
}

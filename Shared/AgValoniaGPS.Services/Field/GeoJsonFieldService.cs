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
using System.IO;
using System.Linq;
using System.Text.Json;
using AgValoniaGPS.Models;
using AgValoniaGPS.Models.Base;
using AgValoniaGPS.Models.GeoJson;
using AgValoniaGPS.Models.Track;
using AgValoniaGPS.Services.Geometry;

namespace AgValoniaGPS.Services.Field;

/// <summary>
/// Loads and saves field data as GeoJSON FeatureCollections.
/// Each field component (boundary, tracks, headland, etc.) becomes a Feature
/// with a "role" property for identification on load.
/// </summary>
public class GeoJsonFieldService
{
    private const string FileName = "field.geojson";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Check whether a field directory contains a GeoJSON field file.
    /// </summary>
    public static bool Exists(string fieldDirectory)
    {
        return File.Exists(Path.Combine(fieldDirectory, FileName));
    }

    /// <summary>
    /// Save a field as a GeoJSON FeatureCollection.
    /// </summary>
    public static void Save(Models.Field field, IReadOnlyList<Models.Track.Track>? tracks)
    {
        if (string.IsNullOrWhiteSpace(field.DirectoryPath))
            throw new ArgumentException("Field.DirectoryPath must be set", nameof(field));

        if (!Directory.Exists(field.DirectoryPath))
            Directory.CreateDirectory(field.DirectoryPath);

        var geo = new GeoConversion(field.Origin.Latitude, field.Origin.Longitude);
        var fc = new GeoJsonFeatureCollection();

        // Metadata feature -- a Point at the field origin
        fc.Features.Add(BuildMetadataFeature(field));

        // Boundaries
        if (field.Boundary != null)
        {
            if (field.Boundary.OuterBoundary is { IsValid: true })
                fc.Features.Add(BuildBoundaryFeature(geo, field.Boundary.OuterBoundary, FeatureRoles.OuterBoundary));

            foreach (var inner in field.Boundary.InnerBoundaries)
            {
                if (inner.IsValid)
                    fc.Features.Add(BuildBoundaryFeature(geo, inner, FeatureRoles.InnerBoundary));
            }

            if (field.Boundary.HeadlandPolygon is { IsValid: true })
                fc.Features.Add(BuildBoundaryFeature(geo, field.Boundary.HeadlandPolygon, FeatureRoles.Headland));
        }

        // Tracks
        if (tracks != null)
        {
            foreach (var track in tracks)
            {
                if (track.Points.Count >= 2)
                    fc.Features.Add(BuildTrackFeature(geo, track));
            }
        }

        // Background image bounds
        if (field.BackgroundImage is { IsValid: true })
            fc.Features.Add(BuildBackgroundImageFeature(geo, field.BackgroundImage));

        var json = JsonSerializer.Serialize(fc, SerializerOptions);
        File.WriteAllText(Path.Combine(field.DirectoryPath, FileName), json);
    }

    /// <summary>
    /// Load a field from a GeoJSON FeatureCollection.
    /// Returns the field and any tracks found.
    /// </summary>
    public static (Models.Field field, List<Models.Track.Track> tracks) Load(string fieldDirectory)
    {
        var path = Path.Combine(fieldDirectory, FileName);
        if (!File.Exists(path))
            throw new FileNotFoundException("field.geojson not found", path);

        var json = File.ReadAllText(path);
        var fc = JsonSerializer.Deserialize<GeoJsonFeatureCollection>(json, SerializerOptions)
            ?? throw new InvalidDataException("Failed to deserialize GeoJSON");

        // We need the origin first to set up coordinate conversion
        var metaFeature = fc.Features.FirstOrDefault(f => GetStringProp(f, FieldPropertyKeys.Role) == FeatureRoles.Metadata);
        if (metaFeature == null)
            throw new InvalidDataException("GeoJSON missing metadata feature");

        double originLat = GetDoubleProp(metaFeature, FieldPropertyKeys.OriginLatitude);
        double originLon = GetDoubleProp(metaFeature, FieldPropertyKeys.OriginLongitude);

        var field = new Models.Field
        {
            Name = Path.GetFileName(fieldDirectory),
            DirectoryPath = fieldDirectory,
            Origin = new Position { Latitude = originLat, Longitude = originLon },
            Convergence = GetDoubleProp(metaFeature, FieldPropertyKeys.Convergence),
        };

        var nameProp = GetStringProp(metaFeature, FieldPropertyKeys.Name);
        if (!string.IsNullOrEmpty(nameProp))
            field.Name = nameProp;

        var createdStr = GetStringProp(metaFeature, FieldPropertyKeys.CreatedDate);
        if (DateTime.TryParse(createdStr, out var created))
            field.CreatedDate = created;

        var modifiedStr = GetStringProp(metaFeature, FieldPropertyKeys.LastModifiedDate);
        if (DateTime.TryParse(modifiedStr, out var modified))
            field.LastModifiedDate = modified;

        var geo = new GeoConversion(originLat, originLon);
        var boundary = new Boundary();
        var tracks = new List<Models.Track.Track>();

        foreach (var feature in fc.Features)
        {
            var role = GetStringProp(feature, FieldPropertyKeys.Role);
            switch (role)
            {
                case FeatureRoles.OuterBoundary:
                    boundary.OuterBoundary = ReadBoundaryPolygon(geo, feature);
                    break;

                case FeatureRoles.InnerBoundary:
                    var inner = ReadBoundaryPolygon(geo, feature);
                    if (inner != null)
                    {
                        inner.IsDriveThrough = GetBoolProp(feature, FieldPropertyKeys.IsDriveThrough);
                        boundary.InnerBoundaries.Add(inner);
                    }
                    break;

                case FeatureRoles.Headland:
                    boundary.HeadlandPolygon = ReadBoundaryPolygon(geo, feature);
                    break;

                case FeatureRoles.Track:
                    var track = ReadTrack(geo, feature);
                    if (track != null)
                        tracks.Add(track);
                    break;

                case FeatureRoles.BackgroundImage:
                    field.BackgroundImage = ReadBackgroundImage(geo, feature);
                    break;
            }
        }

        if (boundary.OuterBoundary != null)
            field.Boundary = boundary;

        return (field, tracks);
    }

    // ---------------------------------------------------------------
    // Feature builders (local -> GeoJSON)
    // ---------------------------------------------------------------

    private static GeoJsonFeature BuildMetadataFeature(Models.Field field)
    {
        return new GeoJsonFeature
        {
            Geometry = new GeoJsonGeometry
            {
                Type = GeoJsonTypes.Point,
                Coordinates = new[] { field.Origin.Longitude, field.Origin.Latitude }
            },
            Properties = new Dictionary<string, object?>
            {
                [FieldPropertyKeys.Role] = FeatureRoles.Metadata,
                [FieldPropertyKeys.Name] = field.Name,
                [FieldPropertyKeys.OriginLatitude] = field.Origin.Latitude,
                [FieldPropertyKeys.OriginLongitude] = field.Origin.Longitude,
                [FieldPropertyKeys.Convergence] = field.Convergence,
                [FieldPropertyKeys.AreaHectares] = field.TotalArea,
                [FieldPropertyKeys.CreatedDate] = field.CreatedDate.ToString("O"),
                [FieldPropertyKeys.LastModifiedDate] = field.LastModifiedDate.ToString("O"),
            }
        };
    }

    private static GeoJsonFeature BuildBoundaryFeature(GeoConversion geo, BoundaryPolygon polygon, string role)
    {
        var ring = BoundaryToGeoJsonRing(geo, polygon);
        return new GeoJsonFeature
        {
            Geometry = new GeoJsonGeometry
            {
                Type = GeoJsonTypes.Polygon,
                Coordinates = new[] { ring }
            },
            Properties = new Dictionary<string, object?>
            {
                [FieldPropertyKeys.Role] = role,
                [FieldPropertyKeys.IsDriveThrough] = polygon.IsDriveThrough,
                [FieldPropertyKeys.AreaHectares] = polygon.AreaHectares,
            }
        };
    }

    private static GeoJsonFeature BuildTrackFeature(GeoConversion geo, Models.Track.Track track)
    {
        var coords = geo.ToGeoJsonCoordinates(track.Points);
        return new GeoJsonFeature
        {
            Geometry = new GeoJsonGeometry
            {
                Type = GeoJsonTypes.LineString,
                Coordinates = coords.Select(c => (object)c).ToArray()
            },
            Properties = new Dictionary<string, object?>
            {
                [FieldPropertyKeys.Role] = FeatureRoles.Track,
                [FieldPropertyKeys.Name] = track.Name,
                [FieldPropertyKeys.TrackType] = (int)track.Type,
                [FieldPropertyKeys.IsClosed] = track.IsClosed,
                [FieldPropertyKeys.NudgeDistance] = track.NudgeDistance,
                [FieldPropertyKeys.IsVisible] = track.IsVisible,
            }
        };
    }

    private static GeoJsonFeature BuildBackgroundImageFeature(GeoConversion geo, BackgroundImage img)
    {
        // Store image bounds as a polygon (4 corners + closing point)
        var corners = new List<Vec2>
        {
            new(img.MinEasting, img.MinNorthing),
            new(img.MaxEasting, img.MinNorthing),
            new(img.MaxEasting, img.MaxNorthing),
            new(img.MinEasting, img.MaxNorthing),
        };
        var ring = geo.ToGeoJsonCoordinates(corners);
        // Close the ring per GeoJSON spec
        ring.Add(ring[0]);

        return new GeoJsonFeature
        {
            Geometry = new GeoJsonGeometry
            {
                Type = GeoJsonTypes.Polygon,
                Coordinates = new[] { ring.Select(c => (object)c).ToArray() }
            },
            Properties = new Dictionary<string, object?>
            {
                [FieldPropertyKeys.Role] = FeatureRoles.BackgroundImage,
            }
        };
    }

    /// <summary>
    /// Convert a BoundaryPolygon to a GeoJSON ring (closed array of [lon, lat, heading]).
    /// </summary>
    private static object[] BoundaryToGeoJsonRing(GeoConversion geo, BoundaryPolygon polygon)
    {
        var ring = new List<object>(polygon.Points.Count + 1);
        foreach (var pt in polygon.Points)
        {
            var (lat, lon) = geo.ToWgs84(new Vec2(pt.Easting, pt.Northing));
            ring.Add(new[] { lon, lat, pt.Heading });
        }
        // Close the ring
        if (polygon.Points.Count > 0)
        {
            var first = polygon.Points[0];
            var (lat, lon) = geo.ToWgs84(new Vec2(first.Easting, first.Northing));
            ring.Add(new[] { lon, lat, first.Heading });
        }
        return ring.ToArray();
    }

    // ---------------------------------------------------------------
    // Feature readers (GeoJSON -> local)
    // ---------------------------------------------------------------

    private static BoundaryPolygon? ReadBoundaryPolygon(GeoConversion geo, GeoJsonFeature feature)
    {
        var coords = ReadPolygonRing(feature.Geometry, 0);
        if (coords == null || coords.Count < 3)
            return null;

        var polygon = new BoundaryPolygon();
        foreach (var coord in coords)
        {
            var local = geo.ToLocal(coord[1], coord[0]);
            double heading = coord.Length >= 3 ? coord[2] : 0;
            polygon.Points.Add(new BoundaryPoint(local.Easting, local.Northing, heading));
        }

        // Remove closing duplicate if present
        if (polygon.Points.Count > 1)
        {
            var first = polygon.Points[0];
            var last = polygon.Points[^1];
            if (Math.Abs(first.Easting - last.Easting) < 0.001 &&
                Math.Abs(first.Northing - last.Northing) < 0.001)
            {
                polygon.Points.RemoveAt(polygon.Points.Count - 1);
            }
        }

        polygon.UpdateBounds();
        return polygon;
    }

    private static Models.Track.Track? ReadTrack(GeoConversion geo, GeoJsonFeature feature)
    {
        var coords = ReadLineStringCoords(feature.Geometry);
        if (coords == null || coords.Count < 2)
            return null;

        var points = geo.FromGeoJsonCoordinatesVec3(coords);

        var trackTypeInt = GetIntProp(feature, FieldPropertyKeys.TrackType);
        var trackType = Enum.IsDefined(typeof(TrackType), trackTypeInt)
            ? (TrackType)trackTypeInt
            : TrackType.ABLine;

        return new Models.Track.Track
        {
            Name = GetStringProp(feature, FieldPropertyKeys.Name) ?? string.Empty,
            Points = points,
            Type = trackType,
            IsClosed = GetBoolProp(feature, FieldPropertyKeys.IsClosed),
            NudgeDistance = GetDoubleProp(feature, FieldPropertyKeys.NudgeDistance),
            IsVisible = GetBoolPropDefault(feature, FieldPropertyKeys.IsVisible, true),
        };
    }

    private static BackgroundImage? ReadBackgroundImage(GeoConversion geo, GeoJsonFeature feature)
    {
        var coords = ReadPolygonRing(feature.Geometry, 0);
        if (coords == null || coords.Count < 4)
            return null;

        double minE = double.MaxValue, maxE = double.MinValue;
        double minN = double.MaxValue, maxN = double.MinValue;

        foreach (var c in coords)
        {
            var local = geo.ToLocal(c[1], c[0]);
            if (local.Easting < minE) minE = local.Easting;
            if (local.Easting > maxE) maxE = local.Easting;
            if (local.Northing < minN) minN = local.Northing;
            if (local.Northing > maxN) maxN = local.Northing;
        }

        return new BackgroundImage
        {
            MinEasting = minE,
            MaxEasting = maxE,
            MinNorthing = minN,
            MaxNorthing = maxN,
            IsEnabled = true,
        };
    }

    // ---------------------------------------------------------------
    // JSON coordinate extraction helpers
    // ---------------------------------------------------------------

    /// <summary>
    /// Read the outer ring (index 0) of a Polygon geometry.
    /// Coordinates arrive as a JsonElement after deserialization.
    /// </summary>
    private static List<double[]>? ReadPolygonRing(GeoJsonGeometry geometry, int ringIndex)
    {
        if (geometry.Coordinates is not JsonElement je)
            return null;

        if (je.ValueKind != JsonValueKind.Array)
            return null;

        int idx = 0;
        foreach (var ringElem in je.EnumerateArray())
        {
            if (idx == ringIndex)
                return ParseCoordArray(ringElem);
            idx++;
        }
        return null;
    }

    private static List<double[]>? ReadLineStringCoords(GeoJsonGeometry geometry)
    {
        if (geometry.Coordinates is not JsonElement je)
            return null;

        if (je.ValueKind != JsonValueKind.Array)
            return null;

        return ParseCoordArray(je);
    }

    private static List<double[]> ParseCoordArray(JsonElement arrayElem)
    {
        var result = new List<double[]>();
        foreach (var ptElem in arrayElem.EnumerateArray())
        {
            if (ptElem.ValueKind != JsonValueKind.Array)
                continue;

            var values = new List<double>();
            foreach (var v in ptElem.EnumerateArray())
            {
                if (v.TryGetDouble(out double d))
                    values.Add(d);
            }
            if (values.Count >= 2)
                result.Add(values.ToArray());
        }
        return result;
    }

    // ---------------------------------------------------------------
    // Property helpers
    // ---------------------------------------------------------------

    private static string? GetStringProp(GeoJsonFeature f, string key)
    {
        if (f.Properties.TryGetValue(key, out var val))
        {
            if (val is JsonElement je && je.ValueKind == JsonValueKind.String)
                return je.GetString();
            return val?.ToString();
        }
        return null;
    }

    private static double GetDoubleProp(GeoJsonFeature f, string key)
    {
        if (f.Properties.TryGetValue(key, out var val))
        {
            if (val is JsonElement je && je.ValueKind == JsonValueKind.Number)
                return je.GetDouble();
            if (val is double d)
                return d;
            if (double.TryParse(val?.ToString(), out double parsed))
                return parsed;
        }
        return 0;
    }

    private static int GetIntProp(GeoJsonFeature f, string key)
    {
        if (f.Properties.TryGetValue(key, out var val))
        {
            if (val is JsonElement je && je.ValueKind == JsonValueKind.Number)
                return je.GetInt32();
            if (val is int i)
                return i;
            if (int.TryParse(val?.ToString(), out int parsed))
                return parsed;
        }
        return 0;
    }

    private static bool GetBoolProp(GeoJsonFeature f, string key)
    {
        return GetBoolPropDefault(f, key, false);
    }

    private static bool GetBoolPropDefault(GeoJsonFeature f, string key, bool defaultValue)
    {
        if (f.Properties.TryGetValue(key, out var val))
        {
            if (val is JsonElement je)
            {
                if (je.ValueKind == JsonValueKind.True) return true;
                if (je.ValueKind == JsonValueKind.False) return false;
            }
            if (val is bool b)
                return b;
            if (bool.TryParse(val?.ToString(), out bool parsed))
                return parsed;
        }
        return defaultValue;
    }
}

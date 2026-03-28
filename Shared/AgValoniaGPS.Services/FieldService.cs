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
using AgValoniaGPS.Models;
using AgValoniaGPS.Services.GeoJson;

namespace AgValoniaGPS.Services;

/// <summary>
/// Implementation of field management service
/// Coordinates file I/O services to provide complete field management
/// </summary>
public class FieldService : IFieldService
{
    private readonly FieldPlaneFileService _fieldPlaneService;
    private readonly BoundaryFileService _boundaryService;
    private readonly BackgroundImageFileService _backgroundImageService;

    public event EventHandler<Field?>? ActiveFieldChanged;
    public Field? ActiveField { get; private set; }

    public FieldService()
    {
        _fieldPlaneService = new FieldPlaneFileService();
        _boundaryService = new BoundaryFileService();
        _backgroundImageService = new BackgroundImageFileService();
    }

    /// <summary>
    /// Get list of available field names in the Fields directory
    /// </summary>
    public List<string> GetAvailableFields(string fieldsRootDirectory)
    {
        if (!Directory.Exists(fieldsRootDirectory))
        {
            return new List<string>();
        }

        return Directory.GetDirectories(fieldsRootDirectory)
            .Select(Path.GetFileName)
            .Where(name => !string.IsNullOrEmpty(name))
            .Cast<string>()
            .OrderBy(name => name)
            .ToList();
    }

    /// <summary>
    /// Load a complete field. Prefers field.geojson when present, falls back to legacy text files.
    /// </summary>
    public Field LoadField(string fieldDirectory)
    {
        if (GeoJsonFieldService.Exists(fieldDirectory))
        {
            var (field, _) = GeoJsonFieldService.Load(fieldDirectory);
            // Background image file (BackPic.png) is still loaded from the legacy service
            // because the image itself is not stored in GeoJSON.
            field.BackgroundImage ??= _backgroundImageService.LoadBackgroundImage(fieldDirectory);
            return field;
        }

        var legacyField = _fieldPlaneService.LoadField(fieldDirectory);
        legacyField.Boundary = _boundaryService.LoadBoundary(fieldDirectory);
        legacyField.BackgroundImage = _backgroundImageService.LoadBackgroundImage(fieldDirectory);
        return legacyField;
    }

    /// <summary>
    /// Save a complete field. Writes both GeoJSON and legacy formats for backwards compatibility.
    /// </summary>
    public void SaveField(Field field)
    {
        if (string.IsNullOrWhiteSpace(field.DirectoryPath))
        {
            throw new ArgumentException("Field.DirectoryPath must be set", nameof(field));
        }

        // Legacy files (keep for AgOpenGPS interop)
        _fieldPlaneService.SaveField(field, field.DirectoryPath);

        if (field.Boundary != null)
        {
            _boundaryService.SaveBoundary(field.Boundary, field.DirectoryPath);
        }

        if (field.BackgroundImage != null)
        {
            _backgroundImageService.SaveBackgroundImage(field.BackgroundImage, field.DirectoryPath);
        }

        // GeoJSON (new canonical format -- tracks saved separately by caller)
        try
        {
            GeoJsonFieldService.Save(field, tracks: null);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"GeoJSON save failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Create a new empty field
    /// </summary>
    public Field CreateField(string fieldsRootDirectory, string fieldName, Position originPosition)
    {
        var fieldDirectory = Path.Combine(fieldsRootDirectory, fieldName);

        if (Directory.Exists(fieldDirectory))
        {
            throw new InvalidOperationException($"Field '{fieldName}' already exists");
        }

        Directory.CreateDirectory(fieldDirectory);

        var field = new Field
        {
            Name = fieldName,
            DirectoryPath = fieldDirectory,
            Origin = originPosition,
            CreatedDate = DateTime.Now,
            LastModifiedDate = DateTime.Now
        };

        // Create empty boundary file
        _boundaryService.CreateEmptyBoundary(fieldDirectory);

        // Save field metadata
        _fieldPlaneService.SaveField(field, fieldDirectory);

        return field;
    }

    /// <summary>
    /// Delete a field (removes entire directory)
    /// </summary>
    public void DeleteField(string fieldDirectory)
    {
        if (Directory.Exists(fieldDirectory))
        {
            Directory.Delete(fieldDirectory, true);
        }
    }

    /// <summary>
    /// Check if a field exists (GeoJSON or legacy)
    /// </summary>
    public bool FieldExists(string fieldDirectory)
    {
        return Directory.Exists(fieldDirectory) &&
               (GeoJsonFieldService.Exists(fieldDirectory) ||
                File.Exists(Path.Combine(fieldDirectory, "Field.txt")));
    }

    /// <summary>
    /// Set the active field
    /// </summary>
    public void SetActiveField(Field? field)
    {
        if (ActiveField != field)
        {
            ActiveField = field;
            ActiveFieldChanged?.Invoke(this, field);
        }
    }
}
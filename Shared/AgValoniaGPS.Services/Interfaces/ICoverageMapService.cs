using System;
using System.Collections.Generic;
using AgValoniaGPS.Models.Base;
using AgValoniaGPS.Models.Coverage;

namespace AgValoniaGPS.Services.Interfaces;

/// <summary>
/// Service for tracking and managing coverage (worked area) as the tool moves across the field.
/// Coverage is stored as triangle strips for efficient rendering.
/// </summary>
public interface ICoverageMapService
{
    /// <summary>
    /// Total worked area in square meters
    /// </summary>
    double TotalWorkedArea { get; }

    /// <summary>
    /// User-resettable worked area counter in square meters
    /// </summary>
    double TotalWorkedAreaUser { get; }

    /// <summary>
    /// Number of coverage patches
    /// </summary>
    int PatchCount { get; }

    /// <summary>
    /// Whether any zone is currently being mapped
    /// </summary>
    bool IsAnyZoneMapping { get; }

    /// <summary>
    /// Start mapping coverage for a zone.
    /// Creates a new triangle strip patch.
    /// </summary>
    /// <param name="zoneIndex">Zone index (0-based)</param>
    /// <param name="leftEdge">Left edge position in world coordinates</param>
    /// <param name="rightEdge">Right edge position in world coordinates</param>
    /// <param name="color">Coverage color (optional, uses default if not specified)</param>
    void StartMapping(int zoneIndex, Vec2 leftEdge, Vec2 rightEdge, CoverageColor? color = null);

    /// <summary>
    /// Stop mapping coverage for a zone.
    /// Finalizes the current triangle strip patch.
    /// </summary>
    /// <param name="zoneIndex">Zone index (0-based)</param>
    void StopMapping(int zoneIndex);

    /// <summary>
    /// Add a coverage point to an active zone.
    /// Extends the current triangle strip with two new vertices.
    /// Should be called each GPS update when mapping is active.
    /// </summary>
    /// <param name="zoneIndex">Zone index (0-based)</param>
    /// <param name="leftEdge">Left edge position in world coordinates</param>
    /// <param name="rightEdge">Right edge position in world coordinates</param>
    void AddCoveragePoint(int zoneIndex, Vec2 leftEdge, Vec2 rightEdge);

    /// <summary>
    /// Check if a zone is currently mapping
    /// </summary>
    /// <param name="zoneIndex">Zone index (0-based)</param>
    /// <returns>True if the zone is actively mapping coverage</returns>
    bool IsZoneMapping(int zoneIndex);

    /// <summary>
    /// Check if a point is covered by any existing coverage
    /// </summary>
    /// <param name="easting">Point easting coordinate</param>
    /// <param name="northing">Point northing coordinate</param>
    /// <returns>True if the point is within a coverage area</returns>
    bool IsPointCovered(double easting, double northing);

    /// <summary>
    /// Get all coverage patches for rendering
    /// </summary>
    /// <returns>Read-only list of coverage patches</returns>
    IReadOnlyList<CoveragePatch> GetPatches();

    /// <summary>
    /// Get patches for a specific zone
    /// </summary>
    /// <param name="zoneIndex">Zone index (0-based)</param>
    /// <returns>Read-only list of coverage patches for the zone</returns>
    IReadOnlyList<CoveragePatch> GetPatchesForZone(int zoneIndex);

    /// <summary>
    /// Clear all coverage data
    /// </summary>
    void ClearAll();

    /// <summary>
    /// Reset user area counter (total area remains)
    /// </summary>
    void ResetUserArea();

    /// <summary>
    /// Save coverage to Sections.txt file
    /// </summary>
    /// <param name="fieldDirectory">Field directory path</param>
    void SaveToFile(string fieldDirectory);

    /// <summary>
    /// Load coverage from Sections.txt file
    /// </summary>
    /// <param name="fieldDirectory">Field directory path</param>
    void LoadFromFile(string fieldDirectory);

    /// <summary>
    /// Event fired when coverage is updated
    /// </summary>
    event EventHandler<CoverageUpdatedEventArgs>? CoverageUpdated;
}

/// <summary>
/// Event arguments for coverage updates
/// </summary>
public class CoverageUpdatedEventArgs : EventArgs
{
    /// <summary>
    /// Total worked area in square meters
    /// </summary>
    public double TotalArea { get; init; }

    /// <summary>
    /// Number of coverage patches
    /// </summary>
    public int PatchCount { get; init; }

    /// <summary>
    /// Area added in this update (square meters)
    /// </summary>
    public double AreaAdded { get; init; }
}

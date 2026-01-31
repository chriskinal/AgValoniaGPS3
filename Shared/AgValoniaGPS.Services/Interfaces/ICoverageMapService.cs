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
    /// Fire the CoverageUpdated event if coverage has changed since last flush.
    /// Call this once per GPS update cycle to avoid firing many events for multiple sections.
    /// </summary>
    void FlushCoverageUpdate();

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
    /// Calculate coverage for a section segment using coordinate transform method.
    /// More accurate than point-based check as it detects partial overlaps and gaps.
    /// </summary>
    /// <param name="sectionCenter">Center point of section in world coords.</param>
    /// <param name="heading">Section heading in radians.</param>
    /// <param name="halfWidth">Half the section width in meters.</param>
    /// <param name="lookAheadDistance">Distance ahead to check (0 = current position).</param>
    /// <returns>Coverage result with percentage and overlap info.</returns>
    CoverageResult GetSegmentCoverage(
        Vec2 sectionCenter,
        double heading,
        double halfWidth,
        double lookAheadDistance = 0);

    /// <summary>
    /// Check coverage at multiple look-ahead distances with single transform pass.
    /// More efficient than calling GetSegmentCoverage multiple times.
    /// </summary>
    /// <param name="sectionCenter">Center point of section in world coords.</param>
    /// <param name="heading">Section heading in radians.</param>
    /// <param name="halfWidth">Half the section width in meters.</param>
    /// <param name="lookOnDistance">Look-ahead distance for section on check.</param>
    /// <param name="lookOffDistance">Look-ahead distance for section off check.</param>
    /// <returns>Tuple of (Current, LookOn, LookOff) coverage results.</returns>
    (CoverageResult Current, CoverageResult LookOn, CoverageResult LookOff) GetSegmentCoverageMulti(
        Vec2 sectionCenter,
        double heading,
        double halfWidth,
        double lookOnDistance,
        double lookOffDistance);

    /// <summary>
    /// Get all coverage patches for rendering (detailed geometry for accurate display)
    /// </summary>
    /// <returns>Read-only list of coverage patches</returns>
    IReadOnlyList<CoveragePatch> GetPatches();

    /// <summary>
    /// Get coverage bitmap bounds in world coordinates.
    /// Returns null if no coverage exists.
    /// </summary>
    (double MinE, double MaxE, double MinN, double MaxN)? GetCoverageBounds();

    /// <summary>
    /// Get coverage cells within viewport bounds for bitmap rendering.
    /// Only returns cells within the specified world coordinate bounds.
    /// Time complexity: O(viewport area), not O(total coverage).
    /// </summary>
    /// <param name="cellSize">Size of each output cell in meters</param>
    /// <param name="viewMinE">Viewport minimum easting</param>
    /// <param name="viewMaxE">Viewport maximum easting</param>
    /// <param name="viewMinN">Viewport minimum northing</param>
    /// <param name="viewMaxN">Viewport maximum northing</param>
    IEnumerable<(int CellX, int CellY, CoverageColor Color)> GetCoverageBitmapCells(
        double cellSize, double viewMinE, double viewMaxE, double viewMinN, double viewMaxN);

    /// <summary>
    /// Get newly added coverage cells since last call (for incremental bitmap updates).
    /// Clears the pending list after returning.
    /// </summary>
    /// <param name="cellSize">Size of each cell in meters</param>
    /// <returns>Enumerable of (cellX, cellY, color) for newly added cells</returns>
    IEnumerable<(int CellX, int CellY, CoverageColor Color)> GetNewCoverageBitmapCells(double cellSize);

    /// <summary>
    /// Get patches for a specific zone
    /// </summary>
    /// <param name="zoneIndex">Zone index (0-based)</param>
    /// <returns>Read-only list of coverage patches for the zone</returns>
    IReadOnlyList<CoveragePatch> GetPatchesForZone(int zoneIndex);

    /// <summary>
    /// Set fixed field bounds for bitmap coordinate calculations.
    /// This establishes a stable coordinate system that doesn't change as coverage expands.
    /// Should be called when a field/boundary is loaded.
    /// </summary>
    /// <param name="minE">Minimum easting (with padding)</param>
    /// <param name="maxE">Maximum easting (with padding)</param>
    /// <param name="minN">Minimum northing (with padding)</param>
    /// <param name="maxN">Maximum northing (with padding)</param>
    void SetFieldBounds(double minE, double maxE, double minN, double maxN);

    /// <summary>
    /// Clear field bounds (when field is closed)
    /// </summary>
    void ClearFieldBounds();

    /// <summary>
    /// Clear all coverage data
    /// </summary>
    void ClearAll();

    /// <summary>
    /// Reset user area counter (total area remains)
    /// </summary>
    void ResetUserArea();

    /// <summary>
    /// Save coverage to Coverage.bin file (RLE-compressed bit array)
    /// </summary>
    /// <param name="fieldDirectory">Field directory path</param>
    void SaveToFile(string fieldDirectory);

    /// <summary>
    /// Load coverage from Coverage.bin file
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

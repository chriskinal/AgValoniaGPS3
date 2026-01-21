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
using AgValoniaGPS.Models;
using AgValoniaGPS.Services.Interfaces;

namespace AgValoniaGPS.Services;

/// <summary>
/// Field statistics and area calculation service
/// Ported from AOG_Dev CFieldData.cs
/// </summary>
public class FieldStatisticsService : IFieldStatisticsService
{
    private FieldStatistics _statistics = new();

    /// <summary>
    /// Current field statistics
    /// </summary>
    public FieldStatistics Statistics => _statistics;

    /// <summary>
    /// Event raised when statistics are updated
    /// </summary>
    public event EventHandler<FieldStatistics>? StatisticsUpdated;

    /// <summary>
    /// Total area worked (sum of all section areas) in square meters
    /// </summary>
    public double WorkedAreaSquareMeters
    {
        get => _statistics.WorkedAreaTotal;
        set => _statistics.WorkedAreaTotal = value;
    }

    /// <summary>
    /// User-accumulated distance in meters
    /// </summary>
    public double UserDistance
    {
        get => _statistics.DistanceUser;
        set => _statistics.DistanceUser = value;
    }

    /// <summary>
    /// Boundary area (outer minus inner) in square meters
    /// </summary>
    public double BoundaryAreaSquareMeters => _statistics.AreaBoundaryOuterLessInner;

    /// <summary>
    /// Actual area covered (worked area minus overlap) in square meters
    /// </summary>
    public double ActualAreaCovered => _statistics.ActualAreaCovered;

    /// <summary>
    /// Overlap percentage
    /// </summary>
    public double OverlapPercent => _statistics.OverlapPercent;

    /// <summary>
    /// Update boundary areas from boundary list
    /// </summary>
    /// <param name="boundaryAreas">List of boundary areas (first is outer, rest are inner)</param>
    public void UpdateBoundaryAreas(IList<double> boundaryAreas)
    {
        if (boundaryAreas == null || boundaryAreas.Count == 0)
        {
            _statistics.AreaBoundaryOuterLessInner = 0;
            _statistics.AreaOuterBoundary = 0;
            return;
        }

        // First area is outer boundary
        double outerArea = boundaryAreas[0];
        _statistics.AreaOuterBoundary = outerArea;

        // Subtract inner boundaries (holes)
        double innerArea = 0;
        for (int i = 1; i < boundaryAreas.Count; i++)
        {
            innerArea += boundaryAreas[i];
        }

        _statistics.AreaBoundaryOuterLessInner = outerArea - innerArea;
        StatisticsUpdated?.Invoke(this, _statistics);
    }

    /// <summary>
    /// Update boundary area from field boundary
    /// </summary>
    public void UpdateBoundaryArea(Boundary? boundary)
    {
        if (boundary == null || !boundary.IsValid)
        {
            _statistics.AreaBoundaryOuterLessInner = 0;
            _statistics.AreaOuterBoundary = 0;
            return;
        }

        // Outer boundary area
        double outerArea = boundary.OuterBoundary?.AreaSquareMeters ?? 0;
        _statistics.AreaOuterBoundary = outerArea;

        // Subtract inner boundaries (holes)
        double innerArea = 0;
        foreach (var inner in boundary.InnerBoundaries)
        {
            innerArea += inner.AreaSquareMeters;
        }

        _statistics.AreaBoundaryOuterLessInner = outerArea - innerArea;
        StatisticsUpdated?.Invoke(this, _statistics);
    }

    /// <summary>
    /// Add worked area from sections
    /// </summary>
    public void AddWorkedArea(double areaSquareMeters)
    {
        _statistics.WorkedAreaTotal += areaSquareMeters;
        StatisticsUpdated?.Invoke(this, _statistics);
    }

    /// <summary>
    /// Add user worked area (cumulative tally)
    /// </summary>
    public void AddUserWorkedArea(double areaSquareMeters)
    {
        _statistics.WorkedAreaTotalUser += areaSquareMeters;
        StatisticsUpdated?.Invoke(this, _statistics);
    }

    /// <summary>
    /// Add user distance traveled
    /// </summary>
    public void AddUserDistance(double distanceMeters)
    {
        _statistics.DistanceUser += distanceMeters;
        StatisticsUpdated?.Invoke(this, _statistics);
    }

    /// <summary>
    /// Update actual area covered (for overlap calculations)
    /// </summary>
    public void UpdateActualAreaCovered(double areaSquareMeters)
    {
        _statistics.ActualAreaCovered = areaSquareMeters;
        StatisticsUpdated?.Invoke(this, _statistics);
    }

    /// <summary>
    /// Update overlap percentage
    /// </summary>
    public void UpdateOverlapPercent(double percent)
    {
        _statistics.OverlapPercent = percent;
        StatisticsUpdated?.Invoke(this, _statistics);
    }

    /// <summary>
    /// Get formatted description of field statistics
    /// </summary>
    public string GetDescription(string fieldName, double toolWidth, int numSections, double overlap)
    {
        return $"Field: {fieldName}\n" +
               $"Tool Width: {toolWidth:F1}m, Sections: {numSections}, Overlap: {overlap:F1}%\n" +
               $"Boundary Area: {FormatArea(BoundaryAreaSquareMeters)}\n" +
               $"Worked Area: {FormatArea(WorkedAreaSquareMeters)}\n" +
               $"Remaining: {FormatArea(BoundaryAreaSquareMeters - WorkedAreaSquareMeters)} ({GetRemainingPercent():F1}%)";
    }

    /// <summary>
    /// Get time until finished based on current speed and tool width
    /// </summary>
    public TimeSpan GetTimeTillFinished(double toolWidth, double avgSpeed)
    {
        return _statistics.CalculateTimeTillFinished(toolWidth, avgSpeed);
    }

    /// <summary>
    /// Get work rate in hectares per hour
    /// </summary>
    public double GetWorkRateHectares(double toolWidth, double avgSpeed)
    {
        return _statistics.CalculateWorkRateHectares(toolWidth, avgSpeed);
    }

    /// <summary>
    /// Get work rate in acres per hour
    /// </summary>
    public double GetWorkRateAcres(double toolWidth, double avgSpeed)
    {
        return _statistics.CalculateWorkRateAcres(toolWidth, avgSpeed);
    }

    /// <summary>
    /// Calculate overlap statistics
    /// </summary>
    public void CalculateOverlap()
    {
        if (_statistics.WorkedAreaTotal > 0)
        {
            // This is simplified - actual implementation would need section data
            // to calculate actual vs theoretical coverage
            _statistics.ActualAreaCovered = _statistics.WorkedAreaTotal * 0.95; // Placeholder
            _statistics.OverlapPercent = ((_statistics.WorkedAreaTotal - _statistics.ActualAreaCovered) / _statistics.WorkedAreaTotal) * 100;
        }
        else
        {
            _statistics.ActualAreaCovered = 0;
            _statistics.OverlapPercent = 0;
        }
    }

    /// <summary>
    /// Get remaining area to work in hectares
    /// </summary>
    public double GetRemainingAreaHectares()
    {
        return _statistics.WorkedAreaRemainHectares;
    }

    /// <summary>
    /// Get remaining area percentage
    /// </summary>
    public double GetRemainingPercent()
    {
        return _statistics.CalculateRemainPercentage();
    }

    /// <summary>
    /// Reset all statistics
    /// </summary>
    public void Reset()
    {
        _statistics.Reset();
        StatisticsUpdated?.Invoke(this, _statistics);
    }

    /// <summary>
    /// Format area for display in hectares or acres
    /// </summary>
    public string FormatArea(double squareMeters, bool useMetric = true)
    {
        if (useMetric)
        {
            return (squareMeters / 10000.0).ToString("F2") + " ha";
        }
        else
        {
            return (squareMeters / 4046.86).ToString("F2") + " ac";
        }
    }

    /// <summary>
    /// Format distance for display in meters or feet
    /// </summary>
    public string FormatDistance(double meters, bool useMetric = true)
    {
        if (useMetric)
        {
            return meters.ToString("F1") + " m";
        }
        else
        {
            return (meters * 3.28084).ToString("F1") + " ft";
        }
    }
}

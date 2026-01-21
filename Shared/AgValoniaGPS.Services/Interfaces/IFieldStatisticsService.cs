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

namespace AgValoniaGPS.Services.Interfaces
{
    /// <summary>
    /// Service for managing field statistics and area calculations
    /// </summary>
    public interface IFieldStatisticsService
    {
        /// <summary>
        /// Current field statistics
        /// </summary>
        FieldStatistics Statistics { get; }

        /// <summary>
        /// Event raised when statistics are updated
        /// </summary>
        event EventHandler<FieldStatistics>? StatisticsUpdated;

        /// <summary>
        /// Update boundary areas from boundary list
        /// </summary>
        /// <param name="boundaryAreas">List of boundary areas (first is outer, rest are inner)</param>
        void UpdateBoundaryAreas(IList<double> boundaryAreas);

        /// <summary>
        /// Add worked area from sections
        /// </summary>
        void AddWorkedArea(double areaSquareMeters);

        /// <summary>
        /// Add user worked area (cumulative tally)
        /// </summary>
        void AddUserWorkedArea(double areaSquareMeters);

        /// <summary>
        /// Add user distance traveled
        /// </summary>
        void AddUserDistance(double distanceMeters);

        /// <summary>
        /// Update actual area covered (for overlap calculations)
        /// </summary>
        void UpdateActualAreaCovered(double areaSquareMeters);

        /// <summary>
        /// Update overlap percentage
        /// </summary>
        void UpdateOverlapPercent(double percent);

        /// <summary>
        /// Get formatted description of field statistics
        /// </summary>
        string GetDescription(string fieldName, double toolWidth, int numSections, double overlap);

        /// <summary>
        /// Get time until finished based on current speed and tool width
        /// </summary>
        TimeSpan GetTimeTillFinished(double toolWidth, double avgSpeed);

        /// <summary>
        /// Get work rate in hectares per hour
        /// </summary>
        double GetWorkRateHectares(double toolWidth, double avgSpeed);

        /// <summary>
        /// Get work rate in acres per hour
        /// </summary>
        double GetWorkRateAcres(double toolWidth, double avgSpeed);

        /// <summary>
        /// Reset all statistics
        /// </summary>
        void Reset();
    }
}

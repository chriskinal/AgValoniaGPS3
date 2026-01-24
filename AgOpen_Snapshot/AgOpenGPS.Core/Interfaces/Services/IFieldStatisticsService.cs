using System;
using System.Collections.Generic;
using AgOpenGPS.Core.Models;

namespace AgOpenGPS.Core.Interfaces.Services
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

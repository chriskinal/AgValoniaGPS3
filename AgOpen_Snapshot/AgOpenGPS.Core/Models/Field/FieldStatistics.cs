using System;

namespace AgOpenGPS.Core.Models
{
    /// <summary>
    /// Contains field statistics and area calculations
    /// </summary>
    public class FieldStatistics
    {
        // Unit conversion constants
        public const double MetersToHectares = 0.0001;
        public const double MetersToAcres = 0.000247105;
        public const double MetersToFeet = 3.28084;

        /// <summary>
        /// All section area added up (square meters)
        /// </summary>
        public double WorkedAreaTotal { get; set; }

        /// <summary>
        /// Cumulative tally based on distance and equipment width (square meters)
        /// </summary>
        public double WorkedAreaTotalUser { get; set; }

        /// <summary>
        /// Accumulated user distance (meters)
        /// </summary>
        public double DistanceUser { get; set; }

        /// <summary>
        /// Progress bar percentage
        /// </summary>
        public double BarPercent { get; set; }

        /// <summary>
        /// Overlap percentage
        /// </summary>
        public double OverlapPercent { get; set; }

        /// <summary>
        /// Outside area minus inner boundary areas (square meters)
        /// </summary>
        public double AreaBoundaryOuterLessInner { get; set; }

        /// <summary>
        /// Total done minus overlap (square meters)
        /// </summary>
        public double ActualAreaCovered { get; set; }

        /// <summary>
        /// Inner area of outer boundary (square meters)
        /// </summary>
        public double AreaOuterBoundary { get; set; }

        /// <summary>
        /// User-defined alarm threshold (square meters)
        /// </summary>
        public double UserSquareMetersAlarm { get; set; }

        // Calculated properties - Hectares
        public double AreaBoundaryLessInnersHectares => AreaBoundaryOuterLessInner * MetersToHectares;
        public double WorkedUserHectares => WorkedAreaTotalUser * MetersToHectares;
        public double WorkedHectares => WorkedAreaTotal * MetersToHectares;
        public double WorkedAreaRemainHectares => (AreaBoundaryOuterLessInner - WorkedAreaTotal) * MetersToHectares;
        public double ActualAreaWorkedHectares => ActualAreaCovered * MetersToHectares;
        public double ActualRemainHectares => (AreaBoundaryOuterLessInner - ActualAreaCovered) * MetersToHectares;

        // Calculated properties - Acres
        public double AreaBoundaryLessInnersAcres => AreaBoundaryOuterLessInner * MetersToAcres;
        public double WorkedUserAcres => WorkedAreaTotalUser * MetersToAcres;
        public double WorkedAcres => WorkedAreaTotal * MetersToAcres;
        public double WorkedAreaRemainAcres => (AreaBoundaryOuterLessInner - WorkedAreaTotal) * MetersToAcres;
        public double ActualAreaWorkedAcres => ActualAreaCovered * MetersToAcres;
        public double ActualRemainAcres => (AreaBoundaryOuterLessInner - ActualAreaCovered) * MetersToAcres;

        // Distance properties
        public double DistanceUserMeters => Math.Round(DistanceUser, 1);
        public double DistanceUserFeet => Math.Round(DistanceUser * MetersToFeet, 1);

        /// <summary>
        /// Calculate remaining percentage
        /// </summary>
        public double CalculateRemainPercentage()
        {
            if (AreaBoundaryOuterLessInner > 10)
            {
                BarPercent = ((AreaBoundaryOuterLessInner - WorkedAreaTotal) * 100 / AreaBoundaryOuterLessInner);
                return BarPercent;
            }
            else
            {
                BarPercent = 0;
                return 0;
            }
        }

        /// <summary>
        /// Calculate work rate in hectares per hour
        /// </summary>
        public double CalculateWorkRateHectares(double toolWidth, double avgSpeed)
        {
            return toolWidth * avgSpeed * 0.1;
        }

        /// <summary>
        /// Calculate work rate in acres per hour
        /// </summary>
        public double CalculateWorkRateAcres(double toolWidth, double avgSpeed)
        {
            return toolWidth * avgSpeed * 0.2471;
        }

        /// <summary>
        /// Calculate time until finished
        /// </summary>
        public TimeSpan CalculateTimeTillFinished(double toolWidth, double avgSpeed)
        {
            if (avgSpeed > 2)
            {
                double hours = (AreaBoundaryOuterLessInner - WorkedAreaTotal) * MetersToHectares
                    / (toolWidth * avgSpeed * 0.1);
                return TimeSpan.FromHours(hours);
            }
            return TimeSpan.MaxValue;
        }

        /// <summary>
        /// Reset all statistics
        /// </summary>
        public void Reset()
        {
            WorkedAreaTotal = 0;
            WorkedAreaTotalUser = 0;
            DistanceUser = 0;
            BarPercent = 0;
            OverlapPercent = 0;
            AreaBoundaryOuterLessInner = 0;
            ActualAreaCovered = 0;
            AreaOuterBoundary = 0;
            UserSquareMetersAlarm = 0;
        }
    }
}

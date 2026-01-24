using System;
using System.Collections.Generic;
using System.Text;
using AgOpenGPS.Core.Interfaces.Services;
using AgOpenGPS.Core.Models;

namespace AgOpenGPS.Core.Services
{
    /// <summary>
    /// Implementation of field statistics service
    /// </summary>
    public class FieldStatisticsService : IFieldStatisticsService
    {
        public FieldStatistics Statistics { get; } = new();

        public event EventHandler<FieldStatistics>? StatisticsUpdated;

        public void UpdateBoundaryAreas(IList<double> boundaryAreas)
        {
            if (boundaryAreas == null || boundaryAreas.Count == 0)
            {
                Statistics.AreaOuterBoundary = 0;
                Statistics.AreaBoundaryOuterLessInner = 0;
            }
            else
            {
                Statistics.AreaOuterBoundary = boundaryAreas[0];
                Statistics.AreaBoundaryOuterLessInner = boundaryAreas[0];

                for (int i = 1; i < boundaryAreas.Count; i++)
                {
                    Statistics.AreaBoundaryOuterLessInner -= boundaryAreas[i];
                }
            }

            OnStatisticsUpdated();
        }

        public void AddWorkedArea(double areaSquareMeters)
        {
            Statistics.WorkedAreaTotal += areaSquareMeters;
            Statistics.CalculateRemainPercentage();
            OnStatisticsUpdated();
        }

        public void AddUserWorkedArea(double areaSquareMeters)
        {
            Statistics.WorkedAreaTotalUser += areaSquareMeters;
            OnStatisticsUpdated();
        }

        public void AddUserDistance(double distanceMeters)
        {
            Statistics.DistanceUser += distanceMeters;
            OnStatisticsUpdated();
        }

        public void UpdateActualAreaCovered(double areaSquareMeters)
        {
            Statistics.ActualAreaCovered = areaSquareMeters;
            OnStatisticsUpdated();
        }

        public void UpdateOverlapPercent(double percent)
        {
            Statistics.OverlapPercent = percent;
            OnStatisticsUpdated();
        }

        public string GetDescription(string fieldName, double toolWidth, int numSections, double overlap)
        {
            var sb = new StringBuilder();
            sb.AppendFormat("Field: {0}", fieldName);
            sb.AppendLine();
            sb.AppendFormat("Total Hectares: {0:N2}", Statistics.AreaBoundaryLessInnersHectares);
            sb.AppendLine();
            sb.AppendFormat("Worked Hectares: {0:N2}", Statistics.WorkedHectares);
            sb.AppendLine();
            sb.AppendFormat("Missing Hectares: {0:N2}", Statistics.WorkedAreaRemainHectares);
            sb.AppendLine();
            sb.AppendFormat("Total Acres: {0:N2}", Statistics.AreaBoundaryLessInnersAcres);
            sb.AppendLine();
            sb.AppendFormat("Worked Acres: {0:N2}", Statistics.WorkedAcres);
            sb.AppendLine();
            sb.AppendFormat("Missing Acres: {0:N2}", Statistics.WorkedAreaRemainAcres);
            sb.AppendLine();
            sb.AppendFormat("Tool Width: {0}", toolWidth);
            sb.AppendLine();
            sb.AppendFormat("Sections: {0}", numSections);
            sb.AppendLine();
            sb.AppendFormat("Section Overlap: {0}", overlap);
            sb.AppendLine();
            return sb.ToString();
        }

        public TimeSpan GetTimeTillFinished(double toolWidth, double avgSpeed)
        {
            return Statistics.CalculateTimeTillFinished(toolWidth, avgSpeed);
        }

        public double GetWorkRateHectares(double toolWidth, double avgSpeed)
        {
            return Statistics.CalculateWorkRateHectares(toolWidth, avgSpeed);
        }

        public double GetWorkRateAcres(double toolWidth, double avgSpeed)
        {
            return Statistics.CalculateWorkRateAcres(toolWidth, avgSpeed);
        }

        public void Reset()
        {
            Statistics.Reset();
            OnStatisticsUpdated();
        }

        private void OnStatisticsUpdated()
        {
            StatisticsUpdated?.Invoke(this, Statistics);
        }
    }
}

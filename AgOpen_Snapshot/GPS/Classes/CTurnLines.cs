using System;
using System.Collections.Generic;
using AgOpenGPS.Core.Models.Base;
using AgOpenGPS.Core.Services.Geometry;

namespace AgOpenGPS
{
    /// <summary>
    /// WinForms wrapper for turn line geometry.
    /// Delegates all calculations to Core TurnLineService.
    /// </summary>
    public partial class CBoundaryList
    {
        private static readonly TurnLineService _coreTurnLineService = new TurnLineService();

        public void CalculateTurnHeadings()
        {
            // Convert WinForms turn line to Core format
            List<Vec3> coreTurnLine = new List<Vec3>(turnLine.Count);
            foreach (vec3 point in turnLine)
            {
                coreTurnLine.Add(new Vec3(point.easting, point.northing, point.heading));
            }

            // Delegate to Core service
            List<Vec3> coreResult = _coreTurnLineService.CalculateHeadings(coreTurnLine);

            // Convert back to WinForms format
            turnLine.Clear();
            foreach (Vec3 corePoint in coreResult)
            {
                turnLine.Add(new vec3(corePoint.Easting, corePoint.Northing, corePoint.Heading));
            }
        }

        public void FixTurnLine(double totalHeadWidth, double spacing)
        {
            // Convert WinForms turn line to Core format
            List<Vec3> coreTurnLine = new List<Vec3>(turnLine.Count);
            foreach (vec3 point in turnLine)
            {
                coreTurnLine.Add(new Vec3(point.easting, point.northing, point.heading));
            }

            // Convert WinForms fence line to Core format
            List<Vec3> coreFenceLine = new List<Vec3>(fenceLine.Count);
            foreach (vec3 point in fenceLine)
            {
                coreFenceLine.Add(new Vec3(point.easting, point.northing, point.heading));
            }

            // Delegate to Core service
            List<Vec3> coreResult = _coreTurnLineService.FixSpacing(coreTurnLine, coreFenceLine, totalHeadWidth, spacing);

            // Convert back to WinForms format
            turnLine.Clear();
            foreach (Vec3 corePoint in coreResult)
            {
                turnLine.Add(new vec3(corePoint.Easting, corePoint.Northing, corePoint.Heading));
            }
        }
    }
}
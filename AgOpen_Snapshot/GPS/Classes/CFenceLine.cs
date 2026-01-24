using System;
using System.Collections.Generic;
using AgOpenGPS.Core.Models.Base;
using AgOpenGPS.Core.Services.Geometry;

namespace AgOpenGPS
{
    /// <summary>
    /// WinForms wrapper for fence line geometry.
    /// Delegates all calculations to Core FenceLineService.
    /// </summary>
    public partial class CBoundaryList
    {
        private static readonly FenceLineService _coreService = new FenceLineService();

        //area variable
        public double area;

        //boundary variables
        public bool isDriveThru;

        public void CalculateFenceLineHeadings()
        {
            // Convert WinForms fence line to Core format
            List<Vec3> coreFenceLine = new List<Vec3>(fenceLine.Count);
            foreach (vec3 point in fenceLine)
            {
                coreFenceLine.Add(new Vec3(point.easting, point.northing, point.heading));
            }

            // Delegate to Core service
            List<Vec3> coreResult = _coreService.CalculateHeadings(coreFenceLine);

            // Convert back to WinForms format
            fenceLine.Clear();
            foreach (Vec3 corePoint in coreResult)
            {
                fenceLine.Add(new vec3(corePoint.Easting, corePoint.Northing, corePoint.Heading));
            }
        }

        public void FixFenceLine(int bndNum)
        {
            // Convert WinForms fence line to Core format
            List<Vec3> coreFenceLine = new List<Vec3>(fenceLine.Count);
            foreach (vec3 point in fenceLine)
            {
                coreFenceLine.Add(new Vec3(point.easting, point.northing, point.heading));
            }

            // Delegate to Core service
            List<Vec2> coreEar;
            List<Vec3> coreResult = _coreService.FixSpacing(coreFenceLine, area, bndNum, out coreEar);

            // Convert back to WinForms format
            fenceLine.Clear();
            foreach (Vec3 corePoint in coreResult)
            {
                fenceLine.Add(new vec3(corePoint.Easting, corePoint.Northing, corePoint.Heading));
            }

            // Convert ear clipping line
            fenceLineEar?.Clear();
            foreach (Vec2 corePoint in coreEar)
            {
                fenceLineEar.Add(new vec2(corePoint.Easting, corePoint.Northing));
            }
        }

        public void ReverseWinding()
        {
            // Convert WinForms fence line to Core format
            List<Vec3> coreFenceLine = new List<Vec3>(fenceLine.Count);
            foreach (vec3 point in fenceLine)
            {
                coreFenceLine.Add(new Vec3(point.easting, point.northing, point.heading));
            }

            // Delegate to Core service
            List<Vec3> coreResult = _coreService.ReverseWinding(coreFenceLine);

            // Convert back to WinForms format
            fenceLine.Clear();
            foreach (Vec3 corePoint in coreResult)
            {
                fenceLine.Add(new vec3(corePoint.Easting, corePoint.Northing, corePoint.Heading));
            }
        }

        //obvious
        public bool CalculateFenceArea(int idx)
        {
            if (fenceLine.Count < 1) return false;

            // Convert WinForms fence line to Core format
            List<Vec3> coreFenceLine = new List<Vec3>(fenceLine.Count);
            foreach (vec3 point in fenceLine)
            {
                coreFenceLine.Add(new Vec3(point.easting, point.northing, point.heading));
            }

            // Delegate to Core service
            double calculatedArea;
            List<Vec3> coreResult = _coreService.CalculateAreaAndFixWinding(coreFenceLine, idx, out calculatedArea);

            // Update area field
            area = calculatedArea;

            // Convert back to WinForms format
            fenceLine.Clear();
            foreach (Vec3 corePoint in coreResult)
            {
                fenceLine.Add(new vec3(corePoint.Easting, corePoint.Northing, corePoint.Heading));
            }

            // Return whether original winding was clockwise (before potential reversal)
            // Core service handles the reversal internally
            return true;
        }
    }
}
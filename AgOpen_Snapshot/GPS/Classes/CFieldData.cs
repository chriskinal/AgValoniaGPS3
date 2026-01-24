using System;
using System.Collections.Generic;
using System.Text;
using AgOpenGPS.Core.Interfaces.Services;
using AgOpenGPS.Core.Models;
using AgOpenGPS.Core.Services;

namespace AgOpenGPS
{
    /// <summary>
    /// Wrapper around Core FieldStatisticsService for backward compatibility
    /// </summary>
    public class CFieldData
    {
        private readonly FormGPS mf;
        private readonly IFieldStatisticsService _service;

        // Direct access to underlying statistics for backward compatibility
        private FieldStatistics Stats => _service.Statistics;

        //all the section area added up;
        public double workedAreaTotal
        {
            get => Stats.WorkedAreaTotal;
            set => Stats.WorkedAreaTotal = value;
        }

        //just a cumulative tally based on distance and eq width.
        public double workedAreaTotalUser
        {
            get => Stats.WorkedAreaTotalUser;
            set => Stats.WorkedAreaTotalUser = value;
        }

        //accumulated user distance
        public double distanceUser
        {
            get => Stats.DistanceUser;
            set => Stats.DistanceUser = value;
        }

        public double barPercent
        {
            get => Stats.BarPercent;
            set => Stats.BarPercent = value;
        }

        public double overlapPercent
        {
            get => Stats.OverlapPercent;
            set => Stats.OverlapPercent = value;
        }

        //Outside area minus inner boundaries areas (m)
        public double areaBoundaryOuterLessInner
        {
            get => Stats.AreaBoundaryOuterLessInner;
            set => Stats.AreaBoundaryOuterLessInner = value;
        }

        //used for overlap calcs - total done minus overlap
        public double actualAreaCovered
        {
            get => Stats.ActualAreaCovered;
            set => Stats.ActualAreaCovered = value;
        }

        //Inner area of outer boundary(m)
        public double areaOuterBoundary
        {
            get => Stats.AreaOuterBoundary;
            set => Stats.AreaOuterBoundary = value;
        }

        //not really used - but if needed
        public double userSquareMetersAlarm
        {
            get => Stats.UserSquareMetersAlarm;
            set => Stats.UserSquareMetersAlarm = value;
        }

        //Area inside Boundary less inside boundary areas
        public string AreaBoundaryLessInnersHectares => (areaBoundaryOuterLessInner * glm.m2ha).ToString("N2");

        public string AreaBoundaryLessInnersAcres => (areaBoundaryOuterLessInner * glm.m2ac).ToString("N2");

        //USer tally string
        public string WorkedUserHectares => (workedAreaTotalUser * glm.m2ha).ToString("N2");

        //user tally string
        public string WorkedUserAcres => (workedAreaTotalUser * glm.m2ac).ToString("N2");

        //String of Area worked
        public string WorkedAcres => (workedAreaTotal * 0.000247105).ToString("N2");

        public string WorkedHectares => (workedAreaTotal * 0.0001).ToString("N2");

        //User Distance strings
        public string DistanceUserMeters => Convert.ToString(Math.Round(distanceUser, 1));

        public string DistanceUserFeet => Convert.ToString(Math.Round((distanceUser * glm.m2ft), 1));

        //remaining area to be worked
        public string WorkedAreaRemainHectares => ((areaBoundaryOuterLessInner - workedAreaTotal) * glm.m2ha).ToString("N2");

        public string WorkedAreaRemainAcres => ((areaBoundaryOuterLessInner - workedAreaTotal) * glm.m2ac).ToString("N2");

        public string WorkedAreaRemainPercentage
        {
            get
            {
                if (areaBoundaryOuterLessInner > 10)
                {
                    barPercent = ((areaBoundaryOuterLessInner - workedAreaTotal) * 100 / areaBoundaryOuterLessInner);
                    return barPercent.ToString("N1") + "%";
                }
                else
                {
                    barPercent = 0;
                    return "0%";
                }
            }
        }

        //overlap strings
        public string ActualAreaWorkedHectares => (actualAreaCovered * glm.m2ha).ToString("N2");
        public string ActualAreaWorkedAcres => (actualAreaCovered * glm.m2ac).ToString("N2");

        public string ActualRemainHectares => ((areaBoundaryOuterLessInner - actualAreaCovered) * glm.m2ha).ToString("N2");
        public string ActualRemainAcres => ((areaBoundaryOuterLessInner - actualAreaCovered) * glm.m2ac).ToString("N2");

        public string ActualOverlapPercent => overlapPercent.ToString("N1") + "% ";

        public string TimeTillFinished
        {
            get
            {
                if (mf.avgSpeed > 2)
                {
                    TimeSpan timeSpan = TimeSpan.FromHours(((areaBoundaryOuterLessInner - workedAreaTotal) * glm.m2ha
                        / (mf.tool.width * mf.avgSpeed * 0.1)));
                    return timeSpan.Hours.ToString("00:") + timeSpan.Minutes.ToString("00") + '"';
                }
                else return "\u221E Hrs";
            }
        }

        public string WorkRateHectares => (mf.tool.width * mf.avgSpeed * 0.1).ToString("N1") + " ha/hr";
        public string WorkRateAcres => (mf.tool.width * mf.avgSpeed * 0.2471).ToString("N1") + " ac/hr";

        /// <summary>
        /// Gets the underlying service for direct access
        /// </summary>
        public IFieldStatisticsService Service => _service;

        //constructor
        public CFieldData(FormGPS _f)
        {
            mf = _f;
            _service = new FieldStatisticsService();
            workedAreaTotal = 0;
            workedAreaTotalUser = 0;
            userSquareMetersAlarm = 0;
        }

        public void UpdateFieldBoundaryGUIAreas()
        {
            // Build list of boundary areas and delegate to Core service
            var boundaryAreas = new List<double>();
            if (mf.bnd.bndList.Count > 0)
            {
                for (int i = 0; i < mf.bnd.bndList.Count; i++)
                {
                    boundaryAreas.Add(mf.bnd.bndList[i].area);
                }
            }
            _service.UpdateBoundaryAreas(boundaryAreas);
        }

        public String GetDescription()
        {
            return _service.GetDescription(mf.displayFieldName, mf.tool.width, mf.tool.numOfSections, mf.tool.overlap);
        }
    }
}
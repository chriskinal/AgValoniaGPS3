using System;
using System.Collections.Generic;
using System.Linq;
using AgOpenGPS.Core.Models.Base;
using AgOpenGPS.Core.Models.Headland;
using AgOpenGPS.Core.Services.Headland;

namespace AgOpenGPS
{
    public partial class CBoundary
    {
        private readonly HeadlandDetectionService _headlandDetectionService = new HeadlandDetectionService();

        public bool isHeadlandOn;

        public bool isToolInHeadland,
            isToolOuterPointsInHeadland, isSectionControlledByHeadland;

        public vec2? HeadlandNearestPoint { get; private set; } = null;
        public double? HeadlandDistance { get; private set; } = null;

        public void SetHydPosition()
        {
            if (mf.vehicle.isHydLiftOn && mf.avgSpeed > 0.2 && !mf.isReverse)
            {
                if (isToolInHeadland)
                {
                    mf.p_239.pgn[mf.p_239.hydLift] = 2;
                    if (mf.sounds.isHydLiftChange != isToolInHeadland)
                    {
                        if (mf.sounds.isHydLiftSoundOn) mf.sounds.sndHydLiftUp.Play();
                        mf.sounds.isHydLiftChange = isToolInHeadland;
                    }
                }
                else
                {
                    mf.p_239.pgn[mf.p_239.hydLift] = 1;
                    if (mf.sounds.isHydLiftChange != isToolInHeadland)
                    {
                        if (mf.sounds.isHydLiftSoundOn) mf.sounds.sndHydLiftDn.Play();
                        mf.sounds.isHydLiftChange = isToolInHeadland;
                    }
                }
            }
        }

        public void WhereAreToolCorners()
        {
            if (bndList.Count > 0 && bndList[0].hdLine.Count > 0)
            {
                // Build input DTO
                var input = BuildHeadlandDetectionInput();

                // Delegate to Core service
                var output = _headlandDetectionService.DetectHeadland(input);

                // Map output back to WinForms state
                mf.tool.isLeftSideInHeadland = output.IsLeftSideInHeadland;
                mf.tool.isRightSideInHeadland = output.IsRightSideInHeadland;
                isToolOuterPointsInHeadland = output.IsToolOuterPointsInHeadland;

                for (int j = 0; j < mf.tool.numOfSections && j < output.SectionStatus.Count; j++)
                {
                    mf.section[j].isInHeadlandArea = output.SectionStatus[j].IsInHeadlandArea;
                }
            }
        }

        public void WhereAreToolLookOnPoints()
        {
            if (bndList.Count > 0 && bndList[0].hdLine.Count > 0)
            {
                // Build input DTO
                var input = BuildHeadlandDetectionInput();

                // Delegate to Core service
                var output = _headlandDetectionService.DetectHeadland(input);

                // Map output back to WinForms state
                for (int j = 0; j < mf.tool.numOfSections && j < output.SectionStatus.Count; j++)
                {
                    mf.section[j].isLookOnInHeadland = output.SectionStatus[j].IsLookOnInHeadland;
                }
            }
        }

        public bool IsPointInsideHeadArea(vec2 pt)
        {
            // Delegate to Core service
            var boundaries = BuildBoundaryList();
            return _headlandDetectionService.IsPointInsideHeadArea(new Vec2(pt.easting, pt.northing), boundaries);
        }
        public void CheckHeadlandProximity()
        {
            if (!isHeadlandOn || bndList.Count == 0 || bndList[0].hdLine.Count < 2)
            {
                HeadlandNearestPoint = null;
                HeadlandDistance = null;
                return;
            }

            // Build input DTO
            var input = BuildHeadlandDetectionInput();

            // Delegate to Core service
            var output = _headlandDetectionService.DetectHeadland(input);

            // Map output back to WinForms state
            if (output.HeadlandNearestPoint.HasValue)
            {
                HeadlandNearestPoint = new vec2(output.HeadlandNearestPoint.Value.Easting, output.HeadlandNearestPoint.Value.Northing);
            }
            else
            {
                HeadlandNearestPoint = null;
            }

            HeadlandDistance = output.HeadlandDistance;

            // Handle warning sound
            if (output.ShouldTriggerWarning && mf.isHeadlandDistanceOn)
            {
                if (!mf.sounds.isBoundAlarming)
                {
                    mf.sounds.sndHeadland.Play();
                    mf.sounds.isBoundAlarming = true;
                }
            }
            else
            {
                mf.sounds.isBoundAlarming = false;
            }
        }

        private List<BoundaryData> BuildBoundaryList()
        {
            var boundaries = new List<BoundaryData>();

            foreach (var bnd in bndList)
            {
                var boundaryData = new BoundaryData
                {
                    IsDriveThru = bnd.isDriveThru,
                    HeadlandLine = bnd.hdLine.Select(v => new Vec3(v.easting, v.northing, v.heading)).ToList()
                };
                boundaries.Add(boundaryData);
            }

            return boundaries;
        }

        private HeadlandDetectionInput BuildHeadlandDetectionInput()
        {
            var input = new HeadlandDetectionInput
            {
                Boundaries = BuildBoundaryList(),
                VehiclePosition = new Vec3(mf.toolPivotPos.easting, mf.toolPivotPos.northing, mf.toolPivotPos.heading),
                IsHeadlandOn = isHeadlandOn,
                LookAhead = new LookAheadConfig
                {
                    LookAheadDistanceOnPixelsLeft = mf.tool.lookAheadDistanceOnPixelsLeft,
                    LookAheadDistanceOnPixelsRight = mf.tool.lookAheadDistanceOnPixelsRight,
                    TotalWidth = mf.tool.rpWidth
                }
            };

            // Build section corner data
            for (int j = 0; j < mf.tool.numOfSections; j++)
            {
                var section = new SectionCornerData
                {
                    LeftPoint = new Vec2(mf.section[j].leftPoint.easting, mf.section[j].leftPoint.northing),
                    RightPoint = new Vec2(mf.section[j].rightPoint.easting, mf.section[j].rightPoint.northing),
                    SectionWidth = mf.section[j].rpSectionWidth
                };
                input.Sections.Add(section);
            }

            return input;
        }

    }
}
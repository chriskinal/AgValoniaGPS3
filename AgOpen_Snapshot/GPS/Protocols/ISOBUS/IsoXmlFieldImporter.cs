using AgOpenGPS.Core.Models;
using AgOpenGPS.Core.Models.Base;
using AgOpenGPS.Core.Models.IsoXml;
using AgOpenGPS.Core.Services.IsoXml;
using AgOpenGPS.Core;
using System.Collections.Generic;
using System.Xml;

namespace AgOpenGPS
{
    /// <summary>
    /// WinForms adapter for importing field data from an ISO XML file.
    /// Uses Core IsoXmlParserHelpers and converts Core types to WinForms types.
    /// This class is stateless and does not hold any references to the application state.
    /// </summary>
    public class IsoXmlFieldImporter
    {
        private readonly XmlNodeList _fieldParts;
        private readonly ApplicationModel _appModel;

        public IsoXmlFieldImporter(XmlNodeList fieldParts, ApplicationModel appModel)
        {
            _fieldParts = fieldParts;
            _appModel = appModel;
        }

        public bool TryGetOrigin(out Wgs84 origin) =>
            Core.Services.IsoXml.IsoXmlParserHelpers.TryExtractOrigin(_fieldParts, out origin);

        public List<CBoundaryList> GetBoundaries()
        {
            var coreBoundaries = Core.Services.IsoXml.IsoXmlParserHelpers.ParseBoundaries(_fieldParts, _appModel.LocalPlane);
            var winformsBoundaries = new List<CBoundaryList>();

            foreach (var coreBoundary in coreBoundaries)
            {
                var boundary = new CBoundaryList();
                foreach (var coreVec in coreBoundary.FenceLine)
                {
                    // Implicit conversion from Vec3 to vec3
                    boundary.fenceLine.Add(coreVec);
                }
                winformsBoundaries.Add(boundary);
            }

            return winformsBoundaries;
        }

        public List<vec3> GetHeadland()
        {
            var coreHeadland = Core.Services.IsoXml.IsoXmlParserHelpers.ParseHeadland(_fieldParts, _appModel.LocalPlane);
            var winformsHeadland = new List<vec3>();

            foreach (var coreVec in coreHeadland)
            {
                // Implicit conversion from Vec3 to vec3
                winformsHeadland.Add(coreVec);
            }

            return winformsHeadland;
        }

        public List<CTrk> GetGuidanceLines()
        {
            var coreTracks = Core.Services.IsoXml.IsoXmlParserHelpers.ParseAllGuidanceLines(_fieldParts, _appModel.LocalPlane);
            var winformsTracks = new List<CTrk>();

            foreach (var coreTrack in coreTracks)
            {
                var track = new CTrk
                {
                    name = coreTrack.Name,
                    heading = coreTrack.Heading,
                    mode = ConvertTrackMode(coreTrack.Mode),
                    isVisible = coreTrack.IsVisible,
                    nudgeDistance = coreTrack.NudgeDistance,
                    ptA = coreTrack.PtA,  // Implicit conversion from Vec2 to vec2
                    ptB = coreTrack.PtB   // Implicit conversion from Vec2 to vec2
                };

                // Convert curve points
                foreach (var coreVec in coreTrack.CurvePoints)
                {
                    track.curvePts.Add(coreVec);  // Implicit conversion from Vec3 to vec3
                }

                winformsTracks.Add(track);
            }

            return winformsTracks;
        }

        private TrackMode ConvertTrackMode(IsoXmlTrackMode coreMode)
        {
            return (TrackMode)(int)coreMode;  // Enum values match
        }
    }
}

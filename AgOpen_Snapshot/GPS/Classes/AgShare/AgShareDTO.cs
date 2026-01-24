using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using AgOpenGPS.Core.Models;
using AgOpenGPS.Core.Models.AgShare;

namespace AgOpenGPS
{
    // DTO for Downloading and Uploading Fields From and to AgShare
    // WinForms-specific DTO that uses vec3 and CTrk
    public class FieldSnapshot
    {
        public string FieldName { get; set; }
        public string FieldDirectory { get; set; }
        public Guid FieldId { get; set; }
        public double OriginLat { get; set; }
        public double OriginLon { get; set; }
        public double Convergence { get; set; }
        public List<List<vec3>> Boundaries { get; set; }
        public List<CTrk> Tracks { get; set; }
        public LocalPlane Converter { get; set; }
    }

    // Note: CoordinateDto, AgShareFieldDto, AbLineUploadDto, and AgShareGetOwnFieldDto
    // have been moved to AgOpenGPS.Core.Models.AgShare for shared use
}




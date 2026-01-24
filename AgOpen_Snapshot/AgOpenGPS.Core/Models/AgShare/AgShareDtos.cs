using System;
using System.Collections.Generic;
using System.Linq;
using AgOpenGPS.Core.Models.Base;

namespace AgOpenGPS.Core.Models.AgShare
{
    /// <summary>
    /// DTO representing a coordinate point in WGS84
    /// </summary>
    public class CoordinateDto
    {
        public double Latitude { get; set; }
        public double Longitude { get; set; }
    }

    /// <summary>
    /// DTO for downloading field data from AgShare
    /// </summary>
    public class AgShareFieldDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public bool IsPublic { get; set; }
        public DateTimeOffset CreatedAt { get; set; }

        public double Latitude { get; set; }
        public double Longitude { get; set; }

        public List<List<CoordinateDto>> Boundaries { get; set; }
        public List<AbLineUploadDto> AbLines { get; set; }
    }

    /// <summary>
    /// DTO for AB line upload/download
    /// </summary>
    public class AbLineUploadDto
    {
        public string Name { get; set; }
        public string Type { get; set; }
        public List<CoordinateDto> Coords { get; set; }
    }

    /// <summary>
    /// DTO for retrieving user's own fields from AgShare
    /// </summary>
    public class AgShareGetOwnFieldDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public List<CoordinateDto> OuterBoundary { get; set; }

        /// <summary>
        /// Calculates approximate area of the outer boundary in hectares
        /// </summary>
        public double AreaHa
        {
            get
            {
                if (OuterBoundary == null || OuterBoundary.Count < 3)
                    return 0;

                var coords = OuterBoundary.Select(c => (c.Latitude, c.Longitude)).ToList();
                return GeoCalculations.CalculateAreaInHectares(coords);
            }
        }
    }
}

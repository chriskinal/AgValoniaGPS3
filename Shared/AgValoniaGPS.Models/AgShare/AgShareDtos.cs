// AgValoniaGPS
// Copyright (C) 2024-2025 AgValoniaGPS Contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program. If not, see <https://www.gnu.org/licenses/>.

using System;
using System.Collections.Generic;
using System.Linq;
using AgValoniaGPS.Models.Base;

namespace AgValoniaGPS.Models.AgShare
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

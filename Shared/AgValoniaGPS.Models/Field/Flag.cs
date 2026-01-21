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

﻿namespace AgValoniaGPS.Models
{
    public enum FlagColor { Red = 0, Green = 1, Yellow = 2 }

    public class Flag
    {
        public Flag(Wgs84 wgs84, GeoCoord geoCoord, GeoDir heading, FlagColor flagColor, int uniqueNumber, string notes)
        {
            Wgs84 = wgs84;
            GeoCoordDir = new GeoCoordDir(geoCoord, heading);
            FlagColor = flagColor;
            UniqueNumber = uniqueNumber;
            Notes = notes;
        }

        public Wgs84 Wgs84 { get; }
        public GeoCoordDir GeoCoordDir { get; }

        public double Latitude => Wgs84.Latitude;
        public double Longitude => Wgs84.Longitude;
        public GeoCoord GeoCoord => GeoCoordDir.Coord;
        public GeoDir Heading => GeoCoordDir.Direction;
        public double Northing => GeoCoord.Northing;
        public double Easting => GeoCoord.Easting;

        public FlagColor FlagColor { get; }
        public int UniqueNumber { get; set; }

        public string Notes { get; set; }

    }
}
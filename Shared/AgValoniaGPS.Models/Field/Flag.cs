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

using CommunityToolkit.Mvvm.ComponentModel;

namespace AgValoniaGPS.Models
{
    public enum FlagColor
    {
        Red = 0, Green = 1, Yellow = 2, Blue = 3, Orange = 4,
        Purple = 5, Cyan = 6, Pink = 7, White = 8, Black = 9
    }

    public class Flag : ObservableObject
    {
        private FlagColor _flagColor;
        private string _name = "";
        private string _notes = "";

        public Flag(Wgs84 wgs84, GeoCoord geoCoord, GeoDir heading, FlagColor flagColor, int uniqueNumber, string notes)
        {
            Wgs84 = wgs84;
            GeoCoordDir = new GeoCoordDir(geoCoord, heading);
            _flagColor = flagColor;
            UniqueNumber = uniqueNumber;
            _notes = notes;
            _name = $"Flag {uniqueNumber}";
        }

        public Flag(double easting, double northing, FlagColor color, int id, string name)
        {
            Wgs84 = new Wgs84(0, 0);
            GeoCoordDir = new GeoCoordDir(new GeoCoord(northing, easting), new GeoDir(0));
            _flagColor = color;
            UniqueNumber = id;
            _notes = "";
            _name = name;
        }

        public Wgs84 Wgs84 { get; }
        public GeoCoordDir GeoCoordDir { get; }

        public double Latitude => Wgs84.Latitude;
        public double Longitude => Wgs84.Longitude;
        public GeoCoord GeoCoord => GeoCoordDir.Coord;
        public GeoDir Heading => GeoCoordDir.Direction;
        public double Northing => GeoCoord.Northing;
        public double Easting => GeoCoord.Easting;

        public FlagColor FlagColor
        {
            get => _flagColor;
            set => SetProperty(ref _flagColor, value);
        }

        public int UniqueNumber { get; set; }

        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        public string Notes
        {
            get => _notes;
            set => SetProperty(ref _notes, value);
        }

        public static string ColorToHex(FlagColor color) => color switch
        {
            FlagColor.Red => "#FF0000",
            FlagColor.Green => "#00CC00",
            FlagColor.Yellow => "#FFCC00",
            FlagColor.Blue => "#2080E0",
            FlagColor.Orange => "#FF8800",
            FlagColor.Purple => "#9933CC",
            FlagColor.Cyan => "#00BBCC",
            FlagColor.Pink => "#FF66AA",
            FlagColor.White => "#FFFFFF",
            FlagColor.Black => "#333333",
            _ => "#FF0000"
        };
    }
}

using AgOpenGPS.Core.Models;
using System;

namespace AgOpenGPS
{
    /// <summary>
    /// Wrapper around Core Flag model for backward compatibility
    /// </summary>
    public class CFlag
    {
        private readonly Flag _flag;

        //WGS84 Lat Long
        public double latitude => _flag.Latitude;

        public double longitude => _flag.Longitude;

        public double heading => _flag.Heading.AngleInDegrees;

        //color of the flag - 0 is red, 1 is green, 2 is purple
        public int color => (int)_flag.FlagColor;

        public int ID
        {
            get => _flag.UniqueNumber;
            set => _flag.UniqueNumber = value;
        }

        public string notes
        {
            get => _flag.Notes;
            set => _flag.Notes = value;
        }

        //constructor
        public CFlag(double _lati, double _longi, double _easting, double _northing, double _heading, int _color, int _ID, string _notes = "Notes")
        {
            var wgs84 = new Wgs84(Math.Round(_lati, 7), Math.Round(_longi, 7));
            var geoCoord = new GeoCoord(Math.Round(_northing, 7), Math.Round(_easting, 7));
            var geoDir = new GeoDir(Math.Round(_heading, 7) * Math.PI / 180.0); // Convert degrees to radians
            var flagColor = (FlagColor)_color;

            _flag = new Flag(wgs84, geoCoord, geoDir, flagColor, _ID, _notes);
        }

        /// <summary>
        /// Gets the underlying Core Flag model
        /// </summary>
        public Flag CoreFlag => _flag;

        public GeoCoord GeoCoord => _flag.GeoCoord;
        public double northing => _flag.Northing;
        public double easting => _flag.Easting;

    }
}
using AgOpenGPS.Core.Models.Base;

namespace AgOpenGPS.Core.Models.GPS
{
    /// <summary>
    /// Complete GPS data package produced by the GPS simulator.
    /// Contains all fields that would normally come from a real GPS unit.
    /// </summary>
    public class SimulatedGpsData
    {
        /// <summary>
        /// Current WGS84 position (latitude/longitude)
        /// </summary>
        public Wgs84 Position { get; set; }

        /// <summary>
        /// Current position in local plane coordinates (northing/easting)
        /// </summary>
        public GeoCoord LocalPosition { get; set; }

        /// <summary>
        /// True heading in radians (0 = North, increases clockwise)
        /// </summary>
        public double HeadingRadians { get; set; }

        /// <summary>
        /// True heading in degrees (0 = North, increases clockwise)
        /// </summary>
        public double HeadingDegrees { get; set; }

        /// <summary>
        /// Speed in km/h
        /// </summary>
        public double SpeedKmh { get; set; }

        /// <summary>
        /// Current steer angle in degrees (smoothed/averaged)
        /// </summary>
        public double SteerAngleDegrees { get; set; }

        /// <summary>
        /// Horizontal dilution of precision (simulated)
        /// </summary>
        public double Hdop { get; set; }

        /// <summary>
        /// Altitude in meters (simulated based on lat/lon)
        /// </summary>
        public double Altitude { get; set; }

        /// <summary>
        /// Number of satellites tracked (simulated)
        /// </summary>
        public int SatellitesTracked { get; set; }

        /// <summary>
        /// Instantaneous step distance used for this update (meters)
        /// </summary>
        public double StepDistance { get; set; }

        public SimulatedGpsData()
        {
            Position = new Wgs84();
            LocalPosition = new GeoCoord();
        }
    }
}

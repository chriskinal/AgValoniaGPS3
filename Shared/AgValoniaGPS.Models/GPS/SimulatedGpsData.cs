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

using AgValoniaGPS.Models.Base;

namespace AgValoniaGPS.Models.GPS
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

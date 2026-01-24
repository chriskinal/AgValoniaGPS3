using System;

namespace AgOpenGPS.Core.Models
{
    // An instance of LocalPlane defines the origin and the meaning of a local coordinate
    // system that uses Northing and Easting coordinates.
    public class LocalPlane
    {
        private SharedFieldProperties _sharedFieldProperties;
        private double _metersPerDegreeLat;

        public LocalPlane(Wgs84 origin, SharedFieldProperties sharedFieldProperties)
        {
            Origin = origin;
            _sharedFieldProperties = sharedFieldProperties;
            SetMetersPerDegreeLat();
        }

        public Wgs84 Origin { get; }

        public GeoCoord ConvertWgs84ToGeoCoord(Wgs84 latLon)
        {
            return new GeoCoord(
                (latLon.Latitude - Origin.Latitude) * _metersPerDegreeLat,
                (latLon.Longitude - Origin.Longitude) * MetersPerDegreeLon(latLon.Latitude)
            );
        }

        public Wgs84 ConvertGeoCoordToWgs84(GeoCoord geoCoord)
        {
            geoCoord += _sharedFieldProperties.DriftCompensation;
            double lat = Origin.Latitude + (geoCoord.Northing / _metersPerDegreeLat);
            double lon = Origin.Longitude + (geoCoord.Easting / MetersPerDegreeLon(lat));
            return new Wgs84(lat, lon);
        }

        // see https://en.wikipedia.org/wiki/Geographic_coordinate_system#Latitude_and_longitude
        private void SetMetersPerDegreeLat()
        {
            double originLatInRad = Units.DegreesToRadians(Origin.Latitude);
            _metersPerDegreeLat = 111132.92
                - 559.82 * Math.Cos(2.0 * originLatInRad)
                + 1.175 * Math.Cos(4.0 * originLatInRad)
                - 0.0023 * Math.Cos(6.0 * originLatInRad);
            // meters per degree longitude depends on latitude
            // so we must calculate it for each point separately in ConvertWgs84ToGeoCoord and ConvertGeoCoordToWgs84
        }

        private double MetersPerDegreeLon(double lat)
        {
            double latRad = Units.DegreesToRadians(lat);
            return
                111412.84 * Math.Cos(latRad)
                - 93.5 * Math.Cos(3.0 * latRad)
                + 0.118 * Math.Cos(5.0 * latRad);
        }

    }
}
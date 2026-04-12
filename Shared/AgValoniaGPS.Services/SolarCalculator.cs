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

namespace AgValoniaGPS.Services;

/// <summary>
/// Computes sunrise and sunset times from GPS latitude/longitude and current date.
/// Based on NOAA simplified solar position algorithms.
/// </summary>
public static class SolarCalculator
{
    /// <summary>
    /// Returns true if the sun is up at the given lat/lon/time.
    /// dateTime should be in UTC not local time
    /// </summary>
    public static bool IsDay(double latitude, double longitude, DateTime dateTime)
    {
        var (sunrise, sunset) = GetSunTimes(latitude, longitude, dateTime.Date);
        return dateTime >= sunrise && dateTime <= sunset;
    }

    /// <summary>
    /// Returns sunrise and sunset in local time for a given date.
    /// </summary>
    /// <returns>
    /// DateTime.MaxValue for both if sun never rises (polar night).
    /// DateTime.MinValue for both if sun never sets (midnight sun).
    /// </returns>
    public static (DateTime sunriseUtc, DateTime sunsetUtc) GetSunTimes(double latitude, double longitude, DateTime dateUtc)
    {
        double lngHour = longitude / 15.0;
        int N = dateUtc.DayOfYear;

        double tRise = N + ((6 - lngHour) / 24.0);
        double tSet  = tRise + 0.5;

        double MRise = (0.9856 * tRise) - 3.289;
        double MSet  = MRise + 0.4928;

        double LRise = MRise + 1.916 * Math.Sin(DegToRad(MRise)) + 0.020 * Math.Sin(DegToRad(2 * MRise)) + 282.634;
        double LSet  = MSet  + 1.916 * Math.Sin(DegToRad(MSet))  + 0.020 * Math.Sin(DegToRad(2 * MSet))  + 282.634;
        LRise = NormalizeAngle(LRise);
        LSet  = NormalizeAngle(LSet);

        double RARise = RadToDeg(Math.Atan(0.91764 * Math.Tan(DegToRad(LRise))));
        double RASet  = RadToDeg(Math.Atan(0.91764 * Math.Tan(DegToRad(LSet))));
        RARise = NormalizeAngle(RARise);
        RASet  = NormalizeAngle(RASet);

        RARise += Math.Floor(LRise / 90.0) * 90.0 - Math.Floor(RARise / 90.0) * 90.0;
        RASet  += Math.Floor(LSet / 90.0)  * 90.0 - Math.Floor(RASet / 90.0)  * 90.0;

        RARise /= 15.0;
        RASet  /= 15.0;

        double sinDecRise = 0.39782 * Math.Sin(DegToRad(LRise));
        double cosDecRise = Math.Sqrt(1.0 - sinDecRise * sinDecRise);
        double sinDecSet  = 0.39782 * Math.Sin(DegToRad(LSet));
        double cosDecSet  = Math.Sqrt(1.0 - sinDecSet * sinDecSet);
        
        double sinLat = Math.Sin(DegToRad(latitude));
        double cosLat = Math.Cos(DegToRad(latitude));

        double cosHRise = (-0.01453808 - (sinDecRise * sinLat)) / (cosDecRise * cosLat);
        double cosHSet  = (-0.01453808 - (sinDecSet  * sinLat)) / (cosDecSet  * cosLat);
        
        if (cosHRise > 1) return (DateTime.MaxValue, DateTime.MinValue); // sun never rises
        if (cosHSet < -1) return (DateTime.MinValue, DateTime.MaxValue);  // sun never sets
        
        double HRise = 360 - RadToDeg(Math.Acos(cosHRise));
        double HSet  = RadToDeg(Math.Acos(cosHSet));
        HRise /= 15.0;
        HSet  /= 15.0;

        double TRise = HRise + RARise - (0.06571 * tRise) - 6.622;
        double TSet  = HSet  + RASet  - (0.06571 * tSet)  - 6.622;

        double UTCRise = NormalizeTime(TRise - lngHour);
        double UTCSet  = NormalizeTime(TSet  - lngHour);

        DateTime sunriseUtc = DateTime.SpecifyKind(new DateTime(dateUtc.Year, dateUtc.Month, dateUtc.Day), DateTimeKind.Utc).AddHours(UTCRise);
        DateTime sunsetUtc  = DateTime.SpecifyKind(new DateTime(dateUtc.Year, dateUtc.Month, dateUtc.Day), DateTimeKind.Utc).AddHours(UTCSet);

        return (sunriseUtc, sunsetUtc);
    }

    private static double DegToRad(double angle) => angle * Math.PI / 180.0;
    private static double RadToDeg(double angle) => angle * 180.0 / Math.PI;
    private static double NormalizeAngle(double angle) => (angle % 360 + 360) % 360;
    private static double NormalizeTime(double t) => (t % 24 + 24) % 24;
}

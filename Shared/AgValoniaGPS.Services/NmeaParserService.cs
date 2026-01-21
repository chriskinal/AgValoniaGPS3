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
using System.Globalization;
using AgValoniaGPS.Models;
using AgValoniaGPS.Models.Configuration;
using AgValoniaGPS.Services.Interfaces;

namespace AgValoniaGPS.Services;

/// <summary>
/// NMEA sentence parser for PANDA and PAOGI formats
/// Based on AgIO NMEA parser.
/// Reads GPS configuration from ConfigurationStore for filtering and processing.
/// Supports dual GPS heading, heading fusion, and rate limiting.
/// </summary>
public class NmeaParserService
{
    private readonly IGpsService _gpsService;

    // Access GPS config from ConfigurationStore
    private static ConnectionConfig Connections => ConfigurationStore.Instance.Connections;

    // Previous position for single-antenna heading calculation
    private double _previousEasting;
    private double _previousNorthing;
    private double _previousHeading;
    private bool _hasPreviousPosition;

    /// <summary>
    /// Raised when IMU data is received (roll, pitch, yaw rate).
    /// </summary>
    public event EventHandler? ImuDataReceived;

    /// <summary>
    /// Raised when GPS fix quality is below the configured minimum.
    /// The int parameter is the actual fix quality received.
    /// </summary>
    public event EventHandler<int>? FixQualityBelowMinimum;

    /// <summary>
    /// Count of consecutive fixes rejected due to low quality.
    /// Resets when a good fix is received.
    /// </summary>
    public int ConsecutiveBadFixes { get; private set; }

    /// <summary>
    /// The final computed heading after fusion/dual processing.
    /// </summary>
    public double FusedHeading { get; private set; }

    public NmeaParserService(IGpsService gpsService)
    {
        _gpsService = gpsService;
    }

    /// <summary>
    /// Parse NMEA sentence and update GPS data
    /// Supports $PANDA and $PAOGI formats
    /// </summary>
    public void ParseSentence(string sentence)
    {
        if (string.IsNullOrWhiteSpace(sentence)) return;

        // Validate checksum
        if (!ValidateChecksum(sentence)) return;

        // Remove checksum
        int asterisk = sentence.IndexOf("*", StringComparison.Ordinal);
        if (asterisk > 0)
        {
            sentence = sentence.Substring(0, asterisk);
        }

        string[] words = sentence.Split(',');
        if (words.Length < 3) return;

        if (words[0] == "$PANDA" && words.Length > 14)
        {
            ParsePANDA(words);
        }
        else if (words[0] == "$PAOGI" && words.Length > 14)
        {
            ParsePAOGI(words);
        }
    }

    private void ParsePANDA(string[] words)
    {
        /*
        $PANDA
        (1) Time of fix
        (2,3) 4807.038,N Latitude 48 deg 07.038' N
        (4,5) 01131.000,E Longitude 11 deg 31.000' E
        (6) Fix quality (0-8)
        (7) Number of satellites
        (8) HDOP
        (9) Altitude in meters
        (10) Age of differential
        (11) Speed in knots
        (12) Heading in degrees
        (13) Roll in degrees
        (14) Pitch in degrees
        (15) Yaw rate in degrees/second
        */

        var gpsData = new GpsData();

        try
        {
            // Parse latitude
            if (!string.IsNullOrEmpty(words[2]) && !string.IsNullOrEmpty(words[3]))
            {
                double latitude = ParseLatitude(words[2], words[3]);
                gpsData.CurrentPosition = gpsData.CurrentPosition with { Latitude = latitude };
            }

            // Parse longitude
            if (!string.IsNullOrEmpty(words[4]) && !string.IsNullOrEmpty(words[5]))
            {
                double longitude = ParseLongitude(words[4], words[5]);
                gpsData.CurrentPosition = gpsData.CurrentPosition with { Longitude = longitude };
            }

            // Fix quality
            byte fixQuality = 0;
            if (byte.TryParse(words[6], NumberStyles.Float, CultureInfo.InvariantCulture, out fixQuality))
            {
                gpsData.FixQuality = fixQuality;
            }

            // Satellites
            if (int.TryParse(words[7], NumberStyles.Float, CultureInfo.InvariantCulture, out int satellites))
            {
                gpsData.SatellitesInUse = satellites;
            }

            // HDOP
            double hdop = 99.0;
            if (double.TryParse(words[8], NumberStyles.Float, CultureInfo.InvariantCulture, out hdop))
            {
                gpsData.Hdop = hdop;
            }

            // Altitude
            if (float.TryParse(words[9], NumberStyles.Float, CultureInfo.InvariantCulture, out float altitude))
            {
                gpsData.CurrentPosition = gpsData.CurrentPosition with { Altitude = altitude };
            }

            // Age of differential
            double age = 0;
            if (double.TryParse(words[10], NumberStyles.Float, CultureInfo.InvariantCulture, out age))
            {
                gpsData.DifferentialAge = age;
            }

            // Speed in knots - convert to m/s
            double speedMs = 0;
            if (float.TryParse(words[11], NumberStyles.Float, CultureInfo.InvariantCulture, out float speedKnots))
            {
                speedMs = speedKnots * 0.514444; // knots to m/s
                gpsData.CurrentPosition = gpsData.CurrentPosition with { Speed = speedMs };
            }

            // Heading from GPS
            double gpsHeading = 0;
            if (double.TryParse(words[12], NumberStyles.Float, CultureInfo.InvariantCulture, out gpsHeading))
            {
                // Initial heading from GPS sentence
            }

            // Parse IMU data (PANDA fields 13, 14, 15)
            double roll = 0, pitch = 0, yawRate = 0;
            if (words.Length > 13 && double.TryParse(words[13], NumberStyles.Float, CultureInfo.InvariantCulture, out roll))
            {
                SensorState.Instance.ImuRoll = roll;
            }
            if (words.Length > 14 && double.TryParse(words[14], NumberStyles.Float, CultureInfo.InvariantCulture, out pitch))
            {
                SensorState.Instance.ImuPitch = pitch;
            }
            if (words.Length > 15 && double.TryParse(words[15], NumberStyles.Float, CultureInfo.InvariantCulture, out yawRate))
            {
                SensorState.Instance.ImuYawRate = yawRate;
            }

            // Raise IMU event if we got data
            if (words.Length > 15)
            {
                ImuDataReceived?.Invoke(this, EventArgs.Empty);
            }

            // Process heading based on configuration (dual GPS, fusion, etc.)
            double finalHeading = ProcessHeading(gpsHeading, speedMs, gpsData.CurrentPosition.Easting, gpsData.CurrentPosition.Northing);
            gpsData.CurrentPosition = gpsData.CurrentPosition with { Heading = finalHeading };
            FusedHeading = finalHeading;

            gpsData.Timestamp = DateTime.Now;

            // Check fix quality against configured minimum
            int minFixQuality = Connections.MinFixQuality;
            double maxHdop = Connections.MaxHdop;
            double maxDiffAge = Connections.MaxDifferentialAge;

            bool isFixAcceptable = fixQuality >= minFixQuality;
            bool isHdopAcceptable = hdop <= maxHdop;
            bool isDiffAgeAcceptable = age <= maxDiffAge || age == 0; // 0 means no differential

            if (!isFixAcceptable || !isHdopAcceptable || !isDiffAgeAcceptable)
            {
                // Fix doesn't meet quality requirements
                ConsecutiveBadFixes++;
                gpsData.IsValid = false;

                // Raise event for UI notification (only on first bad fix or periodically)
                if (ConsecutiveBadFixes == 1 || ConsecutiveBadFixes % 10 == 0)
                {
                    FixQualityBelowMinimum?.Invoke(this, fixQuality);
                }

                // Still update GPS service so UI shows current (poor) status
                _gpsService.UpdateGpsData(gpsData);
                return;
            }

            // Good fix - reset counter and mark valid
            ConsecutiveBadFixes = 0;
            gpsData.IsValid = true;

            // Update GPS service with parsed data
            _gpsService.UpdateGpsData(gpsData);
        }
        catch
        {
            // Ignore parse errors
        }
    }

    private void ParsePAOGI(string[] words)
    {
        // PAOGI has same format as PANDA
        ParsePANDA(words);
    }

    private double ParseLatitude(string latString, string hemisphere)
    {
        // Format: DDMM.MMMM
        int decim = latString.IndexOf(".", StringComparison.Ordinal);
        if (decim == -1)
        {
            latString += ".00";
            decim = latString.IndexOf(".", StringComparison.Ordinal);
        }

        decim -= 2; // DD part

        if (!double.TryParse(latString.Substring(0, decim), NumberStyles.Float, CultureInfo.InvariantCulture, out double degrees))
            return 0;

        if (!double.TryParse(latString.Substring(decim), NumberStyles.Float, CultureInfo.InvariantCulture, out double minutes))
            return 0;

        double latitude = degrees + (minutes * 0.01666666666666666666666666666667); // minutes to degrees

        if (hemisphere == "S")
            latitude *= -1;

        return latitude;
    }

    private double ParseLongitude(string lonString, string hemisphere)
    {
        // Format: DDDMM.MMMM
        int decim = lonString.IndexOf(".", StringComparison.Ordinal);
        if (decim == -1)
        {
            lonString += ".00";
            decim = lonString.IndexOf(".", StringComparison.Ordinal);
        }

        decim -= 2; // DDD part

        if (!double.TryParse(lonString.Substring(0, decim), NumberStyles.Float, CultureInfo.InvariantCulture, out double degrees))
            return 0;

        if (!double.TryParse(lonString.Substring(decim), NumberStyles.Float, CultureInfo.InvariantCulture, out double minutes))
            return 0;

        double longitude = degrees + (minutes * 0.01666666666666666666666666666667); // minutes to degrees

        if (hemisphere == "W")
            longitude *= -1;

        return longitude;
    }

    private bool ValidateChecksum(string sentence)
    {
        // Find checksum position
        int asterisk = sentence.IndexOf("*", StringComparison.Ordinal);
        if (asterisk < 1) return false;

        // Calculate checksum
        byte checksum = 0;
        for (int i = 1; i < asterisk; i++) // Start after $
        {
            checksum ^= (byte)sentence[i];
        }

        // Get provided checksum
        string checksumStr = sentence.Substring(asterisk + 1, Math.Min(2, sentence.Length - asterisk - 1));
        if (byte.TryParse(checksumStr, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte providedChecksum))
        {
            return checksum == providedChecksum;
        }

        return false;
    }

    /// <summary>
    /// Process heading based on configuration (dual GPS, fusion, single antenna).
    /// </summary>
    /// <param name="gpsHeading">Raw heading from GPS sentence</param>
    /// <param name="speedMs">Current speed in m/s</param>
    /// <param name="easting">Current easting position</param>
    /// <param name="northing">Current northing position</param>
    /// <returns>Final processed heading in degrees</returns>
    private double ProcessHeading(double gpsHeading, double speedMs, double easting, double northing)
    {
        double finalHeading = gpsHeading;

        // Dual GPS mode - heading comes from dual antenna baseline
        if (Connections.IsDualGps)
        {
            // Apply dual heading offset (antenna mounting angle)
            finalHeading = gpsHeading + Connections.DualHeadingOffset;

            // Normalize to 0-360
            while (finalHeading < 0) finalHeading += 360;
            while (finalHeading >= 360) finalHeading -= 360;

            // Check if we should use fix-to-fix at low speeds
            if (speedMs < Connections.DualSwitchSpeed && _hasPreviousPosition)
            {
                // At low speed, dual antenna heading may be unreliable
                // Use fix-to-fix heading instead
                double fixToFixHeading = CalculateFixToFixHeading(easting, northing);
                if (fixToFixHeading >= 0)
                {
                    finalHeading = fixToFixHeading;
                }
            }
        }
        else
        {
            // Single antenna mode - may need fix-to-fix heading calculation
            if (speedMs >= Connections.MinGpsStep && _hasPreviousPosition)
            {
                double fixToFixHeading = CalculateFixToFixHeading(easting, northing);
                if (fixToFixHeading >= 0)
                {
                    // Use fix-to-fix heading when moving
                    finalHeading = fixToFixHeading;
                }
            }
        }

        // Heading fusion with IMU (if IMU data is available and fusion is enabled)
        double fusionWeight = Connections.HeadingFusionWeight;
        if (fusionWeight > 0 && fusionWeight < 1.0 && SensorState.Instance.HasValidImu)
        {
            double imuHeading = SensorState.Instance.ImuHeading;

            // Handle wrap-around when blending headings
            double diff = imuHeading - finalHeading;
            if (diff > 180) diff -= 360;
            if (diff < -180) diff += 360;

            // Blend: fusionWeight = GPS weight, (1 - fusionWeight) = IMU weight
            // Higher fusionWeight means more trust in GPS
            finalHeading = finalHeading + diff * (1.0 - fusionWeight);

            // Normalize to 0-360
            while (finalHeading < 0) finalHeading += 360;
            while (finalHeading >= 360) finalHeading -= 360;
        }

        // Store current position for next fix-to-fix calculation
        _previousEasting = easting;
        _previousNorthing = northing;
        _previousHeading = finalHeading;
        _hasPreviousPosition = true;

        return finalHeading;
    }

    /// <summary>
    /// Calculate heading from previous position to current position.
    /// </summary>
    /// <returns>Heading in degrees, or -1 if distance too small</returns>
    private double CalculateFixToFixHeading(double easting, double northing)
    {
        double dx = easting - _previousEasting;
        double dy = northing - _previousNorthing;
        double distance = Math.Sqrt(dx * dx + dy * dy);

        // Only calculate if we've moved enough (FixToFixDistance threshold)
        if (distance < Connections.FixToFixDistance)
        {
            return -1; // Not enough movement
        }

        // Calculate heading from delta
        double heading = Math.Atan2(dx, dy) * 180.0 / Math.PI;

        // Normalize to 0-360
        if (heading < 0) heading += 360;

        return heading;
    }

}
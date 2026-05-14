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
using System.Buffers.Text;
using System.Runtime.CompilerServices;
using AgValoniaGPS.Models;
using AgValoniaGPS.Services.Interfaces;

namespace AgValoniaGPS.Services;

/// <summary>
/// Zero-copy NMEA parser for PANDA/PAOGI formats.
/// Parses directly from byte buffer with no heap allocations.
/// Target: sub-millisecond parsing (matching Teensy New Dawn firmware approach).
/// </summary>
public class NmeaParserServiceFast
{
    private readonly IGpsService _gpsService;

    // Pre-allocated GpsData to avoid allocation per parse
    private GpsData _gpsData;

    // Field indices for PANDA/PAOGI
    private const int FIELD_TIME = 1;
    private const int FIELD_LAT = 2;
    private const int FIELD_LAT_DIR = 3;
    private const int FIELD_LON = 4;
    private const int FIELD_LON_DIR = 5;
    private const int FIELD_FIX = 6;
    private const int FIELD_SATS = 7;
    private const int FIELD_HDOP = 8;
    private const int FIELD_ALT = 9;
    private const int FIELD_AGE = 10;
    private const int FIELD_SPEED = 11;
    private const int FIELD_HEADING = 12;
    private const int FIELD_ROLL = 13;
    private const int FIELD_PITCH = 14;
    private const int FIELD_YAW_RATE = 15;

    private const int MIN_PANDA_FIELDS = 15;

    // Sentence type identifiers (after $)
    private static ReadOnlySpan<byte> PANDA => "PANDA"u8;
    private static ReadOnlySpan<byte> PAOGI => "PAOGI"u8;

    public NmeaParserServiceFast(IGpsService gpsService)
    {
        _gpsService = gpsService;
        _gpsData = new GpsData();
    }

    /// <summary>
    /// Parse NMEA sentence directly from byte buffer.
    /// Zero allocations in the hot path.
    /// </summary>
    /// <param name="buffer">Raw UDP receive buffer</param>
    /// <param name="length">Number of valid bytes in buffer</param>
    /// <returns>True if parsed successfully</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool ParseBuffer(byte[] buffer, int length)
    {
        return ParseSpan(buffer.AsSpan(0, length));
    }

    /// <summary>
    /// Parse NMEA sentence from span.
    /// Zero allocations.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public bool ParseSpan(ReadOnlySpan<byte> data)
    {
        // Minimum valid: $PANDA,... = at least 20 bytes
        if (data.Length < 20) return false;

        // Must start with $
        if (data[0] != '$') return false;

        // Find checksum marker
        int asterisk = data.IndexOf((byte)'*');
        if (asterisk < 10) return false;

        // Validate checksum (XOR of bytes between $ and *)
        if (!ValidateChecksum(data, asterisk)) return false;

        // Get sentence type (bytes 1-5 after $)
        var sentenceType = data.Slice(1, 5);

        // Check for PANDA or PAOGI
        bool isPanda;
        if (sentenceType.SequenceEqual(PANDA)) isPanda = true;
        else if (sentenceType.SequenceEqual(PAOGI)) isPanda = false;
        else return false;

        // Parse the fields (data up to asterisk)
        return ParsePandaFields(data.Slice(0, asterisk), isPanda);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool ValidateChecksum(ReadOnlySpan<byte> data, int asteriskPos)
    {
        // Calculate XOR checksum of bytes between $ and *
        byte checksum = 0;
        for (int i = 1; i < asteriskPos; i++)
        {
            checksum ^= data[i];
        }

        // Parse provided checksum (2 hex digits after *)
        if (asteriskPos + 2 >= data.Length) return false;

        byte providedHigh = HexCharToNibble(data[asteriskPos + 1]);
        byte providedLow = HexCharToNibble(data[asteriskPos + 2]);

        if (providedHigh == 0xFF || providedLow == 0xFF) return false;

        byte provided = (byte)((providedHigh << 4) | providedLow);
        return checksum == provided;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte HexCharToNibble(byte c)
    {
        if (c >= '0' && c <= '9') return (byte)(c - '0');
        if (c >= 'A' && c <= 'F') return (byte)(c - 'A' + 10);
        if (c >= 'a' && c <= 'f') return (byte)(c - 'a' + 10);
        return 0xFF; // Invalid
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private bool ParsePandaFields(ReadOnlySpan<byte> data, bool isPanda)
    {
        // Find all comma positions (up to 16 fields)
        Span<int> commas = stackalloc int[20];
        int commaCount = 0;

        commas[commaCount++] = -1; // Virtual comma before first field

        for (int i = 0; i < data.Length && commaCount < 20; i++)
        {
            if (data[i] == ',')
                commas[commaCount++] = i;
        }

        // Add end position as final "comma"
        commas[commaCount++] = data.Length;

        // Need at least 15 fields for PANDA
        if (commaCount < MIN_PANDA_FIELDS + 1) return false;

        // Reset GPS data
        double latitude = 0, longitude = 0, altitude = 0, speed = 0, heading = 0;
        double hdop = 0, age = 0;
        int fixQuality = 0, satellites = 0;
        bool latSouth = false, lonWest = false;

        // Parse each field using spans (zero-copy)

        // Latitude (field 2): DDMM.MMMMM
        var latField = GetField(data, commas, FIELD_LAT);
        if (latField.Length > 0)
        {
            latitude = ParseLatLon(latField);
        }

        // Latitude direction (field 3): N or S
        var latDirField = GetField(data, commas, FIELD_LAT_DIR);
        if (latDirField.Length > 0 && latDirField[0] == 'S')
            latSouth = true;

        // Longitude (field 4): DDDMM.MMMMM
        var lonField = GetField(data, commas, FIELD_LON);
        if (lonField.Length > 0)
        {
            longitude = ParseLatLon(lonField);
        }

        // Longitude direction (field 5): E or W
        var lonDirField = GetField(data, commas, FIELD_LON_DIR);
        if (lonDirField.Length > 0 && lonDirField[0] == 'W')
            lonWest = true;

        // Fix quality (field 6)
        var fixField = GetField(data, commas, FIELD_FIX);
        if (fixField.Length > 0)
        {
            Utf8Parser.TryParse(fixField, out fixQuality, out _);
        }

        // Satellites (field 7)
        var satsField = GetField(data, commas, FIELD_SATS);
        if (satsField.Length > 0)
        {
            Utf8Parser.TryParse(satsField, out satellites, out _);
        }

        // HDOP (field 8)
        var hdopField = GetField(data, commas, FIELD_HDOP);
        if (hdopField.Length > 0)
        {
            Utf8Parser.TryParse(hdopField, out hdop, out _);
        }

        // Altitude (field 9)
        var altField = GetField(data, commas, FIELD_ALT);
        if (altField.Length > 0)
        {
            Utf8Parser.TryParse(altField, out altitude, out _);
        }

        // Age of differential (field 10)
        var ageField = GetField(data, commas, FIELD_AGE);
        if (ageField.Length > 0)
        {
            Utf8Parser.TryParse(ageField, out age, out _);
        }

        // Speed in knots (field 11)
        var speedField = GetField(data, commas, FIELD_SPEED);
        if (speedField.Length > 0)
        {
            Utf8Parser.TryParse(speedField, out speed, out _);
            speed *= 0.514444; // knots to m/s
        }

        // Heading (field 12). PANDA: int scaled ×10 with 65535 sentinel.
        // PAOGI: float decimal degrees.
        var headingField = GetField(data, commas, FIELD_HEADING);
        if (headingField.Length > 0)
        {
            if (isPanda)
            {
                if (Utf8Parser.TryParse(headingField, out int rawHeading, out _)
                    && rawHeading != 65535)
                {
                    heading = rawHeading * 0.1;
                }
            }
            else
            {
                Utf8Parser.TryParse(headingField, out heading, out _);
            }
        }

        // Apply hemisphere signs
        if (latSouth) latitude = -latitude;
        if (lonWest) longitude = -longitude;

        // Update GPS data
        _gpsData = new GpsData
        {
            CurrentPosition = new Position
            {
                Latitude = latitude,
                Longitude = longitude,
                Altitude = altitude,
                Speed = speed,
                Heading = heading
            },
            FixQuality = fixQuality,
            SatellitesInUse = satellites,
            Hdop = hdop,
            DifferentialAge = age,
            Timestamp = DateTime.UtcNow
        };

        // Notify GPS service
        _gpsService.UpdateGpsData(_gpsData);

        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ReadOnlySpan<byte> GetField(ReadOnlySpan<byte> data, Span<int> commas, int fieldIndex)
    {
        int start = commas[fieldIndex] + 1;
        int end = commas[fieldIndex + 1];
        int length = end - start;

        if (length <= 0 || start >= data.Length) return ReadOnlySpan<byte>.Empty;

        return data.Slice(start, length);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Zero-Copy VehicleState Parsing (AutoSteer Pipeline)
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Parse NMEA sentence directly into VehicleState struct.
    /// Zero allocations - writes directly to struct fields.
    /// </summary>
    /// <param name="data">Raw NMEA data</param>
    /// <param name="state">VehicleState struct to populate (passed by ref)</param>
    /// <returns>True if parsed successfully</returns>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static bool ParseIntoState(ReadOnlySpan<byte> data, ref VehicleState state)
    {
        state.MarkParseStart();

        // Minimum valid: $PANDA,... = at least 20 bytes
        if (data.Length < 20) return false;

        // Must start with $
        if (data[0] != '$') return false;

        // Find checksum marker
        int asterisk = data.IndexOf((byte)'*');
        if (asterisk < 10) return false;

        // Validate checksum (XOR of bytes between $ and *)
        if (!ValidateChecksum(data, asterisk)) return false;

        // Get sentence type (bytes 1-5 after $)
        var sentenceType = data.Slice(1, 5);

        // PANDA = single GPS + IMU; field 12 is IMU heading scaled ×10 with
        // sentinel "65535", field 13 is IMU roll scaled ×10. PAOGI = dual
        // antenna; field 12 is dual-antenna heading as float, field 13 is
        // dual roll as float — no IMU fusion needed.
        bool isPanda;
        if (sentenceType.SequenceEqual(PANDA)) isPanda = true;
        else if (sentenceType.SequenceEqual(PAOGI)) isPanda = false;
        else return false;

        // Parse directly into state
        if (!ParsePandaFieldsIntoState(data.Slice(0, asterisk), ref state, isPanda))
            return false;

        state.MarkParseEnd();
        return true;
    }

    /// <summary>
    /// Parse PANDA/PAOGI fields directly into VehicleState. Fields 1-11 are
    /// identical between the two. Fields 12-13 (heading, roll) differ:
    /// PANDA scales them ×10 as int with a 65535 IMU-invalid sentinel on
    /// heading; PAOGI sends them as floats with no scaling.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static bool ParsePandaFieldsIntoState(ReadOnlySpan<byte> data, ref VehicleState state, bool isPanda)
    {
        // Find all comma positions (up to 20 fields)
        Span<int> commas = stackalloc int[20];
        int commaCount = 0;

        commas[commaCount++] = -1; // Virtual comma before first field

        for (int i = 0; i < data.Length && commaCount < 20; i++)
        {
            if (data[i] == ',')
                commas[commaCount++] = i;
        }

        // Add end position as final "comma"
        commas[commaCount++] = data.Length;

        // Need at least 15 fields for PANDA
        if (commaCount < MIN_PANDA_FIELDS + 1) return false;

        // Parse latitude (field 2): DDMM.MMMMM
        var latField = GetField(data, commas, FIELD_LAT);
        if (latField.Length > 0)
        {
            state.Latitude = ParseLatLon(latField);
        }

        // Latitude direction (field 3): N or S
        var latDirField = GetField(data, commas, FIELD_LAT_DIR);
        if (latDirField.Length > 0 && latDirField[0] == 'S')
            state.Latitude = -state.Latitude;

        // Longitude (field 4): DDDMM.MMMMM
        var lonField = GetField(data, commas, FIELD_LON);
        if (lonField.Length > 0)
        {
            state.Longitude = ParseLatLon(lonField);
        }

        // Longitude direction (field 5): E or W
        var lonDirField = GetField(data, commas, FIELD_LON_DIR);
        if (lonDirField.Length > 0 && lonDirField[0] == 'W')
            state.Longitude = -state.Longitude;

        // Fix quality (field 6)
        var fixField = GetField(data, commas, FIELD_FIX);
        if (fixField.Length > 0)
        {
            Utf8Parser.TryParse(fixField, out state.FixQuality, out _);
        }

        // Satellites (field 7)
        var satsField = GetField(data, commas, FIELD_SATS);
        if (satsField.Length > 0)
        {
            Utf8Parser.TryParse(satsField, out state.Satellites, out _);
        }

        // HDOP (field 8)
        var hdopField = GetField(data, commas, FIELD_HDOP);
        if (hdopField.Length > 0)
        {
            Utf8Parser.TryParse(hdopField, out state.Hdop, out _);
        }

        // Altitude (field 9)
        var altField = GetField(data, commas, FIELD_ALT);
        if (altField.Length > 0)
        {
            Utf8Parser.TryParse(altField, out state.Altitude, out _);
        }

        // Age of differential (field 10)
        var ageField = GetField(data, commas, FIELD_AGE);
        if (ageField.Length > 0)
        {
            Utf8Parser.TryParse(ageField, out state.DifferentialAge, out _);
        }

        // Speed in knots (field 11) - convert to m/s
        var speedField = GetField(data, commas, FIELD_SPEED);
        if (speedField.Length > 0)
        {
            Utf8Parser.TryParse(speedField, out state.Speed, out _);
            state.Speed *= 0.514444; // knots to m/s
        }

        // Heading (field 12) and roll (field 13) — sentence-type dependent.
        var headingField = GetField(data, commas, FIELD_HEADING);
        var rollField = GetField(data, commas, FIELD_ROLL);

        if (isPanda)
        {
            // PANDA: heading is `(int)(degrees * 10)` with sentinel 65535 for
            // "no IMU"; roll is `(int)(degrees * 10)` (the firmware's
            // currentData.roll is pre-multiplied by 10 at the IMU layer —
            // see Firmware_Teensy_AiO_26/lib/aio_navigation/IMUProcessor.cpp).
            int rawHeading = 0;
            bool imuValid = false;
            if (headingField.Length > 0
                && Utf8Parser.TryParse(headingField, out rawHeading, out _)
                && rawHeading != 65535)
            {
                state.ImuHeading = rawHeading * 0.1;
                imuValid = true;
            }
            else
            {
                state.ImuHeading = 0;
            }
            state.ImuValid = imuValid;
            // Seed primary heading from IMU so first-cycle / standstill has a
            // sensible default. Pipeline's fix-to-fix overrides at any real speed.
            state.Heading = imuValid ? state.ImuHeading : 0;

            if (imuValid && rollField.Length > 0
                && Utf8Parser.TryParse(rollField, out int rawRoll, out _))
            {
                state.Roll = rawRoll * 0.1;
                ApplyAhrsRollCalibration(ref state.Roll);
            }
            else
            {
                state.Roll = 0;
            }
        }
        else
        {
            // PAOGI: dual-antenna heading and dual roll, both float decimal
            // degrees. Dual antenna is ground truth — no IMU fusion needed,
            // so ImuHeading stays 0 and ImuValid stays false.
            if (headingField.Length > 0)
            {
                Utf8Parser.TryParse(headingField, out state.Heading, out _);
            }
            else
            {
                state.Heading = 0;
            }
            state.ImuHeading = 0;
            state.ImuValid = false;

            if (rollField.Length > 0)
            {
                Utf8Parser.TryParse(rollField, out state.Roll, out _);
                ApplyAhrsRollCalibration(ref state.Roll);
            }
            else
            {
                state.Roll = 0;
            }
        }

        // Pitch angle in degrees (field 14) — same format both sentences.
        var pitchField = GetField(data, commas, FIELD_PITCH);
        if (pitchField.Length > 0)
        {
            Utf8Parser.TryParse(pitchField, out state.Pitch, out _);
        }

        // Yaw rate in degrees/second (field 15) — same format both sentences.
        var yawField = GetField(data, commas, FIELD_YAW_RATE);
        if (yawField.Length > 0)
        {
            Utf8Parser.TryParse(yawField, out state.YawRate, out _);
        }

        // Pre-compute heading in radians for guidance calculations
        state.HeadingRadians = state.Heading * (Math.PI / 180.0);

        return true;
    }

    /// <summary>
    /// Parse latitude/longitude from NMEA format: DDMM.MMMMM or DDDMM.MMMMM
    /// Zero allocation - works directly on byte span.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double ParseLatLon(ReadOnlySpan<byte> field)
    {
        // Find decimal point
        int decimalPos = -1;
        for (int i = 0; i < field.Length; i++)
        {
            if (field[i] == '.')
            {
                decimalPos = i;
                break;
            }
        }

        if (decimalPos < 2) return 0;

        // Degrees are everything before (decimalPos - 2)
        int degreeEnd = decimalPos - 2;

        // Parse degrees (1-3 digits)
        int degrees = 0;
        for (int i = 0; i < degreeEnd; i++)
        {
            degrees = degrees * 10 + (field[i] - '0');
        }

        // Parse minutes (rest of the field including decimal)
        double minutes = 0;
        if (Utf8Parser.TryParse(field.Slice(degreeEnd), out minutes, out _))
        {
            return degrees + (minutes / 60.0);
        }

        return degrees;
    }

    /// <summary>
    /// Apply the operator-calibrated AHRS roll transform: invert sign if
    /// <c>IsRollInvert</c> is set, then subtract <c>RollZero</c>. Mirrors
    /// the WAS post-process in <c>SmartWasCalibrationService</c>. Without
    /// this, the wizard's roll gauge and the guidance pipeline both see
    /// raw uncalibrated IMU roll, and the operator's "Zero Roll" tap in
    /// the Roll-calibration wizard step has no effect on live readings.
    /// </summary>
    private static void ApplyAhrsRollCalibration(ref double roll)
    {
        var ahrs = Models.Configuration.ConfigurationStore.Instance.Ahrs;
        if (ahrs.IsRollInvert) roll = -roll;
        roll -= ahrs.RollZero;
    }
}

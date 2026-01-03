using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using AgValoniaGPS.Models;
using AgValoniaGPS.Models.Configuration;

namespace AgValoniaGPS.Services.AutoSteer;

/// <summary>
/// Builds PGN packets for transmission to AgOpenGPS hardware modules.
/// Uses thread-local buffers for zero allocations in the hot path.
///
/// Format follows AgOpenGPS standard: [0x80, 0x81, Source, PGN, Length, Data..., CRC]
/// </summary>
public static class PgnBuilder
{
    // Standard AgOpenGPS header
    public const byte HEADER1 = 0x80;
    public const byte HEADER2 = 0x81;
    public const byte SOURCE = 0x7F;  // AgIO/AgOpenGPS source

    // PGN identifiers (from PgnNumbers)
    public const byte PGN_AUTOSTEER = 0xFE;      // 254 - AutoSteer Data
    public const byte PGN_MACHINE = 0xEF;        // 239 - Machine Data
    public const byte PGN_STEER_SETTINGS = 0xFC; // 252 - Steer Settings
    public const byte PGN_STEER_CONFIG = 0xFB;   // 251 - Steer Config
    public const byte PGN_STEER_DATA = 0xFD;     // 253 - Steer Data FROM Module

    // Buffer sizes: header(2) + source(1) + pgn(1) + length(1) + data(N) + crc(1)
    public const int AUTOSTEER_PGN_SIZE = 14;       // 5 header + 8 data + 1 crc
    public const int MACHINE_PGN_SIZE = 14;         // 5 header + 8 data + 1 crc
    public const int STEER_SETTINGS_PGN_SIZE = 14;  // 5 header + 8 data + 1 crc
    public const int STEER_CONFIG_PGN_SIZE = 11;    // 5 header + 5 data + 1 crc

    // Thread-local buffers to avoid allocation
    [ThreadStatic]
    private static byte[]? _autoSteerBuffer;

    [ThreadStatic]
    private static byte[]? _machineBuffer;

    [ThreadStatic]
    private static byte[]? _steerSettingsBuffer;

    [ThreadStatic]
    private static byte[]? _steerConfigBuffer;

    /// <summary>
    /// Build PGN 254 (Steer Data) from VehicleState.
    /// Format per PGN 5.6 spec: [0x80, 0x81, 0x7F, 0xFE, 8, Speed(2), Status, SteerAngle(2), XTE, SC1-8, SC9-16, CRC]
    ///
    /// Byte 5-6:  Speed (high/low)
    /// Byte 7:    Status
    /// Byte 8-9:  steerAngle * 100 (high/low)
    /// Byte 10:   xte (cross-track error, single byte)
    /// Byte 11:   SC1to8 (sections 1-8 bitmask)
    /// Byte 12:   SC9to16 (sections 9-16 bitmask)
    /// Byte 13:   CRC
    ///
    /// When IsInFreeDriveMode is true, overrides speed/status/angle for testing:
    /// - Speed set to 8.0 km/h (fake speed to allow motor operation)
    /// - Status set to 1 (autosteer enabled)
    /// - SteerAngle from FreeDriveSteerAngle instead of guidance
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte[] BuildAutoSteerPgn(ref VehicleState state)
    {
        // Use thread-local buffer to avoid allocation
        _autoSteerBuffer ??= new byte[AUTOSTEER_PGN_SIZE];
        var buf = _autoSteerBuffer;

        // Header
        buf[0] = HEADER1;
        buf[1] = HEADER2;
        buf[2] = SOURCE;
        buf[3] = PGN_AUTOSTEER;
        buf[4] = 8;  // Data length

        // Check for Free Drive mode (test mode from config panel)
        if (state.IsInFreeDriveMode)
        {
            // Free Drive: fake speed of 8.0 km/h to allow motor operation
            ushort freeSpeed = 80;  // 8.0 km/h * 10
            buf[5] = (byte)(freeSpeed >> 8);
            buf[6] = (byte)(freeSpeed & 0xFF);

            // Status = 1 (autosteer enabled for testing)
            buf[7] = 1;

            // Use free drive steer angle instead of guidance angle
            short freeDriveAngle = (short)(state.FreeDriveSteerAngle * 100);
            buf[8] = (byte)(freeDriveAngle >> 8);
            buf[9] = (byte)(freeDriveAngle & 0xFF);

            // XTE = 0 in free drive mode
            buf[10] = 127;  // 0 + 127 offset

            // Section states (still send real values)
            buf[11] = (byte)(state.SectionStates & 0xFF);
            buf[12] = (byte)((state.SectionStates >> 8) & 0xFF);
        }
        else
        {
            // Normal mode: use guidance values

            // Speed - using km/h * 10 format (2 bytes, high/low)
            ushort speedInt = (ushort)(state.SpeedKmh * 10);
            buf[5] = (byte)(speedInt >> 8);
            buf[6] = (byte)(speedInt & 0xFF);

            // Status byte
            byte status = 0;
            if (state.SteerSwitchActive) status |= 0x01;
            if (state.WorkSwitchActive) status |= 0x02;
            if (state.IsAutoSteerEngaged) status |= 0x04;
            if (state.GpsValid) status |= 0x08;
            if (state.GuidanceValid) status |= 0x10;
            buf[7] = status;

            // Steer angle * 100 (signed, 2 bytes, high/low)
            short angleInt = (short)(state.SteerAngle * 100);
            buf[8] = (byte)(angleInt >> 8);
            buf[9] = (byte)(angleInt & 0xFF);

            // XTE - cross-track error (single byte, clamped to -127 to 127 cm)
            int xte = (int)(state.CrossTrackError * 100); // meters to cm
            xte = Math.Clamp(xte, -127, 127);
            buf[10] = (byte)(sbyte)xte;

            // Section states (16 bits = 2 bytes)
            buf[11] = (byte)(state.SectionStates & 0xFF);         // Sections 1-8
            buf[12] = (byte)((state.SectionStates >> 8) & 0xFF);  // Sections 9-16
        }

        // CRC: sum of bytes 2 through 12 (source through last data byte)
        buf[13] = CalculateCrc(buf, 2, 11);

        return buf;
    }

    /// <summary>
    /// Build PGN 239 (Machine Data) from VehicleState.
    /// Format per PGN 5.6 spec: [0x80, 0x81, 0x7F, 0xEF, 8, uturn, speed*10, hydLift, Tram, GeoStop, ***, SC1-8, SC9-16, CRC]
    ///
    /// Byte 5:  uturn (U-turn state)
    /// Byte 6:  speed * 10 (single byte)
    /// Byte 7:  hydLift (hydraulic lift state)
    /// Byte 8:  Tram (tramline state)
    /// Byte 9:  Geo Stop (geo-fence stop)
    /// Byte 10: Reserved
    /// Byte 11: SC1to8 (sections 1-8 bitmask)
    /// Byte 12: SC9to16 (sections 9-16 bitmask)
    /// Byte 13: CRC
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte[] BuildMachinePgn(ref VehicleState state, byte uturn = 0, byte hydLift = 0, byte tram = 0, byte geoStop = 0)
    {
        // Use thread-local buffer
        _machineBuffer ??= new byte[MACHINE_PGN_SIZE];
        var buf = _machineBuffer;

        // Header
        buf[0] = HEADER1;
        buf[1] = HEADER2;
        buf[2] = SOURCE;
        buf[3] = PGN_MACHINE;
        buf[4] = 8;  // Data length

        // U-turn state
        buf[5] = uturn;

        // Speed * 10 (single byte, max 25.5 km/h in this format)
        int speedInt = (int)(state.SpeedKmh * 10);
        buf[6] = (byte)Math.Clamp(speedInt, 0, 255);

        // Hydraulic lift state
        buf[7] = hydLift;

        // Tramline state
        buf[8] = tram;

        // Geo Stop
        buf[9] = geoStop;

        // Reserved
        buf[10] = 0;

        // Section states (16 bits = 2 bytes)
        buf[11] = (byte)(state.SectionStates & 0xFF);         // Sections 1-8
        buf[12] = (byte)((state.SectionStates >> 8) & 0xFF);  // Sections 9-16

        // CRC
        buf[13] = CalculateCrc(buf, 2, 11);

        return buf;
    }

    /// <summary>
    /// Calculate CRC as sum of bytes (matching PgnMessage.CalculateCRC)
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte CalculateCrc(byte[] data, int start, int length)
    {
        byte crc = 0;
        for (int i = start; i < start + length; i++)
        {
            crc += data[i];
        }
        return crc;
    }

    /// <summary>
    /// Validate a received PGN checksum.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool ValidateChecksum(ReadOnlySpan<byte> data)
    {
        if (data.Length < 2) return false;

        int checksumPos = data.Length - 1;
        byte calculated = 0;
        for (int i = 0; i < checksumPos; i++)
        {
            calculated ^= data[i];
        }
        return calculated == data[checksumPos];
    }

    /// <summary>
    /// Build PGN 252 (Steer Settings) from AutoSteerConfig.
    /// Format: [0x80, 0x81, 0x7F, 0xFC, 8, gainP, highPWM, lowPWM, minPWM, countsPerDeg, offsetLo, offsetHi, ackerman, CRC]
    ///
    /// Byte 5:  Proportional gain (1-100)
    /// Byte 6:  High PWM limit (max PWM)
    /// Byte 7:  Low PWM limit (highPWM / 3)
    /// Byte 8:  Minimum PWM to move
    /// Byte 9:  Counts per degree (1-255)
    /// Byte 10: WAS offset low byte (little-endian)
    /// Byte 11: WAS offset high byte (little-endian)
    /// Byte 12: Ackermann correction (0-200)
    /// Byte 13: CRC
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte[] BuildSteerSettingsPgn(AutoSteerConfig config)
    {
        _steerSettingsBuffer ??= new byte[STEER_SETTINGS_PGN_SIZE];
        var buf = _steerSettingsBuffer;

        // Header
        buf[0] = HEADER1;
        buf[1] = HEADER2;
        buf[2] = SOURCE;
        buf[3] = PGN_STEER_SETTINGS;
        buf[4] = 8;  // Data length

        // Proportional gain (1-100)
        buf[5] = (byte)Math.Clamp(config.ProportionalGain, 1, 100);

        // High PWM (max PWM, 50-255)
        buf[6] = (byte)Math.Clamp(config.MaxPwm, 50, 255);

        // Low PWM (typically highPWM / 3)
        buf[7] = (byte)(buf[6] / 3);

        // Min PWM to move (1-50)
        buf[8] = (byte)Math.Clamp(config.MinPwm, 1, 50);

        // Counts per degree (1-255, sent as-is)
        buf[9] = (byte)Math.Clamp((int)config.CountsPerDegree, 1, 255);

        // WAS offset (signed 16-bit, little-endian: low byte first)
        short wasOffset = (short)Math.Clamp(config.WasOffset, -32768, 32767);
        buf[10] = (byte)(wasOffset & 0xFF);        // low byte
        buf[11] = (byte)((wasOffset >> 8) & 0xFF); // high byte

        // Ackermann correction (0-200)
        buf[12] = (byte)Math.Clamp(config.Ackermann, 0, 200);

        // CRC
        buf[13] = CalculateCrc(buf, 2, 11);

        return buf;
    }

    /// <summary>
    /// Build PGN 251 (Steer Config) from AutoSteerConfig.
    /// Format: [0x80, 0x81, 0x7F, 0xFB, 5, set0, pulseCount, minSpeed, set1, angVel, CRC]
    ///
    /// Byte 5 (set0):
    ///   bit 0: Invert WAS
    ///   bit 1: Invert Relays
    ///   bit 2: Invert Motor
    ///   bit 3: AD Converter (0=Differential, 1=Single)
    ///   bit 4: Motor Driver (0=IBT2, 1=Cytron)
    ///   bit 5-6: External Enable (0=None, 1=Switch, 2=Button)
    ///   bit 7: Turn Sensor enabled
    /// Byte 6:  Pulse count
    /// Byte 7:  Min steer speed * 10
    /// Byte 8 (set1):
    ///   bit 0: Danfoss
    ///   bit 1: Pressure Sensor
    ///   bit 2: Current Sensor
    ///   bit 3-4: IMU Axis Swap
    /// Byte 9:  Angular velocity
    /// Byte 10: CRC
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte[] BuildSteerConfigPgn(AutoSteerConfig config)
    {
        _steerConfigBuffer ??= new byte[STEER_CONFIG_PGN_SIZE];
        var buf = _steerConfigBuffer;

        // Header
        buf[0] = HEADER1;
        buf[1] = HEADER2;
        buf[2] = SOURCE;
        buf[3] = PGN_STEER_CONFIG;
        buf[4] = 5;  // Data length

        // Set0 byte (use helper from config)
        buf[5] = config.GetSetting0Byte();

        // Pulse count (not currently used, set to 0)
        buf[6] = 0;

        // Min steer speed * 10
        buf[7] = (byte)Math.Clamp((int)(config.MinSteerSpeed * 10), 0, 255);

        // Set1 byte (use helper from config)
        buf[8] = config.GetSetting1Byte();

        // Angular velocity (not currently used, set to 0)
        buf[9] = 0;

        // CRC
        buf[10] = CalculateCrc(buf, 2, 8);

        return buf;
    }

    /// <summary>
    /// Parse PGN 253 (Steer Data) received from the steering module.
    /// Format: [0x80, 0x81, Source, 0xFD, 8, AngleHi, AngleLo, HeadingHi, HeadingLo, Roll, Switches, PWM, Reserved, CRC]
    ///
    /// Byte 5-6:  Actual steer angle * 100 (signed int16)
    /// Byte 7-8:  Heading from IMU * 16 (unsigned int16)
    /// Byte 9:    Roll from IMU (signed byte, degrees)
    /// Byte 10:   Switch status byte
    ///            bit 0: Steer switch
    ///            bit 1: Work switch
    ///            bit 2: Remote steer button
    /// Byte 11:   PWM display (0-255)
    /// Byte 12:   Reserved
    /// Byte 13:   CRC
    /// </summary>
    /// <param name="data">Raw PGN data including headers</param>
    /// <param name="result">Parsed steer module data</param>
    /// <returns>True if parsing succeeded, false if data is invalid</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryParseSteerData(ReadOnlySpan<byte> data, out SteerModuleData result)
    {
        result = default;

        // Validate minimum length: header(2) + source(1) + pgn(1) + length(1) + data(8) + crc(1) = 14
        if (data.Length < 14)
            return false;

        // Validate header
        if (data[0] != HEADER1 || data[1] != HEADER2)
            return false;

        // Validate PGN
        if (data[3] != PGN_STEER_DATA)
            return false;

        // Parse actual steer angle (signed int16, angle * 100)
        // Little-endian: low byte first (Arduino/Teensy convention)
        short angleRaw = (short)(data[5] | (data[6] << 8));
        double actualSteerAngle = angleRaw / 100.0;

        // Parse IMU heading (unsigned int16, heading * 16)
        // Little-endian: low byte first
        ushort headingRaw = (ushort)(data[7] | (data[8] << 8));
        double imuHeading = headingRaw / 16.0;

        // Parse IMU roll (signed byte)
        sbyte imuRoll = (sbyte)data[9];

        // Parse switch status byte
        byte switches = data[10];
        bool steerSwitch = (switches & 0x01) != 0;
        bool workSwitch = (switches & 0x02) != 0;
        bool remoteButton = (switches & 0x04) != 0;

        // Parse PWM display
        byte pwmDisplay = data[11];

        result = new SteerModuleData(
            actualSteerAngle,
            imuHeading,
            imuRoll,
            steerSwitch,
            workSwitch,
            remoteButton,
            pwmDisplay);

        return true;
    }

    /// <summary>
    /// Parse PGN 253 from a byte array (convenience overload).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryParseSteerData(byte[] data, out SteerModuleData result)
    {
        return TryParseSteerData(data.AsSpan(), out result);
    }
}

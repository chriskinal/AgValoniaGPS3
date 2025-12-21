using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using AgValoniaGPS.Models;

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
    public const byte PGN_AUTOSTEER = 0xFE;  // 254 - AutoSteer Data
    public const byte PGN_MACHINE = 0xEF;    // 239 - Machine Data

    // Buffer sizes: header(2) + source(1) + pgn(1) + length(1) + data(N) + crc(1)
    public const int AUTOSTEER_PGN_SIZE = 14;  // 5 header + 8 data + 1 crc
    public const int MACHINE_PGN_SIZE = 14;    // 5 header + 8 data + 1 crc

    // Thread-local buffers to avoid allocation
    [ThreadStatic]
    private static byte[]? _autoSteerBuffer;

    [ThreadStatic]
    private static byte[]? _machineBuffer;

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
}

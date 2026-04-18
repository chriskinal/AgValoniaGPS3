// AgValoniaGPS
// Copyright (C) 2024-2025 AgValoniaGPS Contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.

using System;

namespace AgValoniaGPS.VehicleSimulator.Modules;

/// <summary>
/// Shared PGN protocol constants and helpers for virtual modules.
/// Matches AgOpenGPS/AgValoniaGPS wire format.
/// </summary>
public static class PgnProtocol
{
    public const byte HEADER1 = 0x80;
    public const byte HEADER2 = 0x81;
    public const byte SOURCE_HOST = 0x7F;   // From AgOpenGPS/AgIO
    public const byte SOURCE_MODULE = 0x7F; // From module (same source byte)

    // PGN numbers - Host to Module
    public const byte PGN_AUTOSTEER_TO_MODULE = 0xFE;  // 254
    public const byte PGN_MACHINE_TO_MODULE = 0xEF;     // 239
    public const byte PGN_STEER_SETTINGS = 0xFC;        // 252
    public const byte PGN_STEER_CONFIG = 0xFB;          // 251
    public const byte PGN_MACHINE_CONFIG = 0xEE;         // 238
    public const byte PGN_MACHINE_PINS = 0xEC;           // 236
    public const byte PGN_HELLO_FROM_HOST = 0xC8;       // 200
    public const byte PGN_SCAN = 0xCA;                  // 202
    public const byte PGN_IP_CONFIG = 0xC9;             // 201

    // PGN numbers - Module to Host
    public const byte PGN_STEER_DATA = 0xFD;            // 253
    public const byte PGN_SENSOR_DATA = 0xFA;           // 250
    public const byte PGN_HELLO_AUTOSTEER = 0x7E;       // 126
    public const byte PGN_HELLO_MACHINE = 0x7B;         // 123
    public const byte PGN_HELLO_IMU = 0x79;             // 121
    public const byte PGN_SCAN_REPLY = 0xCB;            // 203

    /// <summary>
    /// Build a complete PGN packet with header and CRC.
    /// </summary>
    public static byte[] BuildPacket(byte pgn, byte[] data)
    {
        var packet = new byte[5 + data.Length + 1]; // header(5) + data + crc(1)
        packet[0] = HEADER1;
        packet[1] = HEADER2;
        packet[2] = SOURCE_MODULE;
        packet[3] = pgn;
        packet[4] = (byte)data.Length;
        Array.Copy(data, 0, packet, 5, data.Length);
        packet[^1] = CalculateCrc(packet);
        return packet;
    }

    /// <summary>
    /// Build a hello response packet for a module type.
    /// </summary>
    public static byte[] BuildHelloPacket(byte helloPgn)
    {
        return BuildPacket(helloPgn, new byte[] { helloPgn, helloPgn, 0x05 });
    }

    /// <summary>
    /// Calculate CRC: sum of bytes 2 through N-1.
    /// </summary>
    public static byte CalculateCrc(byte[] packet)
    {
        byte crc = 0;
        for (int i = 2; i < packet.Length - 1; i++)
            crc += packet[i];
        return crc;
    }

    /// <summary>
    /// Validate packet header and CRC.
    /// </summary>
    public static bool IsValidPacket(byte[] data, int length)
    {
        if (length < 6) return false;
        if (data[0] != HEADER1 || data[1] != HEADER2) return false;

        byte crc = 0;
        for (int i = 2; i < length - 1; i++)
            crc += data[i];
        return crc == data[length - 1];
    }

    /// <summary>
    /// Extract PGN number from a valid packet.
    /// </summary>
    public static byte GetPgn(byte[] data) => data[3];

    /// <summary>
    /// Extract data length from a valid packet.
    /// </summary>
    public static byte GetDataLength(byte[] data) => data[4];

    /// <summary>
    /// Parse PGN 254 (AutoSteer data from host).
    /// </summary>
    public static AutoSteerCommand ParseAutoSteerCommand(byte[] data)
    {
        return new AutoSteerCommand
        {
            SpeedKmh = BitConverter.ToInt16(data, 5) / 10.0,
            Status = data[7],
            SteerAngleDeg = BitConverter.ToInt16(data, 8) / 100.0,
            CrossTrackErrorCm = (sbyte)data[10],
            SectionBits1to8 = data[11],
            SectionBits9to16 = data[12],
            IsEngaged = (data[7] & 0x04) != 0,
            IsGpsValid = (data[7] & 0x08) != 0
        };
    }

    /// <summary>
    /// Parse PGN 238 (Machine config from host).
    /// </summary>
    public static MachineConfigPacket ParseMachineConfig(byte[] data)
    {
        return new MachineConfigPacket
        {
            RaiseTime = data[5],
            LowerTime = data[6],
            EnableHyd = data[7],
            Set0 = data[8],
            User1 = data[9],
            User2 = data[10],
            User3 = data[11],
            User4 = data[12]
        };
    }

    /// <summary>
    /// Parse PGN 236 (Machine pin config from host).
    /// </summary>
    public static MachinePinConfigPacket ParseMachinePinConfig(byte[] data)
    {
        var pins = new byte[24];
        Array.Copy(data, 5, pins, 0, Math.Min(24, data.Length - 6));
        return new MachinePinConfigPacket { PinAssignments = pins };
    }

    /// <summary>
    /// Parse PGN 239 (Machine data from host).
    /// </summary>
    public static MachineCommand ParseMachineCommand(byte[] data)
    {
        return new MachineCommand
        {
            UTurnState = data[5],
            SpeedByte = data[6],
            HydLiftState = data[7],
            TramState = data[8],
            GeoStopState = data[9],
            SectionBits1to8 = data[11],
            SectionBits9to16 = data[12]
        };
    }
}

public struct AutoSteerCommand
{
    public double SpeedKmh;
    public byte Status;
    public double SteerAngleDeg;
    public sbyte CrossTrackErrorCm;
    public byte SectionBits1to8;
    public byte SectionBits9to16;
    public bool IsEngaged;
    public bool IsGpsValid;
}

public struct MachineCommand
{
    public byte UTurnState;
    public byte SpeedByte;
    public byte HydLiftState;
    public byte TramState;
    public byte GeoStopState;
    public byte SectionBits1to8;
    public byte SectionBits9to16;
    public ushort SectionBits => (ushort)(SectionBits1to8 | (SectionBits9to16 << 8));
}

public struct MachineConfigPacket
{
    public byte RaiseTime;
    public byte LowerTime;
    public byte EnableHyd;
    public byte Set0;
    public byte User1;
    public byte User2;
    public byte User3;
    public byte User4;
    public bool InvertRelay => (Set0 & 0x01) != 0;
    public bool HydEnabled => (Set0 & 0x02) != 0;
}

public struct MachinePinConfigPacket
{
    public byte[] PinAssignments; // 24 bytes
}

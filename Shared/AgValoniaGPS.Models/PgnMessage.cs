namespace AgValoniaGPS.Models;

/// <summary>
/// PGN (Parameter Group Number) message format used in AgOpenGPS
/// Standard format: [0x80, 0x81, source, PGN, length, ...data..., CRC]
/// </summary>
public class PgnMessage
{
    /// <summary>
    /// Message header byte 1 (always 0x80)
    /// </summary>
    public const byte HEADER1 = 0x80;

    /// <summary>
    /// Message header byte 2 (always 0x81)
    /// </summary>
    public const byte HEADER2 = 0x81;

    /// <summary>
    /// Source address (0x7F for AgIO/AgOpenGPS)
    /// </summary>
    public byte Source { get; set; } = 0x7F;

    /// <summary>
    /// Parameter Group Number - identifies message type
    /// </summary>
    public byte PGN { get; set; }

    /// <summary>
    /// Data length (number of data bytes, not including header/CRC)
    /// </summary>
    public byte Length { get; set; }

    /// <summary>
    /// Message data payload
    /// </summary>
    public byte[] Data { get; set; } = Array.Empty<byte>();

    /// <summary>
    /// CRC checksum
    /// </summary>
    public byte CRC { get; set; }

    /// <summary>
    /// Convert PGN message to byte array for transmission
    /// </summary>
    public byte[] ToBytes()
    {
        byte[] message = new byte[5 + Data.Length];
        message[0] = HEADER1;
        message[1] = HEADER2;
        message[2] = Source;
        message[3] = PGN;
        message[4] = Length;
        Array.Copy(Data, 0, message, 5, Data.Length);
        message[^1] = CalculateCRC(message);
        return message;
    }

    /// <summary>
    /// Parse byte array into PGN message
    /// </summary>
    public static PgnMessage? FromBytes(byte[] data)
    {
        if (data.Length < 6) return null;
        if (data[0] != HEADER1 || data[1] != HEADER2) return null;

        var message = new PgnMessage
        {
            Source = data[2],
            PGN = data[3],
            Length = data[4],
            Data = new byte[data[4]]
        };

        if (data.Length < 5 + message.Length + 1) return null;

        Array.Copy(data, 5, message.Data, 0, message.Length);
        message.CRC = data[5 + message.Length];

        return message;
    }

    /// <summary>
    /// Calculate CRC checksum (simple XOR for now)
    /// </summary>
    private byte CalculateCRC(byte[] message)
    {
        byte crc = 0;
        for (int i = 2; i < message.Length - 1; i++)
        {
            crc += message[i];
        }
        return crc;
    }

    /// <summary>
    /// Validate message CRC
    /// </summary>
    public bool IsValidCRC()
    {
        var message = ToBytes();
        return message[^1] == CRC;
    }
}

/// <summary>
/// Common PGN numbers used in AgOpenGPS
/// </summary>
public static class PgnNumbers
{
    /// <summary>
    /// Hello/ping from AutoSteer module (0x7E = 126)
    /// </summary>
    public const byte HELLO_FROM_AUTOSTEER = 126;

    /// <summary>
    /// Hello/ping from Machine module (0x7B = 123)
    /// </summary>
    public const byte HELLO_FROM_MACHINE = 123;

    /// <summary>
    /// Hello/ping from IMU module (0x79 = 121)
    /// </summary>
    public const byte HELLO_FROM_IMU = 121;

    /// <summary>
    /// Communication check / Hello from AgIO (0xC8 = 200)
    /// </summary>
    public const byte HELLO_FROM_AGIO = 200;

    /// <summary>
    /// Scan reply (0xCB = 203)
    /// </summary>
    public const byte SCAN_REPLY = 203;

    /// <summary>
    /// AutoSteer data (0xFD = 253)
    /// </summary>
    public const byte AUTOSTEER_DATA = 253;

    /// <summary>
    /// AutoSteer data alternate (0xFE = 254)
    /// </summary>
    public const byte AUTOSTEER_DATA2 = 254;

    /// <summary>
    /// Steer settings (0xFC = 252)
    /// </summary>
    public const byte STEER_SETTINGS = 252;

    /// <summary>
    /// Sensor data FROM module (pressure/current) (0xFA = 250)
    /// </summary>
    public const byte SENSOR_DATA = 250;

    /// <summary>
    /// Steer config (0xFB = 251)
    /// </summary>
    public const byte STEER_CONFIG = 251;

    /// <summary>
    /// Machine PGN (0xEF = 239)
    /// </summary>
    public const byte MACHINE_DATA = 239;

    /// <summary>
    /// Machine config (0xEE = 238)
    /// </summary>
    public const byte MACHINE_CONFIG = 238;

    /// <summary>
    /// Machine config 2 (0xEC = 236)
    /// </summary>
    public const byte MACHINE_CONFIG2 = 236;

    /// <summary>
    /// Symmetric Sections / Zones (0xE5 = 229)
    /// </summary>
    public const byte SECTION_ZONES = 229;
}
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
using System.Net;
using System.Threading.Tasks;

namespace AgValoniaGPS.Services.Interfaces;

/// <summary>
/// Service for UDP communication with Teensy modules (Steer, Machine, IMU, GPS)
/// Based on AgIO UDP networking pattern - eliminates USB serial connections
/// </summary>
public interface IUdpCommunicationService
{
    /// <summary>
    /// Event fired when data is received from any module
    /// </summary>
    event EventHandler<UdpDataReceivedEventArgs>? DataReceived;

    /// <summary>
    /// Event fired when module connection status changes
    /// </summary>
    event EventHandler<ModuleConnectionEventArgs>? ModuleConnectionChanged;

    /// <summary>
    /// Whether UDP network is connected
    /// </summary>
    bool IsConnected { get; }

    /// <summary>
    /// Local IP address being used
    /// </summary>
    string? LocalIPAddress { get; }

    /// <summary>
    /// Start UDP communication service
    /// Port 9999 for receiving from modules
    /// </summary>
    Task StartAsync();

    /// <summary>
    /// Stop UDP communication service
    /// </summary>
    Task StopAsync();

    /// <summary>
    /// Send data to modules via UDP broadcast
    /// Default endpoint: [subnet].255:8888
    /// </summary>
    void SendToModules(byte[] data);

    /// <summary>
    /// Send hello/ping packet to check module connections
    /// PGN 200 (0xC8) - communication check
    /// </summary>
    void SendHelloPacket();

    /// <summary>
    /// Check if module hello is OK (2 second timeout)
    /// </summary>
    bool IsModuleHelloOk(ModuleType moduleType);

    /// <summary>
    /// Check if module data is flowing (50Hz for Steer/Machine, 10Hz for IMU)
    /// </summary>
    bool IsModuleDataOk(ModuleType moduleType);
}

/// <summary>
/// Types of modules that can connect via UDP
/// </summary>
public enum ModuleType
{
    AutoSteer,
    Machine,
    IMU,
    GPS
}

/// <summary>
/// Event args for UDP data received
/// </summary>
public class UdpDataReceivedEventArgs : EventArgs
{
    public byte[] Data { get; set; } = Array.Empty<byte>();
    public IPEndPoint RemoteEndPoint { get; set; } = new IPEndPoint(IPAddress.Any, 0);
    public byte PGN { get; set; } // Parameter Group Number (data[3])
    public DateTime Timestamp { get; set; } = DateTime.Now;
}

/// <summary>
/// Event args for module connection status change
/// </summary>
public class ModuleConnectionEventArgs : EventArgs
{
    public ModuleType ModuleType { get; set; }
    public bool IsConnected { get; set; }
    public string? IPAddress { get; set; }
}
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
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using AgValoniaGPS.Models;
using AgValoniaGPS.Services.Interfaces;

namespace AgValoniaGPS.Services;

/// <summary>
/// UDP communication service for Teensy modules
/// Eliminates unreliable USB serial connections
/// Port 9999 for module communication
///
/// Zero-copy GPS path: When NMEA data arrives, AutoSteerService.ProcessGpsBuffer
/// is called directly with the receive buffer (no copy). This is the critical
/// path for low-latency GPS-to-PGN processing.
/// </summary>
public class UdpCommunicationService : IUdpCommunicationService, IDisposable
{
    public event EventHandler<UdpDataReceivedEventArgs>? DataReceived;
    public event EventHandler<ModuleConnectionEventArgs>? ModuleConnectionChanged;

    private Socket? _udpSocket;
    private readonly byte[] _receiveBuffer = new byte[1024];
    private EndPoint _remoteEndPoint = new IPEndPoint(IPAddress.Any, 0);
    private CancellationTokenSource? _cancellationTokenSource;
    private bool _isDisposed;

    // AutoSteer service for zero-copy GPS processing
    private IAutoSteerService? _autoSteerService;

    // Module broadcast endpoint (e.g., 192.168.5.255:8888)
    private IPEndPoint? _moduleEndpoint;

    // Hello packet: [0x80, 0x81, 0x7F, 200, 3, 56, 0, 0, CRC]
    private readonly byte[] _helloPacket = { 0x80, 0x81, 0x7F, 200, 3, 56, 0, 0, 0x47 };

    // Module connection tracking - Hello responses (2 second timeout)
    private DateTime _lastHelloFromAutoSteer = DateTime.MinValue;
    private DateTime _lastHelloFromMachine = DateTime.MinValue;
    private DateTime _lastHelloFromIMU = DateTime.MinValue;

    // Module data tracking - Data flow (50ms for 50Hz modules, 250ms for 10Hz GPS/IMU)
    private DateTime _lastDataFromAutoSteer = DateTime.MinValue;
    private DateTime _lastDataFromMachine = DateTime.MinValue;
    private DateTime _lastDataFromIMU = DateTime.MinValue;

    private const int HELLO_TIMEOUT_MS = 2000; // 2 seconds for hello response
    private const int DATA_TIMEOUT_STEER_MACHINE_MS = 100; // 50Hz data = 20ms cycle, allow 100ms
    private const int DATA_TIMEOUT_IMU_MS = 300; // 10Hz data = 100ms cycle, allow 300ms

    public bool IsConnected { get; private set; }
    public string? LocalIPAddress { get; private set; }

    /// <summary>
    /// Register the AutoSteer service for zero-copy GPS processing.
    /// When NMEA data arrives, it will be passed directly to the AutoSteer
    /// service without copying, achieving minimum latency.
    /// </summary>
    public void SetAutoSteerService(IAutoSteerService autoSteerService)
    {
        _autoSteerService = autoSteerService;
    }

    public async Task StartAsync()
    {
        if (IsConnected) return;

        try
        {
            // Get local IP address
            LocalIPAddress = GetLocalIPAddress();

            // Create UDP socket on port 9999
            _udpSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            _udpSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Broadcast, true);
            _udpSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);

            // Reduce receive buffer to minimize packet buffering/delay
            _udpSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReceiveBuffer, 8192);

            _udpSocket.Bind(new IPEndPoint(IPAddress.Any, 9999));

            // Set up module broadcast endpoint (default: 192.168.5.255:8888)
            _moduleEndpoint = new IPEndPoint(IPAddress.Parse("192.168.5.255"), 8888);

            IsConnected = true;

            // Start receiving
            _cancellationTokenSource = new CancellationTokenSource();
            _ = Task.Run(() => ReceiveLoop(_cancellationTokenSource.Token));

            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            IsConnected = false;
            throw new Exception($"Failed to start UDP communication: {ex.Message}", ex);
        }
    }

    public async Task StopAsync()
    {
        if (!IsConnected) return;

        _cancellationTokenSource?.Cancel();
        _udpSocket?.Close();
        _udpSocket?.Dispose();
        _udpSocket = null;
        IsConnected = false;

        await Task.CompletedTask;
    }

    public void SendToModules(byte[] data)
    {
        if (!IsConnected || _udpSocket == null || _moduleEndpoint == null) return;

        try
        {
            _udpSocket.BeginSendTo(data, 0, data.Length, SocketFlags.None, _moduleEndpoint,
                ar =>
                {
                    try
                    {
                        _udpSocket?.EndSendTo(ar);
                    }
                    catch { }
                }, null);
        }
        catch { }
    }

    public void SendHelloPacket()
    {
        SendToModules(_helloPacket);
    }

    public bool IsModuleHelloOk(ModuleType moduleType)
    {
        var now = DateTime.Now;
        var timeout = TimeSpan.FromMilliseconds(HELLO_TIMEOUT_MS);

        return moduleType switch
        {
            ModuleType.AutoSteer => (now - _lastHelloFromAutoSteer) < timeout,
            ModuleType.Machine => (now - _lastHelloFromMachine) < timeout,
            ModuleType.IMU => (now - _lastHelloFromIMU) < timeout,
            _ => false
        };
    }

    public bool IsModuleDataOk(ModuleType moduleType)
    {
        var now = DateTime.Now;

        return moduleType switch
        {
            ModuleType.AutoSteer => (now - _lastDataFromAutoSteer).TotalMilliseconds < DATA_TIMEOUT_STEER_MACHINE_MS,
            ModuleType.Machine => (now - _lastDataFromMachine).TotalMilliseconds < DATA_TIMEOUT_STEER_MACHINE_MS,
            ModuleType.IMU => (now - _lastDataFromIMU).TotalMilliseconds < DATA_TIMEOUT_IMU_MS,
            _ => false
        };
    }

    private async Task ReceiveLoop(CancellationToken cancellationToken)
    {
        // Start the first receive operation
        if (_udpSocket != null)
        {
            try
            {
                _udpSocket.BeginReceiveFrom(_receiveBuffer, 0, _receiveBuffer.Length, SocketFlags.None,
                    ref _remoteEndPoint, ReceiveCallback, null);
            }
            catch { }
        }

        // Keep the task alive until cancellation
        while (!cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(100, cancellationToken);
        }
    }

    private void ReceiveCallback(IAsyncResult ar)
    {
        if (_udpSocket == null) return;

        try
        {
            int bytesReceived = _udpSocket.EndReceiveFrom(ar, ref _remoteEndPoint);

            if (bytesReceived > 0)
            {
                // ZERO-COPY PATH: Check if this is NMEA data for AutoSteer
                // Process directly from receive buffer before any copying
                if (_receiveBuffer[0] == (byte)'$' && _autoSteerService != null)
                {
                    // Direct zero-copy call - this is the critical low-latency path
                    // GPS → Parse → Guidance → PGN all happen here before we continue
                    _autoSteerService.ProcessGpsBuffer(_receiveBuffer, bytesReceived);
                }

                // Now copy for other consumers (events, logging, etc.)
                byte[] data = new byte[bytesReceived];
                Array.Copy(_receiveBuffer, data, bytesReceived);

                ProcessReceivedData(data, (IPEndPoint)_remoteEndPoint);
            }
        }
        catch { }

        // IMMEDIATELY start the next receive operation - this is the key fix!
        if (_udpSocket != null)
        {
            try
            {
                _udpSocket.BeginReceiveFrom(_receiveBuffer, 0, _receiveBuffer.Length, SocketFlags.None,
                    ref _remoteEndPoint, ReceiveCallback, null);
            }
            catch { }
        }
    }

    private void ProcessReceivedData(byte[] data, IPEndPoint remoteEndPoint)
    {
        // Check if this is a binary PGN message or text NMEA sentence
        if (data.Length >= 2 && data[0] == PgnMessage.HEADER1 && data[1] == PgnMessage.HEADER2)
        {
            // Binary PGN message
            if (data.Length < 6) return;

            byte pgn = data[3];

            // Track module connections based on hello messages
            UpdateModuleConnection(pgn);

            // Fire event
            DataReceived?.Invoke(this, new UdpDataReceivedEventArgs
            {
                Data = data,
                RemoteEndPoint = remoteEndPoint,
                PGN = pgn,
                Timestamp = DateTime.Now
            });
        }
        else if (data.Length > 0 && data[0] == (byte)'$')
        {
            // Text NMEA sentence (starts with $)
            // Fire event with PGN 0 to indicate NMEA text
            DataReceived?.Invoke(this, new UdpDataReceivedEventArgs
            {
                Data = data,
                RemoteEndPoint = remoteEndPoint,
                PGN = 0, // Special PGN for NMEA text
                Timestamp = DateTime.Now
            });
        }
    }

    private void UpdateModuleConnection(byte pgn)
    {
        var now = DateTime.Now;

        // Track ALL PGNs as data - if we're getting any PGN from a module, it's sending data
        switch (pgn)
        {
            // AutoSteer PGNs
            case PgnNumbers.HELLO_FROM_AUTOSTEER: // 126
                _lastHelloFromAutoSteer = now;
                System.Diagnostics.Debug.WriteLine($"AutoSteer HELLO received at {now:HH:mm:ss.fff}");
                break;

            case PgnNumbers.SENSOR_DATA:          // 250 - Sensor data from module
            case PgnNumbers.AUTOSTEER_DATA:       // 253 - Regular data
            case PgnNumbers.AUTOSTEER_DATA2:      // 254
            case PgnNumbers.STEER_SETTINGS:       // 252
            case PgnNumbers.STEER_CONFIG:         // 251
                _lastDataFromAutoSteer = now;
                break;

            // Machine PGNs (receive-only, only Hello matters)
            case PgnNumbers.HELLO_FROM_MACHINE:  // 123
                _lastHelloFromMachine = now;
                System.Diagnostics.Debug.WriteLine($"Machine HELLO received at {now:HH:mm:ss.fff}");
                break;

            // IMU PGNs (only Hello matters - data only sent when active)
            case PgnNumbers.HELLO_FROM_IMU: // 121
                _lastHelloFromIMU = now;
                System.Diagnostics.Debug.WriteLine($"IMU HELLO received at {now:HH:mm:ss.fff}");
                break;

            default:
                // Log unknown PGNs to help debug
                System.Diagnostics.Debug.WriteLine($"Unknown PGN {pgn} (0x{pgn:X2}) received");
                break;
        }
    }

    private string? GetLocalIPAddress()
    {
        try
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    return ip.ToString();
                }
            }
        }
        catch { }

        return null;
    }

    public void Dispose()
    {
        if (_isDisposed) return;

        StopAsync().Wait();
        _cancellationTokenSource?.Dispose();
        _isDisposed = true;
        GC.SuppressFinalize(this);
    }
}
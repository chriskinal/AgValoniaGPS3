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
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using AgValoniaGPS.Models;
using AgValoniaGPS.Models.Timing;
using AgValoniaGPS.Services.Interfaces;

namespace AgValoniaGPS.Services;

/// <summary>
/// NTRIP client for receiving RTK correction data from base station
/// Forwards RTCM3 corrections to GPS module via UDP port 2233
/// Based on AgIO NTRIP implementation
/// </summary>
public class NtripClientService : INtripClientService, IDisposable
{
    public event EventHandler<NtripConnectionEventArgs>? ConnectionStatusChanged;
    public event EventHandler<RtcmDataReceivedEventArgs>? RtcmDataReceived;

    private Socket? _tcpSocket;
    private Socket? _udpSocket;
    private readonly byte[] _receiveBuffer = new byte[4096];
    private readonly List<byte> _headerBuffer = new List<byte>();
    private bool _headerDumped = false;
    private CancellationTokenSource? _cancellationTokenSource;
    private NtripConfiguration? _config;
    private bool _isDisposed;

    private IPEndPoint? _rtcmUdpEndpoint;
    private Timer? _ggaTimer;
    private Timer? _rtcmForwardTimer;
    private Timer? _watchdogTimer;
    private readonly Queue<byte> _rtcmQueue = new Queue<byte>();
    private readonly object _queueLock = new object();
    // Bumped from the AgIO-default 256 to drain caster bursts faster. The
    // caster periodically pauses for ~10–20 s and then releases the buffered
    // RTCM in one burst (#334). At 256 B per 50 ms tick the AiO catches up
    // at ~5 KB/s — a 30 KB burst takes ~6 s. At 1024 B per tick the same
    // burst clears in ~1.5 s. 1024 B is well under the typical 1500 B UDP
    // MTU, so no fragmentation risk on the local LAN.
    private const int RTCM_PACKET_SIZE = 1024;

    // ── Stall watchdog ────────────────────────────────────────────────────
    // Last time bytes arrived from the caster. Updated by ForwardRtcmData.
    // Watchdog fires from _watchdogTimer; if no bytes for >= reconnect
    // threshold we disconnect and reconnect (the connection may be silently
    // half-open after the caster's TCP keep-alive timeout). The shorter
    // warn threshold gives operators a log line before reconnect kicks in.
    private long _lastRtcmReceivedTimestamp;
    private const double WATCHDOG_TIMER_INTERVAL_MS = 5000.0;
    private const double WATCHDOG_WARN_SECONDS = 30.0;
    private const double WATCHDOG_RECONNECT_SECONDS = 60.0;
    private bool _watchdogWarnLogged;
    private int _reconnectInProgress;  // 0/1 flag, atomic via Interlocked

    // Cap header accumulation to prevent memory-exhaustion DoS from a
    // malicious caster — or a MITM on the path — streaming bytes without
    // the \r\n\r\n terminator. Real caster headers are well under 1 KB;
    // 8 KiB is generous. See issue #286 / threat model finding F2.
    private const int MaxHeaderBytes = 8 * 1024;
    private readonly IGpsService _gpsService;
    private readonly ILogger<NtripClientService> _logger;

    public bool IsConnected { get; private set; }
    public ulong TotalBytesReceived { get; private set; }

    public NtripClientService(IGpsService gpsService, ILogger<NtripClientService> logger)
    {
        _gpsService = gpsService;
        _logger = logger;
    }

    public async Task ConnectAsync(NtripConfiguration config)
    {
        if (IsConnected)
        {
            await DisconnectAsync();
        }

        _config = config;

        try
        {
            // Create UDP socket for forwarding RTCM data to GPS module (port 2233)
            _udpSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            _udpSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Broadcast, true);
            _udpSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);

            // Set up RTCM forward endpoint (subnet.255:2233)
            _rtcmUdpEndpoint = new IPEndPoint(
                IPAddress.Parse($"{config.SubnetAddress}.255"),
                config.UdpForwardPort);

            // Create TCP socket for NTRIP caster connection
            _tcpSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            _tcpSocket.NoDelay = true;

            // Resolve hostname if needed
            IPAddress? casterIP;
            if (!IPAddress.TryParse(config.CasterAddress, out casterIP))
            {
                var addresses = await Dns.GetHostAddressesAsync(config.CasterAddress);
                casterIP = addresses.Length > 0 ? addresses[0] : throw new Exception("Could not resolve hostname");
            }

            // Connect to NTRIP caster
            await _tcpSocket.ConnectAsync(new IPEndPoint(casterIP, config.CasterPort));

            // Clear header buffer from any previous connection
            _headerBuffer.Clear();
            _headerDumped = false;

            // Send NTRIP request
            await SendNtripRequestAsync();

            // Start receiving RTCM data
            _cancellationTokenSource = new CancellationTokenSource();
            _ = Task.Run(() => ReceiveLoop(_cancellationTokenSource.Token));

            // Start GGA timer if interval > 0
            if (config.GgaIntervalSeconds > 0)
            {
                _ggaTimer = new Timer(
                    GgaTimerCallback,
                    null,
                    TimeSpan.FromSeconds(5), // First GGA after 5 seconds
                    TimeSpan.FromSeconds(config.GgaIntervalSeconds));
            }

            // Start RTCM forward timer (50ms interval like AgIO)
            _rtcmForwardTimer = new Timer(
                RtcmForwardTimerCallback,
                null,
                TimeSpan.FromMilliseconds(50),
                TimeSpan.FromMilliseconds(50));

            // Stall watchdog: triggers reconnect if no RTCM has arrived for
            // WATCHDOG_RECONNECT_SECONDS. The caster's normal hiccups last
            // ~10–20 s (#334 capture), so a 60 s threshold won't false-fire
            // on those but does catch the case where the connection dies
            // silently (the 302 s cutoff in the original report).
            _lastRtcmReceivedTimestamp = Clock.Current.GetTimestamp();
            _watchdogWarnLogged = false;
            _watchdogTimer = new Timer(
                WatchdogTimerCallback,
                null,
                TimeSpan.FromMilliseconds(WATCHDOG_TIMER_INTERVAL_MS),
                TimeSpan.FromMilliseconds(WATCHDOG_TIMER_INTERVAL_MS));

            IsConnected = true;
            TotalBytesReceived = 0;

            ConnectionStatusChanged?.Invoke(this, new NtripConnectionEventArgs
            {
                IsConnected = true,
                Message = $"Connected to {config.CasterAddress}:{config.CasterPort}/{config.MountPoint}"
            });
        }
        catch (Exception ex)
        {
            IsConnected = false;
            ConnectionStatusChanged?.Invoke(this, new NtripConnectionEventArgs
            {
                IsConnected = false,
                Message = $"Connection failed: {ex.Message}"
            });
            throw;
        }
    }

    public async Task DisconnectAsync()
    {
        if (!IsConnected) return;

        _ggaTimer?.Dispose();
        _ggaTimer = null;

        _rtcmForwardTimer?.Dispose();
        _rtcmForwardTimer = null;

        _watchdogTimer?.Dispose();
        _watchdogTimer = null;

        _cancellationTokenSource?.Cancel();

        _tcpSocket?.Close();
        _tcpSocket?.Dispose();
        _tcpSocket = null;

        _udpSocket?.Close();
        _udpSocket?.Dispose();
        _udpSocket = null;

        IsConnected = false;

        ConnectionStatusChanged?.Invoke(this, new NtripConnectionEventArgs
        {
            IsConnected = false,
            Message = "Disconnected"
        });

        await Task.CompletedTask;
    }

    private async Task SendNtripRequestAsync()
    {
        if (_tcpSocket == null || _config == null) return;

        // Build NTRIP request (HTTP GET with Basic Auth)
        // Use NTRIP 1.0 compatible format (simpler, more widely supported)
        var credentials = Convert.ToBase64String(
            Encoding.ASCII.GetBytes($"{_config.Username}:{_config.Password}"));

        // Build request string manually with explicit \r\n to ensure correct formatting
        var request = new StringBuilder();
        request.Append($"GET /{_config.MountPoint} HTTP/1.1\r\n");
        request.Append($"Host: {_config.CasterAddress}\r\n");
        request.Append("User-Agent: NTRIP AgValoniaGPS/1.0\r\n");
        request.Append($"Authorization: Basic {credentials}\r\n");
        request.Append("Accept: */*\r\n");
        request.Append("Connection: keep-alive\r\n");
        request.Append("\r\n");

        string requestStr = request.ToString();
        byte[] requestBytes = Encoding.ASCII.GetBytes(requestStr);
        await _tcpSocket.SendAsync(requestBytes, SocketFlags.None);
    }

    private async Task ReceiveLoop(CancellationToken cancellationToken)
    {
        bool headerReceived = false;

        while (!cancellationToken.IsCancellationRequested && _tcpSocket != null)
        {
            try
            {
                int bytesReceived = await _tcpSocket.ReceiveAsync(
                    new ArraySegment<byte>(_receiveBuffer),
                    SocketFlags.None,
                    cancellationToken);

                if (bytesReceived > 0)
                {
                    // First response is HTTP header - check for success
                    if (!headerReceived)
                    {
                        // Bail if the header has grown past the cap without a
                        // terminator. Without this an unbounded caster could
                        // OOM the tablet by streaming bytes forever.
                        if (_headerBuffer.Count + bytesReceived > MaxHeaderBytes)
                        {
                            _logger.LogWarning(
                                "NTRIP header exceeded {Max} bytes without \\r\\n\\r\\n terminator; disconnecting",
                                MaxHeaderBytes);
                            await DisconnectAsync();
                            return;
                        }

                        // Accumulate header bytes
                        for (int i = 0; i < bytesReceived; i++)
                        {
                            _headerBuffer.Add(_receiveBuffer[i]);
                        }

                        // Dump header bytes once for debugging
                        if (!_headerDumped && _headerBuffer.Count >= 10)
                        {
                            _headerDumped = true;
                            int dumpSize = Math.Min(100, _headerBuffer.Count);
                            string headerPreview = Encoding.ASCII.GetString(_headerBuffer.Take(dumpSize).ToArray());
                            _logger.LogDebug("Response header: {Header}", headerPreview.Replace("\r\n", " "));
                        }

                        // Find header/body boundary
                        // ICY protocol uses single \r\n, HTTP uses \r\n\r\n
                        int headerEnd = -1;
                        int dataStart = -1;

                        // First check for ICY single line response (just \r\n)
                        for (int i = 0; i < _headerBuffer.Count - 1; i++)
                        {
                            if (_headerBuffer[i] == '\r' && _headerBuffer[i + 1] == '\n')
                            {
                                // Check if this looks like ICY response
                                if (i < 50)
                                {
                                    string testHeader = Encoding.ASCII.GetString(_headerBuffer.ToArray(), 0, i);
                                    if (testHeader.StartsWith("ICY 200"))
                                    {
                                        headerEnd = i;
                                        dataStart = i + 2; // After \r\n
                                        break;
                                    }
                                }

                                // Check for HTTP \r\n\r\n
                                if (i + 3 < _headerBuffer.Count &&
                                    _headerBuffer[i + 2] == '\r' && _headerBuffer[i + 3] == '\n')
                                {
                                    headerEnd = i;
                                    dataStart = i + 4; // After \r\n\r\n
                                    break;
                                }
                            }
                        }

                        if (headerEnd >= 0)
                        {
                            // Parse header as ASCII string
                            string response = Encoding.ASCII.GetString(_headerBuffer.ToArray(), 0, headerEnd);

                            if (response.Contains("200 OK") || response.Contains("ICY 200"))
                            {
                                headerReceived = true;
                                _logger.LogInformation("Connected and authorized, receiving RTCM data");

                                // Forward any RTCM data after header
                                if (dataStart < _headerBuffer.Count)
                                {
                                    int rtcmBytes = _headerBuffer.Count - dataStart;
                                    byte[] rtcmData = new byte[rtcmBytes];
                                    _headerBuffer.CopyTo(dataStart, rtcmData, 0, rtcmBytes);
                                    ForwardRtcmData(rtcmData);
                                }

                                // Clear header buffer
                                _headerBuffer.Clear();
                            }
                            else
                            {
                                _logger.LogWarning("Authorization failed or bad response: {Response}", response);
                                await DisconnectAsync();
                                return;
                            }
                        }
                        // If no complete header yet, accumulate more data
                    }
                    else
                    {
                        // All subsequent data is RTCM3 corrections - forward as raw bytes
                        byte[] rtcmData = new byte[bytesReceived];
                        Array.Copy(_receiveBuffer, rtcmData, bytesReceived);
                        ForwardRtcmData(rtcmData);
                    }
                }
                else
                {
                    // Connection closed by server
                    _logger.LogInformation("Connection closed by caster");
                    await DisconnectAsync();
                    return;
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Receive error");
                await DisconnectAsync();
                break;
            }
        }
    }

    private void ForwardRtcmData(byte[] rtcmData)
    {
        if (rtcmData.Length == 0)
            return;

        // Enqueue received RTCM bytes for timer-based forwarding (like AgIO)
        lock (_queueLock)
        {
            foreach (byte b in rtcmData)
            {
                _rtcmQueue.Enqueue(b);
            }
        }

        TotalBytesReceived += (ulong)rtcmData.Length;
        Volatile.Write(ref _lastRtcmReceivedTimestamp, Clock.Current.GetTimestamp());
        if (_watchdogWarnLogged)
        {
            // Recovery — clear so the next stall logs a fresh warning.
            _watchdogWarnLogged = false;
        }
    }

    private void RtcmForwardTimerCallback(object? state)
    {
        if (!IsConnected || _udpSocket == null || _rtcmUdpEndpoint == null)
            return;

        lock (_queueLock)
        {
            if (_rtcmQueue.Count == 0)
                return;

            // Limit per-tick chunk to RTCM_PACKET_SIZE (#334).
            int count = Math.Min(_rtcmQueue.Count, RTCM_PACKET_SIZE);
            byte[] packet = new byte[count];

            for (int i = 0; i < count; i++)
            {
                packet[i] = _rtcmQueue.Dequeue();
            }

            try
            {
                // Forward RTCM3 corrections to GPS module via UDP broadcast
                _udpSocket.SendTo(packet, _rtcmUdpEndpoint);

                RtcmDataReceived?.Invoke(this, new RtcmDataReceivedEventArgs
                {
                    BytesReceived = packet.Length
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to forward RTCM data");
            }

            // Clear queue if it gets too large (like AgIO does at 10000 bytes)
            if (_rtcmQueue.Count > 10000)
            {
                _logger.LogWarning("Queue overflow, clearing {ByteCount} bytes", _rtcmQueue.Count);
                _rtcmQueue.Clear();
            }
        }
    }

    private void GgaTimerCallback(object? state)
    {
        if (!IsConnected || _config == null) return;

        try
        {
            string ggaSentence;

            if (_config.UseManualPosition)
            {
                // Use manual position
                ggaSentence = GenerateGgaSentence(
                    _config.ManualLatitude,
                    _config.ManualLongitude,
                    0, // altitude
                    4, // fix quality (RTK fixed)
                    12); // satellites
            }
            else
            {
                // Use GPS position from GpsService
                var gpsData = _gpsService.CurrentData;
                if (gpsData != null && gpsData.IsValid)
                {
                    ggaSentence = GenerateGgaSentence(
                        gpsData.CurrentPosition.Latitude,
                        gpsData.CurrentPosition.Longitude,
                        gpsData.CurrentPosition.Altitude,
                        gpsData.FixQuality,
                        gpsData.SatellitesInUse);
                }
                else
                {
                    // No GPS data available yet - send default position (center of US)
                    // This allows caster to start sending corrections
                    ggaSentence = GenerateGgaSentence(
                        39.8283, // Latitude (Kansas, US)
                        -98.5795, // Longitude
                        0, // altitude
                        1, // fix quality (GPS fix)
                        8); // satellites
                }
            }

            _ = SendGgaSentenceAsync(ggaSentence);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"NTRIP: GGA timer error: {ex.Message}");
        }
    }

    private void WatchdogTimerCallback(object? state)
    {
        if (!IsConnected) return;

        long now = Clock.Current.GetTimestamp();
        long last = Volatile.Read(ref _lastRtcmReceivedTimestamp);
        double secondsSinceData = Clock.Current.ElapsedMs(last, now) / 1000.0;

        // Periodic health line — once per WATCHDOG_TIMER_INTERVAL_MS regardless
        // of state. Helps operators distinguish "caster paused" (warn line below
        // hasn't fired but data flow is choppy) from "AgValonia broke".
        _logger.LogInformation(
            "[NTRIP] last RTCM {Sec:F1}s ago, total {Bytes} bytes",
            secondsSinceData, TotalBytesReceived);

        if (secondsSinceData >= WATCHDOG_RECONNECT_SECONDS)
        {
            // Reconnect once; serialize via Interlocked so two ticks can't
            // double-fire the reconnect (timer callbacks share the thread pool
            // and a slow reconnect could overlap the next tick).
            if (Interlocked.CompareExchange(ref _reconnectInProgress, 1, 0) == 0)
            {
                _logger.LogWarning(
                    "[NTRIP] no RTCM for {Sec:F1}s — forcing reconnect (#334)",
                    secondsSinceData);
                _ = ReconnectAsync();
            }
        }
        else if (secondsSinceData >= WATCHDOG_WARN_SECONDS && !_watchdogWarnLogged)
        {
            _watchdogWarnLogged = true;
            _logger.LogWarning(
                "[NTRIP] no RTCM for {Sec:F1}s — caster paused or connection stalled",
                secondsSinceData);
        }
    }

    private async Task ReconnectAsync()
    {
        try
        {
            var config = _config;
            if (config == null) return;

            await DisconnectAsync();
            // Brief pause so any TCP teardown completes before the new SYN.
            await Task.Delay(500);
            await ConnectAsync(config);
            _logger.LogInformation("[NTRIP] reconnect complete");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[NTRIP] reconnect failed");
        }
        finally
        {
            Interlocked.Exchange(ref _reconnectInProgress, 0);
        }
    }

    public async Task SendGgaSentenceAsync(string ggaSentence)
    {
        if (!IsConnected || _tcpSocket == null) return;

        try
        {
            byte[] ggaBytes = Encoding.ASCII.GetBytes(ggaSentence + "\r\n");
            await _tcpSocket.SendAsync(ggaBytes, SocketFlags.None);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send GGA");
        }
    }

    private string GenerateGgaSentence(double lat, double lon, double alt, int fixQuality, int sats)
    {
        // Convert decimal degrees to NMEA format (DDMM.MMMM)
        double latDeg = Math.Abs(lat);
        int latDegrees = (int)latDeg;
        double latMinutes = (latDeg - latDegrees) * 60.0;
        string latStr = $"{latDegrees:00}{latMinutes:00.0000}";
        string latDir = lat >= 0 ? "N" : "S";

        double lonDeg = Math.Abs(lon);
        int lonDegrees = (int)lonDeg;
        double lonMinutes = (lonDeg - lonDegrees) * 60.0;
        string lonStr = $"{lonDegrees:000}{lonMinutes:00.0000}";
        string lonDir = lon >= 0 ? "E" : "W";

        // Get UTC time
        DateTime utc = DateTime.UtcNow;
        string timeStr = utc.ToString("HHmmss.ff", CultureInfo.InvariantCulture);

        // Build GGA sentence (without checksum yet)
        string gga = $"GPGGA,{timeStr},{latStr},{latDir},{lonStr},{lonDir},{fixQuality},{sats:00},1.0,{alt:F1},M,0.0,M,,";

        // Calculate checksum (XOR of all characters between $ and *)
        byte checksum = 0;
        foreach (char c in gga)
        {
            checksum ^= (byte)c;
        }

        return $"${gga}*{checksum:X2}";
    }

    public void Dispose()
    {
        if (_isDisposed) return;

        DisconnectAsync().Wait();
        _cancellationTokenSource?.Dispose();
        _isDisposed = true;
        GC.SuppressFinalize(this);
    }
}
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
    private Timer? _watchdogTimer;
    // UDP MTU-safe chunk size for forwarding RTCM to the AiO. RTCM is forwarded
    // directly from the receive callback (no queue, no drain timer) — caching
    // would only deliver stale corrections after a caster pause, and stale RTCM
    // is worse than useless for re-establishing RTK fix. (#334)
    private const int RTCM_PACKET_SIZE = 1024;

    // ── Stall watchdog ────────────────────────────────────────────────────
    // Logs a 5 s health line so operators can distinguish caster pauses from
    // app brokenness. Triggers a reconnect at WATCHDOG_RECONNECT_SECONDS of
    // no data on the wire (catches silent half-open TCP and caster keep-alive
    // timeout cases that the receive loop wouldn't notice on its own).
    private long _lastRtcmReceivedTimestamp;
    private const double WATCHDOG_TIMER_INTERVAL_MS = 5000.0;
    private const double WATCHDOG_RECONNECT_SECONDS = 30.0;

    // ── Reconnect with backoff ────────────────────────────────────────────
    // Triggered by any failure (receive error, send error, watchdog stall).
    // Backoff schedule: 1s, 2s, 4s, 8s, 15s — then hold at 15s indefinitely.
    // Small glitches recover quickly; long outages keep retrying without
    // hammering the caster. Each reconnect is a full TCP teardown + new
    // ConnectAsync (NTRIP is HTTP-style stateless — no session resume).
    private static readonly int[] BackoffScheduleSec = new[] { 1, 2, 4, 8, 15 };
    private int _reconnectInProgress;  // 0/1 flag, atomic via Interlocked
    private CancellationTokenSource? _reconnectCts;

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

            // Stall watchdog — drives the periodic [NTRIP] health log line
            // and triggers a reconnect if no RTCM has arrived for
            // WATCHDOG_RECONNECT_SECONDS (catches silent half-open TCP).
            _lastRtcmReceivedTimestamp = Clock.Current.GetTimestamp();
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

        // Build request string manually with explicit \r\n to ensure correct formatting.
        // NTRIP protocol is invariant — interpolations must not pick up locale formatting.
        var inv = CultureInfo.InvariantCulture;
        var request = new StringBuilder();
        request.Append(inv, $"GET /{_config.MountPoint} HTTP/1.1\r\n");
        request.Append(inv, $"Host: {_config.CasterAddress}\r\n");
        request.Append("User-Agent: NTRIP AgValoniaGPS/1.0\r\n");
        request.Append(inv, $"Authorization: Basic {credentials}\r\n");
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
                    // Connection closed by server (FIN). NTRIP has no resume,
                    // so kick the backoff reconnect loop. (#334)
                    _logger.LogInformation("Connection closed by caster");
                    TriggerReconnect("caster sent FIN");
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
                TriggerReconnect($"receive error: {ex.Message}");
                break;
            }
        }
    }

    private void ForwardRtcmData(byte[] rtcmData)
    {
        if (rtcmData.Length == 0)
            return;

        // Forward each TCP read directly to the AiO in MTU-sized UDP chunks.
        // No queue, no timer — RTCM is real-time data; any buffering would
        // only deliver stale corrections to the AiO after a caster pause and
        // delay the fresh ones that re-establish RTK fix. (#334)
        var udpSocket = _udpSocket;
        var endpoint = _rtcmUdpEndpoint;
        if (udpSocket != null && endpoint != null)
        {
            int offset = 0;
            while (offset < rtcmData.Length)
            {
                int chunkSize = Math.Min(rtcmData.Length - offset, RTCM_PACKET_SIZE);
                byte[] chunk;
                if (offset == 0 && chunkSize == rtcmData.Length)
                {
                    chunk = rtcmData;
                }
                else
                {
                    chunk = new byte[chunkSize];
                    Array.Copy(rtcmData, offset, chunk, 0, chunkSize);
                }
                try
                {
                    udpSocket.SendTo(chunk, endpoint);
                    RtcmDataReceived?.Invoke(this, new RtcmDataReceivedEventArgs
                    {
                        BytesReceived = chunkSize
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to forward RTCM data");
                }
                offset += chunkSize;
            }
        }

        TotalBytesReceived += (ulong)rtcmData.Length;
        Volatile.Write(ref _lastRtcmReceivedTimestamp, Clock.Current.GetTimestamp());
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
        // of state. Operators read this to tell "caster paused" from
        // "AgValonia broke" without needing the debug log.
        _logger.LogInformation(
            "[NTRIP] last RTCM {Sec:F1}s ago, total {Bytes} bytes",
            secondsSinceData, TotalBytesReceived);

        if (secondsSinceData >= WATCHDOG_RECONNECT_SECONDS)
        {
            TriggerReconnect($"no RTCM for {secondsSinceData:F1}s");
        }
    }

    /// <summary>
    /// Kick off the backoff reconnect loop. Idempotent — overlapping
    /// triggers (e.g. watchdog stall + receive error firing in the same
    /// window) are coalesced via the Interlocked guard.
    /// </summary>
    private void TriggerReconnect(string reason)
    {
        if (Interlocked.CompareExchange(ref _reconnectInProgress, 1, 0) != 0)
            return;
        _logger.LogWarning("[NTRIP] reconnect triggered: {Reason}", reason);
        _ = ReconnectWithBackoffAsync();
    }

    private async Task ReconnectWithBackoffAsync()
    {
        var cts = new CancellationTokenSource();
        _reconnectCts = cts;
        var token = cts.Token;
        try
        {
            // Always start with a clean teardown so the next ConnectAsync
            // opens fresh sockets and re-authenticates from scratch — NTRIP
            // is HTTP-style stateless, the caster has dropped our mountpoint
            // subscription anyway.
            try { await DisconnectAsync(); } catch { /* best effort */ }

            for (int attempt = 0; !token.IsCancellationRequested; attempt++)
            {
                int backoffSec = BackoffScheduleSec[Math.Min(attempt, BackoffScheduleSec.Length - 1)];
                _logger.LogInformation(
                    "[NTRIP] reconnect attempt {Attempt} after {Sec}s backoff",
                    attempt + 1, backoffSec);
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(backoffSec), token);
                }
                catch (OperationCanceledException) { return; }

                var config = _config;
                if (config == null) return;
                try
                {
                    await ConnectAsync(config);
                    _logger.LogInformation(
                        "[NTRIP] reconnect succeeded on attempt {Attempt}",
                        attempt + 1);
                    return;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(
                        "[NTRIP] reconnect attempt {Attempt} failed: {Msg}",
                        attempt + 1, ex.Message);
                }
            }
        }
        finally
        {
            Interlocked.Exchange(ref _reconnectInProgress, 0);
            if (ReferenceEquals(_reconnectCts, cts))
                _reconnectCts = null;
            cts.Dispose();
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
            // Send failures usually mean the TCP path is broken even if the
            // receive loop hasn't noticed yet. Kick the reconnect loop. (#334)
            _logger.LogError(ex, "Failed to send GGA");
            TriggerReconnect($"GGA send failed: {ex.Message}");
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
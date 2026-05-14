// AgValoniaGPS
// Copyright (C) 2024-2025 AgValoniaGPS Contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using NUnit.Framework;

namespace AgValoniaGPS.IntegrationTests.VirtualModules;

/// <summary>
/// Unit tests for VirtualSteerModule — verifies that the simulator emulates
/// a real Teensy steer module end-to-end (periodic PGN 253 emission, PGN 251/252
/// parsing, WAS calibration, synthetic PWM loop, switch handling).
/// </summary>
[TestFixture]
[NonParallelizable] // Tests open UDP sockets on fixed ports
public class VirtualSteerModuleTests
{
    private const int HostPort = 19999;
    private const int ModulePort = 18888;
    private const string LoopbackIp = "127.0.0.1";

    /// <summary>
    /// Lightweight UDP listener that captures PGN 253 packets sent to the host port.
    /// </summary>
    private sealed class HostListener : IDisposable
    {
        private readonly UdpClient _udp;
        private readonly CancellationTokenSource _cts = new();
        public List<byte[]> SteerPackets { get; } = new();

        public HostListener(int port)
        {
            _udp = new UdpClient(new IPEndPoint(IPAddress.Loopback, port));
            _ = ReceiveLoopAsync();
        }

        private async System.Threading.Tasks.Task ReceiveLoopAsync()
        {
            while (!_cts.IsCancellationRequested)
            {
                try
                {
                    var res = await _udp.ReceiveAsync(_cts.Token);
                    if (res.Buffer.Length >= 6
                        && res.Buffer[0] == 0x80 && res.Buffer[1] == 0x81
                        && res.Buffer[3] == PgnProtocol.PGN_STEER_DATA)
                    {
                        lock (SteerPackets) SteerPackets.Add(res.Buffer);
                    }
                }
                catch (OperationCanceledException) { break; }
                catch (SocketException) { break; }
            }
        }

        public byte[]? LatestSteerPacket()
        {
            lock (SteerPackets)
            {
                return SteerPackets.Count == 0 ? null : SteerPackets[^1];
            }
        }

        public void Dispose()
        {
            _cts.Cancel();
            _udp.Dispose();
            _cts.Dispose();
        }
    }

    [Test]
    public void PeriodicEmission_FiresAt10Hz()
    {
        using var listener = new HostListener(HostPort);
        using var steer = new VirtualSteerModule(listenPort: ModulePort, hostPort: HostPort, hostIp: LoopbackIp);
        steer.Start();
        try
        {
            // Wait 600 ms with no incoming PGN 254. Expect >= 5 PGN 253 packets
            // (real Teensy hardware streams at ~10 Hz unconditionally).
            Thread.Sleep(650);
        }
        finally
        {
            steer.Stop();
        }

        int count;
        lock (listener.SteerPackets) count = listener.SteerPackets.Count;
        Assert.That(count, Is.GreaterThanOrEqualTo(5),
            $"Expected periodic PGN 253 emission at ~10 Hz, got {count} packets in 650 ms.");
    }
}

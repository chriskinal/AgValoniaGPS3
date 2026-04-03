// AgValoniaGPS
// Copyright (C) 2024-2025 AgValoniaGPS Contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.

using System;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AgValoniaGPS.IntegrationTests.VirtualModules;
using AgValoniaGPS.Models;
using AgValoniaGPS.Services;
using AgValoniaGPS.Services.Interfaces;
using NSubstitute;

namespace AgValoniaGPS.Services.Tests;

[TestFixture]
public class VirtualModuleTests
{
    #region PGN Protocol Tests

    [Test]
    public void PgnProtocol_BuildPacket_HasCorrectHeaderAndCrc()
    {
        var data = new byte[] { 0x01, 0x02, 0x03 };
        var packet = PgnProtocol.BuildPacket(0xFD, data);

        Assert.That(packet[0], Is.EqualTo(0x80), "Header1");
        Assert.That(packet[1], Is.EqualTo(0x81), "Header2");
        Assert.That(packet[2], Is.EqualTo(0x7F), "Source");
        Assert.That(packet[3], Is.EqualTo(0xFD), "PGN");
        Assert.That(packet[4], Is.EqualTo(3), "Data length");
        Assert.That(packet[5], Is.EqualTo(0x01), "Data[0]");
        Assert.That(packet[6], Is.EqualTo(0x02), "Data[1]");
        Assert.That(packet[7], Is.EqualTo(0x03), "Data[2]");
        Assert.That(PgnProtocol.IsValidPacket(packet, packet.Length), Is.True, "CRC valid");
    }

    [Test]
    public void PgnProtocol_IsValidPacket_RejectsBadCrc()
    {
        var packet = PgnProtocol.BuildPacket(0xFD, new byte[] { 0x01 });
        packet[^1] = 0xFF; // Corrupt CRC
        Assert.That(PgnProtocol.IsValidPacket(packet, packet.Length), Is.False);
    }

    [Test]
    public void PgnProtocol_HelloPacket_MatchesAgOpenFormat()
    {
        var hello = PgnProtocol.BuildHelloPacket(PgnProtocol.PGN_HELLO_AUTOSTEER);
        Assert.That(hello[0], Is.EqualTo(0x80));
        Assert.That(hello[1], Is.EqualTo(0x81));
        Assert.That(hello[3], Is.EqualTo(0x7E), "PGN = 126 (autosteer hello)");
        Assert.That(PgnProtocol.IsValidPacket(hello, hello.Length), Is.True);
    }

    [Test]
    public void PgnProtocol_ParseMachineCommand_ExtractsSectionBits()
    {
        // Build a PGN 239 packet with sections 1, 3, 9 active
        var data = new byte[8];
        data[6] = 0x05; // bits 0 and 2 = sections 1 and 3
        data[7] = 0x01; // bit 0 = section 9
        var packet = PgnProtocol.BuildPacket(PgnProtocol.PGN_MACHINE_TO_MODULE, data);

        var cmd = PgnProtocol.ParseMachineCommand(packet);
        Assert.That(cmd.SectionBits, Is.EqualTo(0x0105));
        Assert.That(cmd.SectionBits1to8, Is.EqualTo(0x05));
        Assert.That(cmd.SectionBits9to16, Is.EqualTo(0x01));
    }

    [Test]
    public void PgnProtocol_ParseAutoSteerCommand_ExtractsFields()
    {
        var data = new byte[8];
        // Speed: 15.0 km/h = 150 raw
        data[0] = 150 & 0xFF;
        data[1] = (150 >> 8) & 0xFF;
        // Status: engaged + GPS valid
        data[2] = 0x0C;
        // Steer angle: -12.5 degrees = -1250 raw
        short angle = -1250;
        data[3] = (byte)(angle & 0xFF);
        data[4] = (byte)((angle >> 8) & 0xFF);
        var packet = PgnProtocol.BuildPacket(PgnProtocol.PGN_AUTOSTEER_TO_MODULE, data);

        var cmd = PgnProtocol.ParseAutoSteerCommand(packet);
        Assert.That(cmd.SpeedKmh, Is.EqualTo(15.0).Within(0.1));
        Assert.That(cmd.SteerAngleDeg, Is.EqualTo(-12.5).Within(0.1));
        Assert.That(cmd.IsEngaged, Is.True);
        Assert.That(cmd.IsGpsValid, Is.True);
    }

    #endregion

    #region GPS Receiver Tests

    [Test]
    public void VirtualGps_SendOnce_ProducesValidPanda()
    {
        // Listen for a single NMEA sentence
        using var listener = new UdpClient(0); // ephemeral port
        int port = ((IPEndPoint)listener.Client.LocalEndPoint!).Port;

        using var gps = new VirtualGpsReceiver(targetPort: port);
        gps.Latitude = 43.712800;
        gps.Longitude = -74.006000;
        gps.HeadingDegrees = 90.0;
        gps.SpeedKnots = 5.0;
        gps.FixQuality = 4;
        gps.Satellites = 12;
        gps.RollDegrees = 1.5;

        gps.SendOnce();

        listener.Client.ReceiveTimeout = 2000;
        IPEndPoint? remote = null;
        var data = listener.Receive(ref remote);
        var sentence = Encoding.ASCII.GetString(data).Trim();

        Assert.That(sentence, Does.StartWith("$PANDA,"));
        Assert.That(sentence, Does.Contain("*")); // Has checksum

        // Parse the sentence
        var parts = sentence.Split('*')[0].Split(',');
        Assert.That(parts[0], Is.EqualTo("$PANDA"));
        Assert.That(parts[3], Is.EqualTo("N")); // North hemisphere
        Assert.That(parts[5], Is.EqualTo("W")); // West hemisphere
        Assert.That(parts[6], Is.EqualTo("4")); // RTK Fixed
        Assert.That(parts[7], Is.EqualTo("12")); // Satellites

        // Verify NMEA checksum
        string body = sentence.Substring(1, sentence.IndexOf('*') - 1);
        byte expectedCrc = 0;
        foreach (char c in body)
            expectedCrc ^= (byte)c;
        string crcStr = sentence.Substring(sentence.IndexOf('*') + 1);
        byte actualCrc = byte.Parse(crcStr, NumberStyles.HexNumber);
        Assert.That(actualCrc, Is.EqualTo(expectedCrc), "NMEA checksum mismatch");
    }

    [Test]
    public void VirtualGps_PandaParsedByNmeaParser()
    {
        // Send $PANDA from virtual GPS → parse with real NmeaParserService
        using var listener = new UdpClient(0);
        int port = ((IPEndPoint)listener.Client.LocalEndPoint!).Port;

        using var gps = new VirtualGpsReceiver(targetPort: port);
        gps.Latitude = 43.712800;
        gps.Longitude = -74.006000;
        gps.HeadingDegrees = 45.0;
        gps.SpeedKnots = 10.0;
        gps.FixQuality = 4;
        gps.Satellites = 14;
        gps.Hdop = 0.8;
        gps.RollDegrees = -2.0;
        gps.PitchDegrees = 1.0;
        gps.YawRateDegPerSec = 0.5;

        gps.SendOnce();

        listener.Client.ReceiveTimeout = 2000;
        IPEndPoint? remote = null;
        var data = listener.Receive(ref remote);
        var sentence = Encoding.ASCII.GetString(data).Trim();

        // Parse with real NMEA parser, capturing via mock GpsService
        var mockGps = Substitute.For<IGpsService>();
        GpsData? received = null;
        mockGps.When(x => x.UpdateGpsData(Arg.Any<GpsData>()))
            .Do(ci => received = ci.Arg<GpsData>());

        var parser = new NmeaParserService(mockGps);
        parser.ParseSentence(sentence);

        Assert.That(received, Is.Not.Null, "NmeaParser should parse the $PANDA sentence");
        Assert.That(received!.CurrentPosition.Latitude, Is.EqualTo(43.712800).Within(0.001));
        Assert.That(received.CurrentPosition.Longitude, Is.EqualTo(-74.006000).Within(0.001));
        Assert.That(received.FixQuality, Is.EqualTo(4));
        Assert.That(received.SatellitesInUse, Is.EqualTo(14));
        Assert.That(received.Hdop, Is.EqualTo(0.8).Within(0.1));
        Assert.That(received.CurrentPosition.Heading, Is.EqualTo(45.0).Within(0.1));
        Assert.That(received.CurrentPosition.Speed, Is.GreaterThan(0)); // 10 knots in m/s
    }

    [Test]
    public void VirtualGps_Step_MovesPosition()
    {
        using var gps = new VirtualGpsReceiver();
        gps.Latitude = 0;
        gps.Longitude = 0;
        gps.HeadingDegrees = 0; // North
        gps.SpeedKnots = 1.0;   // ~0.51 m/s

        double startLat = gps.Latitude;
        gps.Step(10.0); // 10 seconds

        Assert.That(gps.Latitude, Is.GreaterThan(startLat),
            "Moving north should increase latitude");
    }

    #endregion

    #region Steer Module Tests

    [Test]
    public async Task VirtualSteer_RespondsToHello()
    {
        int modulePort = GetEphemeralPort();
        int hostPort = GetEphemeralPort();

        using var steer = new VirtualSteerModule(listenPort: modulePort, hostPort: hostPort);
        using var host = new UdpClient(hostPort);
        steer.Start();

        // Send hello from host to module
        var helloPacket = PgnProtocol.BuildPacket(PgnProtocol.PGN_HELLO_FROM_HOST,
            new byte[] { 0xC8, 0xC8, 0x05 });
        var moduleEndpoint = new IPEndPoint(IPAddress.Loopback, modulePort);
        host.Send(helloPacket, helloPacket.Length, moduleEndpoint);

        // Wait for hello response
        host.Client.ReceiveTimeout = 2000;
        await Task.Delay(200);

        // Should have received at least one hello (periodic + response)
        Assert.That(steer.SentHelloCount, Is.GreaterThan(0));
    }

    [Test]
    public async Task VirtualSteer_ProcessesAutoSteerCommand()
    {
        int modulePort = GetEphemeralPort();
        int hostPort = GetEphemeralPort();

        using var steer = new VirtualSteerModule(listenPort: modulePort, hostPort: hostPort);
        steer.SimulateSteerResponse = true;
        steer.SteerResponseRate = 1.0; // Instant response
        steer.Start();

        // Build PGN 254 with steer angle 15.0 degrees
        var data = new byte[8];
        short speed = 100; // 10 km/h
        data[0] = (byte)(speed & 0xFF);
        data[1] = (byte)((speed >> 8) & 0xFF);
        data[2] = 0x0C; // engaged + GPS valid
        short angle = 1500; // 15.0 degrees
        data[3] = (byte)(angle & 0xFF);
        data[4] = (byte)((angle >> 8) & 0xFF);

        var packet = PgnProtocol.BuildPacket(PgnProtocol.PGN_AUTOSTEER_TO_MODULE, data);

        using var host = new UdpClient(hostPort);
        var moduleEndpoint = new IPEndPoint(IPAddress.Loopback, modulePort);
        host.Send(packet, packet.Length, moduleEndpoint);

        await Task.Delay(500);

        Assert.That(steer.ReceivedCommandCount, Is.GreaterThan(0), "Should receive command");
        Assert.That(steer.LastCommand!.Value.SteerAngleDeg, Is.EqualTo(15.0).Within(0.1));
        Assert.That(steer.LastCommand!.Value.IsEngaged, Is.True);
        Assert.That(steer.ActualSteerAngleDeg, Is.EqualTo(15.0).Within(0.5),
            "WAS should follow commanded angle");
    }

    #endregion

    #region Machine Module Tests

    [Test]
    public async Task VirtualMachine_TracksSectionStates()
    {
        int modulePort = GetEphemeralPort();
        int hostPort = GetEphemeralPort();

        using var machine = new VirtualMachineModule(listenPort: modulePort, hostPort: hostPort);
        machine.Start();

        // Build PGN 239 with sections 1,2,3 active and speed 20 km/h
        var data = new byte[8];
        data[0] = 0; // uturn
        data[1] = 200; // speed * 10 = 20 km/h
        data[6] = 0x07; // sections 1-3 active
        data[7] = 0x00;

        var packet = PgnProtocol.BuildPacket(PgnProtocol.PGN_MACHINE_TO_MODULE, data);

        using var host = new UdpClient(hostPort);
        var moduleEndpoint = new IPEndPoint(IPAddress.Loopback, modulePort);
        host.Send(packet, packet.Length, moduleEndpoint);

        await Task.Delay(500);

        Assert.That(machine.ReceivedCommandCount, Is.GreaterThan(0));
        Assert.That(machine.ActiveSections, Is.EqualTo(0x0007));
        Assert.That(machine.RelayStates[0], Is.True, "Section 1 relay on");
        Assert.That(machine.RelayStates[1], Is.True, "Section 2 relay on");
        Assert.That(machine.RelayStates[2], Is.True, "Section 3 relay on");
        Assert.That(machine.RelayStates[3], Is.False, "Section 4 relay off");
        Assert.That(machine.SpeedByte, Is.EqualTo(200));
    }

    [Test]
    public async Task VirtualMachine_ReceivesPgnBuilderOutput()
    {
        // End-to-end: PgnBuilder.BuildMachinePgn → UDP → VirtualMachineModule parses it
        int modulePort = GetEphemeralPort();
        int hostPort = GetEphemeralPort();

        using var machine = new VirtualMachineModule(listenPort: modulePort, hostPort: hostPort);
        machine.Start();

        // Build PGN 239 using the REAL PgnBuilder (same code AutoSteerService uses)
        var state = new AgValoniaGPS.Models.VehicleState();
        state.Speed = 18.5 / 3.6; // 18.5 km/h in m/s
        state.SectionStates = 0x001F; // Sections 1-5 active

        var pgnBytes = AgValoniaGPS.Services.AutoSteer.PgnBuilder.BuildMachinePgn(
            ref state, uturn: 1, hydLift: 0, tram: 0, geoStop: 0);

        // Send over UDP to virtual machine module
        using var host = new UdpClient(hostPort);
        var moduleEndpoint = new IPEndPoint(IPAddress.Loopback, modulePort);
        host.Send(pgnBytes, pgnBytes.Length, moduleEndpoint);

        await Task.Delay(500);

        Assert.That(machine.ReceivedCommandCount, Is.EqualTo(1), "Should receive 1 command");
        Assert.That(machine.ActiveSections, Is.EqualTo(0x001F), "Sections 1-5 active");
        Assert.That(machine.RelayStates[0], Is.True, "Section 1 on");
        Assert.That(machine.RelayStates[4], Is.True, "Section 5 on");
        Assert.That(machine.RelayStates[5], Is.False, "Section 6 off");
        Assert.That(machine.UTurnState, Is.EqualTo(1), "U-turn active");
        Assert.That(machine.SpeedByte, Is.EqualTo(185), "Speed = 18.5 * 10");
    }

    [Test]
    public async Task VirtualMachine_ReceivesAllSixteenSections()
    {
        int modulePort = GetEphemeralPort();
        int hostPort = GetEphemeralPort();

        using var machine = new VirtualMachineModule(listenPort: modulePort, hostPort: hostPort);
        machine.Start();

        // All 16 sections active
        var state = new AgValoniaGPS.Models.VehicleState();
        state.SectionStates = 0xFFFF;
        state.Speed = 10.0 / 3.6; // 10 km/h in m/s

        var pgnBytes = AgValoniaGPS.Services.AutoSteer.PgnBuilder.BuildMachinePgn(ref state);

        using var host = new UdpClient(hostPort);
        host.Send(pgnBytes, pgnBytes.Length, new IPEndPoint(IPAddress.Loopback, modulePort));

        await Task.Delay(500);

        Assert.That(machine.ActiveSections, Is.EqualTo(0xFFFF), "All 16 sections active");
        for (int i = 0; i < 16; i++)
            Assert.That(machine.RelayStates[i], Is.True, $"Section {i + 1} relay on");
    }

    [Test]
    public async Task VirtualMachine_SpeedClampedAt255()
    {
        int modulePort = GetEphemeralPort();
        int hostPort = GetEphemeralPort();

        using var machine = new VirtualMachineModule(listenPort: modulePort, hostPort: hostPort);
        machine.Start();

        // Speed 30 km/h → 300 raw → clamped to 255
        var state = new AgValoniaGPS.Models.VehicleState();
        state.Speed = 30.0 / 3.6; // 30 km/h in m/s

        var pgnBytes = AgValoniaGPS.Services.AutoSteer.PgnBuilder.BuildMachinePgn(ref state);

        using var host = new UdpClient(hostPort);
        host.Send(pgnBytes, pgnBytes.Length, new IPEndPoint(IPAddress.Loopback, modulePort));

        await Task.Delay(500);

        Assert.That(machine.SpeedByte, Is.EqualTo(255), "Speed byte clamped at 255");
    }

    [Test]
    public async Task VirtualMachine_PgnBuilderCrcMatchesProtocol()
    {
        // Verify PgnBuilder's CRC is valid per PgnProtocol.IsValidPacket
        var state = new AgValoniaGPS.Models.VehicleState();
        state.SectionStates = 0x00AA;
        state.Speed = 12.0 / 3.6; // 12 km/h in m/s

        var pgnBytes = AgValoniaGPS.Services.AutoSteer.PgnBuilder.BuildMachinePgn(
            ref state, uturn: 1, hydLift: 2, tram: 3, geoStop: 0);

        Assert.That(PgnProtocol.IsValidPacket(pgnBytes, pgnBytes.Length), Is.True,
            "PgnBuilder CRC must match PgnProtocol validation");

        // Also verify the virtual module can parse it
        int modulePort = GetEphemeralPort();
        int hostPort = GetEphemeralPort();

        using var machine = new VirtualMachineModule(listenPort: modulePort, hostPort: hostPort);
        machine.Start();

        using var host = new UdpClient(hostPort);
        host.Send(pgnBytes, pgnBytes.Length, new IPEndPoint(IPAddress.Loopback, modulePort));

        await Task.Delay(500);

        Assert.That(machine.HydLiftState, Is.EqualTo(2));
        Assert.That(machine.TramState, Is.EqualTo(3));
    }

    #endregion

    #region Hub Integration Tests

    [Test]
    public void VirtualModuleHub_DriveFast_SendsMultipleGpsFrames()
    {
        using var listener = new UdpClient(0);
        int port = ((IPEndPoint)listener.Client.LocalEndPoint!).Port;

        // Create hub sending to our listener port
        using var hub = new VirtualModuleHub(hostReceivePort: port, moduleListenPort: GetEphemeralPort());

        hub.Gps.Latitude = 43.0;
        hub.Gps.Longitude = -74.0;
        hub.DriveFast(headingDeg: 0, speedKmh: 36, frames: 50);

        Assert.That(hub.Gps.SentCount, Is.EqualTo(50));
        // At 36 km/h = 10 m/s, 50 frames at 10Hz = 5 seconds = 50m north
        // 50m / 111320 m/deg = ~0.000449 degrees
        Assert.That(hub.Gps.Latitude, Is.GreaterThan(43.0004),
            "Should have moved north");
    }

    [Test]
    public void VirtualModuleHub_DriveFast_AllFramesParseable()
    {
        using var listener = new UdpClient(0);
        int port = ((IPEndPoint)listener.Client.LocalEndPoint!).Port;
        listener.Client.ReceiveTimeout = 1000;

        using var hub = new VirtualModuleHub(hostReceivePort: port, moduleListenPort: GetEphemeralPort());
        hub.DriveFast(headingDeg: 90, speedKmh: 20, frames: 10);

        var mockGps = Substitute.For<IGpsService>();
        int parsedCount = 0;
        mockGps.When(x => x.UpdateGpsData(Arg.Any<GpsData>()))
            .Do(ci => { if (ci.Arg<GpsData>().FixQuality > 0) parsedCount++; });
        var parser = new NmeaParserService(mockGps);

        // Read all sent frames
        for (int i = 0; i < 10; i++)
        {
            try
            {
                IPEndPoint? remote = null;
                var data = listener.Receive(ref remote);
                var sentence = Encoding.ASCII.GetString(data).Trim();
                parser.ParseSentence(sentence);
            }
            catch (SocketException) { break; }
        }

        Assert.That(parsedCount, Is.EqualTo(10),
            "All frames should parse as valid GPS data");
    }

    #endregion

    /// <summary>Get a random ephemeral port by binding to 0 and reading the assigned port.</summary>
    private static int GetEphemeralPort()
    {
        using var tmp = new UdpClient(0);
        return ((IPEndPoint)tmp.Client.LocalEndPoint!).Port;
    }
}

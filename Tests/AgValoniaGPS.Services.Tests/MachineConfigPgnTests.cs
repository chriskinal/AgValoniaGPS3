// AgValoniaGPS
// Copyright (C) 2024-2025 AgValoniaGPS Contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.

using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using AgValoniaGPS.IntegrationTests.VirtualModules;
using AgValoniaGPS.Models.Configuration;
using AgValoniaGPS.Services.AutoSteer;

namespace AgValoniaGPS.Services.Tests;

[TestFixture]
public class MachineConfigPgnTests
{
    #region PGN 238 (Machine Config) Builder Tests

    [Test]
    public void BuildMachineConfig_HasCorrectHeader()
    {
        var config = new MachineConfig();
        var pgn = PgnBuilder.BuildMachineConfigPgn(config);

        Assert.That(pgn[0], Is.EqualTo(0x80), "Header1");
        Assert.That(pgn[1], Is.EqualTo(0x81), "Header2");
        Assert.That(pgn[2], Is.EqualTo(0x7F), "Source");
        Assert.That(pgn[3], Is.EqualTo(0xEE), "PGN 238");
        Assert.That(pgn[4], Is.EqualTo(8), "Data length");
        Assert.That(pgn.Length, Is.EqualTo(14), "Total packet size");
    }

    [Test]
    public void BuildMachineConfig_EncodesHydraulicSettings()
    {
        var config = new MachineConfig
        {
            RaiseTime = 5,
            LowerTime = 3,
            HydraulicLiftEnabled = true
        };
        var pgn = PgnBuilder.BuildMachineConfigPgn(config);

        Assert.That(pgn[5], Is.EqualTo(5), "RaiseTime");
        Assert.That(pgn[6], Is.EqualTo(3), "LowerTime");
        Assert.That(pgn[7], Is.EqualTo(1), "EnableHyd");
    }

    [Test]
    public void BuildMachineConfig_EncodesSet0Bitfield()
    {
        var config = new MachineConfig
        {
            InvertRelay = true,
            HydraulicLiftEnabled = true
        };
        var pgn = PgnBuilder.BuildMachineConfigPgn(config);

        Assert.That(pgn[8] & 0x01, Is.EqualTo(1), "InvertRelay bit");
        Assert.That(pgn[8] & 0x02, Is.EqualTo(2), "HydEnabled bit");
    }

    [Test]
    public void BuildMachineConfig_EncodesUserValues()
    {
        var config = new MachineConfig
        {
            User1Value = 10,
            User2Value = 20,
            User3Value = 30,
            User4Value = 40
        };
        var pgn = PgnBuilder.BuildMachineConfigPgn(config);

        Assert.That(pgn[9], Is.EqualTo(10), "User1");
        Assert.That(pgn[10], Is.EqualTo(20), "User2");
        Assert.That(pgn[11], Is.EqualTo(30), "User3");
        Assert.That(pgn[12], Is.EqualTo(40), "User4");
    }

    [Test]
    public void BuildMachineConfig_CrcIsValid()
    {
        var config = new MachineConfig
        {
            RaiseTime = 7,
            LowerTime = 2,
            HydraulicLiftEnabled = true,
            InvertRelay = true,
            User1Value = 100
        };
        var pgn = PgnBuilder.BuildMachineConfigPgn(config);

        Assert.That(PgnProtocol.IsValidPacket(pgn, pgn.Length), Is.True,
            "PgnBuilder CRC must pass PgnProtocol validation");
    }

    [Test]
    public void BuildMachineConfig_ClampsValues()
    {
        var config = new MachineConfig
        {
            RaiseTime = 999,   // Should clamp to 255
            User1Value = -5    // Should clamp to 0
        };
        var pgn = PgnBuilder.BuildMachineConfigPgn(config);

        Assert.That(pgn[5], Is.EqualTo(255), "RaiseTime clamped");
        Assert.That(pgn[9], Is.EqualTo(0), "User1 clamped to 0");
    }

    #endregion

    #region PGN 236 (Machine Pin Config) Builder Tests

    [Test]
    public void BuildMachinePins_HasCorrectHeader()
    {
        var config = new MachineConfig();
        var pgn = PgnBuilder.BuildMachinePinsPgn(config);

        Assert.That(pgn[0], Is.EqualTo(0x80), "Header1");
        Assert.That(pgn[1], Is.EqualTo(0x81), "Header2");
        Assert.That(pgn[3], Is.EqualTo(0xEC), "PGN 236");
        Assert.That(pgn[4], Is.EqualTo(24), "Data length = 24 pins");
        Assert.That(pgn.Length, Is.EqualTo(30), "Total packet size");
    }

    [Test]
    public void BuildMachinePins_DefaultPinsFirstSixSections()
    {
        var config = new MachineConfig();
        var pgn = PgnBuilder.BuildMachinePinsPgn(config);

        // Default: pins 0-5 = sections 1-6
        Assert.That(pgn[5], Is.EqualTo((byte)PinFunction.Section1));
        Assert.That(pgn[6], Is.EqualTo((byte)PinFunction.Section2));
        Assert.That(pgn[7], Is.EqualTo((byte)PinFunction.Section3));
        Assert.That(pgn[8], Is.EqualTo((byte)PinFunction.Section4));
        Assert.That(pgn[9], Is.EqualTo((byte)PinFunction.Section5));
        Assert.That(pgn[10], Is.EqualTo((byte)PinFunction.Section6));
        // Remaining pins = None
        for (int i = 6; i < 24; i++)
            Assert.That(pgn[5 + i], Is.EqualTo((byte)PinFunction.None), $"Pin {i} should be None");
    }

    [Test]
    public void BuildMachinePins_CustomPinAssignments()
    {
        var config = new MachineConfig();
        config.SetPinAssignment(0, PinFunction.HydUp);
        config.SetPinAssignment(1, PinFunction.HydDown);
        config.SetPinAssignment(10, PinFunction.TramLeft);
        config.SetPinAssignment(11, PinFunction.TramRight);
        config.SetPinAssignment(23, PinFunction.GeoStop);

        var pgn = PgnBuilder.BuildMachinePinsPgn(config);

        Assert.That(pgn[5], Is.EqualTo((byte)PinFunction.HydUp), "Pin 0 = HydUp");
        Assert.That(pgn[6], Is.EqualTo((byte)PinFunction.HydDown), "Pin 1 = HydDown");
        Assert.That(pgn[15], Is.EqualTo((byte)PinFunction.TramLeft), "Pin 10 = TramLeft");
        Assert.That(pgn[16], Is.EqualTo((byte)PinFunction.TramRight), "Pin 11 = TramRight");
        Assert.That(pgn[28], Is.EqualTo((byte)PinFunction.GeoStop), "Pin 23 = GeoStop");
    }

    [Test]
    public void BuildMachinePins_CrcIsValid()
    {
        var config = new MachineConfig();
        config.SetPinAssignment(0, PinFunction.Section1);
        config.SetPinAssignment(23, PinFunction.GeoStop);

        var pgn = PgnBuilder.BuildMachinePinsPgn(config);

        Assert.That(PgnProtocol.IsValidPacket(pgn, pgn.Length), Is.True,
            "PgnBuilder CRC must pass PgnProtocol validation");
    }

    #endregion

    #region End-to-End UDP Tests

    [Test]
    public async Task MachineConfig_SentAndReceivedOverUdp()
    {
        int modulePort = GetEphemeralPort();
        int hostPort = GetEphemeralPort();

        using var machine = new VirtualMachineModule(listenPort: modulePort, hostPort: hostPort);
        machine.Start();

        var config = new MachineConfig
        {
            RaiseTime = 6,
            LowerTime = 4,
            HydraulicLiftEnabled = true,
            InvertRelay = true,
            User1Value = 42,
            User2Value = 99,
            User3Value = 0,
            User4Value = 255
        };

        var pgn = PgnBuilder.BuildMachineConfigPgn(config);

        using var host = new UdpClient(hostPort);
        host.Send(pgn, pgn.Length, new IPEndPoint(IPAddress.Loopback, modulePort));

        await Task.Delay(500);

        Assert.That(machine.ReceivedConfigCount, Is.EqualTo(1));
        var received = machine.LastConfig!.Value;
        Assert.That(received.RaiseTime, Is.EqualTo(6));
        Assert.That(received.LowerTime, Is.EqualTo(4));
        Assert.That(received.EnableHyd, Is.EqualTo(1));
        Assert.That(received.InvertRelay, Is.True);
        Assert.That(received.HydEnabled, Is.True);
        Assert.That(received.User1, Is.EqualTo(42));
        Assert.That(received.User2, Is.EqualTo(99));
        Assert.That(received.User3, Is.EqualTo(0));
        Assert.That(received.User4, Is.EqualTo(255));
    }

    [Test]
    public async Task MachinePinConfig_SentAndReceivedOverUdp()
    {
        int modulePort = GetEphemeralPort();
        int hostPort = GetEphemeralPort();

        using var machine = new VirtualMachineModule(listenPort: modulePort, hostPort: hostPort);
        machine.Start();

        var config = new MachineConfig();
        config.SetPinAssignment(0, PinFunction.HydUp);
        config.SetPinAssignment(1, PinFunction.HydDown);
        config.SetPinAssignment(2, PinFunction.Section1);
        config.SetPinAssignment(3, PinFunction.Section2);
        config.SetPinAssignment(22, PinFunction.TramLeft);
        config.SetPinAssignment(23, PinFunction.GeoStop);

        var pgn = PgnBuilder.BuildMachinePinsPgn(config);

        using var host = new UdpClient(hostPort);
        host.Send(pgn, pgn.Length, new IPEndPoint(IPAddress.Loopback, modulePort));

        await Task.Delay(500);

        Assert.That(machine.ReceivedPinConfigCount, Is.EqualTo(1));
        var pins = machine.LastPinConfig!.Value.PinAssignments;
        Assert.That(pins[0], Is.EqualTo((byte)PinFunction.HydUp));
        Assert.That(pins[1], Is.EqualTo((byte)PinFunction.HydDown));
        Assert.That(pins[2], Is.EqualTo((byte)PinFunction.Section1));
        Assert.That(pins[3], Is.EqualTo((byte)PinFunction.Section2));
        Assert.That(pins[22], Is.EqualTo((byte)PinFunction.TramLeft));
        Assert.That(pins[23], Is.EqualTo((byte)PinFunction.GeoStop));
        // Unassigned pins should be None
        Assert.That(pins[10], Is.EqualTo((byte)PinFunction.None));
    }

    #endregion

    private static int GetEphemeralPort()
    {
        using var tmp = new UdpClient(0);
        return ((IPEndPoint)tmp.Client.LocalEndPoint!).Port;
    }
}

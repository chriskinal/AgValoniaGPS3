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
using System.Threading;
using System.Threading.Tasks;

namespace AgValoniaGPS.IntegrationTests.VirtualModules;

/// <summary>
/// Virtual machine/implement module. Listens for PGN 239 (Machine Data) commands,
/// responds with PGN 123 (hello). Simulates section relays, hydraulic lift,
/// and tramline control.
/// </summary>
public class VirtualMachineModule : IDisposable
{
    private readonly UdpClient _udp;
    private readonly IPEndPoint _hostEndpoint;
    private CancellationTokenSource? _cts;
    private Task? _receiveTask;
    private Task? _helloTask;

    // Current machine state (received from host)
    public ushort ActiveSections { get; private set; }
    public byte UTurnState { get; private set; }
    public byte HydLiftState { get; private set; }
    public byte TramState { get; private set; }
    public byte GeoStopState { get; private set; }
    public byte SpeedByte { get; private set; }

    // Simulated hardware state
    public bool IsHydraulicUp { get; set; }
    public bool[] RelayStates { get; } = new bool[16];

    // Config state (received from host)
    public MachineConfigPacket? LastConfig { get; private set; }
    public MachinePinConfigPacket? LastPinConfig { get; private set; }
    public long ReceivedConfigCount { get; private set; }
    public long ReceivedPinConfigCount { get; private set; }

    // Counters
    public long ReceivedCommandCount { get; private set; }
    public long SentHelloCount { get; private set; }
    public MachineCommand? LastCommand { get; private set; }

    public VirtualMachineModule(int listenPort = 8888, int hostPort = 9999, string hostIp = "127.0.0.1")
    {
        _udp = new UdpClient(new IPEndPoint(IPAddress.Any, listenPort));
        _hostEndpoint = new IPEndPoint(IPAddress.Parse(hostIp), hostPort);
    }

    public void Start()
    {
        _cts = new CancellationTokenSource();
        _receiveTask = Task.Run(() => ReceiveLoop(_cts.Token));
        _helloTask = Task.Run(() => HelloLoop(_cts.Token));
    }

    public void Stop()
    {
        _cts?.Cancel();
        try { _receiveTask?.Wait(TimeSpan.FromSeconds(2)); } catch { }
        try { _helloTask?.Wait(TimeSpan.FromSeconds(2)); } catch { }
    }

    private async Task ReceiveLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var result = await _udp.ReceiveAsync(ct);
                ProcessPacket(result.Buffer);
            }
            catch (OperationCanceledException) { break; }
            catch (SocketException) { break; }
        }
    }

    private void ProcessPacket(byte[] data)
    {
        if (!PgnProtocol.IsValidPacket(data, data.Length))
            return;

        byte pgn = PgnProtocol.GetPgn(data);

        switch (pgn)
        {
            case PgnProtocol.PGN_MACHINE_TO_MODULE:
                var cmd = PgnProtocol.ParseMachineCommand(data);
                ActiveSections = cmd.SectionBits;
                UTurnState = cmd.UTurnState;
                HydLiftState = cmd.HydLiftState;
                TramState = cmd.TramState;
                GeoStopState = cmd.GeoStopState;
                SpeedByte = cmd.SpeedByte;
                LastCommand = cmd;
                ReceivedCommandCount++;

                // Update relay states from section bits
                for (int i = 0; i < 16; i++)
                    RelayStates[i] = (ActiveSections & (1 << i)) != 0;

                // Simulate hydraulic lift
                IsHydraulicUp = HydLiftState == 1;
                break;

            case PgnProtocol.PGN_HELLO_FROM_HOST:
                SendHello();
                break;

            case PgnProtocol.PGN_MACHINE_CONFIG:
                LastConfig = PgnProtocol.ParseMachineConfig(data);
                ReceivedConfigCount++;
                break;

            case PgnProtocol.PGN_MACHINE_PINS:
                LastPinConfig = PgnProtocol.ParseMachinePinConfig(data);
                ReceivedPinConfigCount++;
                break;
        }
    }

    private void SendHello()
    {
        var packet = PgnProtocol.BuildHelloPacket(PgnProtocol.PGN_HELLO_MACHINE);
        _udp.Send(packet, packet.Length, _hostEndpoint);
        SentHelloCount++;
    }

    private async Task HelloLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                SendHello();
                await Task.Delay(1000, ct);
            }
            catch (OperationCanceledException) { break; }
        }
    }

    public void Dispose()
    {
        Stop();
        _udp.Dispose();
        _cts?.Dispose();
    }
}

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

namespace AgValoniaGPS.VehicleSimulator.Modules;

/// <summary>
/// Orchestrates all virtual modules into a complete simulated hardware environment.
/// Creates GPS, steer module, and machine module on configurable ports.
/// </summary>
public class VirtualModuleHub : IDisposable
{
    public VirtualGpsReceiver Gps { get; }
    public VirtualSteerModule Steer { get; }
    public VirtualMachineModule Machine { get; }

    /// <summary>Port the app should listen on (where GPS/modules send data).</summary>
    public int HostReceivePort { get; }

    /// <summary>Port where modules listen for commands from the app.</summary>
    public int ModuleListenPort { get; }

    public VirtualModuleHub(int hostReceivePort = 9999, int moduleListenPort = 8888)
    {
        HostReceivePort = hostReceivePort;
        ModuleListenPort = moduleListenPort;

        Gps = new VirtualGpsReceiver(targetPort: hostReceivePort);
        Steer = new VirtualSteerModule(listenPort: moduleListenPort, hostPort: hostReceivePort);
        // Machine can't bind to the same port as Steer. In real hardware, one Teensy
        // handles both. Offset the machine to moduleListenPort + 1.
        Machine = new VirtualMachineModule(listenPort: moduleListenPort + 1, hostPort: hostReceivePort);
    }

    public void Start()
    {
        Steer.Start();
        Machine.Start();
        // Don't start Gps.SendLoop. The host's SimulationTick at 10 Hz
        // advances Lat/Lon/Heading and explicitly calls Gps.SendOnce()
        // after each physics step. Starting the receiver's own 10 Hz
        // SendLoop here adds a parallel emitter that reads the same
        // fields between ticks, producing duplicate packets where every
        // pair carries identical position data — visible to the host
        // as stuttering / jumpy tractor motion.
        // SendOnce() remains available for tests that drive emission.
    }

    public void Stop()
    {
        Gps.Stop();
        Steer.Stop();
        Machine.Stop();
    }

    /// <summary>
    /// Drive the virtual GPS in a straight line at given speed and heading.
    /// Sends GPS updates for the specified duration.
    /// </summary>
    public async Task DriveAsync(double headingDeg, double speedKmh, double durationSeconds,
        CancellationToken ct = default)
    {
        Gps.HeadingDegrees = headingDeg;
        Gps.SpeedKnots = speedKmh / 1.852; // km/h to knots
        double stepTime = 1.0 / Gps.UpdateRateHz;
        int steps = (int)(durationSeconds * Gps.UpdateRateHz);

        for (int i = 0; i < steps && !ct.IsCancellationRequested; i++)
        {
            Gps.Step(stepTime);
            Gps.SendOnce();
            await Task.Delay((int)(stepTime * 1000), ct);
        }
    }

    /// <summary>
    /// Send a burst of GPS positions without real-time delay (for fast tests).
    /// </summary>
    public void DriveFast(double headingDeg, double speedKmh, int frames)
    {
        Gps.HeadingDegrees = headingDeg;
        Gps.SpeedKnots = speedKmh / 1.852;
        double stepTime = 1.0 / Gps.UpdateRateHz;

        for (int i = 0; i < frames; i++)
        {
            Gps.Step(stepTime);
            Gps.SendOnce();
        }
    }

    /// <summary>
    /// Drive with autosteer: applies a steer angle to the GPS heading using a bicycle model.
    /// </summary>
    public async Task DriveWithAutoSteerAsync(double speedKmh, int frames,
        Func<double>? steerAngleProvider = null,
        double wheelbase = 2.5, Func<Task>? onFrame = null)
    {
        Gps.SpeedKnots = speedKmh / 1.852;
        double stepTime = 1.0 / Gps.UpdateRateHz;
        double speedMs = speedKmh / 3.6;

        for (int i = 0; i < frames; i++)
        {
            // Read steer angle from provider (ViewModel) or steer module
            double steerAngleDeg = steerAngleProvider?.Invoke() ?? Steer.CommandedSteerAngleDeg;

            // Bicycle model: heading rate = speed * tan(steerAngle) / wheelbase
            if (Math.Abs(steerAngleDeg) > 0.1 && speedMs > 0.1)
            {
                double steerAngleRad = steerAngleDeg * Math.PI / 180.0;
                double turnRate = speedMs * Math.Tan(steerAngleRad) / wheelbase; // rad/s
                double headingChange = turnRate * stepTime * 180.0 / Math.PI; // degrees
                Gps.HeadingDegrees = (Gps.HeadingDegrees + headingChange + 360) % 360;
            }

            Gps.Step(stepTime);
            Gps.SendOnce();

            if (onFrame != null) await onFrame();
            else await Task.Delay((int)(stepTime * 1000));
        }
    }

    public void Dispose()
    {
        Stop();
        Gps.Dispose();
        Steer.Dispose();
        Machine.Dispose();
    }
}

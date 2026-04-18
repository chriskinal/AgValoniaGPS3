// AgValoniaGPS
// Copyright (C) 2024-2025 AgValoniaGPS Contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.

using System;

namespace AgValoniaGPS.VehicleSimulator.Models;

/// <summary>
/// Bicycle model for vehicle physics: heading changes based on speed and steer angle.
/// </summary>
public class VehiclePhysicsModel
{
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double HeadingDeg { get; set; }
    public double SpeedKmh { get; set; }
    public double SteerAngleDeg { get; set; }
    public double Wheelbase { get; set; } = 2.5; // meters

    /// <summary>
    /// Advance the vehicle by deltaSec seconds using the bicycle model.
    /// </summary>
    public void Step(double deltaSec)
    {
        double speedMs = SpeedKmh / 3.6;
        double headingRad = HeadingDeg * Math.PI / 180.0;
        double steerRad = SteerAngleDeg * Math.PI / 180.0;

        // Heading change rate: omega = speed * tan(steerAngle) / wheelbase
        double omega = 0;
        if (Math.Abs(Wheelbase) > 0.1)
            omega = speedMs * Math.Tan(steerRad) / Wheelbase;

        // Update heading
        headingRad += omega * deltaSec;

        // Normalize heading 0-360
        HeadingDeg = (headingRad * 180.0 / Math.PI) % 360.0;
        if (HeadingDeg < 0) HeadingDeg += 360.0;

        // Update position (meters -> degrees approximation)
        double metersPerDegLat = 111111.0;
        double metersPerDegLon = 111111.0 * Math.Cos(Latitude * Math.PI / 180.0);

        double dx = speedMs * Math.Sin(headingRad) * deltaSec; // easting
        double dy = speedMs * Math.Cos(headingRad) * deltaSec; // northing

        Latitude += dy / metersPerDegLat;
        if (Math.Abs(metersPerDegLon) > 0.01)
            Longitude += dx / metersPerDegLon;
    }
}

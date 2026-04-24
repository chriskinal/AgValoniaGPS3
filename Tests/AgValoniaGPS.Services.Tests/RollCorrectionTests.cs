// AgValoniaGPS
// Copyright (C) 2024-2025 AgValoniaGPS Contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.

using System;
using AgValoniaGPS.Models;
using AgValoniaGPS.Models.Configuration;
using AgValoniaGPS.Services;
using AgValoniaGPS.Services.Interfaces;
using NSubstitute;

namespace AgValoniaGPS.Services.Tests;

[TestFixture]
public class RollCorrectionTests
{
    private IGpsService _gpsService = null!;

    [SetUp]
    public void Setup()
    {
        // Reset sensor state for each test
        SensorState.Instance.ImuRoll = 0;
        SensorState.Instance.ImuPitch = 0;

        // Reset vehicle config
        var config = ConfigurationStore.Instance;
        config.Vehicle.AntennaHeight = 3.0;
        config.Vehicle.AntennaPivot = 0;
        config.Vehicle.AntennaOffset = 0;
    }

    [Test]
    public void NoRoll_PositionUnchanged()
    {
        var gpsService = new GpsService();
        SensorState.Instance.ImuRoll = 0;

        var gpsData = MakeGpsData(easting: 100, northing: 200, heading: 0);
        gpsService.UpdateGpsData(gpsData);

        Assert.That(gpsData.CurrentPosition.Easting, Is.EqualTo(100.0).Within(0.001));
        Assert.That(gpsData.CurrentPosition.Northing, Is.EqualTo(200.0).Within(0.001));
    }

    [Test]
    public void RollRight_HeadingNorth_ShiftsPositionWest()
    {
        // When vehicle rolls right (positive roll), antenna moves right.
        // Correction should shift position LEFT (west when heading north).
        var gpsService = new GpsService();
        ConfigurationStore.Instance.Vehicle.AntennaHeight = 3.0;
        SensorState.Instance.ImuRoll = 10.0; // 10 degrees right

        var gpsData = MakeGpsData(easting: 100, northing: 200, heading: 0);
        gpsService.UpdateGpsData(gpsData);

        // sin(10 deg) * 3.0m = 0.521m correction
        // Heading north, perpendicular is east. Roll right = antenna goes east,
        // so correction moves west (decreases easting).
        double expectedOffset = Math.Sin(10.0 * Math.PI / 180.0) * 3.0;
        Assert.That(gpsData.CurrentPosition.Easting, Is.LessThan(100.0),
            "Roll right heading north should decrease easting (shift west)");
        Assert.That(Math.Abs(gpsData.CurrentPosition.Easting - 100.0),
            Is.EqualTo(expectedOffset).Within(0.05),
            "Correction magnitude should match sin(roll) * height");
        Assert.That(gpsData.CurrentPosition.Northing, Is.EqualTo(200.0).Within(0.05),
            "Northing should be mostly unchanged for north heading");
    }

    [Test]
    public void RollLeft_HeadingNorth_ShiftsPositionEast()
    {
        var gpsService = new GpsService();
        ConfigurationStore.Instance.Vehicle.AntennaHeight = 3.0;
        SensorState.Instance.ImuRoll = -10.0; // 10 degrees left

        var gpsData = MakeGpsData(easting: 100, northing: 200, heading: 0);
        gpsService.UpdateGpsData(gpsData);

        Assert.That(gpsData.CurrentPosition.Easting, Is.GreaterThan(100.0),
            "Roll left heading north should increase easting (shift east)");
    }

    [Test]
    public void RollRight_HeadingEast_ShiftsPositionNorth()
    {
        // Heading east (90 deg), roll right = antenna goes south,
        // correction should shift north.
        var gpsService = new GpsService();
        ConfigurationStore.Instance.Vehicle.AntennaHeight = 3.0;
        SensorState.Instance.ImuRoll = 10.0;

        var gpsData = MakeGpsData(easting: 100, northing: 200, heading: 90);
        gpsService.UpdateGpsData(gpsData);

        Assert.That(gpsData.CurrentPosition.Northing, Is.GreaterThan(200.0),
            "Roll right heading east should increase northing (shift north)");
    }

    [Test]
    public void ZeroAntennaHeight_NoCorrectionApplied()
    {
        var gpsService = new GpsService();
        ConfigurationStore.Instance.Vehicle.AntennaHeight = 0;
        SensorState.Instance.ImuRoll = 15.0;

        var gpsData = MakeGpsData(easting: 100, northing: 200, heading: 0);
        gpsService.UpdateGpsData(gpsData);

        Assert.That(gpsData.CurrentPosition.Easting, Is.EqualTo(100.0).Within(0.001),
            "No correction when antenna height is zero");
    }

    [Test]
    public void LargeAntennaHeight_LargerCorrection()
    {
        var gpsService = new GpsService();
        SensorState.Instance.ImuRoll = 5.0;

        // 3m antenna
        ConfigurationStore.Instance.Vehicle.AntennaHeight = 3.0;
        var gpsData1 = MakeGpsData(easting: 100, northing: 200, heading: 0);
        gpsService.UpdateGpsData(gpsData1);
        double offset3m = Math.Abs(gpsData1.CurrentPosition.Easting - 100.0);

        // 6m antenna
        ConfigurationStore.Instance.Vehicle.AntennaHeight = 6.0;
        var gpsData2 = MakeGpsData(easting: 100, northing: 200, heading: 0);
        gpsService.UpdateGpsData(gpsData2);
        double offset6m = Math.Abs(gpsData2.CurrentPosition.Easting - 100.0);

        Assert.That(offset6m, Is.EqualTo(offset3m * 2).Within(0.01),
            "Double antenna height should double correction");
    }

    [Test]
    public void CorrectionMagnitude_MatchesFormula()
    {
        // Verify exact formula: correction = sin(roll) * -height
        // Applied perpendicular to heading
        var gpsService = new GpsService();
        ConfigurationStore.Instance.Vehicle.AntennaHeight = 4.0;
        SensorState.Instance.ImuRoll = 15.0;

        var gpsData = MakeGpsData(easting: 500, northing: 500, heading: 0);
        gpsService.UpdateGpsData(gpsData);

        double expected = Math.Sin(15.0 * Math.PI / 180.0) * 4.0; // ~1.035m
        double actualShift = Math.Sqrt(
            Math.Pow(gpsData.CurrentPosition.Easting - 500, 2) +
            Math.Pow(gpsData.CurrentPosition.Northing - 500, 2));

        Assert.That(actualShift, Is.EqualTo(expected).Within(0.05),
            $"Total shift should be sin(15)*4 = {expected:F3}m, got {actualShift:F3}m");
    }

    [Test]
    public void RollCorrectionCombinedWithAntennaPivot()
    {
        // Both antenna offset and roll correction should apply
        var gpsService = new GpsService();
        ConfigurationStore.Instance.Vehicle.AntennaPivot = 2.0; // 2m ahead
        ConfigurationStore.Instance.Vehicle.AntennaHeight = 3.0;
        SensorState.Instance.ImuRoll = 10.0;

        var gpsData = MakeGpsData(easting: 100, northing: 200, heading: 0);
        gpsService.UpdateGpsData(gpsData);

        // Pivot moves 2m south (behind when heading north)
        // Roll correction moves ~0.52m west
        Assert.That(gpsData.CurrentPosition.Northing, Is.LessThan(200.0),
            "Pivot should move position south");
        Assert.That(gpsData.CurrentPosition.Easting, Is.LessThan(100.0),
            "Roll should move position west");
    }

    private static GpsData MakeGpsData(double easting, double northing, double heading)
    {
        return new GpsData
        {
            CurrentPosition = new Position
            {
                Easting = easting,
                Northing = northing,
                Heading = heading,
                Latitude = 43.7,
                Longitude = -74.0,
                Speed = 5.0
            },
            FixQuality = 4,
            SatellitesInUse = 12,
            Hdop = 0.8
        };
    }
}

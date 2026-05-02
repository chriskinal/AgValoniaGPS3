// AgValoniaGPS
// Copyright (C) 2024-2026 AgValoniaGPS Contributors
//
// Licensed under GNU GPL v3. See LICENSE.md.

using System;
using AgValoniaGPS.Models;
using AgValoniaGPS.Models.Base;
using AgValoniaGPS.Models.Configuration;
using AgValoniaGPS.Models.State;
using AgValoniaGPS.Services;
using AgValoniaGPS.Services.AutoSteer;
using AgValoniaGPS.Services.Coverage;
using AgValoniaGPS.Services.Interfaces;
using AgValoniaGPS.Services.Pipeline;
using AgValoniaGPS.Services.Section;
using AgValoniaGPS.Services.Tool;
using AgValoniaGPS.Services.Track;
using AgValoniaGPS.Services.YouTurn;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace AgValoniaGPS.Services.Tests.Pipeline;

/// <summary>
/// Verifies commit 3 of #313: GpsPipelineService publishes a PoseSnapshot
/// to the position estimator on every GPS frame, using the canonical pose
/// (drift-compensated, fused heading, local-plane coordinates).
/// </summary>
[TestFixture]
[NonParallelizable] // ConfigurationStore is a singleton.
public class GpsPipelinePositionEstimatorTests
{
    private GpsService _gpsService = null!;
    private GpsPipelineService _pipeline = null!;
    private PositionEstimator _estimator = null!;
    private ApplicationState _appState = null!;

    [SetUp]
    public void SetUp()
    {
        ConfigurationStore.SetInstance(new ConfigurationStore());
        var config = ConfigurationStore.Instance;
        config.Vehicle.AntennaPivot = 0;
        config.Vehicle.AntennaOffset = 0;
        config.Vehicle.AntennaHeight = 0;
        config.Tool.Width = 6;
        config.NumSections = 1;
        config.Tool.SetSectionWidth(0, 600);

        _appState = new ApplicationState();
        _appState.Field.LocalPlane = new LocalPlane(
            new Wgs84(43.7128, -74.006), new SharedFieldProperties());

        _gpsService = new GpsService();
        _gpsService.Start();

        var toolPosition = new ToolPositionService();
        var coverage = new CoverageMapService();
        var sectionControl = new SectionControlService(toolPosition, coverage, _appState);
        var autoSteer = new AutoSteerService(new TrackGuidanceService(),
            Substitute.For<IUdpCommunicationService>(), _gpsService, _appState);

        var headingFusion = Substitute.For<IGpsHeadingFusionService>();
        headingFusion.FuseHeading(Arg.Any<double>(), Arg.Any<double>(), Arg.Any<bool>(),
                Arg.Any<double>(), Arg.Any<double>(), Arg.Any<double>())
            .Returns(ci => ci.ArgAt<double>(0));

        _estimator = new PositionEstimator();

        _pipeline = new GpsPipelineService(
            _gpsService, toolPosition, new TrackGuidanceService(),
            sectionControl, coverage, autoSteer,
            new YouTurnGuidanceService(),
            new YouTurnStateMachine(
                new YouTurnCreationService(NullLogger<YouTurnCreationService>.Instance,
                    Substitute.For<AgValoniaGPS.Services.Geometry.IPolygonOffsetService>()),
                new YouTurnPathingService(NullLogger<YouTurnPathingService>.Instance),
                NullLogger<YouTurnStateMachine>.Instance),
            Substitute.For<IAudioService>(),
            new PipelineIntents(),
            headingFusion,
            NullLogger<GpsPipelineService>.Instance, _appState,
            _estimator);

        _pipeline.SynchronousMode = true;
        _pipeline.Start();
    }

    [TearDown]
    public void TearDown()
    {
        _pipeline.Stop();
        _gpsService.Stop();
    }

    [Test]
    public void OnGpsCycle_PublishesSnapshotToEstimator()
    {
        Assert.That(_estimator.GetLatestSnapshot(), Is.Null,
            "No snapshot before any GPS frame");

        _gpsService.UpdateGpsData(BuildGpsData(
            latitude: 43.7128, longitude: -74.006,
            heading: 90, speed: 5.0,
            imuValid: true, imuYawRateDegPerSec: 12, imuRollDeg: 1.5));

        var snap = _estimator.GetLatestSnapshot();
        Assert.That(snap, Is.Not.Null, "Snapshot should land after one GPS cycle");
        Assert.That(snap!.SpeedMps, Is.EqualTo(5.0).Within(1e-9));
        Assert.That(snap.Heading, Is.EqualTo(Math.PI / 2).Within(1e-6),
            "Heading should be in radians (90° → π/2)");
        Assert.That(snap.YawRateRadPerSec, Is.EqualTo(12 * Math.PI / 180).Within(1e-6),
            "YawRate should be in rad/s (12 deg/s converted)");
        Assert.That(snap.Roll, Is.EqualTo(1.5 * Math.PI / 180).Within(1e-6),
            "Roll should be in radians (1.5° converted)");
    }

    [Test]
    public void OnGpsCycle_ImuInvalid_PublishesZeroYawRateAndRoll()
    {
        _gpsService.UpdateGpsData(BuildGpsData(
            latitude: 43.7128, longitude: -74.006,
            heading: 0, speed: 3.0,
            imuValid: false, imuYawRateDegPerSec: 99, imuRollDeg: 99));

        var snap = _estimator.GetLatestSnapshot();
        Assert.That(snap, Is.Not.Null);
        Assert.That(snap!.YawRateRadPerSec, Is.EqualTo(0));
        Assert.That(snap.Roll, Is.EqualTo(0));
    }

    [Test]
    public void OnSecondGpsCycle_SnapshotIsReplaced()
    {
        _gpsService.UpdateGpsData(BuildGpsData(43.7128, -74.006, heading: 0, speed: 1.0));
        var firstSnap = _estimator.GetLatestSnapshot()!;

        _gpsService.UpdateGpsData(BuildGpsData(43.7129, -74.006, heading: 0, speed: 2.0));
        var secondSnap = _estimator.GetLatestSnapshot()!;

        Assert.That(secondSnap, Is.Not.SameAs(firstSnap));
        Assert.That(secondSnap.SpeedMps, Is.EqualTo(2.0).Within(1e-9));
    }

    private static GpsData BuildGpsData(
        double latitude, double longitude,
        double heading = 0, double speed = 0,
        bool imuValid = false,
        double imuYawRateDegPerSec = 0, double imuRollDeg = 0)
    {
        return new GpsData
        {
            CurrentPosition = new Position
            {
                Latitude = latitude,
                Longitude = longitude,
                Heading = heading,
                Speed = speed,
            },
            FixQuality = 4,
            ImuValid = imuValid,
            ImuYawRate = imuYawRateDegPerSec,
            ImuRoll = imuRollDeg,
            IsValid = true,
        };
    }
}

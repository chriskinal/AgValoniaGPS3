// AgValoniaGPS
// Copyright (C) 2024-2026 AgValoniaGPS Contributors
//
// Licensed under GNU GPL v3. See LICENSE.md.

using System.Collections.Generic;
using AgValoniaGPS.Models;
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
/// Origin-guard contract: GpsPipelineService must detect when the live
/// GPS source has wandered far from the LocalPlane origin and either
/// silently re-anchor the temporary plane (no field) or surface a one-shot
/// far-from-field warning (field loaded). The hard cases are
/// off-thread — manual repro requires swapping GPS sources mid-session —
/// so the cycle behavior is locked here.
/// </summary>
[TestFixture]
[NonParallelizable] // ConfigurationStore is a singleton.
public class OriginGuardCycleTests
{
    private GpsService _gpsService = null!;
    private GpsPipelineService _pipeline = null!;
    private ApplicationState _appState = null!;
    private List<GpsCycleResult> _results = null!;

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
            new PositionEstimator());

        _pipeline.SynchronousMode = true;
        _pipeline.Start();

        _results = new List<GpsCycleResult>();
        _pipeline.CycleCompleted += r => _results.Add(r);
    }

    [TearDown]
    public void TearDown()
    {
        _pipeline.Stop();
        _gpsService.Stop();
    }

    private GpsCycleResult Last => _results[^1];

    private static GpsData At(double lat, double lon) => new()
    {
        CurrentPosition = new Position { Latitude = lat, Longitude = lon },
        FixQuality = 4,
        IsValid = true,
    };

    [Test]
    public void TempOrigin_WithinThreshold_DoesNotReanchor()
    {
        // No active field by default; GPS ~1 km from the origin.
        _gpsService.UpdateGpsData(At(43.72, -74.01));

        Assert.Multiple(() =>
        {
            Assert.That(Last.ReplacementLocalPlane, Is.Null);
            Assert.That(Last.ReplacementDistanceKm, Is.EqualTo(0.0));
            Assert.That(Last.FarFromFieldWarning, Is.Null);
        });
    }

    [Test]
    public void TempOrigin_BeyondThreshold_EmitsReplacementPlane()
    {
        // Stockholm: ~6300 km from the upstate-NY origin. No active field
        // → silent re-anchor channel must fire.
        _gpsService.UpdateGpsData(At(59.3293, 18.0686));

        Assert.Multiple(() =>
        {
            Assert.That(Last.ReplacementLocalPlane, Is.Not.Null,
                "GPS jumped >50 km from temp origin with no field loaded → must re-anchor");
            Assert.That(Last.ReplacementDistanceKm, Is.GreaterThan(50.0),
                "Reported distance must reflect the offset that triggered re-anchor");
            Assert.That(Last.FarFromFieldWarning, Is.Null,
                "Far-from-field warning is the field-loaded channel only");
        });
    }

    [Test]
    public void TempOrigin_AfterReanchor_DoesNotReanchorAgainNearby()
    {
        // First cycle: trigger re-anchor.
        _gpsService.UpdateGpsData(At(59.3293, 18.0686));
        var newPlane = Last.ReplacementLocalPlane;
        Assert.That(newPlane, Is.Not.Null, "precondition: first cycle re-anchors");

        // Mirror what ApplyGpsCycleResult does on the UI thread so the next
        // cycle reads the new origin from ApplicationState.
        _appState.Field.LocalPlane = newPlane;

        // Second cycle: stay near the new origin → no further replacement.
        _gpsService.UpdateGpsData(At(59.330, 18.069));

        Assert.That(Last.ReplacementLocalPlane, Is.Null,
            "After re-anchor + commit, nearby positions must not retrigger");
    }

    [Test]
    public void FieldLoaded_WithinThreshold_NoWarning()
    {
        _pipeline.SetHasActiveField(true);
        _gpsService.UpdateGpsData(At(43.72, -74.01)); // ~1 km from field origin

        Assert.Multiple(() =>
        {
            Assert.That(Last.FarFromFieldWarning, Is.Null);
            Assert.That(Last.ReplacementLocalPlane, Is.Null,
                "Field loaded → silent re-anchor must never fire");
        });
    }

    [Test]
    public void FieldLoaded_BeyondThreshold_EmitsWarningOnce()
    {
        _pipeline.SetHasActiveField(true);

        // ~14 km north of the field origin.
        _gpsService.UpdateGpsData(At(43.85, -74.006));
        Assert.That(Last.FarFromFieldWarning, Is.Not.Null,
            "First far-from-field cycle must emit a warning");
        Assert.That(Last.FarFromFieldWarning!.DistanceMeters, Is.GreaterThan(10_000));
        Assert.That(Last.ReplacementLocalPlane, Is.Null,
            "Field loaded → no silent re-anchor");

        // Second far cycle: latch holds, no repeated warning.
        _gpsService.UpdateGpsData(At(43.86, -74.006));
        Assert.That(Last.FarFromFieldWarning, Is.Null,
            "One-shot latch must suppress repeats so the dialog is not nag-spammed");
    }

    [Test]
    public void FieldReopen_ResetsWarningLatch()
    {
        _pipeline.SetHasActiveField(true);

        _gpsService.UpdateGpsData(At(43.85, -74.006));
        Assert.That(Last.FarFromFieldWarning, Is.Not.Null, "precondition: first warning fires");

        _gpsService.UpdateGpsData(At(43.86, -74.006));
        Assert.That(Last.FarFromFieldWarning, Is.Null, "precondition: latch holds");

        // Close + reopen mirrors MainViewModel.ClearFieldState + OpenFieldAsync.
        _pipeline.SetHasActiveField(false);
        _pipeline.SetHasActiveField(true);

        _gpsService.UpdateGpsData(At(43.87, -74.006));
        Assert.That(Last.FarFromFieldWarning, Is.Not.Null,
            "Reopening a field must clear the one-shot latch so the next jump warns again");
    }
}

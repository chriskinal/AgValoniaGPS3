// AgValoniaGPS
// Copyright (C) 2024-2026 AgValoniaGPS Contributors
//
// Licensed under GNU GPL v3. See LICENSE.md.

using System.Text;
using AgValoniaGPS.Models;
using AgValoniaGPS.Models.State;
using AgValoniaGPS.Services.AutoSteer;
using AgValoniaGPS.Services.Interfaces;
using NSubstitute;

namespace AgValoniaGPS.Services.Tests.Pipeline;

/// <summary>
/// End-of-Phase-B locks for the unified pipeline contract. Each test
/// asserts a Phase B invariant that must not regress in later phases.
/// Coverage for coord-conversion behavior that used to live in
/// SimulatorDataFlowTests also lives here now.
/// </summary>
[TestFixture]
public class UnifiedPipelineTests
{
    private static byte[] Bytes
    {
        get
        {
            const string body = "PANDA,120000,4807.038,N,01131.000,E,4,12,0.8,340.0,1.0,5.4,123.4,0.5,-0.3,1.2";
            byte cs = 0;
            foreach (char c in body) cs ^= (byte)c;
            return Encoding.ASCII.GetBytes($"${body}*{cs:X2}");
        }
    }

    /// <summary>
    /// C4 contract: ProcessGpsBuffer does parse-and-publish only.
    /// Guidance and UDP send must NOT happen on the receive thread.
    /// If this test fails, someone reintroduced receive-thread domain work.
    /// </summary>
    [Test]
    public void ProcessGpsBuffer_does_not_invoke_guidance_or_UDP_send()
    {
        var mockGuidance = Substitute.For<ITrackGuidanceService>();
        var mockUdp = Substitute.For<IUdpCommunicationService>();
        var mockGps = Substitute.For<IGpsService>();
        var appState = new ApplicationState();

        // Preseed a LocalPlane and an active track to make sure ProcessGpsBuffer
        // has every dependency it *would* need for guidance/PGN work.
        appState.Field.LocalPlane = new LocalPlane(
            new Wgs84(48.117, 11.517), new SharedFieldProperties());

        var autoSteer = new AutoSteerService(mockGuidance, mockUdp, mockGps, appState);
        autoSteer.Start();
        // Start() now emits a baseline PGN 251 + PGN 252 pair; that
        // happens off the receive thread so it doesn't violate C4, but
        // it does show up on the UDP mock. Clear it so the SendToModules
        // assertion below only counts receive-thread sends.
        mockUdp.ClearReceivedCalls();
        autoSteer.SetCurrentTrack(new Models.Track.Track
        {
            Name = "test",
            Points = new() { new(0, 0, 0), new(0, 100, 0) },
        });

        autoSteer.ProcessGpsBuffer(Bytes, Bytes.Length);

        Assert.Multiple(() =>
        {
            mockGuidance.DidNotReceive().CalculateGuidance(Arg.Any<Models.Track.TrackGuidanceInput>());
            mockUdp.DidNotReceive().SendToModules(Arg.Any<byte[]>());
            // Positive control: parse → publish did happen.
            mockGps.Received(1).UpdateGpsData(Arg.Any<GpsData>());
        });
    }

    /// <summary>
    /// C1 invariant: AutoSteerService reads LocalPlane from ApplicationState.Field;
    /// it never creates or replaces the shared instance. If this test fails,
    /// someone reintroduced a private LocalPlane or an auto-create path.
    /// </summary>
    [Test]
    public void AutoSteer_does_not_replace_ApplicationState_LocalPlane()
    {
        var appState = new ApplicationState();
        var original = new LocalPlane(
            new Wgs84(48.117, 11.517), new SharedFieldProperties());
        appState.Field.LocalPlane = original;

        var autoSteer = new AutoSteerService(
            Substitute.For<ITrackGuidanceService>(),
            Substitute.For<IUdpCommunicationService>(),
            Substitute.For<IGpsService>(),
            appState);
        autoSteer.Start();

        autoSteer.ProcessGpsBuffer(Bytes, Bytes.Length);

        Assert.That(appState.Field.LocalPlane, Is.SameAs(original),
            "AutoSteerService must not replace the shared LocalPlane instance");
    }

    /// <summary>
    /// C1 invariant companion: with no LocalPlane set, AutoSteerService
    /// must leave it null — auto-create is owned by the cycle worker / field-open.
    /// </summary>
    [Test]
    public void AutoSteer_does_not_auto_create_LocalPlane()
    {
        var appState = new ApplicationState();
        Assert.That(appState.Field.LocalPlane, Is.Null, "precondition");

        var autoSteer = new AutoSteerService(
            Substitute.For<ITrackGuidanceService>(),
            Substitute.For<IUdpCommunicationService>(),
            Substitute.For<IGpsService>(),
            appState);
        autoSteer.Start();

        autoSteer.ProcessGpsBuffer(Bytes, Bytes.Length);

        Assert.That(appState.Field.LocalPlane, Is.Null,
            "AutoSteerService must not create its own LocalPlane on receive thread");
    }
}

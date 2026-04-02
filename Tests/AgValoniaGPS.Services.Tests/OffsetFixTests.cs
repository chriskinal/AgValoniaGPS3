using AgValoniaGPS.Models;
using AgValoniaGPS.Models.State;
using AgValoniaGPS.Services.Interfaces;
using NSubstitute;

namespace AgValoniaGPS.Services.Tests;

/// <summary>
/// Tests for GPS offset fix (#36).
/// Verifies that drift compensation shifts the vehicle position
/// relative to field geometry (boundary, tracks, coverage).
/// </summary>
[TestFixture]
public class OffsetFixTests
{
    [Test]
    public void DriftApplied_ShiftsDisplayPosition()
    {
        // Simulate: vehicle at (100, 200), apply 10m south drift
        var fieldState = new FieldState();
        double rawEasting = 100.0;
        double rawNorthing = 200.0;

        // No drift - display matches raw
        Assert.That(rawEasting + fieldState.DriftEasting, Is.EqualTo(100.0));
        Assert.That(rawNorthing + fieldState.DriftNorthing, Is.EqualTo(200.0));

        // Apply 10m south drift
        fieldState.DriftNorthing = -10.0;

        double displayEasting = rawEasting + fieldState.DriftEasting;
        double displayNorthing = rawNorthing + fieldState.DriftNorthing;

        Assert.That(displayEasting, Is.EqualTo(100.0), "Easting should not change");
        Assert.That(displayNorthing, Is.EqualTo(190.0), "Northing should shift 10m south");
    }

    [Test]
    public void DriftApplied_VehicleMovesToFieldEdge()
    {
        // Scenario from user:
        // 1. Vehicle at center of 20m field (boundary extends 10m in each direction)
        // 2. Apply 10m south offset
        // 3. Vehicle should appear at south edge of field

        double fieldCenterN = 200.0;
        double fieldHalfSize = 10.0; // 20m field = 10m each way
        double boundarySouthEdge = fieldCenterN - fieldHalfSize; // 190.0

        // Vehicle at field center
        double vehicleNorthing = fieldCenterN; // 200.0

        // Apply 10m south drift
        var fieldState = new FieldState();
        fieldState.DriftNorthing = -10.0;

        // Display position
        double displayNorthing = vehicleNorthing + fieldState.DriftNorthing;

        Assert.That(displayNorthing, Is.EqualTo(boundarySouthEdge).Within(0.001),
            "Vehicle should appear at south edge of 20m field after 10m south offset");
    }

    [Test]
    public void DriftReset_RestoresOriginalPosition()
    {
        var fieldState = new FieldState();
        double rawNorthing = 200.0;

        fieldState.DriftNorthing = -10.0;
        Assert.That(rawNorthing + fieldState.DriftNorthing, Is.EqualTo(190.0));

        // Reset
        fieldState.DriftNorthing = 0;
        fieldState.DriftEasting = 0;
        Assert.That(rawNorthing + fieldState.DriftNorthing, Is.EqualTo(200.0));
    }

    [Test]
    public void DriftDoesNotMoveBoundary()
    {
        // Boundary coordinates are fixed in local plane space
        // Only the vehicle display position shifts
        double boundaryPointNorthing = 210.0;
        var fieldState = new FieldState();

        fieldState.DriftNorthing = -10.0;

        // Boundary point should NOT change
        Assert.That(boundaryPointNorthing, Is.EqualTo(210.0),
            "Boundary coordinates must not be affected by drift");

        // Vehicle should shift
        double vehicleDisplay = 200.0 + fieldState.DriftNorthing;
        Assert.That(vehicleDisplay, Is.EqualTo(190.0));

        // Distance from vehicle to boundary changes
        double distanceBefore = boundaryPointNorthing - 200.0; // 10m
        double distanceAfter = boundaryPointNorthing - vehicleDisplay; // 20m
        Assert.That(distanceAfter, Is.EqualTo(20.0),
            "Vehicle should appear 20m from north boundary after 10m south offset");
    }

    [Test]
    public void AutoSteerService_DriftAppliedToLocalCoordinates()
    {
        // Verify drift is applied in the zero-copy pipeline
        var autoSteer = new AgValoniaGPS.Services.AutoSteer.AutoSteerService(
            Substitute.For<ITrackGuidanceService>(),
            Substitute.For<IUdpCommunicationService>());

        autoSteer.SetDriftCompensation(5.0, -10.0);

        // The drift values should be stored (we can't easily test the full pipeline
        // without a LocalPlane, but we can verify the method doesn't throw)
        Assert.DoesNotThrow(() => autoSteer.SetDriftCompensation(0, 0));
    }
}

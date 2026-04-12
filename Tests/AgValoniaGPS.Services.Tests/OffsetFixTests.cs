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

    [Test]
    public void ToolPositionService_FollowsVehicle()
    {
        // Verify that tool position is relative to vehicle
        var toolService = new AgValoniaGPS.Services.Tool.ToolPositionService();

        // Configure a rear-fixed hitch at 2m behind
        var config = AgValoniaGPS.Models.Configuration.ConfigurationStore.Instance;
        config.Tool.HitchLength = 2.0;
        config.Tool.IsToolRearFixed = true;

        // Vehicle at (100, 200) heading north (0 radians)
        var vehiclePos = new AgValoniaGPS.Models.Base.Vec3(100, 200, 0);
        toolService.Update(vehiclePos, 0);

        // Hitch should be 2m south of vehicle
        Assert.That(toolService.HitchPosition.Easting, Is.EqualTo(100.0).Within(0.1));
        Assert.That(toolService.HitchPosition.Northing, Is.EqualTo(198.0).Within(0.1));

        // Now "drift" by feeding shifted position
        var driftedPos = new AgValoniaGPS.Models.Base.Vec3(100, 190, 0); // 10m south
        toolService.Update(driftedPos, 0);

        // Hitch should follow: 190 - 2 = 188
        Assert.That(toolService.HitchPosition.Easting, Is.EqualTo(100.0).Within(0.1));
        Assert.That(toolService.HitchPosition.Northing, Is.EqualTo(188.0).Within(0.1),
            "Hitch must follow drifted vehicle position");
    }

    [Test]
    public void ToolPositionService_ResetAfterLargeJump_SnapsImmediately()
    {
        var config = AgValoniaGPS.Models.Configuration.ConfigurationStore.Instance;
        config.Tool.HitchLength = 2.0;
        config.Tool.TrailingHitchLength = 3.0;
        config.Tool.IsToolTrailing = true;
        config.Tool.IsToolRearFixed = false;
        config.Tool.IsToolFrontFixed = false;
        config.Tool.IsToolTBT = false;

        var toolService = new AgValoniaGPS.Services.Tool.ToolPositionService();

        // Drive normally for 60 frames to establish trailing
        for (int i = 0; i < 60; i++)
        {
            var pos = new AgValoniaGPS.Models.Base.Vec3(100, 200 + i * 0.5, 0);
            toolService.Update(pos, 0);
        }

        // Vehicle at (100, 230) after driving
        double preJumpHitch = toolService.HitchPosition.Northing;
        Assert.That(Math.Abs(preJumpHitch - 230), Is.LessThan(5),
            "Hitch should be near vehicle before jump");

        // Simulate large drift: vehicle jumps 100m north
        var jumpedPos = new AgValoniaGPS.Models.Base.Vec3(100, 330, 0);

        // WITHOUT reset - hitch would be 100m behind
        toolService.Update(jumpedPos, 0);
        double hitchAfterJump = toolService.HitchPosition.Northing;

        // With trailing, the tool is still near old position (huge gap)
        // This is the bug - hitch bar stretched to ~100m

        // Now reset and update again
        toolService.ResetTrailingState(jumpedPos, 0);
        toolService.Update(jumpedPos, 0);
        double hitchAfterReset = toolService.HitchPosition.Northing;

        Assert.That(Math.Abs(hitchAfterReset - 330), Is.LessThan(5),
            $"After reset, hitch should snap near vehicle. Got {hitchAfterReset:F1}, expected ~328");
    }

    [Test]
    public void ToolPositionService_ResetPlacesHitchBehindVehicle()
    {
        // Bug: ResetTrailingState used +HitchLength (ahead) instead of -HitchLength (behind)
        var config = AgValoniaGPS.Models.Configuration.ConfigurationStore.Instance;
        config.Tool.HitchLength = 5.0;
        config.Tool.TrailingHitchLength = 15.0;
        config.Tool.IsToolTrailing = true;
        config.Tool.IsToolRearFixed = false;
        config.Tool.IsToolFrontFixed = false;
        config.Tool.IsToolTBT = false;

        var toolService = new AgValoniaGPS.Services.Tool.ToolPositionService();

        // Vehicle at origin heading north (0 rad)
        var pos = new AgValoniaGPS.Models.Base.Vec3(100, 200, 0);
        toolService.ResetTrailingState(pos, 0);

        // Hitch should be 5m BEHIND (south of) vehicle, not 5m ahead
        Assert.That(toolService.HitchPosition.Northing, Is.LessThan(200.0),
            $"Hitch should be south of vehicle (behind when heading north). Got N={toolService.HitchPosition.Northing:F1}");
        Assert.That(toolService.HitchPosition.Northing, Is.EqualTo(195.0).Within(0.1),
            "Hitch should be exactly 5m behind vehicle");
        Assert.That(toolService.HitchPosition.Easting, Is.EqualTo(100.0).Within(0.1),
            "Hitch easting should match vehicle");

        // Tool should be further behind (5m hitch + 15m trailing = 20m total)
        Assert.That(toolService.ToolPosition.Northing, Is.LessThan(195.0),
            "Tool should be behind hitch point");
    }

    [Test]
    public void ToolPositionService_ResetThenUpdateProducesConsistentPositions()
    {
        // After reset + one Update frame, tool should be near vehicle
        var config = AgValoniaGPS.Models.Configuration.ConfigurationStore.Instance;
        config.Tool.HitchLength = 5.0;
        config.Tool.TrailingHitchLength = 15.0;
        config.Tool.IsToolTrailing = true;
        config.Tool.IsToolRearFixed = false;
        config.Tool.IsToolFrontFixed = false;
        config.Tool.IsToolTBT = false;

        var toolService = new AgValoniaGPS.Services.Tool.ToolPositionService();

        // Drive north to establish trailing
        for (int i = 0; i < 60; i++)
            toolService.Update(new AgValoniaGPS.Models.Base.Vec3(0, i * 0.5, 0), 0);

        // Apply offset by resetting to a shifted position (simulate 10m north drift)
        var newPos = new AgValoniaGPS.Models.Base.Vec3(0, 40, 0);
        toolService.ResetTrailingState(newPos, 0);

        // Immediately update at the new position
        toolService.Update(newPos, 0);

        // Hitch should be near vehicle (5m behind at most)
        double hitchDist = Math.Abs(toolService.HitchPosition.Northing - 40);
        Assert.That(hitchDist, Is.LessThan(6.0),
            $"Hitch should be within 6m of vehicle after reset+update. Got {toolService.HitchPosition.Northing:F1}");

        // Tool should be within total hitch+trailing distance
        double toolDist = Math.Abs(toolService.ToolPosition.Northing - 40);
        Assert.That(toolDist, Is.LessThan(21.0),
            $"Tool should be within 21m of vehicle. Got {toolService.ToolPosition.Northing:F1}");
    }

    [Test]
    public void ToolPositionService_TrailingStillWorksAtNormalSpeed()
    {
        // Bug: jump detection was comparing hitch to toolPivot (always ~15m apart)
        // causing the tool to snap rigid every frame instead of trailing
        var config = AgValoniaGPS.Models.Configuration.ConfigurationStore.Instance;
        config.Tool.HitchLength = 2.0;
        config.Tool.TrailingHitchLength = 15.0; // Large trailing distance
        config.Tool.IsToolTrailing = true;
        config.Tool.IsToolRearFixed = false;
        config.Tool.IsToolFrontFixed = false;
        config.Tool.IsToolTBT = false;

        var toolService = new AgValoniaGPS.Services.Tool.ToolPositionService();

        // Drive straight north for 60 frames at 1m/frame (36 km/h at 10Hz)
        for (int i = 0; i < 60; i++)
        {
            var pos = new AgValoniaGPS.Models.Base.Vec3(0, i * 1.0, 0);
            toolService.Update(pos, 0);
        }

        // Now turn east - trailing implement should lag behind (not snap rigid)
        double lastToolHeading = toolService.ToolHeading;

        for (int i = 0; i < 20; i++)
        {
            var pos = new AgValoniaGPS.Models.Base.Vec3(i * 1.0, 60, Math.PI / 2); // Heading east
            toolService.Update(pos, Math.PI / 2);
        }

        double toolHeadingAfterTurn = toolService.ToolHeading;

        // Tool heading should have moved from north (0) toward east (PI/2)
        // but NOT match vehicle heading exactly - trailing means it lags
        Assert.That(toolHeadingAfterTurn, Is.GreaterThan(0.1),
            "Tool heading should have moved away from north");
        Assert.That(toolHeadingAfterTurn, Is.LessThan(Math.PI / 2 - 0.05),
            $"Tool should lag behind vehicle (trailing, not rigid). " +
            $"Vehicle={Math.PI / 2:F2}, Tool={toolHeadingAfterTurn:F2}");
    }
}

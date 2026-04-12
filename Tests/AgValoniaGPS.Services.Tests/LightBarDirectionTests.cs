using AgValoniaGPS.Models.Base;
using AgValoniaGPS.Models.Track;
using AgValoniaGPS.Services.Track;
using TrackModel = AgValoniaGPS.Models.Track.Track;

namespace AgValoniaGPS.Services.Tests;

/// <summary>
/// Verifies that the CrossTrackError sign is correct for light bar arrow direction
/// when driving in both directions along an AB line.
///
/// Convention:
///   Negative XTE = vehicle is LEFT of track (relative to travel direction) = green arrows on RIGHT
///   Positive XTE = vehicle is RIGHT of track (relative to travel direction) = red arrows on LEFT
///
/// After a 180-degree turn, the XTE sign must flip so the arrows still point toward the line.
/// </summary>
[TestFixture]
public class LightBarDirectionTests
{
    private TrackGuidanceService _service = null!;
    private TrackModel _northSouthTrack = null!;

    // AB line running south to north: A at (0,0), B at (0,100)
    // Track heading = 0 (north)
    private const double HeadingNorth = 0.0;
    private const double HeadingSouth = Math.PI;
    private const double OffsetEast = 3.0;   // 3m east of the line
    private const double OffsetWest = -3.0;  // 3m west of the line
    private const double Wheelbase = 2.5;

    [SetUp]
    public void SetUp()
    {
        _service = new TrackGuidanceService();
        _northSouthTrack = TrackModel.FromABLine(
            "NS Line",
            new Vec3(0, 0, 0),
            new Vec3(0, 100, 0));
    }

    // --- Driving A to B (northbound, same way as track) ---

    [Test]
    public void DrivingNorth_VehicleLeftOfLine_XteIsNegative()
    {
        // Vehicle is 3m WEST (left when facing north) of the line, driving north
        var output = RunGuidance(
            easting: OffsetWest,
            northing: 50,
            heading: HeadingNorth);

        Assert.That(output.CrossTrackError, Is.Negative,
            "Vehicle left of track (west, driving north) should produce negative XTE (green arrows right)");
        Assert.That(output.CrossTrackError, Is.EqualTo(-3.0).Within(0.2));
    }

    [Test]
    public void DrivingNorth_VehicleRightOfLine_XteIsPositive()
    {
        // Vehicle is 3m EAST (right when facing north) of the line, driving north
        var output = RunGuidance(
            easting: OffsetEast,
            northing: 50,
            heading: HeadingNorth);

        Assert.That(output.CrossTrackError, Is.Positive,
            "Vehicle right of track (east, driving north) should produce positive XTE (red arrows left)");
        Assert.That(output.CrossTrackError, Is.EqualTo(3.0).Within(0.2));
    }

    // --- Driving B to A (southbound, opposite to track) ---
    // After turning 180 degrees, what was "east of the line" is now "left of travel direction"
    // and the XTE sign must reflect the new relative position.

    [Test]
    public void DrivingSouth_VehicleLeftOfLine_XteIsNegative()
    {
        // Vehicle is 3m EAST of the line, driving south.
        // When facing south, east is to the LEFT.
        // So relative to travel direction, vehicle is LEFT of line => XTE should be negative.
        var output = RunGuidance(
            easting: OffsetEast,
            northing: 50,
            heading: HeadingSouth);

        Assert.That(output.CrossTrackError, Is.Negative,
            "Vehicle left of track (east, driving south) should produce negative XTE (green arrows right)");
        Assert.That(output.CrossTrackError, Is.EqualTo(-3.0).Within(0.2));
    }

    [Test]
    public void DrivingSouth_VehicleRightOfLine_XteIsPositive()
    {
        // Vehicle is 3m WEST of the line, driving south.
        // When facing south, west is to the RIGHT.
        // So relative to travel direction, vehicle is RIGHT of line => XTE should be positive.
        var output = RunGuidance(
            easting: OffsetWest,
            northing: 50,
            heading: HeadingSouth);

        Assert.That(output.CrossTrackError, Is.Positive,
            "Vehicle right of track (west, driving south) should produce positive XTE (red arrows left)");
        Assert.That(output.CrossTrackError, Is.EqualTo(3.0).Within(0.2));
    }

    // --- Symmetry: same physical position, opposite heading, XTE signs must be opposite ---

    [Test]
    public void SamePosition_OppositeHeading_XteSignFlips()
    {
        // Vehicle at 3m east of line
        var outputNorth = RunGuidance(
            easting: OffsetEast,
            northing: 50,
            heading: HeadingNorth);

        var outputSouth = RunGuidance(
            easting: OffsetEast,
            northing: 50,
            heading: HeadingSouth);

        Assert.That(outputNorth.CrossTrackError, Is.Positive,
            "Driving north, east of line = right of track = positive XTE");
        Assert.That(outputSouth.CrossTrackError, Is.Negative,
            "Driving south, east of line = left of track = negative XTE");

        // The absolute values should be equal (same physical offset from line)
        Assert.That(Math.Abs(outputNorth.CrossTrackError),
            Is.EqualTo(Math.Abs(outputSouth.CrossTrackError)).Within(0.1),
            "XTE magnitude should be the same regardless of travel direction");
    }

    // --- On the line: XTE should be near zero regardless of direction ---

    [Test]
    public void OnLine_DrivingNorth_XteIsNearZero()
    {
        var output = RunGuidance(
            easting: 0,
            northing: 50,
            heading: HeadingNorth);

        Assert.That(Math.Abs(output.CrossTrackError), Is.LessThan(0.01),
            "XTE should be near zero when vehicle is on the line");
    }

    [Test]
    public void OnLine_DrivingSouth_XteIsNearZero()
    {
        var output = RunGuidance(
            easting: 0,
            northing: 50,
            heading: HeadingSouth);

        Assert.That(Math.Abs(output.CrossTrackError), Is.LessThan(0.01),
            "XTE should be near zero when vehicle is on the line, even driving opposite direction");
    }

    // --- Stanley algorithm: same direction rules should apply ---

    [Test]
    public void Stanley_DrivingNorth_VehicleRightOfLine_XteIsPositive()
    {
        var output = RunGuidance(
            easting: OffsetEast,
            northing: 50,
            heading: HeadingNorth,
            useStanley: true);

        Assert.That(output.CrossTrackError, Is.Positive,
            "Stanley: vehicle right of track (east, driving north) should produce positive XTE");
        Assert.That(output.CrossTrackError, Is.EqualTo(3.0).Within(0.2));
    }

    [Test]
    public void Stanley_DrivingSouth_VehicleLeftOfLine_XteIsNegative()
    {
        // East of line, driving south = left of travel direction
        var output = RunGuidance(
            easting: OffsetEast,
            northing: 50,
            heading: HeadingSouth,
            useStanley: true);

        Assert.That(output.CrossTrackError, Is.Negative,
            "Stanley: vehicle left of track (east, driving south) should produce negative XTE");
        Assert.That(output.CrossTrackError, Is.EqualTo(-3.0).Within(0.2));
    }

    #region Helpers

    private TrackGuidanceOutput RunGuidance(
        double easting,
        double northing,
        double heading,
        bool useStanley = false)
    {
        // Steer position is slightly ahead of pivot in the direction of travel
        double steerEasting = easting + Math.Sin(heading) * Wheelbase;
        double steerNorthing = northing + Math.Cos(heading) * Wheelbase;

        var input = new TrackGuidanceInput
        {
            Track = _northSouthTrack,
            PivotPosition = new Vec3(easting, northing, heading),
            SteerPosition = new Vec3(steerEasting, steerNorthing, heading),
            UseStanley = useStanley,
            Wheelbase = Wheelbase,
            MaxSteerAngle = 35,
            GoalPointDistance = 5,
            FixHeading = heading,
            AvgSpeed = 10,
            IsHeadingSameWay = true, // This is recalculated internally by the service
            FindGlobalNearest = true,
            StanleyHeadingErrorGain = 1.0,
            StanleyDistanceErrorGain = 0.8
        };

        return _service.CalculateGuidance(input);
    }

    #endregion
}

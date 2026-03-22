using AgValoniaGPS.Models.Base;
using AgValoniaGPS.Models.Track;
using AgValoniaGPS.Services.Track;
using TrackModel = AgValoniaGPS.Models.Track.Track;

namespace AgValoniaGPS.Services.Tests;

[TestFixture]
public class TrackGuidanceServiceTests
{
    private TrackGuidanceService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _service = new TrackGuidanceService();
    }

    #region AB Line Pure Pursuit

    [Test]
    public void ABLine_PurePursuit_VehicleRightOfLine_SteersLeft()
    {
        var track = TrackModel.FromABLine(
            "Test AB",
            new Vec3(0, 0, 0),
            new Vec3(0, 100, 0));

        var input = new TrackGuidanceInput
        {
            Track = track,
            PivotPosition = new Vec3(2, 50, 0),
            SteerPosition = new Vec3(2, 52.5, 0),
            UseStanley = false,
            Wheelbase = 2.5,
            MaxSteerAngle = 35,
            GoalPointDistance = 5,
            FixHeading = 0,
            AvgSpeed = 10,
            IsHeadingSameWay = true,
            FindGlobalNearest = true
        };

        var output = _service.CalculateGuidance(input);

        Assert.That(output.CrossTrackError, Is.EqualTo(2.0).Within(0.1),
            "XTE should be ~2m (vehicle 2m east of line)");
        Assert.That(output.SteerAngle, Is.LessThan(0),
            "Should steer left (negative) to return to line");
        Assert.That(Math.Abs(output.SteerAngle), Is.LessThan(35),
            "Steer angle should be within limits");
    }

    #endregion

    #region Curve Pure Pursuit

    [Test]
    public void Curve_PurePursuit_ReturnsReasonableOutput()
    {
        var points = new List<Vec3>();
        for (int i = 0; i <= 10; i++)
        {
            double angle = i * Math.PI / 20;
            double x = 20 * Math.Sin(angle);
            double y = 20 * (1 - Math.Cos(angle));
            points.Add(new Vec3(x, y, angle));
        }

        var track = TrackModel.FromCurve("Test Curve", points);

        var input = new TrackGuidanceInput
        {
            Track = track,
            PivotPosition = new Vec3(1, 1, 0.1),
            SteerPosition = new Vec3(1, 3.5, 0.1),
            UseStanley = false,
            Wheelbase = 2.5,
            MaxSteerAngle = 35,
            GoalPointDistance = 5,
            FixHeading = 0.1,
            AvgSpeed = 8,
            IsHeadingSameWay = true,
            FindGlobalNearest = true
        };

        var output = _service.CalculateGuidance(input);

        Assert.That(double.IsNaN(output.SteerAngle), Is.False, "Steer angle should not be NaN");
        Assert.That(Math.Abs(output.SteerAngle), Is.LessThanOrEqualTo(35));
        Assert.That(output.GoalPoint.Easting != 0 || output.GoalPoint.Northing != 0, Is.True,
            "Goal point should be non-zero");
    }

    #endregion

    #region Stanley Algorithm

    [Test]
    public void ABLine_Stanley_VehicleLeftOfLine_SteersRight()
    {
        var track = TrackModel.FromABLine(
            "Test AB",
            new Vec3(0, 0, 0),
            new Vec3(0, 100, 0));

        var input = new TrackGuidanceInput
        {
            Track = track,
            PivotPosition = new Vec3(-1.5, 50, 0),
            SteerPosition = new Vec3(-1.5, 52.5, 0),
            UseStanley = true,
            Wheelbase = 2.5,
            MaxSteerAngle = 35,
            GoalPointDistance = 5,
            StanleyHeadingErrorGain = 1.0,
            StanleyDistanceErrorGain = 0.8,
            FixHeading = 0,
            AvgSpeed = 10,
            IsHeadingSameWay = true,
            FindGlobalNearest = true
        };

        var output = _service.CalculateGuidance(input);

        Assert.That(output.CrossTrackError, Is.EqualTo(-1.5).Within(0.1),
            "XTE should be ~-1.5m (vehicle 1.5m west of line)");
        Assert.That(output.SteerAngle, Is.GreaterThan(0),
            "Should steer right (positive) to return to line");
        Assert.That(Math.Abs(output.SteerAngle), Is.LessThan(35));
    }

    #endregion

    #region Vehicle On Line

    [Test]
    public void ABLine_VehicleOnLine_NearZeroXteAndSteering()
    {
        var track = TrackModel.FromABLine(
            "Test AB",
            new Vec3(0, 0, 0),
            new Vec3(0, 100, 0));

        var input = new TrackGuidanceInput
        {
            Track = track,
            PivotPosition = new Vec3(0, 50, 0),
            SteerPosition = new Vec3(0, 52.5, 0),
            UseStanley = false,
            Wheelbase = 2.5,
            MaxSteerAngle = 35,
            GoalPointDistance = 5,
            FixHeading = 0,
            AvgSpeed = 10,
            IsHeadingSameWay = true,
            FindGlobalNearest = true
        };

        var output = _service.CalculateGuidance(input);

        Assert.That(Math.Abs(output.CrossTrackError), Is.LessThan(0.01),
            "XTE should be near zero when on line");
        Assert.That(Math.Abs(output.SteerAngle), Is.LessThan(1.0),
            "Steer angle should be near zero when on line");
    }

    #endregion

    #region Track Conversion Round-Trip

    [Test]
    public void ABLine_TrackConversion_RoundTrip_PreservesProperties()
    {
        var original = TrackModel.FromABLine(
            "Conversion Test",
            new Vec3(10, 20, 0.5),
            new Vec3(30, 40, 0.5));
        original.NudgeDistance = 1.5;
        original.IsVisible = true;

        var abLine = original.ToABLine();
        var roundTrip = TrackModel.FromABLine(abLine);

        Assert.That(roundTrip.Name, Is.EqualTo(original.Name));
        Assert.That(roundTrip.Points, Has.Count.EqualTo(original.Points.Count));
        Assert.That(roundTrip.NudgeDistance, Is.EqualTo(original.NudgeDistance).Within(0.001));
        Assert.That(roundTrip.IsVisible, Is.EqualTo(original.IsVisible));
        Assert.That(roundTrip.Points[0].Easting, Is.EqualTo(original.Points[0].Easting).Within(0.001));
        Assert.That(roundTrip.Points[0].Northing, Is.EqualTo(original.Points[0].Northing).Within(0.001));
    }

    #endregion
}

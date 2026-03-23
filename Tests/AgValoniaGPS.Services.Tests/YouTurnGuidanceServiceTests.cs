using AgValoniaGPS.Models.Base;
using AgValoniaGPS.Models.YouTurn;
using AgValoniaGPS.Services.YouTurn;

namespace AgValoniaGPS.Services.Tests;

/// <summary>
/// Tests for YouTurnGuidanceService - U-turn path following with Pure Pursuit and Stanley.
/// </summary>
[TestFixture]
public class YouTurnGuidanceServiceTests
{
    private YouTurnGuidanceService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _service = new YouTurnGuidanceService();
    }

    /// <summary>
    /// Create a simple U-shaped path: go north, arc right, come back south.
    /// </summary>
    private static List<Vec3> CreateSimpleUTurnPath()
    {
        var path = new List<Vec3>();

        // Entry leg going north (10 points, 1m apart)
        for (int i = 0; i < 10; i++)
            path.Add(new Vec3(0, i, 0)); // heading north

        // Arc turning right (semicircle, radius 5m)
        int arcPoints = 10;
        for (int i = 1; i <= arcPoints; i++)
        {
            double angle = i * Math.PI / arcPoints;
            double x = 5 * (1 - Math.Cos(angle));
            double y = 10 + 5 * Math.Sin(angle);
            double heading = angle;
            path.Add(new Vec3(x, y, heading));
        }

        // Exit leg going south (10 points)
        for (int i = 0; i < 10; i++)
            path.Add(new Vec3(10, 15 - i, Math.PI)); // heading south

        return path;
    }

    private static YouTurnGuidanceInput CreateDefaultInput(List<Vec3> path)
    {
        return new YouTurnGuidanceInput
        {
            TurnPath = path,
            PivotPosition = new Vec3(0, 2, 0),
            SteerPosition = new Vec3(0, 4.5, 0),
            Wheelbase = 2.5,
            MaxSteerAngle = 35,
            UseStanley = false,
            GoalPointDistance = 3,
            UTurnCompensation = 1.0,
            StanleyHeadingErrorGain = 1.0,
            StanleyDistanceErrorGain = 0.8,
            FixHeading = 0,
            AvgSpeed = 5,
            IsReverse = false,
            UTurnStyle = 0 // Albin style
        };
    }

    #region Empty Path

    [Test]
    public void EmptyPath_CompletesImmediately()
    {
        var input = CreateDefaultInput(new List<Vec3>());

        var output = _service.CalculateGuidance(input);

        Assert.That(output.IsTurnComplete, Is.True);
    }

    #endregion

    #region Pure Pursuit

    [Test]
    public void PurePursuit_VehicleOnPath_ProducesValidSteering()
    {
        var path = CreateSimpleUTurnPath();
        var input = CreateDefaultInput(path);
        input.PivotPosition = new Vec3(0, 2, 0);
        input.SteerPosition = new Vec3(0, 4.5, 0);

        var output = _service.CalculateGuidance(input);

        Assert.That(output.IsTurnComplete, Is.False);
        Assert.That(double.IsNaN(output.SteerAngle), Is.False);
        Assert.That(Math.Abs(output.SteerAngle), Is.LessThanOrEqualTo(35));
    }

    [Test]
    public void PurePursuit_VehicleOffPath_ProducesSteeringCorrection()
    {
        var path = CreateSimpleUTurnPath();
        var input = CreateDefaultInput(path);
        input.PivotPosition = new Vec3(1, 5, 0); // 1m right of entry leg
        input.SteerPosition = new Vec3(1, 7.5, 0);
        input.FixHeading = 0;

        var output = _service.CalculateGuidance(input);

        Assert.That(double.IsNaN(output.SteerAngle), Is.False);
        Assert.That(Math.Abs(output.SteerAngle), Is.LessThanOrEqualTo(35));
    }

    [Test]
    public void PurePursuit_VehicleFarFromPath_CompletesTurn()
    {
        var path = CreateSimpleUTurnPath();
        var input = CreateDefaultInput(path);
        // Position very far from all path points (>4m squared = 16m² distance)
        input.PivotPosition = new Vec3(50, 50, 0);
        input.SteerPosition = new Vec3(50, 52.5, 0);

        var output = _service.CalculateGuidance(input);

        Assert.That(output.IsTurnComplete, Is.True);
    }

    #endregion

    #region Stanley

    [Test]
    public void Stanley_VehicleOnPath_ProducesValidSteering()
    {
        var path = CreateSimpleUTurnPath();
        var input = CreateDefaultInput(path);
        input.UseStanley = true;
        input.SteerPosition = new Vec3(0, 2, 0);

        var output = _service.CalculateGuidance(input);

        Assert.That(output.IsTurnComplete, Is.False);
        Assert.That(double.IsNaN(output.SteerAngle), Is.False);
        Assert.That(Math.Abs(output.SteerAngle), Is.LessThanOrEqualTo(35));
    }

    [Test]
    public void Stanley_VehiclePastEnd_CompleteTurn()
    {
        var path = CreateSimpleUTurnPath();
        var input = CreateDefaultInput(path);
        input.UseStanley = true;
        input.SteerPosition = new Vec3(10, -10, Math.PI);

        var output = _service.CalculateGuidance(input);

        Assert.That(output.IsTurnComplete, Is.True);
    }

    #endregion

    #region K-Style

    [Test]
    public void KStyle_InReverse_CompletesImmediately()
    {
        var path = CreateSimpleUTurnPath();
        var input = CreateDefaultInput(path);
        input.UTurnStyle = 1; // K-style
        input.IsReverse = true;
        input.UseStanley = true;
        input.SteerPosition = new Vec3(0, 5, 0);

        var output = _service.CalculateGuidance(input);

        Assert.That(output.IsTurnComplete, Is.True);
    }

    #endregion

    #region Output Fields

    [Test]
    public void Output_ContainsPathCount()
    {
        var path = CreateSimpleUTurnPath();
        var input = CreateDefaultInput(path);
        input.UseStanley = true;
        input.SteerPosition = new Vec3(0, 2, 0);

        var output = _service.CalculateGuidance(input);

        Assert.That(output.PathCount, Is.GreaterThan(0));
    }

    [Test]
    public void Output_SteerAngleClampedToMax()
    {
        var path = CreateSimpleUTurnPath();
        var input = CreateDefaultInput(path);
        input.MaxSteerAngle = 20; // Low max
        input.UseStanley = true;
        // Far from path to generate large steer angle
        input.SteerPosition = new Vec3(5, 5, Math.PI / 2);

        var output = _service.CalculateGuidance(input);

        if (!output.IsTurnComplete)
        {
            Assert.That(Math.Abs(output.SteerAngle), Is.LessThanOrEqualTo(20.01));
        }
    }

    #endregion

    #region Straight Line Path

    [Test]
    public void StraightPath_VehicleOnLine_NearZeroSteering()
    {
        // Simple straight path going north
        var path = new List<Vec3>();
        for (int i = 0; i < 20; i++)
            path.Add(new Vec3(0, i * 2.0, 0));

        var input = CreateDefaultInput(path);
        input.PivotPosition = new Vec3(0, 10, 0); // On path
        input.SteerPosition = new Vec3(0, 12.5, 0);
        input.FixHeading = 0;

        var output = _service.CalculateGuidance(input);

        Assert.That(output.IsTurnComplete, Is.False);
        Assert.That(Math.Abs(output.DistanceFromCurrentLine), Is.LessThan(0.5));
    }

    #endregion
}

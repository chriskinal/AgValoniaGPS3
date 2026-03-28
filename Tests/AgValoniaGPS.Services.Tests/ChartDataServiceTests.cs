using System;
using AgValoniaGPS.Models;
using AgValoniaGPS.Services;
using AgValoniaGPS.Services.Interfaces;
using NSubstitute;

namespace AgValoniaGPS.Services.Tests;

[TestFixture]
public class ChartDataServiceTests
{
    private IAutoSteerService _mockAutoSteer = null!;
    private ChartDataService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _mockAutoSteer = Substitute.For<IAutoSteerService>();
        _service = new ChartDataService(_mockAutoSteer);
    }

    [TearDown]
    public void TearDown()
    {
        _service.Stop();
    }

    [Test]
    public void Start_SubscribesToStateUpdated()
    {
        _service.Start();

        Assert.That(_service.IsRunning, Is.True);
    }

    [Test]
    public void Stop_UnsubscribesFromStateUpdated()
    {
        _service.Start();
        _service.Stop();

        Assert.That(_service.IsRunning, Is.False);
    }

    [Test]
    public void Start_WhenAlreadyRunning_DoesNothing()
    {
        _service.Start();
        _service.Start(); // second call should be idempotent

        Assert.That(_service.IsRunning, Is.True);
    }

    [Test]
    public void AllSeriesAreInitialized()
    {
        Assert.That(_service.SetSteerAngle, Is.Not.Null);
        Assert.That(_service.ActualSteerAngle, Is.Not.Null);
        Assert.That(_service.PwmOutput, Is.Not.Null);
        Assert.That(_service.HeadingError, Is.Not.Null);
        Assert.That(_service.ImuHeading, Is.Not.Null);
        Assert.That(_service.GpsHeading, Is.Not.Null);
        Assert.That(_service.CrossTrackError, Is.Not.Null);
    }

    [Test]
    public void DefaultTimeWindow_Is20Seconds()
    {
        Assert.That(_service.TimeWindowSeconds, Is.EqualTo(20.0));
    }

    [Test]
    public void TimeWindowSeconds_CanBeChanged()
    {
        _service.TimeWindowSeconds = 30.0;
        Assert.That(_service.TimeWindowSeconds, Is.EqualTo(30.0));
    }

    [Test]
    public void StateUpdate_AddsDataToSteerSeries()
    {
        _mockAutoSteer.LastSteerData.Returns(new SteerModuleData(
            ActualSteerAngle: 5.5,
            ImuHeading: 90.0,
            ImuRoll: 0.0,
            WorkSwitchActive: false,
            SteerSwitchActive: true,
            RemoteButtonPressed: false,
            VwasFusionActive: false,
            PwmDisplay: 128));

        _service.Start();

        // Simulate a state update
        var snapshot = new VehicleStateSnapshot
        {
            SteerAngle = 10.0,
            CrossTrackError = 0.5,
            Heading = 180.0
        };

        _mockAutoSteer.StateUpdated += Raise.Event<EventHandler<VehicleStateSnapshot>>(_mockAutoSteer, snapshot);

        // Verify data was recorded
        Assert.That(_service.SetSteerAngle.Count, Is.EqualTo(1));
        Assert.That(_service.ActualSteerAngle.Count, Is.EqualTo(1));
        Assert.That(_service.PwmOutput.Count, Is.EqualTo(1));
        Assert.That(_service.CrossTrackError.Count, Is.EqualTo(1));

        var steerPoints = _service.SetSteerAngle.GetPoints();
        Assert.That(steerPoints[0].Value, Is.EqualTo(10.0));

        var actualPoints = _service.ActualSteerAngle.GetPoints();
        Assert.That(actualPoints[0].Value, Is.EqualTo(5.5));

        var pwmPoints = _service.PwmOutput.GetPoints();
        Assert.That(pwmPoints[0].Value, Is.EqualTo(128));
    }

    [Test]
    public void StateUpdate_AddsDataToHeadingSeries()
    {
        _mockAutoSteer.LastSteerData.Returns(new SteerModuleData(
            ActualSteerAngle: 0.0,
            ImuHeading: 45.0,
            ImuRoll: 0.0,
            WorkSwitchActive: false,
            SteerSwitchActive: false,
            RemoteButtonPressed: false,
            VwasFusionActive: false,
            PwmDisplay: 0));

        _service.Start();

        var snapshot = new VehicleStateSnapshot
        {
            SteerAngle = 3.0,
            CrossTrackError = 0.1,
            Heading = 270.0
        };

        _mockAutoSteer.StateUpdated += Raise.Event<EventHandler<VehicleStateSnapshot>>(_mockAutoSteer, snapshot);

        var gpsHeadingPoints = _service.GpsHeading.GetPoints();
        Assert.That(gpsHeadingPoints[0].Value, Is.EqualTo(270.0));

        var imuPoints = _service.ImuHeading.GetPoints();
        Assert.That(imuPoints[0].Value, Is.EqualTo(45.0));
    }

    [Test]
    public void StateUpdate_AddsDataToXTESeries()
    {
        _mockAutoSteer.LastSteerData.Returns(new SteerModuleData());

        _service.Start();

        var snapshot = new VehicleStateSnapshot
        {
            CrossTrackError = -1.5
        };

        _mockAutoSteer.StateUpdated += Raise.Event<EventHandler<VehicleStateSnapshot>>(_mockAutoSteer, snapshot);

        var xtePoints = _service.CrossTrackError.GetPoints();
        Assert.That(xtePoints[0].Value, Is.EqualTo(-1.5));
    }

    [Test]
    public void CurrentTime_AdvancesAfterStart()
    {
        _service.Start();

        // Should be positive after start
        Thread.Sleep(50);
        Assert.That(_service.CurrentTime, Is.GreaterThan(0));
    }

    [Test]
    public void CurrentTime_IsZeroBeforeStart()
    {
        Assert.That(_service.CurrentTime, Is.EqualTo(0));
    }
}

[TestFixture]
public class ChartSeriesTests
{
    [Test]
    public void AddPoint_IncreasesCount()
    {
        var series = new ChartSeries("Test", 0xFFFFFFFF);

        series.AddPoint(1.0, 10.0);
        series.AddPoint(2.0, 20.0);

        Assert.That(series.Count, Is.EqualTo(2));
    }

    [Test]
    public void GetPoints_ReturnsCopy()
    {
        var series = new ChartSeries("Test", 0xFFFFFFFF);
        series.AddPoint(1.0, 10.0);

        var points = series.GetPoints();
        points.Clear(); // modifying copy shouldn't affect original

        Assert.That(series.Count, Is.EqualTo(1));
    }

    [Test]
    public void TrimBefore_RemovesOldPoints()
    {
        var series = new ChartSeries("Test", 0xFFFFFFFF);
        series.AddPoint(1.0, 10.0);
        series.AddPoint(2.0, 20.0);
        series.AddPoint(3.0, 30.0);
        series.AddPoint(4.0, 40.0);

        series.TrimBefore(2.5);

        Assert.That(series.Count, Is.EqualTo(2));
        var points = series.GetPoints();
        Assert.That(points[0].Timestamp, Is.EqualTo(3.0));
        Assert.That(points[1].Timestamp, Is.EqualTo(4.0));
    }

    [Test]
    public void TrimBefore_WithNoOldPoints_DoesNothing()
    {
        var series = new ChartSeries("Test", 0xFFFFFFFF);
        series.AddPoint(5.0, 10.0);

        series.TrimBefore(1.0);

        Assert.That(series.Count, Is.EqualTo(1));
    }

    [Test]
    public void Clear_RemovesAllPoints()
    {
        var series = new ChartSeries("Test", 0xFFFFFFFF);
        series.AddPoint(1.0, 10.0);
        series.AddPoint(2.0, 20.0);

        series.Clear();

        Assert.That(series.Count, Is.EqualTo(0));
    }

    [Test]
    public void Name_ReturnsConstructorValue()
    {
        var series = new ChartSeries("MyChart", 0xFF00FF00);
        Assert.That(series.Name, Is.EqualTo("MyChart"));
    }

    [Test]
    public void Color_ReturnsConstructorValue()
    {
        var series = new ChartSeries("Test", 0xFF00FF00);
        Assert.That(series.Color, Is.EqualTo(0xFF00FF00));
    }

    [Test]
    public void GetPoints_PreservesOrder()
    {
        var series = new ChartSeries("Test", 0xFFFFFFFF);
        series.AddPoint(1.0, 100.0);
        series.AddPoint(2.0, 200.0);
        series.AddPoint(3.0, 300.0);

        var points = series.GetPoints();
        Assert.That(points[0].Value, Is.EqualTo(100.0));
        Assert.That(points[1].Value, Is.EqualTo(200.0));
        Assert.That(points[2].Value, Is.EqualTo(300.0));
    }
}

using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media.Imaging;
using AgValoniaGPS.Services.Interfaces;
using AgValoniaGPS.Views.Controls;
using AgValoniaGPS.Views.Controls.Panels;

namespace AgValoniaGPS.UI.Tests;

/// <summary>
/// Headless screenshot capture tests for chart panels.
/// Renders each chart panel in a headless window and captures
/// before/after screenshots with mock data.
/// </summary>
[TestFixture]
public class ChartScreenshotTests
{
    private string _screenshotDir = null!;

    [SetUp]
    public void SetUp()
    {
        _screenshotDir = Path.Combine(
            TestContext.CurrentContext.TestDirectory, "screenshots");
        Directory.CreateDirectory(_screenshotDir);
    }

    private static ChartDataServiceFake CreateFakeChartData()
    {
        return new ChartDataServiceFake();
    }

    private static void PopulateSteerData(ChartDataServiceFake chartData)
    {
        // Simulate 2 seconds of steer data at ~10Hz
        for (int i = 0; i < 20; i++)
        {
            double t = i * 0.1;
            double setAngle = 15.0 * Math.Sin(t * 0.5);
            double actualAngle = setAngle * 0.85 + (i % 3) * 0.5;
            double pwm = Math.Abs(setAngle) * 6.0;

            chartData.SetSteerAngle.AddPoint(t, setAngle);
            chartData.ActualSteerAngle.AddPoint(t, actualAngle);
            chartData.PwmOutput.AddPoint(t, pwm);
        }
        chartData.SetCurrentTime(2.0);
    }

    private static void PopulateHeadingData(ChartDataServiceFake chartData)
    {
        for (int i = 0; i < 20; i++)
        {
            double t = i * 0.1;
            double gpsHeading = 180.0 + 5.0 * Math.Sin(t * 0.3);
            double imuHeading = gpsHeading + 0.5;
            double headingError = 5.0 * Math.Sin(t * 0.3);

            chartData.GpsHeading.AddPoint(t, gpsHeading);
            chartData.ImuHeading.AddPoint(t, imuHeading);
            chartData.HeadingError.AddPoint(t, headingError);
        }
        chartData.SetCurrentTime(2.0);
    }

    private static void PopulateXTEData(ChartDataServiceFake chartData)
    {
        for (int i = 0; i < 20; i++)
        {
            double t = i * 0.1;
            double xte = 0.5 * Math.Sin(t * 0.8) + 0.1 * Math.Cos(t * 2.0);
            chartData.CrossTrackError.AddPoint(t, xte);
        }
        chartData.SetCurrentTime(2.0);
    }

    private static void CaptureScreenshot(Window window, string filePath)
    {
        var dir = Path.GetDirectoryName(filePath);
        if (dir != null) Directory.CreateDirectory(dir);

        // Use Avalonia Headless capture API for proper rendering
        var frame = window.CaptureRenderedFrame();
        if (frame != null)
        {
            frame.Save(filePath);
        }
        else
        {
            // Fallback: write an empty marker file so the test can differentiate
            File.WriteAllText(filePath, "");
        }
    }

    // ---- Steer Chart Screenshots ----

    [AvaloniaTest]
    public void SteerChart_EmptyBaseline_CapturesScreenshot()
    {
        var vm = new MainViewModelBuilder().Build();
        vm.IsSteerChartPanelVisible = true;

        var chartData = CreateFakeChartData();
        var panel = new SteerChartPanel { DataContext = vm };
        panel.ConfigureChart(chartData);

        var window = new Window { Content = panel, Width = 800, Height = 400 };
        window.Show();

        var path = Path.Combine(_screenshotDir, "steer_chart_empty.png");
        CaptureScreenshot(window, path);

        Assert.That(File.Exists(path), Is.True, "Steer chart empty screenshot was saved");
        Assert.That(new FileInfo(path).Length, Is.GreaterThan(0), "Screenshot file is not empty");
    }

    [AvaloniaTest]
    public void SteerChart_WithData_CapturesScreenshot()
    {
        var vm = new MainViewModelBuilder().Build();
        vm.IsSteerChartPanelVisible = true;

        var chartData = CreateFakeChartData();
        PopulateSteerData(chartData);

        var panel = new SteerChartPanel { DataContext = vm };
        panel.ConfigureChart(chartData);

        var window = new Window { Content = panel, Width = 800, Height = 400 };
        window.Show();

        var path = Path.Combine(_screenshotDir, "steer_chart.png");
        CaptureScreenshot(window, path);

        Assert.That(File.Exists(path), Is.True, "Steer chart data screenshot was saved");
        Assert.That(new FileInfo(path).Length, Is.GreaterThan(0), "Screenshot file is not empty");
    }

    // ---- Heading Chart Screenshots ----

    [AvaloniaTest]
    public void HeadingChart_EmptyBaseline_CapturesScreenshot()
    {
        var vm = new MainViewModelBuilder().Build();
        vm.IsHeadingChartPanelVisible = true;

        var chartData = CreateFakeChartData();
        var panel = new HeadingChartPanel { DataContext = vm };
        panel.ConfigureChart(chartData);

        var window = new Window { Content = panel, Width = 800, Height = 400 };
        window.Show();

        var path = Path.Combine(_screenshotDir, "heading_chart_empty.png");
        CaptureScreenshot(window, path);

        Assert.That(File.Exists(path), Is.True, "Heading chart empty screenshot was saved");
        Assert.That(new FileInfo(path).Length, Is.GreaterThan(0), "Screenshot file is not empty");
    }

    [AvaloniaTest]
    public void HeadingChart_WithData_CapturesScreenshot()
    {
        var vm = new MainViewModelBuilder().Build();
        vm.IsHeadingChartPanelVisible = true;

        var chartData = CreateFakeChartData();
        PopulateHeadingData(chartData);

        var panel = new HeadingChartPanel { DataContext = vm };
        panel.ConfigureChart(chartData);

        var window = new Window { Content = panel, Width = 800, Height = 400 };
        window.Show();

        var path = Path.Combine(_screenshotDir, "heading_chart.png");
        CaptureScreenshot(window, path);

        Assert.That(File.Exists(path), Is.True, "Heading chart data screenshot was saved");
        Assert.That(new FileInfo(path).Length, Is.GreaterThan(0), "Screenshot file is not empty");
    }

    // ---- XTE Chart Screenshots ----

    [AvaloniaTest]
    public void XTEChart_EmptyBaseline_CapturesScreenshot()
    {
        var vm = new MainViewModelBuilder().Build();
        vm.IsXTEChartPanelVisible = true;

        var chartData = CreateFakeChartData();
        var panel = new XTEChartPanel { DataContext = vm };
        panel.ConfigureChart(chartData);

        var window = new Window { Content = panel, Width = 800, Height = 400 };
        window.Show();

        var path = Path.Combine(_screenshotDir, "xte_chart_empty.png");
        CaptureScreenshot(window, path);

        Assert.That(File.Exists(path), Is.True, "XTE chart empty screenshot was saved");
        Assert.That(new FileInfo(path).Length, Is.GreaterThan(0), "Screenshot file is not empty");
    }

    [AvaloniaTest]
    public void XTEChart_WithData_CapturesScreenshot()
    {
        var vm = new MainViewModelBuilder().Build();
        vm.IsXTEChartPanelVisible = true;

        var chartData = CreateFakeChartData();
        PopulateXTEData(chartData);

        var panel = new XTEChartPanel { DataContext = vm };
        panel.ConfigureChart(chartData);

        var window = new Window { Content = panel, Width = 800, Height = 400 };
        window.Show();

        var path = Path.Combine(_screenshotDir, "xte_chart.png");
        CaptureScreenshot(window, path);

        Assert.That(File.Exists(path), Is.True, "XTE chart data screenshot was saved");
        Assert.That(new FileInfo(path).Length, Is.GreaterThan(0), "Screenshot file is not empty");
    }
}

/// <summary>
/// Minimal fake IChartDataService for screenshot tests.
/// Allows direct data population without needing AutoSteerService.
/// </summary>
internal class ChartDataServiceFake : IChartDataService
{
    private double _currentTime;

    public double TimeWindowSeconds { get; set; } = 20.0;
    public bool IsRunning => true;
    public double CurrentTime => _currentTime;

    public ChartSeries SetSteerAngle { get; } = new("Set Angle", 0xFFFFFF00);
    public ChartSeries ActualSteerAngle { get; } = new("Actual Angle", 0xFF00FF00);
    public ChartSeries PwmOutput { get; } = new("PWM", 0xFF00FFFF);
    public ChartSeries HeadingError { get; } = new("Heading Error", 0xFFFF4444);
    public ChartSeries ImuHeading { get; } = new("IMU Heading", 0xFFFF8800);
    public ChartSeries GpsHeading { get; } = new("GPS Heading", 0xFFFFFFFF);
    public ChartSeries CrossTrackError { get; } = new("XTE", 0xFFFF00FF);

    public void Start() { }
    public void Stop() { }

    public void SetCurrentTime(double time) => _currentTime = time;
}

using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using AgValoniaGPS.Services.Interfaces;
using AgValoniaGPS.ViewModels;
using AgValoniaGPS.Views.Controls;
using AgValoniaGPS.Views.Controls.Panels;

namespace AgValoniaGPS.UI.Tests;

/// <summary>
/// Headless screenshot capture tests for chart panels.
/// Renders each chart panel with empty and populated data,
/// following the shared capture pattern from ScreenshotCaptureTests.
/// Screenshots go to screenshots/charts/ subdirectory.
/// </summary>
[TestFixture]
public class ChartScreenshotTests
{
    private const int ChartWidth = 440;
    private const int ChartHeight = 260;

    private static string ScreenshotBaseDir
    {
        get
        {
            var dir = Path.Combine(
                TestContext.CurrentContext.WorkDirectory,
                "screenshots", "charts");
            Directory.CreateDirectory(dir);
            return dir;
        }
    }

    // ---------------------------------------------------------------
    // Capture helpers (consistent with ScreenshotCaptureTests pattern)
    // ---------------------------------------------------------------

    private static void CaptureScreenshot(Window window, string filePath)
    {
        window.UpdateLayout();

        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        var renderTarget = new RenderTargetBitmap(
            new PixelSize(ChartWidth, ChartHeight), new Vector(96, 96));
        renderTarget.Render(window);
        renderTarget.Save(filePath);
    }

    private static void AssertScreenshotExists(string path, string label)
    {
        Assert.That(File.Exists(path), Is.True, $"{label} screenshot not created: {path}");
        Assert.That(new FileInfo(path).Length, Is.GreaterThan(0), $"{label} screenshot is empty");
    }

    // ---------------------------------------------------------------
    // Window + panel builders
    // ---------------------------------------------------------------

    private static (Window window, TPanel panel) CreateChartWindow<TPanel>(
        Action<MainViewModel> setVisible) where TPanel : UserControl, new()
    {
        var vm = new MainViewModelBuilder().Build();
        setVisible(vm);

        var panel = new TPanel { DataContext = vm };
        var window = new Window
        {
            Content = panel,
            Width = ChartWidth,
            Height = ChartHeight,
            SizeToContent = SizeToContent.Manual
        };
        return (window, panel);
    }

    // ---------------------------------------------------------------
    // Mock data generators (25 seconds at 10Hz to fill 20s window)
    // ---------------------------------------------------------------

    private static ChartDataServiceFake CreateFakeChartData()
    {
        return new ChartDataServiceFake();
    }

    private static void PopulateSteerData(ChartDataServiceFake chartData)
    {
        for (int i = 0; i < 250; i++)
        {
            double t = i * 0.1;
            double setAngle = 18.0 * Math.Sin(t * 0.4) + 5.0 * Math.Sin(t * 1.2);
            double actualAngle = setAngle * 0.85 + 2.0 * Math.Sin(t * 0.4 - 0.3);
            double pwm = Math.Clamp(Math.Abs(setAngle) * 5.5, 0, 255);

            chartData.SetSteerAngle.AddPoint(t, setAngle);
            chartData.ActualSteerAngle.AddPoint(t, actualAngle);
            chartData.PwmOutput.AddPoint(t, pwm);
        }
        chartData.SetCurrentTime(25.0);
    }

    private static void PopulateHeadingData(ChartDataServiceFake chartData)
    {
        for (int i = 0; i < 250; i++)
        {
            double t = i * 0.1;
            double headingError = 8.0 * Math.Sin(t * 0.3) + 3.0 * Math.Cos(t * 0.7);
            double gpsHeading = 180.0 + 5.0 * Math.Sin(t * 0.15);
            double imuHeading = gpsHeading + 0.8 * Math.Sin(t * 0.5);

            chartData.HeadingError.AddPoint(t, headingError);
            chartData.GpsHeading.AddPoint(t, gpsHeading);
            chartData.ImuHeading.AddPoint(t, imuHeading);
        }
        chartData.SetCurrentTime(25.0);
    }

    private static void PopulateXTEData(ChartDataServiceFake chartData)
    {
        for (int i = 0; i < 250; i++)
        {
            double t = i * 0.1;
            double xte = 0.8 * Math.Sin(t * 0.5) + 0.3 * Math.Cos(t * 1.3)
                       + 0.15 * Math.Sin(t * 3.0);
            chartData.CrossTrackError.AddPoint(t, xte);
        }
        chartData.SetCurrentTime(25.0);
    }

    // ---------------------------------------------------------------
    // Chart capture helper: empty + populated for a chart type
    // ---------------------------------------------------------------

    private void CaptureChartToggle<TPanel>(
        string chartName,
        Action<MainViewModel> setVisible,
        Action<TPanel, ChartDataServiceFake> configureChart,
        Action<ChartDataServiceFake> populateData) where TPanel : UserControl, new()
    {
        var baseDir = ScreenshotBaseDir;

        // Empty baseline
        {
            var (window, panel) = CreateChartWindow<TPanel>(setVisible);
            var emptyData = CreateFakeChartData();
            configureChart(panel, emptyData);
            window.Show();

            var path = Path.Combine(baseDir, $"{chartName}_empty.png");
            CaptureScreenshot(window, path);
            window.Close();

            AssertScreenshotExists(path, $"{chartName}/empty");
            TestContext.Out.WriteLine($"[{chartName}] empty: {path}");
        }

        // With data
        {
            var (window, panel) = CreateChartWindow<TPanel>(setVisible);
            var chartData = CreateFakeChartData();
            populateData(chartData);
            configureChart(panel, chartData);
            window.Show();

            var path = Path.Combine(baseDir, $"{chartName}_data.png");
            CaptureScreenshot(window, path);
            window.Close();

            AssertScreenshotExists(path, $"{chartName}/data");
            TestContext.Out.WriteLine($"[{chartName}] data:  {path}");
        }
    }

    // ---------------------------------------------------------------
    // Tests
    // ---------------------------------------------------------------

    [AvaloniaTest]
    public void Capture_SteerChart()
    {
        CaptureChartToggle<SteerChartPanel>(
            "steer_chart",
            vm => vm.IsSteerChartPanelVisible = true,
            (panel, data) => panel.ConfigureChart(data),
            PopulateSteerData);
    }

    [AvaloniaTest]
    public void Capture_HeadingChart()
    {
        CaptureChartToggle<HeadingChartPanel>(
            "heading_chart",
            vm => vm.IsHeadingChartPanelVisible = true,
            (panel, data) => panel.ConfigureChart(data),
            PopulateHeadingData);
    }

    [AvaloniaTest]
    public void Capture_XTEChart()
    {
        CaptureChartToggle<XTEChartPanel>(
            "xte_chart",
            vm => vm.IsXTEChartPanelVisible = true,
            (panel, data) => panel.ConfigureChart(data),
            PopulateXTEData);
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

    public ChartSeries SetSteerAngle { get; } = new("Set Angle", 0xFFE05020);
    public ChartSeries ActualSteerAngle { get; } = new("Actual Angle", 0xFF2080E0);
    public ChartSeries PwmOutput { get; } = new("PWM", 0xFF00A080);
    public ChartSeries HeadingError { get; } = new("Heading Error", 0xFFDD3333);
    public ChartSeries ImuHeading { get; } = new("IMU Heading", 0xFFD07020);
    public ChartSeries GpsHeading { get; } = new("GPS Heading", 0xFF0088AA);
    public ChartSeries CrossTrackError { get; } = new("XTE", 0xFFC020C0);

    public void Start() { }
    public void Stop() { }

    public void SetCurrentTime(double time) => _currentTime = time;
}

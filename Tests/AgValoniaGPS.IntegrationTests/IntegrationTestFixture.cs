using System;
using System.IO;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using AgValoniaGPS.Desktop.DependencyInjection;
using AgValoniaGPS.Desktop.Views;
using AgValoniaGPS.Services.Interfaces;
using AgValoniaGPS.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using NUnit.Framework;

namespace AgValoniaGPS.IntegrationTests;

/// <summary>
/// Base fixture for integration tests that boot the full app with real DI,
/// real Skia rendering, and test-isolated data directory.
/// </summary>
public class IntegrationTestFixture
{
    private IHost? _host;
    private string _tempDataDir = string.Empty;

    protected IServiceProvider Services => _host!.Services;
    protected MainViewModel ViewModel => Services.GetRequiredService<MainViewModel>();
    protected MainWindow? MainWindow { get; private set; }
    protected string ScreenshotDir { get; private set; } = string.Empty;

    [SetUp]
    public void SetUp()
    {
        // Copy test data to a temp directory for isolation
        _tempDataDir = Path.Combine(Path.GetTempPath(), $"AgValoniaGPS_IntTest_{Guid.NewGuid():N}");
        var sourceData = Path.Combine(TestContext.CurrentContext.TestDirectory, "TestData");
        CopyDirectory(sourceData, _tempDataDir);

        ScreenshotDir = Path.Combine(TestContext.CurrentContext.WorkDirectory, "screenshots", "integration");
        Directory.CreateDirectory(ScreenshotDir);

        // Create TestSettingsService pointing at isolated temp data
        var testSettings = new TestSettingsService(_tempDataDir);

        // Build DI container, replacing real SettingsService with test version
        _host = Host.CreateDefaultBuilder()
            .ConfigureServices((context, services) =>
            {
                services.AddAgValoniaServices();
                // Replace SettingsService with test-isolated version
                services.Replace(ServiceDescriptor.Singleton<ISettingsService>(testSettings));
            })
            .Build();

        // Set App.Services so MainWindow.OnOpened can resolve services
        AgValoniaGPS.Desktop.App.Services = Services;

        // Wire cross-references (AutoSteer -> UDP)
        Services.WireUpServices();

        // Load test settings and ensure no first-run dialogs
        var settingsService = Services.GetRequiredService<ISettingsService>();
        settingsService.Load();
        settingsService.Settings.IsFirstRun = false;

        // Sync to ConfigurationStore
        var configService = Services.GetRequiredService<IConfigurationService>();
        configService.LoadAppSettings();
    }

    [TearDown]
    public void TearDown()
    {
        MainWindow?.Close();
        _host?.Dispose();
        AgValoniaGPS.Desktop.App.Services = null;

        // Clean up temp directory
        try
        {
            if (Directory.Exists(_tempDataDir))
                Directory.Delete(_tempDataDir, recursive: true);
        }
        catch
        {
            // Best effort cleanup
        }
    }

    /// <summary>
    /// Show the MainWindow at fixed size and close any startup dialogs.
    /// </summary>
    protected void ShowMainWindow(int width = 1280, int height = 960)
    {
        try
        {
            MainWindow = new MainWindow
            {
                Width = width,
                Height = height,
                WindowStartupLocation = WindowStartupLocation.Manual,
                Position = new PixelPoint(50, 50)
            };
            MainWindow.Show();
            PumpUI(5);

            // Close any dialogs that appeared on startup
            ViewModel.State.UI.CloseDialog();
            PumpUI(3);
        }
        catch (Exception ex)
        {
            TestContext.Out.WriteLine($"ShowMainWindow failed: {ex.GetType().Name}: {ex.Message}");
            TestContext.Out.WriteLine(ex.StackTrace);
            throw;
        }
    }

    /// <summary>
    /// Execute a command and pump the UI thread.
    /// </summary>
    protected void ExecuteCommand(ICommand? command, object? parameter = null)
    {
        if (command == null) return;
        if (command.CanExecute(parameter))
        {
            command.Execute(parameter);
        }
        PumpUI();
    }

    /// <summary>
    /// Pump the UI dispatcher to process pending jobs.
    /// </summary>
    protected void PumpUI(int iterations = 3)
    {
        for (int i = 0; i < iterations; i++)
        {
            Dispatcher.UIThread.RunJobs();
        }
    }

    /// <summary>
    /// Simulate GPS ticks by calling the simulator service.
    /// No Thread.Sleep -- [AvaloniaTest] runs on UI thread so sleep would deadlock.
    /// </summary>
    protected void SimulateTicks(int count, double steerAngle = 0)
    {
        var simService = Services.GetRequiredService<IGpsSimulationService>();
        for (int i = 0; i < count; i++)
        {
            simService.Tick(steerAngle);
            PumpUI(1);
        }
    }

    /// <summary>
    /// Capture a screenshot of the MainWindow via RenderTargetBitmap.
    /// </summary>
    protected string CaptureScreenshot(string name)
    {
        if (MainWindow == null)
            throw new InvalidOperationException("MainWindow not shown yet");

        MainWindow.UpdateLayout();

        var width = (int)MainWindow.Width;
        var height = (int)MainWindow.Height;
        var renderTarget = new RenderTargetBitmap(
            new PixelSize(width, height), new Vector(96, 96));
        renderTarget.Render(MainWindow);

        var filePath = Path.Combine(ScreenshotDir, $"{name}.png");
        renderTarget.Save(filePath);

        Assert.That(File.Exists(filePath), Is.True, $"Screenshot not created: {filePath}");
        Assert.That(new FileInfo(filePath).Length, Is.GreaterThan(0), $"Screenshot is empty: {filePath}");
        TestContext.Out.WriteLine($"Screenshot: {filePath} ({new FileInfo(filePath).Length} bytes)");

        return filePath;
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);

        foreach (var file in Directory.GetFiles(source))
        {
            File.Copy(file, Path.Combine(destination, Path.GetFileName(file)));
        }

        foreach (var dir in Directory.GetDirectories(source))
        {
            CopyDirectory(dir, Path.Combine(destination, Path.GetFileName(dir)));
        }
    }
}

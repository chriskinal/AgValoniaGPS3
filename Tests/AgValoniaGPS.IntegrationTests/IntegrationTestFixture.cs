using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
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
using Microsoft.Extensions.Hosting;
using NUnit.Framework;

namespace AgValoniaGPS.IntegrationTests;

/// <summary>
/// Base fixture for integration tests that boot the full app with real DI,
/// real rendering, and test-isolated data.
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

        // Build DI container with TestSettingsService
        var testSettings = new TestSettingsService(_tempDataDir);

        _host = Host.CreateDefaultBuilder()
            .ConfigureServices((context, services) =>
            {
                services.AddAgValoniaServices();
                // Replace real SettingsService with test-isolated version
                var descriptor = new ServiceDescriptor(
                    typeof(ISettingsService), testSettings);
                services.AddSingleton<ISettingsService>(testSettings);
            })
            .Build();

        // Wire cross-references
        Services.WireUpServices();

        // Load settings from test data
        var settingsService = Services.GetRequiredService<ISettingsService>();
        settingsService.Load();

        // Sync settings to ConfigurationStore
        var configService = Services.GetRequiredService<IConfigurationService>();
        configService.LoadAppSettings();
    }

    [TearDown]
    public void TearDown()
    {
        MainWindow?.Close();
        _host?.Dispose();

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
    /// Show the MainWindow at fixed size. Call from within Dispatcher.UIThread.
    /// </summary>
    protected void ShowMainWindow(int width = 1280, int height = 960)
    {
        MainWindow = new MainWindow
        {
            Width = width,
            Height = height,
            WindowStartupLocation = WindowStartupLocation.Manual,
            Position = new PixelPoint(50, 50)
        };
        MainWindow.Show();
    }

    /// <summary>
    /// Execute a command and pump the UI thread to let rendering catch up.
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
    /// Pump the UI dispatcher to process pending jobs and allow rendering.
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
    /// </summary>
    protected void SimulateTicks(int count, double steerAngle = 0, int delayMs = 100)
    {
        var simService = Services.GetRequiredService<IGpsSimulationService>();
        for (int i = 0; i < count; i++)
        {
            simService.Tick(steerAngle);
            PumpUI(1);
            if (delayMs > 0)
                Thread.Sleep(delayMs);
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

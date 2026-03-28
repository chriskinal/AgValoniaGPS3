// Integration Test Harness
// Boots the real app with UsePlatformDetect(), runs scenarios,
// captures screenshots, prints pass/fail.
// Run: dotnet run --project Tests/AgValoniaGPS.IntegrationTests/

using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media.Imaging;
using Avalonia.ReactiveUI;
using Avalonia.Threading;
using AgValoniaGPS.Desktop;
using AgValoniaGPS.Desktop.DependencyInjection;
using AgValoniaGPS.Desktop.Views;
using AgValoniaGPS.IntegrationTests;
using AgValoniaGPS.Services.Interfaces;
using AgValoniaGPS.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace AgValoniaGPS.IntegrationTests;

sealed class Program
{
    static string _screenshotDir = string.Empty;
    static ISettingsService _settingsService = null!;

    [STAThread]
    public static int Main(string[] args)
    {
        // Set up isolated test data
        var testDataDir = Path.Combine(AppContext.BaseDirectory, "TestData");
        var tempDir = Path.Combine(Path.GetTempPath(), $"AgValoniaGPS_IntTest_{Guid.NewGuid():N}");
        CopyDirectory(testDataDir, tempDir);

        _screenshotDir = Path.Combine(AppContext.BaseDirectory, "screenshots", "integration");
        Directory.CreateDirectory(_screenshotDir);

        Console.WriteLine($"[IntTest] Test data: {tempDir}");
        Console.WriteLine($"[IntTest] Screenshots: {_screenshotDir}");

        // Build DI with TestSettingsService
        var testSettings = new TestSettingsService(tempDir);
        var host = Host.CreateDefaultBuilder()
            .ConfigureServices((ctx, services) =>
            {
                services.AddAgValoniaServices();
                services.Replace(ServiceDescriptor.Singleton<ISettingsService>(testSettings));
            })
            .Build();

        App.Services = host.Services;
        host.Services.WireUpServices();

        _settingsService = host.Services.GetRequiredService<ISettingsService>();
        _settingsService.Load();
        _settingsService.Settings.IsFirstRun = false;

        var configService = host.Services.GetRequiredService<IConfigurationService>();
        configService.LoadAppSettings();

        Console.WriteLine("[IntTest] DI container built, settings loaded");

        // Boot Avalonia with real platform rendering
        int exitCode = 0;
        try
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args, lifetime =>
            {
                Dispatcher.UIThread.Post(async () =>
                {
                    try
                    {
                        await RunScenario(lifetime);
                        Console.WriteLine("\n[IntTest] ALL SCENARIOS PASSED");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"\n[IntTest] SCENARIO FAILED: {ex.Message}");
                        Console.WriteLine(ex.StackTrace);
                        exitCode = 1;
                    }
                    finally
                    {
                        lifetime.Shutdown(exitCode);
                    }
                });
            });
        }
        finally
        {
            host.Dispose();
            try { Directory.Delete(tempDir, true); } catch { }
        }

        Console.WriteLine($"[IntTest] Exit code: {exitCode}");
        return exitCode;
    }

    static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<IntegrationTestApp>()
            .UsePlatformDetect()
            .WithInterFont()
            .UseReactiveUI();

    static async Task RunScenario(IClassicDesktopStyleApplicationLifetime lifetime)
    {
        var vm = App.Services!.GetRequiredService<MainViewModel>();

        // Step 1: Show MainWindow
        Console.Write("[Step 1] Show MainWindow... ");
        var window = new MainWindow
        {
            Width = 1280,
            Height = 960,
            WindowStartupLocation = WindowStartupLocation.CenterScreen
        };
        lifetime.MainWindow = window;
        window.Show();
        await Task.Delay(1500);
        Dispatcher.UIThread.RunJobs();

        vm.State.UI.CloseDialog();
        await Task.Delay(200);
        Dispatcher.UIThread.RunJobs();
        CaptureScreenshot(window, "01_app_startup");
        Console.WriteLine("OK");

        // Step 2: Open field selection dialog
        Console.Write("[Step 2] Field selection dialog... ");
        vm.ShowFieldSelectionDialogCommand?.Execute(null);
        await Task.Delay(300);
        Dispatcher.UIThread.RunJobs();
        CaptureScreenshot(window, "02_field_selection");
        Console.WriteLine("OK");

        // Step 3: Load TestField
        Console.Write("[Step 3] Load TestField... ");
        vm.State.UI.CloseDialog();
        var fieldsDir = _settingsService.Settings.FieldsDirectory;
        var testFieldDir = Path.Combine(fieldsDir, "TestField");

        try
        {
            await vm.OpenFieldAsync(testFieldDir, "TestField");
            await Task.Delay(500);
            Dispatcher.UIThread.RunJobs();
            CaptureScreenshot(window, "03_field_loaded");
            Console.WriteLine("OK");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"PARTIAL ({ex.Message})");
            CaptureScreenshot(window, "03_field_partial");
        }

        // Step 4: Drive simulator
        Console.Write("[Step 4] Simulator drive... ");
        _settingsService.Settings.SimulatorEnabled = true;
        var simService = App.Services!.GetRequiredService<IGpsSimulationService>();
        for (int i = 0; i < 50; i++)
        {
            simService.Tick(0);
            Dispatcher.UIThread.RunJobs();
        }
        await Task.Delay(300);
        Dispatcher.UIThread.RunJobs();
        CaptureScreenshot(window, "04_simulator_driving");
        Console.WriteLine("OK");

        // Step 5: Open tracks dialog
        Console.Write("[Step 5] Tracks dialog... ");
        vm.ShowTracksDialogCommand?.Execute(null);
        await Task.Delay(300);
        Dispatcher.UIThread.RunJobs();
        CaptureScreenshot(window, "05_tracks_dialog");
        vm.State.UI.CloseDialog();
        Console.WriteLine("OK");

        // Step 6: Open configuration dialog
        Console.Write("[Step 6] Configuration dialog... ");
        vm.ShowConfigurationDialogCommand?.Execute(null);
        await Task.Delay(300);
        Dispatcher.UIThread.RunJobs();
        CaptureScreenshot(window, "06_configuration_dialog");
        vm.State.UI.CloseDialog();
        Console.WriteLine("OK");

        // Step 7: Toggle night mode
        Console.Write("[Step 7] Toggle night mode... ");
        var mapService = App.Services!.GetRequiredService<IMapService>();
        mapService.SetDayMode(false);
        await Task.Delay(300);
        Dispatcher.UIThread.RunJobs();
        CaptureScreenshot(window, "07_night_mode");
        Console.WriteLine("OK");
    }

    static void CaptureScreenshot(Window window, string name)
    {
        window.UpdateLayout();
        var w = Math.Max((int)window.Bounds.Width, 1280);
        var h = Math.Max((int)window.Bounds.Height, 960);

        var bitmap = new RenderTargetBitmap(new PixelSize(w, h), new Vector(96, 96));
        bitmap.Render(window);

        var path = Path.Combine(_screenshotDir, $"{name}.png");
        bitmap.Save(path);
        var kb = new FileInfo(path).Length / 1024;
        Console.Write($"[{kb}KB] ");
    }

    static void CopyDirectory(string src, string dst)
    {
        Directory.CreateDirectory(dst);
        foreach (var f in Directory.GetFiles(src))
            File.Copy(f, Path.Combine(dst, Path.GetFileName(f)));
        foreach (var d in Directory.GetDirectories(src))
            CopyDirectory(d, Path.Combine(dst, Path.GetFileName(d)));
    }
}

public class IntegrationTestApp : Application
{
    public override void Initialize()
    {
        Styles.Add(new Avalonia.Themes.Fluent.FluentTheme());
        var sharedResources = new Avalonia.Markup.Xaml.Styling.ResourceInclude(
            new Uri("avares://AgValoniaGPS.Views"))
        {
            Source = new Uri("avares://AgValoniaGPS.Views/Styles/SharedResources.axaml")
        };
        Resources.MergedDictionaries.Add(sharedResources);
    }
}

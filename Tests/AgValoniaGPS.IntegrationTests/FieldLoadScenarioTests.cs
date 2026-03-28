using System.IO;
using Avalonia.Threading;
using AgValoniaGPS.Models.State;
using AgValoniaGPS.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace AgValoniaGPS.IntegrationTests;

/// <summary>
/// End-to-end scenario: boot app with full DI, show real MainWindow,
/// exercise dialogs, capture screenshots at each step.
/// </summary>
[TestFixture]
public class FieldLoadScenarioTests : IntegrationTestFixture
{
    [AvaloniaTest]
    public void FieldLoad_SimulatorDrive_CapturesRealisticScreenshots()
    {
        // 1. App starts -- screenshot of main view (no field loaded yet)
        ShowMainWindow(1280, 960);
        CaptureScreenshot("01_app_startup");

        // 2. Open field selection dialog
        ExecuteCommand(ViewModel.ShowFieldSelectionDialogCommand);
        CaptureScreenshot("02_field_selection_dialog");

        // 3. Close field selection and try loading TestField
        ViewModel.State.UI.CloseDialog();
        PumpUI(3);

        var settingsService = Services.GetRequiredService<ISettingsService>();
        var fieldsDir = settingsService.Settings.FieldsDirectory;
        var testFieldDir = Path.Combine(fieldsDir, "TestField");

        if (Directory.Exists(testFieldDir))
        {
            try
            {
                // Load field via ViewModel (wires up boundary, tracks, map)
                ViewModel.OpenFieldAsync(testFieldDir, "TestField").GetAwaiter().GetResult();
                PumpUI(10);
                CaptureScreenshot("03_field_loaded");
            }
            catch (System.Exception ex)
            {
                // MapService.GetMapControl() throws if no control registered --
                // expected in headless mode where MainWindow doesn't fully initialize
                TestContext.Out.WriteLine($"Field load partial: {ex.Message}");
                CaptureScreenshot("03_field_partial_load");
            }
        }

        // 4. Drive simulator (no sleep -- [AvaloniaTest] runs on UI thread)
        settingsService.Settings.SimulatorEnabled = true;
        SimulateTicks(20);
        PumpUI(5);
        CaptureScreenshot("04_after_simulator");

        // 5. Open tracks dialog
        ExecuteCommand(ViewModel.ShowTracksDialogCommand);
        CaptureScreenshot("05_tracks_dialog");
        ViewModel.State.UI.CloseDialog();

        // 6. Open configuration dialog
        ExecuteCommand(ViewModel.ShowConfigurationDialogCommand);
        CaptureScreenshot("06_configuration_dialog");
        ViewModel.State.UI.CloseDialog();

        // Verify screenshots exist
        Assert.That(File.Exists(Path.Combine(ScreenshotDir, "01_app_startup.png")), Is.True);
        Assert.That(File.Exists(Path.Combine(ScreenshotDir, "06_configuration_dialog.png")), Is.True);
        TestContext.Out.WriteLine($"All screenshots saved to: {ScreenshotDir}");
    }
}

using System.IO;
using Avalonia.Controls;
using Avalonia.Threading;
using AgValoniaGPS.Models.State;
using AgValoniaGPS.Services.Interfaces;
using AgValoniaGPS.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace AgValoniaGPS.IntegrationTests;

/// <summary>
/// End-to-end scenario: boot app, load field, drive simulator, open dialogs,
/// capture screenshots at each step.
/// </summary>
[TestFixture]
public class FieldLoadScenarioTests : IntegrationTestFixture
{
    [AvaloniaTest]
    public void FieldLoad_SimulatorDrive_CapturesRealisticScreenshots()
    {
        // 1. App starts -- screenshot of empty main view
        ShowMainWindow(1280, 960);
        PumpUI(5);
        CaptureScreenshot("01_app_startup");

        // 2. Open field selection dialog
        ExecuteCommand(ViewModel.ShowFieldSelectionDialogCommand);
        PumpUI(5);
        CaptureScreenshot("02_field_selection_dialog");

        // 3. Close field selection and load TestField via ViewModel
        ExecuteCommand(ViewModel.CancelFieldSelectionDialogCommand);

        var settingsService = Services.GetRequiredService<ISettingsService>();
        var fieldsDir = settingsService.Settings.FieldsDirectory;
        var testFieldDir = Path.Combine(fieldsDir, "TestField");

        if (Directory.Exists(testFieldDir))
        {
            try
            {
                // Use the ViewModel's field loading to wire everything up properly
                ViewModel.OpenFieldAsync(testFieldDir, "TestField").GetAwaiter().GetResult();
                PumpUI(10);
                CaptureScreenshot("03_field_loaded");
            }
            catch (System.Exception ex)
            {
                TestContext.Out.WriteLine($"Field load failed: {ex.Message}");
                CaptureScreenshot("03_field_load_error");
            }
        }
        else
        {
            TestContext.Out.WriteLine($"TestField not found at: {testFieldDir}");
            CaptureScreenshot("03_field_not_found");
        }

        // 4. Start simulator and drive forward
        settingsService.Settings.SimulatorEnabled = true;
        SimulateTicks(50, steerAngle: 0, delayMs: 20);
        PumpUI(5);
        CaptureScreenshot("04_simulator_driving");

        // 5. Open tracks dialog
        ExecuteCommand(ViewModel.ShowTracksDialogCommand);
        PumpUI(5);
        CaptureScreenshot("05_tracks_dialog");
        ExecuteCommand(ViewModel.CloseTracksDialogCommand);

        // 6. Open configuration dialog
        ExecuteCommand(ViewModel.ShowConfigurationDialogCommand);
        PumpUI(5);
        CaptureScreenshot("06_configuration_dialog");

        // Verify we made it through the full scenario
        Assert.That(File.Exists(Path.Combine(ScreenshotDir, "01_app_startup.png")), Is.True);
        Assert.That(File.Exists(Path.Combine(ScreenshotDir, "06_configuration_dialog.png")), Is.True);

        TestContext.Out.WriteLine($"All screenshots saved to: {ScreenshotDir}");
    }
}

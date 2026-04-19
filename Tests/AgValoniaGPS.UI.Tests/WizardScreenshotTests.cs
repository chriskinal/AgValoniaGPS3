// AgValoniaGPS
// Copyright (C) 2024-2025 AgValoniaGPS Contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program. If not, see <https://www.gnu.org/licenses/>.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;

using AgValoniaGPS.Models.Configuration;
using AgValoniaGPS.Services.Interfaces;
using AgValoniaGPS.ViewModels.Wizards;
using AgValoniaGPS.ViewModels.Wizards.SteerWizard;
using AgValoniaGPS.Views.Controls.Wizards;

using CommunityToolkit.Mvvm.Input;

using NSubstitute;

namespace AgValoniaGPS.UI.Tests;

/// <summary>
/// Captures screenshots for each step of the Steer Configuration Wizard.
/// Each test navigates to a specific step and renders a screenshot to the
/// output directory for visual verification.
/// </summary>
[TestFixture]
public class WizardScreenshotTests
{
    private const int WizardWidth = 600;
    private const int WizardHeight = 700;

    private IConfigurationService _configService = null!;
    private ConfigurationStore _store = null!;

    private static string ScreenshotDir
    {
        get
        {
            var dir = Path.Combine(
                TestContext.CurrentContext.WorkDirectory,
                "screenshots", "wizard-steps");
            Directory.CreateDirectory(dir);
            return dir;
        }
    }

    [SetUp]
    public void SetUp()
    {
        _store = new ConfigurationStore();
        ConfigurationStore.SetInstance(_store);

        _configService = Substitute.For<IConfigurationService>();
        _configService.Store.Returns(_store);

        // Set valid defaults so all steps can be navigated
        _store.Vehicle.Wheelbase = 2.5;
        _store.Vehicle.TrackWidth = 1.8;
        _store.Vehicle.AntennaPivot = 1.0;
        _store.Vehicle.AntennaHeight = 2.0;
        _store.Vehicle.AntennaOffset = 0.0;
    }

    private SteerWizardViewModel CreateWizard()
    {
        return new SteerWizardViewModel(_configService);
    }

    private static async Task ExecuteNextAsync(WizardViewModel wizard)
    {
        await ((IAsyncRelayCommand)wizard.NextCommand).ExecuteAsync(null);
    }

    private static void CaptureWizardScreenshot(Window window, string filePath)
    {
        window.UpdateLayout();

        var renderTarget = new RenderTargetBitmap(
            new PixelSize(WizardWidth, WizardHeight), new Vector(96, 96));
        renderTarget.Render(window);

        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        renderTarget.Save(filePath);
    }

    private static void AssertScreenshotExists(string path, string label)
    {
        Assert.That(File.Exists(path), Is.True, $"{label} screenshot not created: {path}");
        Assert.That(new FileInfo(path).Length, Is.GreaterThan(0), $"{label} screenshot is empty");
    }

    /// <summary>
    /// Captures a screenshot for a specific wizard step by navigating to it via GoToStep.
    /// Uses the WizardHost to render the actual step view.
    /// </summary>
    /// <summary>
    /// Creates the correct view for a step ViewModel, matching WizardHost.UpdateStepView logic.
    /// </summary>
    private static Control CreateStepView(WizardStepViewModel step)
    {
        Control? view = step switch
        {
            WelcomeStepViewModel => new Views.Controls.Wizards.SteerWizard.WelcomeStepView(),
            VehicleTypeStepViewModel => new Views.Controls.Wizards.SteerWizard.VehicleTypeStepView(),
            FinishStepViewModel => new Views.Controls.Wizards.SteerWizard.FinishStepView(),
            _ => new Views.Controls.Wizards.SteerWizard.NumericStepView(),
        };
        view.DataContext = step;
        return view;
    }

    private void CaptureStepScreenshot(int stepIndex, string stepName)
    {
        var wizard = CreateWizard();
        wizard.GoToStep(stepIndex);

        // Render step view directly (WizardHost.FindControl doesn't work in headless)
        var stepView = CreateStepView(wizard.CurrentStep!);

        // Build a layout matching the wizard structure
        var header = new TextBlock
        {
            Text = $"{wizard.WizardTitle}  -  Step {stepIndex + 1} of {wizard.TotalSteps}: {wizard.CurrentStep!.Title}",
            FontSize = 16, FontWeight = Avalonia.Media.FontWeight.Bold,
            Margin = new Thickness(16, 12)
        };
        var content = new Border
        {
            Padding = new Thickness(20),
            Child = stepView
        };
        var layout = new DockPanel();
        DockPanel.SetDock(header, Dock.Top);
        layout.Children.Add(header);
        layout.Children.Add(content);

        var window = new Window
        {
            Content = layout,
            Width = WizardWidth,
            Height = WizardHeight,
            SizeToContent = SizeToContent.Manual
        };

        window.Show();
        window.UpdateLayout();

        var filePath = Path.Combine(ScreenshotDir, $"{stepIndex:D2}_{stepName}.png");
        CaptureWizardScreenshot(window, filePath);
        window.Close();

        AssertScreenshotExists(filePath, stepName);
        TestContext.Out.WriteLine($"[Step {stepIndex}] {stepName}: {filePath}");
    }

    // ---------------------------------------------------------------
    // Individual step screenshot tests
    // ---------------------------------------------------------------

    [AvaloniaTest]
    public void Capture_Step00_Welcome()
        => CaptureStepScreenshot(0, "Welcome");

    [AvaloniaTest]
    public void Capture_Step01_VehicleType()
        => CaptureStepScreenshot(1, "VehicleType");

    [AvaloniaTest]
    public void Capture_Step02_Wheelbase()
        => CaptureStepScreenshot(2, "Wheelbase");

    [AvaloniaTest]
    public void Capture_Step03_TrackWidth()
        => CaptureStepScreenshot(3, "TrackWidth");

    [AvaloniaTest]
    public void Capture_Step04_AntennaPivot()
        => CaptureStepScreenshot(4, "AntennaPivot");

    [AvaloniaTest]
    public void Capture_Step05_AntennaHeight()
        => CaptureStepScreenshot(5, "AntennaHeight");

    [AvaloniaTest]
    public void Capture_Step06_AntennaOffset()
        => CaptureStepScreenshot(6, "AntennaOffset");

    [AvaloniaTest]
    public void Capture_Step07_SteerEnable()
        => CaptureStepScreenshot(7, "SteerEnable");

    [AvaloniaTest]
    public void Capture_Step08_MotorDriver()
        => CaptureStepScreenshot(8, "MotorDriver");

    [AvaloniaTest]
    public void Capture_Step09_ADConverter()
        => CaptureStepScreenshot(9, "ADConverter");

    [AvaloniaTest]
    public void Capture_Step10_InvertSettings()
        => CaptureStepScreenshot(10, "InvertSettings");

    [AvaloniaTest]
    public void Capture_Step11_Danfoss()
        => CaptureStepScreenshot(11, "Danfoss");

    [AvaloniaTest]
    public void Capture_Step12_WasCalibration()
        => CaptureStepScreenshot(12, "WasCalibration");

    [AvaloniaTest]
    public void Capture_Step13_SteeringGains()
        => CaptureStepScreenshot(13, "SteeringGains");

    [AvaloniaTest]
    public void Capture_Step14_PwmCalibration()
        => CaptureStepScreenshot(14, "PwmCalibration");

    [AvaloniaTest]
    public void Capture_Step15_AlgorithmSelection()
        => CaptureStepScreenshot(15, "AlgorithmSelection");

    [AvaloniaTest]
    public void Capture_Step16_SpeedLimits()
        => CaptureStepScreenshot(16, "SpeedLimits");

    [AvaloniaTest]
    public void Capture_Step17_Sensors()
        => CaptureStepScreenshot(17, "Sensors");

    [AvaloniaTest]
    public void Capture_Step18_Finish()
        => CaptureStepScreenshot(18, "Finish");

    // ---------------------------------------------------------------
    // Aggregate test: verifies all 19 steps are navigable
    // ---------------------------------------------------------------

    [AvaloniaTest]
    public void AllSteps_AreNavigable_WithNonEmptyTitleAndDescription()
    {
        var wizard = CreateWizard();
        Assert.That(wizard.Steps.Count, Is.EqualTo(wizard.Steps.Count), "Wizard should have 19 steps");

        for (var i = 0; i < wizard.Steps.Count; i++)
        {
            wizard.GoToStep(i);

            var step = wizard.CurrentStep;
            Assert.That(step, Is.Not.Null, $"Step {i} should not be null");
            Assert.That(step!.Title, Is.Not.Null.And.Not.Empty,
                $"Step {i} Title should not be empty");
            Assert.That(step.Description, Is.Not.Null.And.Not.Empty,
                $"Step {i} Description should not be empty");

            TestContext.Out.WriteLine($"Step {i}: {step.Title}");
        }
    }

    // ---------------------------------------------------------------
    // Full navigation screenshot capture
    // ---------------------------------------------------------------

    [AvaloniaTest]
    public void CaptureAllSteps_FullNavigation()
    {
        var wizard = CreateWizard();
        Assert.That(wizard.Steps.Count, Is.EqualTo(wizard.Steps.Count));

        for (var i = 0; i < wizard.Steps.Count; i++)
        {
            wizard.GoToStep(i);
            var step = wizard.CurrentStep!;

            // Sanitize step title for filename
            var safeName = step.Title
                .Replace(" ", "_")
                .Replace("/", "_")
                .Replace("\\", "_");

            var wizardHost = new WizardHost
            {
                DataContext = wizard,
                Width = WizardWidth,
                Height = WizardHeight
            };

            var window = new Window
            {
                Content = wizardHost,
                Width = WizardWidth,
                Height = WizardHeight,
                SizeToContent = SizeToContent.Manual
            };

            window.Show();
            window.UpdateLayout();

            var filePath = Path.Combine(ScreenshotDir, $"{i:D2}_{safeName}.png");
            CaptureWizardScreenshot(window, filePath);
            window.Close();

            AssertScreenshotExists(filePath, step.Title);
            TestContext.Out.WriteLine($"[{i:D2}] {step.Title}: {filePath}");
        }
    }
}

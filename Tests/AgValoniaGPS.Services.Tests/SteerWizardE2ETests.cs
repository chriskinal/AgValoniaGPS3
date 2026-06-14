using AgValoniaGPS.Models.Configuration;
using AgValoniaGPS.Services.Interfaces;
using AgValoniaGPS.ViewModels.Wizards;
using AgValoniaGPS.ViewModels.Wizards.SteerWizard;
using CommunityToolkit.Mvvm.Input;
using NSubstitute;

namespace AgValoniaGPS.Services.Tests;

/// <summary>
/// End-to-end tests for the SteerWizardViewModel.
/// These tests exercise the full wizard navigation lifecycle
/// including step transitions, validation, events, and config persistence.
/// </summary>
[TestFixture]
[NonParallelizable]
public class SteerWizardE2ETests
{
    private IConfigurationService _configService = null!;
    private ConfigurationStore _store = null!;

    [SetUp]
    public void SetUp()
    {
        _store = new ConfigurationStore();
        ConfigurationStore.SetInstance(_store);

        _configService = Substitute.For<IConfigurationService>();
        _configService.Store.Returns(_store);
    }

    private SteerWizardViewModel CreateWizard()
    {
        return new SteerWizardViewModel(_configService, new AgValoniaGPS.Services.Threading.InlineUiDispatcher());
    }

    /// <summary>
    /// Execute the NextCommand (which is an AsyncRelayCommand).
    /// </summary>
    private static async Task ExecuteNextAsync(WizardViewModel wizard)
    {
        await ((IAsyncRelayCommand)wizard.NextCommand).ExecuteAsync(null);
    }

    /// <summary>
    /// Execute the FinishCommand (which is an AsyncRelayCommand).
    /// </summary>
    private static async Task ExecuteFinishAsync(WizardViewModel wizard)
    {
        await ((IAsyncRelayCommand)wizard.FinishCommand).ExecuteAsync(null);
    }

    // =========================================================================
    // 1. Full wizard completion flow
    // =========================================================================

    [Test]
    public async Task FullWizardCompletion_NavigatesAllSteps_AndFiresCompleted()
    {
        var wizard = CreateWizard();
        Assert.That(wizard.Steps.Count, Is.EqualTo(14), "Wizard should have 14 steps");

        // Set valid values for steps that require validation
        _store.Vehicle.Wheelbase = 2.5;
        _store.Vehicle.TrackWidth = 1.8;
        _store.Vehicle.AntennaPivot = 1.0;
        _store.Vehicle.AntennaHeight = 2.0;
        _store.Vehicle.AntennaOffset = 0.0;
        _store.AutoSteer.SteerResponseHold = 3.0;
        _store.AutoSteer.StanleyAggressiveness = 1.0;

        var completedFired = false;
        wizard.Completed += (_, _) => completedFired = true;

        // Collect titles as we navigate
        var visitedTitles = new List<string>();
        visitedTitles.Add(wizard.CurrentStep!.Title);
        Assert.That(wizard.CurrentStep!.Title, Is.EqualTo("Welcome to AutoSteer Setup"));

        // Navigate through steps 0..13 (all except last) via NextCommand
        for (var i = 0; i < 13; i++)
        {
            await ExecuteNextAsync(wizard);
            visitedTitles.Add(wizard.CurrentStep!.Title);
        }

        // Should be on last step (index 13)
        Assert.That(wizard.CurrentStepIndex, Is.EqualTo(13));
        Assert.That(wizard.CurrentStep!.Title, Is.EqualTo("Setup Complete"));
        Assert.That(wizard.IsOnLastStep, Is.True);

        // All 14 step titles should be unique
        Assert.That(visitedTitles.Count, Is.EqualTo(14));
        Assert.That(visitedTitles.Distinct().Count(), Is.EqualTo(14),
            "All step titles should be unique");

        // Finish the wizard
        await ExecuteFinishAsync(wizard);

        Assert.That(completedFired, Is.True, "Completed event should fire on finish");
        _configService.Received(1).SaveProfiles(Arg.Any<string>(), Arg.Any<string>());
    }

    // =========================================================================
    // 2. Cancel preserves original config
    // =========================================================================

    [Test]
    public void CancelWizard_PreservesOriginalConfigValues()
    {
        // Set known config values before starting wizard
        _store.Vehicle.Wheelbase = 3.0;
        _store.Vehicle.TrackWidth = 1.5;

        var wizard = CreateWizard();

        // Navigate to vehicle dimensions step (index 2) and modify
        wizard.GoToStep(3);
        var dimStep = (VehicleDimensionsStepViewModel)wizard.CurrentStep!;
        dimStep.Wheelbase = 9.9;
        dimStep.TrackWidth = 5.5;

        // Cancel the wizard
        var cancelledFired = false;
        wizard.Cancelled += (_, _) => cancelledFired = true;
        wizard.CancelCommand.Execute(null);

        Assert.That(cancelledFired, Is.True);

        // SaveProfile was never called.
        _configService.DidNotReceive().SaveProfiles(Arg.Any<string>(), Arg.Any<string>());
    }

    // =========================================================================
    // 3. Navigation state correctness
    // =========================================================================

    [Test]
    public async Task NavigationState_FirstStep_CorrectFlags()
    {
        var wizard = CreateWizard();

        Assert.That(wizard.CurrentStepIndex, Is.EqualTo(0));
        Assert.That(wizard.IsOnFirstStep, Is.True);
        Assert.That(wizard.CanGoBack, Is.False);
        Assert.That(wizard.CanGoNext, Is.True);
        Assert.That(wizard.IsOnLastStep, Is.False);
    }

    [Test]
    public void NavigationState_LastStep_CorrectFlags()
    {
        var wizard = CreateWizard();
        wizard.GoToStep(13);

        Assert.That(wizard.CurrentStepIndex, Is.EqualTo(13));
        Assert.That(wizard.IsOnLastStep, Is.True);
        Assert.That(wizard.IsOnFirstStep, Is.False);
        Assert.That(wizard.CanGoNext, Is.False);
        Assert.That(wizard.CanGoBack, Is.True);
    }

    [Test]
    public async Task NavigationState_MiddleStep_BothDirectionsEnabled()
    {
        var wizard = CreateWizard();

        // Navigate to step 4 (Hardware Config - a middle step)
        _store.Vehicle.Wheelbase = 2.5;
        _store.Vehicle.TrackWidth = 1.8;
        _store.Vehicle.AntennaPivot = 1.0;
        _store.Vehicle.AntennaHeight = 2.0;

        for (var i = 0; i < 4; i++)
            await ExecuteNextAsync(wizard);

        Assert.That(wizard.CurrentStepIndex, Is.EqualTo(4));
        Assert.That(wizard.CanGoBack, Is.True);
        Assert.That(wizard.CanGoNext, Is.True);
        Assert.That(wizard.IsOnFirstStep, Is.False);
        Assert.That(wizard.IsOnLastStep, Is.False);
    }

    // =========================================================================
    // 4. Skip optional steps
    // =========================================================================

    [Test]
    public void SkipCommand_OnSkippableStep_Advances()
    {
        var wizard = CreateWizard();

        // Step 12 (SpeedAndSensors) has CanSkip=true
        wizard.GoToStep(12);
        Assert.That(wizard.CurrentStep!.CanSkip, Is.True,
            "SpeedAndSensors step should be skippable");
        Assert.That(wizard.CanSkip, Is.True);

        var indexBefore = wizard.CurrentStepIndex;
        wizard.SkipCommand.Execute(null);

        Assert.That(wizard.CurrentStepIndex, Is.EqualTo(indexBefore + 1),
            "Skip should advance to next step");
    }

    [Test]
    public void SkipCommand_OnNonSkippableStep_DoesNotAdvance()
    {
        var wizard = CreateWizard();

        // Step 1 (VehicleType) has CanSkip=false
        wizard.GoToStep(1);
        Assert.That(wizard.CurrentStep!.CanSkip, Is.False,
            "VehicleType step should not be skippable");
        Assert.That(wizard.CanSkip, Is.False);

        var indexBefore = wizard.CurrentStepIndex;
        // SkipCommand.CanExecute is false, but calling Execute should be a no-op
        if (((IRelayCommand)wizard.SkipCommand).CanExecute(null))
            wizard.SkipCommand.Execute(null);

        Assert.That(wizard.CurrentStepIndex, Is.EqualTo(indexBefore),
            "Should stay on same step when skip is not allowed");
    }

    // =========================================================================
    // 5. Back navigation preserves values
    // =========================================================================

    [Test]
    public async Task BackNavigation_PreservesStepValues()
    {
        var wizard = CreateWizard();

        // Go to vehicle dimensions step (index 3, after Welcome, VehicleType, HardwareInstalled)
        await ExecuteNextAsync(wizard);
        await ExecuteNextAsync(wizard);
        await ExecuteNextAsync(wizard);
        Assert.That(wizard.CurrentStepIndex, Is.EqualTo(3));

        // Set a value
        var dimStep = (VehicleDimensionsStepViewModel)wizard.CurrentStep!;
        dimStep.Wheelbase = 4.2;
        dimStep.TrackWidth = 2.0;

        // Go next (triggers OnLeaving which saves to config)
        await ExecuteNextAsync(wizard);
        Assert.That(wizard.CurrentStepIndex, Is.EqualTo(4));

        // Go back (triggers OnEntering which loads from config)
        wizard.BackCommand.Execute(null);
        Assert.That(wizard.CurrentStepIndex, Is.EqualTo(3));

        var sameStep = (VehicleDimensionsStepViewModel)wizard.CurrentStep!;
        Assert.That(sameStep.Wheelbase, Is.EqualTo(4.2),
            "Wheelbase value should persist after going back");
        Assert.That(sameStep.TrackWidth, Is.EqualTo(2.0),
            "TrackWidth value should persist after going back");
    }

    // =========================================================================
    // 6. Validation blocks next
    // =========================================================================

    [Test]
    public async Task Validation_InvalidValue_BlocksNavigation()
    {
        var wizard = CreateWizard();

        // Go to vehicle dimensions step (index 3)
        await ExecuteNextAsync(wizard);
        await ExecuteNextAsync(wizard);
        await ExecuteNextAsync(wizard);
        Assert.That(wizard.CurrentStepIndex, Is.EqualTo(3));

        // Set invalid value (too small)
        var dimStep = (VehicleDimensionsStepViewModel)wizard.CurrentStep!;
        dimStep.Wheelbase = 0.1;

        // Try to go next - should stay on same step
        await ExecuteNextAsync(wizard);
        Assert.That(wizard.CurrentStepIndex, Is.EqualTo(3),
            "Should stay on step when validation fails");
        Assert.That(dimStep.ValidationMessage, Is.Not.Null.And.Not.Empty,
            "Validation message should be set");
    }

    [Test]
    public async Task Validation_ValidValue_AllowsNavigation()
    {
        var wizard = CreateWizard();

        // Go to vehicle dimensions step (index 3)
        await ExecuteNextAsync(wizard);
        await ExecuteNextAsync(wizard);
        await ExecuteNextAsync(wizard);
        Assert.That(wizard.CurrentStepIndex, Is.EqualTo(3));

        // Set valid values
        var dimStep = (VehicleDimensionsStepViewModel)wizard.CurrentStep!;
        dimStep.Wheelbase = 2.5;
        dimStep.TrackWidth = 1.8;

        // Try to go next - should advance
        await ExecuteNextAsync(wizard);
        Assert.That(wizard.CurrentStepIndex, Is.EqualTo(4),
            "Should advance when validation passes");
    }

    // =========================================================================
    // 7. Progress tracking
    // =========================================================================

    [Test]
    public async Task Progress_IncreasesAsStepsAdvance()
    {
        var wizard = CreateWizard();
        _store.Vehicle.Wheelbase = 2.5;
        _store.Vehicle.TrackWidth = 1.8;

        var initialProgress = wizard.Progress;
        Assert.That(wizard.StepDisplay, Is.EqualTo("Step 1 of 14"));
        Assert.That(initialProgress, Is.GreaterThan(0));

        await ExecuteNextAsync(wizard);
        Assert.That(wizard.Progress, Is.GreaterThan(initialProgress));
        Assert.That(wizard.StepDisplay, Is.EqualTo("Step 2 of 14"));

        await ExecuteNextAsync(wizard);
        Assert.That(wizard.Progress, Is.GreaterThan(initialProgress));
        Assert.That(wizard.StepDisplay, Is.EqualTo("Step 3 of 14"));

        // Jump to last step
        wizard.GoToStep(13);
        Assert.That(wizard.StepDisplay, Is.EqualTo("Step 14 of 14"));
        Assert.That(wizard.Progress, Is.EqualTo(1.0).Within(0.001));
    }

    // =========================================================================
    // 8. GoToStep direct navigation
    // =========================================================================

    [Test]
    public void GoToStep_JumpsToCorrectIndex()
    {
        var wizard = CreateWizard();

        wizard.GoToStep(5);
        Assert.That(wizard.CurrentStepIndex, Is.EqualTo(5));
        Assert.That(wizard.CurrentStep, Is.EqualTo(wizard.Steps[5]));
    }

    [Test]
    public void GoToStep_OutOfRange_DoesNothing()
    {
        var wizard = CreateWizard();

        wizard.GoToStep(-1);
        Assert.That(wizard.CurrentStepIndex, Is.EqualTo(0),
            "Negative index should be ignored");

        wizard.GoToStep(100);
        Assert.That(wizard.CurrentStepIndex, Is.EqualTo(0),
            "Out-of-range index should be ignored");
    }

    // =========================================================================
    // 9. Completed and Cancelled events
    // =========================================================================

    [Test]
    public async Task CompletedEvent_FiresOnFinish()
    {
        var wizard = CreateWizard();
        wizard.GoToStep(13);

        var completedFired = false;
        wizard.Completed += (_, _) => completedFired = true;

        await ExecuteFinishAsync(wizard);

        Assert.That(completedFired, Is.True);
    }

    [Test]
    public void CancelledEvent_FiresOnCancel()
    {
        var wizard = CreateWizard();

        var cancelledFired = false;
        wizard.Cancelled += (_, _) => cancelledFired = true;

        wizard.CancelCommand.Execute(null);

        Assert.That(cancelledFired, Is.True);
    }

    [Test]
    public async Task CloseRequestedEvent_FiresOnBothFinishAndCancel()
    {
        // Test finish path
        var wizard1 = CreateWizard();
        wizard1.GoToStep(13);
        var closeOnFinish = false;
        wizard1.CloseRequested += (_, _) => closeOnFinish = true;
        await ExecuteFinishAsync(wizard1);
        Assert.That(closeOnFinish, Is.True, "CloseRequested should fire on finish");

        // Test cancel path
        var wizard2 = CreateWizard();
        var closeOnCancel = false;
        wizard2.CloseRequested += (_, _) => closeOnCancel = true;
        wizard2.CancelCommand.Execute(null);
        Assert.That(closeOnCancel, Is.True, "CloseRequested should fire on cancel");
    }

    // =========================================================================
    // 10. Config persistence on completion
    // =========================================================================

    [Test]
    public async Task ConfigPersistence_DimensionsModified_SavedOnCompletion()
    {
        _store.Vehicle.Wheelbase = 2.0;
        _store.Vehicle.TrackWidth = 1.5;

        var wizard = CreateWizard();

        // Navigate to vehicle dimensions step (index 2)
        await ExecuteNextAsync(wizard);
        await ExecuteNextAsync(wizard);
        await ExecuteNextAsync(wizard);
        Assert.That(wizard.CurrentStepIndex, Is.EqualTo(3));

        // Modify values
        var dimStep = (VehicleDimensionsStepViewModel)wizard.CurrentStep!;
        dimStep.Wheelbase = 3.7;
        dimStep.TrackWidth = 2.1;

        // Navigate forward (OnLeaving saves to store)
        await ExecuteNextAsync(wizard);

        // Jump to finish and complete
        wizard.GoToStep(13);
        await ExecuteFinishAsync(wizard);

        // Verify the config store has the new values
        Assert.That(_store.Vehicle.Wheelbase, Is.EqualTo(3.7),
            "Wheelbase should be updated in config store after leaving the step");
        Assert.That(_store.Vehicle.TrackWidth, Is.EqualTo(2.1),
            "TrackWidth should be updated in config store after leaving the step");

        // Verify SaveProfile was called
        _configService.Received(1).SaveProfiles(Arg.Any<string>(), Arg.Any<string>());
    }

    // =========================================================================
    // 11. Conditional step skipping (ShouldSkip)
    // =========================================================================

    /// <summary>
    /// A test step that can toggle ShouldSkip at runtime.
    /// </summary>
    private class SkippableTestStep : WizardStepViewModel
    {
        public override string Title { get; }
        public override string Description => "Test step";
        public bool Skip { get; set; }
        public override bool ShouldSkip => Skip;

        public SkippableTestStep(string title = "Test")
        {
            Title = title;
        }
    }

    /// <summary>
    /// A minimal wizard for testing step skip logic in isolation.
    /// </summary>
    private class TestWizard : WizardViewModel
    {
        public override string WizardTitle => "Test";

        public TestWizard(params WizardStepViewModel[] steps)
        {
            foreach (var s in steps)
                AddStep(s);
            Initialize();
        }
    }

    [Test]
    public async Task ConditionalSkip_GoNext_SkipsStepsWithShouldSkipTrue()
    {
        var step0 = new SkippableTestStep("Step0");
        var step1 = new SkippableTestStep("Step1") { Skip = true };
        var step2 = new SkippableTestStep("Step2");

        var wizard = new TestWizard(step0, step1, step2);
        Assert.That(wizard.CurrentStepIndex, Is.EqualTo(0));

        await ExecuteNextAsync(wizard);

        Assert.That(wizard.CurrentStepIndex, Is.EqualTo(2),
            "GoNext should skip step1 (ShouldSkip=true) and land on step2");
        Assert.That(wizard.CurrentStep!.Title, Is.EqualTo("Step2"));
    }

    [Test]
    public async Task ConditionalSkip_GoBack_SkipsStepsWithShouldSkipTrue()
    {
        var step0 = new SkippableTestStep("Step0");
        var step1 = new SkippableTestStep("Step1") { Skip = true };
        var step2 = new SkippableTestStep("Step2");

        var wizard = new TestWizard(step0, step1, step2);
        wizard.GoToStep(2);
        Assert.That(wizard.CurrentStepIndex, Is.EqualTo(2));

        wizard.BackCommand.Execute(null);

        Assert.That(wizard.CurrentStepIndex, Is.EqualTo(0),
            "GoBack should skip step1 (ShouldSkip=true) and land on step0");
        Assert.That(wizard.CurrentStep!.Title, Is.EqualTo("Step0"));
    }

    [Test]
    public async Task ConditionalSkip_AllMiddleStepsSkipped_JumpsToLast()
    {
        var step0 = new SkippableTestStep("First");
        var step1 = new SkippableTestStep("Mid1") { Skip = true };
        var step2 = new SkippableTestStep("Mid2") { Skip = true };
        var step3 = new SkippableTestStep("Mid3") { Skip = true };
        var step4 = new SkippableTestStep("Last");

        var wizard = new TestWizard(step0, step1, step2, step3, step4);
        Assert.That(wizard.CurrentStepIndex, Is.EqualTo(0));

        await ExecuteNextAsync(wizard);

        Assert.That(wizard.CurrentStepIndex, Is.EqualTo(4),
            "GoNext should skip all middle steps and land on Last");
        Assert.That(wizard.CurrentStep!.Title, Is.EqualTo("Last"));
    }

    [Test]
    public async Task ConditionalSkip_NoSkippedSteps_NormalNavigation()
    {
        var step0 = new SkippableTestStep("Step0");
        var step1 = new SkippableTestStep("Step1");
        var step2 = new SkippableTestStep("Step2");

        var wizard = new TestWizard(step0, step1, step2);
        Assert.That(wizard.CurrentStepIndex, Is.EqualTo(0));

        await ExecuteNextAsync(wizard);
        Assert.That(wizard.CurrentStepIndex, Is.EqualTo(1),
            "With no skips, GoNext should go to adjacent step");

        await ExecuteNextAsync(wizard);
        Assert.That(wizard.CurrentStepIndex, Is.EqualTo(2));

        wizard.BackCommand.Execute(null);
        Assert.That(wizard.CurrentStepIndex, Is.EqualTo(1),
            "With no skips, GoBack should go to adjacent step");
    }

    // =========================================================================
    // 12. GPS Only skips autosteer steps
    // =========================================================================

    [Test]
    public async Task GpsOnly_SkipsAutoSteerSteps()
    {
        var wizard = CreateWizard();

        // Set HardwareLevel to GPS Only (0) on the HardwareInstalled step
        var hardwareStep = (HardwareInstalledStepViewModel)wizard.Steps[2];
        hardwareStep.HardwareLevel = 0;
        Assert.That(hardwareStep.HasAutoSteer, Is.False);

        // Set valid values for dimension/antenna steps
        _store.Vehicle.Wheelbase = 2.5;
        _store.Vehicle.TrackWidth = 1.8;
        _store.Vehicle.AntennaPivot = 1.0;
        _store.Vehicle.AntennaHeight = 2.0;
        _store.Vehicle.AntennaOffset = 0.0;

        // Navigate to Antenna step (index 4)
        // Steps: 0=Welcome, 1=VehicleType, 2=HardwareInstalled, 3=VehicleDimensions, 4=Antenna
        for (var i = 0; i < 4; i++)
            await ExecuteNextAsync(wizard);

        Assert.That(wizard.CurrentStepIndex, Is.EqualTo(4),
            "Should be on Antenna step (index 4)");
        Assert.That(wizard.CurrentStep, Is.InstanceOf<AntennaSetupStepViewModel>());

        // Next from Antenna should skip steps 5-11 (HardwareConfig, Roll, WAS, AutoCal, CpdCircle, Ackermann, SteeringGains)
        // and land on step 12 (SpeedAndSensors)
        await ExecuteNextAsync(wizard);

        Assert.That(wizard.CurrentStepIndex, Is.EqualTo(12),
            "GPS Only should skip autosteer steps 5-11 and land on SpeedAndSensors (index 12)");
        Assert.That(wizard.CurrentStep, Is.InstanceOf<SpeedAndSensorsStepViewModel>());

        // Going back from SpeedAndSensors should skip back to Antenna
        wizard.BackCommand.Execute(null);

        Assert.That(wizard.CurrentStepIndex, Is.EqualTo(4),
            "Going back should skip autosteer steps and return to Antenna (index 4)");
        Assert.That(wizard.CurrentStep, Is.InstanceOf<AntennaSetupStepViewModel>());
    }

    [Test]
    public async Task AutoSteer_ShowsAllSteps()
    {
        var wizard = CreateWizard();

        // Set HardwareLevel to AutoSteer (1)
        var hardwareStep = (HardwareInstalledStepViewModel)wizard.Steps[2];
        hardwareStep.HardwareLevel = 1;
        Assert.That(hardwareStep.HasAutoSteer, Is.True);

        // Set valid values for all steps
        _store.Vehicle.Wheelbase = 2.5;
        _store.Vehicle.TrackWidth = 1.8;
        _store.Vehicle.AntennaPivot = 1.0;
        _store.Vehicle.AntennaHeight = 2.0;
        _store.Vehicle.AntennaOffset = 0.0;
        _store.AutoSteer.SteerResponseHold = 3.0;
        _store.AutoSteer.StanleyAggressiveness = 1.0;

        // Navigate through all 14 steps collecting titles
        var visitedTitles = new List<string>();
        visitedTitles.Add(wizard.CurrentStep!.Title);

        for (var i = 0; i < 13; i++)
        {
            await ExecuteNextAsync(wizard);
            visitedTitles.Add(wizard.CurrentStep!.Title);
        }

        // Should have visited all 14 steps sequentially
        Assert.That(visitedTitles.Count, Is.EqualTo(14),
            "AutoSteer path should visit all 14 steps");
        Assert.That(visitedTitles.Distinct().Count(), Is.EqualTo(14),
            "All step titles should be unique - no steps were skipped");

        // Verify autosteer steps were visited
        Assert.That(visitedTitles, Does.Contain("Hardware Configuration"));
        Assert.That(visitedTitles, Does.Contain("Roll Calibration"));
        Assert.That(visitedTitles, Does.Contain("Wheel Angle Sensor"));
        Assert.That(visitedTitles, Does.Contain("Auto Motor Calibration"));
        Assert.That(visitedTitles, Does.Contain("CPD Circle Test"));
        Assert.That(visitedTitles, Does.Contain("Ackermann Calibration"));
        Assert.That(visitedTitles, Does.Contain("Steering Gains"));
    }
}

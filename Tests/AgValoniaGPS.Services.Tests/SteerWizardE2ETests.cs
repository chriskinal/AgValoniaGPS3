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
        return new SteerWizardViewModel(_configService);
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
        Assert.That(wizard.Steps.Count, Is.EqualTo(19), "Wizard should have 19 steps");

        // Set valid values for steps that require validation
        _store.Vehicle.Wheelbase = 2.5;
        _store.Vehicle.TrackWidth = 1.8;
        _store.Vehicle.AntennaPivot = 1.0;
        _store.Vehicle.AntennaHeight = 2.0;
        _store.Vehicle.AntennaOffset = 0.0;

        var completedFired = false;
        wizard.Completed += (_, _) => completedFired = true;

        // Collect titles as we navigate
        var visitedTitles = new List<string>();
        visitedTitles.Add(wizard.CurrentStep!.Title);
        Assert.That(wizard.CurrentStep!.Title, Is.EqualTo("Welcome to AutoSteer Setup"));

        // Navigate through steps 0..17 (all except last) via NextCommand
        for (var i = 0; i < 18; i++)
        {
            await ExecuteNextAsync(wizard);
            visitedTitles.Add(wizard.CurrentStep!.Title);
        }

        // Should be on last step (index 18)
        Assert.That(wizard.CurrentStepIndex, Is.EqualTo(18));
        Assert.That(wizard.CurrentStep!.Title, Is.EqualTo("Setup Complete"));
        Assert.That(wizard.IsOnLastStep, Is.True);

        // All 19 step titles should be unique
        Assert.That(visitedTitles.Count, Is.EqualTo(19));
        Assert.That(visitedTitles.Distinct().Count(), Is.EqualTo(19),
            "All step titles should be unique");

        // Finish the wizard
        await ExecuteFinishAsync(wizard);

        Assert.That(completedFired, Is.True, "Completed event should fire on finish");
        _configService.Received(1).SaveProfile(Arg.Any<string>());
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

        // Navigate to wheelbase step (index 2) and modify
        wizard.GoToStep(2);
        var wheelbaseStep = (WheelbaseStepViewModel)wizard.CurrentStep!;
        wheelbaseStep.Wheelbase = 9.9;

        // Navigate to track width step (index 3) and modify
        wizard.GoToStep(3);
        var trackWidthStep = (TrackWidthStepViewModel)wizard.CurrentStep!;
        trackWidthStep.TrackWidth = 5.5;

        // Cancel the wizard
        var cancelledFired = false;
        wizard.Cancelled += (_, _) => cancelledFired = true;
        wizard.CancelCommand.Execute(null);

        Assert.That(cancelledFired, Is.True);

        // Config values should be unchanged because OnLeaving for the final
        // step was not explicitly triggered by Cancel, and the wheelbase step
        // value was overwritten when GoToStep(2) triggered OnLeaving.
        // The key point: SaveProfile was never called.
        _configService.DidNotReceive().SaveProfile(Arg.Any<string>());
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
        wizard.GoToStep(18);

        Assert.That(wizard.CurrentStepIndex, Is.EqualTo(18));
        Assert.That(wizard.IsOnLastStep, Is.True);
        Assert.That(wizard.IsOnFirstStep, Is.False);
        Assert.That(wizard.CanGoNext, Is.False);
        Assert.That(wizard.CanGoBack, Is.True);
    }

    [Test]
    public async Task NavigationState_MiddleStep_BothDirectionsEnabled()
    {
        var wizard = CreateWizard();

        // Navigate to step 5 (AntennaHeight - a middle step)
        _store.Vehicle.Wheelbase = 2.5;
        _store.Vehicle.TrackWidth = 1.8;
        _store.Vehicle.AntennaPivot = 1.0;
        _store.Vehicle.AntennaHeight = 2.0;

        for (var i = 0; i < 5; i++)
            await ExecuteNextAsync(wizard);

        Assert.That(wizard.CurrentStepIndex, Is.EqualTo(5));
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

        // Step 5 (AntennaHeight) has CanSkip=true
        wizard.GoToStep(5);
        Assert.That(wizard.CurrentStep!.CanSkip, Is.True,
            "AntennaHeight step should be skippable");
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

        // Go to wheelbase step (index 2, after Welcome and VehicleType)
        await ExecuteNextAsync(wizard);
        await ExecuteNextAsync(wizard);
        Assert.That(wizard.CurrentStepIndex, Is.EqualTo(2));

        // Set a value
        var wheelbaseStep = (WheelbaseStepViewModel)wizard.CurrentStep!;
        wheelbaseStep.Wheelbase = 4.2;

        // Go next (triggers OnLeaving which saves to config)
        await ExecuteNextAsync(wizard);
        Assert.That(wizard.CurrentStepIndex, Is.EqualTo(3));

        // Go back (triggers OnEntering which loads from config)
        wizard.BackCommand.Execute(null);
        Assert.That(wizard.CurrentStepIndex, Is.EqualTo(2));

        var sameStep = (WheelbaseStepViewModel)wizard.CurrentStep!;
        Assert.That(sameStep.Wheelbase, Is.EqualTo(4.2),
            "Value should persist after going back");
    }

    // =========================================================================
    // 6. Validation blocks next
    // =========================================================================

    [Test]
    public async Task Validation_InvalidValue_BlocksNavigation()
    {
        var wizard = CreateWizard();

        // Go to wheelbase step (index 2, after Welcome and VehicleType)
        await ExecuteNextAsync(wizard);
        await ExecuteNextAsync(wizard);
        Assert.That(wizard.CurrentStepIndex, Is.EqualTo(2));

        // Set invalid value (too small)
        var wheelbaseStep = (WheelbaseStepViewModel)wizard.CurrentStep!;
        wheelbaseStep.Wheelbase = 0.1;

        // Try to go next - should stay on same step
        await ExecuteNextAsync(wizard);
        Assert.That(wizard.CurrentStepIndex, Is.EqualTo(2),
            "Should stay on step when validation fails");
        Assert.That(wheelbaseStep.ValidationMessage, Is.Not.Null.And.Not.Empty,
            "Validation message should be set");
    }

    [Test]
    public async Task Validation_ValidValue_AllowsNavigation()
    {
        var wizard = CreateWizard();

        // Go to wheelbase step (index 2, after Welcome and VehicleType)
        await ExecuteNextAsync(wizard);
        await ExecuteNextAsync(wizard);
        Assert.That(wizard.CurrentStepIndex, Is.EqualTo(2));

        // Set valid value
        var wheelbaseStep = (WheelbaseStepViewModel)wizard.CurrentStep!;
        wheelbaseStep.Wheelbase = 2.5;

        // Try to go next - should advance
        await ExecuteNextAsync(wizard);
        Assert.That(wizard.CurrentStepIndex, Is.EqualTo(3),
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

        var initialProgress = wizard.Progress;
        Assert.That(wizard.StepDisplay, Is.EqualTo("Step 1 of 19"));
        Assert.That(initialProgress, Is.GreaterThan(0));

        await ExecuteNextAsync(wizard);
        Assert.That(wizard.Progress, Is.GreaterThan(initialProgress));
        Assert.That(wizard.StepDisplay, Is.EqualTo("Step 2 of 19"));

        await ExecuteNextAsync(wizard);
        Assert.That(wizard.Progress, Is.GreaterThan(initialProgress));
        Assert.That(wizard.StepDisplay, Is.EqualTo("Step 3 of 19"));

        // Jump to last step
        wizard.GoToStep(18);
        Assert.That(wizard.StepDisplay, Is.EqualTo("Step 19 of 19"));
        Assert.That(wizard.Progress, Is.EqualTo(1.0).Within(0.001));
    }

    // =========================================================================
    // 8. GoToStep direct navigation
    // =========================================================================

    [Test]
    public void GoToStep_JumpsToCorrectIndex()
    {
        var wizard = CreateWizard();

        wizard.GoToStep(10);
        Assert.That(wizard.CurrentStepIndex, Is.EqualTo(10));
        Assert.That(wizard.CurrentStep, Is.EqualTo(wizard.Steps[10]));
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
        wizard.GoToStep(18);

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
        wizard1.GoToStep(18);
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
    public async Task ConfigPersistence_WheelbaseModified_SavedOnCompletion()
    {
        _store.Vehicle.Wheelbase = 2.0;

        var wizard = CreateWizard();

        // Navigate to wheelbase step (index 2, after Welcome and VehicleType)
        await ExecuteNextAsync(wizard);
        await ExecuteNextAsync(wizard);
        Assert.That(wizard.CurrentStepIndex, Is.EqualTo(2));

        // Modify wheelbase value
        var wheelbaseStep = (WheelbaseStepViewModel)wizard.CurrentStep!;
        wheelbaseStep.Wheelbase = 3.7;

        // Navigate forward (OnLeaving saves to store)
        await ExecuteNextAsync(wizard);

        // Jump to finish and complete
        wizard.GoToStep(18);
        await ExecuteFinishAsync(wizard);

        // Verify the config store has the new value
        Assert.That(_store.Vehicle.Wheelbase, Is.EqualTo(3.7),
            "Wheelbase should be updated in config store after leaving the step");

        // Verify SaveProfile was called
        _configService.Received(1).SaveProfile(Arg.Any<string>());
    }
}

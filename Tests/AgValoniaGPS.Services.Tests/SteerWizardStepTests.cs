using AgValoniaGPS.Models.Configuration;
using AgValoniaGPS.Services.Interfaces;
using AgValoniaGPS.ViewModels.Wizards;
using AgValoniaGPS.ViewModels.Wizards.SteerWizard;
using NSubstitute;

namespace AgValoniaGPS.Services.Tests;

/// <summary>
/// Helper that exposes protected OnEntering/OnLeaving for testing.
/// </summary>
internal class TestableStep<T> where T : WizardStepViewModel
{
    public T Step { get; }

    public TestableStep(T step)
    {
        Step = step;
    }

    /// <summary>
    /// Triggers OnEntering by toggling IsActive via reflection
    /// (IsActive has internal set, OnEntering/OnLeaving are protected).
    /// </summary>
    public void Enter()
    {
        var prop = typeof(WizardStepViewModel).GetProperty(nameof(WizardStepViewModel.IsActive));
        // IsActive setter is internal; use reflection
        prop!.SetValue(Step, true);
    }

    public void Leave()
    {
        var prop = typeof(WizardStepViewModel).GetProperty(nameof(WizardStepViewModel.IsActive));
        prop!.SetValue(Step, false);
    }
}

/// <summary>
/// Tests for all SteerWizard step ViewModels.
/// Verifies config loading (OnEntering), config saving (OnLeaving),
/// validation logic, and CanSkip behavior.
/// </summary>
[TestFixture]
[NonParallelizable]
public class SteerWizardStepTests
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

    // =========================================================================
    // WelcomeStepViewModel
    // =========================================================================

    [Test]
    public void WelcomeStep_HasCorrectTitle()
    {
        var step = new WelcomeStepViewModel();
        Assert.That(step.Title, Is.EqualTo("Welcome to AutoSteer Setup"));
    }

    [Test]
    public void WelcomeStep_CanGoBack_IsFalse()
    {
        var step = new WelcomeStepViewModel();
        Assert.That(step.CanGoBack, Is.False);
    }

    [Test]
    public void WelcomeStep_CanSkip_IsFalse()
    {
        var step = new WelcomeStepViewModel();
        Assert.That(step.CanSkip, Is.False);
    }

    [Test]
    public async Task WelcomeStep_Validation_AlwaysPasses()
    {
        var step = new WelcomeStepViewModel();
        Assert.That(await step.ValidateAsync(), Is.True);
    }

    // =========================================================================
    // WheelbaseStepViewModel
    // =========================================================================

    [Test]
    public void WheelbaseStep_OnEntering_LoadsFromConfig()
    {
        _store.Vehicle.Wheelbase = 3.5;
        var testable = new TestableStep<WheelbaseStepViewModel>(
            new WheelbaseStepViewModel(_configService));

        testable.Enter();

        Assert.That(testable.Step.Wheelbase, Is.EqualTo(3.5));
    }

    [Test]
    public void WheelbaseStep_OnLeaving_SavesToConfig()
    {
        var testable = new TestableStep<WheelbaseStepViewModel>(
            new WheelbaseStepViewModel(_configService));
        testable.Enter();
        testable.Step.Wheelbase = 4.2;

        testable.Leave();

        Assert.That(_store.Vehicle.Wheelbase, Is.EqualTo(4.2));
    }

    [Test]
    public async Task WheelbaseStep_Validation_ValidValue()
    {
        var step = new WheelbaseStepViewModel(_configService);
        step.Wheelbase = 2.5;

        Assert.That(await step.ValidateAsync(), Is.True);
        Assert.That(step.ValidationMessage, Is.Null);
    }

    [Test]
    public async Task WheelbaseStep_Validation_TooSmall()
    {
        var step = new WheelbaseStepViewModel(_configService);
        step.Wheelbase = 0.3;

        Assert.That(await step.ValidateAsync(), Is.False);
        Assert.That(step.ValidationMessage, Does.Contain("at least 0.5"));
    }

    [Test]
    public async Task WheelbaseStep_Validation_TooLarge()
    {
        var step = new WheelbaseStepViewModel(_configService);
        step.Wheelbase = 16;

        Assert.That(await step.ValidateAsync(), Is.False);
        Assert.That(step.ValidationMessage, Does.Contain("too large"));
    }

    [Test]
    public async Task WheelbaseStep_Validation_BoundaryLow()
    {
        var step = new WheelbaseStepViewModel(_configService);
        step.Wheelbase = 0.5;
        Assert.That(await step.ValidateAsync(), Is.True);
    }

    [Test]
    public async Task WheelbaseStep_Validation_BoundaryHigh()
    {
        var step = new WheelbaseStepViewModel(_configService);
        step.Wheelbase = 15;
        Assert.That(await step.ValidateAsync(), Is.True);
    }

    [Test]
    public void WheelbaseStep_CanSkip_IsFalse()
    {
        var step = new WheelbaseStepViewModel(_configService);
        Assert.That(step.CanSkip, Is.False);
    }

    // =========================================================================
    // TrackWidthStepViewModel
    // =========================================================================

    [Test]
    public void TrackWidthStep_OnEntering_LoadsFromConfig()
    {
        _store.Vehicle.TrackWidth = 2.0;
        var testable = new TestableStep<TrackWidthStepViewModel>(
            new TrackWidthStepViewModel(_configService));

        testable.Enter();

        Assert.That(testable.Step.TrackWidth, Is.EqualTo(2.0));
    }

    [Test]
    public void TrackWidthStep_OnLeaving_SavesToConfig()
    {
        var testable = new TestableStep<TrackWidthStepViewModel>(
            new TrackWidthStepViewModel(_configService));
        testable.Enter();
        testable.Step.TrackWidth = 1.8;

        testable.Leave();

        Assert.That(_store.Vehicle.TrackWidth, Is.EqualTo(1.8));
    }

    [Test]
    public async Task TrackWidthStep_Validation_ValidValue()
    {
        var step = new TrackWidthStepViewModel(_configService);
        step.TrackWidth = 2.0;
        Assert.That(await step.ValidateAsync(), Is.True);
    }

    [Test]
    public async Task TrackWidthStep_Validation_TooSmall()
    {
        var step = new TrackWidthStepViewModel(_configService);
        step.TrackWidth = 0.3;
        Assert.That(await step.ValidateAsync(), Is.False);
        Assert.That(step.ValidationMessage, Does.Contain("at least 0.5"));
    }

    [Test]
    public async Task TrackWidthStep_Validation_TooLarge()
    {
        var step = new TrackWidthStepViewModel(_configService);
        step.TrackWidth = 11;
        Assert.That(await step.ValidateAsync(), Is.False);
        Assert.That(step.ValidationMessage, Does.Contain("too large"));
    }

    [Test]
    public void TrackWidthStep_CanSkip_IsFalse()
    {
        var step = new TrackWidthStepViewModel(_configService);
        Assert.That(step.CanSkip, Is.False);
    }

    // =========================================================================
    // AntennaPivotStepViewModel
    // =========================================================================

    [Test]
    public void AntennaPivotStep_OnEntering_LoadsFromConfig()
    {
        _store.Vehicle.AntennaPivot = 1.2;
        var testable = new TestableStep<AntennaPivotStepViewModel>(
            new AntennaPivotStepViewModel(_configService));

        testable.Enter();

        Assert.That(testable.Step.AntennaPivot, Is.EqualTo(1.2));
    }

    [Test]
    public void AntennaPivotStep_OnLeaving_SavesToConfig()
    {
        var testable = new TestableStep<AntennaPivotStepViewModel>(
            new AntennaPivotStepViewModel(_configService));
        testable.Enter();
        testable.Step.AntennaPivot = -2.5;

        testable.Leave();

        Assert.That(_store.Vehicle.AntennaPivot, Is.EqualTo(-2.5));
    }

    [Test]
    public async Task AntennaPivotStep_Validation_ValidValue()
    {
        var step = new AntennaPivotStepViewModel(_configService);
        step.AntennaPivot = 3.0;
        Assert.That(await step.ValidateAsync(), Is.True);
    }

    [Test]
    public async Task AntennaPivotStep_Validation_NegativeValid()
    {
        var step = new AntennaPivotStepViewModel(_configService);
        step.AntennaPivot = -5.0;
        Assert.That(await step.ValidateAsync(), Is.True);
    }

    [Test]
    public async Task AntennaPivotStep_Validation_TooNegative()
    {
        var step = new AntennaPivotStepViewModel(_configService);
        step.AntennaPivot = -11;
        Assert.That(await step.ValidateAsync(), Is.False);
    }

    [Test]
    public async Task AntennaPivotStep_Validation_TooLarge()
    {
        var step = new AntennaPivotStepViewModel(_configService);
        step.AntennaPivot = 16;
        Assert.That(await step.ValidateAsync(), Is.False);
    }

    [Test]
    public void AntennaPivotStep_CanSkip_IsFalse()
    {
        var step = new AntennaPivotStepViewModel(_configService);
        Assert.That(step.CanSkip, Is.False);
    }

    // =========================================================================
    // AntennaHeightStepViewModel
    // =========================================================================

    [Test]
    public void AntennaHeightStep_OnEntering_LoadsFromConfig()
    {
        _store.Vehicle.AntennaHeight = 2.8;
        var testable = new TestableStep<AntennaHeightStepViewModel>(
            new AntennaHeightStepViewModel(_configService));

        testable.Enter();

        Assert.That(testable.Step.AntennaHeight, Is.EqualTo(2.8));
    }

    [Test]
    public void AntennaHeightStep_OnLeaving_SavesToConfig()
    {
        var testable = new TestableStep<AntennaHeightStepViewModel>(
            new AntennaHeightStepViewModel(_configService));
        testable.Enter();
        testable.Step.AntennaHeight = 3.1;

        testable.Leave();

        Assert.That(_store.Vehicle.AntennaHeight, Is.EqualTo(3.1));
    }

    [Test]
    public async Task AntennaHeightStep_Validation_ValidValue()
    {
        var step = new AntennaHeightStepViewModel(_configService);
        step.AntennaHeight = 2.5;
        Assert.That(await step.ValidateAsync(), Is.True);
    }

    [Test]
    public async Task AntennaHeightStep_Validation_Negative()
    {
        var step = new AntennaHeightStepViewModel(_configService);
        step.AntennaHeight = -1;
        Assert.That(await step.ValidateAsync(), Is.False);
        Assert.That(step.ValidationMessage, Does.Contain("negative"));
    }

    [Test]
    public async Task AntennaHeightStep_Validation_TooLarge()
    {
        var step = new AntennaHeightStepViewModel(_configService);
        step.AntennaHeight = 11;
        Assert.That(await step.ValidateAsync(), Is.False);
        Assert.That(step.ValidationMessage, Does.Contain("too large"));
    }

    [Test]
    public async Task AntennaHeightStep_Validation_ZeroIsValid()
    {
        var step = new AntennaHeightStepViewModel(_configService);
        step.AntennaHeight = 0;
        Assert.That(await step.ValidateAsync(), Is.True);
    }

    [Test]
    public void AntennaHeightStep_CanSkip_IsTrue()
    {
        var step = new AntennaHeightStepViewModel(_configService);
        Assert.That(step.CanSkip, Is.True);
    }

    // =========================================================================
    // AntennaOffsetStepViewModel
    // =========================================================================

    [Test]
    public void AntennaOffsetStep_OnEntering_LoadsFromConfig()
    {
        _store.Vehicle.AntennaOffset = -0.3;
        var testable = new TestableStep<AntennaOffsetStepViewModel>(
            new AntennaOffsetStepViewModel(_configService));

        testable.Enter();

        Assert.That(testable.Step.AntennaOffset, Is.EqualTo(-0.3));
    }

    [Test]
    public void AntennaOffsetStep_OnLeaving_SavesToConfig()
    {
        var testable = new TestableStep<AntennaOffsetStepViewModel>(
            new AntennaOffsetStepViewModel(_configService));
        testable.Enter();
        testable.Step.AntennaOffset = 0.7;

        testable.Leave();

        Assert.That(_store.Vehicle.AntennaOffset, Is.EqualTo(0.7));
    }

    [Test]
    public async Task AntennaOffsetStep_Validation_ValidValue()
    {
        var step = new AntennaOffsetStepViewModel(_configService);
        step.AntennaOffset = 0;
        Assert.That(await step.ValidateAsync(), Is.True);
    }

    [Test]
    public async Task AntennaOffsetStep_Validation_TooNegative()
    {
        var step = new AntennaOffsetStepViewModel(_configService);
        step.AntennaOffset = -6;
        Assert.That(await step.ValidateAsync(), Is.False);
    }

    [Test]
    public async Task AntennaOffsetStep_Validation_TooLarge()
    {
        var step = new AntennaOffsetStepViewModel(_configService);
        step.AntennaOffset = 6;
        Assert.That(await step.ValidateAsync(), Is.False);
    }

    [Test]
    public void AntennaOffsetStep_CanSkip_IsTrue()
    {
        var step = new AntennaOffsetStepViewModel(_configService);
        Assert.That(step.CanSkip, Is.True);
    }

    [Test]
    public void AntennaOffsetStep_IsLeft_WhenNegative()
    {
        var step = new AntennaOffsetStepViewModel(_configService);
        step.AntennaOffset = -0.5;
        Assert.That(step.IsLeft, Is.True);
        Assert.That(step.IsRight, Is.False);
    }

    [Test]
    public void AntennaOffsetStep_IsRight_WhenPositive()
    {
        var step = new AntennaOffsetStepViewModel(_configService);
        step.AntennaOffset = 0.5;
        Assert.That(step.IsRight, Is.True);
        Assert.That(step.IsLeft, Is.False);
    }

    [Test]
    public void AntennaOffsetStep_IsCenter_WhenZero()
    {
        var step = new AntennaOffsetStepViewModel(_configService);
        step.AntennaOffset = 0;
        Assert.That(step.IsCenter, Is.True);
    }

    // =========================================================================
    // SteerEnableStepViewModel
    // =========================================================================

    [Test]
    public void SteerEnableStep_OnEntering_LoadsFromConfig()
    {
        _store.AutoSteer.ExternalEnable = 2;
        var testable = new TestableStep<SteerEnableStepViewModel>(
            new SteerEnableStepViewModel(_configService));

        testable.Enter();

        Assert.That(testable.Step.ExternalEnable, Is.EqualTo(2));
    }

    [Test]
    public void SteerEnableStep_OnLeaving_SavesToConfig()
    {
        var testable = new TestableStep<SteerEnableStepViewModel>(
            new SteerEnableStepViewModel(_configService));
        testable.Enter();
        testable.Step.ExternalEnable = 1;

        testable.Leave();

        Assert.That(_store.AutoSteer.ExternalEnable, Is.EqualTo(1));
    }

    [Test]
    public async Task SteerEnableStep_Validation_ValidValues()
    {
        var step = new SteerEnableStepViewModel(_configService);

        step.ExternalEnable = 0;
        Assert.That(await step.ValidateAsync(), Is.True);

        step.ExternalEnable = 1;
        Assert.That(await step.ValidateAsync(), Is.True);

        step.ExternalEnable = 2;
        Assert.That(await step.ValidateAsync(), Is.True);
    }

    [Test]
    public async Task SteerEnableStep_Validation_InvalidValue()
    {
        var step = new SteerEnableStepViewModel(_configService);
        step.ExternalEnable = 3;
        Assert.That(await step.ValidateAsync(), Is.False);
    }

    [Test]
    public async Task SteerEnableStep_Validation_NegativeValue()
    {
        var step = new SteerEnableStepViewModel(_configService);
        step.ExternalEnable = -1;
        Assert.That(await step.ValidateAsync(), Is.False);
    }

    [Test]
    public void SteerEnableStep_SelectionHelpers()
    {
        var step = new SteerEnableStepViewModel(_configService);

        step.SelectNone();
        Assert.That(step.IsNoneSelected, Is.True);
        Assert.That(step.IsSwitchSelected, Is.False);

        step.SelectSwitch();
        Assert.That(step.IsSwitchSelected, Is.True);

        step.SelectButton();
        Assert.That(step.IsButtonSelected, Is.True);
    }

    [Test]
    public void SteerEnableStep_CanSkip_IsFalse()
    {
        var step = new SteerEnableStepViewModel(_configService);
        Assert.That(step.CanSkip, Is.False);
    }

    // =========================================================================
    // MotorDriverStepViewModel
    // =========================================================================

    [Test]
    public void MotorDriverStep_OnEntering_LoadsFromConfig()
    {
        _store.AutoSteer.MotorDriver = 1;
        var testable = new TestableStep<MotorDriverStepViewModel>(
            new MotorDriverStepViewModel(_configService));

        testable.Enter();

        Assert.That(testable.Step.MotorDriver, Is.EqualTo(1));
    }

    [Test]
    public void MotorDriverStep_OnLeaving_SavesToConfig()
    {
        var testable = new TestableStep<MotorDriverStepViewModel>(
            new MotorDriverStepViewModel(_configService));
        testable.Enter();
        testable.Step.MotorDriver = 1;

        testable.Leave();

        Assert.That(_store.AutoSteer.MotorDriver, Is.EqualTo(1));
    }

    [Test]
    public async Task MotorDriverStep_Validation_AlwaysPasses()
    {
        var step = new MotorDriverStepViewModel(_configService);
        step.MotorDriver = 0;
        Assert.That(await step.ValidateAsync(), Is.True);

        step.MotorDriver = 1;
        Assert.That(await step.ValidateAsync(), Is.True);
    }

    [Test]
    public void MotorDriverStep_SelectionHelpers()
    {
        var step = new MotorDriverStepViewModel(_configService);

        step.SelectIBT2();
        Assert.That(step.IsIBT2Selected, Is.True);
        Assert.That(step.IsCytronSelected, Is.False);

        step.SelectCytron();
        Assert.That(step.IsCytronSelected, Is.True);
        Assert.That(step.IsIBT2Selected, Is.False);
    }

    [Test]
    public void MotorDriverStep_CanSkip_IsFalse()
    {
        var step = new MotorDriverStepViewModel(_configService);
        Assert.That(step.CanSkip, Is.False);
    }

    // =========================================================================
    // ADConverterStepViewModel
    // =========================================================================

    [Test]
    public void ADConverterStep_OnEntering_LoadsFromConfig()
    {
        _store.AutoSteer.AdConverter = 1;
        var testable = new TestableStep<ADConverterStepViewModel>(
            new ADConverterStepViewModel(_configService));

        testable.Enter();

        Assert.That(testable.Step.AdConverter, Is.EqualTo(1));
    }

    [Test]
    public void ADConverterStep_OnLeaving_SavesToConfig()
    {
        var testable = new TestableStep<ADConverterStepViewModel>(
            new ADConverterStepViewModel(_configService));
        testable.Enter();
        testable.Step.AdConverter = 1;

        testable.Leave();

        Assert.That(_store.AutoSteer.AdConverter, Is.EqualTo(1));
    }

    [Test]
    public async Task ADConverterStep_Validation_AlwaysPasses()
    {
        var step = new ADConverterStepViewModel(_configService);
        Assert.That(await step.ValidateAsync(), Is.True);
    }

    [Test]
    public void ADConverterStep_SelectionHelpers()
    {
        var step = new ADConverterStepViewModel(_configService);

        step.SelectDifferential();
        Assert.That(step.IsDifferentialSelected, Is.True);

        step.SelectSingle();
        Assert.That(step.IsSingleSelected, Is.True);
    }

    [Test]
    public void ADConverterStep_CanSkip_IsTrue()
    {
        var step = new ADConverterStepViewModel(_configService);
        Assert.That(step.CanSkip, Is.True);
    }

    // =========================================================================
    // InvertSettingsStepViewModel
    // =========================================================================

    [Test]
    public void InvertSettingsStep_OnEntering_LoadsFromConfig()
    {
        _store.AutoSteer.InvertWas = true;
        _store.AutoSteer.InvertMotor = true;
        _store.AutoSteer.InvertRelays = false;
        var testable = new TestableStep<InvertSettingsStepViewModel>(
            new InvertSettingsStepViewModel(_configService));

        testable.Enter();

        Assert.That(testable.Step.InvertWas, Is.True);
        Assert.That(testable.Step.InvertMotor, Is.True);
        Assert.That(testable.Step.InvertRelays, Is.False);
    }

    [Test]
    public void InvertSettingsStep_OnLeaving_SavesToConfig()
    {
        var testable = new TestableStep<InvertSettingsStepViewModel>(
            new InvertSettingsStepViewModel(_configService));
        testable.Enter();
        testable.Step.InvertWas = true;
        testable.Step.InvertMotor = false;
        testable.Step.InvertRelays = true;

        testable.Leave();

        Assert.That(_store.AutoSteer.InvertWas, Is.True);
        Assert.That(_store.AutoSteer.InvertMotor, Is.False);
        Assert.That(_store.AutoSteer.InvertRelays, Is.True);
    }

    [Test]
    public async Task InvertSettingsStep_Validation_AlwaysPasses()
    {
        var step = new InvertSettingsStepViewModel(_configService);
        Assert.That(await step.ValidateAsync(), Is.True);
    }

    [Test]
    public void InvertSettingsStep_CanSkip_IsTrue()
    {
        var step = new InvertSettingsStepViewModel(_configService);
        Assert.That(step.CanSkip, Is.True);
    }

    // =========================================================================
    // DanfossStepViewModel
    // =========================================================================

    [Test]
    public void DanfossStep_OnEntering_LoadsFromConfig()
    {
        _store.AutoSteer.DanfossEnabled = true;
        var testable = new TestableStep<DanfossStepViewModel>(
            new DanfossStepViewModel(_configService));

        testable.Enter();

        Assert.That(testable.Step.DanfossEnabled, Is.True);
    }

    [Test]
    public void DanfossStep_OnLeaving_SavesToConfig()
    {
        var testable = new TestableStep<DanfossStepViewModel>(
            new DanfossStepViewModel(_configService));
        testable.Enter();
        testable.Step.DanfossEnabled = true;

        testable.Leave();

        Assert.That(_store.AutoSteer.DanfossEnabled, Is.True);
    }

    [Test]
    public async Task DanfossStep_Validation_AlwaysPasses()
    {
        var step = new DanfossStepViewModel(_configService);
        Assert.That(await step.ValidateAsync(), Is.True);
    }

    [Test]
    public void DanfossStep_CanSkip_IsTrue()
    {
        var step = new DanfossStepViewModel(_configService);
        Assert.That(step.CanSkip, Is.True);
    }

    // =========================================================================
    // WasCalibrationStepViewModel
    // =========================================================================

    [Test]
    public void WasCalibrationStep_OnEntering_LoadsFromConfig()
    {
        _store.AutoSteer.WasOffset = 123;
        _store.AutoSteer.CountsPerDegree = 80;
        _store.AutoSteer.MaxSteerAngle = 40;
        var testable = new TestableStep<WasCalibrationStepViewModel>(
            new WasCalibrationStepViewModel(_configService));

        testable.Enter();

        Assert.That(testable.Step.WasOffset, Is.EqualTo(123));
        Assert.That(testable.Step.CountsPerDegree, Is.EqualTo(80));
        Assert.That(testable.Step.MaxSteerAngle, Is.EqualTo(40));
    }

    [Test]
    public void WasCalibrationStep_OnLeaving_SavesToConfig()
    {
        var testable = new TestableStep<WasCalibrationStepViewModel>(
            new WasCalibrationStepViewModel(_configService));
        testable.Enter();
        testable.Step.WasOffset = 50;
        testable.Step.CountsPerDegree = 120;
        testable.Step.MaxSteerAngle = 60;

        testable.Leave();

        Assert.That(_store.AutoSteer.WasOffset, Is.EqualTo(50));
        Assert.That(_store.AutoSteer.CountsPerDegree, Is.EqualTo(120));
        Assert.That(_store.AutoSteer.MaxSteerAngle, Is.EqualTo(60));
    }

    [Test]
    public async Task WasCalibrationStep_Validation_ValidValues()
    {
        var step = new WasCalibrationStepViewModel(_configService);
        step.CountsPerDegree = 100;
        step.MaxSteerAngle = 45;
        Assert.That(await step.ValidateAsync(), Is.True);
    }

    [Test]
    public async Task WasCalibrationStep_Validation_CpdTooLow()
    {
        var step = new WasCalibrationStepViewModel(_configService);
        step.CountsPerDegree = 0;
        step.MaxSteerAngle = 45;
        Assert.That(await step.ValidateAsync(), Is.False);
        Assert.That(step.ValidationMessage, Does.Contain("Counts Per Degree"));
    }

    [Test]
    public async Task WasCalibrationStep_Validation_CpdTooHigh()
    {
        var step = new WasCalibrationStepViewModel(_configService);
        step.CountsPerDegree = 256;
        step.MaxSteerAngle = 45;
        Assert.That(await step.ValidateAsync(), Is.False);
    }

    [Test]
    public async Task WasCalibrationStep_Validation_CpdBoundaryLow()
    {
        var step = new WasCalibrationStepViewModel(_configService);
        step.CountsPerDegree = 1;
        step.MaxSteerAngle = 45;
        Assert.That(await step.ValidateAsync(), Is.True);
    }

    [Test]
    public async Task WasCalibrationStep_Validation_CpdBoundaryHigh()
    {
        var step = new WasCalibrationStepViewModel(_configService);
        step.CountsPerDegree = 255;
        step.MaxSteerAngle = 45;
        Assert.That(await step.ValidateAsync(), Is.True);
    }

    [Test]
    public async Task WasCalibrationStep_Validation_MaxSteerTooLow()
    {
        var step = new WasCalibrationStepViewModel(_configService);
        step.CountsPerDegree = 100;
        step.MaxSteerAngle = 9;
        Assert.That(await step.ValidateAsync(), Is.False);
        Assert.That(step.ValidationMessage, Does.Contain("Max Steer Angle"));
    }

    [Test]
    public async Task WasCalibrationStep_Validation_MaxSteerTooHigh()
    {
        var step = new WasCalibrationStepViewModel(_configService);
        step.CountsPerDegree = 100;
        step.MaxSteerAngle = 91;
        Assert.That(await step.ValidateAsync(), Is.False);
    }

    [Test]
    public async Task WasCalibrationStep_Validation_MaxSteerBoundaryLow()
    {
        var step = new WasCalibrationStepViewModel(_configService);
        step.CountsPerDegree = 100;
        step.MaxSteerAngle = 10;
        Assert.That(await step.ValidateAsync(), Is.True);
    }

    [Test]
    public async Task WasCalibrationStep_Validation_MaxSteerBoundaryHigh()
    {
        var step = new WasCalibrationStepViewModel(_configService);
        step.CountsPerDegree = 100;
        step.MaxSteerAngle = 90;
        Assert.That(await step.ValidateAsync(), Is.True);
    }

    [Test]
    public void WasCalibrationStep_CanSkip_IsFalse()
    {
        var step = new WasCalibrationStepViewModel(_configService);
        Assert.That(step.CanSkip, Is.False);
    }

    // =========================================================================
    // SteeringGainsStepViewModel
    // =========================================================================

    [Test]
    public void SteeringGainsStep_OnEntering_LoadsFromConfig()
    {
        _store.AutoSteer.ProportionalGain = 25;
        _store.AutoSteer.IntegralGain = 0.5;
        var testable = new TestableStep<SteeringGainsStepViewModel>(
            new SteeringGainsStepViewModel(_configService));

        testable.Enter();

        Assert.That(testable.Step.ProportionalGain, Is.EqualTo(25));
        Assert.That(testable.Step.IntegralGain, Is.EqualTo(0.5));
    }

    [Test]
    public void SteeringGainsStep_OnLeaving_SavesToConfig()
    {
        var testable = new TestableStep<SteeringGainsStepViewModel>(
            new SteeringGainsStepViewModel(_configService));
        testable.Enter();
        testable.Step.ProportionalGain = 30;
        testable.Step.IntegralGain = 0.3;

        testable.Leave();

        Assert.That(_store.AutoSteer.ProportionalGain, Is.EqualTo(30));
        Assert.That(_store.AutoSteer.IntegralGain, Is.EqualTo(0.3));
    }

    [Test]
    public async Task SteeringGainsStep_Validation_ValidValues()
    {
        var step = new SteeringGainsStepViewModel(_configService);
        step.ProportionalGain = 10;
        step.IntegralGain = 0.0;
        Assert.That(await step.ValidateAsync(), Is.True);
    }

    [Test]
    public async Task SteeringGainsStep_Validation_KpTooLow()
    {
        var step = new SteeringGainsStepViewModel(_configService);
        step.ProportionalGain = 0;
        step.IntegralGain = 0.0;
        Assert.That(await step.ValidateAsync(), Is.False);
        Assert.That(step.ValidationMessage, Does.Contain("Proportional Gain"));
    }

    [Test]
    public async Task SteeringGainsStep_Validation_KpTooHigh()
    {
        var step = new SteeringGainsStepViewModel(_configService);
        step.ProportionalGain = 101;
        step.IntegralGain = 0.0;
        Assert.That(await step.ValidateAsync(), Is.False);
    }

    [Test]
    public async Task SteeringGainsStep_Validation_KpBoundaryLow()
    {
        var step = new SteeringGainsStepViewModel(_configService);
        step.ProportionalGain = 1;
        step.IntegralGain = 0.0;
        Assert.That(await step.ValidateAsync(), Is.True);
    }

    [Test]
    public async Task SteeringGainsStep_Validation_KpBoundaryHigh()
    {
        var step = new SteeringGainsStepViewModel(_configService);
        step.ProportionalGain = 100;
        step.IntegralGain = 0.0;
        Assert.That(await step.ValidateAsync(), Is.True);
    }

    [Test]
    public async Task SteeringGainsStep_Validation_KiTooLow()
    {
        var step = new SteeringGainsStepViewModel(_configService);
        step.ProportionalGain = 10;
        step.IntegralGain = -0.1;
        Assert.That(await step.ValidateAsync(), Is.False);
        Assert.That(step.ValidationMessage, Does.Contain("Integral Gain"));
    }

    [Test]
    public async Task SteeringGainsStep_Validation_KiTooHigh()
    {
        var step = new SteeringGainsStepViewModel(_configService);
        step.ProportionalGain = 10;
        step.IntegralGain = 1.1;
        Assert.That(await step.ValidateAsync(), Is.False);
    }

    [Test]
    public async Task SteeringGainsStep_Validation_KiBoundaryLow()
    {
        var step = new SteeringGainsStepViewModel(_configService);
        step.ProportionalGain = 10;
        step.IntegralGain = 0.0;
        Assert.That(await step.ValidateAsync(), Is.True);
    }

    [Test]
    public async Task SteeringGainsStep_Validation_KiBoundaryHigh()
    {
        var step = new SteeringGainsStepViewModel(_configService);
        step.ProportionalGain = 10;
        step.IntegralGain = 1.0;
        Assert.That(await step.ValidateAsync(), Is.True);
    }

    [Test]
    public void SteeringGainsStep_CanSkip_IsFalse()
    {
        var step = new SteeringGainsStepViewModel(_configService);
        Assert.That(step.CanSkip, Is.False);
    }

    // =========================================================================
    // PwmCalibrationStepViewModel
    // =========================================================================

    [Test]
    public void PwmCalibrationStep_OnEntering_LoadsFromConfig()
    {
        _store.AutoSteer.MaxPwm = 200;
        _store.AutoSteer.MinPwm = 10;
        var testable = new TestableStep<PwmCalibrationStepViewModel>(
            new PwmCalibrationStepViewModel(_configService));

        testable.Enter();

        Assert.That(testable.Step.MaxPwm, Is.EqualTo(200));
        Assert.That(testable.Step.MinPwm, Is.EqualTo(10));
    }

    [Test]
    public void PwmCalibrationStep_OnLeaving_SavesToConfig()
    {
        var testable = new TestableStep<PwmCalibrationStepViewModel>(
            new PwmCalibrationStepViewModel(_configService));
        testable.Enter();
        testable.Step.MaxPwm = 180;
        testable.Step.MinPwm = 15;

        testable.Leave();

        Assert.That(_store.AutoSteer.MaxPwm, Is.EqualTo(180));
        Assert.That(_store.AutoSteer.MinPwm, Is.EqualTo(15));
    }

    [Test]
    public async Task PwmCalibrationStep_Validation_ValidValues()
    {
        var step = new PwmCalibrationStepViewModel(_configService);
        step.MaxPwm = 200;
        step.MinPwm = 10;
        Assert.That(await step.ValidateAsync(), Is.True);
    }

    [Test]
    public async Task PwmCalibrationStep_Validation_MaxPwmTooLow()
    {
        var step = new PwmCalibrationStepViewModel(_configService);
        step.MaxPwm = 49;
        step.MinPwm = 10;
        Assert.That(await step.ValidateAsync(), Is.False);
        Assert.That(step.ValidationMessage, Does.Contain("Max PWM"));
    }

    [Test]
    public async Task PwmCalibrationStep_Validation_MaxPwmTooHigh()
    {
        var step = new PwmCalibrationStepViewModel(_configService);
        step.MaxPwm = 256;
        step.MinPwm = 10;
        Assert.That(await step.ValidateAsync(), Is.False);
    }

    [Test]
    public async Task PwmCalibrationStep_Validation_MinPwmTooLow()
    {
        var step = new PwmCalibrationStepViewModel(_configService);
        step.MaxPwm = 200;
        step.MinPwm = 0;
        Assert.That(await step.ValidateAsync(), Is.False);
        Assert.That(step.ValidationMessage, Does.Contain("Min PWM"));
    }

    [Test]
    public async Task PwmCalibrationStep_Validation_MinPwmTooHigh()
    {
        var step = new PwmCalibrationStepViewModel(_configService);
        step.MaxPwm = 200;
        step.MinPwm = 51;
        Assert.That(await step.ValidateAsync(), Is.False);
    }

    [Test]
    public async Task PwmCalibrationStep_Validation_MaxNotGreaterThanMin()
    {
        var step = new PwmCalibrationStepViewModel(_configService);
        step.MaxPwm = 50;
        step.MinPwm = 50;
        Assert.That(await step.ValidateAsync(), Is.False);
        Assert.That(step.ValidationMessage, Does.Contain("greater than"));
    }

    [Test]
    public async Task PwmCalibrationStep_Validation_BoundaryValues()
    {
        var step = new PwmCalibrationStepViewModel(_configService);
        step.MaxPwm = 255;
        step.MinPwm = 1;
        Assert.That(await step.ValidateAsync(), Is.True);

        step.MaxPwm = 50;
        step.MinPwm = 1;
        Assert.That(await step.ValidateAsync(), Is.True);
    }

    [Test]
    public void PwmCalibrationStep_CanSkip_IsTrue()
    {
        var step = new PwmCalibrationStepViewModel(_configService);
        Assert.That(step.CanSkip, Is.True);
    }

    // =========================================================================
    // AlgorithmSelectionStepViewModel
    // =========================================================================

    [Test]
    public void AlgorithmSelectionStep_OnEntering_LoadsFromConfig()
    {
        _store.AutoSteer.IsStanleyMode = true;
        _store.AutoSteer.SteerResponseHold = 5.0;
        _store.AutoSteer.StanleyAggressiveness = 2.5;
        var testable = new TestableStep<AlgorithmSelectionStepViewModel>(
            new AlgorithmSelectionStepViewModel(_configService));

        testable.Enter();

        Assert.That(testable.Step.IsStanleyMode, Is.True);
        Assert.That(testable.Step.SteerResponseHold, Is.EqualTo(5.0));
        Assert.That(testable.Step.StanleyAggressiveness, Is.EqualTo(2.5));
    }

    [Test]
    public void AlgorithmSelectionStep_OnLeaving_SavesToConfig()
    {
        var testable = new TestableStep<AlgorithmSelectionStepViewModel>(
            new AlgorithmSelectionStepViewModel(_configService));
        testable.Enter();
        testable.Step.IsStanleyMode = true;
        testable.Step.SteerResponseHold = 7.0;
        testable.Step.StanleyAggressiveness = 4.0;

        testable.Leave();

        Assert.That(_store.AutoSteer.IsStanleyMode, Is.True);
        Assert.That(_store.AutoSteer.SteerResponseHold, Is.EqualTo(7.0));
        Assert.That(_store.AutoSteer.StanleyAggressiveness, Is.EqualTo(4.0));
    }

    [Test]
    public async Task AlgorithmSelectionStep_Validation_ValidValues()
    {
        var step = new AlgorithmSelectionStepViewModel(_configService);
        step.SteerResponseHold = 3.0;
        step.StanleyAggressiveness = 1.0;
        Assert.That(await step.ValidateAsync(), Is.True);
    }

    [Test]
    public async Task AlgorithmSelectionStep_Validation_SteerResponseHoldTooLow()
    {
        var step = new AlgorithmSelectionStepViewModel(_configService);
        step.SteerResponseHold = 0.5;
        step.StanleyAggressiveness = 1.0;
        Assert.That(await step.ValidateAsync(), Is.False);
        Assert.That(step.ValidationMessage, Does.Contain("Steer Response Hold"));
    }

    [Test]
    public async Task AlgorithmSelectionStep_Validation_SteerResponseHoldTooHigh()
    {
        var step = new AlgorithmSelectionStepViewModel(_configService);
        step.SteerResponseHold = 11;
        step.StanleyAggressiveness = 1.0;
        Assert.That(await step.ValidateAsync(), Is.False);
    }

    [Test]
    public async Task AlgorithmSelectionStep_Validation_SteerResponseHoldBoundary()
    {
        var step = new AlgorithmSelectionStepViewModel(_configService);
        step.StanleyAggressiveness = 1.0;

        step.SteerResponseHold = 1.0;
        Assert.That(await step.ValidateAsync(), Is.True);

        step.SteerResponseHold = 10.0;
        Assert.That(await step.ValidateAsync(), Is.True);
    }

    [Test]
    public async Task AlgorithmSelectionStep_Validation_StanleyAggressivenessTooLow()
    {
        var step = new AlgorithmSelectionStepViewModel(_configService);
        step.SteerResponseHold = 3.0;
        step.StanleyAggressiveness = -1;
        Assert.That(await step.ValidateAsync(), Is.False);
        Assert.That(step.ValidationMessage, Does.Contain("Stanley Aggressiveness"));
    }

    [Test]
    public async Task AlgorithmSelectionStep_Validation_StanleyAggressivenessTooHigh()
    {
        var step = new AlgorithmSelectionStepViewModel(_configService);
        step.SteerResponseHold = 3.0;
        step.StanleyAggressiveness = 11;
        Assert.That(await step.ValidateAsync(), Is.False);
    }

    [Test]
    public async Task AlgorithmSelectionStep_Validation_StanleyAggressivenessBoundary()
    {
        var step = new AlgorithmSelectionStepViewModel(_configService);
        step.SteerResponseHold = 3.0;

        step.StanleyAggressiveness = 0;
        Assert.That(await step.ValidateAsync(), Is.True);

        step.StanleyAggressiveness = 10;
        Assert.That(await step.ValidateAsync(), Is.True);
    }

    [Test]
    public void AlgorithmSelectionStep_CanSkip_IsTrue()
    {
        var step = new AlgorithmSelectionStepViewModel(_configService);
        Assert.That(step.CanSkip, Is.True);
    }

    // =========================================================================
    // SpeedLimitsStepViewModel
    // =========================================================================

    [Test]
    public void SpeedLimitsStep_OnEntering_LoadsFromConfig()
    {
        _store.AutoSteer.MinSteerSpeed = 2.0;
        _store.AutoSteer.MaxSteerSpeed = 20.0;
        var testable = new TestableStep<SpeedLimitsStepViewModel>(
            new SpeedLimitsStepViewModel(_configService));

        testable.Enter();

        Assert.That(testable.Step.MinSteerSpeed, Is.EqualTo(2.0));
        Assert.That(testable.Step.MaxSteerSpeed, Is.EqualTo(20.0));
    }

    [Test]
    public void SpeedLimitsStep_OnLeaving_SavesToConfig()
    {
        var testable = new TestableStep<SpeedLimitsStepViewModel>(
            new SpeedLimitsStepViewModel(_configService));
        testable.Enter();
        testable.Step.MinSteerSpeed = 1.0;
        testable.Step.MaxSteerSpeed = 25.0;

        testable.Leave();

        Assert.That(_store.AutoSteer.MinSteerSpeed, Is.EqualTo(1.0));
        Assert.That(_store.AutoSteer.MaxSteerSpeed, Is.EqualTo(25.0));
    }

    [Test]
    public async Task SpeedLimitsStep_Validation_ValidValues()
    {
        var step = new SpeedLimitsStepViewModel(_configService);
        step.MinSteerSpeed = 1.0;
        step.MaxSteerSpeed = 15.0;
        Assert.That(await step.ValidateAsync(), Is.True);
    }

    [Test]
    public async Task SpeedLimitsStep_Validation_MinNegative()
    {
        var step = new SpeedLimitsStepViewModel(_configService);
        step.MinSteerSpeed = -1;
        step.MaxSteerSpeed = 15.0;
        Assert.That(await step.ValidateAsync(), Is.False);
        Assert.That(step.ValidationMessage, Does.Contain("Min Steer Speed"));
    }

    [Test]
    public async Task SpeedLimitsStep_Validation_MaxZero()
    {
        var step = new SpeedLimitsStepViewModel(_configService);
        step.MinSteerSpeed = 0;
        step.MaxSteerSpeed = 0;
        Assert.That(await step.ValidateAsync(), Is.False);
        Assert.That(step.ValidationMessage, Does.Contain("Max Steer Speed"));
    }

    [Test]
    public async Task SpeedLimitsStep_Validation_MaxNotGreaterThanMin()
    {
        var step = new SpeedLimitsStepViewModel(_configService);
        step.MinSteerSpeed = 10;
        step.MaxSteerSpeed = 10;
        Assert.That(await step.ValidateAsync(), Is.False);
        Assert.That(step.ValidationMessage, Does.Contain("greater than"));
    }

    [Test]
    public async Task SpeedLimitsStep_Validation_MaxLessThanMin()
    {
        var step = new SpeedLimitsStepViewModel(_configService);
        step.MinSteerSpeed = 10;
        step.MaxSteerSpeed = 5;
        Assert.That(await step.ValidateAsync(), Is.False);
    }

    [Test]
    public async Task SpeedLimitsStep_Validation_ZeroMinIsValid()
    {
        var step = new SpeedLimitsStepViewModel(_configService);
        step.MinSteerSpeed = 0;
        step.MaxSteerSpeed = 15;
        Assert.That(await step.ValidateAsync(), Is.True);
    }

    [Test]
    public void SpeedLimitsStep_CanSkip_IsTrue()
    {
        var step = new SpeedLimitsStepViewModel(_configService);
        Assert.That(step.CanSkip, Is.True);
    }

    // =========================================================================
    // SensorsStepViewModel
    // =========================================================================

    [Test]
    public void SensorsStep_OnEntering_LoadsFromConfig()
    {
        _store.AutoSteer.TurnSensorEnabled = true;
        _store.AutoSteer.PressureSensorEnabled = false;
        _store.AutoSteer.CurrentSensorEnabled = true;
        var testable = new TestableStep<SensorsStepViewModel>(
            new SensorsStepViewModel(_configService));

        testable.Enter();

        Assert.That(testable.Step.TurnSensorEnabled, Is.True);
        Assert.That(testable.Step.PressureSensorEnabled, Is.False);
        Assert.That(testable.Step.CurrentSensorEnabled, Is.True);
    }

    [Test]
    public void SensorsStep_OnLeaving_SavesToConfig()
    {
        var testable = new TestableStep<SensorsStepViewModel>(
            new SensorsStepViewModel(_configService));
        testable.Enter();
        testable.Step.TurnSensorEnabled = false;
        testable.Step.PressureSensorEnabled = true;
        testable.Step.CurrentSensorEnabled = true;

        testable.Leave();

        Assert.That(_store.AutoSteer.TurnSensorEnabled, Is.False);
        Assert.That(_store.AutoSteer.PressureSensorEnabled, Is.True);
        Assert.That(_store.AutoSteer.CurrentSensorEnabled, Is.True);
    }

    [Test]
    public async Task SensorsStep_Validation_AlwaysPasses()
    {
        var step = new SensorsStepViewModel(_configService);
        Assert.That(await step.ValidateAsync(), Is.True);
    }

    [Test]
    public void SensorsStep_CanSkip_IsTrue()
    {
        var step = new SensorsStepViewModel(_configService);
        Assert.That(step.CanSkip, Is.True);
    }

    // =========================================================================
    // FinishStepViewModel
    // =========================================================================

    [Test]
    public void FinishStep_HasCorrectTitle()
    {
        var step = new FinishStepViewModel();
        Assert.That(step.Title, Is.EqualTo("Setup Complete"));
    }

    [Test]
    public void FinishStep_CanSkip_IsFalse()
    {
        var step = new FinishStepViewModel();
        Assert.That(step.CanSkip, Is.False);
    }

    [Test]
    public async Task FinishStep_Validation_AlwaysPasses()
    {
        var step = new FinishStepViewModel();
        Assert.That(await step.ValidateAsync(), Is.True);
    }

    // =========================================================================
    // Cross-cutting: Validation clears previous errors
    // =========================================================================

    [Test]
    public async Task ValidationError_ClearedOnSubsequentValidPass()
    {
        var step = new WheelbaseStepViewModel(_configService);

        // First fail
        step.Wheelbase = 0.1;
        Assert.That(await step.ValidateAsync(), Is.False);
        Assert.That(step.HasValidationError, Is.True);

        // Then pass - error should be cleared
        step.Wheelbase = 2.5;
        Assert.That(await step.ValidateAsync(), Is.True);
        Assert.That(step.HasValidationError, Is.False);
        Assert.That(step.ValidationMessage, Is.Null);
    }
}

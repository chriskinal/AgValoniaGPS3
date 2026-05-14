using AgValoniaGPS.Models;
using AgValoniaGPS.Models.Configuration;
using AgValoniaGPS.Services.Interfaces;
using AgValoniaGPS.ViewModels.Wizards;
using AgValoniaGPS.ViewModels.Wizards.SteerWizard;
using CommunityToolkit.Mvvm.Input;
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
    // VehicleTypeStepViewModel
    // =========================================================================

    [Test]
    public void VehicleTypeStep_HasCorrectTitle()
    {
        var step = new VehicleTypeStepViewModel(_configService);
        Assert.That(step.Title, Is.EqualTo("Vehicle Type"));
    }

    [Test]
    public void VehicleTypeStep_OnEntering_LoadsFromConfig()
    {
        _store.Vehicle.Type = VehicleType.Harvester;
        var testable = new TestableStep<VehicleTypeStepViewModel>(
            new VehicleTypeStepViewModel(_configService));

        testable.Enter();

        Assert.That(testable.Step.VehicleType, Is.EqualTo(VehicleType.Harvester));
    }

    [Test]
    public void VehicleTypeStep_OnLeaving_SavesToConfig()
    {
        var testable = new TestableStep<VehicleTypeStepViewModel>(
            new VehicleTypeStepViewModel(_configService));
        testable.Enter();
        testable.Step.VehicleType = VehicleType.FourWD;

        testable.Leave();

        Assert.That(_store.Vehicle.Type, Is.EqualTo(VehicleType.FourWD));
    }

    [Test]
    public async Task VehicleTypeStep_Validation_AlwaysPasses()
    {
        var step = new VehicleTypeStepViewModel(_configService);
        step.VehicleType = VehicleType.Tractor;
        Assert.That(await step.ValidateAsync(), Is.True);

        step.VehicleType = VehicleType.Harvester;
        Assert.That(await step.ValidateAsync(), Is.True);

        step.VehicleType = VehicleType.FourWD;
        Assert.That(await step.ValidateAsync(), Is.True);
    }

    [Test]
    public void VehicleTypeStep_SelectionHelpers()
    {
        var step = new VehicleTypeStepViewModel(_configService);

        step.SelectTractor();
        Assert.That(step.IsTractorSelected, Is.True);
        Assert.That(step.IsHarvesterSelected, Is.False);
        Assert.That(step.IsFourWDSelected, Is.False);

        step.SelectHarvester();
        Assert.That(step.IsHarvesterSelected, Is.True);
        Assert.That(step.IsTractorSelected, Is.False);
        Assert.That(step.IsFourWDSelected, Is.False);

        step.SelectFourWD();
        Assert.That(step.IsFourWDSelected, Is.True);
        Assert.That(step.IsTractorSelected, Is.False);
        Assert.That(step.IsHarvesterSelected, Is.False);
    }

    [Test]
    public void VehicleTypeStep_CanSkip_IsFalse()
    {
        var step = new VehicleTypeStepViewModel(_configService);
        Assert.That(step.CanSkip, Is.False);
    }

    // =========================================================================
    // VehicleDimensionsStepViewModel
    // =========================================================================

    [Test]
    public void VehicleDimensionsStep_OnEntering_LoadsFromConfig()
    {
        _store.Vehicle.Wheelbase = 3.5;
        _store.Vehicle.TrackWidth = 2.0;
        var testable = new TestableStep<VehicleDimensionsStepViewModel>(
            new VehicleDimensionsStepViewModel(_configService));

        testable.Enter();

        Assert.That(testable.Step.Wheelbase, Is.EqualTo(3.5));
        Assert.That(testable.Step.TrackWidth, Is.EqualTo(2.0));
    }

    [Test]
    public void VehicleDimensionsStep_OnLeaving_SavesToConfig()
    {
        var testable = new TestableStep<VehicleDimensionsStepViewModel>(
            new VehicleDimensionsStepViewModel(_configService));
        testable.Enter();
        testable.Step.Wheelbase = 4.2;
        testable.Step.TrackWidth = 1.8;

        testable.Leave();

        Assert.That(_store.Vehicle.Wheelbase, Is.EqualTo(4.2));
        Assert.That(_store.Vehicle.TrackWidth, Is.EqualTo(1.8));
    }

    [Test]
    public async Task VehicleDimensionsStep_Validation_ValidValues()
    {
        var step = new VehicleDimensionsStepViewModel(_configService);
        step.Wheelbase = 2.5;
        step.TrackWidth = 2.0;

        Assert.That(await step.ValidateAsync(), Is.True);
        Assert.That(step.ValidationMessage, Is.Null);
    }

    [Test]
    public async Task VehicleDimensionsStep_Validation_WheelbaseTooSmall()
    {
        var step = new VehicleDimensionsStepViewModel(_configService);
        step.Wheelbase = 0.3;
        step.TrackWidth = 2.0;

        Assert.That(await step.ValidateAsync(), Is.False);
        Assert.That(step.ValidationMessage, Does.Contain("at least 0.5"));
    }

    [Test]
    public async Task VehicleDimensionsStep_Validation_WheelbaseTooLarge()
    {
        var step = new VehicleDimensionsStepViewModel(_configService);
        step.Wheelbase = 16;
        step.TrackWidth = 2.0;

        Assert.That(await step.ValidateAsync(), Is.False);
        Assert.That(step.ValidationMessage, Does.Contain("too large"));
    }

    [Test]
    public async Task VehicleDimensionsStep_Validation_TrackWidthTooSmall()
    {
        var step = new VehicleDimensionsStepViewModel(_configService);
        step.Wheelbase = 2.5;
        step.TrackWidth = 0.3;

        Assert.That(await step.ValidateAsync(), Is.False);
        Assert.That(step.ValidationMessage, Does.Contain("at least 0.5"));
    }

    [Test]
    public async Task VehicleDimensionsStep_Validation_TrackWidthTooLarge()
    {
        var step = new VehicleDimensionsStepViewModel(_configService);
        step.Wheelbase = 2.5;
        step.TrackWidth = 11;

        Assert.That(await step.ValidateAsync(), Is.False);
        Assert.That(step.ValidationMessage, Does.Contain("too large"));
    }

    [Test]
    public async Task VehicleDimensionsStep_Validation_BoundaryValues()
    {
        var step = new VehicleDimensionsStepViewModel(_configService);
        step.Wheelbase = 0.5;
        step.TrackWidth = 0.5;
        Assert.That(await step.ValidateAsync(), Is.True);

        step.Wheelbase = 15;
        step.TrackWidth = 10;
        Assert.That(await step.ValidateAsync(), Is.True);
    }

    [Test]
    public void VehicleDimensionsStep_CanSkip_IsFalse()
    {
        var step = new VehicleDimensionsStepViewModel(_configService);
        Assert.That(step.CanSkip, Is.False);
    }

    // =========================================================================
    // AntennaSetupStepViewModel
    // =========================================================================

    [Test]
    public void AntennaSetupStep_OnEntering_LoadsFromConfig()
    {
        _store.Vehicle.AntennaPivot = 1.2;
        _store.Vehicle.AntennaHeight = 2.8;
        _store.Vehicle.AntennaOffset = -0.3;
        var testable = new TestableStep<AntennaSetupStepViewModel>(
            new AntennaSetupStepViewModel(_configService));

        testable.Enter();

        Assert.That(testable.Step.AntennaPivot, Is.EqualTo(1.2));
        Assert.That(testable.Step.AntennaHeight, Is.EqualTo(2.8));
        Assert.That(testable.Step.AntennaOffset, Is.EqualTo(-0.3));
    }

    [Test]
    public void AntennaSetupStep_OnLeaving_SavesToConfig()
    {
        var testable = new TestableStep<AntennaSetupStepViewModel>(
            new AntennaSetupStepViewModel(_configService));
        testable.Enter();
        testable.Step.AntennaPivot = -2.5;
        testable.Step.AntennaHeight = 3.1;
        testable.Step.AntennaOffset = 0.7;

        testable.Leave();

        Assert.That(_store.Vehicle.AntennaPivot, Is.EqualTo(-2.5));
        Assert.That(_store.Vehicle.AntennaHeight, Is.EqualTo(3.1));
        Assert.That(_store.Vehicle.AntennaOffset, Is.EqualTo(0.7));
    }

    [Test]
    public async Task AntennaSetupStep_Validation_ValidValues()
    {
        var step = new AntennaSetupStepViewModel(_configService);
        step.AntennaPivot = 3.0;
        step.AntennaHeight = 2.5;
        step.AntennaOffset = 0;
        Assert.That(await step.ValidateAsync(), Is.True);
    }

    [Test]
    public async Task AntennaSetupStep_Validation_PivotTooNegative()
    {
        var step = new AntennaSetupStepViewModel(_configService);
        step.AntennaPivot = -11;
        step.AntennaHeight = 2.5;
        step.AntennaOffset = 0;
        Assert.That(await step.ValidateAsync(), Is.False);
    }

    [Test]
    public async Task AntennaSetupStep_Validation_PivotTooLarge()
    {
        var step = new AntennaSetupStepViewModel(_configService);
        step.AntennaPivot = 16;
        step.AntennaHeight = 2.5;
        step.AntennaOffset = 0;
        Assert.That(await step.ValidateAsync(), Is.False);
    }

    [Test]
    public async Task AntennaSetupStep_Validation_HeightNegative()
    {
        var step = new AntennaSetupStepViewModel(_configService);
        step.AntennaPivot = 1.0;
        step.AntennaHeight = -1;
        step.AntennaOffset = 0;
        Assert.That(await step.ValidateAsync(), Is.False);
    }

    [Test]
    public async Task AntennaSetupStep_Validation_HeightTooLarge()
    {
        var step = new AntennaSetupStepViewModel(_configService);
        step.AntennaPivot = 1.0;
        step.AntennaHeight = 11;
        step.AntennaOffset = 0;
        Assert.That(await step.ValidateAsync(), Is.False);
    }

    [Test]
    public async Task AntennaSetupStep_Validation_OffsetTooNegative()
    {
        var step = new AntennaSetupStepViewModel(_configService);
        step.AntennaPivot = 1.0;
        step.AntennaHeight = 2.5;
        step.AntennaOffset = -6;
        Assert.That(await step.ValidateAsync(), Is.False);
    }

    [Test]
    public async Task AntennaSetupStep_Validation_OffsetTooLarge()
    {
        var step = new AntennaSetupStepViewModel(_configService);
        step.AntennaPivot = 1.0;
        step.AntennaHeight = 2.5;
        step.AntennaOffset = 6;
        Assert.That(await step.ValidateAsync(), Is.False);
    }

    [Test]
    public void AntennaSetupStep_OffsetDirectionProperties()
    {
        var step = new AntennaSetupStepViewModel(_configService);

        step.AntennaOffset = -0.5;
        Assert.That(step.IsLeft, Is.True);
        Assert.That(step.IsRight, Is.False);

        step.AntennaOffset = 0.5;
        Assert.That(step.IsRight, Is.True);
        Assert.That(step.IsLeft, Is.False);

        step.AntennaOffset = 0;
        Assert.That(step.IsCenter, Is.True);
    }

    // =========================================================================
    // HardwareConfigStepViewModel
    // =========================================================================

    [Test]
    public void HardwareConfigStep_OnEntering_LoadsFromConfig()
    {
        _store.AutoSteer.ExternalEnable = 2;
        _store.AutoSteer.MotorDriver = 1;
        _store.AutoSteer.AdConverter = 1;
        _store.AutoSteer.InvertRelays = false;
        _store.AutoSteer.DanfossEnabled = true;
        var testable = new TestableStep<HardwareConfigStepViewModel>(
            new HardwareConfigStepViewModel(_configService));

        testable.Enter();

        Assert.That(testable.Step.ExternalEnable, Is.EqualTo(2));
        Assert.That(testable.Step.MotorDriver, Is.EqualTo(1));
        Assert.That(testable.Step.AdConverter, Is.EqualTo(1));
        Assert.That(testable.Step.InvertRelays, Is.False);
        Assert.That(testable.Step.DanfossEnabled, Is.True);
    }

    [Test]
    public void HardwareConfigStep_OnLeaving_SavesToConfig()
    {
        var testable = new TestableStep<HardwareConfigStepViewModel>(
            new HardwareConfigStepViewModel(_configService));
        testable.Enter();
        testable.Step.ExternalEnable = 1;
        testable.Step.MotorDriver = 1;
        testable.Step.AdConverter = 1;
        testable.Step.InvertRelays = true;
        testable.Step.DanfossEnabled = true;

        testable.Leave();

        Assert.That(_store.AutoSteer.ExternalEnable, Is.EqualTo(1));
        Assert.That(_store.AutoSteer.MotorDriver, Is.EqualTo(1));
        Assert.That(_store.AutoSteer.AdConverter, Is.EqualTo(1));
        Assert.That(_store.AutoSteer.InvertRelays, Is.True);
        Assert.That(_store.AutoSteer.DanfossEnabled, Is.True);
    }

    [Test]
    public async Task HardwareConfigStep_Validation_AlwaysPasses()
    {
        var step = new HardwareConfigStepViewModel(_configService);
        Assert.That(await step.ValidateAsync(), Is.True);
    }

    [Test]
    public void HardwareConfigStep_SteerEnableSelectionHelpers()
    {
        var step = new HardwareConfigStepViewModel(_configService);

        step.SelectNone();
        Assert.That(step.IsNoneSelected, Is.True);
        Assert.That(step.IsSwitchSelected, Is.False);

        step.SelectSwitch();
        Assert.That(step.IsSwitchSelected, Is.True);

        step.SelectButton();
        Assert.That(step.IsButtonSelected, Is.True);
    }

    [Test]
    public void HardwareConfigStep_MotorDriverSelectionHelpers()
    {
        var step = new HardwareConfigStepViewModel(_configService);

        step.SelectIBT2();
        Assert.That(step.IsIBT2Selected, Is.True);
        Assert.That(step.IsCytronSelected, Is.False);

        step.SelectCytron();
        Assert.That(step.IsCytronSelected, Is.True);
        Assert.That(step.IsIBT2Selected, Is.False);
    }

    [Test]
    public void HardwareConfigStep_ADConverterSelectionHelpers()
    {
        var step = new HardwareConfigStepViewModel(_configService);

        step.SelectDifferential();
        Assert.That(step.IsDifferentialSelected, Is.True);

        step.SelectSingle();
        Assert.That(step.IsSingleSelected, Is.True);
    }

    // =========================================================================
    // WasCalibrationStepViewModel
    // =========================================================================

    [Test]
    public void WasCalibrationStep_OnEntering_LoadsFromConfig()
    {
        _store.AutoSteer.WasOffset = 123;
        _store.AutoSteer.InvertWas = true;
        var testable = new TestableStep<WasCalibrationStepViewModel>(
            new WasCalibrationStepViewModel(_configService));

        testable.Enter();

        Assert.That(testable.Step.WasOffset, Is.EqualTo(123));
        Assert.That(testable.Step.InvertWas, Is.True);
    }

    [Test]
    public void WasCalibrationStep_OnLeaving_SavesToConfig()
    {
        var testable = new TestableStep<WasCalibrationStepViewModel>(
            new WasCalibrationStepViewModel(_configService));
        testable.Enter();
        testable.Step.WasOffset = 50;
        testable.Step.InvertWas = true;

        testable.Leave();

        Assert.That(_store.AutoSteer.WasOffset, Is.EqualTo(50));
        Assert.That(_store.AutoSteer.InvertWas, Is.True);
    }

    [Test]
    public async Task WasCalibrationStep_Validation_AlwaysPasses()
    {
        var step = new WasCalibrationStepViewModel(_configService);
        Assert.That(await step.ValidateAsync(), Is.True);
    }

    [Test]
    public void WasCalibrationStep_CanSkip_IsFalse()
    {
        var step = new WasCalibrationStepViewModel(_configService);
        Assert.That(step.CanSkip, Is.False);
    }

    // =========================================================================
    // SteeringGainsStepViewModel (now includes algorithm selection)
    // =========================================================================

    [Test]
    public void SteeringGainsStep_OnEntering_LoadsFromConfig()
    {
        _store.AutoSteer.ProportionalGain = 25;
        _store.AutoSteer.IntegralGain = 0.5;
        _store.AutoSteer.IsStanleyMode = true;
        _store.AutoSteer.SteerResponseHold = 5.0;
        _store.AutoSteer.StanleyAggressiveness = 2.5;
        var testable = new TestableStep<SteeringGainsStepViewModel>(
            new SteeringGainsStepViewModel(_configService));

        testable.Enter();

        Assert.That(testable.Step.ProportionalGain, Is.EqualTo(25));
        Assert.That(testable.Step.IntegralGain, Is.EqualTo(0.5));
        Assert.That(testable.Step.IsStanleyMode, Is.True);
        Assert.That(testable.Step.SteerResponseHold, Is.EqualTo(5.0));
        Assert.That(testable.Step.StanleyAggressiveness, Is.EqualTo(2.5));
    }

    [Test]
    public void SteeringGainsStep_OnLeaving_SavesToConfig()
    {
        var testable = new TestableStep<SteeringGainsStepViewModel>(
            new SteeringGainsStepViewModel(_configService));
        testable.Enter();
        testable.Step.ProportionalGain = 30;
        testable.Step.IntegralGain = 0.3;
        testable.Step.IsStanleyMode = true;
        testable.Step.SteerResponseHold = 7.0;
        testable.Step.StanleyAggressiveness = 4.0;

        testable.Leave();

        Assert.That(_store.AutoSteer.ProportionalGain, Is.EqualTo(30));
        Assert.That(_store.AutoSteer.IntegralGain, Is.EqualTo(0.3));
        Assert.That(_store.AutoSteer.IsStanleyMode, Is.True);
        Assert.That(_store.AutoSteer.SteerResponseHold, Is.EqualTo(7.0));
        Assert.That(_store.AutoSteer.StanleyAggressiveness, Is.EqualTo(4.0));
    }

    [Test]
    public async Task SteeringGainsStep_Validation_ValidValues()
    {
        var step = new SteeringGainsStepViewModel(_configService);
        step.ProportionalGain = 10;
        step.IntegralGain = 0.0;
        step.SteerResponseHold = 3.0;
        step.StanleyAggressiveness = 1.0;
        Assert.That(await step.ValidateAsync(), Is.True);
    }

    [Test]
    public async Task SteeringGainsStep_Validation_KpTooLow()
    {
        var step = new SteeringGainsStepViewModel(_configService);
        step.ProportionalGain = 0;
        step.IntegralGain = 0.0;
        step.SteerResponseHold = 3.0;
        step.StanleyAggressiveness = 1.0;
        Assert.That(await step.ValidateAsync(), Is.False);
        Assert.That(step.ValidationMessage, Does.Contain("Proportional Gain"));
    }

    [Test]
    public async Task SteeringGainsStep_Validation_KpTooHigh()
    {
        var step = new SteeringGainsStepViewModel(_configService);
        step.ProportionalGain = 101;
        step.IntegralGain = 0.0;
        step.SteerResponseHold = 3.0;
        step.StanleyAggressiveness = 1.0;
        Assert.That(await step.ValidateAsync(), Is.False);
    }

    [Test]
    public async Task SteeringGainsStep_Validation_KpBoundaryLow()
    {
        var step = new SteeringGainsStepViewModel(_configService);
        step.ProportionalGain = 1;
        step.IntegralGain = 0.0;
        step.SteerResponseHold = 3.0;
        step.StanleyAggressiveness = 1.0;
        Assert.That(await step.ValidateAsync(), Is.True);
    }

    [Test]
    public async Task SteeringGainsStep_Validation_KpBoundaryHigh()
    {
        var step = new SteeringGainsStepViewModel(_configService);
        step.ProportionalGain = 100;
        step.IntegralGain = 0.0;
        step.SteerResponseHold = 3.0;
        step.StanleyAggressiveness = 1.0;
        Assert.That(await step.ValidateAsync(), Is.True);
    }

    [Test]
    public async Task SteeringGainsStep_Validation_KiTooLow()
    {
        var step = new SteeringGainsStepViewModel(_configService);
        step.ProportionalGain = 10;
        step.IntegralGain = -0.1;
        step.SteerResponseHold = 3.0;
        step.StanleyAggressiveness = 1.0;
        Assert.That(await step.ValidateAsync(), Is.False);
        Assert.That(step.ValidationMessage, Does.Contain("Integral Gain"));
    }

    [Test]
    public async Task SteeringGainsStep_Validation_KiTooHigh()
    {
        var step = new SteeringGainsStepViewModel(_configService);
        step.ProportionalGain = 10;
        step.IntegralGain = 1.1;
        step.SteerResponseHold = 3.0;
        step.StanleyAggressiveness = 1.0;
        Assert.That(await step.ValidateAsync(), Is.False);
    }

    [Test]
    public async Task SteeringGainsStep_Validation_KiBoundaryLow()
    {
        var step = new SteeringGainsStepViewModel(_configService);
        step.ProportionalGain = 10;
        step.IntegralGain = 0.0;
        step.SteerResponseHold = 3.0;
        step.StanleyAggressiveness = 1.0;
        Assert.That(await step.ValidateAsync(), Is.True);
    }

    [Test]
    public async Task SteeringGainsStep_Validation_KiBoundaryHigh()
    {
        var step = new SteeringGainsStepViewModel(_configService);
        step.ProportionalGain = 10;
        step.IntegralGain = 1.0;
        step.SteerResponseHold = 3.0;
        step.StanleyAggressiveness = 1.0;
        Assert.That(await step.ValidateAsync(), Is.True);
    }

    [Test]
    public async Task SteeringGainsStep_Validation_SteerResponseHoldTooLow()
    {
        var step = new SteeringGainsStepViewModel(_configService);
        step.ProportionalGain = 10;
        step.IntegralGain = 0.0;
        step.SteerResponseHold = 0.5;
        step.StanleyAggressiveness = 1.0;
        Assert.That(await step.ValidateAsync(), Is.False);
        Assert.That(step.ValidationMessage, Does.Contain("Steer Response Hold"));
    }

    [Test]
    public async Task SteeringGainsStep_Validation_SteerResponseHoldTooHigh()
    {
        var step = new SteeringGainsStepViewModel(_configService);
        step.ProportionalGain = 10;
        step.IntegralGain = 0.0;
        step.SteerResponseHold = 11;
        step.StanleyAggressiveness = 1.0;
        Assert.That(await step.ValidateAsync(), Is.False);
    }

    [Test]
    public async Task SteeringGainsStep_Validation_SteerResponseHoldBoundary()
    {
        var step = new SteeringGainsStepViewModel(_configService);
        step.ProportionalGain = 10;
        step.IntegralGain = 0.0;
        step.StanleyAggressiveness = 1.0;

        step.SteerResponseHold = 1.0;
        Assert.That(await step.ValidateAsync(), Is.True);

        step.SteerResponseHold = 10.0;
        Assert.That(await step.ValidateAsync(), Is.True);
    }

    [Test]
    public async Task SteeringGainsStep_Validation_StanleyAggressivenessTooLow()
    {
        var step = new SteeringGainsStepViewModel(_configService);
        step.ProportionalGain = 10;
        step.IntegralGain = 0.0;
        step.SteerResponseHold = 3.0;
        step.StanleyAggressiveness = -1;
        Assert.That(await step.ValidateAsync(), Is.False);
        Assert.That(step.ValidationMessage, Does.Contain("Stanley Aggressiveness"));
    }

    [Test]
    public async Task SteeringGainsStep_Validation_StanleyAggressivenessTooHigh()
    {
        var step = new SteeringGainsStepViewModel(_configService);
        step.ProportionalGain = 10;
        step.IntegralGain = 0.0;
        step.SteerResponseHold = 3.0;
        step.StanleyAggressiveness = 11;
        Assert.That(await step.ValidateAsync(), Is.False);
    }

    [Test]
    public async Task SteeringGainsStep_Validation_StanleyAggressivenessBoundary()
    {
        var step = new SteeringGainsStepViewModel(_configService);
        step.ProportionalGain = 10;
        step.IntegralGain = 0.0;
        step.SteerResponseHold = 3.0;

        step.StanleyAggressiveness = 0;
        Assert.That(await step.ValidateAsync(), Is.True);

        step.StanleyAggressiveness = 10;
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
    // MotorDirectionTestStepViewModel
    // =========================================================================

    [Test]
    public void MotorDirectionTest_Title_IsCorrect()
    {
        var step = new MotorDirectionTestStepViewModel(_configService);
        Assert.That(step.Title, Is.EqualTo("Motor Direction Test"));
    }

    [Test]
    public void MotorDirectionTest_OnEntering_LoadsInvertMotor()
    {
        _store.AutoSteer.InvertMotor = true;
        var testable = new TestableStep<MotorDirectionTestStepViewModel>(
            new MotorDirectionTestStepViewModel(_configService));

        testable.Enter();

        Assert.That(testable.Step.InvertMotor, Is.True);
    }

    [Test]
    public void MotorDirectionTest_OnLeaving_SavesInvertMotor()
    {
        var testable = new TestableStep<MotorDirectionTestStepViewModel>(
            new MotorDirectionTestStepViewModel(_configService));
        testable.Enter();
        testable.Step.InvertMotor = true;

        testable.Leave();

        Assert.That(_store.AutoSteer.InvertMotor, Is.True);
    }

    [Test]
    public async Task MotorDirectionTest_PulseLeft_CallsFreeDrive()
    {
        var autoSteerService = Substitute.For<IAutoSteerService>();
        var step = new MotorDirectionTestStepViewModel(_configService, autoSteerService);

        await ((AsyncRelayCommand)step.PulseLeftCommand).ExecuteAsync(null);

        autoSteerService.Received(1).EnableFreeDrive();
        autoSteerService.Received(1).SetFreeDriveAngle(-20);
        autoSteerService.Received(1).SetFreeDriveAngle(0);
        autoSteerService.Received(1).DisableFreeDrive();
    }

    [Test]
    public void MotorDirectionTest_ShouldSkip_WhenGpsOnly()
    {
        var hardwareStep = new HardwareInstalledStepViewModel();
        hardwareStep.HardwareLevel = 0;
        var step = new MotorDirectionTestStepViewModel(_configService);
        step.SetHardwareStep(hardwareStep);

        Assert.That(step.ShouldSkip, Is.True);
    }

    // =========================================================================
    // SpeedAndSensorsStepViewModel
    // =========================================================================

    [Test]
    public void SpeedAndSensorsStep_OnEntering_LoadsFromConfig()
    {
        _store.AutoSteer.MinSteerSpeed = 2.0;
        _store.AutoSteer.MaxSteerSpeed = 20.0;
        _store.AutoSteer.TurnSensorEnabled = true;
        _store.AutoSteer.PressureSensorEnabled = false;
        _store.AutoSteer.CurrentSensorEnabled = true;
        var testable = new TestableStep<SpeedAndSensorsStepViewModel>(
            new SpeedAndSensorsStepViewModel(_configService));

        testable.Enter();

        Assert.That(testable.Step.MinSteerSpeed, Is.EqualTo(2.0));
        Assert.That(testable.Step.MaxSteerSpeed, Is.EqualTo(20.0));
        Assert.That(testable.Step.TurnSensorEnabled, Is.True);
        Assert.That(testable.Step.PressureSensorEnabled, Is.False);
        Assert.That(testable.Step.CurrentSensorEnabled, Is.True);
    }

    [Test]
    public void SpeedAndSensorsStep_OnLeaving_SavesToConfig()
    {
        var testable = new TestableStep<SpeedAndSensorsStepViewModel>(
            new SpeedAndSensorsStepViewModel(_configService));
        testable.Enter();
        testable.Step.MinSteerSpeed = 1.0;
        testable.Step.MaxSteerSpeed = 25.0;
        testable.Step.TurnSensorEnabled = false;
        testable.Step.PressureSensorEnabled = true;
        testable.Step.CurrentSensorEnabled = true;

        testable.Leave();

        Assert.That(_store.AutoSteer.MinSteerSpeed, Is.EqualTo(1.0));
        Assert.That(_store.AutoSteer.MaxSteerSpeed, Is.EqualTo(25.0));
        Assert.That(_store.AutoSteer.TurnSensorEnabled, Is.False);
        Assert.That(_store.AutoSteer.PressureSensorEnabled, Is.True);
        Assert.That(_store.AutoSteer.CurrentSensorEnabled, Is.True);
    }

    [Test]
    public async Task SpeedAndSensorsStep_Validation_ValidValues()
    {
        var step = new SpeedAndSensorsStepViewModel(_configService);
        step.MinSteerSpeed = 1.0;
        step.MaxSteerSpeed = 15.0;
        Assert.That(await step.ValidateAsync(), Is.True);
    }

    [Test]
    public async Task SpeedAndSensorsStep_Validation_MinNegative()
    {
        var step = new SpeedAndSensorsStepViewModel(_configService);
        step.MinSteerSpeed = -1;
        step.MaxSteerSpeed = 15.0;
        Assert.That(await step.ValidateAsync(), Is.False);
        Assert.That(step.ValidationMessage, Does.Contain("Min Steer Speed"));
    }

    [Test]
    public async Task SpeedAndSensorsStep_Validation_MaxZero()
    {
        var step = new SpeedAndSensorsStepViewModel(_configService);
        step.MinSteerSpeed = 0;
        step.MaxSteerSpeed = 0;
        Assert.That(await step.ValidateAsync(), Is.False);
        Assert.That(step.ValidationMessage, Does.Contain("Max Steer Speed"));
    }

    [Test]
    public async Task SpeedAndSensorsStep_Validation_MaxNotGreaterThanMin()
    {
        var step = new SpeedAndSensorsStepViewModel(_configService);
        step.MinSteerSpeed = 10;
        step.MaxSteerSpeed = 10;
        Assert.That(await step.ValidateAsync(), Is.False);
        Assert.That(step.ValidationMessage, Does.Contain("greater than"));
    }

    [Test]
    public async Task SpeedAndSensorsStep_Validation_MaxLessThanMin()
    {
        var step = new SpeedAndSensorsStepViewModel(_configService);
        step.MinSteerSpeed = 10;
        step.MaxSteerSpeed = 5;
        Assert.That(await step.ValidateAsync(), Is.False);
    }

    [Test]
    public async Task SpeedAndSensorsStep_Validation_ZeroMinIsValid()
    {
        var step = new SpeedAndSensorsStepViewModel(_configService);
        step.MinSteerSpeed = 0;
        step.MaxSteerSpeed = 15;
        Assert.That(await step.ValidateAsync(), Is.True);
    }

    [Test]
    public void SpeedAndSensorsStep_CanSkip_IsTrue()
    {
        var step = new SpeedAndSensorsStepViewModel(_configService);
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
    // CpdCircleTestStepViewModel
    // =========================================================================

    [Test]
    public void CpdCircleTest_Title_IsCorrect()
    {
        var step = new CpdCircleTestStepViewModel(_configService);
        Assert.That(step.Title, Is.EqualTo("CPD Circle Test"));
    }

    [Test]
    public void CpdCircleTest_CanSkip_IsTrue()
    {
        var step = new CpdCircleTestStepViewModel(_configService);
        Assert.That(step.CanSkip, Is.True);
    }

    [Test]
    public void CpdCircleTest_ShouldSkip_WhenGpsOnly()
    {
        var hardwareStep = new HardwareInstalledStepViewModel();
        hardwareStep.HardwareLevel = 0; // GPS Only

        var step = new CpdCircleTestStepViewModel(_configService);
        step.SetHardwareStep(hardwareStep);

        Assert.That(step.ShouldSkip, Is.True);
    }

    [Test]
    public void CpdCircleTest_OnEntering_LoadsCountsPerDegree()
    {
        _store.AutoSteer.CountsPerDegree = 42;
        var step = new CpdCircleTestStepViewModel(_configService);
        var testable = new TestableStep<CpdCircleTestStepViewModel>(step);

        testable.Enter();

        Assert.That(step.CountsPerDegree, Is.EqualTo(42));
    }

    [Test]
    public void CpdCircleTest_OnLeaving_SavesCountsPerDegree()
    {
        _store.AutoSteer.CountsPerDegree = 100;
        var step = new CpdCircleTestStepViewModel(_configService);
        var testable = new TestableStep<CpdCircleTestStepViewModel>(step);

        testable.Enter();
        step.CountsPerDegree = 75;
        testable.Leave();

        Assert.That(_store.AutoSteer.CountsPerDegree, Is.EqualTo(75));
    }

    [Test]
    public void CpdCircleTest_CalculateCpd_KnownCircle()
    {
        // Given: wheelbase=2.5, trackWidth=1.8, diameter=20m, actualAngle=7.0, currentCpd=100
        // calcAngle = atan(2.5 / ((20 - 1.8*0.5) / 2)) = atan(2.5 / 9.55) = atan(0.2618) ~ 14.67 deg
        // newCpd = (7.0 / 14.67) * 100 * 0.9 ~ 42.9
        double result = CpdCircleTestStepViewModel.CalculateCpdFromCircle(
            wheelbase: 2.5, trackWidth: 1.8, diameter: 20.0,
            actualAngle: 7.0, currentCpd: 100);

        // The result should be clamped to int range 1-255 and use the 0.9 conservative factor
        Assert.That(result, Is.InRange(30, 55));

        // More precise check:
        // calcAngle = Math.Atan(2.5 / ((20 - 0.9) / 2)) * 180 / Math.PI
        //           = Math.Atan(2.5 / 9.55) * 180 / PI ~ 14.67
        // newCpd = (7.0 / 14.67) * 100 * 0.9 ~ 42.9
        double calcAngle = Math.Atan(2.5 / ((20 - 1.8 * 0.5) / 2)) * 180.0 / Math.PI;
        double expected = (7.0 / calcAngle) * 100 * 0.9;
        Assert.That(result, Is.EqualTo(Math.Clamp((int)expected, 1, 255)));
    }

    [Test]
    public void CpdCircleTest_DiameterTracking_FindsMaxDistance()
    {
        var autoSteerService = Substitute.For<IAutoSteerService>();
        autoSteerService.LastSteerData.Returns(new SteerModuleData(
            ActualSteerAngle: 15.0, ImuHeading: 0, ImuRoll: 0,
            WorkSwitchActive: false, SteerSwitchActive: false,
            RemoteButtonPressed: false, VwasFusionActive: false, PwmDisplay: 0));

        _store.Vehicle.Wheelbase = 2.5;
        _store.Vehicle.TrackWidth = 1.8;
        _store.AutoSteer.CountsPerDegree = 100;

        var step = new CpdCircleTestStepViewModel(_configService, autoSteerService);
        var testable = new TestableStep<CpdCircleTestStepViewModel>(step);
        testable.Enter();

        // Start recording at position (100, 200)
        step.StartRecordingAt(100, 200);
        Assert.That(step.IsRecording, Is.True);

        // Feed increasing distances (positions moving away from start)
        // Distance = sqrt((easting-100)^2 + (northing-200)^2)
        step.ProcessGpsUpdate(110, 200); // dist = 10
        Assert.That(step.Diameter, Is.EqualTo(10).Within(0.1));

        step.ProcessGpsUpdate(115, 200); // dist = 15
        Assert.That(step.Diameter, Is.EqualTo(15).Within(0.1));

        step.ProcessGpsUpdate(120, 200); // dist = 20 (max)
        Assert.That(step.Diameter, Is.EqualTo(20).Within(0.1));

        // Now feed stable distances (no increase) 10 times to trigger stabilization
        for (int i = 0; i < 10; i++)
        {
            step.ProcessGpsUpdate(115, 200); // dist = 15, less than max 20
        }

        // After 10 stable updates, recording should stop and CPD should be calculated
        Assert.That(step.IsRecording, Is.False);
        Assert.That(step.Diameter, Is.EqualTo(20).Within(0.1));
        Assert.That(step.TestResult, Does.Contain("Diameter"));
        Assert.That(step.CountsPerDegree, Is.GreaterThan(0));
    }

    [Test]
    public void CpdCircleTest_IsRtkFixed_RequiresFixQualityExactlyFour()
    {
        // RTK Float (5) and higher modes still drift at the centimeter
        // scale; only RTK Fixed (4) is accurate enough for the circle
        // measurement, so IsRtkFixed must be a strict equality check,
        // not a 'quality >= 4' gate.
        var autoSteerService = Substitute.For<IAutoSteerService>();
        autoSteerService.LastSteerData.Returns(SteerModuleData.Empty);
        var step = new CpdCircleTestStepViewModel(_configService, autoSteerService);
        var testable = new TestableStep<CpdCircleTestStepViewModel>(step);
        testable.Enter();

        // FixQuality == 4 -> RTK Fixed -> recording gate open.
        autoSteerService.StateUpdated += Raise.Event<EventHandler<VehicleStateSnapshot>>(
            autoSteerService,
            new VehicleStateSnapshot { FixQuality = 4, Speed = 1.5 });
        Assert.That(step.IsRtkFixed, Is.True, "FixQuality 4 must enable recording");
        Assert.That(step.FixQuality, Is.EqualTo(4));

        // FixQuality == 5 (RTK Float) -> gate closed.
        autoSteerService.StateUpdated += Raise.Event<EventHandler<VehicleStateSnapshot>>(
            autoSteerService,
            new VehicleStateSnapshot { FixQuality = 5, Speed = 1.5 });
        Assert.That(step.IsRtkFixed, Is.False, "RTK Float must NOT pass the gate");
        Assert.That(step.FixQuality, Is.EqualTo(5));

        // Downgrade to FixQuality 2 (DGPS) -> gate stays closed.
        autoSteerService.StateUpdated += Raise.Event<EventHandler<VehicleStateSnapshot>>(
            autoSteerService,
            new VehicleStateSnapshot { FixQuality = 2, Speed = 1.5 });
        Assert.That(step.IsRtkFixed, Is.False);
        Assert.That(step.FixQuality, Is.EqualTo(2));
    }

    [Test]
    public void CpdCircleTest_FixQualityLabel_MapsNmeaCodesToHumanText()
    {
        var step = new CpdCircleTestStepViewModel(_configService);

        step.FixQuality = 0;
        Assert.That(step.FixQualityLabel, Is.EqualTo("No Fix"));

        step.FixQuality = 4;
        Assert.That(step.FixQualityLabel, Is.EqualTo("RTK Fixed"));

        step.FixQuality = 5;
        Assert.That(step.FixQualityLabel, Is.EqualTo("RTK Float"));

        step.FixQuality = 99;
        Assert.That(step.FixQualityLabel, Does.StartWith("Unknown"));
    }

    [Test]
    public void CpdCircleTest_IsAtRecommendedSpeed_TracksFiveKmhWindow()
    {
        // ~5 km/h is the documented sweet spot. The window is 3..7 so
        // the wizard nudges the operator without flickering green/red
        // at every tenth of a km/h.
        var autoSteerService = Substitute.For<IAutoSteerService>();
        autoSteerService.LastSteerData.Returns(SteerModuleData.Empty);
        var step = new CpdCircleTestStepViewModel(_configService, autoSteerService);
        var testable = new TestableStep<CpdCircleTestStepViewModel>(step);
        testable.Enter();

        // 2 km/h: too slow. (SpeedKmh = Speed * 3.6, so Speed = 0.55 -> ~2 km/h)
        autoSteerService.StateUpdated += Raise.Event<EventHandler<VehicleStateSnapshot>>(
            autoSteerService,
            new VehicleStateSnapshot { FixQuality = 4, Speed = 2.0 / 3.6 });
        Assert.That(step.IsAtRecommendedSpeed, Is.False, "2 km/h is below the window");

        // 5 km/h: bullseye.
        autoSteerService.StateUpdated += Raise.Event<EventHandler<VehicleStateSnapshot>>(
            autoSteerService,
            new VehicleStateSnapshot { FixQuality = 4, Speed = 5.0 / 3.6 });
        Assert.That(step.IsAtRecommendedSpeed, Is.True, "5 km/h is in the window");

        // 10 km/h: too fast.
        autoSteerService.StateUpdated += Raise.Event<EventHandler<VehicleStateSnapshot>>(
            autoSteerService,
            new VehicleStateSnapshot { FixQuality = 4, Speed = 10.0 / 3.6 });
        Assert.That(step.IsAtRecommendedSpeed, Is.False, "10 km/h is above the window");
    }

    // =========================================================================
    // AckermannTestStepViewModel
    // =========================================================================

    [Test]
    public void AckermannTest_Title_IsCorrect()
    {
        var step = new AckermannTestStepViewModel(_configService);
        Assert.That(step.Title, Is.EqualTo("Ackermann Calibration"));
    }

    [Test]
    public void AckermannTest_CanSkip_IsTrue()
    {
        var step = new AckermannTestStepViewModel(_configService);
        Assert.That(step.CanSkip, Is.True);
    }

    [Test]
    public void AckermannTest_ShouldSkip_WhenGpsOnly()
    {
        var hardwareStep = new HardwareInstalledStepViewModel();
        hardwareStep.HardwareLevel = 0; // GPS Only

        var step = new AckermannTestStepViewModel(_configService);
        step.SetHardwareStep(hardwareStep);

        Assert.That(step.ShouldSkip, Is.True);
    }

    [Test]
    public void AckermannTest_OnEntering_LoadsAckermann()
    {
        _store.AutoSteer.Ackermann = 80;
        var step = new AckermannTestStepViewModel(_configService);
        var testable = new TestableStep<AckermannTestStepViewModel>(step);

        testable.Enter();

        Assert.That(step.Ackermann, Is.EqualTo(80));
    }

    [Test]
    public void AckermannTest_OnLeaving_SavesAckermann()
    {
        _store.AutoSteer.Ackermann = 100;
        var step = new AckermannTestStepViewModel(_configService);
        var testable = new TestableStep<AckermannTestStepViewModel>(step);

        testable.Enter();
        step.Ackermann = 120;
        testable.Leave();

        Assert.That(_store.AutoSteer.Ackermann, Is.EqualTo(120));
    }

    [Test]
    public void AckermannTest_CalculateAckermann_KnownValues()
    {
        // Given: wheelbase=2.5, trackWidth=1.8, diameter=20m, startAngle=-15
        // leftAngle = atan(2.5 / ((20 - 1.8*0.5) / 2)) * 180/PI
        //           = atan(2.5 / 9.55) * 180/PI ~ 14.67 deg
        // ackermann = (14.67 / abs(-15)) * 100 ~ 97.8 -> 97
        int result = AckermannTestStepViewModel.CalculateAckermann(
            wheelbase: 2.5, trackWidth: 1.8, diameter: 20.0, startAngle: -15.0);

        // Should be near 100 (neutral), clamped 0-200
        Assert.That(result, Is.InRange(80, 120));

        // More precise check
        double leftAngle = Math.Atan(2.5 / ((20 - 1.8 * 0.5) / 2)) * 180.0 / Math.PI;
        int expected = (int)((leftAngle / Math.Abs(-15.0)) * 100);
        expected = Math.Clamp(expected, 0, 200);
        Assert.That(result, Is.EqualTo(expected));
    }

    // =========================================================================
    // SteeringGains: SideHillCompensation
    // =========================================================================

    [Test]
    public void SteeringGains_OnEntering_LoadsSideHillComp()
    {
        _store.AutoSteer.SideHillCompensation = 0.65;
        var testable = new TestableStep<SteeringGainsStepViewModel>(
            new SteeringGainsStepViewModel(_configService));

        testable.Enter();

        Assert.That(testable.Step.SideHillCompensation, Is.EqualTo(0.65));
    }

    [Test]
    public void SteeringGains_OnLeaving_SavesSideHillComp()
    {
        var testable = new TestableStep<SteeringGainsStepViewModel>(
            new SteeringGainsStepViewModel(_configService));
        testable.Enter();
        testable.Step.SideHillCompensation = 0.8;

        testable.Leave();

        Assert.That(_store.AutoSteer.SideHillCompensation, Is.EqualTo(0.8));
    }

    // =========================================================================
    // SpeedAndSensors: Safety settings
    // =========================================================================

    [Test]
    public void SpeedAndSensors_OnEntering_LoadsSafetySettings()
    {
        _store.AutoSteer.SteerInReverse = true;
        _store.AutoSteer.DeadzoneHeading = 2.5;
        _store.AutoSteer.ManualTurnsEnabled = true;
        var testable = new TestableStep<SpeedAndSensorsStepViewModel>(
            new SpeedAndSensorsStepViewModel(_configService));

        testable.Enter();

        Assert.That(testable.Step.SteerInReverse, Is.True);
        Assert.That(testable.Step.DeadzoneHeading, Is.EqualTo(2.5));
        Assert.That(testable.Step.ManualTurnsEnabled, Is.True);
    }

    [Test]
    public void SpeedAndSensors_OnLeaving_SavesSafetySettings()
    {
        var testable = new TestableStep<SpeedAndSensorsStepViewModel>(
            new SpeedAndSensorsStepViewModel(_configService));
        testable.Enter();
        testable.Step.SteerInReverse = true;
        testable.Step.DeadzoneHeading = 1.5;
        testable.Step.ManualTurnsEnabled = true;

        testable.Leave();

        Assert.That(_store.AutoSteer.SteerInReverse, Is.True);
        Assert.That(_store.AutoSteer.DeadzoneHeading, Is.EqualTo(1.5));
        Assert.That(_store.AutoSteer.ManualTurnsEnabled, Is.True);
    }

    // =========================================================================
    // Cross-cutting: Validation clears previous errors
    // =========================================================================

    [Test]
    public async Task ValidationError_ClearedOnSubsequentValidPass()
    {
        var step = new VehicleDimensionsStepViewModel(_configService);

        // First fail
        step.Wheelbase = 0.1;
        step.TrackWidth = 2.0;
        Assert.That(await step.ValidateAsync(), Is.False);
        Assert.That(step.HasValidationError, Is.True);

        // Then pass - error should be cleared
        step.Wheelbase = 2.5;
        Assert.That(await step.ValidateAsync(), Is.True);
        Assert.That(step.HasValidationError, Is.False);
        Assert.That(step.ValidationMessage, Is.Null);
    }

    // =========================================================================
    // AutoMotorCalibrationStepViewModel
    // =========================================================================

    private AutoMotorCalibrationStepViewModel CreateAutoCalStep(
        IAutoSteerService? autoSteerService = null)
    {
        var step = new AutoMotorCalibrationStepViewModel(_configService, autoSteerService);
        // Use instant delays for testing
        step.DelayFunc = (_, _) => Task.CompletedTask;
        return step;
    }

    [Test]
    public void AutoCalibration_Title_IsCorrect()
    {
        var step = CreateAutoCalStep();
        Assert.That(step.Title, Is.EqualTo("Auto Motor Calibration"));
    }

    [Test]
    public void AutoCalibration_ShouldSkip_WhenGpsOnly()
    {
        var hardwareStep = new HardwareInstalledStepViewModel();
        hardwareStep.HardwareLevel = 0;
        var step = CreateAutoCalStep();
        step.SetHardwareStep(hardwareStep);

        Assert.That(step.ShouldSkip, Is.True);
    }

    [Test]
    public void AutoCalibration_ShouldNotSkip_WhenAutoSteer()
    {
        var hardwareStep = new HardwareInstalledStepViewModel();
        hardwareStep.HardwareLevel = 1;
        var step = CreateAutoCalStep();
        step.SetHardwareStep(hardwareStep);

        Assert.That(step.ShouldSkip, Is.False);
    }

    [Test]
    public async Task AutoCalibration_PhaseA_DetectsNormalMotorDirection()
    {
        var autoSteerService = Substitute.For<IAutoSteerService>();
        var step = CreateAutoCalStep(autoSteerService);

        // Simulate: WAS starts at 0, then after some PWM ramp, reads +15 (positive = normal)
        int callCount = 0;
        step.ReadWasAngle = () =>
        {
            callCount++;
            // Return 0 for first few calls (no movement), then 15.0 (movement detected)
            return callCount <= 5 ? 0.0 : 15.0;
        };

        await step.RunPwmRampAsync();

        Assert.That(step.DetectedInvertMotor, Is.False, "Positive WAS movement = normal direction");
        Assert.That(step.Phase, Is.EqualTo(CalibrationPhase.RampResult));
        Assert.That(step.PhaseResult, Does.Contain("Normal"));
    }

    [Test]
    public async Task AutoCalibration_PhaseA_DetectsInvertedMotor()
    {
        var autoSteerService = Substitute.For<IAutoSteerService>();
        var step = CreateAutoCalStep(autoSteerService);

        // Simulate: WAS starts at 0, then reads -15 (negative = inverted)
        int callCount = 0;
        step.ReadWasAngle = () =>
        {
            callCount++;
            return callCount <= 5 ? 0.0 : -15.0;
        };

        await step.RunPwmRampAsync();

        Assert.That(step.DetectedInvertMotor, Is.True, "Negative WAS movement = inverted direction");
        Assert.That(step.Phase, Is.EqualTo(CalibrationPhase.RampResult));
        Assert.That(step.PhaseResult, Does.Contain("Inverted"));
    }

    [Test]
    public async Task AutoCalibration_PhaseA_CalculatesMinPwm()
    {
        var autoSteerService = Substitute.For<IAutoSteerService>();
        var step = CreateAutoCalStep(autoSteerService);

        // Movement detected at PWM=25 (call 6: pwm=0,5,10,15,20,25 -> index 5 is pwm 25)
        // startAngle read at call 0 (before loop)
        // Loop: call 1 (pwm=0), call 2 (pwm=5), call 3 (pwm=10), call 4 (pwm=15), call 5 (pwm=20), call 6 (pwm=25)
        int callCount = 0;
        step.ReadWasAngle = () =>
        {
            callCount++;
            // First call is startAngle (0). Calls 2-6 still 0. Call 7 (pwm=25) returns 12.0
            return callCount <= 6 ? 0.0 : 12.0;
        };

        await step.RunPwmRampAsync();

        // Movement detected at PWM=25 -> MinPwm = 25 * 1.1 = 27
        Assert.That(step.DetectedMinPwm, Is.EqualTo((int)(25 * 1.1)));
    }

    [Test]
    public async Task AutoCalibration_PhaseA_NoMovement_WarnsUser()
    {
        var autoSteerService = Substitute.For<IAutoSteerService>();
        var step = CreateAutoCalStep(autoSteerService);

        // WAS always returns 0 - no movement
        step.ReadWasAngle = () => 0.0;

        await step.RunPwmRampAsync();

        Assert.That(step.NoMovementDetected, Is.True);
        Assert.That(step.Phase, Is.EqualTo(CalibrationPhase.RampResult));
        Assert.That(step.PhaseResult, Does.Contain("No wheel movement detected"));
    }

    [Test]
    public async Task AutoCalibration_PhaseB_MeasuresMaxAngles()
    {
        var autoSteerService = Substitute.For<IAutoSteerService>();
        var step = CreateAutoCalStep(autoSteerService);

        // Simulate: right lock = 30.0, center = 0, left lock = -25.0, center = 0
        int callCount = 0;
        step.ReadWasAngle = () =>
        {
            callCount++;
            return callCount switch
            {
                1 => 30.0,   // right full lock reading
                2 => -25.0,  // left full lock reading
                _ => 0.0
            };
        };

        await step.RunMaxAngleMeasurementAsync();

        Assert.That(step.DetectedMaxAngleRight, Is.EqualTo(30.0));
        Assert.That(step.DetectedMaxAngleLeft, Is.EqualTo(25.0));
        // MaxSteerAngle = min(30, 25) * 0.9 = 22.5 -> (int)22
        Assert.That(step.MaxSteerAngle, Is.EqualTo((int)(25.0 * 0.9)));
        Assert.That(step.Phase, Is.EqualTo(CalibrationPhase.Complete));
        Assert.That(step.CalibrationCompleted, Is.True);
    }

    [Test]
    public async Task AutoCalibration_PhaseB_MaxSteerAngleIsRawWasValue()
    {
        var autoSteerService = Substitute.For<IAutoSteerService>();
        var step = CreateAutoCalStep(autoSteerService);

        // Simulate large raw WAS values (not degrees)
        int callCount = 0;
        step.ReadWasAngle = () =>
        {
            callCount++;
            return callCount switch
            {
                1 => 420.0,  // right
                2 => -380.0, // left
                _ => 0.0
            };
        };

        await step.RunMaxAngleMeasurementAsync();

        // MaxSteerAngle = min(420, 380) * 0.9 = 342
        Assert.That(step.MaxSteerAngle, Is.EqualTo((int)(380.0 * 0.9)));
    }

    [Test]
    public async Task AutoCalibration_OnLeaving_SavesResults_WhenCompleted()
    {
        var autoSteerService = Substitute.For<IAutoSteerService>();
        var step = CreateAutoCalStep(autoSteerService);
        var testable = new TestableStep<AutoMotorCalibrationStepViewModel>(step);
        testable.Enter();

        // Set calibration results
        step.DetectedInvertMotor = true;
        step.DetectedMinPwm = 28;
        step.MaxSteerAngle = 22;
        step.CalibrationCompleted = true;

        testable.Leave();

        Assert.That(_store.AutoSteer.InvertMotor, Is.True);
        Assert.That(_store.AutoSteer.MinPwm, Is.EqualTo(28));
        Assert.That(_store.AutoSteer.MaxSteerAngle, Is.EqualTo(22));
    }

    [Test]
    public void AutoCalibration_OnLeaving_DoesNotSave_WhenNotCompleted()
    {
        var step = CreateAutoCalStep();
        var testable = new TestableStep<AutoMotorCalibrationStepViewModel>(step);

        _store.AutoSteer.InvertMotor = false;
        _store.AutoSteer.MinPwm = 5;
        _store.AutoSteer.MaxSteerAngle = 45;

        testable.Enter();

        // Modify but don't complete calibration
        step.DetectedInvertMotor = true;
        step.DetectedMinPwm = 28;
        step.MaxSteerAngle = 22;
        // CalibrationCompleted remains false

        testable.Leave();

        // Original values preserved
        Assert.That(_store.AutoSteer.InvertMotor, Is.False);
        Assert.That(_store.AutoSteer.MinPwm, Is.EqualTo(5));
        Assert.That(_store.AutoSteer.MaxSteerAngle, Is.EqualTo(45));
    }

    [Test]
    public void AutoCalibration_OnEntering_LoadsCurrentConfig()
    {
        _store.AutoSteer.MinPwm = 15;
        _store.AutoSteer.InvertMotor = true;
        _store.AutoSteer.MaxSteerAngle = 35;

        var step = CreateAutoCalStep();
        var testable = new TestableStep<AutoMotorCalibrationStepViewModel>(step);

        testable.Enter();

        Assert.That(step.DetectedMinPwm, Is.EqualTo(15));
        Assert.That(step.DetectedInvertMotor, Is.True);
        Assert.That(step.MaxSteerAngle, Is.EqualTo(35));
    }

    [Test]
    public async Task AutoCalibration_RedoPhaseA_ResetsState()
    {
        var autoSteerService = Substitute.For<IAutoSteerService>();
        var step = CreateAutoCalStep(autoSteerService);

        // Complete phase A first
        int callCount = 0;
        step.ReadWasAngle = () =>
        {
            callCount++;
            return callCount <= 5 ? 0.0 : 15.0;
        };
        await step.RunPwmRampAsync();

        Assert.That(step.Phase, Is.EqualTo(CalibrationPhase.RampResult));

        // Redo
        await ((AsyncRelayCommand)step.RedoPhaseACommand).ExecuteAsync(null);

        Assert.That(step.Phase, Is.EqualTo(CalibrationPhase.WaitingToStart));
        Assert.That(step.PhaseResult, Is.EqualTo(""));
        Assert.That(step.Progress, Is.EqualTo(0));
        Assert.That(step.DetectedMinPwm, Is.EqualTo(0));
        Assert.That(step.DetectedInvertMotor, Is.False);
    }

    [Test]
    public async Task AutoCalibration_RedoPhaseB_ResetsState()
    {
        var autoSteerService = Substitute.For<IAutoSteerService>();
        var step = CreateAutoCalStep(autoSteerService);

        // Complete phase B
        int callCount = 0;
        step.ReadWasAngle = () =>
        {
            callCount++;
            return callCount switch
            {
                1 => 30.0,
                2 => -25.0,
                _ => 0.0
            };
        };
        await step.RunMaxAngleMeasurementAsync();

        Assert.That(step.Phase, Is.EqualTo(CalibrationPhase.Complete));

        // Redo
        await ((AsyncRelayCommand)step.RedoPhaseBCommand).ExecuteAsync(null);

        Assert.That(step.Phase, Is.EqualTo(CalibrationPhase.WaitingForMaxAngle));
        Assert.That(step.DetectedMaxAngleRight, Is.EqualTo(0));
        Assert.That(step.DetectedMaxAngleLeft, Is.EqualTo(0));
        Assert.That(step.MaxSteerAngle, Is.EqualTo(0));
        Assert.That(step.CalibrationCompleted, Is.False);
    }

    [Test]
    public async Task AutoCalibration_PhaseA_EnablesAndDisablesFreeDrive()
    {
        var autoSteerService = Substitute.For<IAutoSteerService>();
        var step = CreateAutoCalStep(autoSteerService);

        // Quick movement detection
        int callCount = 0;
        step.ReadWasAngle = () =>
        {
            callCount++;
            return callCount <= 1 ? 0.0 : 15.0;
        };

        await step.RunPwmRampAsync();

        autoSteerService.Received(1).EnableFreeDrive();
        autoSteerService.Received(1).DisableFreeDrive();
    }

    [Test]
    public async Task AutoCalibration_PhaseA_TransitionsToRampResult()
    {
        var step = CreateAutoCalStep();
        step.ReadWasAngle = () => 0.0;

        Assert.That(step.Phase, Is.EqualTo(CalibrationPhase.WaitingToStart));

        await step.RunPwmRampAsync();

        Assert.That(step.Phase, Is.EqualTo(CalibrationPhase.RampResult));
    }

    [Test]
    public async Task AutoCalibration_PhaseB_TransitionsToComplete()
    {
        var step = CreateAutoCalStep();
        int callCount = 0;
        step.ReadWasAngle = () =>
        {
            callCount++;
            return callCount switch
            {
                1 => 20.0,
                2 => -18.0,
                _ => 0.0
            };
        };

        await step.RunMaxAngleMeasurementAsync();

        Assert.That(step.Phase, Is.EqualTo(CalibrationPhase.Complete));
    }
}

using System;
using System.Threading.Tasks;
using AgValoniaGPS.Models.Configuration;
using AgValoniaGPS.Services.AutoSteer;
using AgValoniaGPS.Services.Interfaces;
using AgValoniaGPS.ViewModels;
using Avalonia.Headless.NUnit;
using Avalonia.Threading;
using NSubstitute;

namespace AgValoniaGPS.UI.Tests;

/// <summary>
/// VM-level tests for the Smart WAS calibration dialog. Most tests are pure
/// CPU; the off-thread SnapshotChanged test uses [AvaloniaTest] so it has a
/// real Dispatcher to marshal to.
/// </summary>
[TestFixture]
[NonParallelizable] // Touches ConfigurationStore.Instance in PGN builders
public class SmartWasViewModelTests
{
    private ISmartWasCalibrationService _smartWas = null!;
    private IConfigurationService _configService = null!;
    private IUdpCommunicationService _udpService = null!;
    private IAutoSteerService _autoSteer = null!;
    private ConfigurationStore _store = null!;

    [SetUp]
    public void SetUp()
    {
        // PgnBuilder.BuildSteerSettingsPgn reads ConfigurationStore.Instance internally
        ConfigurationStore.SetInstance(new ConfigurationStore());

        _smartWas = Substitute.For<ISmartWasCalibrationService>();
        _udpService = Substitute.For<IUdpCommunicationService>();
        _autoSteer = Substitute.For<IAutoSteerService>();

        _configService = Substitute.For<IConfigurationService>();
        _store = new ConfigurationStore();
        _store.AutoSteer.CountsPerDegree = 100;
        _store.AutoSteer.WasOffset = 10;
        _store.ActiveVehicleProfileName = "TestProfile";
        _configService.Store.Returns(_store);
    }

    private SmartWasViewModel BuildVm()
    {
        // Ensure GetSnapshot() returns something coherent for the constructor's initial pull
        _smartWas.GetSnapshot().Returns(new SmartWasSnapshot
        {
            HasValidCalibration = false,
            SampleCount = 0,
        });
        return new SmartWasViewModel(_smartWas, _configService, _udpService, _autoSteer, new AgValoniaGPS.Services.Threading.InlineUiDispatcher());
    }

    private void SetSnapshot(SmartWasSnapshot snap)
    {
        _smartWas.GetSnapshot().Returns(snap);
        _smartWas.SnapshotChanged += Raise.Event<EventHandler<SmartWasSnapshot>>(_smartWas, snap);
    }

    // 1. Apply writes additively to WasOffset
    [Test]
    public void Apply_AddsRoundedOffsetCountsToExistingWasOffset()
    {
        var vm = BuildVm();
        SetSnapshot(new SmartWasSnapshot
        {
            HasValidCalibration = true,
            RecommendedOffset = 0.5, // degrees
        });

        vm.ApplyCommand.Execute(null);

        // 0.5° × 100 cpd = 50 counts; existing WasOffset 10 → 60
        Assert.That(_store.AutoSteer.WasOffset, Is.EqualTo(60));
    }

    // 2. Apply calls ApplyOffsetCorrection on the service
    [Test]
    public void Apply_CallsApplyOffsetCorrectionWithRecommendedOffset()
    {
        var vm = BuildVm();
        SetSnapshot(new SmartWasSnapshot
        {
            HasValidCalibration = true,
            RecommendedOffset = -0.3,
        });

        vm.ApplyCommand.Execute(null);

        _smartWas.Received(1).ApplyOffsetCorrection(-0.3);
    }

    // 3. Apply sends PGN 252 (Steer Settings) over UDP
    [Test]
    public void Apply_SendsSteerSettingsPgn()
    {
        var vm = BuildVm();
        SetSnapshot(new SmartWasSnapshot
        {
            HasValidCalibration = true,
            RecommendedOffset = 0.4,
        });

        vm.ApplyCommand.Execute(null);

        _udpService.Received(1).SendToModules(
            Arg.Is<byte[]>(pgn =>
                pgn.Length >= 4 &&
                pgn[0] == 0x80 &&
                pgn[1] == 0x81 &&
                pgn[3] == 0xFC));  // 252 = STEER_SETTINGS
    }

    // 4. Apply disabled while HasValidCalibration is false
    [Test]
    public void Apply_DisabledWhenInvalid()
    {
        var vm = BuildVm();
        SetSnapshot(new SmartWasSnapshot
        {
            HasValidCalibration = false,
        });

        Assert.That(vm.ApplyCommand.CanExecute(null), Is.False);
    }

    [Test]
    public void Apply_EnabledWhenValid()
    {
        var vm = BuildVm();
        SetSnapshot(new SmartWasSnapshot
        {
            HasValidCalibration = true,
            RecommendedOffset = 0.2,
        });

        Assert.That(vm.ApplyCommand.CanExecute(null), Is.True);
    }

    // 5. Off-thread SnapshotChanged reaches UI properties (PR #320 regression)
    [AvaloniaTest]
    public async Task SnapshotChanged_FromBackgroundThread_UpdatesUiPropertiesAfterDispatcherTick()
    {
        var vm = BuildVm();

        // Fire SnapshotChanged from a background thread (simulating UDP receive thread)
        await Task.Run(() =>
        {
            _smartWas.SnapshotChanged += Raise.Event<EventHandler<SmartWasSnapshot>>(_smartWas,
                new SmartWasSnapshot
                {
                    SampleCount = 612,
                    Mean = -0.42,
                    Median = -0.38,
                    StdDev = 0.21,
                    RecommendedOffset = 0.38,
                    Confidence = 76,
                    HasValidCalibration = true,
                });
        });

        // Drain the dispatcher so the posted ApplySnapshot runs
        await Dispatcher.UIThread.InvokeAsync(() => { });

        Assert.That(vm.SampleCount, Is.EqualTo(612));
        Assert.That(vm.Median, Is.EqualTo(-0.38));
        Assert.That(vm.Confidence, Is.EqualTo(76));
        Assert.That(vm.HasValidCalibration, Is.True);
    }

    // 6. CloseSmartWasDialogCommand on MainViewModel restores ActiveDialog=None
    [AvaloniaTest]
    public void CloseSmartWasDialogCommand_RestoresActiveDialogToNone()
    {
        var builder = new MainViewModelBuilder();
        var mvm = builder.Build();

        // Open and then close
        mvm.ShowSmartWasCommand?.Execute(null);
        Assert.That(mvm.State.UI.ActiveDialog, Is.EqualTo(AgValoniaGPS.Models.State.DialogType.SmartWas));

        mvm.CloseSmartWasDialogCommand?.Execute(null);
        Assert.That(mvm.State.UI.ActiveDialog, Is.EqualTo(AgValoniaGPS.Models.State.DialogType.None));
    }
}

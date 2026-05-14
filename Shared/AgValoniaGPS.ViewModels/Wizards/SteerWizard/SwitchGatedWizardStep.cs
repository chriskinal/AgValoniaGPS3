// AgValoniaGPS
// Copyright (C) 2024-2026 AgValoniaGPS Contributors
//
// Licensed under GNU GPL v3. See LICENSE.md.

using System;
using System.ComponentModel;

using AgValoniaGPS.Services.Interfaces;

using Avalonia.Threading;

namespace AgValoniaGPS.ViewModels.Wizards.SteerWizard;

/// <summary>
/// Base for steer-wizard calibration steps that must respect the
/// physical Steer-Switch safety gate. When the operator has
/// <c>Tool.IsSteerSwitchEnabled = true</c>, the step is held off until
/// the live PGN 253 reports the switch active; when that flag is
/// false, the gate is bypassed entirely so operators without a wired
/// switch can still calibrate.
///
/// Subclasses get <see cref="WaitingForPhysicalSwitch"/>,
/// <see cref="PhysicalSwitchPromptText"/>, <see cref="HasHardware"/>
/// and <see cref="CanStartTest"/> for free. They must:
///   1. Call <see cref="SubscribeToSwitchGate"/> in their
///      <c>OnEntering</c> override and
///      <see cref="UnsubscribeFromSwitchGate"/> in <c>OnLeaving</c>.
///   2. Bind the Start button's <c>IsEnabled</c> to
///      <see cref="CanStartTest"/> in AXAML.
///   3. Display <see cref="PhysicalSwitchPromptText"/> when
///      <see cref="WaitingForPhysicalSwitch"/> is true.
///
/// The gate recomputes on PGN 253 transitions
/// (<see cref="IAutoSteerService.StateUpdated"/>) and on
/// <c>Tool.IsSteerSwitchEnabled</c> changes via
/// <c>INotifyPropertyChanged</c>. All updates are marshalled to the
/// UI thread (<see cref="Dispatcher.UIThread"/>) so Avalonia bindings
/// refresh correctly even when the events fire on the UDP receive
/// thread.
/// </summary>
public abstract class SwitchGatedWizardStep : WizardStepViewModel
{
    protected IConfigurationService ConfigService { get; }
    protected IAutoSteerService? AutoSteerService { get; }

    private bool _gateSubscribed;

    protected SwitchGatedWizardStep(IConfigurationService configService,
        IAutoSteerService? autoSteerService)
    {
        ConfigService = configService;
        AutoSteerService = autoSteerService;
    }

    /// <summary>True when hardware is connected and sending data.</summary>
    public bool HasHardware => AutoSteerService != null;

    private bool _waitingForPhysicalSwitch;
    /// <summary>
    /// True when <c>Tool.IsSteerSwitchEnabled</c> is set but the live
    /// PGN 253 reports the physical switch is OFF. While true, the
    /// Start button must stay disabled and the prompt visible.
    /// </summary>
    public bool WaitingForPhysicalSwitch
    {
        get => _waitingForPhysicalSwitch;
        private set
        {
            if (SetProperty(ref _waitingForPhysicalSwitch, value))
            {
                OnPropertyChanged(nameof(PhysicalSwitchPromptText));
                OnPropertyChanged(nameof(CanStartTest));
            }
        }
    }

    /// <summary>
    /// Operator-facing prompt explaining why <see cref="CanStartTest"/>
    /// is false. Empty when the gate is open so AXAML can drive
    /// visibility off <c>StringConverters.IsNotNullOrEmpty</c>.
    /// </summary>
    public virtual string PhysicalSwitchPromptText => WaitingForPhysicalSwitch
        ? "Turn the physical AutoSteer switch (Steer Switch) ON to start. The host is configured to require it."
        : string.Empty;

    /// <summary>
    /// Composite gate for the Start button. Subclasses should bind the
    /// button's <c>IsEnabled</c> to this so the button is greyed when
    /// either hardware is missing or the switch is currently off.
    /// </summary>
    public bool CanStartTest => HasHardware && !WaitingForPhysicalSwitch;

    /// <summary>
    /// Hook into AutoSteerService + ConfigStore.Tool so the gate keeps
    /// pace with both the live module feedback and the operator's
    /// config-dialog edits. Idempotent — safe to call from OnEntering
    /// without a "first time" guard.
    /// </summary>
    protected void SubscribeToSwitchGate()
    {
        if (_gateSubscribed)
            return;
        _gateSubscribed = true;

        if (AutoSteerService != null)
            AutoSteerService.StateUpdated += OnSwitchGateStateUpdated;
        ConfigService.Store.Tool.PropertyChanged += OnSwitchGateToolPropertyChanged;

        UpdatePhysicalSwitchGate();
    }

    /// <summary>
    /// Mirror of <see cref="SubscribeToSwitchGate"/>. Subclasses must
    /// call this from <c>OnLeaving</c> so the wizard step doesn't leak
    /// handlers onto the singletons it observes.
    /// </summary>
    protected void UnsubscribeFromSwitchGate()
    {
        if (!_gateSubscribed)
            return;
        _gateSubscribed = false;

        if (AutoSteerService != null)
            AutoSteerService.StateUpdated -= OnSwitchGateStateUpdated;
        ConfigService.Store.Tool.PropertyChanged -= OnSwitchGateToolPropertyChanged;
    }

    private void OnSwitchGateStateUpdated(object? sender, VehicleStateSnapshot snapshot)
    {
        // StateUpdated may fire from the UDP receive thread (PGN 253
        // path). Avalonia bindings — including the Start button's
        // IsEnabled gate — won't refresh on off-thread PropertyChanged,
        // so marshal to the UI thread. CheckAccess keeps test code
        // (which fires StateUpdated synchronously via NSubstitute
        // Raise.Event) running inline.
        DispatchToUI(UpdatePhysicalSwitchGate);
    }

    private void OnSwitchGateToolPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(ConfigService.Store.Tool.IsSteerSwitchEnabled))
            return;

        // Operator-driven changes typically come in on the UI thread,
        // but defensive marshalling future-proofs against a config
        // writer that fires PropertyChanged from a worker.
        DispatchToUI(UpdatePhysicalSwitchGate);
    }

    /// <summary>
    /// Recompute <see cref="WaitingForPhysicalSwitch"/> from the
    /// current config + live module feedback. Public so subclasses
    /// can re-seed after manual state changes (e.g. snapshot priming
    /// in OnEntering).
    /// </summary>
    protected void UpdatePhysicalSwitchGate()
    {
        bool requireSwitch = ConfigService.Store.Tool.IsSteerSwitchEnabled;
        bool switchActive = AutoSteerService?.LastSteerData.SteerSwitchActive ?? false;
        WaitingForPhysicalSwitch = requireSwitch && !switchActive;
    }

    private static void DispatchToUI(Action action)
    {
        if (Dispatcher.UIThread.CheckAccess())
            action();
        else
            Dispatcher.UIThread.Post(action);
    }
}

using ReactiveUI;

namespace AgValoniaGPS.Models.Configuration;

/// <summary>
/// AHRS/IMU configuration settings.
/// Part of ConfigurationStore, persisted with profile.
/// Runtime sensor values are in SensorState (not persisted).
/// </summary>
public class AhrsConfig : ReactiveObject
{
    private double _rollZero;
    public double RollZero
    {
        get => _rollZero;
        set => this.RaiseAndSetIfChanged(ref _rollZero, value);
    }

    private double _rollFilter;
    public double RollFilter
    {
        get => _rollFilter;
        set => this.RaiseAndSetIfChanged(ref _rollFilter, value);
    }

    private double _fusionWeight;
    public double FusionWeight
    {
        get => _fusionWeight;
        set => this.RaiseAndSetIfChanged(ref _fusionWeight, value);
    }

    private bool _isRollInvert;
    public bool IsRollInvert
    {
        get => _isRollInvert;
        set => this.RaiseAndSetIfChanged(ref _isRollInvert, value);
    }

    private double _forwardCompensation;
    public double ForwardCompensation
    {
        get => _forwardCompensation;
        set => this.RaiseAndSetIfChanged(ref _forwardCompensation, value);
    }

    private double _reverseCompensation;
    public double ReverseCompensation
    {
        get => _reverseCompensation;
        set => this.RaiseAndSetIfChanged(ref _reverseCompensation, value);
    }

    private bool _isAutoSteerAuto = true;
    public bool IsAutoSteerAuto
    {
        get => _isAutoSteerAuto;
        set => this.RaiseAndSetIfChanged(ref _isAutoSteerAuto, value);
    }

    private bool _isReverseOn;
    public bool IsReverseOn
    {
        get => _isReverseOn;
        set => this.RaiseAndSetIfChanged(ref _isReverseOn, value);
    }

    private bool _isDualAsIMU;
    public bool IsDualAsIMU
    {
        get => _isDualAsIMU;
        set => this.RaiseAndSetIfChanged(ref _isDualAsIMU, value);
    }

    private bool _autoSwitchDualFixOn;
    public bool AutoSwitchDualFixOn
    {
        get => _autoSwitchDualFixOn;
        set => this.RaiseAndSetIfChanged(ref _autoSwitchDualFixOn, value);
    }

    private double _autoSwitchDualFixSpeed;
    public double AutoSwitchDualFixSpeed
    {
        get => _autoSwitchDualFixSpeed;
        set => this.RaiseAndSetIfChanged(ref _autoSwitchDualFixSpeed, value);
    }

    /// <summary>
    /// Whether alarms (like RTK lost) should automatically disengage AutoSteer.
    /// </summary>
    private bool _alarmStopsAutoSteer = true;
    public bool AlarmStopsAutoSteer
    {
        get => _alarmStopsAutoSteer;
        set => this.RaiseAndSetIfChanged(ref _alarmStopsAutoSteer, value);
    }
}

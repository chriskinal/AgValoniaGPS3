using System;
using System.Globalization;
using System.Linq;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AgValoniaGPS.Models;
using AgValoniaGPS.Models.Configuration;
using AgValoniaGPS.Services.AutoSteer;
using AgValoniaGPS.Services.Interfaces;

namespace AgValoniaGPS.ViewModels;

/// <summary>
/// ViewModel for the AutoSteer Configuration Panel.
/// Handles compact/full mode switching, test mode, and PGN communication.
/// </summary>
public partial class AutoSteerConfigViewModel : ObservableObject
{
    private readonly IConfigurationService _configService;
    private readonly IUdpCommunicationService? _udpService;
    private readonly IAutoSteerService? _autoSteerService;

    public AutoSteerConfigViewModel(
        IConfigurationService configService,
        IUdpCommunicationService? udpService = null,
        IAutoSteerService? autoSteerService = null)
    {
        _configService = configService;
        _udpService = udpService;
        _autoSteerService = autoSteerService;

        InitializeNumericInputCommands();
        InitializeTab1Commands();
        InitializeTab2Commands();
        InitializeTab3Commands();
        InitializeTab4Commands();
        InitializeTab5Commands();
        InitializeTab6Commands();
        InitializeTab7Commands();
        InitializeTab8Commands();
        InitializeTab9Commands();
        InitializeTestModeCommands();
        InitializeActionCommands();
    }

    #region Configuration Access

    public ConfigurationStore Config => _configService.Store;
    public AutoSteerConfig AutoSteer => Config.AutoSteer;

    #endregion

    #region Panel Visibility & Mode

    private bool _isPanelVisible;
    public bool IsPanelVisible
    {
        get => _isPanelVisible;
        set
        {
            if (SetProperty(ref _isPanelVisible, value))
            {
                if (value)
                {
                    SubscribeToUdpEvents();
                }
                else
                {
                    UnsubscribeFromUdpEvents();
                    // Disable free drive when panel closes (safety)
                    if (IsFreeDriveMode)
                    {
                        _autoSteerService?.DisableFreeDrive();
                        IsFreeDriveMode = false;
                        FreeDriveSteerAngle = 0;
                    }
                }
            }
        }
    }

    private bool _isFullMode;
    public bool IsFullMode
    {
        get => _isFullMode;
        set => SetProperty(ref _isFullMode, value);
    }

    private int _selectedTabIndex;
    public int SelectedTabIndex
    {
        get => _selectedTabIndex;
        set => SetProperty(ref _selectedTabIndex, value);
    }

    private int _selectedRightTabIndex;
    public int SelectedRightTabIndex
    {
        get => _selectedRightTabIndex;
        set => SetProperty(ref _selectedRightTabIndex, value);
    }

    public ICommand ToggleFullModeCommand { get; private set; } = null!;
    public ICommand ClosePanelCommand { get; private set; } = null!;

    #endregion

    #region Numeric Input Dialog

    private bool _isNumericInputVisible;
    public bool IsNumericInputVisible
    {
        get => _isNumericInputVisible;
        set => SetProperty(ref _isNumericInputVisible, value);
    }

    private string _numericInputTitle = string.Empty;
    public string NumericInputTitle
    {
        get => _numericInputTitle;
        set => SetProperty(ref _numericInputTitle, value);
    }

    private string _numericInputUnit = string.Empty;
    public string NumericInputUnit
    {
        get => _numericInputUnit;
        set => SetProperty(ref _numericInputUnit, value);
    }

    private string _numericInputDisplayText = string.Empty;
    public string NumericInputDisplayText
    {
        get => _numericInputDisplayText;
        set => SetProperty(ref _numericInputDisplayText, value);
    }

    private bool _numericInputIntegerOnly;
    private bool _numericInputAllowNegative = true;
    private double _numericInputMin = double.MinValue;
    private double _numericInputMax = double.MaxValue;
    private bool _isFirstDigitEntry = true;
    private Action<double>? _numericInputCallback;

    public ICommand ConfirmNumericInputCommand { get; private set; } = null!;
    public ICommand CancelNumericInputCommand { get; private set; } = null!;
    public ICommand NumericInputDigitCommand { get; private set; } = null!;
    public ICommand NumericInputBackspaceCommand { get; private set; } = null!;
    public ICommand NumericInputClearCommand { get; private set; } = null!;
    public ICommand NumericInputNegateCommand { get; private set; } = null!;

    private void ShowNumericInput(
        string title,
        double currentValue,
        Action<double> onConfirm,
        string unit = "",
        bool integerOnly = false,
        bool allowNegative = true,
        double min = double.MinValue,
        double max = double.MaxValue)
    {
        NumericInputTitle = title;
        NumericInputUnit = unit;
        _numericInputIntegerOnly = integerOnly;
        _numericInputAllowNegative = allowNegative;
        _numericInputMin = min;
        _numericInputMax = max;
        _numericInputCallback = onConfirm;
        _isFirstDigitEntry = true;

        NumericInputDisplayText = integerOnly
            ? ((int)currentValue).ToString()
            : currentValue.ToString("F2");

        IsNumericInputVisible = true;
    }

    private void InitializeNumericInputCommands()
    {
        ConfirmNumericInputCommand = new RelayCommand(() =>
        {
            if (_numericInputCallback != null &&
                decimal.TryParse(NumericInputDisplayText, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
            {
                var value = Math.Clamp((double)parsed, _numericInputMin, _numericInputMax);
                _numericInputCallback(value);
                Config.MarkChanged();
            }
            IsNumericInputVisible = false;
            _numericInputCallback = null;
        });

        CancelNumericInputCommand = new RelayCommand(() =>
        {
            IsNumericInputVisible = false;
            _numericInputCallback = null;
        });

        NumericInputDigitCommand = new RelayCommand<string>(digit =>
        {
            if (string.IsNullOrEmpty(digit)) return;

            if (digit == ".")
            {
                if (_numericInputIntegerOnly) return;
                if (_isFirstDigitEntry)
                {
                    NumericInputDisplayText = "0.";
                    _isFirstDigitEntry = false;
                }
                else if (!NumericInputDisplayText.Contains("."))
                {
                    NumericInputDisplayText += ".";
                }
                return;
            }

            if (_isFirstDigitEntry)
            {
                NumericInputDisplayText = digit;
                _isFirstDigitEntry = false;
            }
            else
            {
                NumericInputDisplayText = NumericInputDisplayText == "0" ? digit : NumericInputDisplayText + digit;
            }
        });

        NumericInputBackspaceCommand = new RelayCommand(() =>
        {
            _isFirstDigitEntry = false;
            var current = NumericInputDisplayText;
            if (current.Length > 1)
            {
                NumericInputDisplayText = current.Length == 2 && current.StartsWith("-")
                    ? "0"
                    : current.Substring(0, current.Length - 1);
            }
            else
            {
                NumericInputDisplayText = "0";
            }
        });

        NumericInputClearCommand = new RelayCommand(() =>
        {
            NumericInputDisplayText = "0";
            _isFirstDigitEntry = false;
        });

        NumericInputNegateCommand = new RelayCommand(() =>
        {
            if (!_numericInputAllowNegative) return;
            _isFirstDigitEntry = false;

            if (NumericInputDisplayText.StartsWith("-"))
                NumericInputDisplayText = NumericInputDisplayText.Substring(1);
            else if (NumericInputDisplayText != "0")
                NumericInputDisplayText = "-" + NumericInputDisplayText;
        });

        ToggleFullModeCommand = new RelayCommand(() => IsFullMode = !IsFullMode);
        ClosePanelCommand = new RelayCommand(() => IsPanelVisible = false);
    }

    #endregion

    #region Tab 1: Pure Pursuit / Stanley

    // Pure Pursuit commands
    public ICommand EditSteerResponseCommand { get; private set; } = null!;
    public ICommand EditIntegralGainCommand { get; private set; } = null!;
    public ICommand ToggleStanleyModeCommand { get; private set; } = null!;

    // Stanley commands
    public ICommand EditStanleyAggressivenessCommand { get; private set; } = null!;
    public ICommand EditStanleyOvershootCommand { get; private set; } = null!;
    public ICommand EditStanleyIntegralCommand { get; private set; } = null!;

    private void InitializeTab1Commands()
    {
        // Pure Pursuit commands
        EditSteerResponseCommand = new RelayCommand(() =>
            ShowNumericInput("Steer Response (Hold)", AutoSteer.SteerResponseHold,
                v => AutoSteer.SteerResponseHold = v,
                "m", integerOnly: false, allowNegative: false, min: 1.0, max: 10.0));

        EditIntegralGainCommand = new RelayCommand(() =>
            ShowNumericInput("Integral Gain", AutoSteer.IntegralGain * 100,
                v => AutoSteer.IntegralGain = v / 100.0,
                "%", integerOnly: true, allowNegative: false, min: 0, max: 100));

        ToggleStanleyModeCommand = new RelayCommand(() =>
        {
            AutoSteer.IsStanleyMode = !AutoSteer.IsStanleyMode;
            Config.MarkChanged();
        });

        // Stanley commands
        EditStanleyAggressivenessCommand = new RelayCommand(() =>
            ShowNumericInput("Aggressiveness", AutoSteer.StanleyAggressiveness,
                v => AutoSteer.StanleyAggressiveness = v,
                "", integerOnly: false, allowNegative: false, min: 0.0, max: 10.0));

        EditStanleyOvershootCommand = new RelayCommand(() =>
            ShowNumericInput("Overshoot Reduction", AutoSteer.StanleyOvershootReduction,
                v => AutoSteer.StanleyOvershootReduction = v,
                "", integerOnly: false, allowNegative: false, min: 0.0, max: 10.0));

        EditStanleyIntegralCommand = new RelayCommand(() =>
            ShowNumericInput("Integral", AutoSteer.IntegralGain * 100,
                v => AutoSteer.IntegralGain = v / 100.0,
                "", integerOnly: true, allowNegative: false, min: 0, max: 100));
    }

    #endregion

    #region Tab 2: Steering Sensor

    public ICommand ZeroWasCommand { get; private set; } = null!;
    public ICommand EditCountsPerDegreeCommand { get; private set; } = null!;
    public ICommand EditAckermannCommand { get; private set; } = null!;
    public ICommand EditMaxSteerAngleCommand { get; private set; } = null!;

    private void InitializeTab2Commands()
    {
        ZeroWasCommand = new RelayCommand(() =>
        {
            // Calculate new WAS offset to make current angle read zero.
            // Module formula: angle = (rawCounts - wasOffset) / countsPerDegree
            // To zero: newOffset = currentOffset + (currentAngle * countsPerDegree)
            var angleCorrection = (int)Math.Round(_smoothedActualAngle * AutoSteer.CountsPerDegree);
            AutoSteer.WasOffset += angleCorrection;
            Config.MarkChanged();

            // Send updated settings to module immediately
            SendSteerSettingsPgn();
        });

        EditCountsPerDegreeCommand = new RelayCommand(() =>
            ShowNumericInput("Counts Per Degree", AutoSteer.CountsPerDegree,
                v => AutoSteer.CountsPerDegree = v,
                "", integerOnly: true, allowNegative: false, min: 1, max: 255));

        EditAckermannCommand = new RelayCommand(() =>
            ShowNumericInput("Ackermann", AutoSteer.Ackermann,
                v => AutoSteer.Ackermann = (int)v,
                "", integerOnly: true, allowNegative: false, min: 0, max: 200));

        EditMaxSteerAngleCommand = new RelayCommand(() =>
            ShowNumericInput("Max Steer Angle", AutoSteer.MaxSteerAngle,
                v => AutoSteer.MaxSteerAngle = (int)v,
                "°", integerOnly: true, allowNegative: false, min: 10, max: 90));
    }

    #endregion

    #region Tab 3: Deadzone / Timing

    public ICommand EditDeadzoneHeadingCommand { get; private set; } = null!;
    public ICommand EditDeadzoneDelayCommand { get; private set; } = null!;
    public ICommand EditSpeedFactorCommand { get; private set; } = null!;
    public ICommand EditAcquireFactorCommand { get; private set; } = null!;

    private void InitializeTab3Commands()
    {
        EditDeadzoneHeadingCommand = new RelayCommand(() =>
            ShowNumericInput("Deadzone Heading", AutoSteer.DeadzoneHeading,
                v => AutoSteer.DeadzoneHeading = v,
                "°", integerOnly: false, allowNegative: false, min: 0.0, max: 5.0));

        EditDeadzoneDelayCommand = new RelayCommand(() =>
            ShowNumericInput("On-Delay", AutoSteer.DeadzoneDelay,
                v => AutoSteer.DeadzoneDelay = (int)v,
                "", integerOnly: true, allowNegative: false, min: 0, max: 50));

        EditSpeedFactorCommand = new RelayCommand(() =>
            ShowNumericInput("Speed Factor", AutoSteer.SpeedFactor,
                v => AutoSteer.SpeedFactor = v,
                "", integerOnly: false, allowNegative: false, min: 0.5, max: 3.0));

        EditAcquireFactorCommand = new RelayCommand(() =>
            ShowNumericInput("Acquire Factor", AutoSteer.AcquireFactor,
                v => AutoSteer.AcquireFactor = v,
                "", integerOnly: false, allowNegative: false, min: 0.5, max: 1.0));
    }

    #endregion

    #region Tab 4: Gain / PWM

    public ICommand EditProportionalGainCommand { get; private set; } = null!;
    public ICommand EditMaxPwmCommand { get; private set; } = null!;
    public ICommand EditMinPwmCommand { get; private set; } = null!;

    private void InitializeTab4Commands()
    {
        EditProportionalGainCommand = new RelayCommand(() =>
            ShowNumericInput("Proportional Gain", AutoSteer.ProportionalGain,
                v => AutoSteer.ProportionalGain = (int)v,
                "", integerOnly: true, allowNegative: false, min: 1, max: 100));

        EditMaxPwmCommand = new RelayCommand(() =>
            ShowNumericInput("Max PWM", AutoSteer.MaxPwm,
                v => AutoSteer.MaxPwm = (int)v,
                "", integerOnly: true, allowNegative: false, min: 50, max: 255));

        EditMinPwmCommand = new RelayCommand(() =>
            ShowNumericInput("Min PWM", AutoSteer.MinPwm,
                v => AutoSteer.MinPwm = (int)v,
                "", integerOnly: true, allowNegative: false, min: 1, max: 50));
    }

    #endregion

    #region Tab 5: Turn Sensors

    public ICommand ToggleTurnSensorCommand { get; private set; } = null!;
    public ICommand TogglePressureSensorCommand { get; private set; } = null!;
    public ICommand ToggleCurrentSensorCommand { get; private set; } = null!;

    private void InitializeTab5Commands()
    {
        ToggleTurnSensorCommand = new RelayCommand(() =>
        {
            AutoSteer.TurnSensorEnabled = !AutoSteer.TurnSensorEnabled;
            Config.MarkChanged();
        });

        TogglePressureSensorCommand = new RelayCommand(() =>
        {
            AutoSteer.PressureSensorEnabled = !AutoSteer.PressureSensorEnabled;
            Config.MarkChanged();
        });

        ToggleCurrentSensorCommand = new RelayCommand(() =>
        {
            AutoSteer.CurrentSensorEnabled = !AutoSteer.CurrentSensorEnabled;
            Config.MarkChanged();
        });
    }

    #endregion

    #region Tab 6: Hardware Config

    public ICommand ToggleDanfossCommand { get; private set; } = null!;
    public ICommand ToggleInvertWasCommand { get; private set; } = null!;
    public ICommand ToggleInvertMotorCommand { get; private set; } = null!;
    public ICommand ToggleInvertRelaysCommand { get; private set; } = null!;
    public ICommand SetMotorDriverCommand { get; private set; } = null!;
    public ICommand SetAdConverterCommand { get; private set; } = null!;
    public ICommand SetImuAxisCommand { get; private set; } = null!;
    public ICommand SetExternalEnableCommand { get; private set; } = null!;

    private void InitializeTab6Commands()
    {
        ToggleDanfossCommand = new RelayCommand(() =>
        {
            AutoSteer.DanfossEnabled = !AutoSteer.DanfossEnabled;
            Config.MarkChanged();
        });

        ToggleInvertWasCommand = new RelayCommand(() =>
        {
            AutoSteer.InvertWas = !AutoSteer.InvertWas;
            Config.MarkChanged();
        });

        ToggleInvertMotorCommand = new RelayCommand(() =>
        {
            AutoSteer.InvertMotor = !AutoSteer.InvertMotor;
            Config.MarkChanged();
        });

        ToggleInvertRelaysCommand = new RelayCommand(() =>
        {
            AutoSteer.InvertRelays = !AutoSteer.InvertRelays;
            Config.MarkChanged();
        });

        SetMotorDriverCommand = new RelayCommand<int>(v =>
        {
            AutoSteer.MotorDriver = v;
            Config.MarkChanged();
        });

        SetAdConverterCommand = new RelayCommand<int>(v =>
        {
            AutoSteer.AdConverter = v;
            Config.MarkChanged();
        });

        SetImuAxisCommand = new RelayCommand<int>(v =>
        {
            AutoSteer.ImuAxisSwap = v;
            Config.MarkChanged();
        });

        SetExternalEnableCommand = new RelayCommand<int>(v =>
        {
            AutoSteer.ExternalEnable = v;
            Config.MarkChanged();
        });
    }

    #endregion

    #region Tab 7: Algorithm Settings

    public ICommand EditUTurnCompensationCommand { get; private set; } = null!;
    public ICommand EditSideHillCompensationCommand { get; private set; } = null!;
    public ICommand ToggleSteerInReverseCommand { get; private set; } = null!;

    private void InitializeTab7Commands()
    {
        EditUTurnCompensationCommand = new RelayCommand(() =>
            ShowNumericInput("U-Turn Compensation", AutoSteer.UTurnCompensation,
                v => AutoSteer.UTurnCompensation = v,
                "", integerOnly: true, allowNegative: true, min: -100, max: 100));

        EditSideHillCompensationCommand = new RelayCommand(() =>
            ShowNumericInput("Side Hill Compensation", AutoSteer.SideHillCompensation,
                v => AutoSteer.SideHillCompensation = v,
                "", integerOnly: false, allowNegative: false, min: 0.0, max: 1.0));

        ToggleSteerInReverseCommand = new RelayCommand(() =>
        {
            AutoSteer.SteerInReverse = !AutoSteer.SteerInReverse;
            Config.MarkChanged();
        });
    }

    #endregion

    #region Tab 8: Speed Limits

    public ICommand ToggleManualTurnsCommand { get; private set; } = null!;
    public ICommand EditManualTurnsSpeedCommand { get; private set; } = null!;
    public ICommand EditMinSteerSpeedCommand { get; private set; } = null!;
    public ICommand EditMaxSteerSpeedCommand { get; private set; } = null!;

    private void InitializeTab8Commands()
    {
        ToggleManualTurnsCommand = new RelayCommand(() =>
        {
            AutoSteer.ManualTurnsEnabled = !AutoSteer.ManualTurnsEnabled;
            Config.MarkChanged();
        });

        EditManualTurnsSpeedCommand = new RelayCommand(() =>
            ShowNumericInput("Manual Turns Speed", AutoSteer.ManualTurnsSpeed,
                v => AutoSteer.ManualTurnsSpeed = v,
                "km/h", integerOnly: false, allowNegative: false, min: 0, max: 30));

        EditMinSteerSpeedCommand = new RelayCommand(() =>
            ShowNumericInput("Min Steer Speed", AutoSteer.MinSteerSpeed,
                v => AutoSteer.MinSteerSpeed = v,
                "km/h", integerOnly: false, allowNegative: false, min: 0, max: 10));

        EditMaxSteerSpeedCommand = new RelayCommand(() =>
            ShowNumericInput("Max Steer Speed", AutoSteer.MaxSteerSpeed,
                v => AutoSteer.MaxSteerSpeed = v,
                "km/h", integerOnly: false, allowNegative: false, min: 5, max: 50));
    }

    #endregion

    #region Tab 9: Display Settings

    public ICommand EditLineWidthCommand { get; private set; } = null!;
    public ICommand EditNudgeDistanceCommand { get; private set; } = null!;
    public ICommand EditNextGuidanceTimeCommand { get; private set; } = null!;
    public ICommand EditCmPerPixelCommand { get; private set; } = null!;
    public ICommand ToggleLightbarCommand { get; private set; } = null!;
    public ICommand ToggleSteerBarCommand { get; private set; } = null!;
    public ICommand ToggleGuidanceBarCommand { get; private set; } = null!;

    private void InitializeTab9Commands()
    {
        EditLineWidthCommand = new RelayCommand(() =>
            ShowNumericInput("Line Width", AutoSteer.LineWidth,
                v => AutoSteer.LineWidth = (int)v,
                "px", integerOnly: true, allowNegative: false, min: 1, max: 10));

        EditNudgeDistanceCommand = new RelayCommand(() =>
            ShowNumericInput("Nudge Distance", AutoSteer.NudgeDistance,
                v => AutoSteer.NudgeDistance = (int)v,
                "cm", integerOnly: true, allowNegative: false, min: 1, max: 100));

        EditNextGuidanceTimeCommand = new RelayCommand(() =>
            ShowNumericInput("Next Guidance Line", AutoSteer.NextGuidanceTime,
                v => AutoSteer.NextGuidanceTime = v,
                "sec", integerOnly: false, allowNegative: false, min: 0.5, max: 5.0));

        EditCmPerPixelCommand = new RelayCommand(() =>
            ShowNumericInput("Cm Per Pixel", AutoSteer.CmPerPixel,
                v => AutoSteer.CmPerPixel = (int)v,
                "", integerOnly: true, allowNegative: false, min: 1, max: 20));

        ToggleLightbarCommand = new RelayCommand(() =>
        {
            AutoSteer.LightbarEnabled = !AutoSteer.LightbarEnabled;
            Config.MarkChanged();
        });

        ToggleSteerBarCommand = new RelayCommand(() =>
        {
            AutoSteer.SteerBarEnabled = !AutoSteer.SteerBarEnabled;
            Config.MarkChanged();
        });

        ToggleGuidanceBarCommand = new RelayCommand(() =>
        {
            AutoSteer.GuidanceBarOn = !AutoSteer.GuidanceBarOn;
            Config.MarkChanged();
        });
    }

    #endregion

    #region Test Mode (Free Drive)

    private bool _isFreeDriveMode;
    public bool IsFreeDriveMode
    {
        get => _isFreeDriveMode;
        set => SetProperty(ref _isFreeDriveMode, value);
    }

    private double _freeDriveSteerAngle;
    public double FreeDriveSteerAngle
    {
        get => _freeDriveSteerAngle;
        set => SetProperty(ref _freeDriveSteerAngle, Math.Clamp(value, -40, 40));
    }

    private int _pwmDisplay;
    public int PwmDisplay
    {
        get => _pwmDisplay;
        set => SetProperty(ref _pwmDisplay, value);
    }

    private double _actualSteerAngle;
    public double ActualSteerAngle
    {
        get => _actualSteerAngle;
        set => SetProperty(ref _actualSteerAngle, value);
    }

    private double _steerError;
    public double SteerError
    {
        get => _steerError;
        set => SetProperty(ref _steerError, value);
    }

    private double _setSteerAngle;
    public double SetSteerAngle
    {
        get => _setSteerAngle;
        set => SetProperty(ref _setSteerAngle, value);
    }

    private double _actualPressurePercent;
    public double ActualPressurePercent
    {
        get => _actualPressurePercent;
        set => SetProperty(ref _actualPressurePercent, value);
    }

    private double _actualCurrentPercent;
    public double ActualCurrentPercent
    {
        get => _actualCurrentPercent;
        set => SetProperty(ref _actualCurrentPercent, value);
    }

    // Diameter measurement
    private bool _isRecording;
    public bool IsRecording
    {
        get => _isRecording;
        set => SetProperty(ref _isRecording, value);
    }

    private double _measuredDiameter;
    public double MeasuredDiameter
    {
        get => _measuredDiameter;
        set => SetProperty(ref _measuredDiameter, value);
    }

    private int _testSteerOffset;
    public int TestSteerOffset
    {
        get => _testSteerOffset;
        set => SetProperty(ref _testSteerOffset, value);
    }

    public ICommand ToggleFreeDriveCommand { get; private set; } = null!;
    public ICommand SteerLeftCommand { get; private set; } = null!;
    public ICommand SteerRightCommand { get; private set; } = null!;
    public ICommand SteerCenterCommand { get; private set; } = null!;
    public ICommand SteerOffset5Command { get; private set; } = null!;
    public ICommand ToggleRecordingCommand { get; private set; } = null!;
    public ICommand EditSteerOffsetCommand { get; private set; } = null!;
    public ICommand RecordDiameterCommand { get; private set; } = null!;
    public ICommand EditTurnSensorCountsCommand { get; private set; } = null!;

    private void InitializeTestModeCommands()
    {
        ToggleFreeDriveCommand = new RelayCommand(() =>
        {
            IsFreeDriveMode = !IsFreeDriveMode;
            if (IsFreeDriveMode)
            {
                _autoSteerService?.EnableFreeDrive();
                FreeDriveSteerAngle = 0;
            }
            else
            {
                _autoSteerService?.DisableFreeDrive();
                FreeDriveSteerAngle = 0;
            }
        });

        SteerLeftCommand = new RelayCommand(() =>
        {
            if (IsFreeDriveMode)
            {
                FreeDriveSteerAngle = Math.Max(FreeDriveSteerAngle - 1, -40);
                _autoSteerService?.SetFreeDriveAngle(FreeDriveSteerAngle);
            }
        });

        SteerRightCommand = new RelayCommand(() =>
        {
            if (IsFreeDriveMode)
            {
                FreeDriveSteerAngle = Math.Min(FreeDriveSteerAngle + 1, 40);
                _autoSteerService?.SetFreeDriveAngle(FreeDriveSteerAngle);
            }
        });

        SteerCenterCommand = new RelayCommand(() =>
        {
            FreeDriveSteerAngle = 0;
            _autoSteerService?.SetFreeDriveAngle(0);
        });

        SteerOffset5Command = new RelayCommand(() =>
        {
            FreeDriveSteerAngle = FreeDriveSteerAngle == 0 ? 5 : 0;
            _autoSteerService?.SetFreeDriveAngle(FreeDriveSteerAngle);
        });

        ToggleRecordingCommand = new RelayCommand(() =>
        {
            IsRecording = !IsRecording;
            if (IsRecording)
            {
                MeasuredDiameter = 0;
                // TODO: Start recording position for diameter calculation
            }
            else
            {
                // TODO: Calculate diameter from recorded path
            }
        });

        EditSteerOffsetCommand = new RelayCommand(() =>
        {
            ShowNumericInput("Steer Offset", TestSteerOffset, value =>
            {
                TestSteerOffset = (int)Math.Clamp(value, -50, 50);
            });
        });

        EditTurnSensorCountsCommand = new RelayCommand(() =>
        {
            ShowNumericInput("Turn Sensor Counts", AutoSteer.TurnSensorCounts, value =>
            {
                AutoSteer.TurnSensorCounts = (int)Math.Clamp(value, 0, 255);
            });
        });

        RecordDiameterCommand = new RelayCommand(() =>
        {
            IsRecording = !IsRecording;
            if (IsRecording)
            {
                MeasuredDiameter = 0;
                // Start recording - actual diameter measurement would require GPS tracking
            }
        });
    }

    #endregion

    #region Action Commands

    public ICommand SendAndSaveCommand { get; private set; } = null!;
    public ICommand ResetToDefaultsCommand { get; private set; } = null!;
    public ICommand WizardCommand { get; private set; } = null!;

    private bool _isResetConfirmVisible;
    public bool IsResetConfirmVisible
    {
        get => _isResetConfirmVisible;
        set => SetProperty(ref _isResetConfirmVisible, value);
    }

    public ICommand ConfirmResetCommand { get; private set; } = null!;
    public ICommand CancelResetCommand { get; private set; } = null!;

    private void InitializeActionCommands()
    {
        SendAndSaveCommand = new RelayCommand(() =>
        {
            // Build and send PGN 252 (Steer Settings)
            SendSteerSettingsPgn();

            // Build and send PGN 251 (Steer Config)
            SendSteerConfigPgn();

            // Save configuration
            _configService.SaveProfile(Config.ActiveProfileName);
        });

        ResetToDefaultsCommand = new RelayCommand(() =>
        {
            // Show confirmation before reset
            IsResetConfirmVisible = true;
        });

        ConfirmResetCommand = new RelayCommand(() =>
        {
            // Reset all settings to defaults
            AutoSteer.ResetToDefaults();
            Config.MarkChanged();

            // Send updated settings to module
            SendSteerSettingsPgn();
            SendSteerConfigPgn();

            IsResetConfirmVisible = false;
        });

        CancelResetCommand = new RelayCommand(() =>
        {
            IsResetConfirmVisible = false;
        });

        WizardCommand = new RelayCommand(() =>
        {
            // TODO: Launch setup wizard
        });
    }

    /// <summary>
    /// Build and send PGN 252 - Steer Settings
    /// </summary>
    private void SendSteerSettingsPgn()
    {
        var pgn = PgnBuilder.BuildSteerSettingsPgn(AutoSteer);
        _udpService?.SendToModules(pgn);
    }

    /// <summary>
    /// Build and send PGN 251 - Steer Config
    /// </summary>
    private void SendSteerConfigPgn()
    {
        var pgn = PgnBuilder.BuildSteerConfigPgn(AutoSteer);
        _udpService?.SendToModules(pgn);
    }

    #endregion

    #region Status Display (Updated from hardware feedback)

    /// <summary>
    /// Update status display from incoming PGN 253 data
    /// </summary>
    public void UpdateFromSteerData(double actualAngle, double setAngle, int pwm)
    {
        ActualSteerAngle = actualAngle;
        SetSteerAngle = setAngle;
        PwmDisplay = pwm;
        SteerError = Math.Abs(actualAngle - setAngle);
    }

    #endregion

    #region UDP Event Subscription

    private bool _isSubscribed;

    // Display throttling: update at ~10Hz instead of 50Hz
    private const int DisplayUpdateIntervalMs = 100;
    private DateTime _lastDisplayUpdate = DateTime.MinValue;

    // Smoothing: exponential moving average for angle display
    private double _smoothedActualAngle;
    private const double SmoothingFactor = 0.3; // Lower = smoother, higher = more responsive

    private void SubscribeToUdpEvents()
    {
        if (_isSubscribed || _udpService == null) return;

        _udpService.DataReceived += OnUdpDataReceived;
        _isSubscribed = true;
        _smoothedActualAngle = 0;
        _lastDisplayUpdate = DateTime.MinValue;
    }

    private void UnsubscribeFromUdpEvents()
    {
        if (!_isSubscribed || _udpService == null) return;

        _udpService.DataReceived -= OnUdpDataReceived;
        _isSubscribed = false;
    }

    private void OnUdpDataReceived(object? sender, UdpDataReceivedEventArgs e)
    {
        // Only process PGN 253 (Steer Data from module)
        if (e.PGN != PgnNumbers.AUTOSTEER_DATA) return;

        // Parse the incoming steer data
        if (!PgnBuilder.TryParseSteerData(e.Data, out var steerData))
            return;

        // Apply exponential moving average smoothing to angle
        _smoothedActualAngle = (_smoothedActualAngle * (1 - SmoothingFactor)) +
                              (steerData.ActualSteerAngle * SmoothingFactor);

        // Throttle display updates to ~10Hz
        var now = DateTime.UtcNow;
        if ((now - _lastDisplayUpdate).TotalMilliseconds < DisplayUpdateIntervalMs)
            return;

        _lastDisplayUpdate = now;

        // Update display properties with smoothed values
        ActualSteerAngle = Math.Round(_smoothedActualAngle, 1);
        PwmDisplay = steerData.PwmDisplay;
        SteerError = Math.Round(Math.Abs(SetSteerAngle - _smoothedActualAngle), 1);
    }

    #endregion
}

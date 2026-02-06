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

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Reactive;
using System.Windows.Input;
using ReactiveUI;
using AgValoniaGPS.Models;
using AgValoniaGPS.Models.Configuration;
using AgValoniaGPS.Services.AutoSteer;
using AgValoniaGPS.Services.Interfaces;

namespace AgValoniaGPS.ViewModels;

/// <summary>
/// ViewModel for the AutoSteer Configuration Panel.
/// Handles compact/full mode switching, test mode, and PGN communication.
/// </summary>
public partial class AutoSteerConfigViewModel : ReactiveObject
{
    private readonly IConfigurationService _configService;
    private readonly IUdpCommunicationService? _udpService;
    private readonly IAutoSteerService? _autoSteerService;

    // Debounce timer for auto-sending slider changes
    private readonly System.Timers.Timer _sliderDebounceTimer;
    private const int SliderDebounceDelayMs = 1000;

    // Properties that trigger immediate slider debounce (left-side compact mode)
    private static readonly HashSet<string> LeftSideSliderProperties = new()
    {
        nameof(AutoSteerConfig.SteerResponseHold),
        nameof(AutoSteerConfig.IntegralGain),
        nameof(AutoSteerConfig.StanleyAggressiveness),
        nameof(AutoSteerConfig.StanleyOvershootReduction),
        nameof(AutoSteerConfig.CountsPerDegree),
        nameof(AutoSteerConfig.Ackermann),
        nameof(AutoSteerConfig.MaxSteerAngle),
        nameof(AutoSteerConfig.SpeedFactor),
        nameof(AutoSteerConfig.AcquireFactor),
        nameof(AutoSteerConfig.ProportionalGain),
        nameof(AutoSteerConfig.MaxPwm),
        nameof(AutoSteerConfig.MinPwm)
    };

    public AutoSteerConfigViewModel(
        IConfigurationService configService,
        IUdpCommunicationService? udpService = null,
        IAutoSteerService? autoSteerService = null)
    {
        _configService = configService;
        _udpService = udpService;
        _autoSteerService = autoSteerService;

        // Set up debounce timer for slider changes
        _sliderDebounceTimer = new System.Timers.Timer(SliderDebounceDelayMs);
        _sliderDebounceTimer.AutoReset = false;
        _sliderDebounceTimer.Elapsed += OnSliderDebounceElapsed;

        // Subscribe to AutoSteer property changes
        AutoSteer.PropertyChanged += OnAutoSteerPropertyChanged;

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

    #region Change Tracking & Auto-Send

    private bool _hasUnsavedRightSideChanges;
    /// <summary>
    /// True when right-side (expanded mode) settings have changed but not been saved.
    /// Shows warning indicator to remind user to click Send+Save.
    /// </summary>
    public bool HasUnsavedRightSideChanges
    {
        get => _hasUnsavedRightSideChanges;
        set => this.RaiseAndSetIfChanged(ref _hasUnsavedRightSideChanges, value);
    }

    private void OnAutoSteerPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == null) return;

        if (LeftSideSliderProperties.Contains(e.PropertyName))
        {
            // Left-side slider changed - debounce and auto-send
            _sliderDebounceTimer.Stop();
            _sliderDebounceTimer.Start();
        }
        else
        {
            // Right-side setting changed - show warning
            HasUnsavedRightSideChanges = true;
        }
    }

    private void OnSliderDebounceElapsed(object? sender, System.Timers.ElapsedEventArgs e)
    {
        // Timer fires on background thread - send PGN updates
        SendSettingsToModule();
    }

    /// <summary>
    /// Send current settings to the steering module (PGN 251 + 252).
    /// Thread-safe, can be called from timer thread.
    /// </summary>
    private void SendSettingsToModule()
    {
        if (_udpService == null) return;

        try
        {
            var pgn251 = PgnBuilder.BuildSteerConfigPgn(AutoSteer);
            var pgn252 = PgnBuilder.BuildSteerSettingsPgn(AutoSteer);
            _udpService.SendToModules(pgn251);
            _udpService.SendToModules(pgn252);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to send settings: {ex.Message}");
        }
    }

    #endregion

    #region Panel Visibility & Mode

    private bool _isPanelVisible;
    public bool IsPanelVisible
    {
        get => _isPanelVisible;
        set
        {
            if (this.RaiseAndSetIfChanged(ref _isPanelVisible, value))
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
        set => this.RaiseAndSetIfChanged(ref _isFullMode, value);
    }

    private int _selectedTabIndex;
    public int SelectedTabIndex
    {
        get => _selectedTabIndex;
        set => this.RaiseAndSetIfChanged(ref _selectedTabIndex, value);
    }

    private int _selectedRightTabIndex;
    public int SelectedRightTabIndex
    {
        get => _selectedRightTabIndex;
        set => this.RaiseAndSetIfChanged(ref _selectedRightTabIndex, value);
    }

    public ICommand ToggleFullModeCommand { get; private set; } = null!;
    public ICommand ClosePanelCommand { get; private set; } = null!;

    #endregion

    #region Numeric Input Dialog

    private bool _isNumericInputVisible;
    public bool IsNumericInputVisible
    {
        get => _isNumericInputVisible;
        set => this.RaiseAndSetIfChanged(ref _isNumericInputVisible, value);
    }

    private string _numericInputTitle = string.Empty;
    public string NumericInputTitle
    {
        get => _numericInputTitle;
        set => this.RaiseAndSetIfChanged(ref _numericInputTitle, value);
    }

    private string _numericInputUnit = string.Empty;
    public string NumericInputUnit
    {
        get => _numericInputUnit;
        set => this.RaiseAndSetIfChanged(ref _numericInputUnit, value);
    }

    private string _numericInputDisplayText = string.Empty;
    public string NumericInputDisplayText
    {
        get => _numericInputDisplayText;
        set => this.RaiseAndSetIfChanged(ref _numericInputDisplayText, value);
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
        ConfirmNumericInputCommand = ReactiveCommand.Create(() =>
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

        CancelNumericInputCommand = ReactiveCommand.Create(() =>
        {
            IsNumericInputVisible = false;
            _numericInputCallback = null;
        });

        NumericInputDigitCommand = ReactiveCommand.Create<string>(digit =>
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

        NumericInputBackspaceCommand = ReactiveCommand.Create(() =>
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

        NumericInputClearCommand = ReactiveCommand.Create(() =>
        {
            NumericInputDisplayText = "0";
            _isFirstDigitEntry = false;
        });

        NumericInputNegateCommand = ReactiveCommand.Create(() =>
        {
            if (!_numericInputAllowNegative) return;
            _isFirstDigitEntry = false;

            if (NumericInputDisplayText.StartsWith("-"))
                NumericInputDisplayText = NumericInputDisplayText.Substring(1);
            else if (NumericInputDisplayText != "0")
                NumericInputDisplayText = "-" + NumericInputDisplayText;
        });

        ToggleFullModeCommand = ReactiveCommand.Create(() => IsFullMode = !IsFullMode);
        ClosePanelCommand = ReactiveCommand.Create(() => IsPanelVisible = false);
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
        EditSteerResponseCommand = ReactiveCommand.Create(() =>
            ShowNumericInput("Steer Response (Hold)", AutoSteer.SteerResponseHold,
                v => AutoSteer.SteerResponseHold = v,
                "m", integerOnly: false, allowNegative: false, min: 1.0, max: 10.0));

        EditIntegralGainCommand = ReactiveCommand.Create(() =>
            ShowNumericInput("Integral Gain", AutoSteer.IntegralGain * 100,
                v => AutoSteer.IntegralGain = v / 100.0,
                "%", integerOnly: true, allowNegative: false, min: 0, max: 100));

        ToggleStanleyModeCommand = ReactiveCommand.Create(() =>
        {
            AutoSteer.IsStanleyMode = !AutoSteer.IsStanleyMode;
            Config.MarkChanged();
        });

        // Stanley commands
        EditStanleyAggressivenessCommand = ReactiveCommand.Create(() =>
            ShowNumericInput("Aggressiveness", AutoSteer.StanleyAggressiveness,
                v => AutoSteer.StanleyAggressiveness = v,
                "", integerOnly: false, allowNegative: false, min: 0.0, max: 10.0));

        EditStanleyOvershootCommand = ReactiveCommand.Create(() =>
            ShowNumericInput("Overshoot Reduction", AutoSteer.StanleyOvershootReduction,
                v => AutoSteer.StanleyOvershootReduction = v,
                "", integerOnly: false, allowNegative: false, min: 0.0, max: 10.0));

        EditStanleyIntegralCommand = ReactiveCommand.Create(() =>
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
        ZeroWasCommand = ReactiveCommand.Create(() =>
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

        EditCountsPerDegreeCommand = ReactiveCommand.Create(() =>
            ShowNumericInput("Counts Per Degree", AutoSteer.CountsPerDegree,
                v => AutoSteer.CountsPerDegree = v,
                "", integerOnly: true, allowNegative: false, min: 1, max: 255));

        EditAckermannCommand = ReactiveCommand.Create(() =>
            ShowNumericInput("Ackermann", AutoSteer.Ackermann,
                v => AutoSteer.Ackermann = (int)v,
                "", integerOnly: true, allowNegative: false, min: 0, max: 200));

        EditMaxSteerAngleCommand = ReactiveCommand.Create(() =>
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
        EditDeadzoneHeadingCommand = ReactiveCommand.Create(() =>
            ShowNumericInput("Deadzone Heading", AutoSteer.DeadzoneHeading,
                v => AutoSteer.DeadzoneHeading = v,
                "°", integerOnly: false, allowNegative: false, min: 0.0, max: 5.0));

        EditDeadzoneDelayCommand = ReactiveCommand.Create(() =>
            ShowNumericInput("On-Delay", AutoSteer.DeadzoneDelay,
                v => AutoSteer.DeadzoneDelay = (int)v,
                "", integerOnly: true, allowNegative: false, min: 0, max: 50));

        EditSpeedFactorCommand = ReactiveCommand.Create(() =>
            ShowNumericInput("Speed Factor", AutoSteer.SpeedFactor,
                v => AutoSteer.SpeedFactor = v,
                "", integerOnly: false, allowNegative: false, min: 0.5, max: 3.0));

        EditAcquireFactorCommand = ReactiveCommand.Create(() =>
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
        EditProportionalGainCommand = ReactiveCommand.Create(() =>
            ShowNumericInput("Proportional Gain", AutoSteer.ProportionalGain,
                v => AutoSteer.ProportionalGain = (int)v,
                "", integerOnly: true, allowNegative: false, min: 1, max: 100));

        EditMaxPwmCommand = ReactiveCommand.Create(() =>
            ShowNumericInput("Max PWM", AutoSteer.MaxPwm,
                v => AutoSteer.MaxPwm = (int)v,
                "", integerOnly: true, allowNegative: false, min: 50, max: 255));

        EditMinPwmCommand = ReactiveCommand.Create(() =>
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
        ToggleTurnSensorCommand = ReactiveCommand.Create(() =>
        {
            AutoSteer.TurnSensorEnabled = !AutoSteer.TurnSensorEnabled;
            Config.MarkChanged();
        });

        TogglePressureSensorCommand = ReactiveCommand.Create(() =>
        {
            AutoSteer.PressureSensorEnabled = !AutoSteer.PressureSensorEnabled;
            Config.MarkChanged();
        });

        ToggleCurrentSensorCommand = ReactiveCommand.Create(() =>
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
        ToggleDanfossCommand = ReactiveCommand.Create(() =>
        {
            AutoSteer.DanfossEnabled = !AutoSteer.DanfossEnabled;
            Config.MarkChanged();
        });

        ToggleInvertWasCommand = ReactiveCommand.Create(() =>
        {
            AutoSteer.InvertWas = !AutoSteer.InvertWas;
            Config.MarkChanged();
        });

        ToggleInvertMotorCommand = ReactiveCommand.Create(() =>
        {
            AutoSteer.InvertMotor = !AutoSteer.InvertMotor;
            Config.MarkChanged();
        });

        ToggleInvertRelaysCommand = ReactiveCommand.Create(() =>
        {
            AutoSteer.InvertRelays = !AutoSteer.InvertRelays;
            Config.MarkChanged();
        });

        SetMotorDriverCommand = ReactiveCommand.Create<int>(v =>
        {
            AutoSteer.MotorDriver = v;
            Config.MarkChanged();
        });

        SetAdConverterCommand = ReactiveCommand.Create<int>(v =>
        {
            AutoSteer.AdConverter = v;
            Config.MarkChanged();
        });

        SetImuAxisCommand = ReactiveCommand.Create<int>(v =>
        {
            AutoSteer.ImuAxisSwap = v;
            Config.MarkChanged();
        });

        SetExternalEnableCommand = ReactiveCommand.Create<int>(v =>
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
        EditUTurnCompensationCommand = ReactiveCommand.Create(() =>
            ShowNumericInput("U-Turn Compensation", AutoSteer.UTurnCompensation,
                v => AutoSteer.UTurnCompensation = v,
                "", integerOnly: true, allowNegative: true, min: -100, max: 100));

        EditSideHillCompensationCommand = ReactiveCommand.Create(() =>
            ShowNumericInput("Side Hill Compensation", AutoSteer.SideHillCompensation,
                v => AutoSteer.SideHillCompensation = v,
                "", integerOnly: false, allowNegative: false, min: 0.0, max: 1.0));

        ToggleSteerInReverseCommand = ReactiveCommand.Create(() =>
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
        ToggleManualTurnsCommand = ReactiveCommand.Create(() =>
        {
            AutoSteer.ManualTurnsEnabled = !AutoSteer.ManualTurnsEnabled;
            Config.MarkChanged();
        });

        EditManualTurnsSpeedCommand = ReactiveCommand.Create(() =>
            ShowNumericInput("Manual Turns Speed", AutoSteer.ManualTurnsSpeed,
                v => AutoSteer.ManualTurnsSpeed = v,
                "km/h", integerOnly: false, allowNegative: false, min: 0, max: 30));

        EditMinSteerSpeedCommand = ReactiveCommand.Create(() =>
            ShowNumericInput("Min Steer Speed", AutoSteer.MinSteerSpeed,
                v => AutoSteer.MinSteerSpeed = v,
                "km/h", integerOnly: false, allowNegative: false, min: 0, max: 10));

        EditMaxSteerSpeedCommand = ReactiveCommand.Create(() =>
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
        EditLineWidthCommand = ReactiveCommand.Create(() =>
            ShowNumericInput("Line Width", AutoSteer.LineWidth,
                v => AutoSteer.LineWidth = (int)v,
                "px", integerOnly: true, allowNegative: false, min: 1, max: 10));

        EditNudgeDistanceCommand = ReactiveCommand.Create(() =>
            ShowNumericInput("Nudge Distance", AutoSteer.NudgeDistance,
                v => AutoSteer.NudgeDistance = (int)v,
                "cm", integerOnly: true, allowNegative: false, min: 1, max: 100));

        EditNextGuidanceTimeCommand = ReactiveCommand.Create(() =>
            ShowNumericInput("Next Guidance Line", AutoSteer.NextGuidanceTime,
                v => AutoSteer.NextGuidanceTime = v,
                "sec", integerOnly: false, allowNegative: false, min: 0.5, max: 5.0));

        EditCmPerPixelCommand = ReactiveCommand.Create(() =>
            ShowNumericInput("Cm Per Pixel", AutoSteer.CmPerPixel,
                v => AutoSteer.CmPerPixel = (int)v,
                "", integerOnly: true, allowNegative: false, min: 1, max: 20));

        ToggleLightbarCommand = ReactiveCommand.Create(() =>
        {
            AutoSteer.LightbarEnabled = !AutoSteer.LightbarEnabled;
            Config.MarkChanged();
        });

        ToggleSteerBarCommand = ReactiveCommand.Create(() =>
        {
            AutoSteer.SteerBarEnabled = !AutoSteer.SteerBarEnabled;
            Config.MarkChanged();
        });

        ToggleGuidanceBarCommand = ReactiveCommand.Create(() =>
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
        set => this.RaiseAndSetIfChanged(ref _isFreeDriveMode, value);
    }

    private double _freeDriveSteerAngle;
    public double FreeDriveSteerAngle
    {
        get => _freeDriveSteerAngle;
        set => this.RaiseAndSetIfChanged(ref _freeDriveSteerAngle, Math.Clamp(value, -40, 40));
    }

    private int _pwmDisplay;
    public int PwmDisplay
    {
        get => _pwmDisplay;
        set => this.RaiseAndSetIfChanged(ref _pwmDisplay, value);
    }

    private double _actualSteerAngle;
    public double ActualSteerAngle
    {
        get => _actualSteerAngle;
        set => this.RaiseAndSetIfChanged(ref _actualSteerAngle, value);
    }

    private double _steerError;
    public double SteerError
    {
        get => _steerError;
        set => this.RaiseAndSetIfChanged(ref _steerError, value);
    }

    private double _setSteerAngle;
    public double SetSteerAngle
    {
        get => _setSteerAngle;
        set => this.RaiseAndSetIfChanged(ref _setSteerAngle, value);
    }

    private double _actualPressurePercent;
    public double ActualPressurePercent
    {
        get => _actualPressurePercent;
        set => this.RaiseAndSetIfChanged(ref _actualPressurePercent, value);
    }

    private double _actualCurrentPercent;
    public double ActualCurrentPercent
    {
        get => _actualCurrentPercent;
        set => this.RaiseAndSetIfChanged(ref _actualCurrentPercent, value);
    }

    // Switch status from module (PGN 253)
    private bool _steerSwitchActive;
    public bool SteerSwitchActive
    {
        get => _steerSwitchActive;
        set => this.RaiseAndSetIfChanged(ref _steerSwitchActive, value);
    }

    private bool _workSwitchActive;
    public bool WorkSwitchActive
    {
        get => _workSwitchActive;
        set => this.RaiseAndSetIfChanged(ref _workSwitchActive, value);
    }

    private bool _remoteButtonPressed;
    public bool RemoteButtonPressed
    {
        get => _remoteButtonPressed;
        set => this.RaiseAndSetIfChanged(ref _remoteButtonPressed, value);
    }

    private bool _vwasFusionActive;
    public bool VwasFusionActive
    {
        get => _vwasFusionActive;
        set => this.RaiseAndSetIfChanged(ref _vwasFusionActive, value);
    }

    private int _testSteerOffset;
    public int TestSteerOffset
    {
        get => _testSteerOffset;
        set => this.RaiseAndSetIfChanged(ref _testSteerOffset, value);
    }

    public ICommand ToggleFreeDriveCommand { get; private set; } = null!;
    public ICommand SteerLeftCommand { get; private set; } = null!;
    public ICommand SteerRightCommand { get; private set; } = null!;
    public ICommand SteerCenterCommand { get; private set; } = null!;
    public ICommand SteerOffset5Command { get; private set; } = null!;
    public ICommand EditSteerOffsetCommand { get; private set; } = null!;
    public ICommand EditTurnSensorCountsCommand { get; private set; } = null!;

    private void InitializeTestModeCommands()
    {
        ToggleFreeDriveCommand = ReactiveCommand.Create(() =>
        {
            IsFreeDriveMode = !IsFreeDriveMode;
            if (IsFreeDriveMode)
            {
                _autoSteerService?.EnableFreeDrive();
                FreeDriveSteerAngle = 0;
                SetSteerAngle = 0; // Show target angle in status bar
            }
            else
            {
                _autoSteerService?.DisableFreeDrive();
                FreeDriveSteerAngle = 0;
            }
        });

        SteerLeftCommand = ReactiveCommand.Create(() =>
        {
            if (IsFreeDriveMode)
            {
                FreeDriveSteerAngle = Math.Max(FreeDriveSteerAngle - 2, -40);
                _autoSteerService?.SetFreeDriveAngle(FreeDriveSteerAngle);
                SetSteerAngle = FreeDriveSteerAngle; // Update status bar
            }
        });

        SteerRightCommand = ReactiveCommand.Create(() =>
        {
            if (IsFreeDriveMode)
            {
                FreeDriveSteerAngle = Math.Min(FreeDriveSteerAngle + 2, 40);
                _autoSteerService?.SetFreeDriveAngle(FreeDriveSteerAngle);
                SetSteerAngle = FreeDriveSteerAngle; // Update status bar
            }
        });

        SteerCenterCommand = ReactiveCommand.Create(() =>
        {
            FreeDriveSteerAngle = 0;
            _autoSteerService?.SetFreeDriveAngle(0);
            if (IsFreeDriveMode) SetSteerAngle = 0;
        });

        SteerOffset5Command = ReactiveCommand.Create(() =>
        {
            FreeDriveSteerAngle = FreeDriveSteerAngle == 0 ? 5 : 0;
            _autoSteerService?.SetFreeDriveAngle(FreeDriveSteerAngle);
            if (IsFreeDriveMode) SetSteerAngle = FreeDriveSteerAngle;
        });

        EditSteerOffsetCommand = ReactiveCommand.Create(() =>
        {
            ShowNumericInput("Steer Offset", TestSteerOffset, value =>
            {
                TestSteerOffset = (int)Math.Clamp(value, -50, 50);
            });
        });

        EditTurnSensorCountsCommand = ReactiveCommand.Create(() =>
        {
            ShowNumericInput("Turn Sensor Counts", AutoSteer.TurnSensorCounts, value =>
            {
                AutoSteer.TurnSensorCounts = (int)Math.Clamp(value, 0, 255);
            });
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
        set => this.RaiseAndSetIfChanged(ref _isResetConfirmVisible, value);
    }

    public ICommand ConfirmResetCommand { get; private set; } = null!;
    public ICommand CancelResetCommand { get; private set; } = null!;

    private void InitializeActionCommands()
    {
        SendAndSaveCommand = ReactiveCommand.Create(() =>
        {
            // Build and send PGN 252 (Steer Settings)
            SendSteerSettingsPgn();

            // Build and send PGN 251 (Steer Config)
            SendSteerConfigPgn();

            // Save configuration
            _configService.SaveProfile(Config.ActiveProfileName);

            // Clear the unsaved changes warning
            HasUnsavedRightSideChanges = false;
        });

        ResetToDefaultsCommand = ReactiveCommand.Create(() =>
        {
            // Show confirmation before reset
            IsResetConfirmVisible = true;
        });

        ConfirmResetCommand = ReactiveCommand.Create(() =>
        {
            // Reset all settings to defaults
            AutoSteer.ResetToDefaults();
            Config.MarkChanged();

            // Send updated settings to module
            SendSteerSettingsPgn();
            SendSteerConfigPgn();

            IsResetConfirmVisible = false;
        });

        CancelResetCommand = ReactiveCommand.Create(() =>
        {
            IsResetConfirmVisible = false;
        });

        WizardCommand = ReactiveCommand.Create(() =>
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
        // Handle PGN 250 (Sensor Data from module - pressure/current)
        // Note: Kickout logic is handled by AutoSteerService; we just update display here
        if (e.PGN == PgnNumbers.SENSOR_DATA)
        {
            if (PgnBuilder.TryParseSensorData(e.Data, out var sensorData))
            {
                // Map sensor value (0-255) to percentage (0-100)
                double percent = sensorData.SensorValue / 255.0 * 100.0;

                // Update display for the enabled sensor type
                if (AutoSteer.PressureSensorEnabled)
                    ActualPressurePercent = Math.Round(percent, 1);
                else if (AutoSteer.CurrentSensorEnabled)
                    ActualCurrentPercent = Math.Round(percent, 1);
            }
            return;
        }

        // Handle PGN 253 (Steer Data from module)
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

        // Calculate steer error
        double error = SetSteerAngle - _smoothedActualAngle;
        SteerError = Math.Round(Math.Abs(error), 1);

        // Calculate PWM based on error and proportional gain
        // This is the PWM AgValonia would send to the motor
        double pwmRaw = error * AutoSteer.ProportionalGain;
        int pwmClamped = (int)Math.Clamp(Math.Abs(pwmRaw), 0, AutoSteer.MaxPwm);

        // Apply minimum PWM threshold (deadband)
        if (pwmClamped < AutoSteer.MinPwm)
            pwmClamped = 0;

        PwmDisplay = pwmClamped;

        // Update switch status from module
        WorkSwitchActive = steerData.WorkSwitchActive;
        SteerSwitchActive = steerData.SteerSwitchActive;
        RemoteButtonPressed = steerData.RemoteButtonPressed;
        VwasFusionActive = steerData.VwasFusionActive;
    }

    #endregion
}

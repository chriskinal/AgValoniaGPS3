using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Input;
using ReactiveUI;
using AgValoniaGPS.Models;
using AgValoniaGPS.Models.Configuration;
using AgValoniaGPS.Services.Interfaces;

namespace AgValoniaGPS.ViewModels;

/// <summary>
/// ViewModel for the Configuration Dialog.
/// Binds directly to ConfigurationStore - no property mapping needed.
/// </summary>
public class ConfigurationViewModel : ReactiveObject
{
    private readonly IConfigurationService _configService;

    #region Dialog Visibility

    private bool _isDialogVisible;
    public bool IsDialogVisible
    {
        get => _isDialogVisible;
        set => this.RaiseAndSetIfChanged(ref _isDialogVisible, value);
    }

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

    private decimal? _numericInputValue;
    public decimal? NumericInputValue
    {
        get => _numericInputValue;
        set => this.RaiseAndSetIfChanged(ref _numericInputValue, value);
    }

    private string _numericInputDisplayText = string.Empty;
    public string NumericInputDisplayText
    {
        get => _numericInputDisplayText;
        set => this.RaiseAndSetIfChanged(ref _numericInputDisplayText, value);
    }

    private bool _numericInputIntegerOnly;
    public bool NumericInputIntegerOnly
    {
        get => _numericInputIntegerOnly;
        set => this.RaiseAndSetIfChanged(ref _numericInputIntegerOnly, value);
    }

    private bool _numericInputAllowNegative = true;
    public bool NumericInputAllowNegative
    {
        get => _numericInputAllowNegative;
        set => this.RaiseAndSetIfChanged(ref _numericInputAllowNegative, value);
    }

    private double _numericInputMin = double.MinValue;
    private double _numericInputMax = double.MaxValue;
    private bool _isFirstDigitEntry = true; // Track if user has started typing

    private Action<double>? _numericInputCallback;

    public ICommand ConfirmNumericInputCommand { get; private set; } = null!;
    public ICommand CancelNumericInputCommand { get; private set; } = null!;
    public ICommand NumericInputDigitCommand { get; private set; } = null!;
    public ICommand NumericInputBackspaceCommand { get; private set; } = null!;
    public ICommand NumericInputClearCommand { get; private set; } = null!;
    public ICommand NumericInputNegateCommand { get; private set; } = null!;

    /// <summary>
    /// Shows the numeric input dialog for editing a value
    /// </summary>
    private void ShowNumericInput(
        string title,
        double currentValue,
        Action<double> onConfirm,
        string unit = "m",
        bool integerOnly = false,
        bool allowNegative = true,
        double min = double.MinValue,
        double max = double.MaxValue)
    {
        NumericInputTitle = title;
        NumericInputUnit = unit;
        NumericInputIntegerOnly = integerOnly;
        NumericInputAllowNegative = allowNegative;
        _numericInputMin = min;
        _numericInputMax = max;
        _numericInputCallback = onConfirm;
        _isFirstDigitEntry = true; // Reset - first digit will replace current value

        // Set initial value and display
        _numericInputValue = (decimal)currentValue;
        this.RaisePropertyChanged(nameof(NumericInputValue));

        NumericInputDisplayText = integerOnly
            ? ((int)currentValue).ToString()
            : currentValue.ToString("F2");

        IsNumericInputVisible = true;
    }

    private void InitializeNumericInputCommands()
    {
        ConfirmNumericInputCommand = new RelayCommand(() =>
        {
            if (_numericInputCallback != null)
            {
                // Parse from display text to get actual value
                // Use InvariantCulture to handle decimal point consistently
                if (decimal.TryParse(NumericInputDisplayText, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
                {
                    var value = (double)parsed;
                    // Clamp to min/max
                    value = Math.Clamp(value, _numericInputMin, _numericInputMax);
                    _numericInputCallback(value);
                    Config.MarkChanged();
                }
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

            // Handle decimal point
            if (digit == ".")
            {
                if (NumericInputIntegerOnly) return;

                if (_isFirstDigitEntry)
                {
                    // Start fresh with "0."
                    NumericInputDisplayText = "0.";
                    _isFirstDigitEntry = false;
                }
                else if (!NumericInputDisplayText.Contains("."))
                {
                    NumericInputDisplayText += ".";
                }
                return;
            }

            // First digit replaces the initial value
            if (_isFirstDigitEntry)
            {
                NumericInputDisplayText = digit;
                _isFirstDigitEntry = false;
            }
            else
            {
                // Append digit
                if (NumericInputDisplayText == "0")
                    NumericInputDisplayText = digit;
                else
                    NumericInputDisplayText += digit;
            }

            // Update the backing value
            if (decimal.TryParse(NumericInputDisplayText, out var parsed))
            {
                _numericInputValue = parsed;
                this.RaisePropertyChanged(nameof(NumericInputValue));
            }
        });

        NumericInputBackspaceCommand = new RelayCommand(() =>
        {
            _isFirstDigitEntry = false; // User is editing

            var current = NumericInputDisplayText;
            if (current.Length > 1)
            {
                // Handle negative numbers - don't delete just the minus sign
                if (current.Length == 2 && current.StartsWith("-"))
                    NumericInputDisplayText = "0";
                else
                    NumericInputDisplayText = current.Substring(0, current.Length - 1);
            }
            else
            {
                NumericInputDisplayText = "0";
            }

            if (decimal.TryParse(NumericInputDisplayText, out var parsed))
            {
                _numericInputValue = parsed;
                this.RaisePropertyChanged(nameof(NumericInputValue));
            }
        });

        NumericInputClearCommand = new RelayCommand(() =>
        {
            NumericInputDisplayText = "0";
            _numericInputValue = 0;
            _isFirstDigitEntry = false;
            this.RaisePropertyChanged(nameof(NumericInputValue));
        });

        NumericInputNegateCommand = new RelayCommand(() =>
        {
            if (!NumericInputAllowNegative) return;

            _isFirstDigitEntry = false; // User is editing

            if (NumericInputDisplayText.StartsWith("-"))
            {
                NumericInputDisplayText = NumericInputDisplayText.Substring(1);
            }
            else if (NumericInputDisplayText != "0")
            {
                NumericInputDisplayText = "-" + NumericInputDisplayText;
            }

            if (decimal.TryParse(NumericInputDisplayText, out var parsed))
            {
                _numericInputValue = parsed;
                this.RaisePropertyChanged(nameof(NumericInputValue));
            }
        });
    }

    #endregion

    #region Direct Access to Configuration

    /// <summary>
    /// The configuration store - bind directly to sub-configs in XAML
    /// Example: {Binding Config.Vehicle.Wheelbase}
    /// </summary>
    public ConfigurationStore Config => _configService.Store;

    // Convenience accessors for cleaner XAML bindings
    public VehicleConfig Vehicle => Config.Vehicle;
    public ToolConfig Tool => Config.Tool;
    public GuidanceConfig Guidance => Config.Guidance;
    public DisplayConfig Display => Config.Display;
    public SimulatorConfig Simulator => Config.Simulator;

    /// <summary>
    /// Calculated total width from sections (NumSections × DefaultSectionWidth in meters)
    /// </summary>
    public double CalculatedSectionTotal => Config.NumSections * Tool.DefaultSectionWidth / 100.0;

    #endregion

    #region Profile Management

    public ObservableCollection<string> AvailableProfiles { get; } = new();

    private string? _selectedProfileName;
    public string? SelectedProfileName
    {
        get => _selectedProfileName;
        set
        {
            this.RaiseAndSetIfChanged(ref _selectedProfileName, value);
            if (value != null && value != Config.ActiveProfileName)
            {
                _configService.LoadProfile(value);
            }
        }
    }

    /// <summary>
    /// Whether there are unsaved changes (delegates to ConfigurationStore)
    /// </summary>
    public bool HasUnsavedChanges
    {
        get => Config.HasUnsavedChanges;
        set => Config.HasUnsavedChanges = value;
    }

    #endregion

    #region Commands

    public ICommand LoadProfileCommand { get; }
    public ICommand SaveProfileCommand { get; }
    public ICommand NewProfileCommand { get; }
    public ICommand DeleteProfileCommand { get; }
    public ICommand ApplyCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand SetToolTypeCommand { get; }
    public ICommand SetVehicleTypeCommand { get; }

    // Vehicle Tab Edit Commands
    public ICommand EditWheelbaseCommand { get; private set; } = null!;
    public ICommand EditTrackWidthCommand { get; private set; } = null!;
    public ICommand EditHitchLengthCommand { get; private set; } = null!;
    public ICommand EditAntennaPivotCommand { get; private set; } = null!;
    public ICommand EditAntennaHeightCommand { get; private set; } = null!;
    public ICommand EditAntennaOffsetCommand { get; private set; } = null!;

    // Tool Tab Edit Commands
    public ICommand EditToolWidthCommand { get; private set; } = null!;
    public ICommand EditToolOverlapCommand { get; private set; } = null!;
    public ICommand EditToolOffsetCommand { get; private set; } = null!;
    public ICommand EditToolHitchLengthCommand { get; private set; } = null!;
    public ICommand EditTrailingHitchLengthCommand { get; private set; } = null!;
    public ICommand EditTankHitchLengthCommand { get; private set; } = null!;
    public ICommand EditToolPivotCommand { get; private set; } = null!;

    // Sections Tab Edit Commands
    public ICommand EditNumSectionsCommand { get; private set; } = null!;
    public ICommand EditLookAheadOnCommand { get; private set; } = null!;
    public ICommand EditLookAheadOffCommand { get; private set; } = null!;
    public ICommand EditTurnOffDelayCommand { get; private set; } = null!;
    public ICommand EditDefaultSectionWidthCommand { get; private set; } = null!;
    public ICommand EditMinCoverageCommand { get; private set; } = null!;
    public ICommand EditCutoffSpeedCommand { get; private set; } = null!;

    // U-Turn Tab Edit Commands
    public ICommand EditUTurnRadiusCommand { get; private set; } = null!;
    public ICommand EditUTurnExtensionCommand { get; private set; } = null!;
    public ICommand EditUTurnDistanceCommand { get; private set; } = null!;
    public ICommand EditUTurnSkipWidthCommand { get; private set; } = null!;
    public ICommand EditUTurnSmoothingCommand { get; private set; } = null!;

    #endregion

    #region Events

    public event EventHandler? CloseRequested;
    public event EventHandler<string>? ProfileSaved;

    #endregion

    public ConfigurationViewModel(IConfigurationService configService)
    {
        _configService = configService;

        // Initialize commands
        LoadProfileCommand = new RelayCommand<string>(LoadProfile);
        SaveProfileCommand = new RelayCommand(SaveProfile);
        NewProfileCommand = new RelayCommand<string>(CreateNewProfile);
        DeleteProfileCommand = new RelayCommand(DeleteProfile);
        ApplyCommand = new RelayCommand(ApplyChanges);
        CancelCommand = new RelayCommand(Cancel);
        SetToolTypeCommand = new RelayCommand<string>(SetToolType);
        SetVehicleTypeCommand = new RelayCommand<string>(SetVehicleType);

        // Initialize numeric input commands
        InitializeNumericInputCommands();

        // Initialize edit commands for all tabs
        InitializeVehicleEditCommands();
        InitializeToolEditCommands();
        InitializeSectionsEditCommands();
        InitializeUTurnEditCommands();

        // Subscribe to config changes for HasUnsavedChanges notification
        Config.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ConfigurationStore.HasUnsavedChanges))
            {
                this.RaisePropertyChanged(nameof(HasUnsavedChanges));
            }
            // Update calculated section total when NumSections changes
            if (e.PropertyName == nameof(ConfigurationStore.NumSections))
            {
                this.RaisePropertyChanged(nameof(CalculatedSectionTotal));
            }
        };

        // Subscribe to tool changes for CalculatedSectionTotal
        Tool.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ToolConfig.DefaultSectionWidth))
            {
                this.RaisePropertyChanged(nameof(CalculatedSectionTotal));
            }
        };

        // Load available profiles
        RefreshProfileList();

        // Set selected profile name to current
        _selectedProfileName = Config.ActiveProfileName;
    }

    private void InitializeVehicleEditCommands()
    {
        // Vehicle dimensions stored in meters, edit in meters
        EditWheelbaseCommand = new RelayCommand(() =>
            ShowNumericInput("Wheelbase", Vehicle.Wheelbase,
                v => Vehicle.Wheelbase = v,
                "m", integerOnly: false, allowNegative: false, min: 0.5, max: 10));

        EditTrackWidthCommand = new RelayCommand(() =>
            ShowNumericInput("Track Width", Vehicle.TrackWidth,
                v => Vehicle.TrackWidth = v,
                "m", integerOnly: false, allowNegative: false, min: 0.5, max: 5));

        EditHitchLengthCommand = new RelayCommand(() =>
            ShowNumericInput("Hitch Length", Tool.HitchLength,
                v => Tool.HitchLength = v,
                "m", integerOnly: false, allowNegative: true, min: -15, max: 15));

        EditAntennaPivotCommand = new RelayCommand(() =>
            ShowNumericInput("Antenna Pivot", Vehicle.AntennaPivot,
                v => Vehicle.AntennaPivot = v,
                "m", integerOnly: false, allowNegative: true, min: -10, max: 10));

        EditAntennaHeightCommand = new RelayCommand(() =>
            ShowNumericInput("Antenna Height", Vehicle.AntennaHeight,
                v => Vehicle.AntennaHeight = v,
                "m", integerOnly: false, allowNegative: false, min: 0, max: 10));

        EditAntennaOffsetCommand = new RelayCommand(() =>
            ShowNumericInput("Antenna Offset", Vehicle.AntennaOffset,
                v => Vehicle.AntennaOffset = v,
                "m", integerOnly: false, allowNegative: true, min: -5, max: 5));
    }

    private void InitializeToolEditCommands()
    {
        // Tool dimensions stored in meters, edit in meters
        EditToolWidthCommand = new RelayCommand(() =>
            ShowNumericInput("Tool Width", Tool.Width,
                v => Tool.Width = v,
                "m", integerOnly: false, allowNegative: false, min: 0.5, max: 50));

        EditToolOverlapCommand = new RelayCommand(() =>
            ShowNumericInput("Tool Overlap", Tool.Overlap,
                v => Tool.Overlap = v,
                "m", integerOnly: false, allowNegative: true, min: -2, max: 2));

        EditToolOffsetCommand = new RelayCommand(() =>
            ShowNumericInput("Tool Offset", Tool.Offset,
                v => Tool.Offset = v,
                "m", integerOnly: false, allowNegative: true, min: -5, max: 5));

        EditToolHitchLengthCommand = new RelayCommand(() =>
            ShowNumericInput("Hitch Length", Tool.HitchLength,
                v => Tool.HitchLength = v,
                "m", integerOnly: false, allowNegative: true, min: -15, max: 15));

        EditTrailingHitchLengthCommand = new RelayCommand(() =>
            ShowNumericInput("Trailing Hitch Length", Tool.TrailingHitchLength,
                v => Tool.TrailingHitchLength = v,
                "m", integerOnly: false, allowNegative: true, min: -15, max: 15));

        EditTankHitchLengthCommand = new RelayCommand(() =>
            ShowNumericInput("Tank Hitch Length", Tool.TankTrailingHitchLength,
                v => Tool.TankTrailingHitchLength = v,
                "m", integerOnly: false, allowNegative: false, min: 0, max: 15));

        EditToolPivotCommand = new RelayCommand(() =>
            ShowNumericInput("Tool Pivot Distance", Tool.TrailingToolToPivotLength,
                v => Tool.TrailingToolToPivotLength = v,
                "m", integerOnly: false, allowNegative: true, min: -10, max: 10));
    }

    private void InitializeSectionsEditCommands()
    {
        EditNumSectionsCommand = new RelayCommand(() =>
            ShowNumericInput("Number of Sections", Config.NumSections,
                v => Config.NumSections = (int)v,
                "", integerOnly: true, allowNegative: false, min: 1, max: 16));

        EditLookAheadOnCommand = new RelayCommand(() =>
            ShowNumericInput("Look Ahead On", Tool.LookAheadOnSetting,
                v => Tool.LookAheadOnSetting = v,
                "s", integerOnly: false, allowNegative: false, min: 0, max: 5));

        EditLookAheadOffCommand = new RelayCommand(() =>
            ShowNumericInput("Look Ahead Off", Tool.LookAheadOffSetting,
                v => Tool.LookAheadOffSetting = v,
                "s", integerOnly: false, allowNegative: false, min: 0, max: 5));

        EditTurnOffDelayCommand = new RelayCommand(() =>
            ShowNumericInput("Turn Off Delay", Tool.TurnOffDelay,
                v => Tool.TurnOffDelay = v,
                "s", integerOnly: false, allowNegative: false, min: 0, max: 5));

        EditDefaultSectionWidthCommand = new RelayCommand(() =>
            ShowNumericInput("Default Section Width", Tool.DefaultSectionWidth,
                v => Tool.DefaultSectionWidth = v,
                "cm", integerOnly: false, allowNegative: false, min: 10, max: 500));

        EditMinCoverageCommand = new RelayCommand(() =>
            ShowNumericInput("Minimum Coverage", Tool.MinCoverage,
                v => Tool.MinCoverage = (int)v,
                "%", integerOnly: true, allowNegative: false, min: 0, max: 100));

        EditCutoffSpeedCommand = new RelayCommand(() =>
            ShowNumericInput("Slow Speed Cutoff", Tool.SlowSpeedCutoff,
                v => Tool.SlowSpeedCutoff = v,
                "km/h", integerOnly: false, allowNegative: false, min: 0, max: 10));
    }

    private void InitializeUTurnEditCommands()
    {
        EditUTurnRadiusCommand = new RelayCommand(() =>
            ShowNumericInput("U-Turn Radius", Guidance.UTurnRadius,
                v => Guidance.UTurnRadius = v,
                "m", integerOnly: false, allowNegative: false, min: 2, max: 30));

        EditUTurnExtensionCommand = new RelayCommand(() =>
            ShowNumericInput("U-Turn Extension", Guidance.UTurnExtension,
                v => Guidance.UTurnExtension = v,
                "m", integerOnly: false, allowNegative: false, min: 0, max: 50));

        EditUTurnDistanceCommand = new RelayCommand(() =>
            ShowNumericInput("Distance from Boundary", Guidance.UTurnDistanceFromBoundary,
                v => Guidance.UTurnDistanceFromBoundary = v,
                "m", integerOnly: false, allowNegative: false, min: 0, max: 10));

        EditUTurnSkipWidthCommand = new RelayCommand(() =>
            ShowNumericInput("Skip Width", Guidance.UTurnSkipWidth,
                v => Guidance.UTurnSkipWidth = (int)v,
                "", integerOnly: true, allowNegative: false, min: 1, max: 10));

        EditUTurnSmoothingCommand = new RelayCommand(() =>
            ShowNumericInput("Smoothing", Guidance.UTurnSmoothing,
                v => Guidance.UTurnSmoothing = (int)v,
                "", integerOnly: true, allowNegative: false, min: 1, max: 50));
    }

    private void RefreshProfileList()
    {
        AvailableProfiles.Clear();
        foreach (var profileName in _configService.GetAvailableProfiles())
        {
            AvailableProfiles.Add(profileName);
        }
    }

    private void LoadProfile(string? profileName)
    {
        if (string.IsNullOrEmpty(profileName)) return;
        _configService.LoadProfile(profileName);
        _selectedProfileName = profileName;
        this.RaisePropertyChanged(nameof(SelectedProfileName));
    }

    private void SaveProfile()
    {
        _configService.SaveProfile(Config.ActiveProfileName);
        ProfileSaved?.Invoke(this, Config.ActiveProfileName);
    }

    private void CreateNewProfile(string? profileName)
    {
        if (string.IsNullOrWhiteSpace(profileName)) return;

        _configService.CreateProfile(profileName);
        RefreshProfileList();
        _selectedProfileName = profileName;
        this.RaisePropertyChanged(nameof(SelectedProfileName));
    }

    private void DeleteProfile()
    {
        if (string.IsNullOrEmpty(SelectedProfileName)) return;
        if (SelectedProfileName == Config.ActiveProfileName) return; // Can't delete active

        _configService.DeleteProfile(SelectedProfileName);
        RefreshProfileList();
    }

    private void ApplyChanges()
    {
        _configService.SaveProfile(Config.ActiveProfileName);
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    private void Cancel()
    {
        _configService.ReloadCurrentProfile();
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    private void SetToolType(string? toolType)
    {
        if (string.IsNullOrEmpty(toolType)) return;
        Tool.SetToolType(toolType);
        Config.MarkChanged();
    }

    private void SetVehicleType(string? vehicleType)
    {
        if (string.IsNullOrEmpty(vehicleType)) return;

        Vehicle.Type = vehicleType.ToLowerInvariant() switch
        {
            "tractor" => VehicleType.Tractor,
            "harvester" => VehicleType.Harvester,
            "fourwd" or "4wd" or "articulated" => VehicleType.FourWD,
            _ => VehicleType.Tractor
        };
        Config.MarkChanged();
    }
}

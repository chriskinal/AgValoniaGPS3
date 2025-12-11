using System;
using System.Collections.ObjectModel;
using System.Windows.Input;
using ReactiveUI;
using AgValoniaGPS.Models;
using AgValoniaGPS.Models.Tool;
using AgValoniaGPS.Services.Interfaces;

namespace AgValoniaGPS.ViewModels;

/// <summary>
/// ViewModel for the Configuration Dialog
/// Manages vehicle, tool, sections, and system configuration
/// </summary>
public class ConfigurationViewModel : ReactiveObject
{
    private readonly IVehicleProfileService _profileService;
    private readonly ISettingsService _settingsService;

    // Working copy of the profile being edited
    private VehicleProfile? _workingProfile;

    #region Dialog Visibility

    private bool _isDialogVisible;
    public bool IsDialogVisible
    {
        get => _isDialogVisible;
        set => this.RaiseAndSetIfChanged(ref _isDialogVisible, value);
    }

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
            if (value != null)
            {
                LoadProfile(value);
            }
        }
    }

    private bool _hasUnsavedChanges;
    public bool HasUnsavedChanges
    {
        get => _hasUnsavedChanges;
        set => this.RaiseAndSetIfChanged(ref _hasUnsavedChanges, value);
    }

    #endregion

    #region Vehicle Settings

    private VehicleType _vehicleType;
    public VehicleType VehicleType
    {
        get => _vehicleType;
        set
        {
            var oldValue = _vehicleType;
            this.RaiseAndSetIfChanged(ref _vehicleType, value);
            if (oldValue != value)
            {
                // Notify that dependent properties have changed
                this.RaisePropertyChanged(nameof(WheelbaseImageSource));
                this.RaisePropertyChanged(nameof(AntennaImageSource));
                this.RaisePropertyChanged(nameof(VehicleTypeDisplayName));
            }
            MarkChanged();
        }
    }

    /// <summary>
    /// Gets the image source for the wheelbase/track diagram based on vehicle type
    /// </summary>
    public string WheelbaseImageSource => VehicleType switch
    {
        VehicleType.Harvester => "avares://AgValoniaGPS.Views/Assets/Icons/RadiusWheelBaseHarvester.png",
        VehicleType.FourWD => "avares://AgValoniaGPS.Views/Assets/Icons/RadiusWheelBaseArticulated.png",
        _ => "avares://AgValoniaGPS.Views/Assets/Icons/RadiusWheelBase.png"
    };

    /// <summary>
    /// Gets the image source for the antenna position diagram based on vehicle type
    /// </summary>
    public string AntennaImageSource => VehicleType switch
    {
        VehicleType.Harvester => "avares://AgValoniaGPS.Views/Assets/Icons/AntennaHarvester.png",
        VehicleType.FourWD => "avares://AgValoniaGPS.Views/Assets/Icons/AntennaArticulated.png",
        _ => "avares://AgValoniaGPS.Views/Assets/Icons/AntennaTractor.png"
    };

    /// <summary>
    /// Gets a user-friendly display name for the current vehicle type
    /// </summary>
    public string VehicleTypeDisplayName => VehicleType switch
    {
        VehicleType.Harvester => "Harvester",
        VehicleType.FourWD => "Articulated",
        _ => "Tractor"
    };

    private double _antennaHeight = 3.0;
    public double AntennaHeight
    {
        get => _antennaHeight;
        set { this.RaiseAndSetIfChanged(ref _antennaHeight, value); MarkChanged(); }
    }

    private double _antennaOffset;
    public double AntennaOffset
    {
        get => _antennaOffset;
        set { this.RaiseAndSetIfChanged(ref _antennaOffset, value); MarkChanged(); }
    }

    private double _antennaPivot = 0.6;
    public double AntennaPivot
    {
        get => _antennaPivot;
        set { this.RaiseAndSetIfChanged(ref _antennaPivot, value); MarkChanged(); }
    }

    private double _wheelbase = 2.5;
    public double Wheelbase
    {
        get => _wheelbase;
        set { this.RaiseAndSetIfChanged(ref _wheelbase, value); MarkChanged(); }
    }

    private double _trackWidth = 1.8;
    public double TrackWidth
    {
        get => _trackWidth;
        set { this.RaiseAndSetIfChanged(ref _trackWidth, value); MarkChanged(); }
    }

    private double _maxSteerAngle = 35.0;
    public double MaxSteerAngle
    {
        get => _maxSteerAngle;
        set { this.RaiseAndSetIfChanged(ref _maxSteerAngle, value); MarkChanged(); }
    }

    private double _lookAheadHold = 4.0;
    public double LookAheadHold
    {
        get => _lookAheadHold;
        set { this.RaiseAndSetIfChanged(ref _lookAheadHold, value); MarkChanged(); }
    }

    private double _lookAheadMult = 1.4;
    public double LookAheadMult
    {
        get => _lookAheadMult;
        set { this.RaiseAndSetIfChanged(ref _lookAheadMult, value); MarkChanged(); }
    }

    private double _acquireFactor = 1.5;
    public double AcquireFactor
    {
        get => _acquireFactor;
        set { this.RaiseAndSetIfChanged(ref _acquireFactor, value); MarkChanged(); }
    }

    private bool _isPurePursuit = true;
    public bool IsPurePursuit
    {
        get => _isPurePursuit;
        set { this.RaiseAndSetIfChanged(ref _isPurePursuit, value); MarkChanged(); }
    }

    #endregion

    #region Tool Settings

    private double _toolWidth = 6.0;
    public double ToolWidth
    {
        get => _toolWidth;
        set { this.RaiseAndSetIfChanged(ref _toolWidth, value); MarkChanged(); }
    }

    private double _toolOverlap;
    public double ToolOverlap
    {
        get => _toolOverlap;
        set { this.RaiseAndSetIfChanged(ref _toolOverlap, value); MarkChanged(); }
    }

    private double _toolOffset;
    public double ToolOffset
    {
        get => _toolOffset;
        set { this.RaiseAndSetIfChanged(ref _toolOffset, value); MarkChanged(); }
    }

    private double _hitchLength = -1.8;
    public double HitchLength
    {
        get => _hitchLength;
        set { this.RaiseAndSetIfChanged(ref _hitchLength, value); MarkChanged(); }
    }

    private double _trailingHitchLength = -2.5;
    public double TrailingHitchLength
    {
        get => _trailingHitchLength;
        set { this.RaiseAndSetIfChanged(ref _trailingHitchLength, value); MarkChanged(); }
    }

    private double _tankTrailingHitchLength = 3.0;
    public double TankTrailingHitchLength
    {
        get => _tankTrailingHitchLength;
        set { this.RaiseAndSetIfChanged(ref _tankTrailingHitchLength, value); MarkChanged(); }
    }

    private double _trailingToolToPivotLength;
    public double TrailingToolToPivotLength
    {
        get => _trailingToolToPivotLength;
        set { this.RaiseAndSetIfChanged(ref _trailingToolToPivotLength, value); MarkChanged(); }
    }

    private bool _isToolTrailing;
    public bool IsToolTrailing
    {
        get => _isToolTrailing;
        set { this.RaiseAndSetIfChanged(ref _isToolTrailing, value); MarkChanged(); UpdateToolTypeFlags(); }
    }

    private bool _isToolTBT;
    public bool IsToolTBT
    {
        get => _isToolTBT;
        set { this.RaiseAndSetIfChanged(ref _isToolTBT, value); MarkChanged(); UpdateToolTypeFlags(); }
    }

    private bool _isToolRearFixed = true;
    public bool IsToolRearFixed
    {
        get => _isToolRearFixed;
        set { this.RaiseAndSetIfChanged(ref _isToolRearFixed, value); MarkChanged(); UpdateToolTypeFlags(); }
    }

    private bool _isToolFrontFixed;
    public bool IsToolFrontFixed
    {
        get => _isToolFrontFixed;
        set { this.RaiseAndSetIfChanged(ref _isToolFrontFixed, value); MarkChanged(); UpdateToolTypeFlags(); }
    }

    #endregion

    #region Section Settings

    private int _numSections = 1;
    public int NumSections
    {
        get => _numSections;
        set { this.RaiseAndSetIfChanged(ref _numSections, Math.Clamp(value, 1, 16)); MarkChanged(); }
    }

    private double[] _sectionPositions = new double[17];
    public double[] SectionPositions
    {
        get => _sectionPositions;
        set { this.RaiseAndSetIfChanged(ref _sectionPositions, value); MarkChanged(); }
    }

    private double _lookAheadOn = 1.0;
    public double LookAheadOn
    {
        get => _lookAheadOn;
        set { this.RaiseAndSetIfChanged(ref _lookAheadOn, value); MarkChanged(); }
    }

    private double _lookAheadOff = 0.5;
    public double LookAheadOff
    {
        get => _lookAheadOff;
        set { this.RaiseAndSetIfChanged(ref _lookAheadOff, value); MarkChanged(); }
    }

    private double _turnOffDelay;
    public double TurnOffDelay
    {
        get => _turnOffDelay;
        set { this.RaiseAndSetIfChanged(ref _turnOffDelay, value); MarkChanged(); }
    }

    #endregion

    #region U-Turn Settings

    private double _uTurnRadius = 8.0;
    public double UTurnRadius
    {
        get => _uTurnRadius;
        set { this.RaiseAndSetIfChanged(ref _uTurnRadius, value); MarkChanged(); }
    }

    private double _uTurnExtension = 20.0;
    public double UTurnExtension
    {
        get => _uTurnExtension;
        set { this.RaiseAndSetIfChanged(ref _uTurnExtension, value); MarkChanged(); }
    }

    private double _uTurnDistanceFromBoundary = 2.0;
    public double UTurnDistanceFromBoundary
    {
        get => _uTurnDistanceFromBoundary;
        set { this.RaiseAndSetIfChanged(ref _uTurnDistanceFromBoundary, value); MarkChanged(); }
    }

    private int _uTurnSkipWidth = 1;
    public int UTurnSkipWidth
    {
        get => _uTurnSkipWidth;
        set { this.RaiseAndSetIfChanged(ref _uTurnSkipWidth, Math.Max(1, value)); MarkChanged(); }
    }

    private int _uTurnStyle;
    public int UTurnStyle
    {
        get => _uTurnStyle;
        set { this.RaiseAndSetIfChanged(ref _uTurnStyle, value); MarkChanged(); }
    }

    private int _uTurnSmoothing = 14;
    public int UTurnSmoothing
    {
        get => _uTurnSmoothing;
        set { this.RaiseAndSetIfChanged(ref _uTurnSmoothing, Math.Clamp(value, 1, 50)); MarkChanged(); }
    }

    #endregion

    #region Display Settings

    private bool _isMetric;
    public bool IsMetric
    {
        get => _isMetric;
        set { this.RaiseAndSetIfChanged(ref _isMetric, value); MarkChanged(); }
    }

    private bool _gridVisible = true;
    public bool GridVisible
    {
        get => _gridVisible;
        set { this.RaiseAndSetIfChanged(ref _gridVisible, value); MarkChanged(); }
    }

    private bool _compassVisible = true;
    public bool CompassVisible
    {
        get => _compassVisible;
        set { this.RaiseAndSetIfChanged(ref _compassVisible, value); MarkChanged(); }
    }

    private bool _speedVisible = true;
    public bool SpeedVisible
    {
        get => _speedVisible;
        set { this.RaiseAndSetIfChanged(ref _speedVisible, value); MarkChanged(); }
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

    #endregion

    #region Dialog Control

    public event EventHandler? CloseRequested;
    public event EventHandler<string>? ProfileSaved;

    #endregion

    public ConfigurationViewModel(
        IVehicleProfileService profileService,
        ISettingsService settingsService)
    {
        _profileService = profileService;
        _settingsService = settingsService;

        // Initialize commands
        LoadProfileCommand = new RelayCommand<string>(LoadProfile);
        SaveProfileCommand = new RelayCommand(SaveProfile);
        NewProfileCommand = new RelayCommand<string>(CreateNewProfile);
        DeleteProfileCommand = new RelayCommand(DeleteProfile);
        ApplyCommand = new RelayCommand(ApplyChanges);
        CancelCommand = new RelayCommand(Cancel);
        SetToolTypeCommand = new RelayCommand<string>(SetToolType);
        SetVehicleTypeCommand = new RelayCommand<string>(SetVehicleType);

        // Load available profiles
        RefreshProfileList();

        // Load active profile if exists
        if (_profileService.ActiveProfile != null)
        {
            _selectedProfileName = _profileService.ActiveProfile.Name;
            _workingProfile = _profileService.ActiveProfile;
            LoadFromProfile(_profileService.ActiveProfile);
        }
        else
        {
            // Create a default profile if none exists
            _workingProfile = _profileService.CreateDefaultProfile("Default");
            _selectedProfileName = _workingProfile.Name;
            LoadFromProfile(_workingProfile);
        }
    }

    private void RefreshProfileList()
    {
        AvailableProfiles.Clear();
        foreach (var profileName in _profileService.GetAvailableProfiles())
        {
            AvailableProfiles.Add(profileName);
        }
    }

    private void LoadProfile(string? profileName)
    {
        if (string.IsNullOrEmpty(profileName)) return;

        var profile = _profileService.Load(profileName);
        if (profile != null)
        {
            _workingProfile = profile;
            LoadFromProfile(profile);
            HasUnsavedChanges = false;
        }
    }

    private void LoadFromProfile(VehicleProfile profile)
    {
        // Suppress change tracking during load
        _isLoading = true;

        try
        {
            // Vehicle settings
            VehicleType = profile.Vehicle.Type;
            AntennaHeight = profile.Vehicle.AntennaHeight;
            AntennaOffset = profile.Vehicle.AntennaOffset;
            AntennaPivot = profile.Vehicle.AntennaPivot;
            Wheelbase = profile.Vehicle.Wheelbase;
            TrackWidth = profile.Vehicle.TrackWidth;
            MaxSteerAngle = profile.Vehicle.MaxSteerAngle;
            LookAheadHold = profile.Vehicle.GoalPointLookAheadHold;
            LookAheadMult = profile.Vehicle.GoalPointLookAheadMult;
            AcquireFactor = profile.Vehicle.GoalPointAcquireFactor;
            IsPurePursuit = profile.IsPurePursuit;

            // Tool settings
            ToolWidth = profile.Tool.Width;
            ToolOverlap = profile.Tool.Overlap;
            ToolOffset = profile.Tool.Offset;
            HitchLength = profile.Tool.HitchLength;
            TrailingHitchLength = profile.Tool.TrailingHitchLength;
            TankTrailingHitchLength = profile.Tool.TankTrailingHitchLength;
            TrailingToolToPivotLength = profile.Tool.TrailingToolToPivotLength;
            IsToolTrailing = profile.Tool.IsToolTrailing;
            IsToolTBT = profile.Tool.IsToolTBT;
            IsToolRearFixed = profile.Tool.IsToolRearFixed;
            IsToolFrontFixed = profile.Tool.IsToolFrontFixed;

            // Section settings
            NumSections = profile.NumSections;
            SectionPositions = (double[])profile.SectionPositions.Clone();
            LookAheadOn = profile.Tool.LookAheadOnSetting;
            LookAheadOff = profile.Tool.LookAheadOffSetting;
            TurnOffDelay = profile.Tool.TurnOffDelay;

            // U-Turn settings
            UTurnRadius = profile.YouTurn.TurnRadius;
            UTurnExtension = profile.YouTurn.ExtensionLength;
            UTurnDistanceFromBoundary = profile.YouTurn.DistanceFromBoundary;
            UTurnSkipWidth = profile.YouTurn.SkipWidth;
            UTurnStyle = profile.YouTurn.Style;
            UTurnSmoothing = profile.YouTurn.Smoothing;

            // Display settings
            IsMetric = profile.IsMetric;
        }
        finally
        {
            _isLoading = false;
        }
    }

    private bool _isLoading;

    private void MarkChanged()
    {
        if (!_isLoading)
        {
            HasUnsavedChanges = true;
        }
    }

    private void SaveToProfile(VehicleProfile profile)
    {
        // Vehicle settings
        profile.Vehicle.Type = VehicleType;
        profile.Vehicle.AntennaHeight = AntennaHeight;
        profile.Vehicle.AntennaOffset = AntennaOffset;
        profile.Vehicle.AntennaPivot = AntennaPivot;
        profile.Vehicle.Wheelbase = Wheelbase;
        profile.Vehicle.TrackWidth = TrackWidth;
        profile.Vehicle.MaxSteerAngle = MaxSteerAngle;
        profile.Vehicle.GoalPointLookAheadHold = LookAheadHold;
        profile.Vehicle.GoalPointLookAheadMult = LookAheadMult;
        profile.Vehicle.GoalPointAcquireFactor = AcquireFactor;
        profile.IsPurePursuit = IsPurePursuit;

        // Tool settings
        profile.Tool.Width = ToolWidth;
        profile.Tool.HalfWidth = ToolWidth / 2.0;
        profile.Tool.Overlap = ToolOverlap;
        profile.Tool.Offset = ToolOffset;
        profile.Tool.HitchLength = HitchLength;
        profile.Tool.TrailingHitchLength = TrailingHitchLength;
        profile.Tool.TankTrailingHitchLength = TankTrailingHitchLength;
        profile.Tool.TrailingToolToPivotLength = TrailingToolToPivotLength;
        profile.Tool.IsToolTrailing = IsToolTrailing;
        profile.Tool.IsToolTBT = IsToolTBT;
        profile.Tool.IsToolRearFixed = IsToolRearFixed;
        profile.Tool.IsToolFrontFixed = IsToolFrontFixed;

        // Section settings
        profile.NumSections = NumSections;
        profile.SectionPositions = (double[])SectionPositions.Clone();
        profile.Tool.LookAheadOnSetting = LookAheadOn;
        profile.Tool.LookAheadOffSetting = LookAheadOff;
        profile.Tool.TurnOffDelay = TurnOffDelay;

        // U-Turn settings
        profile.YouTurn.TurnRadius = UTurnRadius;
        profile.YouTurn.ExtensionLength = UTurnExtension;
        profile.YouTurn.DistanceFromBoundary = UTurnDistanceFromBoundary;
        profile.YouTurn.SkipWidth = UTurnSkipWidth;
        profile.YouTurn.Style = UTurnStyle;
        profile.YouTurn.Smoothing = UTurnSmoothing;

        // Display settings
        profile.IsMetric = IsMetric;
    }

    private void SaveProfile()
    {
        if (_workingProfile == null) return;

        SaveToProfile(_workingProfile);
        _profileService.Save(_workingProfile);
        HasUnsavedChanges = false;
        ProfileSaved?.Invoke(this, _workingProfile.Name);
    }

    private void CreateNewProfile(string? profileName)
    {
        if (string.IsNullOrWhiteSpace(profileName)) return;

        var profile = _profileService.CreateDefaultProfile(profileName);
        _profileService.Save(profile);
        RefreshProfileList();
        SelectedProfileName = profileName;
    }

    private void DeleteProfile()
    {
        // TODO: Implement profile deletion with confirmation
    }

    private void ApplyChanges()
    {
        if (_workingProfile == null) return;

        SaveToProfile(_workingProfile);
        _profileService.Save(_workingProfile);
        _profileService.SetActiveProfile(_workingProfile.Name);
        HasUnsavedChanges = false;
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    private void Cancel()
    {
        // Revert to saved state
        if (_workingProfile != null)
        {
            LoadFromProfile(_workingProfile);
        }
        HasUnsavedChanges = false;
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    private void SetToolType(string? toolType)
    {
        if (string.IsNullOrEmpty(toolType)) return;

        // Clear all flags first
        _isLoading = true;
        IsToolTrailing = false;
        IsToolTBT = false;
        IsToolRearFixed = false;
        IsToolFrontFixed = false;
        _isLoading = false;

        // Set the appropriate flag
        switch (toolType.ToLower())
        {
            case "front":
                IsToolFrontFixed = true;
                break;
            case "rear":
                IsToolRearFixed = true;
                break;
            case "tbt":
                IsToolTBT = true;
                break;
            case "trailing":
                IsToolTrailing = true;
                break;
        }
    }

    private void SetVehicleType(string? vehicleType)
    {
        if (string.IsNullOrEmpty(vehicleType)) return;

        VehicleType = vehicleType.ToLower() switch
        {
            "tractor" => VehicleType.Tractor,
            "harvester" => VehicleType.Harvester,
            "fourwd" or "4wd" or "articulated" => VehicleType.FourWD,
            _ => VehicleType.Tractor
        };
    }

    private void UpdateToolTypeFlags()
    {
        // Ensure only one tool type is selected
        // This is called when any tool type flag changes
    }

    /// <summary>
    /// Gets the current tool type as a string for display
    /// </summary>
    public string CurrentToolType
    {
        get
        {
            if (IsToolFrontFixed) return "Front Fixed";
            if (IsToolRearFixed) return "Rear Fixed";
            if (IsToolTBT) return "TBT";
            if (IsToolTrailing) return "Trailing";
            return "None";
        }
    }
}

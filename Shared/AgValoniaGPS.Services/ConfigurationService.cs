using System;
using System.Collections.Generic;
using System.IO;
using AgValoniaGPS.Models;
using AgValoniaGPS.Models.Configuration;
using AgValoniaGPS.Services.Interfaces;

namespace AgValoniaGPS.Services;

/// <summary>
/// Service for managing the unified configuration store.
/// Bridges between ConfigurationStore and existing persistence services
/// to maintain AgOpenGPS XML compatibility.
/// </summary>
public class ConfigurationService : IConfigurationService
{
    private readonly IVehicleProfileService _profileService;
    private readonly ISettingsService _settingsService;

    public ConfigurationStore Store => ConfigurationStore.Instance;

    public string ProfilesDirectory => _profileService.VehiclesDirectory;

    public event EventHandler<string>? ProfileLoaded;
    public event EventHandler<string>? ProfileSaved;

    public ConfigurationService(
        IVehicleProfileService profileService,
        ISettingsService settingsService)
    {
        _profileService = profileService;
        _settingsService = settingsService;
    }

    #region Profile Management

    public IReadOnlyList<string> GetAvailableProfiles()
    {
        return _profileService.GetAvailableProfiles();
    }

    public bool LoadProfile(string name)
    {
        var profile = _profileService.Load(name);
        if (profile == null)
            return false;

        ApplyProfileToStore(profile);
        Store.ActiveProfileName = name;
        Store.ActiveProfilePath = profile.FilePath;
        Store.HasUnsavedChanges = false;
        Store.OnProfileLoaded();
        ProfileLoaded?.Invoke(this, name);
        return true;
    }

    public void SaveProfile(string name)
    {
        var profile = CreateProfileFromStore(name);
        _profileService.Save(profile);
        Store.HasUnsavedChanges = false;
        Store.OnProfileSaved();
        ProfileSaved?.Invoke(this, name);
    }

    public void CreateProfile(string name)
    {
        var profile = _profileService.CreateDefaultProfile(name);
        ApplyProfileToStore(profile);
        Store.ActiveProfileName = name;
        Store.ActiveProfilePath = profile.FilePath;
        Store.HasUnsavedChanges = false;
    }

    public bool DeleteProfile(string name)
    {
        var filePath = Path.Combine(ProfilesDirectory, $"{name}.XML");
        if (!File.Exists(filePath))
            return false;

        try
        {
            File.Delete(filePath);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public void ReloadCurrentProfile()
    {
        if (!string.IsNullOrEmpty(Store.ActiveProfileName))
        {
            LoadProfile(Store.ActiveProfileName);
        }
    }

    #endregion

    #region App Settings Management

    public void LoadAppSettings()
    {
        _settingsService.Load();
        ApplyAppSettingsToStore(_settingsService.Settings);
    }

    public void SaveAppSettings()
    {
        ApplyStoreToAppSettings(_settingsService.Settings);
        _settingsService.Save();
    }

    #endregion

    #region Profile <-> Store Mapping

    /// <summary>
    /// Applies a VehicleProfile to the ConfigurationStore
    /// </summary>
    private void ApplyProfileToStore(VehicleProfile profile)
    {
        var store = Store;

        // Vehicle config
        store.Vehicle.Name = profile.Name;
        store.Vehicle.Type = profile.Vehicle.Type;
        store.Vehicle.AntennaHeight = profile.Vehicle.AntennaHeight;
        store.Vehicle.AntennaPivot = profile.Vehicle.AntennaPivot;
        store.Vehicle.AntennaOffset = profile.Vehicle.AntennaOffset;
        store.Vehicle.Wheelbase = profile.Vehicle.Wheelbase;
        store.Vehicle.TrackWidth = profile.Vehicle.TrackWidth;
        store.Vehicle.MaxSteerAngle = profile.Vehicle.MaxSteerAngle;
        store.Vehicle.MaxAngularVelocity = profile.Vehicle.MaxAngularVelocity;

        // Guidance config
        store.Guidance.IsPurePursuit = profile.IsPurePursuit;
        store.Guidance.GoalPointLookAheadHold = profile.Vehicle.GoalPointLookAheadHold;
        store.Guidance.GoalPointLookAheadMult = profile.Vehicle.GoalPointLookAheadMult;
        store.Guidance.GoalPointAcquireFactor = profile.Vehicle.GoalPointAcquireFactor;
        store.Guidance.MinLookAheadDistance = profile.Vehicle.MinLookAheadDistance;
        store.Guidance.StanleyDistanceErrorGain = profile.Vehicle.StanleyDistanceErrorGain;
        store.Guidance.StanleyHeadingErrorGain = profile.Vehicle.StanleyHeadingErrorGain;
        store.Guidance.StanleyIntegralGainAB = profile.Vehicle.StanleyIntegralGainAB;
        store.Guidance.StanleyIntegralDistanceAwayTriggerAB = profile.Vehicle.StanleyIntegralDistanceAwayTriggerAB;
        store.Guidance.PurePursuitIntegralGain = profile.Vehicle.PurePursuitIntegralGain;
        store.Guidance.DeadZoneHeading = profile.Vehicle.DeadZoneHeading;
        store.Guidance.DeadZoneDelay = profile.Vehicle.DeadZoneDelay;
        store.Guidance.HydLiftLookAheadDistanceLeft = profile.Vehicle.HydLiftLookAheadDistanceLeft;
        store.Guidance.HydLiftLookAheadDistanceRight = profile.Vehicle.HydLiftLookAheadDistanceRight;

        // U-Turn settings
        store.Guidance.UTurnRadius = profile.YouTurn.TurnRadius;
        store.Guidance.UTurnExtension = profile.YouTurn.ExtensionLength;
        store.Guidance.UTurnDistanceFromBoundary = profile.YouTurn.DistanceFromBoundary;
        store.Guidance.UTurnSkipWidth = profile.YouTurn.SkipWidth;
        store.Guidance.UTurnStyle = profile.YouTurn.Style;
        store.Guidance.UTurnSmoothing = profile.YouTurn.Smoothing;
        store.Guidance.UTurnCompensation = profile.Vehicle.UTurnCompensation;

        // Tool config
        store.Tool.Width = profile.Tool.Width;
        store.Tool.Overlap = profile.Tool.Overlap;
        store.Tool.Offset = profile.Tool.Offset;
        store.Tool.HitchLength = profile.Tool.HitchLength;
        store.Tool.TrailingHitchLength = profile.Tool.TrailingHitchLength;
        store.Tool.TankTrailingHitchLength = profile.Tool.TankTrailingHitchLength;
        store.Tool.TrailingToolToPivotLength = profile.Tool.TrailingToolToPivotLength;
        store.Tool.IsToolTrailing = profile.Tool.IsToolTrailing;
        store.Tool.IsToolTBT = profile.Tool.IsToolTBT;
        store.Tool.IsToolRearFixed = profile.Tool.IsToolRearFixed;
        store.Tool.IsToolFrontFixed = profile.Tool.IsToolFrontFixed;
        store.Tool.LookAheadOnSetting = profile.Tool.LookAheadOnSetting;
        store.Tool.LookAheadOffSetting = profile.Tool.LookAheadOffSetting;
        store.Tool.TurnOffDelay = profile.Tool.TurnOffDelay;
        store.Tool.MinCoverage = profile.Tool.MinCoverage;
        store.Tool.IsMultiColoredSections = profile.Tool.IsMultiColoredSections;
        store.Tool.IsSectionOffWhenOut = profile.Tool.IsSectionOffWhenOut;
        store.Tool.IsHeadlandSectionControl = profile.Tool.IsHeadlandSectionControl;

        // Section config
        store.NumSections = profile.NumSections;
        store.SectionPositions = (double[])profile.SectionPositions.Clone();

        // Simulator config
        store.Simulator.Enabled = profile.IsSimulatorOn;
        store.Simulator.Latitude = profile.SimLatitude;
        store.Simulator.Longitude = profile.SimLongitude;

        // Display config
        store.IsMetric = profile.IsMetric;
    }

    /// <summary>
    /// Creates a VehicleProfile from the current ConfigurationStore state
    /// </summary>
    private VehicleProfile CreateProfileFromStore(string name)
    {
        var store = Store;

        var profile = new VehicleProfile
        {
            Name = name,
            FilePath = Path.Combine(ProfilesDirectory, $"{name}.XML"),
            IsMetric = store.IsMetric,
            IsPurePursuit = store.Guidance.IsPurePursuit,
            IsSimulatorOn = store.Simulator.Enabled,
            SimLatitude = store.Simulator.Latitude,
            SimLongitude = store.Simulator.Longitude,
            NumSections = store.NumSections,
            SectionPositions = (double[])store.SectionPositions.Clone()
        };

        // Vehicle configuration
        profile.Vehicle.Type = store.Vehicle.Type;
        profile.Vehicle.AntennaHeight = store.Vehicle.AntennaHeight;
        profile.Vehicle.AntennaPivot = store.Vehicle.AntennaPivot;
        profile.Vehicle.AntennaOffset = store.Vehicle.AntennaOffset;
        profile.Vehicle.Wheelbase = store.Vehicle.Wheelbase;
        profile.Vehicle.TrackWidth = store.Vehicle.TrackWidth;
        profile.Vehicle.MaxSteerAngle = store.Vehicle.MaxSteerAngle;
        profile.Vehicle.MaxAngularVelocity = store.Vehicle.MaxAngularVelocity;
        profile.Vehicle.GoalPointLookAheadHold = store.Guidance.GoalPointLookAheadHold;
        profile.Vehicle.GoalPointLookAheadMult = store.Guidance.GoalPointLookAheadMult;
        profile.Vehicle.GoalPointAcquireFactor = store.Guidance.GoalPointAcquireFactor;
        profile.Vehicle.MinLookAheadDistance = store.Guidance.MinLookAheadDistance;
        profile.Vehicle.StanleyDistanceErrorGain = store.Guidance.StanleyDistanceErrorGain;
        profile.Vehicle.StanleyHeadingErrorGain = store.Guidance.StanleyHeadingErrorGain;
        profile.Vehicle.StanleyIntegralGainAB = store.Guidance.StanleyIntegralGainAB;
        profile.Vehicle.StanleyIntegralDistanceAwayTriggerAB = store.Guidance.StanleyIntegralDistanceAwayTriggerAB;
        profile.Vehicle.PurePursuitIntegralGain = store.Guidance.PurePursuitIntegralGain;
        profile.Vehicle.DeadZoneHeading = store.Guidance.DeadZoneHeading;
        profile.Vehicle.DeadZoneDelay = store.Guidance.DeadZoneDelay;
        profile.Vehicle.UTurnCompensation = store.Guidance.UTurnCompensation;
        profile.Vehicle.HydLiftLookAheadDistanceLeft = store.Guidance.HydLiftLookAheadDistanceLeft;
        profile.Vehicle.HydLiftLookAheadDistanceRight = store.Guidance.HydLiftLookAheadDistanceRight;

        // Tool configuration
        profile.Tool.Width = store.Tool.Width;
        profile.Tool.HalfWidth = store.Tool.Width / 2.0;
        profile.Tool.Overlap = store.Tool.Overlap;
        profile.Tool.Offset = store.Tool.Offset;
        profile.Tool.HitchLength = store.Tool.HitchLength;
        profile.Tool.TrailingHitchLength = store.Tool.TrailingHitchLength;
        profile.Tool.TankTrailingHitchLength = store.Tool.TankTrailingHitchLength;
        profile.Tool.TrailingToolToPivotLength = store.Tool.TrailingToolToPivotLength;
        profile.Tool.IsToolTrailing = store.Tool.IsToolTrailing;
        profile.Tool.IsToolTBT = store.Tool.IsToolTBT;
        profile.Tool.IsToolRearFixed = store.Tool.IsToolRearFixed;
        profile.Tool.IsToolFrontFixed = store.Tool.IsToolFrontFixed;
        profile.Tool.LookAheadOnSetting = store.Tool.LookAheadOnSetting;
        profile.Tool.LookAheadOffSetting = store.Tool.LookAheadOffSetting;
        profile.Tool.TurnOffDelay = store.Tool.TurnOffDelay;
        profile.Tool.NumOfSections = store.NumSections;
        profile.Tool.MinCoverage = store.Tool.MinCoverage;
        profile.Tool.IsMultiColoredSections = store.Tool.IsMultiColoredSections;
        profile.Tool.IsSectionOffWhenOut = store.Tool.IsSectionOffWhenOut;
        profile.Tool.IsHeadlandSectionControl = store.Tool.IsHeadlandSectionControl;

        // YouTurn configuration
        profile.YouTurn.TurnRadius = store.Guidance.UTurnRadius;
        profile.YouTurn.ExtensionLength = store.Guidance.UTurnExtension;
        profile.YouTurn.DistanceFromBoundary = store.Guidance.UTurnDistanceFromBoundary;
        profile.YouTurn.SkipWidth = store.Guidance.UTurnSkipWidth;
        profile.YouTurn.Style = store.Guidance.UTurnStyle;
        profile.YouTurn.Smoothing = store.Guidance.UTurnSmoothing;
        profile.YouTurn.UTurnCompensation = store.Guidance.UTurnCompensation;

        return profile;
    }

    #endregion

    #region AppSettings <-> Store Mapping

    /// <summary>
    /// Applies AppSettings to the ConfigurationStore
    /// </summary>
    private void ApplyAppSettingsToStore(AppSettings settings)
    {
        var store = Store;

        // Display config
        store.Display.WindowWidth = settings.WindowWidth;
        store.Display.WindowHeight = settings.WindowHeight;
        store.Display.WindowX = settings.WindowX;
        store.Display.WindowY = settings.WindowY;
        store.Display.WindowMaximized = settings.WindowMaximized;
        store.Display.SimulatorPanelX = settings.SimulatorPanelX;
        store.Display.SimulatorPanelY = settings.SimulatorPanelY;
        store.Display.SimulatorPanelVisible = settings.SimulatorPanelVisible;
        store.Display.GridVisible = settings.GridVisible;
        store.Display.CompassVisible = settings.CompassVisible;
        store.Display.SpeedVisible = settings.SpeedVisible;
        store.Display.CameraZoom = settings.CameraZoom;
        store.Display.CameraPitch = settings.CameraPitch;

        // Connection config
        store.Connections.NtripCasterHost = settings.NtripCasterIp;
        store.Connections.NtripCasterPort = settings.NtripCasterPort;
        store.Connections.NtripMountPoint = settings.NtripMountPoint;
        store.Connections.NtripUsername = settings.NtripUsername;
        store.Connections.NtripPassword = settings.NtripPassword;
        store.Connections.NtripAutoConnect = settings.NtripAutoConnect;
        store.Connections.AgShareServer = settings.AgShareServer;
        store.Connections.AgShareApiKey = settings.AgShareApiKey;
        store.Connections.AgShareEnabled = settings.AgShareEnabled;
        store.Connections.GpsUpdateRate = settings.GpsUpdateRate;
        store.Connections.UseRtk = settings.UseRtk;

        // Simulator config (AppSettings values as fallback if no profile loaded)
        if (!store.Simulator.Enabled && settings.SimulatorEnabled)
        {
            store.Simulator.Enabled = settings.SimulatorEnabled;
            store.Simulator.Latitude = settings.SimulatorLatitude;
            store.Simulator.Longitude = settings.SimulatorLongitude;
            store.Simulator.Speed = settings.SimulatorSpeed;
            store.Simulator.SteerAngle = settings.SimulatorSteerAngle;
        }
    }

    /// <summary>
    /// Applies ConfigurationStore to AppSettings
    /// </summary>
    private void ApplyStoreToAppSettings(AppSettings settings)
    {
        var store = Store;

        // Display config
        settings.WindowWidth = store.Display.WindowWidth;
        settings.WindowHeight = store.Display.WindowHeight;
        settings.WindowX = store.Display.WindowX;
        settings.WindowY = store.Display.WindowY;
        settings.WindowMaximized = store.Display.WindowMaximized;
        settings.SimulatorPanelX = store.Display.SimulatorPanelX;
        settings.SimulatorPanelY = store.Display.SimulatorPanelY;
        settings.SimulatorPanelVisible = store.Display.SimulatorPanelVisible;
        settings.GridVisible = store.Display.GridVisible;
        settings.CompassVisible = store.Display.CompassVisible;
        settings.SpeedVisible = store.Display.SpeedVisible;
        settings.CameraZoom = store.Display.CameraZoom;
        settings.CameraPitch = store.Display.CameraPitch;

        // Connection config
        settings.NtripCasterIp = store.Connections.NtripCasterHost;
        settings.NtripCasterPort = store.Connections.NtripCasterPort;
        settings.NtripMountPoint = store.Connections.NtripMountPoint;
        settings.NtripUsername = store.Connections.NtripUsername;
        settings.NtripPassword = store.Connections.NtripPassword;
        settings.NtripAutoConnect = store.Connections.NtripAutoConnect;
        settings.AgShareServer = store.Connections.AgShareServer;
        settings.AgShareApiKey = store.Connections.AgShareApiKey;
        settings.AgShareEnabled = store.Connections.AgShareEnabled;
        settings.GpsUpdateRate = store.Connections.GpsUpdateRate;
        settings.UseRtk = store.Connections.UseRtk;

        // Simulator config
        settings.SimulatorEnabled = store.Simulator.Enabled;
        settings.SimulatorLatitude = store.Simulator.Latitude;
        settings.SimulatorLongitude = store.Simulator.Longitude;
        settings.SimulatorSpeed = store.Simulator.Speed;
        settings.SimulatorSteerAngle = store.Simulator.SteerAngle;

        // Active profile
        settings.LastUsedVehicleProfile = store.ActiveProfileName;
    }

    #endregion
}

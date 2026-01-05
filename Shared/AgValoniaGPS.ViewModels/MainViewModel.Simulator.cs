using System;
using AgValoniaGPS.Models.Base;
using AgValoniaGPS.Services.Interfaces;

namespace AgValoniaGPS.ViewModels;

/// <summary>
/// MainViewModel partial class containing Simulator state, properties, and event handlers.
/// Handles GPS simulation for testing guidance without real GPS hardware.
/// </summary>
public partial class MainViewModel
{
    #region Simulator Fields

    // LocalPlane for coordinate conversion (created on first GPS data update)
    private AgValoniaGPS.Models.LocalPlane? _simulatorLocalPlane;

    // Backing fields for properties
    private bool _isSimulatorEnabled;
    private double _simulatorSteerAngle;
    private double _simulatorSpeedKph;

    #endregion

    #region Simulator Event Handlers

    private void OnSimulatorTick(object? sender, EventArgs e)
    {
        // Call simulator Tick with current steer angle
        _simulatorService.Tick(SimulatorSteerAngle);
    }

    private void OnSimulatorGpsDataUpdated(object? sender, GpsSimulationEventArgs e)
    {
        var simulatedData = e.Data;

        // Create LocalPlane if not yet created
        // Use FIELD origin if a field is loaded, otherwise use simulator position
        if (_simulatorLocalPlane == null)
        {
            var sharedProps = new AgValoniaGPS.Models.SharedFieldProperties();
            AgValoniaGPS.Models.Wgs84 origin;

            if (_fieldOriginLatitude != 0 && _fieldOriginLongitude != 0)
            {
                // Use field origin so coordinates match the field's boundary/track data
                origin = new AgValoniaGPS.Models.Wgs84(_fieldOriginLatitude, _fieldOriginLongitude);
                Console.WriteLine($"[Simulator] Using field origin: {_fieldOriginLatitude}, {_fieldOriginLongitude}");
            }
            else
            {
                // No field loaded, use simulator position as origin
                origin = simulatedData.Position;
                Console.WriteLine($"[Simulator] Using simulator position as origin: {origin.Latitude}, {origin.Longitude}");
            }

            _simulatorLocalPlane = new AgValoniaGPS.Models.LocalPlane(origin, sharedProps);
        }

        // Convert WGS84 to local coordinates (Northing/Easting)
        var localCoord = _simulatorLocalPlane.ConvertWgs84ToGeoCoord(simulatedData.Position);

        // Build Position object with both WGS84 and UTM coordinates
        var position = new AgValoniaGPS.Models.Position
        {
            Latitude = simulatedData.Position.Latitude,
            Longitude = simulatedData.Position.Longitude,
            Altitude = simulatedData.Altitude,
            Easting = localCoord.Easting,
            Northing = localCoord.Northing,
            Heading = simulatedData.HeadingDegrees,
            Speed = simulatedData.SpeedKmh / 3.6  // Convert km/h to m/s
        };

        // Build GpsData object
        var gpsData = new AgValoniaGPS.Models.GpsData
        {
            CurrentPosition = position,
            FixQuality = 4,  // RTK Fixed
            SatellitesInUse = simulatedData.SatellitesTracked,
            Hdop = simulatedData.Hdop,
            DifferentialAge = 0.0,
            Timestamp = DateTime.Now
        };

        // Directly update GPS service (bypasses NMEA parsing like WinForms version does)
        // This applies antenna-to-pivot transformation to gpsData.CurrentPosition
        _gpsService.UpdateGpsData(gpsData);

        // Use the TRANSFORMED position (pivot/tractor center) for all guidance calculations
        var transformedPosition = gpsData.CurrentPosition;

        // Update tool position based on vehicle pivot position
        // Tool position service handles fixed, trailing, and TBT configurations
        var vehiclePivot = new Vec3(
            transformedPosition.Easting,
            transformedPosition.Northing,
            transformedPosition.Heading * Math.PI / 180.0  // Convert to radians
        );
        _toolPositionService.Update(vehiclePivot, transformedPosition.Heading * Math.PI / 180.0);

        // Process through AutoSteer pipeline for latency measurement
        _autoSteerService.ProcessSimulatedPosition(
            transformedPosition.Latitude, transformedPosition.Longitude, transformedPosition.Altitude,
            transformedPosition.Heading, transformedPosition.Speed, gpsData.FixQuality,
            gpsData.SatellitesInUse, gpsData.Hdop,
            transformedPosition.Easting, transformedPosition.Northing);

        // Auto-disengage autosteer if vehicle is outside the outer boundary
        // BUT skip this check on first pass (howManyPathsAway == 0) if track runs along boundary
        bool skipBoundaryCheck = _isSelectedTrackOnBoundary && _howManyPathsAway == 0;
        if (IsAutoSteerEngaged && !skipBoundaryCheck && !IsPointInsideBoundary(transformedPosition.Easting, transformedPosition.Northing))
        {
            IsAutoSteerEngaged = false;
            StatusMessage = "AutoSteer disengaged - outside boundary";
        }

        // Calculate autosteer guidance if engaged and we have an active track
        if (IsAutoSteerEngaged && HasActiveTrack && SelectedTrack != null)
        {
            // Increment YouTurn counter (used for throttling)
            _youTurnCounter++;

            // Check for YouTurn execution or create path if approaching headland
            if (IsYouTurnEnabled && _currentHeadlandLine != null && _currentHeadlandLine.Count >= 3)
            {
                ProcessYouTurn(transformedPosition);
            }

            // If we're in a YouTurn, use YouTurn guidance; otherwise use AB line guidance
            if (_isYouTurnTriggered && _youTurnPath != null && _youTurnPath.Count > 0)
            {
                CalculateYouTurnGuidance(transformedPosition);
            }
            else
            {
                CalculateAutoSteerGuidance(transformedPosition);
            }
        }
    }

    #endregion

    #region Simulator Properties

    public bool IsSimulatorEnabled
    {
        get => _isSimulatorEnabled;
        set
        {
            if (SetProperty(ref _isSimulatorEnabled, value))
            {
                // Update centralized state
                State.Simulator.IsEnabled = value;

                // Save to settings
                _settingsService.Settings.SimulatorEnabled = value;
                _settingsService.Save();

                // Start or stop simulator timer based on enabled state
                if (value)
                {
                    // Initialize simulator with saved coordinates
                    var settings = _settingsService.Settings;
                    _simulatorService.Initialize(new AgValoniaGPS.Models.Wgs84(
                        settings.SimulatorLatitude,
                        settings.SimulatorLongitude));

                    State.Simulator.IsRunning = true;
                    _simulatorTimer.Start();
                    StatusMessage = $"Simulator ON at {settings.SimulatorLatitude:F8}, {settings.SimulatorLongitude:F8}";
                }
                else
                {
                    State.Simulator.IsRunning = false;
                    _simulatorTimer.Stop();
                    StatusMessage = "Simulator OFF";
                }
            }
        }
    }

    public double SimulatorSteerAngle
    {
        get => _simulatorSteerAngle;
        set
        {
            SetProperty(ref _simulatorSteerAngle, value);
            State.Simulator.SteerAngle = value;
            OnPropertyChanged(nameof(SimulatorSteerAngleDisplay)); // Notify display property
            if (_isSimulatorEnabled)
            {
                _simulatorService.SteerAngle = value;
            }
        }
    }

    public string SimulatorSteerAngleDisplay => $"Steer Angle: {_simulatorSteerAngle:F1}°";

    /// <summary>
    /// Simulator speed in kph. Range: -10 to +25 kph.
    /// Converts to/from stepDistance using formula: speedKph = stepDistance * 40
    /// </summary>
    public double SimulatorSpeedKph
    {
        get => _simulatorSpeedKph;
        set
        {
            // Clamp to valid range
            value = Math.Max(-10, Math.Min(25, value));
            SetProperty(ref _simulatorSpeedKph, value);
            State.Simulator.Speed = value;
            State.Simulator.TargetSpeed = value;
            OnPropertyChanged(nameof(SimulatorSpeedDisplay));
            if (_isSimulatorEnabled)
            {
                // Convert kph to stepDistance: stepDistance = speedKph / 40
                _simulatorService.StepDistance = value / 40.0;
                // Disable acceleration when manually setting speed
                _simulatorService.IsAcceleratingForward = false;
                _simulatorService.IsAcceleratingBackward = false;
            }
        }
    }

    public string SimulatorSpeedDisplay => $"Speed: {_simulatorSpeedKph:F1} kph";

    #endregion

    #region Simulator Methods

    /// <summary>
    /// Set new starting coordinates for the simulator
    /// </summary>
    public void SetSimulatorCoordinates(double latitude, double longitude)
    {
        Console.WriteLine($"[SimCoords] Setting simulator to: {latitude}, {longitude}");

        // Reinitialize simulator with new coordinates
        _simulatorService.Initialize(new AgValoniaGPS.Models.Wgs84(latitude, longitude));
        _simulatorService.StepDistance = 0;

        // Clear LocalPlane so it will be recreated with new origin on next GPS data update
        _simulatorLocalPlane = null;

        // Reset steering
        SimulatorSteerAngle = 0;

        // Save coordinates to settings so they persist
        _settingsService.Settings.SimulatorLatitude = latitude;
        _settingsService.Settings.SimulatorLongitude = longitude;

        // Also update ConfigurationStore so SaveAppSettings won't overwrite with stale values
        Models.Configuration.ConfigurationStore.Instance.Simulator.Latitude = latitude;
        Models.Configuration.ConfigurationStore.Instance.Simulator.Longitude = longitude;

        var saved = _settingsService.Save();

        // Also update the Latitude/Longitude properties directly so that
        // the map boundary dialog uses the correct coordinates even if
        // the simulator timer hasn't ticked yet
        Latitude = latitude;
        Longitude = longitude;

        StatusMessage = saved
            ? $"Simulator reset to {latitude:F8}, {longitude:F8}"
            : $"Reset to {latitude:F8}, {longitude:F8} (save failed: {_settingsService.GetSettingsFilePath()})";
    }

    /// <summary>
    /// Get current simulator position
    /// </summary>
    public AgValoniaGPS.Models.Wgs84 GetSimulatorPosition()
    {
        return _simulatorService.CurrentPosition;
    }

    #endregion
}

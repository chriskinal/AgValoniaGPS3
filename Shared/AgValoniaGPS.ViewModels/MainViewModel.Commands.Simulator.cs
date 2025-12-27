using System;
using System.Windows.Input;
using ReactiveUI;
using AgValoniaGPS.Models.State;

namespace AgValoniaGPS.ViewModels;

/// <summary>
/// Partial class containing simulator command initialization.
/// </summary>
public partial class MainViewModel
{
    private void InitializeSimulatorCommands()
    {
        // Simulator panel toggle
        ToggleSimulatorPanelCommand = new RelayCommand(() =>
        {
            IsSimulatorPanelVisible = !IsSimulatorPanelVisible;
        });

        ResetSimulatorCommand = new RelayCommand(() =>
        {
            _simulatorService.Reset();
            SimulatorSteerAngle = 0;
            StatusMessage = "Simulator Reset";
        });

        ResetSteerAngleCommand = new RelayCommand(() =>
        {
            SimulatorSteerAngle = 0;
            StatusMessage = "Steer Angle Reset to 0°";
        });

        SimulatorForwardCommand = new RelayCommand(() =>
        {
            _simulatorService.StepDistance = 0;  // Reset speed before accelerating
            _simulatorService.IsAcceleratingForward = true;
            _simulatorService.IsAcceleratingBackward = false;
            StatusMessage = "Sim: Accelerating Forward";
        });

        SimulatorStopCommand = new RelayCommand(() =>
        {
            _simulatorService.IsAcceleratingForward = false;
            _simulatorService.IsAcceleratingBackward = false;
            _simulatorService.StepDistance = 0;  // Immediately stop movement
            _simulatorSpeedKph = 0;  // Reset speed slider (use backing field to avoid triggering setter again)
            this.RaisePropertyChanged(nameof(SimulatorSpeedKph));
            this.RaisePropertyChanged(nameof(SimulatorSpeedDisplay));
            StatusMessage = "Sim: Stopped";
        });

        SimulatorReverseCommand = new RelayCommand(() =>
        {
            _simulatorService.StepDistance = 0;  // Reset speed before accelerating
            _simulatorService.IsAcceleratingBackward = true;
            _simulatorService.IsAcceleratingForward = false;
            StatusMessage = "Sim: Accelerating Reverse";
        });

        SimulatorReverseDirectionCommand = new RelayCommand(() =>
        {
            // Reverse direction by adding 180 degrees to current heading
            var newHeading = _simulatorService.HeadingRadians + Math.PI;
            // Normalize to 0-2π range
            if (newHeading > Math.PI * 2)
                newHeading -= Math.PI * 2;
            _simulatorService.SetHeading(newHeading);
            StatusMessage = "Sim: Direction Reversed";
        });

        SimulatorSteerLeftCommand = new RelayCommand(() =>
        {
            SimulatorSteerAngle -= 5.0; // 5 degree increments
            StatusMessage = $"Steer: {SimulatorSteerAngle:F1}°";
        });

        SimulatorSteerRightCommand = new RelayCommand(() =>
        {
            SimulatorSteerAngle += 5.0; // 5 degree increments
            StatusMessage = $"Steer: {SimulatorSteerAngle:F1}°";
        });

        // SimCoords dialog commands
        ShowSimCoordsDialogCommand = new RelayCommand(() =>
        {
            if (IsSimulatorEnabled)
            {
                // Don't allow changing coords while simulator is running
                System.Diagnostics.Debug.WriteLine("[SimCoords] Disable simulator first to change coordinates");
                return;
            }
            // Load current position into the dialog fields
            // Round to 8 decimal places
            var currentPos = GetSimulatorPosition();
            SimCoordsDialogLatitude = Math.Round((decimal)currentPos.Latitude, 8);
            SimCoordsDialogLongitude = Math.Round((decimal)currentPos.Longitude, 8);
            // Show the panel-based dialog
            State.UI.ShowDialog(DialogType.SimCoords);
        });

        CancelSimCoordsDialogCommand = new RelayCommand(() =>
        {
            State.UI.CloseDialog();
        });

        ConfirmSimCoordsDialogCommand = new RelayCommand(() =>
        {
            // Apply the coordinates from the dialog (convert from decimal? to double)
            double lat = (double)(SimCoordsDialogLatitude ?? 0m);
            double lon = (double)(SimCoordsDialogLongitude ?? 0m);
            SetSimulatorCoordinates(lat, lon);
            State.UI.CloseDialog();
        });
    }
}

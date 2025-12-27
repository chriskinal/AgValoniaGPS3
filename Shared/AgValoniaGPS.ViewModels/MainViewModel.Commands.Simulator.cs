using System;
using System.Windows.Input;
using AgValoniaGPS.Models.State;

namespace AgValoniaGPS.ViewModels;

/// <summary>
/// Partial class containing simulator command initialization.
/// Commands delegate to SimulatorViewModel for actual implementation.
/// </summary>
public partial class MainViewModel
{
    private void InitializeSimulatorCommands()
    {
        // All commands delegate to SimulatorViewModel
        // This maintains backwards compatibility with existing XAML bindings

        ToggleSimulatorPanelCommand = Simulator.TogglePanelCommand;

        ResetSimulatorCommand = new RelayCommand(() =>
        {
            Simulator.ResetCommand?.Execute(null);
            StatusMessage = "Simulator Reset";
        });

        ResetSteerAngleCommand = new RelayCommand(() =>
        {
            Simulator.ResetSteerAngleCommand?.Execute(null);
            StatusMessage = "Steer Angle Reset to 0°";
        });

        SimulatorForwardCommand = new RelayCommand(() =>
        {
            Simulator.ForwardCommand?.Execute(null);
            StatusMessage = "Sim: Accelerating Forward";
        });

        SimulatorStopCommand = new RelayCommand(() =>
        {
            Simulator.StopCommand?.Execute(null);
            StatusMessage = "Sim: Stopped";
        });

        SimulatorReverseCommand = new RelayCommand(() =>
        {
            Simulator.ReverseCommand?.Execute(null);
            StatusMessage = "Sim: Accelerating Reverse";
        });

        SimulatorReverseDirectionCommand = new RelayCommand(() =>
        {
            Simulator.ReverseDirectionCommand?.Execute(null);
            StatusMessage = "Sim: Direction Reversed";
        });

        SimulatorSteerLeftCommand = new RelayCommand(() =>
        {
            Simulator.SteerLeftCommand?.Execute(null);
            StatusMessage = $"Steer: {SimulatorSteerAngle:F1}°";
        });

        SimulatorSteerRightCommand = new RelayCommand(() =>
        {
            Simulator.SteerRightCommand?.Execute(null);
            StatusMessage = $"Steer: {SimulatorSteerAngle:F1}°";
        });

        // SimCoords dialog commands delegate to SimulatorViewModel
        ShowSimCoordsDialogCommand = Simulator.ShowCoordsDialogCommand;
        CancelSimCoordsDialogCommand = Simulator.CancelCoordsDialogCommand;
        ConfirmSimCoordsDialogCommand = new RelayCommand(() =>
        {
            Simulator.ConfirmCoordsDialogCommand?.Execute(null);
            // Also update MainViewModel Latitude/Longitude for map dialog
            var pos = Simulator.GetPosition();
            Latitude = pos.Latitude;
            Longitude = pos.Longitude;
        });
    }
}

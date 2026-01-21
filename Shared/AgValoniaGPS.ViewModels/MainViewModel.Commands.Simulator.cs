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
using CommunityToolkit.Mvvm.Input;
using AgValoniaGPS.Models.State;

namespace AgValoniaGPS.ViewModels;

/// <summary>
/// Simulator commands - GPS simulation controls.
/// </summary>
public partial class MainViewModel
{
    private void InitializeSimulatorCommands()
    {
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
            StatusMessage = "Steer Angle Reset to 0";
        });

        SimulatorForwardCommand = new RelayCommand(() =>
        {
            _simulatorService.StepDistance = 0;
            _simulatorService.IsAcceleratingForward = true;
            _simulatorService.IsAcceleratingBackward = false;
            StatusMessage = "Sim: Accelerating Forward";
        });

        SimulatorStopCommand = new RelayCommand(() =>
        {
            _simulatorService.IsAcceleratingForward = false;
            _simulatorService.IsAcceleratingBackward = false;
            _simulatorService.StepDistance = 0;
            _simulatorSpeedKph = 0;
            OnPropertyChanged(nameof(SimulatorSpeedKph));
            OnPropertyChanged(nameof(SimulatorSpeedDisplay));
            StatusMessage = "Sim: Stopped";
        });

        SimulatorReverseCommand = new RelayCommand(() =>
        {
            _simulatorService.StepDistance = 0;
            _simulatorService.IsAcceleratingBackward = true;
            _simulatorService.IsAcceleratingForward = false;
            StatusMessage = "Sim: Accelerating Reverse";
        });

        SimulatorReverseDirectionCommand = new RelayCommand(() =>
        {
            var newHeading = _simulatorService.HeadingRadians + Math.PI;
            if (newHeading > Math.PI * 2)
                newHeading -= Math.PI * 2;
            _simulatorService.SetHeading(newHeading);
            StatusMessage = "Sim: Direction Reversed";
        });

        SimulatorSteerLeftCommand = new RelayCommand(() =>
        {
            SimulatorSteerAngle -= 5.0;
            StatusMessage = $"Steer: {SimulatorSteerAngle:F1}";
        });

        SimulatorSteerRightCommand = new RelayCommand(() =>
        {
            SimulatorSteerAngle += 5.0;
            StatusMessage = $"Steer: {SimulatorSteerAngle:F1}";
        });

        // Simulator coordinates dialog commands
        ShowSimCoordsDialogCommand = new RelayCommand(() =>
        {
            if (IsSimulatorEnabled)
            {
                StatusMessage = "Disable simulator first to change coordinates";
                return;
            }
            var currentPos = GetSimulatorPosition();
            SimCoordsDialogLatitude = Math.Round((decimal)currentPos.Latitude, 8);
            SimCoordsDialogLongitude = Math.Round((decimal)currentPos.Longitude, 8);
            State.UI.ShowDialog(DialogType.SimCoords);
        });

        CancelSimCoordsDialogCommand = new RelayCommand(() =>
        {
            State.UI.CloseDialog();
        });

        ConfirmSimCoordsDialogCommand = new RelayCommand(() =>
        {
            double lat = (double)(SimCoordsDialogLatitude ?? 0m);
            double lon = (double)(SimCoordsDialogLongitude ?? 0m);
            SetSimulatorCoordinates(lat, lon);
            State.UI.CloseDialog();
        });
    }
}

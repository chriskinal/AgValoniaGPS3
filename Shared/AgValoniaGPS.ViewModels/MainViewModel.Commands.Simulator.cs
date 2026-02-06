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
using System.Reactive;
using ReactiveUI;
using AgValoniaGPS.Models.State;

namespace AgValoniaGPS.ViewModels;

/// <summary>
/// Simulator commands - GPS simulation controls.
/// </summary>
public partial class MainViewModel
{
    private void InitializeSimulatorCommands()
    {
        ToggleSimulatorPanelCommand = ReactiveCommand.Create(() =>
        {
            IsSimulatorPanelVisible = !IsSimulatorPanelVisible;
        });

        ResetSimulatorCommand = ReactiveCommand.Create(() =>
        {
            _simulatorService.Reset();
            SimulatorSteerAngle = 0;
            StatusMessage = "Simulator Reset";
        });

        ResetSteerAngleCommand = ReactiveCommand.Create(() =>
        {
            SimulatorSteerAngle = 0;
            StatusMessage = "Steer Angle Reset to 0";
        });

        SimulatorForwardCommand = ReactiveCommand.Create(() =>
        {
            _simulatorService.StepDistance = 0;
            _simulatorService.IsAcceleratingForward = true;
            _simulatorService.IsAcceleratingBackward = false;
            StatusMessage = "Sim: Accelerating Forward";
        });

        SimulatorStopCommand = ReactiveCommand.Create(() =>
        {
            _simulatorService.IsAcceleratingForward = false;
            _simulatorService.IsAcceleratingBackward = false;
            _simulatorService.StepDistance = 0;
            _simulatorSpeedKph = 0;
            this.RaisePropertyChanged(nameof(SimulatorSpeedKph));
            this.RaisePropertyChanged(nameof(SimulatorSpeedDisplay));
            StatusMessage = "Sim: Stopped";
        });

        SimulatorReverseCommand = ReactiveCommand.Create(() =>
        {
            _simulatorService.StepDistance = 0;
            _simulatorService.IsAcceleratingBackward = true;
            _simulatorService.IsAcceleratingForward = false;
            StatusMessage = "Sim: Accelerating Reverse";
        });

        SimulatorReverseDirectionCommand = ReactiveCommand.Create(() =>
        {
            var newHeading = _simulatorService.HeadingRadians + Math.PI;
            if (newHeading > Math.PI * 2)
                newHeading -= Math.PI * 2;
            _simulatorService.SetHeading(newHeading);
            StatusMessage = "Sim: Direction Reversed";
        });

        SimulatorSteerLeftCommand = ReactiveCommand.Create(() =>
        {
            SimulatorSteerAngle -= 5.0;
            StatusMessage = $"Steer: {SimulatorSteerAngle:F1}";
        });

        SimulatorSteerRightCommand = ReactiveCommand.Create(() =>
        {
            SimulatorSteerAngle += 5.0;
            StatusMessage = $"Steer: {SimulatorSteerAngle:F1}";
        });

        // Simulator coordinates dialog commands
        ShowSimCoordsDialogCommand = ReactiveCommand.Create(() =>
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

        CancelSimCoordsDialogCommand = ReactiveCommand.Create(() =>
        {
            State.UI.CloseDialog();
        });

        ConfirmSimCoordsDialogCommand = ReactiveCommand.Create(() =>
        {
            double lat = (double)(SimCoordsDialogLatitude ?? 0m);
            double lon = (double)(SimCoordsDialogLongitude ?? 0m);
            SetSimulatorCoordinates(lat, lon);
            State.UI.CloseDialog();
        });
    }
}

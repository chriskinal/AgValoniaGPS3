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

using System.Threading.Tasks;
using ReactiveUI;
using AgValoniaGPS.Services.Interfaces;

namespace AgValoniaGPS.ViewModels.Wizards.SteerWizard;

/// <summary>
/// Step for configuring vehicle track width.
/// </summary>
public class TrackWidthStepViewModel : WizardStepViewModel
{
    private readonly IConfigurationService _configService;

    public override string Title => "Vehicle Track Width";

    public override string Description =>
        "Enter the track width of your vehicle - the distance between the centers of the " +
        "left and right wheels (or tracks).";

    private double _trackWidth;
    public double TrackWidth
    {
        get => _trackWidth;
        set => this.RaiseAndSetIfChanged(ref _trackWidth, value);
    }

    private string _unit = "m";
    public string Unit
    {
        get => _unit;
        set => this.RaiseAndSetIfChanged(ref _unit, value);
    }

    public TrackWidthStepViewModel(IConfigurationService configService)
    {
        _configService = configService;
    }

    protected override void OnEntering()
    {
        TrackWidth = _configService.Store.Vehicle.TrackWidth;
    }

    protected override void OnLeaving()
    {
        _configService.Store.Vehicle.TrackWidth = TrackWidth;
    }

    public override Task<bool> ValidateAsync()
    {
        if (TrackWidth < 0.5)
        {
            SetValidationError("Track width must be at least 0.5 meters");
            return Task.FromResult(false);
        }
        if (TrackWidth > 10)
        {
            SetValidationError("Track width seems too large. Please check the value.");
            return Task.FromResult(false);
        }

        ClearValidation();
        return Task.FromResult(true);
    }
}

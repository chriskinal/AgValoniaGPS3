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
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AgValoniaGPS.Services.Interfaces;

namespace AgValoniaGPS.ViewModels.Wizards.SteerWizard;

/// <summary>
/// Step for configuring antenna lateral offset.
/// </summary>
public partial class AntennaOffsetStepViewModel : WizardStepViewModel
{
    private readonly IConfigurationService _configService;

    public override string Title => "Antenna Offset";

    public override string Description =>
        "Enter the lateral offset of the GPS antenna from the vehicle centerline. " +
        "Positive values = right of center, negative values = left of center. " +
        "If your antenna is mounted on the centerline, enter 0.";

    public override bool CanSkip => true; // Optional setting

    [ObservableProperty]
    private double _antennaOffset;

    [ObservableProperty]
    private string _unit = "m";

    /// <summary>
    /// Whether the antenna is on the left side.
    /// </summary>
    public bool IsLeft => AntennaOffset < 0;

    /// <summary>
    /// Whether the antenna is centered.
    /// </summary>
    public bool IsCenter => Math.Abs(AntennaOffset) < 0.01;

    /// <summary>
    /// Whether the antenna is on the right side.
    /// </summary>
    public bool IsRight => AntennaOffset > 0;

    public AntennaOffsetStepViewModel(IConfigurationService configService)
    {
        _configService = configService;
    }

    protected override void OnEntering()
    {
        AntennaOffset = _configService.Store.Vehicle.AntennaOffset;
    }

    protected override void OnLeaving()
    {
        _configService.Store.Vehicle.AntennaOffset = AntennaOffset;
    }

    partial void OnAntennaOffsetChanged(double value)
    {
        OnPropertyChanged(nameof(IsLeft));
        OnPropertyChanged(nameof(IsCenter));
        OnPropertyChanged(nameof(IsRight));
    }

    [RelayCommand]
    private void SetLeft() => AntennaOffset = -0.5;

    [RelayCommand]
    private void SetCenter() => AntennaOffset = 0;

    [RelayCommand]
    private void SetRight() => AntennaOffset = 0.5;

    public override Task<bool> ValidateAsync()
    {
        if (AntennaOffset < -5 || AntennaOffset > 5)
        {
            SetValidationError("Antenna offset should be between -5 and 5 meters");
            return Task.FromResult(false);
        }

        ClearValidation();
        return Task.FromResult(true);
    }
}

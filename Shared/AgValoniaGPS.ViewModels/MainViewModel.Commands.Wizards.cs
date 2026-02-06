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

using System.Reactive;
using System.Windows.Input;
using ReactiveUI;
using AgValoniaGPS.ViewModels.Wizards;
using AgValoniaGPS.ViewModels.Wizards.SteerWizard;

namespace AgValoniaGPS.ViewModels;

/// <summary>
/// Wizard commands - AutoSteer wizard, etc.
/// </summary>
public partial class MainViewModel
{
    // SteerWizard ViewModel
    private SteerWizardViewModel? _steerWizardViewModel;
    public SteerWizardViewModel? SteerWizardViewModel
    {
        get => _steerWizardViewModel;
        set => this.RaiseAndSetIfChanged(ref _steerWizardViewModel, value);
    }

    // Wizard commands
    public ICommand? ShowSteerWizardCommand { get; private set; }

    private void InitializeWizardCommands()
    {
        ShowSteerWizardCommand = ReactiveCommand.Create(ShowSteerWizard);
    }

    private void ShowSteerWizard()
    {
        // Create a new instance of the wizard
        SteerWizardViewModel = new SteerWizardViewModel(_configurationService);

        // Handle wizard close
        SteerWizardViewModel.CloseRequested += (s, e) =>
        {
            if (SteerWizardViewModel != null)
                SteerWizardViewModel.IsDialogVisible = false;
        };

        // Show the wizard
        SteerWizardViewModel.IsDialogVisible = true;
    }
}

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
using ReactiveUI;
using AgValoniaGPS.Models.State;

namespace AgValoniaGPS.ViewModels;

/// <summary>
/// Configuration dialog commands - profiles, settings dialogs.
/// </summary>
public partial class MainViewModel
{
    private void InitializeConfigurationCommands()
    {
        // Data IO Dialog
        ShowDataIODialogCommand = ReactiveCommand.Create(() =>
        {
            State.UI.ShowDialog(DialogType.DataIO);
        });

        CloseDataIODialogCommand = ReactiveCommand.Create(CloseDataIODialog);

        // Configuration Dialog
        ShowConfigurationDialogCommand = ReactiveCommand.Create(() =>
        {
            ConfigurationViewModel = new ConfigurationViewModel(_configurationService);
            ConfigurationViewModel.CloseRequested += (s, e) =>
            {
                ConfigurationViewModel.IsDialogVisible = false;
            };
            ConfigurationViewModel.IsDialogVisible = true;
        });

        CancelConfigurationDialogCommand = ReactiveCommand.Create(() =>
        {
            if (ConfigurationViewModel != null)
                ConfigurationViewModel.IsDialogVisible = false;
        });

        // AutoSteer Configuration Panel
        ShowAutoSteerConfigCommand = ReactiveCommand.Create(() =>
        {
            AutoSteerConfigViewModel ??= new AutoSteerConfigViewModel(_configurationService, _udpService, _autoSteerService);
            AutoSteerConfigViewModel.IsPanelVisible = true;
        });

        // Profile management
        ShowLoadProfileDialogCommand = ReactiveCommand.Create(() =>
        {
            AvailableProfiles.Clear();
            foreach (var profile in _configurationService.GetAvailableProfiles())
            {
                AvailableProfiles.Add(profile);
            }
            SelectedProfile = _configurationService.Store.ActiveProfileName;
            IsProfileSelectionVisible = true;
        });

        LoadSelectedProfileCommand = ReactiveCommand.Create(() =>
        {
            if (!string.IsNullOrEmpty(SelectedProfile))
            {
                _configurationService.LoadProfile(SelectedProfile);
                _settingsService.Settings.LastUsedVehicleProfile = SelectedProfile;
                _settingsService.Save();
                this.RaisePropertyChanged(nameof(CurrentProfileName));
            }
            IsProfileSelectionVisible = false;
        });

        CancelProfileSelectionCommand = ReactiveCommand.Create(() =>
        {
            IsProfileSelectionVisible = false;
        });

        ShowNewProfileDialogCommand = ReactiveCommand.Create(() =>
        {
            var baseName = "New Profile";
            var profileName = baseName;
            var counter = 1;
            var existingProfiles = _configurationService.GetAvailableProfiles();
            while (existingProfiles.Contains(profileName))
            {
                profileName = $"{baseName} {counter++}";
            }

            _configurationService.CreateProfile(profileName);
            _settingsService.Settings.LastUsedVehicleProfile = profileName;
            _settingsService.Save();
            this.RaisePropertyChanged(nameof(CurrentProfileName));
        });
    }
}

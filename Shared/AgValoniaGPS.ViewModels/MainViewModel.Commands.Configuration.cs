using System;
using ReactiveUI;
using System.Windows.Input;
using System.Linq;
using AgValoniaGPS.Models.State;

namespace AgValoniaGPS.ViewModels;

/// <summary>
/// Partial class containing configuration and profile command initialization.
/// </summary>
public partial class MainViewModel
{
    private void InitializeConfigurationCommands()
    {
        // Data IO Dialog
        ShowDataIODialogCommand = new RelayCommand(() =>
        {
            State.UI.ShowDialog(DialogType.DataIO);
        });

        CloseDataIODialogCommand = new RelayCommand(CloseDataIODialog);
        ConnectToNtripCommand = new AsyncRelayCommand(ConnectToNtripAsync);
        DisconnectFromNtripCommand = new AsyncRelayCommand(DisconnectFromNtripAsync);
        SaveNtripSettingsCommand = new RelayCommand(SaveNtripSettings);
        SetActiveDataIOFieldCommand = new RelayCommand<string>(SetActiveDataIOField);

        // Configuration Dialog Commands
        ShowConfigurationDialogCommand = new RelayCommand(() =>
        {
            ConfigurationViewModel = new ConfigurationViewModel(_configurationService, _ntripService);
            ConfigurationViewModel.CloseRequested += (s, e) =>
            {
                ConfigurationViewModel.IsDialogVisible = false;
            };
            ConfigurationViewModel.IsDialogVisible = true;
        });

        CancelConfigurationDialogCommand = new RelayCommand(() =>
        {
            if (ConfigurationViewModel != null)
                ConfigurationViewModel.IsDialogVisible = false;
        });

        // Profile selection commands
        ShowLoadProfileDialogCommand = new RelayCommand(() =>
        {
            // Refresh available profiles
            AvailableProfiles.Clear();
            foreach (var profile in _configurationService.GetAvailableProfiles())
            {
                AvailableProfiles.Add(profile);
            }
            SelectedProfile = _configurationService.Store.ActiveProfileName;
            IsProfileSelectionVisible = true;
        });

        LoadSelectedProfileCommand = new RelayCommand(() =>
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

        CancelProfileSelectionCommand = new RelayCommand(() =>
        {
            IsProfileSelectionVisible = false;
        });

        ShowNewProfileDialogCommand = new RelayCommand(() =>
        {
            // Generate a unique profile name
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

        // AgShare settings commands
        ShowAgShareSettingsDialogCommand = new RelayCommand(() =>
        {
            // Load current settings from storage
            AgShareSettingsServerUrl = _settingsService.Settings.AgShareServer;
            AgShareSettingsApiKey = _settingsService.Settings.AgShareApiKey;
            AgShareSettingsEnabled = _settingsService.Settings.AgShareEnabled;
            State.UI.ShowDialog(DialogType.AgShareSettings);
        });

        CancelAgShareSettingsDialogCommand = new RelayCommand(() =>
        {
            State.UI.CloseDialog();
        });

        ConfirmAgShareSettingsDialogCommand = new RelayCommand(() =>
        {
            // Save settings to storage
            _settingsService.Settings.AgShareServer = AgShareSettingsServerUrl;
            _settingsService.Settings.AgShareApiKey = AgShareSettingsApiKey;
            _settingsService.Settings.AgShareEnabled = AgShareSettingsEnabled;
            _settingsService.Save();

            State.UI.CloseDialog();
            StatusMessage = "AgShare settings saved";
        });

        ShowAgShareDownloadDialogCommand = new RelayCommand(() =>
        {
            State.UI.ShowDialog(DialogType.AgShareDownload);
        });

        CancelAgShareDownloadDialogCommand = new RelayCommand(() =>
        {
            State.UI.CloseDialog();
        });

        ShowAgShareUploadDialogCommand = new RelayCommand(() =>
        {
            State.UI.ShowDialog(DialogType.AgShareUpload);
        });

        CancelAgShareUploadDialogCommand = new RelayCommand(() =>
        {
            State.UI.CloseDialog();
        });

        // Numeric Input Dialog Commands
        CancelNumericInputDialogCommand = new RelayCommand(() =>
        {
            State.UI.CloseDialog();
        });

        ConfirmNumericInputDialogCommand = new RelayCommand(() =>
        {
            State.UI.CloseDialog();
        });
    }
}

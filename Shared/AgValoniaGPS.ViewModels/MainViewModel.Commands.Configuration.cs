using CommunityToolkit.Mvvm.Input;
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
        ShowDataIODialogCommand = new RelayCommand(() =>
        {
            State.UI.ShowDialog(DialogType.DataIO);
        });

        CloseDataIODialogCommand = new RelayCommand(CloseDataIODialog);

        // Configuration Dialog
        ShowConfigurationDialogCommand = new RelayCommand(() =>
        {
            ConfigurationViewModel = new ConfigurationViewModel(_configurationService);
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

        // AutoSteer Configuration Panel
        ShowAutoSteerConfigCommand = new RelayCommand(() =>
        {
            AutoSteerConfigViewModel ??= new AutoSteerConfigViewModel(
                _configurationService, _udpService, _autoSteerService, ShowSteerWizard);
            AutoSteerConfigViewModel.IsPanelVisible = true;
        });

        // Profile management
        ShowLoadProfileDialogCommand = new RelayCommand(() =>
        {
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
                OnPropertyChanged(nameof(CurrentProfileName));
            }
            IsProfileSelectionVisible = false;
        });

        CancelProfileSelectionCommand = new RelayCommand(() =>
        {
            IsProfileSelectionVisible = false;
        });

        ShowNewProfileDialogCommand = new RelayCommand(() =>
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
            OnPropertyChanged(nameof(CurrentProfileName));
        });
    }
}

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

using System.Collections.ObjectModel;
using System.IO;
using AgValoniaGPS.Models.Configuration;
using ReactiveUI;

namespace AgValoniaGPS.ViewModels;

public partial class MainViewModel
{
    private void InitializeSettingsCommands()
    {
        ShowAppDirectoriesDialogCommand = ReactiveCommand.Create(() =>
        {
            RefreshAppDirectories();
            State.UI.ShowDialog(Models.State.DialogType.AppDirectories);
        });

        CloseAppDirectoriesDialogCommand = ReactiveCommand.Create(() =>
        {
            State.UI.CloseDialog();
        });

        ShowAboutDialogCommand = ReactiveCommand.Create(() =>
        {
            State.UI.ShowDialog(Models.State.DialogType.About);
        });

        CloseAboutDialogCommand = ReactiveCommand.Create(() =>
        {
            State.UI.CloseDialog();
        });

        ResetAllSettingsCommand = ReactiveCommand.Create(() =>
        {
            ShowConfirmationDialog(
                "Reset All Settings",
                "Are you sure you want to reset all settings to their defaults? This cannot be undone.",
                () =>
                {
                    _settingsService.ResetToDefaults();
                    ConfigurationStore.SetInstance(new ConfigurationStore());
                    _settingsService.Save();
                    StatusMessage = "All settings reset to defaults. Restart recommended.";
                });
        });
    }

    private void RefreshAppDirectories()
    {
        var dirs = new ObservableCollection<AppDirectoryInfo>();

        var settingsPath = _settingsService.GetSettingsFilePath();
        dirs.Add(new AppDirectoryInfo("Settings", Path.GetDirectoryName(settingsPath) ?? ""));
        dirs.Add(new AppDirectoryInfo("Fields", _settingsService.Settings.FieldsDirectory));
        dirs.Add(new AppDirectoryInfo("Vehicle Profiles", _vehicleProfileService.VehiclesDirectory));
        dirs.Add(new AppDirectoryInfo("NTRIP Profiles", _ntripProfileService.ProfilesDirectory));

        AppDirectories = dirs;
    }
}

public class AppDirectoryInfo
{
    public string Name { get; }
    public string Path { get; }
    public bool Exists { get; }

    public AppDirectoryInfo(string name, string path)
    {
        Name = name;
        Path = path;
        Exists = Directory.Exists(path);
    }
}

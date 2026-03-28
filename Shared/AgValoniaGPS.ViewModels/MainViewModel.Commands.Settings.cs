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
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Input;
using AgValoniaGPS.Models.Configuration;
using AgValoniaGPS.Services.Logging;
using Avalonia.Threading;
using Microsoft.Extensions.Logging;
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

        // Log Viewer (#22)
        ShowLogViewerDialogCommand = ReactiveCommand.Create(() =>
        {
            RefreshLogEntries();
            _logStoreSubscribed = true;
            LogStore.Instance.LogAdded += OnLogStoreUpdated;
            State.UI.ShowDialog(Models.State.DialogType.LogViewer);
        });

        CloseLogViewerDialogCommand = ReactiveCommand.Create(() =>
        {
            if (_logStoreSubscribed)
            {
                LogStore.Instance.LogAdded -= OnLogStoreUpdated;
                _logStoreSubscribed = false;
            }
            State.UI.CloseDialog();
        });

        ClearLogEntriesCommand = ReactiveCommand.Create(() =>
        {
            LogStore.Instance.Clear();
            FilteredLogEntries.Clear();
        });

        SetLogFilterCommand = ReactiveCommand.Create<string>(level =>
        {
            LogFilterLevel = Enum.TryParse<LogLevel>(level, out var parsed) ? parsed : LogLevel.Debug;
            RefreshLogEntries();
        });

        // Flag By Lat/Lon (#23)
        ShowFlagByLatLonDialogCommand = ReactiveCommand.Create(() =>
        {
            FlagLatitudeInput = "";
            FlagLongitudeInput = "";
            FlagByLatLonError = "";
            State.UI.ShowDialog(Models.State.DialogType.FlagByLatLon);
        });

        CloseFlagByLatLonDialogCommand = ReactiveCommand.Create(() =>
        {
            State.UI.CloseDialog();
        });

        PlaceFlagByLatLonCommand = ReactiveCommand.Create(() =>
        {
            PlaceFlagAtLatLon();
        });

        // View All Settings (#29)
        ShowViewSettingsDialogCommand = ReactiveCommand.Create(() =>
        {
            RefreshSettingsTree();
            State.UI.ShowDialog(Models.State.DialogType.ViewSettings);
        });

        CloseViewSettingsDialogCommand = ReactiveCommand.Create(() =>
        {
            State.UI.CloseDialog();
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

// --- Log Viewer (#22) ---
public partial class MainViewModel
{
    private bool _logStoreSubscribed;

    private LogLevel _logFilterLevel = LogLevel.Debug;
    public LogLevel LogFilterLevel
    {
        get => _logFilterLevel;
        set => this.RaiseAndSetIfChanged(ref _logFilterLevel, value);
    }

    public ObservableCollection<LogEntry> FilteredLogEntries { get; } = new();

    private void OnLogStoreUpdated()
    {
        Dispatcher.UIThread.Post(RefreshLogEntries);
    }

    private void RefreshLogEntries()
    {
        var entries = LogStore.Instance.GetSnapshot()
            .Where(e => e.Level >= _logFilterLevel)
            .TakeLast(500)
            .ToList();
        FilteredLogEntries.Clear();
        foreach (var entry in entries)
            FilteredLogEntries.Add(entry);
    }

    public ICommand? ShowLogViewerDialogCommand { get; private set; }
    public ICommand? CloseLogViewerDialogCommand { get; private set; }
    public ICommand? ClearLogEntriesCommand { get; private set; }
    public ICommand? SetLogFilterCommand { get; private set; }
}

// --- Flag By Lat/Lon (#23) ---
public partial class MainViewModel
{
    private string _flagLatitudeInput = "";
    public string FlagLatitudeInput
    {
        get => _flagLatitudeInput;
        set => this.RaiseAndSetIfChanged(ref _flagLatitudeInput, value);
    }

    private string _flagLongitudeInput = "";
    public string FlagLongitudeInput
    {
        get => _flagLongitudeInput;
        set => this.RaiseAndSetIfChanged(ref _flagLongitudeInput, value);
    }

    private string _flagByLatLonError = "";
    public string FlagByLatLonError
    {
        get => _flagByLatLonError;
        set => this.RaiseAndSetIfChanged(ref _flagByLatLonError, value);
    }

    private void PlaceFlagAtLatLon()
    {
        if (!double.TryParse(FlagLatitudeInput, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out double lat) ||
            lat < -90 || lat > 90)
        {
            FlagByLatLonError = "Invalid latitude (must be -90 to 90)";
            return;
        }

        if (!double.TryParse(FlagLongitudeInput, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out double lon) ||
            lon < -180 || lon > 180)
        {
            FlagByLatLonError = "Invalid longitude (must be -180 to 180)";
            return;
        }

        if (_fieldOriginLatitude == 0 && _fieldOriginLongitude == 0 &&
            Latitude == 0 && Longitude == 0)
        {
            FlagByLatLonError = "No field or GPS origin available";
            return;
        }

        // Use field origin if available, else current GPS position as origin
        double originLat = _fieldOriginLatitude != 0 ? _fieldOriginLatitude : Latitude;
        double originLon = _fieldOriginLongitude != 0 ? _fieldOriginLongitude : Longitude;

        var converter = new Models.Base.GeoConversion(originLat, originLon);
        var local = converter.ToLocal(lat, lon);

        var point = new Models.Base.Vec3(local.Easting, local.Northing, 0);
        _flagPoints.Add((point, "Red"));
        StatusMessage = $"Flag #{_flagPoints.Count} placed at {lat:F6}, {lon:F6}";
        FlagByLatLonError = "";

        State.UI.CloseDialog();
    }

    public ICommand? ShowFlagByLatLonDialogCommand { get; private set; }
    public ICommand? CloseFlagByLatLonDialogCommand { get; private set; }
    public ICommand? PlaceFlagByLatLonCommand { get; private set; }
}

// --- View All Settings (#29) ---
public partial class MainViewModel
{
    public ObservableCollection<SettingsGroupItem> SettingsTree { get; } = new();

    private void RefreshSettingsTree()
    {
        SettingsTree.Clear();
        var store = ConfigurationStore.Instance;

        AddConfigGroup("Vehicle", store.Vehicle);
        AddConfigGroup("Tool", store.Tool);
        AddConfigGroup("Guidance", store.Guidance);
        AddConfigGroup("Display", store.Display);
        AddConfigGroup("Simulator", store.Simulator);
        AddConfigGroup("Connections", store.Connections);
        AddConfigGroup("AHRS", store.Ahrs);
        AddConfigGroup("Machine", store.Machine);
        AddConfigGroup("Tram", store.Tram);
        AddConfigGroup("AutoSteer", store.AutoSteer);

        // Global settings
        var global = new SettingsGroupItem("Global");
        global.Items.Add(new SettingsValueItem("Active Profile", store.ActiveProfileName));
        global.Items.Add(new SettingsValueItem("Is Metric", store.IsMetric.ToString()));
        global.Items.Add(new SettingsValueItem("Num Sections", store.NumSections.ToString()));
        global.Items.Add(new SettingsValueItem("Actual Tool Width", $"{store.ActualToolWidth:F2} m"));
        SettingsTree.Add(global);
    }

    private void AddConfigGroup(string name, object config)
    {
        var group = new SettingsGroupItem(name);
        var props = config.GetType()
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && !p.GetIndexParameters().Any())
            .OrderBy(p => p.Name);

        foreach (var prop in props)
        {
            try
            {
                var value = prop.GetValue(config);
                var display = value?.ToString() ?? "(null)";
                group.Items.Add(new SettingsValueItem(prop.Name, display));
            }
            catch
            {
                // Skip properties that throw
            }
        }

        SettingsTree.Add(group);
    }

    public ICommand? ShowViewSettingsDialogCommand { get; private set; }
    public ICommand? CloseViewSettingsDialogCommand { get; private set; }
}

public class SettingsGroupItem
{
    public string Name { get; }
    public ObservableCollection<SettingsValueItem> Items { get; } = new();

    public SettingsGroupItem(string name) => Name = name;
}

public class SettingsValueItem
{
    public string Name { get; }
    public string Value { get; }

    public SettingsValueItem(string name, string value)
    {
        Name = name;
        Value = value;
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

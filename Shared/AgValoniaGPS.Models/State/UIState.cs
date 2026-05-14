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
using CommunityToolkit.Mvvm.ComponentModel;

namespace AgValoniaGPS.Models.State;

/// <summary>
/// UI state - active dialogs, panels, selections.
/// Replaces 25+ dialog visibility flags with a proper dialog system.
/// </summary>
public class UIState : ObservableObject
{
    // Active dialog (only one modal at a time)
    private DialogType _activeDialog = DialogType.None;
    public DialogType ActiveDialog
    {
        get => _activeDialog;
        set
        {
            if (_activeDialog != value)
            {
                var previous = _activeDialog;
                SetProperty(ref _activeDialog, value);

                // Raise property changed for all dialog visibility properties
                OnPropertyChanged(nameof(IsDialogOpen));
                OnPropertyChanged(nameof(IsFieldSelectionDialogVisible));
                OnPropertyChanged(nameof(IsStartWorkSessionDialogVisible));
                OnPropertyChanged(nameof(IsResumeJobDialogVisible));
                OnPropertyChanged(nameof(IsTracksDialogVisible));
                OnPropertyChanged(nameof(IsConfigurationDialogVisible));
                OnPropertyChanged(nameof(IsNewFieldDialogVisible));
                OnPropertyChanged(nameof(IsFromExistingFieldDialogVisible));
                OnPropertyChanged(nameof(IsKmlImportDialogVisible));
                OnPropertyChanged(nameof(IsIsoXmlImportDialogVisible));
                OnPropertyChanged(nameof(IsBoundaryMapDialogVisible));
                OnPropertyChanged(nameof(IsNumericInputDialogVisible));
                OnPropertyChanged(nameof(IsAgShareSettingsDialogVisible));
                OnPropertyChanged(nameof(IsAgShareUploadDialogVisible));
                OnPropertyChanged(nameof(IsAgShareDownloadDialogVisible));
                OnPropertyChanged(nameof(IsSimCoordsDialogVisible));
                OnPropertyChanged(nameof(IsQuickABSelectorVisible));
                OnPropertyChanged(nameof(IsDrawABDialogVisible));
                OnPropertyChanged(nameof(IsNtripProfilesDialogVisible));
                OnPropertyChanged(nameof(IsNtripProfileEditorDialogVisible));
                OnPropertyChanged(nameof(IsConfirmationDialogVisible));
                OnPropertyChanged(nameof(IsErrorDialogVisible));
                OnPropertyChanged(nameof(IsAppDirectoriesDialogVisible));
                OnPropertyChanged(nameof(IsHotkeyConfigDialogVisible));
                OnPropertyChanged(nameof(IsAboutDialogVisible));
                OnPropertyChanged(nameof(IsLogViewerDialogVisible));
                OnPropertyChanged(nameof(IsFlagByLatLonDialogVisible));
                OnPropertyChanged(nameof(IsFlagListDialogVisible));
                OnPropertyChanged(nameof(IsViewSettingsDialogVisible));
                OnPropertyChanged(nameof(IsImportTracksDialogVisible));
                OnPropertyChanged(nameof(IsHelpDialogVisible));
                OnPropertyChanged(nameof(IsLanguageDialogVisible));
                OnPropertyChanged(nameof(IsRecordedPathDialogVisible));
                OnPropertyChanged(nameof(IsTramSettingsDialogVisible));
                OnPropertyChanged(nameof(IsFieldBuilderDialogVisible));
                OnPropertyChanged(nameof(IsBugReportDialogVisible));
                OnPropertyChanged(nameof(IsSmartWasDialogVisible));
                OnPropertyChanged(nameof(IsLoadVehicleToolDialogVisible));
                OnPropertyChanged(nameof(IsAppSettingsDialogVisible));

                DialogChanged?.Invoke(this, new DialogChangedEventArgs(previous, value));
            }
        }
    }

    public bool IsDialogOpen => ActiveDialog != DialogType.None;

    // Convenience properties for XAML binding (backwards compatible)
    public bool IsFieldSelectionDialogVisible => ActiveDialog == DialogType.FieldSelection;
    public bool IsStartWorkSessionDialogVisible => ActiveDialog == DialogType.StartWorkSession;
    public bool IsResumeJobDialogVisible => ActiveDialog == DialogType.ResumeJob;
    public bool IsTracksDialogVisible => ActiveDialog == DialogType.Tracks;
    public bool IsConfigurationDialogVisible => ActiveDialog == DialogType.Configuration;
    public bool IsNewFieldDialogVisible => ActiveDialog == DialogType.NewField;
    public bool IsFromExistingFieldDialogVisible => ActiveDialog == DialogType.FromExistingField;
    public bool IsKmlImportDialogVisible => ActiveDialog == DialogType.KmlImport;
    public bool IsIsoXmlImportDialogVisible => ActiveDialog == DialogType.IsoXmlImport;
    public bool IsBoundaryMapDialogVisible => ActiveDialog == DialogType.BoundaryMap;
    public bool IsNumericInputDialogVisible => ActiveDialog == DialogType.NumericInput;
    public bool IsAgShareSettingsDialogVisible => ActiveDialog == DialogType.AgShareSettings;
    public bool IsAgShareUploadDialogVisible => ActiveDialog == DialogType.AgShareUpload;
    public bool IsAgShareDownloadDialogVisible => ActiveDialog == DialogType.AgShareDownload;
    public bool IsSimCoordsDialogVisible => ActiveDialog == DialogType.SimCoords;
    public bool IsQuickABSelectorVisible => ActiveDialog == DialogType.QuickABSelector;
    public bool IsDrawABDialogVisible => ActiveDialog == DialogType.DrawAB;
    public bool IsNtripProfilesDialogVisible => ActiveDialog == DialogType.NtripProfiles;
    public bool IsNtripProfileEditorDialogVisible => ActiveDialog == DialogType.NtripProfileEditor;
    public bool IsConfirmationDialogVisible => ActiveDialog == DialogType.Confirmation;
    public bool IsErrorDialogVisible => ActiveDialog == DialogType.Error;
    public bool IsAppDirectoriesDialogVisible => ActiveDialog == DialogType.AppDirectories;
    public bool IsHotkeyConfigDialogVisible => ActiveDialog == DialogType.HotkeyConfig;
    public bool IsAboutDialogVisible => ActiveDialog == DialogType.About;
    public bool IsLogViewerDialogVisible => ActiveDialog == DialogType.LogViewer;
    public bool IsFlagByLatLonDialogVisible => ActiveDialog == DialogType.FlagByLatLon;
    public bool IsFlagListDialogVisible => ActiveDialog == DialogType.FlagList;
    public bool IsViewSettingsDialogVisible => ActiveDialog == DialogType.ViewSettings;
    public bool IsImportTracksDialogVisible => ActiveDialog == DialogType.ImportTracks;
    public bool IsHelpDialogVisible => ActiveDialog == DialogType.Help;
    public bool IsLanguageDialogVisible => ActiveDialog == DialogType.Language;
    public bool IsRecordedPathDialogVisible => ActiveDialog == DialogType.RecordedPath;
    public bool IsTramSettingsDialogVisible => ActiveDialog == DialogType.TramSettings;
    public bool IsFieldBuilderDialogVisible => ActiveDialog == DialogType.FieldBuilder;
    public bool IsBugReportDialogVisible => ActiveDialog == DialogType.BugReport;
    public bool IsSmartWasDialogVisible => ActiveDialog == DialogType.SmartWas;
    public bool IsLoadVehicleToolDialogVisible => ActiveDialog == DialogType.LoadVehicleTool;
    public bool IsAppSettingsDialogVisible => ActiveDialog == DialogType.AppSettings;

    // Panel visibility (non-modal, can have multiple open)
    private bool _isSimulatorPanelVisible;
    public bool IsSimulatorPanelVisible
    {
        get => _isSimulatorPanelVisible;
        set => SetProperty(ref _isSimulatorPanelVisible, value);
    }

    private bool _isBoundaryPanelVisible;
    public bool IsBoundaryPanelVisible
    {
        get => _isBoundaryPanelVisible;
        set => SetProperty(ref _isBoundaryPanelVisible, value);
    }

    private bool _isViewSettingsPanelVisible;
    public bool IsViewSettingsPanelVisible
    {
        get => _isViewSettingsPanelVisible;
        set => SetProperty(ref _isViewSettingsPanelVisible, value);
    }

    private bool _isSectionControlPanelVisible;
    public bool IsSectionControlPanelVisible
    {
        get => _isSectionControlPanelVisible;
        set => SetProperty(ref _isSectionControlPanelVisible, value);
    }

    private bool _isOffsetFixPanelVisible;
    public bool IsOffsetFixPanelVisible
    {
        get => _isOffsetFixPanelVisible;
        set => SetProperty(ref _isOffsetFixPanelVisible, value);
    }

    // Busy overlay state (for blocking operations like file save/load)
    private bool _isBusy;
    public bool IsBusy
    {
        get => _isBusy;
        set => SetProperty(ref _isBusy, value);
    }

    private string _busyMessage = "";
    public string BusyMessage
    {
        get => _busyMessage;
        set => SetProperty(ref _busyMessage, value);
    }

    // Selection state (shared across dialogs)
    private object? _selectedItem;
    public object? SelectedItem
    {
        get => _selectedItem;
        set => SetProperty(ref _selectedItem, value);
    }

    // Dialog events
    public event EventHandler<DialogChangedEventArgs>? DialogChanged;

    // Methods
    public void ShowDialog(DialogType dialog)
    {
        ActiveDialog = dialog;
    }

    public void CloseDialog()
    {
        ActiveDialog = DialogType.None;
        SelectedItem = null;
    }

    public void CloseAllPanels()
    {
        IsSimulatorPanelVisible = false;
        IsBoundaryPanelVisible = false;
        IsViewSettingsPanelVisible = false;
        IsSectionControlPanelVisible = false;
    }

    public void Reset()
    {
        CloseDialog();
        CloseAllPanels();
    }
}

/// <summary>
/// All dialog types in the application
/// </summary>
public enum DialogType
{
    None,
    FieldSelection,
    Tracks,
    Configuration,
    NewField,
    FromExistingField,
    KmlImport,
    IsoXmlImport,
    BoundaryMap,
    NumericInput,
    AgShareSettings,
    AgShareUpload,
    AgShareDownload,
    Headland,
    HeadlandBuilder,
    SimCoords,
    QuickABSelector,
    DrawAB,
    NtripProfiles,
    NtripProfileEditor,
    Confirmation,
    Error,
    AppDirectories,
    HotkeyConfig,
    About,
    LogViewer,
    FlagByLatLon,
    FlagList,
    ViewSettings,
    ImportTracks,
    Help,
    Language,
    RecordedPath,
    TramSettings,
    FieldBuilder,
    BugReport,
    SmartWas,
    LoadVehicleTool,
    StartWorkSession,
    ResumeJob,
    AppSettings,
}

/// <summary>
/// Event args for dialog changes
/// </summary>
public class DialogChangedEventArgs : EventArgs
{
    public DialogType Previous { get; }
    public DialogType Current { get; }

    public DialogChangedEventArgs(DialogType previous, DialogType current)
    {
        Previous = previous;
        Current = current;
    }
}

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
using ReactiveUI;

namespace AgValoniaGPS.Models.State;

/// <summary>
/// UI state - active dialogs, panels, selections.
/// Replaces 25+ dialog visibility flags with a proper dialog system.
/// </summary>
public class UIState : ReactiveObject
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
                this.RaiseAndSetIfChanged(ref _activeDialog, value);

                // Raise property changed for all dialog visibility properties
                this.RaisePropertyChanged(nameof(IsDialogOpen));
                this.RaisePropertyChanged(nameof(IsFieldSelectionDialogVisible));
                this.RaisePropertyChanged(nameof(IsTracksDialogVisible));
                this.RaisePropertyChanged(nameof(IsConfigurationDialogVisible));
                this.RaisePropertyChanged(nameof(IsNewFieldDialogVisible));
                this.RaisePropertyChanged(nameof(IsFromExistingFieldDialogVisible));
                this.RaisePropertyChanged(nameof(IsKmlImportDialogVisible));
                this.RaisePropertyChanged(nameof(IsIsoXmlImportDialogVisible));
                this.RaisePropertyChanged(nameof(IsBoundaryMapDialogVisible));
                this.RaisePropertyChanged(nameof(IsNumericInputDialogVisible));
                this.RaisePropertyChanged(nameof(IsAgShareSettingsDialogVisible));
                this.RaisePropertyChanged(nameof(IsAgShareUploadDialogVisible));
                this.RaisePropertyChanged(nameof(IsAgShareDownloadDialogVisible));
                this.RaisePropertyChanged(nameof(IsDataIODialogVisible));
                this.RaisePropertyChanged(nameof(IsHeadlandDialogVisible));
                this.RaisePropertyChanged(nameof(IsHeadlandBuilderDialogVisible));
                this.RaisePropertyChanged(nameof(IsSimCoordsDialogVisible));
                this.RaisePropertyChanged(nameof(IsQuickABSelectorVisible));
                this.RaisePropertyChanged(nameof(IsDrawABDialogVisible));
                this.RaisePropertyChanged(nameof(IsNtripProfilesDialogVisible));
                this.RaisePropertyChanged(nameof(IsNtripProfileEditorDialogVisible));
                this.RaisePropertyChanged(nameof(IsConfirmationDialogVisible));
                this.RaisePropertyChanged(nameof(IsErrorDialogVisible));

                DialogChanged?.Invoke(this, new DialogChangedEventArgs(previous, value));
            }
        }
    }

    public bool IsDialogOpen => ActiveDialog != DialogType.None;

    // Convenience properties for XAML binding (backwards compatible)
    public bool IsFieldSelectionDialogVisible => ActiveDialog == DialogType.FieldSelection;
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
    public bool IsDataIODialogVisible => ActiveDialog == DialogType.DataIO;
    public bool IsHeadlandDialogVisible => ActiveDialog == DialogType.Headland;
    public bool IsHeadlandBuilderDialogVisible => ActiveDialog == DialogType.HeadlandBuilder;
    public bool IsSimCoordsDialogVisible => ActiveDialog == DialogType.SimCoords;
    public bool IsQuickABSelectorVisible => ActiveDialog == DialogType.QuickABSelector;
    public bool IsDrawABDialogVisible => ActiveDialog == DialogType.DrawAB;
    public bool IsNtripProfilesDialogVisible => ActiveDialog == DialogType.NtripProfiles;
    public bool IsNtripProfileEditorDialogVisible => ActiveDialog == DialogType.NtripProfileEditor;
    public bool IsConfirmationDialogVisible => ActiveDialog == DialogType.Confirmation;
    public bool IsErrorDialogVisible => ActiveDialog == DialogType.Error;

    // Panel visibility (non-modal, can have multiple open)
    private bool _isSimulatorPanelVisible;
    public bool IsSimulatorPanelVisible
    {
        get => _isSimulatorPanelVisible;
        set => this.RaiseAndSetIfChanged(ref _isSimulatorPanelVisible, value);
    }

    private bool _isBoundaryPanelVisible;
    public bool IsBoundaryPanelVisible
    {
        get => _isBoundaryPanelVisible;
        set => this.RaiseAndSetIfChanged(ref _isBoundaryPanelVisible, value);
    }

    private bool _isViewSettingsPanelVisible;
    public bool IsViewSettingsPanelVisible
    {
        get => _isViewSettingsPanelVisible;
        set => this.RaiseAndSetIfChanged(ref _isViewSettingsPanelVisible, value);
    }

    private bool _isSectionControlPanelVisible;
    public bool IsSectionControlPanelVisible
    {
        get => _isSectionControlPanelVisible;
        set => this.RaiseAndSetIfChanged(ref _isSectionControlPanelVisible, value);
    }

    // Busy overlay state (for blocking operations like file save/load)
    private bool _isBusy;
    public bool IsBusy
    {
        get => _isBusy;
        set => this.RaiseAndSetIfChanged(ref _isBusy, value);
    }

    private string _busyMessage = "";
    public string BusyMessage
    {
        get => _busyMessage;
        set => this.RaiseAndSetIfChanged(ref _busyMessage, value);
    }

    // Selection state (shared across dialogs)
    private object? _selectedItem;
    public object? SelectedItem
    {
        get => _selectedItem;
        set => this.RaiseAndSetIfChanged(ref _selectedItem, value);
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
    DataIO,
    Headland,
    HeadlandBuilder,
    SimCoords,
    QuickABSelector,
    DrawAB,
    NtripProfiles,
    NtripProfileEditor,
    Confirmation,
    Error
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

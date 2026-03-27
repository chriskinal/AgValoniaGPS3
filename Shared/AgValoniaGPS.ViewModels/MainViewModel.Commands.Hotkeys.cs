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

using System.Collections.Generic;
using System.Windows.Input;
using ReactiveUI;
using AgValoniaGPS.Models.Configuration;
using AgValoniaGPS.Models.State;

namespace AgValoniaGPS.ViewModels;

public partial class MainViewModel
{
    // Hotkey commands
    public ICommand? ShowHotkeyConfigDialogCommand { get; private set; }
    public ICommand? CloseHotkeyConfigDialogCommand { get; private set; }
    public ICommand? ResetHotkeysToDefaultCommand { get; private set; }
    public ReactiveCommand<HotkeyAction, System.Reactive.Unit>? StartEditHotkeyCommand { get; private set; }

    // Editing state
    private HotkeyAction? _editingHotkeyAction;
    public HotkeyAction? EditingHotkeyAction
    {
        get => _editingHotkeyAction;
        set => this.RaiseAndSetIfChanged(ref _editingHotkeyAction, value);
    }

    // Dispatch table: HotkeyAction -> command
    private Dictionary<HotkeyAction, ICommand?>? _hotkeyDispatch;

    private void InitializeHotkeyCommands()
    {
        ShowHotkeyConfigDialogCommand = ReactiveCommand.Create(() =>
        {
            IsFileMenuPanelVisible = false;
            State.UI.ShowDialog(DialogType.HotkeyConfig);
        });

        CloseHotkeyConfigDialogCommand = ReactiveCommand.Create(() =>
        {
            EditingHotkeyAction = null;
            State.UI.CloseDialog();
            _configurationService.SaveAppSettings();
        });

        ResetHotkeysToDefaultCommand = ReactiveCommand.Create(() =>
        {
            ConfigStore.Hotkeys.ResetToDefaults();
            _hotkeyDispatch = null;
        });

        StartEditHotkeyCommand = ReactiveCommand.Create<HotkeyAction>(action =>
        {
            EditingHotkeyAction = action;
        });

        BuildHotkeyDispatch();
    }

    private void BuildHotkeyDispatch()
    {
        _hotkeyDispatch = new Dictionary<HotkeyAction, ICommand?>
        {
            { HotkeyAction.AutoSteer, ToggleAutoSteerCommand },
            { HotkeyAction.CycleLines, CycleABLinesCommand },
            { HotkeyAction.FieldMenu, ShowFieldSelectionDialogCommand },
            { HotkeyAction.Flag, PlaceRedFlagCommand },
            { HotkeyAction.ManualSection, ToggleSectionMasterCommand },
            { HotkeyAction.AutoSection, ToggleSectionMasterCommand },
            { HotkeyAction.SnapPivot, SnapToPivotCommand },
            { HotkeyAction.NudgeLeft, NudgeLeftCommand },
            { HotkeyAction.NudgeRight, NudgeRightCommand },
            { HotkeyAction.VehicleSettings, ShowConfigurationDialogCommand },
            { HotkeyAction.SteerWizard, ShowSteerWizardCommand },
        };
    }

    public void CaptureHotkeyBinding(string key)
    {
        if (EditingHotkeyAction == null) return;

        ConfigStore.Hotkeys.SetKeyForAction(EditingHotkeyAction.Value, key);
        EditingHotkeyAction = null;
        _hotkeyDispatch = null;
    }

    public bool HandleHotkey(string key)
    {
        if (!ConfigStore.Display.KeyboardEnabled) return false;
        if (State.UI.IsDialogOpen) return false;

        var action = ConfigStore.Hotkeys.GetActionForKey(key);
        if (action == null) return false;

        // Section toggles need parameter
        if (action.Value >= HotkeyAction.Section1 && action.Value <= HotkeyAction.Section8)
        {
            ToggleSectionCommand?.Execute((int)(action.Value - HotkeyAction.Section1));
            return true;
        }

        _hotkeyDispatch ??= new Dictionary<HotkeyAction, ICommand?>();
        if (_hotkeyDispatch.Count == 0) BuildHotkeyDispatch();

        if (_hotkeyDispatch.TryGetValue(action.Value, out var cmd) && cmd != null)
        {
            cmd.Execute(null);
            return true;
        }

        StatusMessage = $"{action} not yet implemented";
        return true;
    }
}

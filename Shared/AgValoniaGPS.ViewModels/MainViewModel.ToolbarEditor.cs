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
using System.Linq;
using System.Windows.Input;
using AgValoniaGPS.Models.State;
using AgValoniaGPS.Models.Toolbar;
using CommunityToolkit.Mvvm.Input;

namespace AgValoniaGPS.ViewModels;

public partial class MainViewModel
{
    public ObservableCollection<ButtonDefinition> AvailableButtons { get; } = new();
    public ObservableCollection<ButtonDefinition> CurrentShortcuts { get; } = new();

    public ICommand? ShowShortcutEditorCommand { get; private set; }
    public ICommand? CloseShortcutEditorCommand { get; private set; }
    public ICommand? AddShortcutCommand { get; private set; }
    public ICommand? RemoveShortcutCommand { get; private set; }
    public ICommand? MoveShortcutUpCommand { get; private set; }
    public ICommand? MoveShortcutDownCommand { get; private set; }

    private void InitializeToolbarEditorCommands()
    {
        ShowShortcutEditorCommand = new RelayCommand(() =>
        {
            RefreshEditorLists();
            State.UI.ShowDialog(DialogType.ShortcutEditor);
        });

        CloseShortcutEditorCommand = new RelayCommand(() =>
        {
            SaveShortcutLayout();
            State.UI.CloseDialog();
        });

        AddShortcutCommand = new RelayCommand<ButtonDefinition>(button =>
        {
            if (button == null) return;
            CurrentShortcuts.Add(button);
            AvailableButtons.Remove(button);
        });

        RemoveShortcutCommand = new RelayCommand<ButtonDefinition>(button =>
        {
            if (button == null) return;
            CurrentShortcuts.Remove(button);
            AvailableButtons.Add(button);
        });

        MoveShortcutUpCommand = new RelayCommand<ButtonDefinition>(button =>
        {
            if (button == null) return;
            var index = CurrentShortcuts.IndexOf(button);
            if (index > 0)
                CurrentShortcuts.Move(index, index - 1);
        });

        MoveShortcutDownCommand = new RelayCommand<ButtonDefinition>(button =>
        {
            if (button == null) return;
            var index = CurrentShortcuts.IndexOf(button);
            if (index >= 0 && index < CurrentShortcuts.Count - 1)
                CurrentShortcuts.Move(index, index + 1);
        });
    }

    private void RefreshEditorLists()
    {
        AvailableButtons.Clear();
        CurrentShortcuts.Clear();

        var store = Models.Configuration.ConfigurationStore.Instance;
        var layout = store.Toolbar.ShortcutLayouts
            .FirstOrDefault(l => l.Name == store.Toolbar.ActiveLayoutName)
            ?? store.Toolbar.ShortcutLayouts.FirstOrDefault();

        // Populate current shortcuts
        if (layout != null)
        {
            foreach (var shortcut in layout.Shortcuts)
            {
                var def = _buttonRegistry.GetById(shortcut.ButtonId);
                if (def != null)
                    CurrentShortcuts.Add(def);
            }
        }

        // Populate available (all buttons not currently in shortcuts)
        var currentIds = CurrentShortcuts.Select(b => b.Id).ToHashSet();
        foreach (var button in _buttonRegistry.GetAll())
        {
            if (!currentIds.Contains(button.Id))
                AvailableButtons.Add(button);
        }
    }

    private void SaveShortcutLayout()
    {
        var store = Models.Configuration.ConfigurationStore.Instance;
        var layout = store.Toolbar.ShortcutLayouts
            .FirstOrDefault(l => l.Name == store.Toolbar.ActiveLayoutName);

        if (layout == null)
        {
            layout = new ShortcutLayout { Name = store.Toolbar.ActiveLayoutName ?? "Default" };
            store.Toolbar.ShortcutLayouts.Add(layout);
        }

        layout.Shortcuts = CurrentShortcuts
            .Select(b => new ToolbarShortcut { ButtonId = b.Id })
            .ToList();

        RefreshShortcutBar();

        // Persist
        _configurationService.SaveAppSettings();
    }
}

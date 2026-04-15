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
    public ObservableCollection<string> LayoutNames { get; } = new();

    private string _selectedLayoutName = "Default";
    public string SelectedLayoutName
    {
        get => _selectedLayoutName;
        set
        {
            if (SetProperty(ref _selectedLayoutName, value) && value != null)
            {
                var store = Models.Configuration.ConfigurationStore.Instance;
                store.Toolbar.ActiveLayoutName = value;
                RefreshEditorLists();
                RefreshShortcutBar();
                _configurationService.SaveAppSettings();
            }
        }
    }

    private string _newLayoutName = string.Empty;
    public string NewLayoutName
    {
        get => _newLayoutName;
        set => SetProperty(ref _newLayoutName, value);
    }

    public ICommand? ShowShortcutEditorCommand { get; private set; }
    public ICommand? CloseShortcutEditorCommand { get; private set; }
    public ICommand? AddShortcutCommand { get; private set; }
    public ICommand? RemoveShortcutCommand { get; private set; }
    public ICommand? MoveShortcutUpCommand { get; private set; }
    public ICommand? MoveShortcutDownCommand { get; private set; }
    public ICommand? CreateLayoutCommand { get; private set; }
    public ICommand? DeleteLayoutCommand { get; private set; }

    private void InitializeToolbarEditorCommands()
    {
        ShowShortcutEditorCommand = new RelayCommand(() =>
        {
            RefreshLayoutNames();
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

        CreateLayoutCommand = new RelayCommand(() =>
        {
            var name = NewLayoutName?.Trim();
            if (string.IsNullOrEmpty(name)) return;

            var store = Models.Configuration.ConfigurationStore.Instance;

            // Don't allow duplicate names
            if (store.Toolbar.ShortcutLayouts.Any(l => l.Name == name))
            {
                StatusMessage = $"Layout '{name}' already exists";
                return;
            }

            // Save current layout first
            SaveShortcutLayout();

            // Create new layout as a copy of current
            var newLayout = new ShortcutLayout
            {
                Name = name,
                Shortcuts = CurrentShortcuts
                    .Select(b => new ToolbarShortcut { ButtonId = b.Id })
                    .ToList()
            };
            store.Toolbar.ShortcutLayouts.Add(newLayout);
            store.Toolbar.ActiveLayoutName = name;

            NewLayoutName = string.Empty;
            RefreshLayoutNames();
            _selectedLayoutName = name;
            OnPropertyChanged(nameof(SelectedLayoutName));
            _configurationService.SaveAppSettings();
        });

        DeleteLayoutCommand = new RelayCommand(() =>
        {
            var store = Models.Configuration.ConfigurationStore.Instance;
            if (store.Toolbar.ShortcutLayouts.Count <= 1)
            {
                StatusMessage = "Cannot delete the last layout";
                return;
            }

            var layout = store.Toolbar.ShortcutLayouts
                .FirstOrDefault(l => l.Name == SelectedLayoutName);
            if (layout != null)
            {
                store.Toolbar.ShortcutLayouts.Remove(layout);
                store.Toolbar.ActiveLayoutName = store.Toolbar.ShortcutLayouts[0].Name;
                RefreshLayoutNames();
                _selectedLayoutName = store.Toolbar.ActiveLayoutName;
                OnPropertyChanged(nameof(SelectedLayoutName));
                RefreshEditorLists();
                _configurationService.SaveAppSettings();
                RefreshShortcutBar();
            }
        });
    }

    private void RefreshLayoutNames()
    {
        LayoutNames.Clear();
        var store = Models.Configuration.ConfigurationStore.Instance;
        foreach (var layout in store.Toolbar.ShortcutLayouts)
            LayoutNames.Add(layout.Name);
        _selectedLayoutName = store.Toolbar.ActiveLayoutName ?? "Default";
        OnPropertyChanged(nameof(SelectedLayoutName));
    }

    private void RefreshEditorLists()
    {
        AvailableButtons.Clear();
        CurrentShortcuts.Clear();

        var store = Models.Configuration.ConfigurationStore.Instance;
        var layout = store.Toolbar.ShortcutLayouts
            .FirstOrDefault(l => l.Name == store.Toolbar.ActiveLayoutName)
            ?? store.Toolbar.ShortcutLayouts.FirstOrDefault();

        if (layout != null)
        {
            foreach (var shortcut in layout.Shortcuts)
            {
                var def = _buttonRegistry.GetById(shortcut.ButtonId);
                if (def != null)
                    CurrentShortcuts.Add(def);
            }
        }

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
        _configurationService.SaveAppSettings();
    }
}

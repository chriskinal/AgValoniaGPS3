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
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Windows.Input;
using AgValoniaGPS.Models.Toolbar;
using AgValoniaGPS.Services.Interfaces;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using CommunityToolkit.Mvvm.Input;

namespace AgValoniaGPS.ViewModels;

public partial class MainViewModel
{
    private IButtonRegistryService _buttonRegistry = null!;

    public ObservableCollection<ShortcutButtonViewModel> ActiveShortcutButtons { get; } = new();

    private bool _isSectionOverlayVisible;
    public bool IsSectionOverlayVisible
    {
        get => _isSectionOverlayVisible;
        set => SetProperty(ref _isSectionOverlayVisible, value);
    }

    public ICommand? ToggleSectionOverlayCommand { get; private set; }

    private void InitializeToolbarCommands()
    {
        ToggleSectionOverlayCommand = new RelayCommand(() =>
            IsSectionOverlayVisible = !IsSectionOverlayVisible);
    }

    public void InitializeToolbar(IButtonRegistryService buttonRegistry)
    {
        _buttonRegistry = buttonRegistry;
        InitializeToolbarCommands();
        RefreshShortcutBar();
    }

    public void RefreshShortcutBar()
    {
        ActiveShortcutButtons.Clear();

        var store = Models.Configuration.ConfigurationStore.Instance;
        var layout = store.Toolbar.ShortcutLayouts
            .FirstOrDefault(l => l.Name == store.Toolbar.ActiveLayoutName);

        if (layout == null && store.Toolbar.ShortcutLayouts.Count > 0)
            layout = store.Toolbar.ShortcutLayouts[0];

        if (layout == null)
        {
            layout = ToolbarConfiguration.CreateDefaultLayout();
            store.Toolbar.ShortcutLayouts.Add(layout);
            store.Toolbar.ActiveLayoutName = layout.Name;
        }

        foreach (var shortcut in layout.Shortcuts)
        {
            var definition = _buttonRegistry.GetById(shortcut.ButtonId);
            if (definition == null)
            {
                Debug.WriteLine($"[Toolbar] Unknown button ID: {shortcut.ButtonId}");
                continue;
            }

            var vm = CreateShortcutButtonViewModel(definition);
            if (vm != null)
                ActiveShortcutButtons.Add(vm);
        }
    }

    private ShortcutButtonViewModel? CreateShortcutButtonViewModel(ButtonDefinition definition)
    {
        // Resolve the ICommand from this MainViewModel via reflection
        var commandProp = GetType().GetProperty(definition.CommandPropertyName,
            BindingFlags.Public | BindingFlags.Instance);
        if (commandProp == null)
        {
            Debug.WriteLine($"[Toolbar] Command property not found: {definition.CommandPropertyName}");
            return null;
        }

        var command = commandProp.GetValue(this) as ICommand;
        if (command == null)
        {
            Debug.WriteLine($"[Toolbar] Command is null: {definition.CommandPropertyName}");
            return null;
        }

        // Load icons
        var defaultIcon = LoadBitmap(definition.IconResource);
        if (defaultIcon == null)
        {
            Debug.WriteLine($"[Toolbar] Failed to load icon: {definition.IconResource}");
            return null;
        }

        Bitmap? activeIcon = null;
        if (definition.ActiveIconResource != null)
            activeIcon = LoadBitmap(definition.ActiveIconResource);

        var vm = new ShortcutButtonViewModel
        {
            ButtonId = definition.Id,
            Tooltip = definition.Tooltip,
            Command = command,
            DefaultIcon = defaultIcon,
            ActiveIcon = activeIcon,
        };

        // Bind IsActive to the source property on this MainViewModel
        if (definition.IsActivePropertyName != null)
        {
            var activeProp = GetType().GetProperty(definition.IsActivePropertyName,
                BindingFlags.Public | BindingFlags.Instance);
            if (activeProp != null)
            {
                // Set initial value
                vm.IsActive = activeProp.GetValue(this) is true;

                // Subscribe to changes
                PropertyChanged += (_, e) =>
                {
                    if (e.PropertyName == definition.IsActivePropertyName)
                        vm.IsActive = activeProp.GetValue(this) is true;
                };
            }
        }

        // Bind IsVisible to the source property on this MainViewModel
        if (definition.IsVisiblePropertyName != null)
        {
            var visibleProp = GetType().GetProperty(definition.IsVisiblePropertyName,
                BindingFlags.Public | BindingFlags.Instance);
            if (visibleProp != null)
            {
                vm.IsVisible = visibleProp.GetValue(this) is true;

                PropertyChanged += (_, e) =>
                {
                    if (e.PropertyName == definition.IsVisiblePropertyName)
                        vm.IsVisible = visibleProp.GetValue(this) is true;
                };
            }
        }

        return vm;
    }

    /// <summary>
    /// Saves the current ActiveShortcutButtons order back to the active layout.
    /// Called by ShortcutBar after drag reorder or remove.
    /// </summary>
    public void SaveShortcutLayoutFromBar()
    {
        var store = Models.Configuration.ConfigurationStore.Instance;
        var layout = store.Toolbar.ShortcutLayouts
            .FirstOrDefault(l => l.Name == store.Toolbar.ActiveLayoutName);

        if (layout == null) return;

        layout.Shortcuts = ActiveShortcutButtons
            .Select(b => new ToolbarShortcut { ButtonId = b.ButtonId })
            .ToList();

        _configurationService.SaveAppSettings();
    }

    /// <summary>
    /// Adds a button to the shortcut bar by its ButtonId.
    /// Called by RightNavigationPanel long-press-to-add.
    /// </summary>
    public void AddButtonToShortcutBar(string buttonId)
    {
        // Check if already in the bar
        if (ActiveShortcutButtons.Any(b => b.ButtonId == buttonId))
            return;

        var definition = _buttonRegistry.GetById(buttonId);
        if (definition == null) return;

        var vm = CreateShortcutButtonViewModel(definition);
        if (vm != null)
        {
            ActiveShortcutButtons.Add(vm);
            SaveShortcutLayoutFromBar();
        }
    }

    private static Bitmap? LoadBitmap(string avaresPath)
    {
        try
        {
            var uri = new Uri(avaresPath);
            using var stream = AssetLoader.Open(uri);
            return new Bitmap(stream);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Toolbar] Failed to load bitmap {avaresPath}: {ex.Message}");
            return null;
        }
    }
}

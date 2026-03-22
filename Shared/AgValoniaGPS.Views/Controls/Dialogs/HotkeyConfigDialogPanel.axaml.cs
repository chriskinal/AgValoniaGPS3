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
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using AgValoniaGPS.Models.Configuration;
using AgValoniaGPS.ViewModels;

namespace AgValoniaGPS.Views.Controls.Dialogs;

public partial class HotkeyConfigDialogPanel : UserControl
{
    private readonly Dictionary<HotkeyAction, Button> _keyButtons = new();
    private MainViewModel? _vm;

    private static readonly Dictionary<HotkeyAction, string> ActionLabels = new()
    {
        { HotkeyAction.AutoSteer, "Auto Steer" },
        { HotkeyAction.CycleLines, "Cycle Lines" },
        { HotkeyAction.FieldMenu, "Field Menu" },
        { HotkeyAction.Flag, "Flag" },
        { HotkeyAction.ManualSection, "Manual Section" },
        { HotkeyAction.AutoSection, "Auto Section" },
        { HotkeyAction.SnapPivot, "Snap to Pivot" },
        { HotkeyAction.NudgeLeft, "Nudge Left" },
        { HotkeyAction.NudgeRight, "Nudge Right" },
        { HotkeyAction.VehicleSettings, "Vehicle Settings" },
        { HotkeyAction.SteerWizard, "Steer Wizard" },
        { HotkeyAction.Section1, "Section 1" },
        { HotkeyAction.Section2, "Section 2" },
        { HotkeyAction.Section3, "Section 3" },
        { HotkeyAction.Section4, "Section 4" },
        { HotkeyAction.Section5, "Section 5" },
        { HotkeyAction.Section6, "Section 6" },
        { HotkeyAction.Section7, "Section 7" },
        { HotkeyAction.Section8, "Section 8" },
    };

    public HotkeyConfigDialogPanel()
    {
        InitializeComponent();
        BuildHotkeyRows();

        DataContextChanged += OnDataContextChanged;

        // Grab focus when the dialog becomes visible so KeyDown events fire
        this.GetObservable(IsVisibleProperty).Subscribe(visible =>
        {
            if (visible)
            {
                Focus();
                RefreshAllKeyLabels();
            }
        });
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_vm != null)
        {
            _vm.PropertyChanged -= OnViewModelPropertyChanged;
            ConfigurationStore.Instance.Hotkeys.PropertyChanged -= OnHotkeyConfigChanged;
        }

        _vm = DataContext as MainViewModel;

        if (_vm != null)
        {
            _vm.PropertyChanged += OnViewModelPropertyChanged;
            ConfigurationStore.Instance.Hotkeys.PropertyChanged += OnHotkeyConfigChanged;
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.EditingHotkeyAction))
        {
            UpdateButtonStates();
        }
    }

    private void OnHotkeyConfigChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(HotkeyConfig.Bindings))
        {
            RefreshAllKeyLabels();
        }
    }

    private void BuildHotkeyRows()
    {
        var panel = this.FindControl<StackPanel>("HotkeyListPanel");
        if (panel == null) return;

        foreach (var action in Enum.GetValues<HotkeyAction>())
        {
            var label = ActionLabels.GetValueOrDefault(action, action.ToString());
            var defaultKey = HotkeyConfig.Defaults.GetValueOrDefault(action, "");

            var row = new Grid
            {
                ColumnDefinitions = ColumnDefinitions.Parse("*,Auto"),
                Height = 36,
                Margin = new Thickness(4, 0),
            };

            var textBlock = new TextBlock
            {
                Text = label,
                Foreground = Brushes.White,
                FontSize = 14,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8, 0, 0, 0),
            };
            Grid.SetColumn(textBlock, 0);
            row.Children.Add(textBlock);

            var button = new Button
            {
                Content = defaultKey,
                MinWidth = 60,
                Height = 32,
                FontSize = 14,
                FontWeight = FontWeight.Bold,
                Background = new SolidColorBrush(Color.Parse("#DD34495E")),
                Foreground = Brushes.White,
                CornerRadius = new CornerRadius(4),
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                BorderThickness = new Thickness(0),
                Cursor = new Cursor(StandardCursorType.Hand),
                Tag = action,
            };
            button.Click += KeyButton_Click;
            Grid.SetColumn(button, 1);
            row.Children.Add(button);

            _keyButtons[action] = button;
            panel.Children.Add(row);
        }
    }

    private void KeyButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is HotkeyAction action && _vm != null)
        {
            _vm.StartEditHotkeyCommand?.Execute(action);
        }
    }

    private void UpdateButtonStates()
    {
        var editing = _vm?.EditingHotkeyAction;
        foreach (var kvp in _keyButtons)
        {
            if (kvp.Key == editing)
            {
                kvp.Value.Content = "Press a key...";
                kvp.Value.Background = new SolidColorBrush(Color.Parse("#E74C3C"));
            }
            else
            {
                var key = ConfigurationStore.Instance.Hotkeys.GetKeyForAction(kvp.Key);
                kvp.Value.Content = string.IsNullOrEmpty(key) ? "(none)" : key;
                kvp.Value.Background = new SolidColorBrush(Color.Parse("#DD34495E"));
            }
        }
    }

    private void RefreshAllKeyLabels()
    {
        foreach (var kvp in _keyButtons)
        {
            if (_vm?.EditingHotkeyAction == kvp.Key) continue;
            var key = ConfigurationStore.Instance.Hotkeys.GetKeyForAction(kvp.Key);
            kvp.Value.Content = string.IsNullOrEmpty(key) ? "(none)" : key;
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (_vm?.EditingHotkeyAction != null)
        {
            var keyStr = KeyToString(e.Key);
            if (keyStr != null)
            {
                _vm.CaptureHotkeyBinding(keyStr);
                e.Handled = true;
                return;
            }

            // Escape cancels editing
            if (e.Key == Key.Escape)
            {
                _vm.EditingHotkeyAction = null;
                e.Handled = true;
                return;
            }
        }

        base.OnKeyDown(e);
    }

    private static string? KeyToString(Key key)
    {
        if (key >= Key.A && key <= Key.Z)
            return ((char)('A' + (key - Key.A))).ToString();
        if (key >= Key.D0 && key <= Key.D9)
            return ((char)('0' + (key - Key.D0))).ToString();
        if (key >= Key.NumPad0 && key <= Key.NumPad9)
            return ((char)('0' + (key - Key.NumPad0))).ToString();
        return null;
    }

    private void Backdrop_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_vm != null)
        {
            _vm.CloseHotkeyConfigDialogCommand?.Execute(null);
        }
    }
}

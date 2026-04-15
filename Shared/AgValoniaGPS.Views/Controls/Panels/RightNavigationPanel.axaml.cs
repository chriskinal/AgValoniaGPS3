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
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AgValoniaGPS.ViewModels;

namespace AgValoniaGPS.Views.Controls.Panels;

public partial class RightNavigationPanel : UserControl
{
    private DispatcherTimer? _longPressTimer;
    private Button? _pressedButton;
    private Point _pressPoint;
    private bool _longPressTriggered;
    private const double LongPressThresholdMs = 500;
    private const double MoveDeadzone = 8;

    public RightNavigationPanel()
    {
        InitializeComponent();
        AddHandler(PointerPressedEvent, OnRightBarPointerPressed, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        AddHandler(PointerMovedEvent, OnRightBarPointerMoved, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        AddHandler(PointerReleasedEvent, OnRightBarPointerReleased, Avalonia.Interactivity.RoutingStrategies.Tunnel);
    }

    private void OnRightBarPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;

        var button = FindRightPanelButton(e.Source as Visual);
        if (button == null) return;

        _pressedButton = button;
        _pressPoint = e.GetPosition(this);
        _longPressTriggered = false;

        _longPressTimer?.Stop();
        _longPressTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(LongPressThresholdMs)
        };
        _longPressTimer.Tick += (_, _) =>
        {
            _longPressTimer.Stop();
            _longPressTriggered = true;
            OnLongPress(_pressedButton);
        };
        _longPressTimer.Start();
    }

    private void OnRightBarPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_pressedButton == null || _longPressTriggered) return;

        var currentPos = e.GetPosition(this);
        var dist = Math.Sqrt(Math.Pow(currentPos.X - _pressPoint.X, 2) +
                             Math.Pow(currentPos.Y - _pressPoint.Y, 2));
        if (dist > MoveDeadzone)
        {
            _longPressTimer?.Stop();
            _pressedButton = null;
        }
    }

    private void OnRightBarPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        _longPressTimer?.Stop();

        if (_longPressTriggered)
        {
            e.Handled = true;
        }

        _pressedButton = null;
        _longPressTriggered = false;
    }

    private void OnLongPress(Button button)
    {
        if (DataContext is not MainViewModel vm) return;

        var tooltip = ToolTip.GetTip(button)?.ToString();
        if (string.IsNullOrEmpty(tooltip)) return;

        // Find the matching ButtonDefinition by tooltip (delegated to ViewModel)
        var match = vm.GetButtonDefinitionByTooltip(tooltip);
        if (match == null)
        {
            vm.ShowErrorDialog("Menu Button",
                "This is a menu button. Open it and long-press individual actions inside to add them as shortcuts.");
            return;
        }

        // Check if already on the shortcut bar
        if (vm.ActiveShortcutButtons.Any(b => b.ButtonId == match.Id))
        {
            vm.ShowErrorDialog("Already Added",
                $"'{match.Tooltip}' is already on the shortcut bar.");
            return;
        }

        vm.ShowConfirmationDialog(
            "Add to Shortcuts",
            $"Add '{match.Tooltip}' to the shortcut bar?",
            () => vm.AddButtonToShortcutBar(match.Id));
    }

    private static Button? FindRightPanelButton(Visual? visual)
    {
        while (visual != null)
        {
            if (visual is Button btn && ToolTip.GetTip(btn) != null)
                return btn;
            if (visual is RightNavigationPanel)
                return null; // reached the panel root without finding a button
            visual = visual.GetVisualParent();
        }
        return null;
    }
}

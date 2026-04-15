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

public partial class ShortcutBar : UserControl
{
    private DispatcherTimer? _longPressTimer;
    private ShortcutButtonViewModel? _draggedItem;
    private Point _pressPoint;
    private int _dragStartIndex;
    private bool _isDragging;
    private bool _longPressTriggered;
    private IPointer? _capturedPointer;
    private const double LongPressThresholdMs = 500;
    private const double MoveDeadzone = 8;
    private const double RemoveThresholdY = -50;
    private const double ButtonSlotWidth = 68; // 64 button + 4 spacing

    public ShortcutBar()
    {
        InitializeComponent();
        AddHandler(PointerPressedEvent, OnShortcutPointerPressed, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        AddHandler(PointerMovedEvent, OnShortcutPointerMoved, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        AddHandler(PointerReleasedEvent, OnShortcutPointerReleased, Avalonia.Interactivity.RoutingStrategies.Tunnel);
    }

    private void OnShortcutPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;

        var button = FindShortcutButton(e.Source as Visual);
        if (button?.DataContext is not ShortcutButtonViewModel buttonVm) return;

        var vm = DataContext as MainViewModel;
        if (vm == null) return;

        _draggedItem = buttonVm;
        _pressPoint = e.GetPosition(this);
        _dragStartIndex = vm.ActiveShortcutButtons.IndexOf(buttonVm);
        _isDragging = false;
        _longPressTriggered = false;

        _longPressTimer?.Stop();
        _longPressTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(LongPressThresholdMs)
        };
        _longPressTimer.Tick += (_, _) =>
        {
            _longPressTimer.Stop();
            if (_draggedItem == null) return;
            _longPressTriggered = true;
            _isDragging = true;
            // Capture on the UserControl so we keep getting events even after ItemsControl rebuilds
            _capturedPointer = e.Pointer;
            e.Pointer.Capture(this);
            // Visual feedback on the current button
            UpdateDraggedButtonVisual(0.6, true);
        };
        _longPressTimer.Start();
    }

    private void OnShortcutPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_draggedItem == null) return;
        var currentPos = e.GetPosition(this);

        if (!_longPressTriggered)
        {
            var dist = Math.Sqrt(Math.Pow(currentPos.X - _pressPoint.X, 2) +
                                 Math.Pow(currentPos.Y - _pressPoint.Y, 2));
            if (dist > MoveDeadzone)
                CancelLongPress();
            return;
        }

        if (!_isDragging) return;

        var deltaY = currentPos.Y - _pressPoint.Y;
        var deltaX = currentPos.X - _pressPoint.X;

        // Visual feedback for remove (dragging up)
        UpdateDraggedButtonVisual(deltaY < RemoveThresholdY ? 0.3 : 0.6, true);

        // Compute target index from drag distance
        var vm = DataContext as MainViewModel;
        if (vm == null) return;

        var currentIndex = vm.ActiveShortcutButtons.IndexOf(_draggedItem);
        if (currentIndex < 0) return;

        var targetIndex = _dragStartIndex + (int)Math.Round(deltaX / ButtonSlotWidth);
        targetIndex = Math.Clamp(targetIndex, 0, vm.ActiveShortcutButtons.Count - 1);

        if (targetIndex != currentIndex)
        {
            vm.ActiveShortcutButtons.Move(currentIndex, targetIndex);
        }

        e.Handled = true;
    }

    private void OnShortcutPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        _longPressTimer?.Stop();

        if (_draggedItem != null && _isDragging)
        {
            var currentPos = e.GetPosition(this);
            var deltaY = currentPos.Y - _pressPoint.Y;

            var vm = DataContext as MainViewModel;

            // Drag up = remove
            if (deltaY < RemoveThresholdY && vm != null)
            {
                vm.ActiveShortcutButtons.Remove(_draggedItem);
            }

            // Reset visual state
            UpdateDraggedButtonVisual(1.0, false);
            _capturedPointer?.Capture(null);
            _capturedPointer = null;

            vm?.SaveShortcutLayoutFromBar();
            e.Handled = true;
        }

        if (_longPressTriggered)
            e.Handled = true;

        _draggedItem = null;
        _isDragging = false;
        _longPressTriggered = false;
    }

    private void UpdateDraggedButtonVisual(double opacity, bool scaled)
    {
        if (_draggedItem == null) return;
        // Find the button that currently displays this ViewModel (may change after Move)
        var button = this.GetVisualDescendants()
            .OfType<Button>()
            .FirstOrDefault(b => b.DataContext == _draggedItem);
        if (button != null)
        {
            button.Opacity = opacity;
            button.RenderTransform = scaled ? new Avalonia.Media.ScaleTransform(1.15, 1.15) : null;
        }
    }

    private void CancelLongPress()
    {
        _longPressTimer?.Stop();
        _draggedItem = null;
    }

    private static Button? FindShortcutButton(Visual? visual)
    {
        while (visual != null)
        {
            if (visual is Button btn && btn.Classes.Contains("ShortcutButton") &&
                btn.DataContext is ShortcutButtonViewModel)
                return btn;
            visual = visual.GetVisualParent();
        }
        return null;
    }
}

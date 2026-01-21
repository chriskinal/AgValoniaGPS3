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
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace AgValoniaGPS.Views.Controls.Panels;

/// <summary>
/// Base class for panels that can be dragged (hold 300ms) and rotated (tap).
/// Subclasses must call InitializeDragRotate() after InitializeComponent().
/// </summary>
public abstract class DraggableRotatablePanel : UserControl
{
    // Drag state
    private bool _isDragging;
    private Point _dragStartPoint;
    private double _panelStartLeft;
    private double _panelStartTop;
    private DispatcherTimer? _holdTimer;
    private bool _isHolding;
    private Visual? _positionReference;

    /// <summary>
    /// Event raised when the user is dragging the panel.
    /// Provides the new absolute position (Left, Top).
    /// </summary>
    public event EventHandler<Point>? DragMoved;

    /// <summary>
    /// Initialize drag and rotate behavior. Call this after InitializeComponent().
    /// </summary>
    protected void InitializeDragRotate()
    {
        // Find the drag handle - subclasses define this
        var dragHandle = FindDragHandle();
        if (dragHandle != null)
        {
            dragHandle.PointerPressed += DragHandle_PointerPressed;
            dragHandle.PointerMoved += DragHandle_PointerMoved;
            dragHandle.PointerReleased += DragHandle_PointerReleased;
        }

        // Setup hold timer for drag detection (300ms hold = drag, quick tap = rotate)
        _holdTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(300)
        };
        _holdTimer.Tick += HoldTimer_Tick;
    }

    /// <summary>
    /// Find the drag handle control. Override to return a different control.
    /// Default looks for a Grid named "DragHandle".
    /// </summary>
    protected virtual Control? FindDragHandle()
    {
        return this.FindControl<Grid>("DragHandle");
    }

    /// <summary>
    /// Find the StackPanel to rotate. Override to return a different control.
    /// Default looks for a StackPanel named "ButtonStack".
    /// </summary>
    protected virtual StackPanel? FindButtonStack()
    {
        return this.FindControl<StackPanel>("ButtonStack");
    }

    /// <summary>
    /// Rotate the panel. Override to customize rotation behavior.
    /// Default toggles StackPanel orientation between Vertical and Horizontal.
    /// </summary>
    protected virtual void RotatePanel()
    {
        var buttonStack = FindButtonStack();
        if (buttonStack != null)
        {
            buttonStack.Orientation = buttonStack.Orientation == Orientation.Vertical
                ? Orientation.Horizontal
                : Orientation.Vertical;
        }
    }

    /// <summary>
    /// Called after drag position is updated. Override to perform additional updates.
    /// </summary>
    protected virtual void OnDragPositionUpdated(double newLeft, double newTop)
    {
        // Base implementation does nothing - subclasses can override
    }

    private void DragHandle_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        // Find the Canvas that contains this panel (walk up to find it)
        _positionReference = FindParentCanvas();
        if (_positionReference == null)
        {
            _positionReference = this.Parent as Visual;
        }

        // Get position relative to the Canvas for smooth dragging
        _dragStartPoint = e.GetPosition(_positionReference);

        // Get current canvas position of this UserControl (not internal controls)
        _panelStartLeft = Canvas.GetLeft(this);
        _panelStartTop = Canvas.GetTop(this);

        // If using Canvas.Right, convert to Left
        if (double.IsNaN(_panelStartLeft))
        {
            if (_positionReference is Canvas canvas)
            {
                var parentBounds = canvas.Bounds;
                var rightValue = Canvas.GetRight(this);
                if (double.IsNaN(rightValue)) rightValue = 20;
                _panelStartLeft = parentBounds.Width - this.Bounds.Width - rightValue;
            }
            else
            {
                _panelStartLeft = 20;
            }
        }
        if (double.IsNaN(_panelStartTop)) _panelStartTop = 100;

        _isHolding = false;
        _holdTimer?.Start();

        // Close any tooltips on the drag handle
        if (sender is Control control)
        {
            ToolTip.SetIsOpen(control, false);
        }
        e.Handled = true;
    }

    /// <summary>
    /// Find the Canvas parent that contains this panel.
    /// </summary>
    private Canvas? FindParentCanvas()
    {
        Visual? current = this.Parent as Visual;
        while (current != null)
        {
            if (current is Canvas canvas)
                return canvas;
            current = current.GetVisualParent();
        }
        return null;
    }

    private void HoldTimer_Tick(object? sender, EventArgs e)
    {
        _holdTimer?.Stop();
        _isHolding = true;
        _isDragging = true;
    }

    private void DragHandle_PointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_isDragging || _positionReference == null) return;

        // Get position relative to the same reference used in PointerPressed
        var currentPoint = e.GetPosition(_positionReference);
        var deltaX = currentPoint.X - _dragStartPoint.X;
        var deltaY = currentPoint.Y - _dragStartPoint.Y;

        // Calculate new absolute position
        var newLeft = Math.Max(0, _panelStartLeft + deltaX);
        var newTop = Math.Max(0, _panelStartTop + deltaY);

        // Always move the UserControl itself (this), not internal controls
        Canvas.SetLeft(this, newLeft);
        Canvas.SetTop(this, newTop);

        // Allow subclasses to perform additional updates
        OnDragPositionUpdated(newLeft, newTop);

        // Fire event for any platform-specific handling (e.g., bounds clamping)
        DragMoved?.Invoke(this, new Point(newLeft, newTop));

        e.Handled = true;
    }

    private void DragHandle_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        _holdTimer?.Stop();

        if (!_isHolding && !_isDragging)
        {
            // Quick tap - rotate the panel
            RotatePanel();
        }

        _isDragging = false;
        _isHolding = false;
        e.Handled = true;
    }
}

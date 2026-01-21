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

namespace AgValoniaGPS.Views.Behaviors;

/// <summary>
/// Shared drag behavior for panels.
/// Attach to any control to enable Canvas-based dragging.
/// </summary>
public static class DragBehavior
{
    private static bool _isDragging;
    private static Point _dragStartPoint;
    private static Control? _dragTarget;

    /// <summary>
    /// Enables drag behavior on a control that is positioned in a Canvas.
    /// Call this in your control's constructor or Loaded event.
    /// </summary>
    /// <param name="control">The control to make draggable</param>
    /// <param name="boundsProvider">Optional: Control that provides bounds for constraining (e.g., parent window)</param>
    public static void EnableDrag(Control control, Control? boundsProvider = null)
    {
        control.PointerPressed += (s, e) => OnPointerPressed(control, e);
        control.PointerMoved += (s, e) => OnPointerMoved(control, boundsProvider, e);
        control.PointerReleased += (s, e) => OnPointerReleased(e);
    }

    /// <summary>
    /// Handle pointer pressed - start drag
    /// </summary>
    public static void OnPointerPressed(Control control, PointerPressedEventArgs e)
    {
        _isDragging = true;
        _dragTarget = control;
        _dragStartPoint = e.GetPosition(control.Parent as Visual);
        e.Pointer.Capture(control);
    }

    /// <summary>
    /// Handle pointer moved - update position
    /// </summary>
    public static void OnPointerMoved(Control control, Control? boundsProvider, PointerEventArgs e)
    {
        if (!_isDragging || _dragTarget != control) return;

        var parent = control.Parent as Visual;
        if (parent == null) return;

        var currentPoint = e.GetPosition(parent);
        var deltaX = currentPoint.X - _dragStartPoint.X;
        var deltaY = currentPoint.Y - _dragStartPoint.Y;

        var currentLeft = Canvas.GetLeft(control);
        var currentTop = Canvas.GetTop(control);

        if (double.IsNaN(currentLeft)) currentLeft = 0;
        if (double.IsNaN(currentTop)) currentTop = 0;

        var newLeft = currentLeft + deltaX;
        var newTop = currentTop + deltaY;

        // Constrain to bounds if provider is specified
        if (boundsProvider != null && boundsProvider.Bounds.Width > 0)
        {
            var maxLeft = boundsProvider.Bounds.Width - control.Bounds.Width;
            var maxTop = boundsProvider.Bounds.Height - control.Bounds.Height;
            newLeft = Math.Clamp(newLeft, 0, Math.Max(0, maxLeft));
            newTop = Math.Clamp(newTop, 0, Math.Max(0, maxTop));
        }

        Canvas.SetLeft(control, newLeft);
        Canvas.SetTop(control, newTop);

        _dragStartPoint = currentPoint;
    }

    /// <summary>
    /// Handle pointer released - end drag
    /// </summary>
    public static void OnPointerReleased(PointerReleasedEventArgs e)
    {
        if (_isDragging)
        {
            _isDragging = false;
            _dragTarget = null;
            e.Pointer.Capture(null);
        }
    }
}

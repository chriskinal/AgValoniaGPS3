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

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace AgValoniaGPS.Views.Controls.Panels;

public partial class SectionControlPanel : UserControl
{
    private bool _isDragging;
    private Point _dragStartScreen;
    private double _dragStartTranslateX;
    private double _dragStartTranslateY;
    private readonly TranslateTransform _translate = new();

    public SectionControlPanel()
    {
        InitializeComponent();
        RenderTransform = _translate;

        var dragHandle = this.FindControl<Border>("DragHandle");
        if (dragHandle != null)
        {
            dragHandle.PointerPressed += OnHandlePointerPressed;
        }

        // Move and release handled at control level to capture outside the handle
        PointerMoved += OnPointerMovedDrag;
        PointerReleased += OnPointerReleasedDrag;
    }

    private void OnHandlePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;

        _isDragging = true;
        var root = this.VisualRoot as Visual ?? this;
        _dragStartScreen = e.GetPosition(root);
        _dragStartTranslateX = _translate.X;
        _dragStartTranslateY = _translate.Y;
        e.Pointer.Capture(this);
        e.Handled = true;
    }

    private void OnPointerMovedDrag(object? sender, PointerEventArgs e)
    {
        if (!_isDragging || e.Pointer.Captured != this) return;

        var root = this.VisualRoot as Visual ?? this;
        var current = e.GetPosition(root);
        _translate.X = _dragStartTranslateX + (current.X - _dragStartScreen.X);
        _translate.Y = _dragStartTranslateY + (current.Y - _dragStartScreen.Y);
        e.Handled = true;
    }

    private void OnPointerReleasedDrag(object? sender, PointerReleasedEventArgs e)
    {
        if (!_isDragging) return;
        _isDragging = false;
        e.Pointer.Capture(null);
        e.Handled = true;
    }
}

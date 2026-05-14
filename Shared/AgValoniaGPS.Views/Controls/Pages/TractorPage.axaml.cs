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
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using AgValoniaGPS.ViewModels;

namespace AgValoniaGPS.Views.Controls.Pages;

/// <summary>
/// TractorPage hosts the Tractor-specific sub-tabs (Dimensions, U-Turn,
/// Machine Control, Tram Lines, GPS / Data Sources). Pattern mirrors
/// AppShell: bottom button strip + content swap driven by code-behind.
///
/// Dev feature — Edit Layout: when the toggle is on, each positioned
/// element on the Dimensions sub-tab becomes draggable. Move the mouse
/// to reposition; the readout in the heading shows the current X% /
/// Y% so the user can dictate locked positions back to a developer.
/// </summary>
public partial class TractorPage : UserControl
{
    /// <summary>
    /// Draggable elements on the Dimensions sub-tab. Each entry includes
    /// a display label (for the readout) and the container size used to
    /// turn pixel margins into percentage coordinates.
    /// </summary>
    private record Draggable(string Label, Control Control, double ContainerWidth, double ContainerHeight);

    private readonly List<Draggable> _draggables = new();

    private bool _editMode;
    private Draggable? _dragging;
    private Point _dragStartPointer;
    private Thickness _dragStartMargin;

    public TractorPage()
    {
        InitializeComponent();

        // Page bindings target ConfigurationViewModel; reach through
        // MainViewModel.TabConfigurationViewModel on first attach.
        AttachedToVisualTree += (_, _) =>
        {
            if (DataContext is MainViewModel vm)
                DataContext = vm.TabConfigurationViewModel;
            RegisterDraggables();
        };
    }

    /// <summary>
    /// Bind the named Dimensions elements to the draggable registry.
    /// Container sizes match the fixed Width/Height on the diagram Grids
    /// in TractorPage.axaml so pct = pixel/size lines up.
    /// </summary>
    private void RegisterDraggables()
    {
        if (_draggables.Count > 0) return;

        const double canvasW = 1100, canvasH = 640;
        const double geometryW = 720, geometryH = 280;
        const double antennaW = 720, antennaH = 345;

        // Section headings — moveable against the outer Dimensions canvas
        // so the user can slot the divider in wherever the layout lands.
        Add("Geometry header", "GeometryHeader", canvasW, canvasH);
        Add("Antenna header",  "AntennaHeader",  canvasW, canvasH);

        void Add(string label, string name, double w, double h)
        {
            var c = this.FindControl<Control>(name);
            if (c != null) _draggables.Add(new Draggable(label, c, w, h));
        }

        // Geometry section: single composite diagram + value boxes (720×280).
        Add("Geometry diag", "WheelbaseDiagramImage", geometryW, geometryH);
        Add("Hitch Length",  "HitchLengthBox",        geometryW, geometryH);
        Add("Track",         "TrackBox",              geometryW, geometryH);
        Add("Wheelbase",     "WheelbaseBox",          geometryW, geometryH);

        // Antenna section: two diagram images + value boxes (720×345 container).
        Add("Side-view img",  "SideViewImage",    antennaW,  antennaH);
        Add("Rear-view img",  "RearViewImage",    antennaW,  antennaH);
        Add("Pivot Distance", "PivotDistanceBox", antennaW,  antennaH);
        Add("Antenna Height", "AntennaHeightBox", antennaW,  antennaH);
        Add("Antenna Offset", "AntennaOffsetBox", antennaW,  antennaH);
    }

    private void OnEditLayoutToggleChanged(object? sender, RoutedEventArgs e)
    {
        var newMode = (sender as ToggleButton)?.IsChecked == true;
        if (newMode == _editMode) return;
        _editMode = newMode;

        foreach (var d in _draggables)
        {
            if (_editMode)
            {
                d.Control.Cursor = new Cursor(StandardCursorType.SizeAll);
                d.Control.AddHandler(InputElement.PointerPressedEvent, OnDragPointerPressed, RoutingStrategies.Tunnel);
                d.Control.AddHandler(InputElement.PointerMovedEvent, OnDragPointerMoved, RoutingStrategies.Tunnel);
                d.Control.AddHandler(InputElement.PointerReleasedEvent, OnDragPointerReleased, RoutingStrategies.Tunnel);
            }
            else
            {
                d.Control.Cursor = Cursor.Default;
                d.Control.RemoveHandler(InputElement.PointerPressedEvent, OnDragPointerPressed);
                d.Control.RemoveHandler(InputElement.PointerMovedEvent, OnDragPointerMoved);
                d.Control.RemoveHandler(InputElement.PointerReleasedEvent, OnDragPointerReleased);
            }
        }

        UpdateReadout();
    }

    private void OnDragPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Control c) return;
        var d = _draggables.FirstOrDefault(x => x.Control == c);
        if (d == null || c.Parent is not Visual parent) return;

        _dragging = d;
        _dragStartPointer = e.GetPosition(parent);
        _dragStartMargin = c.Margin;
        e.Pointer.Capture(c);
        e.Handled = true;
    }

    private void OnDragPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_dragging == null) return;
        if (_dragging.Control.Parent is not Visual parent) return;

        var pos = e.GetPosition(parent);
        var dx = pos.X - _dragStartPointer.X;
        var dy = pos.Y - _dragStartPointer.Y;
        _dragging.Control.Margin = new Thickness(
            _dragStartMargin.Left + dx,
            _dragStartMargin.Top + dy,
            0, 0);
        UpdateReadout();
        e.Handled = true;
    }

    private void OnDragPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_dragging == null) return;

        // Snap to integer percent before letting go so the readout is
        // stable and the locked-in margin reproduces cleanly.
        var d = _dragging;
        var (pctX, pctY) = ToPercent(d);
        var pixX = (int)System.Math.Round(pctX / 100.0 * d.ContainerWidth);
        var pixY = (int)System.Math.Round(pctY / 100.0 * d.ContainerHeight);
        d.Control.Margin = new Thickness(pixX, pixY, 0, 0);

        // Mirror to stdout so the locked position is searchable when the
        // app is launched via `dotnet run` (Debug.WriteLine doesn't reach
        // stdout — that wrote nothing during the 2026-05-14 PoC session).
        Console.WriteLine($"[TractorPage layout] {d.Label}: X={pctX:F0}% Y={pctY:F0}% (Margin={pixX},{pixY},0,0)");

        e.Pointer.Capture(null);
        _dragging = null;
        UpdateReadout();
        e.Handled = true;
    }

    private (double pctX, double pctY) ToPercent(Draggable d) =>
        (d.Control.Margin.Left / d.ContainerWidth * 100.0,
         d.Control.Margin.Top / d.ContainerHeight * 100.0);

    private void UpdateReadout()
    {
        var readout = this.FindControl<TextBlock>("LayoutReadout");
        if (readout == null) return;

        if (!_editMode) { readout.Text = ""; return; }

        if (_dragging != null)
        {
            var (x, y) = ToPercent(_dragging);
            readout.Text = $"{_dragging.Label}: X={x:F0}% Y={y:F0}%";
        }
        else
        {
            // Idle in edit mode: render the full list of current positions
            // on one line so the user can read them all off at once.
            readout.Text = string.Join("  ·  ",
                _draggables.Select(d =>
                {
                    var (x, y) = ToPercent(d);
                    return $"{d.Label}=({x:F0},{y:F0})";
                }));
        }
    }

    private void OnSubTabClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string tag })
            SetActiveSubTab(tag);
    }

    /// <summary>
    /// Home button. The page's DataContext is ConfigurationViewModel,
    /// so walk up the visual tree until we find an ancestor whose
    /// DataContext is the MainViewModel and invoke GoHomeCommand on it.
    /// Walking the visual tree works on every platform; the older
    /// TopLevel.DataContext lookup only resolved on Desktop where the
    /// Window itself carried the MainViewModel.
    /// </summary>
    private void OnHomeClicked(object? sender, RoutedEventArgs e)
    {
        foreach (var ancestor in this.GetVisualAncestors())
        {
            if (ancestor is StyledElement el
                && el.DataContext is MainViewModel vm
                && vm.GoHomeCommand.CanExecute(null))
            {
                vm.GoHomeCommand.Execute(null);
                return;
            }
        }
    }

    private void SetActiveSubTab(string tag)
    {
        var tabs = new Dictionary<string, (StackPanel? tab, Control? section)>
        {
            ["Dimensions"]     = (this.FindControl<StackPanel>("TabDimensions"),     this.FindControl<Control>("SectionDimensions")),
            ["UTurnTramLines"] = (this.FindControl<StackPanel>("TabUTurnTramLines"), this.FindControl<Control>("SectionUTurnTramLines")),
            ["MachineControl"] = (this.FindControl<StackPanel>("TabMachineControl"), this.FindControl<Control>("SectionMachineControl")),
            ["Gps"]            = (this.FindControl<StackPanel>("TabGps"),            this.FindControl<Control>("SectionGps")),
        };

        foreach (var (key, (tab, section)) in tabs)
        {
            var active = key == tag;
            if (tab != null)
            {
                tab.Classes.Set("Active", active);
                if (tab.Children.Count > 0 && tab.Children[0] is Button btn)
                    btn.Classes.Set("Active", active);
            }
            if (section != null)
                section.IsVisible = active;
        }
    }
}

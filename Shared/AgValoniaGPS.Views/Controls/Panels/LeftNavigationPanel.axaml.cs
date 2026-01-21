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

namespace AgValoniaGPS.Views.Controls.Panels;

public partial class LeftNavigationPanel : DraggableRotatablePanel
{
    public LeftNavigationPanel()
    {
        InitializeComponent();

        // Initialize drag and rotate behavior from base class
        InitializeDragRotate();

        // Wire up sub-panel drag events
        WireUpSubPanelDrag<SimulatorPanel>("SimulatorPanelControl");
        WireUpSubPanelDrag<ViewSettingsPanel>("ViewSettingsPanelControl");
        WireUpSubPanelDrag<FileMenuPanel>("FileMenuPanelControl");
        WireUpSubPanelDrag<ToolsPanel>("ToolsPanelControl");
        WireUpSubPanelDrag<ConfigurationPanel>("ConfigurationPanelControl");
        WireUpSubPanelDrag<JobMenuPanel>("JobMenuPanelControl");
        WireUpSubPanelDrag<FieldToolsPanel>("FieldToolsPanelControl");
        WireUpSubPanelDrag<BoundaryRecordingPanel>("BoundaryRecordingPanelControl");
        WireUpSubPanelDrag<BoundaryPlayerPanel>("BoundaryPlayerPanelControl");
    }

    private void WireUpSubPanelDrag<T>(string controlName) where T : UserControl
    {
        var panel = this.FindControl<T>(controlName);
        if (panel == null) return;

        // Use reflection to check for DragMoved event
        var dragMovedEvent = typeof(T).GetEvent("DragMoved");
        if (dragMovedEvent != null)
        {
            dragMovedEvent.AddEventHandler(panel, new EventHandler<Vector>((sender, delta) =>
            {
                if (sender is Control control)
                {
                    var left = Canvas.GetLeft(control);
                    var top = Canvas.GetTop(control);
                    if (double.IsNaN(left)) left = 0;
                    if (double.IsNaN(top)) top = 0;
                    Canvas.SetLeft(control, left + delta.X);
                    Canvas.SetTop(control, top + delta.Y);
                }
            }));
        }
    }
}

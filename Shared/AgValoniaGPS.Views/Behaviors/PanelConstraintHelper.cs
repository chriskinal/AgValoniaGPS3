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

namespace AgValoniaGPS.Views.Behaviors;

/// <summary>
/// Shared helper for constraining floating panels to stay within view bounds during window resize.
/// </summary>
public static class PanelConstraintHelper
{
    /// <summary>
    /// Constrains a panel that uses Canvas.Left/Top positioning to stay within bounds.
    /// </summary>
    public static void ConstrainLeftTopPanel(Control? panel, double viewWidth, double viewHeight,
        double defaultLeft = 0, double defaultTop = 0)
    {
        if (panel == null || panel.Bounds.Width <= 0) return;

        double currentLeft = Canvas.GetLeft(panel);
        double currentTop = Canvas.GetTop(panel);

        if (double.IsNaN(currentLeft)) currentLeft = defaultLeft;
        if (double.IsNaN(currentTop)) currentTop = defaultTop;

        double maxLeft = viewWidth - panel.Bounds.Width;
        double maxTop = viewHeight - panel.Bounds.Height;

        Canvas.SetLeft(panel, Math.Clamp(currentLeft, 0, Math.Max(0, maxLeft)));
        Canvas.SetTop(panel, Math.Clamp(currentTop, 0, Math.Max(0, maxTop)));
    }

    /// <summary>
    /// Constrains a panel that uses Canvas.Right/Top positioning to stay within bounds.
    /// When the window becomes too narrow, switches to Canvas.Left positioning.
    /// If both Canvas.Left and Canvas.Right are set, prefers Canvas.Left (our convention after dragging).
    /// </summary>
    public static void ConstrainRightTopPanel(Control? panel, double viewWidth, double viewHeight,
        double defaultTop = 100)
    {
        if (panel == null || panel.Bounds.Width <= 0) return;

        double currentRight = Canvas.GetRight(panel);
        double currentLeft = Canvas.GetLeft(panel);
        double currentTop = Canvas.GetTop(panel);

        if (double.IsNaN(currentTop)) currentTop = defaultTop;

        // Constrain vertical position
        double maxTop = viewHeight - panel.Bounds.Height;
        Canvas.SetTop(panel, Math.Clamp(currentTop, 0, Math.Max(0, maxTop)));

        // Prefer Canvas.Left if set (our convention after dragging), otherwise use Canvas.Right
        if (!double.IsNaN(currentLeft))
        {
            // Using Canvas.Left - clamp normally
            // Also ensure Canvas.Right is cleared to avoid conflicts
            if (!double.IsNaN(currentRight))
                panel.ClearValue(Canvas.RightProperty);

            double maxLeft = viewWidth - panel.Bounds.Width;
            Canvas.SetLeft(panel, Math.Clamp(currentLeft, 0, Math.Max(0, maxLeft)));
        }
        else if (!double.IsNaN(currentRight))
        {
            // Using Canvas.Right - check if panel would go off left edge
            double panelLeftEdge = viewWidth - currentRight - panel.Bounds.Width;
            if (panelLeftEdge < 0)
            {
                // Panel would go off left edge - switch to Canvas.Left=0
                panel.ClearValue(Canvas.RightProperty);
                Canvas.SetLeft(panel, 0);
            }
            // Otherwise keep Canvas.Right as-is (panel stays anchored to right edge)
        }
    }

    /// <summary>
    /// Constrains a panel with extra margin for sub-panels that extend beyond its bounds.
    /// </summary>
    public static void ConstrainPanelWithExtent(Control? panel, double viewWidth, double viewHeight,
        double subPanelExtent, double defaultLeft = 20, double defaultTop = 100)
    {
        if (panel == null || panel.Bounds.Width <= 0) return;

        double currentLeft = Canvas.GetLeft(panel);
        double currentTop = Canvas.GetTop(panel);

        if (double.IsNaN(currentLeft)) currentLeft = defaultLeft;
        if (double.IsNaN(currentTop)) currentTop = defaultTop;

        double maxLeft = viewWidth - panel.Bounds.Width - subPanelExtent;
        double maxTop = viewHeight - panel.Bounds.Height;

        Canvas.SetLeft(panel, Math.Clamp(currentLeft, 0, Math.Max(0, maxLeft)));
        Canvas.SetTop(panel, Math.Clamp(currentTop, 0, Math.Max(0, maxTop)));
    }

    /// <summary>
    /// Constrains sub-panels inside a parent panel (like LeftNavigationPanel) to stay within view bounds.
    /// Sub-panels are positioned relative to the parent, so we calculate their absolute position.
    /// </summary>
    public static void ConstrainSubPanels(Control? parentPanel, double viewWidth, double viewHeight,
        string[] subPanelNames, double defaultRelativeLeft = 90, double defaultRelativeTop = 0)
    {
        if (parentPanel == null) return;

        // Get parent panel's absolute position
        double parentX = Canvas.GetLeft(parentPanel);
        double parentY = Canvas.GetTop(parentPanel);
        if (double.IsNaN(parentX)) parentX = 20;
        if (double.IsNaN(parentY)) parentY = 100;

        foreach (var name in subPanelNames)
        {
            var subPanel = parentPanel.FindControl<Control>(name);
            if (subPanel == null || subPanel.Bounds.Width <= 0) continue;

            double relativeLeft = Canvas.GetLeft(subPanel);
            double relativeTop = Canvas.GetTop(subPanel);
            if (double.IsNaN(relativeLeft)) relativeLeft = defaultRelativeLeft;
            if (double.IsNaN(relativeTop)) relativeTop = defaultRelativeTop;

            // Calculate absolute position of sub-panel
            double absoluteRight = parentX + relativeLeft + subPanel.Bounds.Width;
            double absoluteBottom = parentY + relativeTop + subPanel.Bounds.Height;

            // If sub-panel extends beyond view, adjust its relative position
            if (absoluteRight > viewWidth)
            {
                double newRelativeLeft = Math.Max(0, viewWidth - parentX - subPanel.Bounds.Width);
                Canvas.SetLeft(subPanel, newRelativeLeft);
            }
            if (absoluteBottom > viewHeight)
            {
                double newRelativeTop = Math.Max(0, viewHeight - parentY - subPanel.Bounds.Height);
                Canvas.SetTop(subPanel, newRelativeTop);
            }
        }
    }

    /// <summary>
    /// Standard sub-panel names used in LeftNavigationPanel
    /// </summary>
    public static readonly string[] LeftNavSubPanelNames = new[]
    {
        "SimulatorPanelControl", "ViewSettingsPanelControl", "FileMenuPanelControl",
        "ToolsPanelControl", "ConfigurationPanelControl", "JobMenuPanelControl",
        "FieldToolsPanelControl", "BoundaryRecordingPanelControl", "BoundaryPlayerPanelControl"
    };
}

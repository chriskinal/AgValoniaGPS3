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
using Avalonia.Layout;
using Avalonia.Media;

namespace AgValoniaGPS.Views.Controls;

/// <summary>
/// Development-only positioning aid. Draws a 10×10 grid of semi-
/// transparent cells over its parent, with percentage labels at the
/// top edge (X) and left edge (Y). Mark a dot on a screenshot and the
/// (X%, Y%) reading converts directly into Margin/Grid placement for
/// overlaid value boxes. Remove once positions are locked.
/// </summary>
public class CoordinateGridOverlay : UserControl
{
    public CoordinateGridOverlay()
    {
        var grid = new Grid();
        for (int i = 0; i < 10; i++)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Star));
        }

        var lineBrush = new SolidColorBrush(Color.FromArgb(120, 255, 215, 0));   // semi-transparent gold
        var labelBrush = new SolidColorBrush(Color.FromArgb(220, 255, 215, 0));

        // Each cell: 1px border on right + bottom forms the grid lines.
        // Last column / last row skip their outer edges so the overlay
        // doesn't render a border on the section's outermost boundary.
        for (int row = 0; row < 10; row++)
        for (int col = 0; col < 10; col++)
        {
            var border = new Border
            {
                BorderBrush = lineBrush,
                BorderThickness = new Thickness(
                    0, 0,
                    col < 9 ? 1 : 0,
                    row < 9 ? 1 : 0),
            };
            Grid.SetColumn(border, col);
            Grid.SetRow(border, row);
            grid.Children.Add(border);
        }

        // X-axis (column) labels along the top: "10" at the right edge
        // of column 0, "20" at the right edge of column 1, ...
        for (int col = 0; col < 10; col++)
        {
            var label = new TextBlock
            {
                Text = $"{(col + 1) * 10}",
                FontSize = 10,
                FontWeight = FontWeight.SemiBold,
                Foreground = labelBrush,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 2, 3, 0),
            };
            Grid.SetColumn(label, col);
            Grid.SetRow(label, 0);
            grid.Children.Add(label);
        }

        // Y-axis (row) labels along the left: "10" at the bottom edge
        // of row 0, "20" at the bottom edge of row 1, ...
        for (int row = 0; row < 10; row++)
        {
            var label = new TextBlock
            {
                Text = $"{(row + 1) * 10}",
                FontSize = 10,
                FontWeight = FontWeight.SemiBold,
                Foreground = labelBrush,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(3, 0, 0, 0),
            };
            Grid.SetColumn(label, 0);
            Grid.SetRow(label, row);
            grid.Children.Add(label);
        }

        Content = grid;
        IsHitTestVisible = false;
    }
}

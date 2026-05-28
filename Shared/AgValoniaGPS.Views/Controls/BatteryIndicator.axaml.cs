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
using Avalonia.Controls.Shapes;
using Avalonia.Media;

namespace AgValoniaGPS.Views.Controls;

public partial class BatteryIndicator : UserControl
{
    public static readonly StyledProperty<double> LevelProperty =
        AvaloniaProperty.Register<BatteryIndicator, double>(nameof(Level), 0.0);

    public static readonly StyledProperty<bool> IsChargingProperty =
        AvaloniaProperty.Register<BatteryIndicator, bool>(nameof(IsCharging), false);

    public double Level
    {
        get => GetValue(LevelProperty);
        set => SetValue(LevelProperty, value);
    }

    public bool IsCharging
    {
        get => GetValue(IsChargingProperty);
        set => SetValue(IsChargingProperty, value);
    }

    public BatteryIndicator()
    {
        InitializeComponent();
        Render();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == LevelProperty || change.Property == IsChargingProperty)
        {
            Render();
        }
    }

    private void Render()
    {
        var fill = this.FindControl<Rectangle>("FillBar");
        var bolt = this.FindControl<Avalonia.Controls.Shapes.Path>("ChargeBolt");
        if (fill is null || bolt is null) return;

        var clamped = Math.Clamp(Level, 0.0, 1.0);
        // Inner fill region is 18 px tall (22 px outer body minus 3 px top
        // breathing room minus 1 px floor margin); fill grows up from the
        // bottom so a drop in level shrinks the bar visually.
        fill.Height = 18.0 * clamped;

        // Colour bands: ≥40 % green, ≥15 % gold, otherwise red.
        IBrush brush = clamped >= 0.4 ? Brushes.LimeGreen
                       : clamped >= 0.15 ? Brushes.Gold
                       : Brushes.OrangeRed;
        fill.Fill = brush;

        bolt.IsVisible = IsCharging;
    }
}

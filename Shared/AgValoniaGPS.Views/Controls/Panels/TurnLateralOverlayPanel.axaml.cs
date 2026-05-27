// AgValoniaGPS
// Copyright (C) 2024-2026 AgValoniaGPS Contributors
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

using Avalonia.Controls;

namespace AgValoniaGPS.Views.Controls.Panels;

/// <summary>
/// On-map U-Turn + Lateral overlay. Pure binding/layout — all behavior is in the
/// bound MainViewModel commands (manual U-turn L/R, lateral nudge L/R) and the
/// computed overlay-visibility properties.
/// </summary>
public partial class TurnLateralOverlayPanel : UserControl
{
    public TurnLateralOverlayPanel()
    {
        InitializeComponent();
    }
}

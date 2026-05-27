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
/// On-map 4-way camera pad. Tilt (vertical) / zoom (horizontal) directional keys
/// are RepeatButtons that tap-to-step and hold-to-repeat via their Command
/// bindings; the center key cycles camera mode. All behavior is in the bound
/// commands, so the control needs no code-behind beyond initialization.
/// </summary>
public partial class CameraPadControl : UserControl
{
    public CameraPadControl()
    {
        InitializeComponent();
    }
}

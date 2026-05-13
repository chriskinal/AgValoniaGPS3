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

using CommunityToolkit.Mvvm.ComponentModel;

namespace AgValoniaGPS.Models.Configuration;

/// <summary>
/// Operator access level. <see cref="Operator"/> is the default
/// day-to-day user; <see cref="Installer"/> reserves deeper
/// configuration access (calibration, hardware setup, advanced
/// geometry) for a later phase that gates UI visibility on this
/// field. Phase 1 stores the value but does not enforce.
/// See Plans/PageBasedNavigation/PLAN.md §Operator Profile.
/// </summary>
public enum OperatorLevel
{
    Operator,
    Installer,
}

/// <summary>
/// Operator profile placeholder (Phase 1) — Name + Level only.
/// </summary>
public partial class OperatorConfig : ObservableObject
{
    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private OperatorLevel _level = OperatorLevel.Operator;
}

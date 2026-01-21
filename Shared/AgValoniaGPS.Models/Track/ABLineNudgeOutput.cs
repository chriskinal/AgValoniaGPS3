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

using AgValoniaGPS.Models.Base;

namespace AgValoniaGPS.Models.Track;

/// <summary>
/// Output from AB line nudging calculation.
/// </summary>
/// <param name="NewPointA">New Point A after nudging.</param>
/// <param name="NewPointB">New Point B after nudging.</param>
public record ABLineNudgeOutput(Vec2 NewPointA, Vec2 NewPointB);

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

namespace AgValoniaGPS.Models.Coverage;

/// <summary>
/// Result of segment-based coverage analysis.
/// </summary>
/// <param name="CoveragePercent">Coverage from 0.0 (none) to 1.0 (full).</param>
/// <param name="HasAnyOverlap">True if any overlap exists.</param>
/// <param name="IsFullyCovered">True if 100% covered (within tolerance).</param>
/// <param name="UncoveredLength">Total uncovered length in meters.</param>
public readonly record struct CoverageResult(
    double CoveragePercent,
    bool HasAnyOverlap,
    bool IsFullyCovered,
    double UncoveredLength)
{
    /// <summary>
    /// No coverage result.
    /// </summary>
    public static CoverageResult None => new(0, false, false, 0);
}

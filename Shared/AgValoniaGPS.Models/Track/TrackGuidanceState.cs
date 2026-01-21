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

namespace AgValoniaGPS.Models.Track;

/// <summary>
/// Filter and integrator state for track guidance.
/// Passed between guidance calculations to maintain continuity.
/// </summary>
public class TrackGuidanceState
{
    /// <summary>
    /// Integral accumulator for PID control.
    /// </summary>
    public double Integral { get; set; }

    /// <summary>
    /// Pivot distance error (current frame).
    /// </summary>
    public double PivotDistanceError { get; set; }

    /// <summary>
    /// Pivot distance error (previous frame).
    /// </summary>
    public double PivotDistanceErrorLast { get; set; }

    /// <summary>
    /// Derivative of pivot distance error.
    /// </summary>
    public double PivotDerivative { get; set; }

    /// <summary>
    /// Frame counter for integral calculations.
    /// </summary>
    public int Counter { get; set; }

    /// <summary>
    /// Cross-track steer correction (Stanley).
    /// </summary>
    public double XTrackSteerCorrection { get; set; }

    /// <summary>
    /// Distance-based steer error (Stanley).
    /// </summary>
    public double DistSteerError { get; set; }

    /// <summary>
    /// Previous distance-based steer error (Stanley).
    /// </summary>
    public double LastDistSteerError { get; set; }

    /// <summary>
    /// Derivative of distance steer error (Stanley).
    /// </summary>
    public double DerivativeDistError { get; set; }

    /// <summary>
    /// Current location index on curve (for efficient local search).
    /// </summary>
    public int CurrentLocationIndex { get; set; }

    /// <summary>
    /// Create a fresh state for starting guidance.
    /// </summary>
    public static TrackGuidanceState Initial() => new();

    /// <summary>
    /// Create a copy of this state.
    /// </summary>
    public TrackGuidanceState Clone() => new()
    {
        Integral = Integral,
        PivotDistanceError = PivotDistanceError,
        PivotDistanceErrorLast = PivotDistanceErrorLast,
        PivotDerivative = PivotDerivative,
        Counter = Counter,
        XTrackSteerCorrection = XTrackSteerCorrection,
        DistSteerError = DistSteerError,
        LastDistSteerError = LastDistSteerError,
        DerivativeDistError = DerivativeDistError,
        CurrentLocationIndex = CurrentLocationIndex
    };
}

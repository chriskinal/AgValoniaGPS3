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

using AgValoniaGPS.Models.Track;
using AgValoniaGPS.Services.AutoSteer;
using AgValoniaGPS.Services.Interfaces;

namespace AgValoniaGPS.ViewModels;

/// <summary>
/// MainViewModel partial class containing guidance-related state and event handlers.
/// Guidance computation is done by GpsPipelineService on a background thread.
/// Results are applied via ApplyGpsCycleResult.
/// </summary>
public partial class MainViewModel
{
    #region Guidance State

    // Track guidance state (carried between iterations for track snapping/preview)
    private TrackGuidanceState? _trackGuidanceState;

    #endregion

    #region AutoSteer Event Handlers

    private void OnAutoSteerStateUpdated(object? sender, VehicleStateSnapshot state)
    {
        // AutoSteer fires StateUpdated at the unified control-loop rate
        // (100 Hz, decoupled from GPS — see Plans/Completed/
        // UNIFIED_CONTROL_LOOP_PLAN.md). Latency display can't sit on this
        // path — at 100 Hz the PropertyChanged → TextLayout cascade for
        // the status-bar "Lat: 0.00ms" readout was 5.9% main-thread CPU
        // (Phase 2b trace). We cache the latest value here and let
        // OnStatusTick publish it at 5 Hz.
        //
        // TramControlByte → map service is structural (not display) and
        // stays on the source rate. Single-double / single-byte writes are
        // atomic on x86/ARM — no lock needed.
        _latestGpsToPgnLatencyMs = state.TotalLatencyMs;
        if (_dispatcher.CheckAccess())
        {
            TramControlByte = state.TramState;
            _mapService.SetTramControlByte(state.TramState);
        }
        else
        {
            _dispatcher.Post(() =>
            {
                TramControlByte = state.TramState;
                _mapService.SetTramControlByte(state.TramState);
            });
        }
    }

    #endregion
}

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

using System.Linq;
using AgValoniaGPS.Services.Interfaces;
using Avalonia.Threading;

namespace AgValoniaGPS.ViewModels;

/// <summary>
/// MainViewModel partial class containing Boundary Recording state and event handling.
/// Manages boundary point collection and area calculation during field boundary recording.
/// </summary>
public partial class MainViewModel
{
    #region Boundary Recording Fields

    private bool _isBoundaryRecording;
    private int _boundaryPointCount;
    private double _boundaryAreaHectares;

    #endregion

    #region Boundary Recording Properties

    public bool IsBoundaryRecording
    {
        get => _isBoundaryRecording;
        set => SetProperty(ref _isBoundaryRecording, value);
    }

    public int BoundaryPointCount
    {
        get => _boundaryPointCount;
        set => SetProperty(ref _boundaryPointCount, value);
    }

    public double BoundaryAreaHectares
    {
        get => _boundaryAreaHectares;
        set => SetProperty(ref _boundaryAreaHectares, value);
    }

    #endregion

    #region Boundary Recording Event Handlers

    private void OnBoundaryPointAdded(object? sender, BoundaryPointAddedEventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            // Update centralized state
            State.BoundaryRec.PointCount = e.TotalPoints;
            State.BoundaryRec.AreaHectares = e.AreaHectares;
            State.BoundaryRec.AreaAcres = e.AreaHectares * 2.47105;

            // Legacy properties
            BoundaryPointCount = e.TotalPoints;
            BoundaryAreaHectares = e.AreaHectares;

            // Update map with recorded points
            var points = _boundaryRecordingService.RecordedPoints
                .Select(p => (p.Easting, p.Northing))
                .ToList();
            _mapService.SetRecordingPoints(points);
        });
    }

    private void OnBoundaryStateChanged(object? sender, BoundaryRecordingStateChangedEventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            // Update centralized state
            State.BoundaryRec.IsRecording = e.State == BoundaryRecordingState.Recording;
            State.BoundaryRec.IsPaused = e.State == BoundaryRecordingState.Paused;
            State.BoundaryRec.PointCount = e.PointCount;
            State.BoundaryRec.AreaHectares = e.AreaHectares;
            State.BoundaryRec.AreaAcres = e.AreaHectares * 2.47105;

            // Legacy properties
            IsBoundaryRecording = e.State == BoundaryRecordingState.Recording;
            BoundaryPointCount = e.PointCount;
            BoundaryAreaHectares = e.AreaHectares;

            // Clear recording points from map when recording becomes idle
            if (e.State == BoundaryRecordingState.Idle)
            {
                State.BoundaryRec.RecordingPoints.Clear();
                _mapService.ClearRecordingPoints();
            }
            // Update map with current recorded points (for undo/clear operations)
            else if (e.PointCount >= 0)
            {
                var points = _boundaryRecordingService.RecordedPoints
                    .Select(p => (p.Easting, p.Northing))
                    .ToList();
                _mapService.SetRecordingPoints(points);
            }
        });
    }

    #endregion
}

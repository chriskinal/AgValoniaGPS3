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

using System.Collections.ObjectModel;
using AgValoniaGPS.Models.Base;
using ReactiveUI;

namespace AgValoniaGPS.Models.State;

/// <summary>
/// Boundary recording state (distinct from field boundaries in FieldState).
/// </summary>
public class BoundaryRecState : ReactiveObject
{
    private bool _isRecording;
    public bool IsRecording
    {
        get => _isRecording;
        set => this.RaiseAndSetIfChanged(ref _isRecording, value);
    }

    private bool _isPaused;
    public bool IsPaused
    {
        get => _isPaused;
        set => this.RaiseAndSetIfChanged(ref _isPaused, value);
    }

    private int _pointCount;
    public int PointCount
    {
        get => _pointCount;
        set => this.RaiseAndSetIfChanged(ref _pointCount, value);
    }

    private double _areaHectares;
    public double AreaHectares
    {
        get => _areaHectares;
        set => this.RaiseAndSetIfChanged(ref _areaHectares, value);
    }

    private double _areaAcres;
    public double AreaAcres
    {
        get => _areaAcres;
        set => this.RaiseAndSetIfChanged(ref _areaAcres, value);
    }

    // Recording options
    private bool _isDrawRightSide = true;
    public bool IsDrawRightSide
    {
        get => _isDrawRightSide;
        set => this.RaiseAndSetIfChanged(ref _isDrawRightSide, value);
    }

    private bool _isDrawAtPivot;
    public bool IsDrawAtPivot
    {
        get => _isDrawAtPivot;
        set => this.RaiseAndSetIfChanged(ref _isDrawAtPivot, value);
    }

    private double _boundaryOffset;
    public double BoundaryOffset
    {
        get => _boundaryOffset;
        set => this.RaiseAndSetIfChanged(ref _boundaryOffset, value);
    }

    // Current recording points
    public ObservableCollection<Vec2> RecordingPoints { get; } = new();

    // Live preview line
    private Vec2? _lastRecordedPoint;
    public Vec2? LastRecordedPoint
    {
        get => _lastRecordedPoint;
        set => this.RaiseAndSetIfChanged(ref _lastRecordedPoint, value);
    }

    public void Reset()
    {
        IsRecording = false;
        IsPaused = false;
        PointCount = 0;
        AreaHectares = 0;
        AreaAcres = 0;
        RecordingPoints.Clear();
        LastRecordedPoint = null;
    }

    public void AddPoint(Vec2 point)
    {
        RecordingPoints.Add(point);
        LastRecordedPoint = point;
        PointCount = RecordingPoints.Count;
    }
}

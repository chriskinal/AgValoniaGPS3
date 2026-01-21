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
using System.Collections.Generic;
using AgValoniaGPS.Models;
using AgValoniaGPS.Services.Interfaces;

namespace AgValoniaGPS.Services;

/// <summary>
/// Service for recording boundary polygons during GPS driving
/// Collects GPS waypoints and creates boundary polygons
/// </summary>
public class BoundaryRecordingService : IBoundaryRecordingService
{
    private readonly List<BoundaryPoint> _recordedPoints = new();
    private BoundaryRecordingState _state = BoundaryRecordingState.Idle;
    private BoundaryType _currentBoundaryType = BoundaryType.Outer;
    private double _minPointSpacing = 1.0; // Default 1 meter between points (matches AgOpenGPS)
    private BoundaryPoint? _lastPoint;

    /// <inheritdoc/>
    public BoundaryRecordingState State => _state;

    /// <inheritdoc/>
    public bool IsRecording => _state == BoundaryRecordingState.Recording;

    /// <inheritdoc/>
    public BoundaryType CurrentBoundaryType => _currentBoundaryType;

    /// <inheritdoc/>
    public int PointCount => _recordedPoints.Count;

    /// <inheritdoc/>
    public double AreaHectares => CalculateArea();

    /// <inheritdoc/>
    public double MinPointSpacing
    {
        get => _minPointSpacing;
        set => _minPointSpacing = Math.Max(0.5, value); // Minimum 0.5m spacing
    }

    /// <inheritdoc/>
    public IReadOnlyList<BoundaryPoint> RecordedPoints => _recordedPoints.AsReadOnly();

    /// <inheritdoc/>
    public event EventHandler<BoundaryRecordingStateChangedEventArgs>? StateChanged;

    /// <inheritdoc/>
    public event EventHandler<BoundaryPointAddedEventArgs>? PointAdded;

    /// <inheritdoc/>
    public void StartRecording(BoundaryType boundaryType = BoundaryType.Outer)
    {
        _recordedPoints.Clear();
        _lastPoint = null;
        _currentBoundaryType = boundaryType;
        _state = BoundaryRecordingState.Recording;

        OnStateChanged();
    }

    /// <inheritdoc/>
    public void PauseRecording()
    {
        if (_state == BoundaryRecordingState.Recording)
        {
            _state = BoundaryRecordingState.Paused;
            OnStateChanged();
        }
    }

    /// <inheritdoc/>
    public void ResumeRecording()
    {
        if (_state == BoundaryRecordingState.Paused)
        {
            _state = BoundaryRecordingState.Recording;
            OnStateChanged();
        }
    }

    /// <inheritdoc/>
    public BoundaryPolygon? StopRecording()
    {
        if (_state == BoundaryRecordingState.Idle)
        {
            return null;
        }

        _state = BoundaryRecordingState.Idle;

        // Need at least 3 points for a valid polygon
        if (_recordedPoints.Count < 3)
        {
            _recordedPoints.Clear();
            _lastPoint = null;
            OnStateChanged();
            return null;
        }

        // Create the polygon from recorded points
        var polygon = new BoundaryPolygon
        {
            IsDriveThrough = false,
            Points = new List<BoundaryPoint>(_recordedPoints)
        };

        // Clear recorded points for next recording
        _recordedPoints.Clear();
        _lastPoint = null;

        OnStateChanged();

        return polygon;
    }

    /// <inheritdoc/>
    public void CancelRecording()
    {
        _recordedPoints.Clear();
        _lastPoint = null;
        _state = BoundaryRecordingState.Idle;

        OnStateChanged();
    }

    /// <inheritdoc/>
    public void AddPoint(double easting, double northing, double heading)
    {
        if (_state != BoundaryRecordingState.Recording)
        {
            return;
        }

        // Check minimum spacing from last point
        if (_lastPoint != null)
        {
            double dx = easting - _lastPoint.Easting;
            double dy = northing - _lastPoint.Northing;
            double distance = Math.Sqrt(dx * dx + dy * dy);

            if (distance < _minPointSpacing)
            {
                return; // Too close to last point, skip
            }
        }

        var point = new BoundaryPoint(easting, northing, heading);
        _recordedPoints.Add(point);
        _lastPoint = point;

        OnPointAdded(point);
    }

    /// <inheritdoc/>
    public void AddPointManual(double easting, double northing, double heading)
    {
        // Manual add bypasses recording state check - works when paused
        if (_state == BoundaryRecordingState.Idle)
        {
            return; // Must have started recording at least once
        }

        var point = new BoundaryPoint(easting, northing, heading);
        _recordedPoints.Add(point);
        _lastPoint = point;

        OnPointAdded(point);
    }

    /// <inheritdoc/>
    public bool RemoveLastPoint()
    {
        if (_recordedPoints.Count == 0)
        {
            return false;
        }

        _recordedPoints.RemoveAt(_recordedPoints.Count - 1);
        _lastPoint = _recordedPoints.Count > 0 ? _recordedPoints[^1] : null;

        OnStateChanged();
        return true;
    }

    /// <inheritdoc/>
    public void ClearPoints()
    {
        _recordedPoints.Clear();
        _lastPoint = null;

        OnStateChanged();
    }

    /// <summary>
    /// Calculate area of current polygon using Shoelace formula
    /// </summary>
    private double CalculateArea()
    {
        if (_recordedPoints.Count < 3)
        {
            return 0;
        }

        double area = 0;
        for (int i = 0; i < _recordedPoints.Count; i++)
        {
            int j = (i + 1) % _recordedPoints.Count;
            area += _recordedPoints[i].Easting * _recordedPoints[j].Northing;
            area -= _recordedPoints[j].Easting * _recordedPoints[i].Northing;
        }

        // Convert square meters to hectares
        return Math.Abs(area) / 2.0 / 10000.0;
    }

    private void OnStateChanged()
    {
        StateChanged?.Invoke(this, new BoundaryRecordingStateChangedEventArgs(
            _state,
            _recordedPoints.Count,
            AreaHectares
        ));
    }

    private void OnPointAdded(BoundaryPoint point)
    {
        PointAdded?.Invoke(this, new BoundaryPointAddedEventArgs(
            point,
            _recordedPoints.Count,
            AreaHectares
        ));
    }
}

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

namespace AgValoniaGPS.Services.Interfaces;

/// <summary>
/// A single data point in a chart time series.
/// </summary>
public readonly struct ChartDataPoint
{
    public double Timestamp { get; init; }
    public double Value { get; init; }
}

/// <summary>
/// A named data series with color and data points for chart rendering.
/// </summary>
public class ChartSeries
{
    public string Name { get; }
    public uint Color { get; }
    private readonly List<ChartDataPoint> _points = new();
    private readonly object _lock = new();

    public ChartSeries(string name, uint color)
    {
        Name = name;
        Color = color;
    }

    public void AddPoint(double timestamp, double value)
    {
        lock (_lock)
        {
            _points.Add(new ChartDataPoint { Timestamp = timestamp, Value = value });
        }
    }

    /// <summary>
    /// Remove points older than the given timestamp.
    /// </summary>
    public void TrimBefore(double timestamp)
    {
        lock (_lock)
        {
            int removeCount = 0;
            for (int i = 0; i < _points.Count; i++)
            {
                if (_points[i].Timestamp < timestamp)
                    removeCount++;
                else
                    break;
            }
            if (removeCount > 0)
                _points.RemoveRange(0, removeCount);
        }
    }

    /// <summary>
    /// Get a snapshot of current points (thread-safe copy).
    /// </summary>
    public List<ChartDataPoint> GetPoints()
    {
        lock (_lock)
        {
            return new List<ChartDataPoint>(_points);
        }
    }

    public int Count
    {
        get { lock (_lock) { return _points.Count; } }
    }

    public void Clear()
    {
        lock (_lock)
        {
            _points.Clear();
        }
    }
}

/// <summary>
/// Provides rolling time-series data for diagnostic charts.
/// Collects data from AutoSteerService state updates and exposes
/// series for Steer, Heading, and XTE charts.
/// </summary>
public interface IChartDataService
{
    /// <summary>
    /// Time window in seconds for the rolling chart display.
    /// </summary>
    double TimeWindowSeconds { get; set; }

    /// <summary>
    /// Start collecting chart data from AutoSteerService.
    /// </summary>
    void Start();

    /// <summary>
    /// Stop collecting chart data.
    /// </summary>
    void Stop();

    /// <summary>
    /// Whether the service is actively collecting data.
    /// </summary>
    bool IsRunning { get; }

    // Steer chart series
    ChartSeries SetSteerAngle { get; }
    ChartSeries ActualSteerAngle { get; }
    ChartSeries PwmOutput { get; }

    // Heading chart series
    ChartSeries HeadingError { get; }
    ChartSeries ImuHeading { get; }
    ChartSeries GpsHeading { get; }

    // XTE chart series
    ChartSeries CrossTrackError { get; }

    /// <summary>
    /// Current time reference (seconds since service start).
    /// Used by chart controls to determine the visible window.
    /// </summary>
    double CurrentTime { get; }
}

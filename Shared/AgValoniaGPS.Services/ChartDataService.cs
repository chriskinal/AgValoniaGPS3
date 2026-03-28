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
using System.Diagnostics;
using AgValoniaGPS.Services.Interfaces;

namespace AgValoniaGPS.Services;

/// <summary>
/// Collects real-time data from AutoSteerService state updates
/// and stores rolling time-series for diagnostic chart display.
/// </summary>
public class ChartDataService : IChartDataService
{
    private readonly IAutoSteerService _autoSteerService;
    private readonly Stopwatch _stopwatch = new();
    private bool _isRunning;

    // Colors: ARGB uint values
    private const uint ColorYellow = 0xFFFFFF00;
    private const uint ColorGreen = 0xFF00FF00;
    private const uint ColorCyan = 0xFF00FFFF;
    private const uint ColorRed = 0xFFFF4444;
    private const uint ColorOrange = 0xFFFF8800;
    private const uint ColorWhite = 0xFFFFFFFF;
    private const uint ColorMagenta = 0xFFFF00FF;

    public double TimeWindowSeconds { get; set; } = 20.0;
    public bool IsRunning => _isRunning;
    public double CurrentTime => _stopwatch.Elapsed.TotalSeconds;

    // Steer chart series
    public ChartSeries SetSteerAngle { get; }
    public ChartSeries ActualSteerAngle { get; }
    public ChartSeries PwmOutput { get; }

    // Heading chart series
    public ChartSeries HeadingError { get; }
    public ChartSeries ImuHeading { get; }
    public ChartSeries GpsHeading { get; }

    // XTE chart series
    public ChartSeries CrossTrackError { get; }

    public ChartDataService(IAutoSteerService autoSteerService)
    {
        _autoSteerService = autoSteerService;

        SetSteerAngle = new ChartSeries("Set Angle", ColorYellow);
        ActualSteerAngle = new ChartSeries("Actual Angle", ColorGreen);
        PwmOutput = new ChartSeries("PWM", ColorCyan);

        HeadingError = new ChartSeries("Heading Error", ColorRed);
        ImuHeading = new ChartSeries("IMU Heading", ColorOrange);
        GpsHeading = new ChartSeries("GPS Heading", ColorWhite);

        CrossTrackError = new ChartSeries("XTE", ColorMagenta);
    }

    public void Start()
    {
        if (_isRunning) return;
        _isRunning = true;
        _stopwatch.Restart();
        _autoSteerService.StateUpdated += OnStateUpdated;
    }

    public void Stop()
    {
        if (!_isRunning) return;
        _isRunning = false;
        _autoSteerService.StateUpdated -= OnStateUpdated;
        _stopwatch.Stop();
    }

    private void OnStateUpdated(object? sender, VehicleStateSnapshot snapshot)
    {
        double t = _stopwatch.Elapsed.TotalSeconds;
        double trimTime = t - TimeWindowSeconds - 2.0; // keep 2s extra buffer

        // Steer data
        SetSteerAngle.AddPoint(t, snapshot.SteerAngle);
        ActualSteerAngle.AddPoint(t, _autoSteerService.LastSteerData.ActualSteerAngle);
        PwmOutput.AddPoint(t, _autoSteerService.LastSteerData.PwmDisplay);

        // Heading data
        HeadingError.AddPoint(t, snapshot.CrossTrackError != 0 ? ComputeHeadingError(snapshot) : 0);
        ImuHeading.AddPoint(t, _autoSteerService.LastSteerData.ImuHeading);
        GpsHeading.AddPoint(t, snapshot.Heading);

        // XTE data
        CrossTrackError.AddPoint(t, snapshot.CrossTrackError);

        // Trim old data
        if (trimTime > 0)
        {
            SetSteerAngle.TrimBefore(trimTime);
            ActualSteerAngle.TrimBefore(trimTime);
            PwmOutput.TrimBefore(trimTime);
            HeadingError.TrimBefore(trimTime);
            ImuHeading.TrimBefore(trimTime);
            GpsHeading.TrimBefore(trimTime);
            CrossTrackError.TrimBefore(trimTime);
        }
    }

    /// <summary>
    /// Compute heading error from the guidance state.
    /// The SteerAngle from guidance represents the correction needed,
    /// which serves as a proxy for heading error relative to the track.
    /// </summary>
    private static double ComputeHeadingError(VehicleStateSnapshot snapshot)
    {
        // SteerAngle is the guidance-computed steering correction.
        // When on-track with no heading error, this approaches zero.
        return snapshot.SteerAngle;
    }
}

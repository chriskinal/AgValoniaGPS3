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
using System.Globalization;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;

namespace AgValoniaGPS.ViewModels;

/// <summary>
/// Status-strip rotator: drives the two-line text stack at the left of the
/// strip. The top line is a static "Fix • Age" readout; the bottom line
/// cycles every <see cref="RotationIntervalSeconds"/> through field name,
/// field stats, and the selected AB-line's heading info. A pause button
/// stops the cycle so the user can settle on one page.
/// </summary>
public partial class MainViewModel
{
    private const int RotationIntervalSeconds = 5;

    /// <summary>Pages the bottom line cycles through.</summary>
    public enum StatusStripPage
    {
        Field,
        Stats,
        AbLine,
    }

    private DispatcherTimer? _statusStripRotationTimer;
    private StatusStripPage _statusStripPage = StatusStripPage.Field;
    private bool _isStatusStripRotationPaused;

    /// <summary>
    /// Bottom line: rotates through field / stats / AB-line content. Falls
    /// back to placeholders when the relevant data isn't available so the
    /// row never shrinks unexpectedly.
    /// </summary>
    public string StatusStripRotatingLine
    {
        get
        {
            return _statusStripPage switch
            {
                StatusStripPage.Field => HasActiveField
                    ? FormatFieldLine(ActiveFieldName, CurrentJobTaskName)
                    : "No field",

                StatusStripPage.Stats => HasActiveField
                    ? $"Done {WorkedAreaDisplay}  Left {RemainingPercent:F0}%  Rate {WorkRateDisplay}"
                    : "—",

                StatusStripPage.AbLine => SelectedTrack is { } track && track.Points.Count >= 2
                    ? FormatAbLineLine(track.Name, track.HeadingDegrees)
                    : "No AB Line",

                _ => string.Empty,
            };
        }
    }

    private static string FormatFieldLine(string? fieldName, string? jobName)
    {
        var field = string.IsNullOrEmpty(fieldName) ? "—" : fieldName;
        return string.IsNullOrEmpty(jobName)
            ? $"Field: {field}"
            : $"Field: {field}  Job: {jobName}";
    }

    private static string FormatAbLineLine(string name, double headingDeg)
    {
        var primary = ((headingDeg % 360) + 360) % 360;
        var reciprocal = (primary + 180) % 360;
        return string.Create(CultureInfo.InvariantCulture,
            $"AB Line: {name}  {primary:F1}°, {reciprocal:F1}°");
    }

    /// <summary>True when the user has tapped the pause button on the strip.</summary>
    public bool IsStatusStripRotationPaused
    {
        get => _isStatusStripRotationPaused;
        set
        {
            if (SetProperty(ref _isStatusStripRotationPaused, value))
            {
                if (value) _statusStripRotationTimer?.Stop();
                else _statusStripRotationTimer?.Start();
            }
        }
    }

    /// <summary>Pause-button command bound from the strip.</summary>
    public IRelayCommand ToggleStatusStripRotationCommand { get; private set; } = null!;

    /// <summary>
    /// Called from the MainViewModel constructor after services are wired.
    /// Starts the rotation timer and installs the pause command.
    /// </summary>
    private void InitializeStatusStripRotation()
    {
        ToggleStatusStripRotationCommand = new RelayCommand(() =>
            IsStatusStripRotationPaused = !IsStatusStripRotationPaused);

        _statusStripRotationTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(RotationIntervalSeconds),
        };
        _statusStripRotationTimer.Tick += (_, _) => AdvanceStatusStripPage();
        _statusStripRotationTimer.Start();
    }

    private void AdvanceStatusStripPage()
    {
        _statusStripPage = _statusStripPage switch
        {
            StatusStripPage.Field => StatusStripPage.Stats,
            StatusStripPage.Stats => StatusStripPage.AbLine,
            _ => StatusStripPage.Field,
        };
        OnPropertyChanged(nameof(StatusStripRotatingLine));
    }

    /// <summary>
    /// Re-raise the rotating bottom line when its underlying data changes
    /// (field opened, track selected, area updated). Cheap — bound to a
    /// single TextBlock. The top line binds directly to State.Vehicle so it
    /// updates without going through here.
    /// </summary>
    internal void RaiseStatusStripChanged()
    {
        OnPropertyChanged(nameof(StatusStripRotatingLine));
    }
}

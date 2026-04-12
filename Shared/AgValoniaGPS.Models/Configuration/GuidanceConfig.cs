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
using CommunityToolkit.Mvvm.ComponentModel;

namespace AgValoniaGPS.Models.Configuration;

/// <summary>
/// Guidance and steering algorithm configuration.
/// Replaces: steering parts of VehicleConfiguration.cs + YouTurnConfiguration.cs
/// </summary>
public class GuidanceConfig : ObservableObject
{
    // Algorithm selection
    private bool _isPurePursuit = true;
    public bool IsPurePursuit
    {
        get => _isPurePursuit;
        set
        {
            SetProperty(ref _isPurePursuit, value);
            OnPropertyChanged(nameof(IsStanley));
        }
    }

    public bool IsStanley => !IsPurePursuit;

    // Look-ahead parameters (both algorithms)
    private double _goalPointLookAheadHold = 4.0;
    public double GoalPointLookAheadHold
    {
        get => _goalPointLookAheadHold;
        set => SetProperty(ref _goalPointLookAheadHold, value);
    }

    private double _goalPointLookAheadMult = 1.4;
    public double GoalPointLookAheadMult
    {
        get => _goalPointLookAheadMult;
        set => SetProperty(ref _goalPointLookAheadMult, value);
    }

    private double _goalPointAcquireFactor = 1.5;
    public double GoalPointAcquireFactor
    {
        get => _goalPointAcquireFactor;
        set => SetProperty(ref _goalPointAcquireFactor, value);
    }

    private double _minLookAheadDistance = 2.0;
    public double MinLookAheadDistance
    {
        get => _minLookAheadDistance;
        set => SetProperty(ref _minLookAheadDistance, value);
    }

    // Pure Pursuit specific
    private double _purePursuitIntegralGain = 0.0;
    public double PurePursuitIntegralGain
    {
        get => _purePursuitIntegralGain;
        set => SetProperty(ref _purePursuitIntegralGain, value);
    }

    // Stanley specific
    private double _stanleyDistanceErrorGain = 0.8;
    public double StanleyDistanceErrorGain
    {
        get => _stanleyDistanceErrorGain;
        set => SetProperty(ref _stanleyDistanceErrorGain, value);
    }

    private double _stanleyHeadingErrorGain = 1.0;
    public double StanleyHeadingErrorGain
    {
        get => _stanleyHeadingErrorGain;
        set => SetProperty(ref _stanleyHeadingErrorGain, value);
    }

    private double _stanleyIntegralGainAB = 0.0;
    public double StanleyIntegralGainAB
    {
        get => _stanleyIntegralGainAB;
        set => SetProperty(ref _stanleyIntegralGainAB, value);
    }

    private double _stanleyIntegralDistanceAwayTriggerAB = 0.3;
    public double StanleyIntegralDistanceAwayTriggerAB
    {
        get => _stanleyIntegralDistanceAwayTriggerAB;
        set => SetProperty(ref _stanleyIntegralDistanceAwayTriggerAB, value);
    }

    // Dead zone
    private double _deadZoneHeading = 0.5;
    public double DeadZoneHeading
    {
        get => _deadZoneHeading;
        set => SetProperty(ref _deadZoneHeading, value);
    }

    private int _deadZoneDelay = 10;
    public int DeadZoneDelay
    {
        get => _deadZoneDelay;
        set => SetProperty(ref _deadZoneDelay, value);
    }

    // U-Turn settings (merged from YouTurnConfiguration)
    private double _uTurnRadius = 8.0;
    public double UTurnRadius
    {
        get => _uTurnRadius;
        set => SetProperty(ref _uTurnRadius, value);
    }

    private double _uTurnExtension = 20.0;
    public double UTurnExtension
    {
        get => _uTurnExtension;
        set => SetProperty(ref _uTurnExtension, value);
    }

    private double _uTurnDistanceFromBoundary = 2.0;
    public double UTurnDistanceFromBoundary
    {
        get => _uTurnDistanceFromBoundary;
        set => SetProperty(ref _uTurnDistanceFromBoundary, value);
    }

    private int _uTurnSkipWidth = 1;
    public int UTurnSkipWidth
    {
        get => _uTurnSkipWidth;
        set => SetProperty(ref _uTurnSkipWidth, Math.Max(1, value));
    }

    private int _uTurnStyle;
    public int UTurnStyle
    {
        get => _uTurnStyle;
        set => SetProperty(ref _uTurnStyle, value);
    }

    private double _uTurnCompensation = 1.0;
    public double UTurnCompensation
    {
        get => _uTurnCompensation;
        set => SetProperty(ref _uTurnCompensation, value);
    }

    private int _uTurnSmoothing = 14;
    public int UTurnSmoothing
    {
        get => _uTurnSmoothing;
        set => SetProperty(ref _uTurnSmoothing, Math.Clamp(value, 1, 50));
    }

    // Tram Lines
    private int _tramPasses = 3;
    public int TramPasses
    {
        get => _tramPasses;
        set => SetProperty(ref _tramPasses, Math.Max(1, value));
    }

    private bool _tramDisplay = true;
    public bool TramDisplay
    {
        get => _tramDisplay;
        set => SetProperty(ref _tramDisplay, value);
    }

    private int _tramLine = 1;
    public int TramLine
    {
        get => _tramLine;
        set => SetProperty(ref _tramLine, Math.Max(1, value));
    }

    // Hydraulic lift look-ahead distances
    private double _hydLiftLookAheadDistanceLeft = 1.0;
    public double HydLiftLookAheadDistanceLeft
    {
        get => _hydLiftLookAheadDistanceLeft;
        set => SetProperty(ref _hydLiftLookAheadDistanceLeft, value);
    }

    private double _hydLiftLookAheadDistanceRight = 1.0;
    public double HydLiftLookAheadDistanceRight
    {
        get => _hydLiftLookAheadDistanceRight;
        set => SetProperty(ref _hydLiftLookAheadDistanceRight, value);
    }
}

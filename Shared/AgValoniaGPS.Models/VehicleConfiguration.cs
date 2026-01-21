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

namespace AgValoniaGPS.Models;

/// <summary>
/// LEGACY: Vehicle configuration for AgOpenGPS profile compatibility.
/// New code should use ConfigurationStore.Instance.Vehicle and ConfigurationStore.Instance.Guidance.
/// This class is retained only for reading/writing AgOpenGPS vehicle XML files.
/// </summary>
[Obsolete("Use ConfigurationStore.Instance.Vehicle/Guidance instead. Retained for AgOpenGPS profile compatibility.")]
public class VehicleConfiguration
{
    // Vehicle physical dimensions
    public double AntennaHeight { get; set; } = 3.0; // meters
    public double AntennaPivot { get; set; } = 0.0; // meters from pivot to antenna
    public double AntennaOffset { get; set; } = 0.0; // lateral offset
    public double Wheelbase { get; set; } = 2.5; // meters
    public double TrackWidth { get; set; } = 1.8; // meters

    // Vehicle type (0=Tractor, 1=Harvester, 2=4WD)
    public VehicleType Type { get; set; } = VehicleType.Tractor;

    // Steering limits
    public double MaxSteerAngle { get; set; } = 35.0; // degrees
    public double MaxAngularVelocity { get; set; } = 35.0; // degrees/second

    // Guidance look-ahead parameters
    public double GoalPointLookAheadHold { get; set; } = 4.0; // meters when on line
    public double GoalPointLookAheadMult { get; set; } = 1.4; // speed multiplier
    public double GoalPointAcquireFactor { get; set; } = 1.5; // factor when acquiring line
    public double MinLookAheadDistance { get; set; } = 2.0; // meters

    // Stanley steering algorithm parameters
    public double StanleyDistanceErrorGain { get; set; } = 0.8;
    public double StanleyHeadingErrorGain { get; set; } = 1.0;
    public double StanleyIntegralGainAB { get; set; } = 0.0;
    public double StanleyIntegralDistanceAwayTriggerAB { get; set; } = 0.3; // meters

    // Pure Pursuit algorithm parameters
    public double PurePursuitIntegralGain { get; set; } = 0.0;

    // Heading dead zone
    public double DeadZoneHeading { get; set; } = 0.5; // degrees (* 0.01 in original)
    public int DeadZoneDelay { get; set; } = 10; // cycles

    // U-turn compensation
    public double UTurnCompensation { get; set; } = 1.0;

    // Hydraulic lift look-ahead distances
    public double HydLiftLookAheadDistanceLeft { get; set; } = 1.0; // meters
    public double HydLiftLookAheadDistanceRight { get; set; } = 1.0; // meters
}

// Note: VehicleType and SteeringAlgorithm enums moved to Enums/VehicleEnums.cs

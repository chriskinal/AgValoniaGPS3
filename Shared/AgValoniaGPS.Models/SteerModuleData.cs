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

namespace AgValoniaGPS.Models;

/// <summary>
/// Parsed data from PGN 253 (Steer Data FROM Module).
/// Immutable record containing actual steering sensor readings.
/// </summary>
/// <param name="ActualSteerAngle">Actual wheel angle in degrees (from WAS sensor)</param>
/// <param name="ImuHeading">Heading from IMU in degrees (0-360) - deprecated, GNSS supplies this now</param>
/// <param name="ImuRoll">Roll angle from IMU in degrees - deprecated, GNSS supplies this now</param>
/// <param name="WorkSwitchActive">Work switch is engaged (ON)</param>
/// <param name="SteerSwitchActive">Steer switch is engaged (steering enabled)</param>
/// <param name="RemoteButtonPressed">Remote steer button is pressed</param>
/// <param name="VwasFusionActive">Virtual WAS fusion is active</param>
/// <param name="PwmDisplay">Current PWM output magnitude (0-255)</param>
public readonly record struct SteerModuleData(
    double ActualSteerAngle,
    double ImuHeading,
    double ImuRoll,
    bool WorkSwitchActive,
    bool SteerSwitchActive,
    bool RemoteButtonPressed,
    bool VwasFusionActive,
    byte PwmDisplay)
{
    /// <summary>
    /// Indicates valid data was received (used to check parse success).
    /// </summary>
    public bool IsValid => true;

    /// <summary>
    /// Empty/invalid data instance.
    /// </summary>
    public static SteerModuleData Empty => default;
}

/// <summary>
/// Parsed data from PGN 250 (Sensor Data FROM Module).
/// Contains pressure/current sensor readings if hardware supports it.
/// </summary>
/// <param name="SensorValue">Raw sensor value (0-255), interpretation depends on hardware config</param>
public readonly record struct SensorModuleData(byte SensorValue)
{
    /// <summary>
    /// Empty/invalid data instance.
    /// </summary>
    public static SensorModuleData Empty => default;
}

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

using ReactiveUI;

namespace AgValoniaGPS.Models.Configuration;

/// <summary>
/// Runtime sensor state - updated every GPS frame.
/// NOT persisted, NOT part of ConfigurationStore.
/// This separates runtime state from configuration.
/// </summary>
public class SensorState : ReactiveObject
{
    private static SensorState? _instance;

    /// <summary>
    /// Singleton instance for runtime sensor state
    /// </summary>
    public static SensorState Instance => _instance ??= new SensorState();

    /// <summary>
    /// For testing - allows replacing the singleton instance
    /// </summary>
    public static void SetInstance(SensorState state) => _instance = state;

    // IMU/AHRS runtime values
    private double _imuHeading = 99999;
    public double ImuHeading
    {
        get => _imuHeading;
        set => this.RaiseAndSetIfChanged(ref _imuHeading, value);
    }

    private double _imuRoll;
    public double ImuRoll
    {
        get => _imuRoll;
        set => this.RaiseAndSetIfChanged(ref _imuRoll, value);
    }

    private double _imuPitch;
    public double ImuPitch
    {
        get => _imuPitch;
        set => this.RaiseAndSetIfChanged(ref _imuPitch, value);
    }

    private double _imuYawRate;
    public double ImuYawRate
    {
        get => _imuYawRate;
        set => this.RaiseAndSetIfChanged(ref _imuYawRate, value);
    }

    private short _angularVelocity;
    public short AngularVelocity
    {
        get => _angularVelocity;
        set => this.RaiseAndSetIfChanged(ref _angularVelocity, value);
    }

    // Validity checks
    public bool HasValidImu => ImuHeading != 99999;

    /// <summary>
    /// Resets all sensor values to their defaults
    /// </summary>
    public void Reset()
    {
        ImuHeading = 99999;
        ImuRoll = 0;
        ImuPitch = 0;
        ImuYawRate = 0;
        AngularVelocity = 0;
    }
}

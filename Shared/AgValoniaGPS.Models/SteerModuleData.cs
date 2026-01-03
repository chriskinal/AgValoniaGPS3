namespace AgValoniaGPS.Models;

/// <summary>
/// Parsed data from PGN 253 (Steer Data FROM Module).
/// Immutable record containing actual steering sensor readings.
/// </summary>
/// <param name="ActualSteerAngle">Actual wheel angle in degrees (from WAS sensor)</param>
/// <param name="ImuHeading">Heading from IMU in degrees (0-360)</param>
/// <param name="ImuRoll">Roll angle from IMU in degrees (-127 to 127)</param>
/// <param name="SteerSwitchActive">Steer switch is engaged</param>
/// <param name="WorkSwitchActive">Work switch is engaged</param>
/// <param name="RemoteButtonPressed">Remote steer button is pressed</param>
/// <param name="PwmDisplay">Current PWM output (0-255)</param>
public readonly record struct SteerModuleData(
    double ActualSteerAngle,
    double ImuHeading,
    sbyte ImuRoll,
    bool SteerSwitchActive,
    bool WorkSwitchActive,
    bool RemoteButtonPressed,
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

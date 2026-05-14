// AgValoniaGPS
// Copyright (C) 2024-2026 AgValoniaGPS Contributors
//
// Licensed under GNU GPL v3. See LICENSE.md.

using AgValoniaGPS.Models;
using AgValoniaGPS.Services.AutoSteer;

namespace AgValoniaGPS.Services.Tests;

/// <summary>
/// Pins the free-drive PGN 254 status byte so receivers — both the
/// real firmware autosteer task and the simulator's
/// <c>VirtualSteerModule</c> — engage their PID loops on the wizard's
/// motor-calibration ramp commands.
///
/// Pre-fix bug: <see cref="PgnBuilder.BuildAutoSteerPgn"/> set the
/// status byte to <c>0x01</c> (SteerSwitchActive) when
/// <see cref="VehicleState.IsInFreeDriveMode"/> was true. The
/// receiver's engagement gate, however, checks bit <c>0x04</c>
/// (IsAutoSteerEngaged) — see <c>PgnProtocol.ParseAutoSteerCommand</c>
/// in the simulator's PgnProtocol.cs. The PID therefore stayed in
/// the "not engaged → pwm = 0" branch and the wheels never moved.
/// </summary>
[TestFixture]
public class FreeDrivePgnEngagementTests
{
    /// <summary>
    /// Build the wire-level PGN 254 for a free-drive command at the
    /// given angle. Mirrors what <c>SendPgnsForControlTick</c> would
    /// emit while the wizard's motor ramp is running.
    /// </summary>
    private static byte[] BuildFreeDrivePgn(double angleDeg)
    {
        var state = new VehicleState
        {
            IsInFreeDriveMode = true,
            FreeDriveSteerAngle = angleDeg,
        };
        return (byte[])PgnBuilder.BuildAutoSteerPgn(ref state).Clone();
    }

    [Test]
    public void FreeDrivePgn254_StatusByte_HasAutoSteerEngagedBit()
    {
        // 0x04 is the bit the receiver's parser inspects for "is engaged".
        var packet = BuildFreeDrivePgn(angleDeg: 5.0);
        byte status = packet[7];

        Assert.That(status & 0x04, Is.Not.Zero,
            "Free-drive PGN 254 must set the IsAutoSteerEngaged bit (0x04) " +
            "so the firmware/simulator PID engages and drives toward the " +
            "commanded angle. Without it the wizard's motor ramp is a no-op.");
    }

    [Test]
    public void FreeDrivePgn254_AngleEncoding_IsLittleEndianX100()
    {
        // Sanity check: the angle goes out in the same scale the
        // simulator's parser expects (signed int16, *100). 5.0° -> 500.
        var packet = BuildFreeDrivePgn(angleDeg: 5.0);
        short angleRaw = (short)(packet[8] | (packet[9] << 8));

        Assert.That(angleRaw, Is.EqualTo(500));
    }

    [Test]
    public void FreeDrivePgn254_SpeedField_IsEightKmhFakeValue()
    {
        // The firmware refuses to drive the motor below MinSpeed (default
        // 1 km/h) to keep stationary turning from burning out the motor.
        // Free-drive bypasses that by reporting a constant fake speed —
        // verify it stays where the receiver expects (~8 km/h x10 = 80).
        var packet = BuildFreeDrivePgn(angleDeg: 0);
        ushort speedRaw = (ushort)(packet[5] | (packet[6] << 8));

        Assert.That(speedRaw, Is.EqualTo(80));
    }
}

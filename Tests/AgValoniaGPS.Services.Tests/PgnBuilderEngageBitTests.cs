using AgValoniaGPS.Models;
using AgValoniaGPS.Services.AutoSteer;
using NUnit.Framework;

namespace AgValoniaGPS.Services.Tests;

/// <summary>
/// Fences PGN 254 status byte bit 2 (AutoSteerEngaged) so the wire-level
/// engage signal can never silently regress again. The companion VM-level
/// wiring tests (UI.Tests) ensure VehicleState.IsAutoSteerEngaged actually
/// gets set when the user toggles autosteer; this test ensures that, once
/// set, the bit ends up on the outbound packet.
/// </summary>
[TestFixture]
public class PgnBuilderEngageBitTests
{
    private const int STATUS_BYTE_INDEX = 7;
    private const byte ENGAGED_BIT = 0x04;

    [Test]
    public void BuildAutoSteerPgn_WhenEngaged_SetsStatusBit2()
    {
        var state = new VehicleState
        {
            IsAutoSteerEngaged = true,
            GpsValid = true,
            SteerSwitchActive = true
        };

        var packet = PgnBuilder.BuildAutoSteerPgn(ref state);

        Assert.That(packet[STATUS_BYTE_INDEX] & ENGAGED_BIT, Is.EqualTo(ENGAGED_BIT),
            "Status bit 2 (engaged) must be set when state.IsAutoSteerEngaged is true.");
    }

    [Test]
    public void BuildAutoSteerPgn_WhenNotEngaged_ClearsStatusBit2()
    {
        var state = new VehicleState
        {
            IsAutoSteerEngaged = false,
            GpsValid = true,
            SteerSwitchActive = true
        };

        var packet = PgnBuilder.BuildAutoSteerPgn(ref state);

        Assert.That(packet[STATUS_BYTE_INDEX] & ENGAGED_BIT, Is.EqualTo(0),
            "Status bit 2 (engaged) must be clear when state.IsAutoSteerEngaged is false.");
    }
}

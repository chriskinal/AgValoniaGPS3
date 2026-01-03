using System.Diagnostics;
using System.Runtime.InteropServices;

namespace AgValoniaGPS.Models;

/// <summary>
/// Mutable vehicle state - single instance, updated in place.
/// All hot-path data in one cache-friendly location.
///
/// This struct is the core data structure for the zero-copy AutoSteer pipeline.
/// The parser writes GPS data here, guidance writes output here, and PGN builder reads from here.
/// No allocations occur in the hot path.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct VehicleState
{
    // ═══════════════════════════════════════════════════════════════════════
    // GPS Data (updated by NMEA parser)
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Latitude in decimal degrees (WGS84)</summary>
    public double Latitude;

    /// <summary>Longitude in decimal degrees (WGS84)</summary>
    public double Longitude;

    /// <summary>Altitude in meters above sea level</summary>
    public double Altitude;

    /// <summary>Speed in meters per second</summary>
    public double Speed;

    /// <summary>Heading in degrees (0-360, true north)</summary>
    public double Heading;

    /// <summary>GPS fix quality (0=invalid, 1=GPS, 2=DGPS, 4=RTK fixed, 5=RTK float)</summary>
    public int FixQuality;

    /// <summary>Number of satellites in use</summary>
    public int Satellites;

    /// <summary>Horizontal dilution of precision</summary>
    public double Hdop;

    /// <summary>Age of differential corrections in seconds</summary>
    public double DifferentialAge;

    // ═══════════════════════════════════════════════════════════════════════
    // IMU Data (updated by NMEA parser if available)
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Roll angle in degrees (positive = right side down)</summary>
    public double Roll;

    /// <summary>Pitch angle in degrees (positive = nose up)</summary>
    public double Pitch;

    /// <summary>Yaw rate in degrees per second</summary>
    public double YawRate;

    // ═══════════════════════════════════════════════════════════════════════
    // Local Coordinates (updated after GPS parse, using LocalPlane)
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Easting in meters from local origin</summary>
    public double Easting;

    /// <summary>Northing in meters from local origin</summary>
    public double Northing;

    /// <summary>Heading in radians (for internal calculations)</summary>
    public double HeadingRadians;

    // ═══════════════════════════════════════════════════════════════════════
    // Guidance Output (updated by guidance calculation)
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Cross-track error in meters (positive = right of line)</summary>
    public double CrossTrackError;

    /// <summary>Calculated steer angle in degrees (positive = steer right)</summary>
    public double SteerAngle;

    /// <summary>Distance to next turn in meters</summary>
    public double DistanceToTurn;

    /// <summary>Distance to end of current track segment in meters</summary>
    public double DistanceToEnd;

    /// <summary>Whether vehicle is currently on a guidance track</summary>
    public bool IsOnTrack;

    /// <summary>Whether auto-steer is currently engaged</summary>
    public bool IsAutoSteerEngaged;

    // ═══════════════════════════════════════════════════════════════════════
    // Section Control (updated by section control logic)
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Section states as a bitmask (bit 0 = section 1, etc.)</summary>
    public ushort SectionStates;

    /// <summary>Master section switch state</summary>
    public bool MasterSectionOn;

    // ═══════════════════════════════════════════════════════════════════════
    // Switch States (received from hardware via PGN)
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Work switch state from hardware</summary>
    public bool WorkSwitchActive;

    /// <summary>Steer switch state from hardware</summary>
    public bool SteerSwitchActive;

    // ═══════════════════════════════════════════════════════════════════════
    // Free Drive Mode (for config panel testing)
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>When true, use FreeDriveSteerAngle instead of guidance SteerAngle</summary>
    public bool IsInFreeDriveMode;

    /// <summary>Manual steer angle for free drive testing (-40 to +40 degrees)</summary>
    public double FreeDriveSteerAngle;

    // ═══════════════════════════════════════════════════════════════════════
    // Validity Flags
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>GPS data is valid (recent and good fix)</summary>
    public bool GpsValid;

    /// <summary>Guidance has been calculated for current position</summary>
    public bool GuidanceValid;

    /// <summary>IMU data is valid (recent)</summary>
    public bool ImuValid;

    // ═══════════════════════════════════════════════════════════════════════
    // Timing (for latency measurement)
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Stopwatch timestamp when GPS data was received</summary>
    public long GpsReceivedTicks;

    /// <summary>Stopwatch timestamp when parsing started</summary>
    public long ParseStartTicks;

    /// <summary>Stopwatch timestamp when parsing completed</summary>
    public long ParseEndTicks;

    /// <summary>Stopwatch timestamp when guidance calculation completed</summary>
    public long GuidanceEndTicks;

    /// <summary>Stopwatch timestamp when PGN was sent</summary>
    public long PgnSentTicks;

    // ═══════════════════════════════════════════════════════════════════════
    // Computed Properties (no storage, just calculations)
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Speed in km/h</summary>
    public readonly double SpeedKmh => Speed * 3.6;

    /// <summary>Speed in mph</summary>
    public readonly double SpeedMph => Speed * 2.23694;

    /// <summary>Whether GPS fix is RTK quality (fixed or float)</summary>
    public readonly bool IsRtkFix => FixQuality >= 4;

    /// <summary>Total latency from GPS receive to PGN send in milliseconds</summary>
    public readonly double TotalLatencyMs
    {
        get
        {
            if (PgnSentTicks == 0 || GpsReceivedTicks == 0) return 0;
            return (PgnSentTicks - GpsReceivedTicks) * 1000.0 / Stopwatch.Frequency;
        }
    }

    /// <summary>Parse latency in milliseconds</summary>
    public readonly double ParseLatencyMs
    {
        get
        {
            if (ParseEndTicks == 0 || ParseStartTicks == 0) return 0;
            return (ParseEndTicks - ParseStartTicks) * 1000.0 / Stopwatch.Frequency;
        }
    }

    /// <summary>Guidance calculation latency in milliseconds</summary>
    public readonly double GuidanceLatencyMs
    {
        get
        {
            if (GuidanceEndTicks == 0 || ParseEndTicks == 0) return 0;
            return (GuidanceEndTicks - ParseEndTicks) * 1000.0 / Stopwatch.Frequency;
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Methods
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Reset all validity flags and timing. Called at start of new GPS cycle.
    /// </summary>
    public void BeginNewCycle()
    {
        GpsValid = false;
        GuidanceValid = false;
        GpsReceivedTicks = Stopwatch.GetTimestamp();
        ParseStartTicks = 0;
        ParseEndTicks = 0;
        GuidanceEndTicks = 0;
        PgnSentTicks = 0;
    }

    /// <summary>
    /// Mark parse as started.
    /// </summary>
    public void MarkParseStart()
    {
        ParseStartTicks = Stopwatch.GetTimestamp();
    }

    /// <summary>
    /// Mark parse as completed.
    /// </summary>
    public void MarkParseEnd()
    {
        ParseEndTicks = Stopwatch.GetTimestamp();
        GpsValid = true;
    }

    /// <summary>
    /// Mark guidance calculation as completed.
    /// </summary>
    public void MarkGuidanceEnd()
    {
        GuidanceEndTicks = Stopwatch.GetTimestamp();
        GuidanceValid = true;
    }

    /// <summary>
    /// Mark PGN as sent.
    /// </summary>
    public void MarkPgnSent()
    {
        PgnSentTicks = Stopwatch.GetTimestamp();
    }

    public override readonly string ToString()
    {
        return $"VehicleState: ({Latitude:F7}, {Longitude:F7}) " +
               $"Fix={FixQuality} Sats={Satellites} " +
               $"Speed={SpeedKmh:F1}km/h Heading={Heading:F1}° " +
               $"XTE={CrossTrackError:F2}m Steer={SteerAngle:F1}° " +
               $"Latency={TotalLatencyMs:F2}ms";
    }
}

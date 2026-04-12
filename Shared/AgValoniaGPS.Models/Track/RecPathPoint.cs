// AgValoniaGPS
// Copyright (C) 2024-2025 AgValoniaGPS Contributors
//
// Licensed under GNU GPL v3. See LICENSE.md.

namespace AgValoniaGPS.Models.Track;

/// <summary>
/// A single recorded path point with speed and section control state.
/// Matches the legacy AgOpenGPS CRecPathPt format.
/// </summary>
public struct RecPathPoint
{
    public double Easting;
    public double Northing;
    public double Heading;
    public double Speed;       // km/h at time of recording
    public bool AutoBtnState;  // section master auto state at time of recording

    public RecPathPoint(double easting, double northing, double heading, double speed, bool autoBtnState)
    {
        Easting = easting;
        Northing = northing;
        Heading = heading;
        Speed = speed;
        AutoBtnState = autoBtnState;
    }
}

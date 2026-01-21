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
/// Pin function assignments for machine relay control.
/// </summary>
public enum PinFunction
{
    None = 0,
    Section1 = 1,
    Section2 = 2,
    Section3 = 3,
    Section4 = 4,
    Section5 = 5,
    Section6 = 6,
    Section7 = 7,
    Section8 = 8,
    Section9 = 9,
    Section10 = 10,
    Section11 = 11,
    Section12 = 12,
    Section13 = 13,
    Section14 = 14,
    Section15 = 15,
    Section16 = 16,
    HydUp = 17,
    HydDown = 18,
    TramLeft = 19,
    TramRight = 20,
    GeoStop = 21
}

/// <summary>
/// Machine module and relay configuration.
/// Controls hydraulic lift timing and pin assignments.
/// </summary>
public class MachineConfig : ReactiveObject
{
    // Hydraulic Lift Settings
    private bool _hydraulicLiftEnabled;
    public bool HydraulicLiftEnabled
    {
        get => _hydraulicLiftEnabled;
        set => this.RaiseAndSetIfChanged(ref _hydraulicLiftEnabled, value);
    }

    private int _raiseTime = 4;
    public int RaiseTime
    {
        get => _raiseTime;
        set => this.RaiseAndSetIfChanged(ref _raiseTime, value);
    }

    private double _lookAhead = 2.0;
    public double LookAhead
    {
        get => _lookAhead;
        set => this.RaiseAndSetIfChanged(ref _lookAhead, value);
    }

    private int _lowerTime = 2;
    public int LowerTime
    {
        get => _lowerTime;
        set => this.RaiseAndSetIfChanged(ref _lowerTime, value);
    }

    private bool _invertRelay;
    public bool InvertRelay
    {
        get => _invertRelay;
        set => this.RaiseAndSetIfChanged(ref _invertRelay, value);
    }

    // User Custom Values (sent to machine module)
    private int _user1Value = 1;
    public int User1Value
    {
        get => _user1Value;
        set => this.RaiseAndSetIfChanged(ref _user1Value, value);
    }

    private int _user2Value = 2;
    public int User2Value
    {
        get => _user2Value;
        set => this.RaiseAndSetIfChanged(ref _user2Value, value);
    }

    private int _user3Value = 3;
    public int User3Value
    {
        get => _user3Value;
        set => this.RaiseAndSetIfChanged(ref _user3Value, value);
    }

    private int _user4Value = 4;
    public int User4Value
    {
        get => _user4Value;
        set => this.RaiseAndSetIfChanged(ref _user4Value, value);
    }

    // Pin Assignments (24 pins)
    // Default: Pins 1-6 = Section 1-6, rest = None
    private PinFunction[] _pinAssignments = new PinFunction[24]
    {
        PinFunction.Section1, PinFunction.Section2, PinFunction.Section3,
        PinFunction.Section4, PinFunction.Section5, PinFunction.Section6,
        PinFunction.None, PinFunction.None, PinFunction.None, PinFunction.None,
        PinFunction.None, PinFunction.None, PinFunction.None, PinFunction.None,
        PinFunction.None, PinFunction.None, PinFunction.None, PinFunction.None,
        PinFunction.None, PinFunction.None, PinFunction.None, PinFunction.None,
        PinFunction.None, PinFunction.None
    };

    public PinFunction[] PinAssignments
    {
        get => _pinAssignments;
        set => this.RaiseAndSetIfChanged(ref _pinAssignments, value);
    }

    /// <summary>
    /// Get or set a specific pin assignment.
    /// </summary>
    public PinFunction GetPinAssignment(int pinIndex)
    {
        if (pinIndex < 0 || pinIndex >= 24) return PinFunction.None;
        return _pinAssignments[pinIndex];
    }

    public void SetPinAssignment(int pinIndex, PinFunction function)
    {
        if (pinIndex < 0 || pinIndex >= 24) return;
        var newAssignments = (PinFunction[])_pinAssignments.Clone();
        newAssignments[pinIndex] = function;
        PinAssignments = newAssignments;
    }

    /// <summary>
    /// Reset all pins to default (Pins 1-6 = Section 1-6, rest = None).
    /// </summary>
    public void ResetPinAssignments()
    {
        PinAssignments = new PinFunction[24]
        {
            PinFunction.Section1, PinFunction.Section2, PinFunction.Section3,
            PinFunction.Section4, PinFunction.Section5, PinFunction.Section6,
            PinFunction.None, PinFunction.None, PinFunction.None, PinFunction.None,
            PinFunction.None, PinFunction.None, PinFunction.None, PinFunction.None,
            PinFunction.None, PinFunction.None, PinFunction.None, PinFunction.None,
            PinFunction.None, PinFunction.None, PinFunction.None, PinFunction.None,
            PinFunction.None, PinFunction.None
        };
    }
}

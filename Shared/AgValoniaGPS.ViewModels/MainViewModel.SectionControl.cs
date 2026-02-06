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
using ReactiveUI;
using AgValoniaGPS.Services.Interfaces;

namespace AgValoniaGPS.ViewModels;

/// <summary>
/// MainViewModel partial class containing Section Control state and event handling.
/// Manages section on/off states, color codes, and synchronization with ISectionControlService.
/// </summary>
public partial class MainViewModel
{
    #region Section State Fields

    // Section states (backing fields)
    private bool _section1Active;
    private bool _section2Active;
    private bool _section3Active;
    private bool _section4Active;
    private bool _section5Active;
    private bool _section6Active;
    private bool _section7Active;
    private bool _section8Active;
    private bool _section9Active;
    private bool _section10Active;
    private bool _section11Active;
    private bool _section12Active;
    private bool _section13Active;
    private bool _section14Active;
    private bool _section15Active;
    private bool _section16Active;

    // Section color codes (0=Red/Off, 1=Yellow/ManualOn, 2=Green/AutoOn, 3=Gray/AutoOff)
    private int _section1ColorCode;
    private int _section2ColorCode;
    private int _section3ColorCode;
    private int _section4ColorCode;
    private int _section5ColorCode;
    private int _section6ColorCode;
    private int _section7ColorCode;
    private int _section8ColorCode;
    private int _section9ColorCode;
    private int _section10ColorCode;
    private int _section11ColorCode;
    private int _section12ColorCode;
    private int _section13ColorCode;
    private int _section14ColorCode;
    private int _section15ColorCode;
    private int _section16ColorCode;

    // Cached section count for binding
    private int _numSections;

    #endregion

    #region Section Active Properties

    public bool Section1Active
    {
        get => _section1Active;
        set => this.RaiseAndSetIfChanged(ref _section1Active, value);
    }

    public bool Section2Active
    {
        get => _section2Active;
        set => this.RaiseAndSetIfChanged(ref _section2Active, value);
    }

    public bool Section3Active
    {
        get => _section3Active;
        set => this.RaiseAndSetIfChanged(ref _section3Active, value);
    }

    public bool Section4Active
    {
        get => _section4Active;
        set => this.RaiseAndSetIfChanged(ref _section4Active, value);
    }

    public bool Section5Active
    {
        get => _section5Active;
        set => this.RaiseAndSetIfChanged(ref _section5Active, value);
    }

    public bool Section6Active
    {
        get => _section6Active;
        set => this.RaiseAndSetIfChanged(ref _section6Active, value);
    }

    public bool Section7Active
    {
        get => _section7Active;
        set => this.RaiseAndSetIfChanged(ref _section7Active, value);
    }

    public bool Section8Active
    {
        get => _section8Active;
        set => this.RaiseAndSetIfChanged(ref _section8Active, value);
    }

    public bool Section9Active
    {
        get => _section9Active;
        set => this.RaiseAndSetIfChanged(ref _section9Active, value);
    }

    public bool Section10Active
    {
        get => _section10Active;
        set => this.RaiseAndSetIfChanged(ref _section10Active, value);
    }

    public bool Section11Active
    {
        get => _section11Active;
        set => this.RaiseAndSetIfChanged(ref _section11Active, value);
    }

    public bool Section12Active
    {
        get => _section12Active;
        set => this.RaiseAndSetIfChanged(ref _section12Active, value);
    }

    public bool Section13Active
    {
        get => _section13Active;
        set => this.RaiseAndSetIfChanged(ref _section13Active, value);
    }

    public bool Section14Active
    {
        get => _section14Active;
        set => this.RaiseAndSetIfChanged(ref _section14Active, value);
    }

    public bool Section15Active
    {
        get => _section15Active;
        set => this.RaiseAndSetIfChanged(ref _section15Active, value);
    }

    public bool Section16Active
    {
        get => _section16Active;
        set => this.RaiseAndSetIfChanged(ref _section16Active, value);
    }

    #endregion

    #region Section Color Code Properties

    // Section color codes for panel buttons (0=Red/Off, 1=Yellow/ManualOn, 2=Green/AutoOn, 3=Gray/AutoOff)
    public int Section1ColorCode
    {
        get => _section1ColorCode;
        set => this.RaiseAndSetIfChanged(ref _section1ColorCode, value);
    }

    public int Section2ColorCode
    {
        get => _section2ColorCode;
        set => this.RaiseAndSetIfChanged(ref _section2ColorCode, value);
    }

    public int Section3ColorCode
    {
        get => _section3ColorCode;
        set => this.RaiseAndSetIfChanged(ref _section3ColorCode, value);
    }

    public int Section4ColorCode
    {
        get => _section4ColorCode;
        set => this.RaiseAndSetIfChanged(ref _section4ColorCode, value);
    }

    public int Section5ColorCode
    {
        get => _section5ColorCode;
        set => this.RaiseAndSetIfChanged(ref _section5ColorCode, value);
    }

    public int Section6ColorCode
    {
        get => _section6ColorCode;
        set => this.RaiseAndSetIfChanged(ref _section6ColorCode, value);
    }

    public int Section7ColorCode
    {
        get => _section7ColorCode;
        set => this.RaiseAndSetIfChanged(ref _section7ColorCode, value);
    }

    public int Section8ColorCode
    {
        get => _section8ColorCode;
        set => this.RaiseAndSetIfChanged(ref _section8ColorCode, value);
    }

    public int Section9ColorCode
    {
        get => _section9ColorCode;
        set => this.RaiseAndSetIfChanged(ref _section9ColorCode, value);
    }

    public int Section10ColorCode
    {
        get => _section10ColorCode;
        set => this.RaiseAndSetIfChanged(ref _section10ColorCode, value);
    }

    public int Section11ColorCode
    {
        get => _section11ColorCode;
        set => this.RaiseAndSetIfChanged(ref _section11ColorCode, value);
    }

    public int Section12ColorCode
    {
        get => _section12ColorCode;
        set => this.RaiseAndSetIfChanged(ref _section12ColorCode, value);
    }

    public int Section13ColorCode
    {
        get => _section13ColorCode;
        set => this.RaiseAndSetIfChanged(ref _section13ColorCode, value);
    }

    public int Section14ColorCode
    {
        get => _section14ColorCode;
        set => this.RaiseAndSetIfChanged(ref _section14ColorCode, value);
    }

    public int Section15ColorCode
    {
        get => _section15ColorCode;
        set => this.RaiseAndSetIfChanged(ref _section15ColorCode, value);
    }

    public int Section16ColorCode
    {
        get => _section16ColorCode;
        set => this.RaiseAndSetIfChanged(ref _section16ColorCode, value);
    }

    #endregion

    #region Section Helper Methods

    /// <summary>
    /// Get section on/off states for map rendering.
    /// </summary>
    public bool[] GetSectionStates()
    {
        var states = _sectionControlService.SectionStates;
        var result = new bool[16];
        for (int i = 0; i < Math.Min(states.Count, 16); i++)
        {
            result[i] = states[i].IsOn;
        }
        return result;
    }

    /// <summary>
    /// Get section button states (Off=0, Auto=1, On=2) for 3-state rendering.
    /// </summary>
    public int[] GetSectionButtonStates()
    {
        var states = _sectionControlService.SectionStates;
        var result = new int[16];
        for (int i = 0; i < Math.Min(states.Count, 16); i++)
        {
            result[i] = (int)states[i].ButtonState; // Off=0, Auto=1, On=2
        }
        return result;
    }

    /// <summary>
    /// Get section widths in meters for map rendering.
    /// </summary>
    public double[] GetSectionWidths()
    {
        var config = Models.Configuration.ConfigurationStore.Instance;
        var result = new double[16];
        for (int i = 0; i < 16; i++)
        {
            result[i] = config.Tool.GetSectionWidth(i) / 100.0; // cm to meters
        }
        return result;
    }

    /// <summary>
    /// Number of configured sections for map rendering.
    /// </summary>
    public int NumSections
    {
        get => _numSections;
        private set => this.RaiseAndSetIfChanged(ref _numSections, value);
    }

    #endregion

    #region Section Event Handlers

    private void OnSectionStateChanged(object? sender, SectionStateChangedEventArgs e)
    {
        // Marshal to UI thread
        if (Avalonia.Threading.Dispatcher.UIThread.CheckAccess())
        {
            UpdateSectionActiveProperties();
        }
        else
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(UpdateSectionActiveProperties);
        }
    }

    private void UpdateSectionActiveProperties()
    {
        // Sync Section*Active properties with service state
        var states = _sectionControlService.SectionStates;
        Section1Active = states.Count > 0 && states[0].IsOn;
        Section2Active = states.Count > 1 && states[1].IsOn;
        Section3Active = states.Count > 2 && states[2].IsOn;
        Section4Active = states.Count > 3 && states[3].IsOn;
        Section5Active = states.Count > 4 && states[4].IsOn;
        Section6Active = states.Count > 5 && states[5].IsOn;
        Section7Active = states.Count > 6 && states[6].IsOn;
        Section8Active = states.Count > 7 && states[7].IsOn;
        Section9Active = states.Count > 8 && states[8].IsOn;
        Section10Active = states.Count > 9 && states[9].IsOn;
        Section11Active = states.Count > 10 && states[10].IsOn;
        Section12Active = states.Count > 11 && states[11].IsOn;
        Section13Active = states.Count > 12 && states[12].IsOn;
        Section14Active = states.Count > 13 && states[13].IsOn;
        Section15Active = states.Count > 14 && states[14].IsOn;
        Section16Active = states.Count > 15 && states[15].IsOn;

        // Update color codes for panel buttons (matching map rendering colors)
        // 0=Red (Off), 1=Yellow (Manual On), 2=Green (Auto On), 3=Gray (Auto Off)
        Section1ColorCode = states.Count > 0 ? GetSectionColorCode(states[0]) : 0;
        Section2ColorCode = states.Count > 1 ? GetSectionColorCode(states[1]) : 0;
        Section3ColorCode = states.Count > 2 ? GetSectionColorCode(states[2]) : 0;
        Section4ColorCode = states.Count > 3 ? GetSectionColorCode(states[3]) : 0;
        Section5ColorCode = states.Count > 4 ? GetSectionColorCode(states[4]) : 0;
        Section6ColorCode = states.Count > 5 ? GetSectionColorCode(states[5]) : 0;
        Section7ColorCode = states.Count > 6 ? GetSectionColorCode(states[6]) : 0;
        Section8ColorCode = states.Count > 7 ? GetSectionColorCode(states[7]) : 0;
        Section9ColorCode = states.Count > 8 ? GetSectionColorCode(states[8]) : 0;
        Section10ColorCode = states.Count > 9 ? GetSectionColorCode(states[9]) : 0;
        Section11ColorCode = states.Count > 10 ? GetSectionColorCode(states[10]) : 0;
        Section12ColorCode = states.Count > 11 ? GetSectionColorCode(states[11]) : 0;
        Section13ColorCode = states.Count > 12 ? GetSectionColorCode(states[12]) : 0;
        Section14ColorCode = states.Count > 13 ? GetSectionColorCode(states[13]) : 0;
        Section15ColorCode = states.Count > 14 ? GetSectionColorCode(states[14]) : 0;
        Section16ColorCode = states.Count > 15 ? GetSectionColorCode(states[15]) : 0;
    }

    /// <summary>
    /// Calculate color code for a section state (matches map rendering logic).
    /// </summary>
    private static int GetSectionColorCode(SectionControlState state)
    {
        // 3-state model: Off=Red, Auto=Green, On=Yellow
        return state.ButtonState switch
        {
            SectionButtonState.Off => 0,  // Red - manually off
            SectionButtonState.On => 1,   // Yellow - manually on
            SectionButtonState.Auto => 2, // Green - automatic mode
            _ => 0
        };
    }

    #endregion
}

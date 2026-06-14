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
using System.Collections.Generic;
using System.Collections.ObjectModel;

using AgValoniaGPS.Models.Configuration;
using AgValoniaGPS.Models.State;
using AgValoniaGPS.Services.Interfaces;

namespace AgValoniaGPS.ViewModels;

/// <summary>
/// MainViewModel partial class containing Section Control state and event handling.
/// Manages the section control bar (one button per configured section, laid out
/// into rows) and synchronization with ISectionControlService.
/// </summary>
public partial class MainViewModel
{
    #region Section Bar State

    // Stable button objects keyed by section index. Reused across row rebuilds
    // so only ColorCode changes as sections cycle Off → Auto → On.
    private readonly List<SectionButtonViewModel> _sectionButtons = new();

    // Cached section count for binding/layout.
    private int _numSections;

    /// <summary>
    /// Section buttons laid out into rows. One row for up to 16 sections; above
    /// 16 the sections split into evenly-sized rows (ceil(N/16), max 4 rows,
    /// max 16 buttons per row). When N doesn't divide evenly, earlier rows take
    /// the extra so the top row holds the greater count.
    /// </summary>
    public ObservableCollection<SectionRowViewModel> SectionRows { get; } = new();

    /// <summary>
    /// Number of configured sections (map rendering + bar layout).
    /// </summary>
    public int NumSections
    {
        get => _numSections;
        private set
        {
            if (SetProperty(ref _numSections, value))
            {
                RebuildSectionRows();
                RefreshSectionButtonColors();
                // The renderer picks up the new section count/layout on the
                // next GPS cycle via ApplyGpsCycleResult (matching prior
                // behavior); no direct push here, which can run before the
                // map control is registered.
            }
        }
    }

    /// <summary>
    /// The section control bar is shown only when a field is open AND at least
    /// one master is engaged (Auto or Manual). With both masters off the bar is
    /// hidden; the master toggles already force every section off in that case.
    /// </summary>
    public bool IsSectionBarVisible => IsFieldOpen && (IsManualSectionMode || IsSectionMasterOn);

    /// <summary>
    /// Build the initial section bar. Called once from the constructor because
    /// the initial NumSections is seeded directly into the backing field.
    /// </summary>
    private void InitializeSectionBar()
    {
        RebuildSectionRows();
        RefreshSectionButtonColors();
    }

    private void RebuildSectionRows()
    {
        int n = Math.Clamp(NumSections, 1, ToolConfig.MaxSections);

        // Ensure a stable button object exists for every visible section.
        while (_sectionButtons.Count < n)
        {
            int index = _sectionButtons.Count;
            _sectionButtons.Add(new SectionButtonViewModel(index, i => ToggleSectionCommand.Execute(i)));
        }

        const int maxPerRow = 16;
        int rows = (n + maxPerRow - 1) / maxPerRow;  // ceil(n / 16), 1..4
        int baseCount = n / rows;
        int remainder = n % rows;  // first `remainder` rows get one extra (top larger)

        SectionRows.Clear();
        int idx = 0;
        for (int r = 0; r < rows; r++)
        {
            int count = baseCount + (r < remainder ? 1 : 0);
            var buttons = new List<SectionButtonViewModel>(count);
            for (int k = 0; k < count; k++)
                buttons.Add(_sectionButtons[idx++]);
            SectionRows.Add(new SectionRowViewModel(buttons));
        }
    }

    #endregion

    #region Section Helper Methods

    /// <summary>
    /// Get section on/off states for map rendering, sized to the configured
    /// section count.
    /// </summary>
    public bool[] GetSectionStates()
    {
        var states = _sectionControlService.SectionStates;
        int n = Math.Min(NumSections, ToolConfig.MaxSections);
        var result = new bool[Math.Max(n, 0)];
        for (int i = 0; i < result.Length && i < states.Count; i++)
            result[i] = states[i].IsOn;
        return result;
    }

    /// <summary>
    /// Get per-section color codes for renderer + control bar. Returns the
    /// pipeline's 6-state palette: 0=Off, 1=Manual On, 2=Auto On,
    /// 3=Turning Off, 4=Turning On, 5=Auto Off.
    /// </summary>
    public int[] GetSectionButtonStates()
    {
        var states = _sectionControlService.SectionStates;
        int n = Math.Min(NumSections, ToolConfig.MaxSections);
        var result = new int[Math.Max(n, 0)];
        for (int i = 0; i < result.Length && i < states.Count; i++)
            result[i] = GetSectionColorCode(states[i]);
        return result;
    }

    /// <summary>
    /// Get section widths in meters for map rendering, sized to the configured
    /// section count.
    /// </summary>
    public double[] GetSectionWidths()
    {
        var config = ConfigurationStore.Instance;
        int n = Math.Min(NumSections, ToolConfig.MaxSections);
        var result = new double[Math.Max(n, 0)];
        for (int i = 0; i < result.Length; i++)
            result[i] = config.Tool.GetSectionWidth(i) / 100.0; // cm to meters
        return result;
    }

    #endregion

    #region Section Event Handlers

    // Tracks aggregate state at the last event so we can detect transitions.
    // We track two notions:
    //   - AnyOn:     any section's IsOn flag is true (driven by manual ON
    //                or by the cycle when an auto section enters work area).
    //   - AnyActive: any section is in a non-Off button state (Auto/On).
    //                Captures the user's master-toggle-to-Auto intent even
    //                though IsOn doesn't flip synchronously on that click.
    // Playing on either transition (IsOn first, AnyActive as fallback) gives
    // exactly one sound per master toggle, manual toggle, or auto headland
    // transition.
    private bool _lastAnyOn;
    private bool _lastAnyActive;

    private void OnSectionStateChanged(object? sender, SectionStateChangedEventArgs e)
    {
        bool currentAnyOn = _sectionControlService.IsAnySectionOn;
        bool currentAnyActive = false;
        var states = _sectionControlService.SectionStates;
        for (int i = 0; i < states.Count; i++)
        {
            if (states[i].ButtonState != SectionButtonState.Off)
            {
                currentAnyActive = true;
                break;
            }
        }

        if (e.SectionIndex >= 0)
        {
            // Per-section toolbar toggle — sound matches the section's new state.
            _audioService.Play(e.IsOn
                ? Services.Interfaces.SoundEffect.SectionOn
                : Services.Interfaces.SoundEffect.SectionOff);
        }
        else if (currentAnyOn != _lastAnyOn)
        {
            // Aggregate IsOn transition — manual ON/OFF master toggle, or
            // auto sections turning on/off as the tool enters/exits the
            // work area.
            _audioService.Play(currentAnyOn
                ? Services.Interfaces.SoundEffect.SectionOn
                : Services.Interfaces.SoundEffect.SectionOff);
        }
        else if (currentAnyActive != _lastAnyActive)
        {
            // Master Auto toggle from Off → Auto doesn't flip IsOn
            // synchronously (the cycle decides), so the IsOn check above
            // misses it. Catch it on the ButtonState-active transition.
            _audioService.Play(currentAnyActive
                ? Services.Interfaces.SoundEffect.SectionOn
                : Services.Interfaces.SoundEffect.SectionOff);
        }
        _lastAnyOn = currentAnyOn;
        _lastAnyActive = currentAnyActive;

        // Marshal to UI thread
        if (_dispatcher.CheckAccess())
        {
            UpdateSectionStates();
        }
        else
        {
            _dispatcher.Post(UpdateSectionStates);
        }
    }

    /// <summary>
    /// Refresh each section button's color code from the service. Safe to call
    /// before the map control is registered (no renderer push).
    /// </summary>
    private void RefreshSectionButtonColors()
    {
        var states = _sectionControlService.SectionStates;
        for (int i = 0; i < _sectionButtons.Count; i++)
            _sectionButtons[i].ColorCode = i < states.Count ? GetSectionColorCode(states[i]) : 0;
    }

    /// <summary>
    /// Refresh button colors and push the layout/state to the renderer. The
    /// push covers non-cycle changes (e.g. a manual toggle while GPS/sim isn't
    /// running); the GPS pipeline also pushes every cycle from
    /// ApplyGpsCycleResult. Called only from the runtime section-changed event,
    /// after the map control has been registered.
    /// </summary>
    private void UpdateSectionStates()
    {
        RefreshSectionButtonColors();

        if (NumSections > 0)
        {
            _mapService.SetSectionStates(
                GetSectionStates(),
                GetSectionWidths(),
                NumSections,
                GetSectionButtonStates());
        }
    }

    /// <summary>
    /// Calculate color code for a section state. Mirrors
    /// GpsPipelineService.GetSectionColorCode so the section control bar,
    /// the map renderer, and the cycle-driven SectionColorCodes array all
    /// agree on the same 6-state palette:
    ///   0 = Off (red)
    ///   1 = Manual ON (yellow)
    ///   2 = Auto ON (green)
    ///   3 = Turning OFF (cyan) — IsOn but off-request pending
    ///   4 = Turning ON (orange) — !IsOn but on-request pending
    ///   5 = Auto OFF (gray)
    /// </summary>
    private static int GetSectionColorCode(SectionControlState state)
    {
        if (state.ButtonState == SectionButtonState.Off)
            return 0;
        if (state.ButtonState == SectionButtonState.On)
            return 1;

        // Auto mode transition states
        if (state.IsOn && state.SectionOffRequest)
            return 3;
        if (!state.IsOn && state.SectionOnRequest)
            return 4;

        // Auto mode steady states
        if (state.IsOn)
            return 2;

        return 5;
    }

    #endregion
}

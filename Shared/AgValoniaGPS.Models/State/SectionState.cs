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

using System.Linq;
using ReactiveUI;

namespace AgValoniaGPS.Models.State;

/// <summary>
/// Section control state - which sections are on, coverage mode.
/// </summary>
public class SectionState : ReactiveObject
{
    public const int MaxSections = 16;

    // Section on/off states
    private readonly bool[] _sectionActive = new bool[MaxSections];

    public bool GetSectionActive(int index) =>
        index >= 0 && index < MaxSections && _sectionActive[index];

    public void SetSectionActive(int index, bool active)
    {
        if (index >= 0 && index < MaxSections && _sectionActive[index] != active)
        {
            _sectionActive[index] = active;
            this.RaisePropertyChanged($"Section{index + 1}Active");
            UpdateActiveSectionCount();
        }
    }

    // Convenience properties for common section counts (XAML binding)
    public bool Section1Active => GetSectionActive(0);
    public bool Section2Active => GetSectionActive(1);
    public bool Section3Active => GetSectionActive(2);
    public bool Section4Active => GetSectionActive(3);
    public bool Section5Active => GetSectionActive(4);
    public bool Section6Active => GetSectionActive(5);
    public bool Section7Active => GetSectionActive(6);
    public bool Section8Active => GetSectionActive(7);

    // Count of active sections
    private int _activeSectionCount;
    public int ActiveSectionCount
    {
        get => _activeSectionCount;
        private set => this.RaiseAndSetIfChanged(ref _activeSectionCount, value);
    }

    private void UpdateActiveSectionCount()
    {
        ActiveSectionCount = _sectionActive.Count(s => s);
    }

    // Total configured sections
    private int _numberOfSections = 1;
    public int NumberOfSections
    {
        get => _numberOfSections;
        set => this.RaiseAndSetIfChanged(ref _numberOfSections, value);
    }

    // Master control
    private bool _isMasterOn;
    public bool IsMasterOn
    {
        get => _isMasterOn;
        set => this.RaiseAndSetIfChanged(ref _isMasterOn, value);
    }

    private bool _isManualMode;
    public bool IsManualMode
    {
        get => _isManualMode;
        set => this.RaiseAndSetIfChanged(ref _isManualMode, value);
    }

    private bool _isAutoMode;
    public bool IsAutoMode
    {
        get => _isAutoMode;
        set => this.RaiseAndSetIfChanged(ref _isAutoMode, value);
    }

    // Headland section control
    private bool _isSectionControlInHeadland;
    public bool IsSectionControlInHeadland
    {
        get => _isSectionControlInHeadland;
        set => this.RaiseAndSetIfChanged(ref _isSectionControlInHeadland, value);
    }

    public void Reset()
    {
        for (int i = 0; i < MaxSections; i++)
            _sectionActive[i] = false;
        ActiveSectionCount = 0;
        IsMasterOn = false;
        IsManualMode = false;
        IsAutoMode = false;
        IsSectionControlInHeadland = false;
    }

    /// <summary>
    /// Set all sections at once (from UDP message bitmask)
    /// </summary>
    public void SetAllSections(ushort sectionBits)
    {
        for (int i = 0; i < MaxSections; i++)
        {
            SetSectionActive(i, (sectionBits & (1 << i)) != 0);
        }
    }

    /// <summary>
    /// Get all sections as a bitmask
    /// </summary>
    public ushort GetAllSectionsAsBits()
    {
        ushort bits = 0;
        for (int i = 0; i < MaxSections; i++)
        {
            if (_sectionActive[i])
                bits |= (ushort)(1 << i);
        }
        return bits;
    }
}

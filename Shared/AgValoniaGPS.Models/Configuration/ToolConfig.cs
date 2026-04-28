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

using CommunityToolkit.Mvvm.ComponentModel;

namespace AgValoniaGPS.Models.Configuration;

/// <summary>
/// Tool/implement configuration.
/// Consolidates tool-related settings from ToolConfiguration.
/// </summary>
public class ToolConfig : ObservableObject
{
    // Tool dimensions
    private double _width = 6.0;
    public double Width
    {
        get => _width;
        set
        {
            SetProperty(ref _width, value);
            OnPropertyChanged(nameof(HalfWidth));
        }
    }

    public double HalfWidth => Width / 2.0;

    private double _overlap;
    public double Overlap
    {
        get => _overlap;
        set => SetProperty(ref _overlap, value);
    }

    private double _offset;
    public double Offset
    {
        get => _offset;
        set => SetProperty(ref _offset, value);
    }

    // Hitch configuration
    private double _hitchLength = 1.8;
    public double HitchLength
    {
        get => _hitchLength;
        set => SetProperty(ref _hitchLength, Math.Max(0, value));
    }

    // Distance from hitch to trailing tool, measured behind the hitch.
    // Convention: positive = tool is behind hitch (the only physically valid direction
    // for a trailing implement). Sign-agnostic at runtime; legacy profiles with
    // negative values are migrated on load.
    private double _trailingHitchLength = 2.5;
    public double TrailingHitchLength
    {
        get => _trailingHitchLength;
        set => SetProperty(ref _trailingHitchLength, value);
    }

    private double _tankTrailingHitchLength = 3.0;
    public double TankTrailingHitchLength
    {
        get => _tankTrailingHitchLength;
        set => SetProperty(ref _tankTrailingHitchLength, value);
    }

    private double _trailingToolToPivotLength;
    public double TrailingToolToPivotLength
    {
        get => _trailingToolToPivotLength;
        set => SetProperty(ref _trailingToolToPivotLength, value);
    }

    // Tool type flags
    private bool _isToolTrailing;
    public bool IsToolTrailing
    {
        get => _isToolTrailing;
        set => SetProperty(ref _isToolTrailing, value);
    }

    private bool _isToolTBT;
    public bool IsToolTBT
    {
        get => _isToolTBT;
        set => SetProperty(ref _isToolTBT, value);
    }

    private bool _isToolRearFixed = true;
    public bool IsToolRearFixed
    {
        get => _isToolRearFixed;
        set => SetProperty(ref _isToolRearFixed, value);
    }

    private bool _isToolFrontFixed;
    public bool IsToolFrontFixed
    {
        get => _isToolFrontFixed;
        set => SetProperty(ref _isToolFrontFixed, value);
    }

    // Section lookahead settings
    private double _lookAheadOnSetting = 1.0;
    public double LookAheadOnSetting
    {
        get => _lookAheadOnSetting;
        set => SetProperty(ref _lookAheadOnSetting, value);
    }

    private double _lookAheadOffSetting = 0.5;
    public double LookAheadOffSetting
    {
        get => _lookAheadOffSetting;
        set => SetProperty(ref _lookAheadOffSetting, value);
    }

    private double _turnOffDelay;
    public double TurnOffDelay
    {
        get => _turnOffDelay;
        set => SetProperty(ref _turnOffDelay, value);
    }

    // Section configuration
    private int _minCoverage;
    public int MinCoverage
    {
        get => _minCoverage;
        set => SetProperty(ref _minCoverage, value);
    }

    private bool _isMultiColoredSections;
    public bool IsMultiColoredSections
    {
        get => _isMultiColoredSections;
        set => SetProperty(ref _isMultiColoredSections, value);
    }

    // Section colors (RGB values stored as 0xRRGGBB)
    // Default colors match AgOpenGPS preset palette
    private uint[] _sectionColors = new uint[16]
    {
        0x00FF00, // Green
        0xFF0000, // Red
        0x0000FF, // Blue
        0xFFFF00, // Yellow
        0xFF00FF, // Magenta
        0x00FFFF, // Cyan
        0xFF8000, // Orange
        0x8000FF, // Purple
        0x80FF00, // Lime
        0xFF0080, // Pink
        0x0080FF, // Sky Blue
        0x80FF80, // Light Green
        0xFF8080, // Light Red
        0x8080FF, // Light Blue
        0xFFFF80, // Light Yellow
        0xFF80FF  // Light Magenta
    };

    /// <summary>
    /// Section colors as RGB values (0xRRGGBB format).
    /// </summary>
    public uint[] SectionColors
    {
        get => _sectionColors;
        set => SetProperty(ref _sectionColors, value);
    }

    /// <summary>
    /// Gets a section color by index.
    /// </summary>
    public uint GetSectionColor(int index)
    {
        if (index < 0 || index >= 16) return _sectionColors[0];
        return _sectionColors[index];
    }

    /// <summary>
    /// Sets a section color by index.
    /// </summary>
    public void SetSectionColor(int index, uint color)
    {
        if (index < 0 || index >= 16) return;
        _sectionColors[index] = color;
        OnPropertyChanged(nameof(SectionColors));
    }

    /// <summary>
    /// Single coverage color used when IsMultiColoredSections is false (0xRRGGBB).
    /// Default is pale green.
    /// </summary>
    private uint _singleCoverageColor = 0x98FB98; // Pale green (152, 251, 152)
    public uint SingleCoverageColor
    {
        get => _singleCoverageColor;
        set => SetProperty(ref _singleCoverageColor, value);
    }

    private bool _isSectionOffWhenOut;
    public bool IsSectionOffWhenOut
    {
        get => _isSectionOffWhenOut;
        set => SetProperty(ref _isSectionOffWhenOut, value);
    }

    /// <summary>
    /// When true, sections automatically turn off when in headland zone.
    /// </summary>
    private bool _isHeadlandSectionControl = true;
    public bool IsHeadlandSectionControl
    {
        get => _isHeadlandSectionControl;
        set => SetProperty(ref _isHeadlandSectionControl, value);
    }

    private bool _isSectionsNotZones = true;
    public bool IsSectionsNotZones
    {
        get => _isSectionsNotZones;
        set => SetProperty(ref _isSectionsNotZones, value);
    }

    private double _defaultSectionWidth = 100; // cm
    public double DefaultSectionWidth
    {
        get => _defaultSectionWidth;
        set => SetProperty(ref _defaultSectionWidth, value);
    }

    // Cutoff is stored and edited in km/h to match the UI label and user
    // expectation. SectionControlService converts to m/s before comparing
    // against the GPS-reported speed.
    private double _slowSpeedCutoff = 0.5;
    public double SlowSpeedCutoff
    {
        get => _slowSpeedCutoff;
        set => SetProperty(ref _slowSpeedCutoff, value);
    }

    /// <summary>
    /// Coverage margin in centimeters. Expands recorded coverage triangles
    /// on each edge to prevent gaps between passes due to GPS drift.
    /// Default 5cm (0.05m) on each side = 10cm total overlap.
    /// </summary>
    private double _coverageMargin = 5.0;
    public double CoverageMargin
    {
        get => _coverageMargin;
        set => SetProperty(ref _coverageMargin, value);
    }

    /// <summary>
    /// Coverage margin in meters (converted from cm).
    /// </summary>
    public double CoverageMarginMeters => _coverageMargin / 100.0;

    // Individual section widths (cm) - up to 16 sections
    private double[] _sectionWidths = new double[16] { 100, 100, 100, 100, 100, 100, 100, 100, 100, 100, 100, 100, 100, 100, 100, 100 };

    /// <summary>
    /// Gets or sets individual section widths in centimeters.
    /// Array of 16 values, one per section.
    /// </summary>
    public double[] SectionWidths
    {
        get => _sectionWidths;
        set => SetProperty(ref _sectionWidths, value);
    }

    /// <summary>
    /// Gets or sets a specific section width by index (0-15).
    /// </summary>
    public double GetSectionWidth(int index)
    {
        if (index < 0 || index >= 16) return DefaultSectionWidth;
        return _sectionWidths[index];
    }

    /// <summary>
    /// Sets a specific section width by index and raises change notification.
    /// </summary>
    public void SetSectionWidth(int index, double value)
    {
        if (index < 0 || index >= 16) return;
        _sectionWidths[index] = value;
        OnPropertyChanged(nameof(SectionWidths));
        OnPropertyChanged(nameof(TotalSectionWidth));
    }

    /// <summary>
    /// Calculates total width of all active sections in meters.
    /// </summary>
    public double TotalSectionWidth
    {
        get
        {
            double total = 0;
            // NumSections is in ConfigurationStore, so we use a simpler approach here
            // This will be calculated properly in the ViewModel
            for (int i = 0; i < 16; i++)
                total += _sectionWidths[i];
            return total / 100.0; // Convert cm to meters
        }
    }

    // Zone configuration
    private int _zones = 2;
    public int Zones
    {
        get => _zones;
        set => SetProperty(ref _zones, value);
    }

    // Zone end sections - which section each zone ends at (up to 8 zones)
    private int[] _zoneRanges = new int[9] { 0, 2, 4, 6, 8, 10, 12, 14, 16 };

    /// <summary>
    /// Gets or sets zone end section indices.
    /// ZoneRanges[0] is always 0 (start), ZoneRanges[i] is the end section of zone i.
    /// </summary>
    public int[] ZoneRanges
    {
        get => _zoneRanges;
        set => SetProperty(ref _zoneRanges, value);
    }

    /// <summary>
    /// Gets the end section for a zone (1-8).
    /// </summary>
    public int GetZoneEndSection(int zoneIndex)
    {
        if (zoneIndex < 1 || zoneIndex > 8) return 0;
        return _zoneRanges[zoneIndex];
    }

    /// <summary>
    /// Sets the end section for a zone (1-8).
    /// </summary>
    public void SetZoneEndSection(int zoneIndex, int endSection)
    {
        if (zoneIndex < 1 || zoneIndex > 8) return;
        _zoneRanges[zoneIndex] = endSection;
        OnPropertyChanged(nameof(ZoneRanges));
    }

    // Switch configuration
    private bool _isWorkSwitchEnabled;
    public bool IsWorkSwitchEnabled
    {
        get => _isWorkSwitchEnabled;
        set => SetProperty(ref _isWorkSwitchEnabled, value);
    }

    private bool _isWorkSwitchActiveLow;
    public bool IsWorkSwitchActiveLow
    {
        get => _isWorkSwitchActiveLow;
        set => SetProperty(ref _isWorkSwitchActiveLow, value);
    }

    private bool _isWorkSwitchManualSections;
    public bool IsWorkSwitchManualSections
    {
        get => _isWorkSwitchManualSections;
        set => SetProperty(ref _isWorkSwitchManualSections, value);
    }

    private bool _isSteerSwitchEnabled;
    public bool IsSteerSwitchEnabled
    {
        get => _isSteerSwitchEnabled;
        set => SetProperty(ref _isSteerSwitchEnabled, value);
    }

    private bool _isSteerSwitchManualSections;
    public bool IsSteerSwitchManualSections
    {
        get => _isSteerSwitchManualSections;
        set => SetProperty(ref _isSteerSwitchManualSections, value);
    }

    /// <summary>
    /// Gets the current tool type as a string for display
    /// </summary>
    public string CurrentToolType
    {
        get
        {
            if (IsToolFrontFixed) return "Front Fixed";
            if (IsToolRearFixed) return "Rear Fixed";
            if (IsToolTBT) return "TBT";
            if (IsToolTrailing) return "Trailing";
            return "None";
        }
    }

    /// <summary>
    /// Sets the tool type, clearing other flags
    /// </summary>
    public void SetToolType(string toolType)
    {
        IsToolTrailing = false;
        IsToolTBT = false;
        IsToolRearFixed = false;
        IsToolFrontFixed = false;

        switch (toolType.ToLowerInvariant())
        {
            case "front":
                IsToolFrontFixed = true;
                break;
            case "rear":
                IsToolRearFixed = true;
                break;
            case "tbt":
                IsToolTBT = true;
                break;
            case "trailing":
                IsToolTrailing = true;
                break;
        }

        OnPropertyChanged(nameof(CurrentToolType));
    }
}

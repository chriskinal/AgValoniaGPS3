using ReactiveUI;

namespace AgValoniaGPS.Models.Configuration;

/// <summary>
/// Tool/implement configuration.
/// Consolidates tool-related settings from ToolConfiguration.
/// </summary>
public class ToolConfig : ReactiveObject
{
    // Tool dimensions
    private double _width = 6.0;
    public double Width
    {
        get => _width;
        set
        {
            this.RaiseAndSetIfChanged(ref _width, value);
            this.RaisePropertyChanged(nameof(HalfWidth));
        }
    }

    public double HalfWidth => Width / 2.0;

    private double _overlap;
    public double Overlap
    {
        get => _overlap;
        set => this.RaiseAndSetIfChanged(ref _overlap, value);
    }

    private double _offset;
    public double Offset
    {
        get => _offset;
        set => this.RaiseAndSetIfChanged(ref _offset, value);
    }

    // Hitch configuration
    private double _hitchLength = -1.8;
    public double HitchLength
    {
        get => _hitchLength;
        set => this.RaiseAndSetIfChanged(ref _hitchLength, value);
    }

    private double _trailingHitchLength = -2.5;
    public double TrailingHitchLength
    {
        get => _trailingHitchLength;
        set => this.RaiseAndSetIfChanged(ref _trailingHitchLength, value);
    }

    private double _tankTrailingHitchLength = 3.0;
    public double TankTrailingHitchLength
    {
        get => _tankTrailingHitchLength;
        set => this.RaiseAndSetIfChanged(ref _tankTrailingHitchLength, value);
    }

    private double _trailingToolToPivotLength;
    public double TrailingToolToPivotLength
    {
        get => _trailingToolToPivotLength;
        set => this.RaiseAndSetIfChanged(ref _trailingToolToPivotLength, value);
    }

    // Tool type flags
    private bool _isToolTrailing;
    public bool IsToolTrailing
    {
        get => _isToolTrailing;
        set => this.RaiseAndSetIfChanged(ref _isToolTrailing, value);
    }

    private bool _isToolTBT;
    public bool IsToolTBT
    {
        get => _isToolTBT;
        set => this.RaiseAndSetIfChanged(ref _isToolTBT, value);
    }

    private bool _isToolRearFixed = true;
    public bool IsToolRearFixed
    {
        get => _isToolRearFixed;
        set => this.RaiseAndSetIfChanged(ref _isToolRearFixed, value);
    }

    private bool _isToolFrontFixed;
    public bool IsToolFrontFixed
    {
        get => _isToolFrontFixed;
        set => this.RaiseAndSetIfChanged(ref _isToolFrontFixed, value);
    }

    // Section lookahead settings
    private double _lookAheadOnSetting = 1.0;
    public double LookAheadOnSetting
    {
        get => _lookAheadOnSetting;
        set => this.RaiseAndSetIfChanged(ref _lookAheadOnSetting, value);
    }

    private double _lookAheadOffSetting = 0.5;
    public double LookAheadOffSetting
    {
        get => _lookAheadOffSetting;
        set => this.RaiseAndSetIfChanged(ref _lookAheadOffSetting, value);
    }

    private double _turnOffDelay;
    public double TurnOffDelay
    {
        get => _turnOffDelay;
        set => this.RaiseAndSetIfChanged(ref _turnOffDelay, value);
    }

    // Section configuration
    private int _minCoverage;
    public int MinCoverage
    {
        get => _minCoverage;
        set => this.RaiseAndSetIfChanged(ref _minCoverage, value);
    }

    private bool _isMultiColoredSections;
    public bool IsMultiColoredSections
    {
        get => _isMultiColoredSections;
        set => this.RaiseAndSetIfChanged(ref _isMultiColoredSections, value);
    }

    private bool _isSectionOffWhenOut;
    public bool IsSectionOffWhenOut
    {
        get => _isSectionOffWhenOut;
        set => this.RaiseAndSetIfChanged(ref _isSectionOffWhenOut, value);
    }

    private bool _isSectionsNotZones = true;
    public bool IsSectionsNotZones
    {
        get => _isSectionsNotZones;
        set => this.RaiseAndSetIfChanged(ref _isSectionsNotZones, value);
    }

    private double _defaultSectionWidth = 100; // cm
    public double DefaultSectionWidth
    {
        get => _defaultSectionWidth;
        set => this.RaiseAndSetIfChanged(ref _defaultSectionWidth, value);
    }

    private double _slowSpeedCutoff = 0.5;
    public double SlowSpeedCutoff
    {
        get => _slowSpeedCutoff;
        set => this.RaiseAndSetIfChanged(ref _slowSpeedCutoff, value);
    }

    // Zone configuration
    private int _zones = 2;
    public int Zones
    {
        get => _zones;
        set => this.RaiseAndSetIfChanged(ref _zones, value);
    }

    // Switch configuration
    private bool _isWorkSwitchEnabled;
    public bool IsWorkSwitchEnabled
    {
        get => _isWorkSwitchEnabled;
        set => this.RaiseAndSetIfChanged(ref _isWorkSwitchEnabled, value);
    }

    private bool _isWorkSwitchActiveLow;
    public bool IsWorkSwitchActiveLow
    {
        get => _isWorkSwitchActiveLow;
        set => this.RaiseAndSetIfChanged(ref _isWorkSwitchActiveLow, value);
    }

    private bool _isWorkSwitchManualSections;
    public bool IsWorkSwitchManualSections
    {
        get => _isWorkSwitchManualSections;
        set => this.RaiseAndSetIfChanged(ref _isWorkSwitchManualSections, value);
    }

    private bool _isSteerSwitchEnabled;
    public bool IsSteerSwitchEnabled
    {
        get => _isSteerSwitchEnabled;
        set => this.RaiseAndSetIfChanged(ref _isSteerSwitchEnabled, value);
    }

    private bool _isSteerSwitchManualSections;
    public bool IsSteerSwitchManualSections
    {
        get => _isSteerSwitchManualSections;
        set => this.RaiseAndSetIfChanged(ref _isSteerSwitchManualSections, value);
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

        this.RaisePropertyChanged(nameof(CurrentToolType));
    }
}
